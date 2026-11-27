using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Reporting;

public static class ReportBuilder
{
    private const int MaxSeriesPoints = 1500;

    private static readonly string[] TaskColors =
    {
        "#1565c0", "#00897b", "#2e7d32", "#ef6c00", "#c62828", "#6a1b9a", "#5d4037", "#0277bd"
    };

    public static ReportModel Build(
        IReadOnlyList<Session> sessions,
        string sourcePath,
        DateTimeOffset? generatedAt = null)
    {
        var ordered = sessions
            .OrderBy(session => session.StartTime)
            .ToList();

        var devices = ordered
            .GroupBy(session => session.Device.DeviceKey, StringComparer.OrdinalIgnoreCase)
            .Select(BuildDevice)
            .OrderByDescending(device => device.SessionCount)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var taskCatalog = ordered
            .SelectMany(session => session.Tasks.Select(task => task.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var reportSessions = ordered
            .Select(BuildSession)
            .ToList();

        return new ReportModel
        {
            SchemaVersion = 1,
            GeneratedAt = generatedAt ?? DateTimeOffset.Now,
            SourcePath = sourcePath,
            Title = "TRIOV2 效能報告",
            Devices = devices,
            Sessions = reportSessions,
            TaskCatalog = taskCatalog
        };
    }

    private static ReportDevice BuildDevice(IGrouping<string, Session> group)
    {
        var first = group.OrderBy(session => session.StartTime).First();
        var device = first.Device;
        var baseline = group.FirstOrDefault(session => session.Metadata?.IsBaseline == true);

        return new ReportDevice
        {
            DeviceKey = device.DeviceKey,
            DisplayName = DeviceDisplay(device),
            DeviceName = device.DeviceName,
            Alias = device.Alias,
            CpuModel = device.CpuModel,
            Cores = device.Cores,
            LogicalCpus = device.LogicalCpus,
            RamGb = device.RamGb,
            RamSpeed = device.RamSpeed,
            OsName = device.OsName,
            OsVersion = device.OsVersion,
            OsBuild = device.OsBuild,
            Gpu = device.Gpu,
            Fingerprint = device.Fingerprint,
            IsIdentified = device.IsIdentified,
            BaselineSessionKey = baseline?.SessionKey,
            SessionCount = group.Count()
        };
    }

    private static ReportSession BuildSession(Session session)
    {
        var aggregate = session.Aggregate;
        var tasks = session.Tasks
            .Select((task, index) => BuildTask(session, task, index))
            .ToList();

        var diagnostics = session.Diagnostics
            .Select(diagnostic => new ReportDiagnostic
            {
                Severity = diagnostic.Severity.ToString(),
                Code = diagnostic.Code,
                Message = diagnostic.Message
            })
            .ToList();

        var health = HealthAssessor.Assess(session);

        return new ReportSession
        {
            SessionKey = session.SessionKey,
            FolderName = session.FolderName,
            DeviceKey = session.Device.DeviceKey,
            DeviceDisplay = DeviceDisplay(session.Device),
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            DurationSeconds = Safe(session.Duration.TotalSeconds),
            SampleCount = aggregate.SampleCount,
            ExpectedSampleCount = aggregate.ExpectedSampleCount,
            HasRefTxt = session.HasRefTxt,
            HasSystemLog = session.HasSystemLog,
            Mode = MostCommonMode(session),
            IsBaseline = session.Metadata?.IsBaseline == true,
            Notes = session.Metadata?.Notes,
            Labels = session.Metadata?.Labels?.ToArray() ?? Array.Empty<string>(),
            Metrics = BuildSessionMetrics(session),
            Series = BuildSystemSeries(session),
            Tasks = tasks,
            Diagnostics = diagnostics,
            WarningCount = session.Diagnostics.Count(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Warning),
            ErrorCount = session.Diagnostics.Count(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error),
            Health = new ReportHealth
            {
                Level = health.Level.ToString(),
                LevelText = health.LevelText,
                Confidence = health.ConfidenceText,
                Headline = health.Headline,
                Findings = health.Findings
                    .Select(finding => new ReportFinding
                    {
                        Impact = finding.Impact.ToString(),
                        Category = HealthAssessor.CategoryText(finding.Category),
                        Code = finding.Code,
                        Title = finding.Title,
                        Explanation = finding.Explanation,
                        Suggestion = finding.Suggestion,
                        Detail = finding.Detail
                    })
                    .ToList()
            }
        };
    }

    private static ReportSessionMetrics BuildSessionMetrics(Session session)
    {
        var aggregate = session.Aggregate;
        var diskPeak = session.Tasks.Count == 0
            ? 0
            : session.Tasks.Max(task => task.Aggregate.DiskKbPerSecond.Max);
        var networkPeak = session.Tasks.Count == 0
            ? 0
            : session.Tasks.Max(task => task.Aggregate.NetworkKbps.Max);

        return new ReportSessionMetrics
        {
            CpuAvg = Safe(aggregate.CpuTotal.Mean),
            CpuMin = Safe(aggregate.CpuTotal.Min),
            CpuMax = Safe(aggregate.CpuTotal.Max),
            CpuP95 = Safe(aggregate.CpuTotal.P95),
            CpuSimAvg = Safe(aggregate.CpuSimulator.Mean),
            CpuSimMax = Safe(aggregate.CpuSimulator.Max),
            RamAvg = Safe(aggregate.MemoryUsedGb.Mean),
            RamMin = Safe(aggregate.MemoryUsedGb.Min),
            RamMax = Safe(aggregate.MemoryUsedGb.Max),
            RamPercentMax = Safe(aggregate.MemoryUsagePercent.Max),
            FrameBufferMax = Safe(aggregate.FrameBufferMb.Max),
            RecognitionAvg = Safe(aggregate.RecognitionMs.Mean),
            RecognitionMax = Safe(aggregate.RecognitionMs.Max),
            DiskPeak = Safe(diskPeak),
            NetworkPeak = Safe(networkPeak),
            Completeness = Safe(aggregate.DataCompleteness),
            MaxGapSeconds = aggregate.MaxGapSeconds
        };
    }

    private static IReadOnlyList<ReportSeries> BuildSystemSeries(Session session)
    {
        var samples = session.SystemSamples;
        if (samples.Count == 0)
        {
            return Array.Empty<ReportSeries>();
        }

        var origin = session.StartTime;
        var grouped = samples
            .GroupBy(sample => sample.Timestamp)
            .OrderBy(group => group.Key)
            .ToList();

        return new[]
        {
            BuildSeries(grouped, origin, "cpuTotal", "全系統 CPU", "%", "#1565c0",
                group => group.Average(sample => sample.CpuTotalPercent)),
            BuildSeries(grouped, origin, "cpuSimulator", "模擬器 CPU", "%", "#00897b",
                group => group.Average(sample => sample.CpuSimulatorPercent)),
            BuildSeries(grouped, origin, "cpuFree", "閒置 CPU", "%", "#78909c",
                group => group.Average(sample => sample.CpuFreePercent)),
            BuildSeries(grouped, origin, "memoryUsed", "已用記憶體", "GB", "#6a1b9a",
                group => group.Average(sample => sample.MemoryUsedGb)),
            BuildSeries(grouped, origin, "memoryAvailable", "可用記憶體", "GB", "#2e7d32",
                group => group.Average(sample => sample.MemoryAvailableGb)),
            BuildSeries(grouped, origin, "memoryPercent", "記憶體使用率", "%", "#ef6c00",
                group => group.Average(sample => sample.MemoryUsagePercent)),
            BuildSeries(grouped, origin, "frameBuffer", "影像緩衝", "MB", "#5c6bc0",
                group => group.Average(sample => sample.FrameBufferMb)),
            BuildSeries(grouped, origin, "recognition", "辨識時長", "ms", "#c62828",
                group => group.Average(sample => sample.RecognitionDurationMs))
        };
    }

    private static ReportSeries BuildSeries(
        IReadOnlyList<IGrouping<DateTime, SystemSample>> groups,
        DateTime origin,
        string key,
        string name,
        string unit,
        string color,
        Func<IGrouping<DateTime, SystemSample>, double> selector)
    {
        var points = groups
            .Select(group => new[]
            {
                Safe((group.Key - origin).TotalSeconds),
                Safe(selector(group))
            })
            .ToList();

        return new ReportSeries
        {
            Key = key,
            Name = name,
            Unit = unit,
            Color = color,
            Points = Downsample(points, MaxSeriesPoints)
        };
    }

    private static ReportTask BuildTask(Session session, TaskLog task, int index)
    {
        var aggregate = task.Aggregate;
        var running = task.Samples
            .Where(sample => sample.State == TaskRunState.Running)
            .GroupBy(sample => sample.Timestamp)
            .OrderBy(group => group.Key)
            .ToList();

        var cpuPoints = running
            .Select(group => new[]
            {
                Safe((group.Key - session.StartTime).TotalSeconds),
                Safe(group.Average(sample => sample.CpuPercent))
            })
            .ToList();

        var memoryPoints = running
            .Select(group => new[]
            {
                Safe((group.Key - session.StartTime).TotalSeconds),
                Safe(group.Average(sample => sample.MemoryMb))
            })
            .ToList();

        var runs = task.Runs
            .Select(run => new ReportRun
            {
                StartSeconds = Safe((run.Start - session.StartTime).TotalSeconds),
                DurationSeconds = Safe(run.Duration.TotalSeconds)
            })
            .ToList();

        return new ReportTask
        {
            Name = task.Name,
            Color = TaskColors[index % TaskColors.Length],
            ActiveSeconds = Safe(aggregate.ActiveDuration.TotalSeconds),
            ActiveRatio = Safe(aggregate.ActiveRatio),
            CpuAvg = Safe(aggregate.Cpu.Mean),
            CpuMax = Safe(aggregate.Cpu.Max),
            CpuP95 = Safe(aggregate.Cpu.P95),
            MemoryAvg = Safe(aggregate.MemoryMb.Mean),
            MemoryMax = Safe(aggregate.MemoryMb.Max),
            CpuSeconds = Safe(aggregate.CpuSeconds),
            MemoryGrowth = Safe(aggregate.MemoryGrowthMbPerMinute),
            DiskPeak = Safe(aggregate.DiskKbPerSecond.Max),
            NetworkPeak = Safe(aggregate.NetworkKbps.Max),
            RunCount = aggregate.RunCount,
            RestartCount = aggregate.RestartCount,
            Runs = runs,
            CpuPoints = Downsample(cpuPoints, MaxSeriesPoints),
            MemoryPoints = Downsample(memoryPoints, MaxSeriesPoints)
        };
    }

    private static string DeviceDisplay(DeviceProfile device)
    {
        var name = device.DisplayName;
        if (device.IsIdentified)
        {
            return name;
        }

        var key = device.DeviceKey;
        var suffix = key.Length > 4 ? key[^4..] : key;
        return $"{name} ({suffix})";
    }

    private static string? MostCommonMode(Session session)
    {
        if (!string.IsNullOrWhiteSpace(session.Metadata?.Mode))
        {
            return session.Metadata.Mode;
        }

        return session.SystemSamples
            .Select(sample => sample.OperationMode)
            .Where(mode => !string.IsNullOrWhiteSpace(mode))
            .GroupBy(mode => mode, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    /// <summary>
    /// Largest-Triangle-Three-Buckets downsampling. Input points are [x, y] sorted by x.
    /// </summary>
    private static IReadOnlyList<double[]> Downsample(IReadOnlyList<double[]> points, int threshold)
    {
        if (points.Count <= threshold || threshold < 4)
        {
            return points;
        }

        var sampled = new List<double[]>(threshold) { points[0] };
        var bucketSize = (points.Count - 2.0) / (threshold - 2);
        var anchorIndex = 0;

        for (var i = 0; i < threshold - 2; i++)
        {
            var averageStart = (int)Math.Floor((i + 1) * bucketSize) + 1;
            var averageEnd = (int)Math.Floor((i + 2) * bucketSize) + 1;
            averageEnd = Math.Min(averageEnd, points.Count);
            if (averageStart >= averageEnd)
            {
                averageStart = Math.Min(averageStart, points.Count - 1);
                averageEnd = Math.Min(averageStart + 1, points.Count);
            }

            var averageX = 0d;
            var averageY = 0d;
            var averageCount = 0;
            for (var j = averageStart; j < averageEnd; j++)
            {
                averageX += points[j][0];
                averageY += points[j][1];
                averageCount++;
            }

            if (averageCount > 0)
            {
                averageX /= averageCount;
                averageY /= averageCount;
            }

            var rangeStart = (int)Math.Floor(i * bucketSize) + 1;
            var rangeEnd = (int)Math.Floor((i + 1) * bucketSize) + 1;
            rangeStart = Math.Clamp(rangeStart, 1, points.Count - 1);
            rangeEnd = Math.Clamp(rangeEnd, rangeStart + 1, points.Count);

            var maxArea = -1d;
            var maxIndex = rangeStart;
            var anchorX = points[anchorIndex][0];
            var anchorY = points[anchorIndex][1];

            for (var j = rangeStart; j < rangeEnd; j++)
            {
                var area = Math.Abs(
                    (anchorX - averageX) * (points[j][1] - anchorY)
                    - (anchorX - points[j][0]) * (averageY - anchorY)) * 0.5;

                if (area > maxArea)
                {
                    maxArea = area;
                    maxIndex = j;
                }
            }

            sampled.Add(points[maxIndex]);
            anchorIndex = maxIndex;
        }

        sampled.Add(points[^1]);
        return sampled;
    }

    private static double Safe(double value)
        => double.IsFinite(value) ? Math.Round(value, 3) : 0;
}
