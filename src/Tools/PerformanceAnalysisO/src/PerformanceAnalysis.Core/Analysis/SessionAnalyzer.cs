using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Models;
using PerformanceAnalysis.Core.Parsing;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Analysis;

public sealed class SessionAnalyzer
{
    private const string UnknownDeviceName = "未識別裝置";

    private readonly SessionMetadataStore _metadataStore;
    private readonly bool _writeDerivedMetadata;
    private readonly AnalysisOptions _options;

    public SessionAnalyzer(
        SessionMetadataStore? metadataStore = null,
        bool writeDerivedMetadata = false,
        AnalysisOptions? options = null)
    {
        _metadataStore = metadataStore ?? new SessionMetadataStore();
        _writeDerivedMetadata = writeDerivedMetadata;
        _options = (options ?? AnalysisOptions.Default).Clone();
        _options.Normalize();
    }

    public Session Analyze(SessionCandidate candidate)
    {
        var folderPath = candidate.Path;
        var folderName = Path.GetFileName(
            folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var diagnostics = new List<Diagnostic>();

        var refPath = Path.Combine(folderPath, "ref.txt");
        var hasRef = File.Exists(refPath);
        RefDocument? refDocument = null;

        if (hasRef)
        {
            refDocument = RefDocument.Parse(refPath);
            diagnostics.AddRange(refDocument.Diagnostics);
        }
        else
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "NO_REF",
                $"{folderName}: 缺少 ref.txt，硬體資訊將由 CSV／資料夾推斷"));
        }

        var systemPath = Path.Combine(folderPath, "System_BenchmarkLog.csv");
        var hasSystem = File.Exists(systemPath);
        var systemResult = hasSystem
            ? SystemLogParser.Parse(systemPath)
            : LogParseResult<SystemSample>.Empty();

        diagnostics.AddRange(systemResult.Diagnostics);
        if (!hasSystem)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "NO_SYSTEM_LOG",
                $"{folderName}: 缺少 System_BenchmarkLog.csv"));
        }

        var taskResults = new List<TaskParseResult>();
        try
        {
            foreach (var file in Directory
                         .EnumerateFiles(folderPath, "*_BenchmarkLog.csv", SearchOption.AllDirectories)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(
                        Path.GetFullPath(file),
                        Path.GetFullPath(systemPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var taskName = GetTaskName(file);
                taskResults.Add(new TaskParseResult(
                    taskName,
                    file,
                    TaskLogParser.Parse(file, taskName)));
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "TASK_SCAN_FAILED",
                $"{folderName}: 掃描任務紀錄失敗 ({ex.Message})"));
        }

        if (taskResults.Count == 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "NO_TASK_LOGS",
                $"{folderName}: 沒有任務紀錄，僅分析系統資源"));
        }

        var allTaskSamples = taskResults
            .SelectMany(result => result.ParseResult.Items)
            .ToList();

        var (start, end) = ResolveTimeRange(systemResult.Items, allTaskSamples, folderName, folderPath, diagnostics);
        var duration = end >= start
            ? end - start
            : TimeSpan.Zero;

        var sessionKey = ComputeSessionKey(systemPath, systemResult.Items, taskResults);
        var metadata = _metadataStore.TryLoad(sessionKey)
                       ?? _metadataStore.TryLoadByFolderName(folderName, start);

        var hardware = metadata?.Hardware ?? refDocument?.GetHardware();
        var metadataDeviceName = metadata?.DeviceOverridden == true
            ? metadata.DeviceName
            : string.Equals(metadata?.DeviceName, UnknownDeviceName, StringComparison.Ordinal)
                ? null
                : metadata?.DeviceName;
        var deviceName = FirstNonEmpty(
            metadataDeviceName,
            refDocument?.DeviceName,
            candidate.DeviceFolderHint,
            UnknownDeviceName)!;
        var device = BuildDeviceProfile(deviceName, metadata, hardware, refDocument, candidate);

        var mode = MostCommon(systemResult.Items.Select(sample => sample.OperationMode));

        var taskLogs = new List<TaskLog>();
        foreach (var taskResult in taskResults)
        {
            var samples = taskResult.ParseResult.Items;
            var runs = TaskRunBuilder.Build(samples);
            var taskAggregate = BuildTaskAggregate(samples, runs, duration);

            taskLogs.Add(new TaskLog
            {
                Name = taskResult.Name,
                FilePath = taskResult.FilePath,
                Samples = samples,
                Runs = runs,
                Aggregate = taskAggregate,
                Diagnostics = taskResult.ParseResult.Diagnostics
            });

            diagnostics.AddRange(taskResult.ParseResult.Diagnostics);

            if (samples.Count > 0 && runs.Count == 0)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "TASK_NEVER_RUN",
                    $"{folderName} / {taskResult.Name}: 全程未進入「執行中」狀態"));
            }

            if (taskAggregate.RestartCount > _options.TaskRestartThreshold)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "TASK_RESTARTS",
                    $"{folderName} / {taskResult.Name}: 重啟 {taskAggregate.RestartCount} 次，超過門檻 {_options.TaskRestartThreshold} 次"));
            }
        }

        taskLogs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        var aggregate = BuildSessionAggregate(systemResult.Items, duration);
        AddSystemQualityDiagnostics(
            diagnostics,
            folderName,
            systemResult.Items,
            aggregate,
            duration,
            taskLogs.Count,
            _options);
        AddRefMismatchDiagnostics(diagnostics, folderName, aggregate, refDocument?.GetSummary());

        if (metadata is null && _writeDerivedMetadata && (systemResult.Items.Count > 0 || taskLogs.Count > 0))
        {
            try
            {
                var derived = DerivedMetadataFactory.Create(
                    sessionKey,
                    folderName,
                    device,
                    start,
                    end,
                    mode,
                    taskLogs.Select(task => task.Name));
                _metadataStore.Save(derived);
                metadata = derived;
            }
            catch (Exception ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "METADATA_WRITE_FAILED",
                    $"{folderName}: 無法寫入中繼資料 ({ex.Message})"));
            }
        }
        else if (metadata is null)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Info,
                "METADATA_MISSING",
                $"{folderName}: 未找到 session.json，已使用 ref.txt／CSV 推斷裝置資訊"));
        }

        return new Session
        {
            SessionKey = sessionKey,
            FolderPath = folderPath,
            FolderName = folderName,
            StartTime = start,
            EndTime = end,
            Duration = duration,
            SystemSamples = systemResult.Items,
            Tasks = taskLogs,
            Device = device,
            Aggregate = aggregate,
            Diagnostics = diagnostics,
            Metadata = metadata,
            HasRefTxt = hasRef,
            HasSystemLog = hasSystem,
            DeviceFolderHint = candidate.DeviceFolderHint
        };
    }

    private static (DateTime Start, DateTime End) ResolveTimeRange(
        IReadOnlyList<SystemSample> systemSamples,
        IReadOnlyList<TaskSample> taskSamples,
        string folderName,
        string folderPath,
        ICollection<Diagnostic> diagnostics)
    {
        if (systemSamples.Count > 0)
        {
            return (systemSamples[0].Timestamp, systemSamples[^1].Timestamp);
        }

        if (taskSamples.Count > 0)
        {
            return (taskSamples.Min(sample => sample.Timestamp), taskSamples.Max(sample => sample.Timestamp));
        }

        diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Error,
            "NO_SAMPLES",
            $"{folderName}: 找不到任何有效樣本"));

        if (DateTime.TryParseExact(
                folderName,
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var folderTime))
        {
            return (folderTime, folderTime);
        }

        try
        {
            var fallback = Directory.GetLastWriteTime(folderPath);
            return (fallback, fallback);
        }
        catch
        {
            return (DateTime.MinValue, DateTime.MinValue);
        }
    }

    private static string ComputeSessionKey(
        string systemPath,
        IReadOnlyList<SystemSample> systemSamples,
        IReadOnlyList<TaskParseResult> taskResults)
    {
        if (systemSamples.Count > 0)
        {
            var length = File.Exists(systemPath) ? new FileInfo(systemPath).Length : 0;
            return SessionKey.Compute(
                systemSamples[0].Timestamp,
                systemSamples[^1].Timestamp,
                systemSamples.Count,
                length);
        }

        var taskSamples = taskResults.SelectMany(result => result.ParseResult.Items).ToList();
        if (taskSamples.Count > 0)
        {
            var length = taskResults
                .Where(result => File.Exists(result.FilePath))
                .Sum(result => new FileInfo(result.FilePath).Length);
            return SessionKey.Compute(
                taskSamples.Min(sample => sample.Timestamp),
                taskSamples.Max(sample => sample.Timestamp),
                taskSamples.Count,
                length);
        }

        return SessionKey.Compute(DateTime.MinValue, DateTime.MinValue, 0, 0);
    }

    private static DeviceProfile BuildDeviceProfile(
        string deviceName,
        SessionMetadata? metadata,
        HardwareInfo? hardware,
        RefDocument? refDocument,
        SessionCandidate candidate)
    {
        var fingerprintParts = new List<string> { deviceName };
        if (!string.IsNullOrWhiteSpace(hardware?.Cpu))
        {
            fingerprintParts.Add(hardware!.Cpu!);
        }

        if (hardware?.Cores is int cores)
        {
            fingerprintParts.Add($"{cores}C");
        }

        if (hardware?.RamGB is double ram)
        {
            fingerprintParts.Add($"{ram:0.##}GB");
        }

        if (!string.IsNullOrWhiteSpace(hardware?.OsBuild))
        {
            fingerprintParts.Add(hardware!.OsBuild!);
        }

        if (!string.IsNullOrWhiteSpace(hardware?.Gpu))
        {
            fingerprintParts.Add(hardware!.Gpu!);
        }

        var fingerprintRaw = string.Join("|", fingerprintParts);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintRaw)))[..12]
            .ToLowerInvariant();

        var readableParts = new[]
        {
            hardware?.Cpu ?? "CPU?",
            hardware?.Cores is int c ? $"{c}C" : null,
            hardware?.RamGB is double r ? $"{r:0.#}GB" : null,
            hardware?.OsBuild is string build ? $"Build {build}" : null
        }.Where(part => !string.IsNullOrWhiteSpace(part));

        var metadataNameIsReal = !string.IsNullOrWhiteSpace(metadata?.DeviceName)
                                 && !string.Equals(metadata.DeviceName, UnknownDeviceName, StringComparison.Ordinal);
        var hasAlias = !string.IsNullOrWhiteSpace(metadata?.Alias);
        var isIdentified = hasAlias
                           || (metadata?.DeviceOverridden == true
                               ? metadataNameIsReal
                               : metadataNameIsReal
                                 || !string.IsNullOrWhiteSpace(refDocument?.DeviceName)
                                 || !string.IsNullOrWhiteSpace(candidate.DeviceFolderHint));

        return new DeviceProfile(
            FirstNonEmpty(metadata?.DeviceId, $"dev-{hash}")!,
            deviceName,
            metadata?.Alias,
            hardware?.Cpu,
            hardware?.Cores,
            hardware?.LogicalCpus,
            hardware?.RamGB,
            hardware?.RamSpeed,
            hardware?.OsName,
            hardware?.OsVersion,
            hardware?.OsBuild,
            hardware?.Gpu,
            string.Join(" / ", readableParts),
            isIdentified);
    }

    private static SessionAggregate BuildSessionAggregate(
        IReadOnlyList<SystemSample> samples,
        TimeSpan duration)
    {
        var expected = samples.Count == 0
            ? 0
            : (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero) + 1;
        if (samples.Count == 0)
        {
            return new SessionAggregate(
                duration,
                0,
                0,
                expected,
                0,
                MetricStats.Empty,
                MetricStats.Empty,
                MetricStats.Empty,
                MetricStats.Empty,
                MetricStats.Empty,
                MetricStats.Empty,
                0,
                0,
                0,
                0);
        }

        var uniqueTimestamps = samples
            .Select(sample => sample.Timestamp)
            .Distinct()
            .OrderBy(timestamp => timestamp)
            .ToList();

        var completeness = expected > 0
            ? Math.Clamp(uniqueTimestamps.Count / (double)expected, 0, 1)
            : 0;

        var maxGap = 0d;
        for (var i = 1; i < uniqueTimestamps.Count; i++)
        {
            maxGap = Math.Max(maxGap, (uniqueTimestamps[i] - uniqueTimestamps[i - 1]).TotalSeconds);
        }

        var origin = samples[0].Timestamp;
        var memorySeries = Statistics.ToElapsedPerSecondSeries(
            samples,
            sample => sample.Timestamp,
            sample => sample.MemoryUsedGb,
            origin);

        return new SessionAggregate(
            duration,
            samples.Count,
            uniqueTimestamps.Count,
            expected,
            completeness,
            Statistics.Describe(samples.Select(sample => sample.CpuTotalPercent)),
            Statistics.Describe(samples.Select(sample => sample.CpuSimulatorPercent)),
            Statistics.Describe(samples.Select(sample => sample.MemoryUsedGb)),
            Statistics.Describe(samples.Select(sample => sample.MemoryUsagePercent)),
            Statistics.Describe(samples.Select(sample => sample.FrameBufferMb)),
            Statistics.Describe(samples
                .Where(sample => sample.RecognitionDurationMs > 0)
                .Select(sample => sample.RecognitionDurationMs)),
            samples.Count(sample => sample.CpuTotalPercent >= 80) / (double)samples.Count,
            samples.Count(sample => sample.CpuTotalPercent >= 90) / (double)samples.Count,
            Statistics.SlopePerMinute(memorySeries) * 1024d,
            (int)Math.Round(maxGap, MidpointRounding.AwayFromZero));
    }

    private static TaskAggregate BuildTaskAggregate(
        IReadOnlyList<TaskSample> samples,
        IReadOnlyList<TaskRun> runs,
        TimeSpan sessionDuration)
    {
        var running = samples
            .Where(sample => sample.State == TaskRunState.Running)
            .OrderBy(sample => sample.Timestamp)
            .ToList();

        var runCount = runs.Count;
        var restartCount = Math.Max(0, runCount - 1);
        var stateFlaps = CountStateFlaps(samples);

        if (running.Count == 0)
        {
            return TaskAggregate.Empty with
            {
                RunCount = runCount,
                RestartCount = restartCount,
                StateFlaps = stateFlaps
            };
        }

        var perSecond = running
            .GroupBy(sample => sample.Timestamp)
            .OrderBy(group => group.Key)
            .ToList();

        var cpuSeconds = perSecond.Sum(group => group.Average(sample => sample.CpuPercent) / 100d);
        var diskTotalMb = perSecond.Sum(group => group.Average(sample => sample.DiskKbPerSecond)) / 1024d;
        var networkTotalMb = perSecond.Sum(group => group.Average(sample => sample.NetworkKbps)) / 8d / 1024d;

        var origin = running[0].Timestamp;
        var memorySeries = perSecond
            .Select(group => (
                X: (group.Key - origin).TotalSeconds,
                Y: group.Average(sample => sample.MemoryMb)))
            .ToList();

        var activeDuration = runs.Aggregate(TimeSpan.Zero, (total, run) => total + run.Duration);
        var activeRatio = Math.Clamp(
            activeDuration.TotalSeconds / Math.Max(1d, sessionDuration.TotalSeconds),
            0,
            1);

        return new TaskAggregate(
            activeDuration,
            activeRatio,
            Statistics.Describe(running.Select(sample => sample.CpuPercent)),
            Statistics.Describe(running.Select(sample => sample.MemoryMb)),
            Statistics.Describe(running.Select(sample => sample.DiskKbPerSecond)),
            Statistics.Describe(running.Select(sample => sample.NetworkKbps)),
            cpuSeconds,
            Statistics.SlopePerMinute(memorySeries),
            diskTotalMb,
            networkTotalMb,
            runCount,
            restartCount,
            stateFlaps);
    }

    private static int CountStateFlaps(IReadOnlyList<TaskSample> samples)
    {
        var flaps = 0;
        bool? previous = null;

        foreach (var sample in samples.OrderBy(sample => sample.Timestamp))
        {
            var current = sample.State == TaskRunState.Running;
            if (previous is not null && previous.Value != current)
            {
                flaps++;
            }

            previous = current;
        }

        return flaps;
    }

    private static void AddSystemQualityDiagnostics(
        ICollection<Diagnostic> diagnostics,
        string folderName,
        IReadOnlyList<SystemSample> samples,
        SessionAggregate aggregate,
        TimeSpan duration,
        int taskCount,
        AnalysisOptions options)
    {
        if (samples.Count == 0)
        {
            return;
        }

        var unique = samples.Select(sample => sample.Timestamp).Distinct().ToList();
        var duplicateCount = samples.Count - unique.Count;
        var duplicateRatio = samples.Count == 0 ? 0 : duplicateCount * 100d / samples.Count;
        if (duplicateCount > 0 && duplicateRatio > options.DuplicateRatioPercent)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "DUPLICATE_TIMESTAMPS",
                $"{folderName}: 有 {duplicateCount} 筆重複時間戳記（{duplicateRatio:0.##}%），超過門檻 {options.DuplicateRatioPercent:0.##}%；圖表按秒平均、統計使用原始樣本"));
        }

        var ordered = unique.OrderBy(timestamp => timestamp).ToList();
        var gaps = new List<double>();
        for (var i = 1; i < ordered.Count; i++)
        {
            var seconds = (ordered[i] - ordered[i - 1]).TotalSeconds;
            if (seconds > options.SampleGapSeconds)
            {
                gaps.Add(seconds);
            }
        }

        if (gaps.Count > 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "SAMPLE_GAP",
                $"{folderName}: 有 {gaps.Count} 段紀錄中斷，最大 {gaps.Max():0.#} 秒（門檻 {options.SampleGapSeconds} 秒）"));
        }

        if (duration.TotalSeconds < options.ShortSessionSeconds)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "SHORT_SESSION",
                $"{folderName}: 紀錄長度僅 {duration.TotalSeconds:0.#} 秒（門檻 {options.ShortSessionSeconds} 秒）"));
        }

        var highRun = LongestCpuHighRun(samples, options.CpuHighPercent, options.SampleGapSeconds);
        if (highRun >= options.CpuHighSustainedSeconds)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "CPU_HIGH_SUSTAINED",
                $"{folderName}: CPU ≥ {options.CpuHighPercent:0.#}% 持續約 {highRun} 秒（門檻 {options.CpuHighSustainedSeconds} 秒）"));
        }

        if (aggregate.MemoryUsagePercent.HasData
            && aggregate.MemoryUsagePercent.Max >= options.RamHighPercent)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "RAM_HIGH",
                $"{folderName}: 記憶體使用率最高 {aggregate.MemoryUsagePercent.Max:0.#}%，超過門檻 {options.RamHighPercent:0.#}%"));
        }
        else if (samples.Count > 0)
        {
            var minimumAvailable = samples.Min(sample => sample.MemoryAvailableGb);
            var maximumAvailable = samples.Max(sample => sample.MemoryAvailableGb);
            if (maximumAvailable > 0 && minimumAvailable <= options.AvailableRamMinimumGb)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "RAM_HIGH",
                    $"{folderName}: 可用記憶體最低 {minimumAvailable:0.##} GB，低於門檻 {options.AvailableRamMinimumGb:0.##} GB"));
            }
        }

        if (duration.TotalSeconds >= options.MemoryGrowthMinimumSeconds
            && aggregate.MemoryGrowthMbPerMinute > options.MemoryGrowthMbPerMinute)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "MEMORY_GROWTH",
                $"{folderName}: 記憶體成長 {aggregate.MemoryGrowthMbPerMinute:0.#} MB/min，超過門檻 {options.MemoryGrowthMbPerMinute:0.#} MB/min"));
        }

        if (taskCount == 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Info,
                "SYSTEM_ONLY_SESSION",
                $"{folderName}: 此場次只有系統資源紀錄"));
        }
    }

    private static int LongestCpuHighRun(
        IReadOnlyList<SystemSample> samples,
        double threshold,
        int sampleGapSeconds)
    {
        var grouped = samples
            .GroupBy(sample => sample.Timestamp)
            .OrderBy(group => group.Key)
            .Select(group => (
                Time: group.Key,
                Cpu: group.Average(sample => sample.CpuTotalPercent)))
            .ToList();

        var longest = 0;
        var current = 0;
        DateTime? previous = null;

        foreach (var (time, cpu) in grouped)
        {
            if (previous is not null && (time - previous.Value).TotalSeconds > sampleGapSeconds)
            {
                current = 0;
            }

            if (cpu >= threshold)
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 0;
            }

            previous = time;
        }

        return longest;
    }

    private static void AddRefMismatchDiagnostics(
        ICollection<Diagnostic> diagnostics,
        string folderName,
        SessionAggregate aggregate,
        RefSummary? summary)
    {
        if (summary is null || !aggregate.CpuTotal.HasData)
        {
            return;
        }

        var mismatches = new List<string>();

        AddMismatch(mismatches, "CPU 平均", aggregate.CpuTotal.Mean, summary.CpuAvg, 0.25, 0.02);
        AddMismatch(mismatches, "CPU 最高", aggregate.CpuTotal.Max, summary.CpuMax, 0.25, 0.02);
        AddMismatch(mismatches, "CPU 最低", aggregate.CpuTotal.Min, summary.CpuMin, 0.25, 0.02);
        AddMismatch(mismatches, "RAM 平均", aggregate.MemoryUsedGb.Mean, summary.RamAvgGb, 0.1, 0.02);
        AddMismatch(mismatches, "RAM 最高", aggregate.MemoryUsedGb.Max, summary.RamMaxGb, 0.1, 0.02);
        AddMismatch(mismatches, "RAM 最低", aggregate.MemoryUsedGb.Min, summary.RamMinGb, 0.1, 0.02);
        AddMismatch(mismatches, "RAM% 平均", aggregate.MemoryUsagePercent.Mean, summary.RamAvgPercent, 1, 0.02);
        AddMismatch(mismatches, "RAM% 最高", aggregate.MemoryUsagePercent.Max, summary.RamMaxPercent, 1, 0.02);
        AddMismatch(mismatches, "RAM% 最低", aggregate.MemoryUsagePercent.Min, summary.RamMinPercent, 1, 0.02);

        if (mismatches.Count > 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "REF_MISMATCH",
                $"{folderName}: ref.txt 與實算統計不一致（{string.Join("；", mismatches)}）"));
        }
    }

    private static void AddMismatch(
        ICollection<string> mismatches,
        string name,
        double computed,
        double? reference,
        double absoluteTolerance,
        double relativeTolerance)
    {
        if (reference is null)
        {
            return;
        }

        var difference = Math.Abs(computed - reference.Value);
        var tolerance = Math.Max(absoluteTolerance, Math.Abs(reference.Value) * relativeTolerance);
        if (difference > tolerance)
        {
            mismatches.Add($"{name} 實算 {computed:0.##} / ref {reference.Value:0.##}");
        }
    }

    private static string GetTaskName(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        const string suffix = "_BenchmarkLog";
        if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^suffix.Length];
        }

        var directoryName = Path.GetFileName(Path.GetDirectoryName(path));
        return string.IsNullOrWhiteSpace(directoryName) ? fileName : directoryName;
    }

    private static string? MostCommon(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record TaskParseResult(
        string Name,
        string FilePath,
        LogParseResult<TaskSample> ParseResult);
}
