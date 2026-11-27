using System.Globalization;
using System.IO;
using System.Text.Json;
using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 掃描路徑並解析單次或跨目錄彙整之效能測試場次
/// 製作者: Office of William
/// </summary>
public sealed class ScanOutputResult
{
    public bool IsSingleSessionMode { get; init; }
    public string ScannedRootPath { get; init; } = string.Empty;
    public IReadOnlyList<BenchmarkSession> Sessions { get; init; } = Array.Empty<BenchmarkSession>();
}

public static class LogSessionScanner
{
    private const string SystemCsvFileName = "System_BenchmarkLog.csv";
    private const string RefFileName = "ref.txt";
    private const string AliasFileName = ".trio_analyzer_aliases.json";

    public static bool IsSessionFolder(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return false;
        return File.Exists(Path.Combine(directoryPath, SystemCsvFileName)) ||
               File.Exists(Path.Combine(directoryPath, RefFileName));
    }

    public static async Task<ScanOutputResult> ScanPathAsync(
        string targetPath,
        EvaluationThresholds thresholds,
        IProgress<(int Done, int Total, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ScanPathSync(targetPath, thresholds, progress, cancellationToken), cancellationToken);
    }

    public static Dictionary<string, string> LoadAliases(string rootPath)
    {
        try
        {
            string file = Path.Combine(rootPath, AliasFileName);
            if (!File.Exists(file)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <returns>是否成功寫入（資料夾唯讀或被占用時回傳 false）。</returns>
    public static bool SaveAliases(string rootPath, Dictionary<string, string> aliases)
    {
        try
        {
            if (!Directory.Exists(rootPath)) return false;
            string file = Path.Combine(rootPath, AliasFileName);
            string json = JsonSerializer.Serialize(aliases, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ScanOutputResult ScanPathSync(
        string targetPath,
        EvaluationThresholds thresholds,
        IProgress<(int Done, int Total, string Status)>? progress,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
        {
            return new ScanOutputResult { ScannedRootPath = targetPath ?? string.Empty };
        }

        string fullRoot = Path.GetFullPath(targetPath);
        bool singleMode = IsSessionFolder(fullRoot);

        var sessionFolders = new List<string>();
        if (singleMode)
        {
            sessionFolders.Add(fullRoot);
        }
        else
        {
            DiscoverSessionFoldersRecursive(fullRoot, sessionFolders, ct);
        }

        var aliases = LoadAliases(fullRoot);
        var sessions = new List<BenchmarkSession>();

        int total = sessionFolders.Count;
        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            string dir = sessionFolders[i];
            progress?.Report((i + 1, total, Path.GetFileName(dir)));

            BenchmarkSession? session = ParseSessionFolder(dir, fullRoot, thresholds, aliases);
            if (session != null)
            {
                sessions.Add(session);
            }
        }

        sessions.Sort((a, b) => b.MeasuredAt.CompareTo(a.MeasuredAt));

        return new ScanOutputResult
        {
            IsSingleSessionMode = singleMode,
            ScannedRootPath = fullRoot,
            Sessions = sessions
        };
    }

    private static void DiscoverSessionFoldersRecursive(string dir, List<string> results, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            foreach (string subDir in Directory.EnumerateDirectories(dir))
            {
                ct.ThrowIfCancellationRequested();
                string name = Path.GetFileName(subDir);
                if (name.StartsWith(".")) continue;

                if (IsSessionFolder(subDir))
                {
                    // 已確認是測試場次根目錄，不需再往底下的 Task 子目錄遞迴
                    results.Add(subDir);
                }
                else
                {
                    DiscoverSessionFoldersRecursive(subDir, results, ct);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static BenchmarkSession? ParseSessionFolder(
        string folderPath,
        string rootPath,
        EvaluationThresholds thresholds,
        Dictionary<string, string> aliases)
    {
        string refPath = Path.Combine(folderPath, RefFileName);
        string sysCsvPath = Path.Combine(folderPath, SystemCsvFileName);

        bool hasRef = File.Exists(refPath);
        bool hasSysCsv = File.Exists(sysCsvPath);
        if (!hasRef && !hasSysCsv) return null;

        RefParsedResult? refParsed = hasRef ? RefSummaryParser.ParseFile(refPath) : null;
        List<SystemSamplePoint> sysSamples = hasSysCsv ? BenchmarkCsvParser.ParseSystemCsv(sysCsvPath) : new();

        // 掃描所有任務 CSV (子資料夾內的 *_BenchmarkLog.csv)
        var taskFiles = new List<(string TaskName, string FilePath)>();
        try
        {
            foreach (string subDir in Directory.EnumerateDirectories(folderPath))
            {
                string taskFolderName = Path.GetFileName(subDir);
                foreach (string csv in Directory.EnumerateFiles(subDir, "*_BenchmarkLog.csv"))
                {
                    taskFiles.Add((taskFolderName, csv));
                }
            }
        }
        catch
        {
        }

        var tasks = new List<TaskBenchmarkSummary>();
        for (int i = 0; i < taskFiles.Count; i++)
        {
            var (taskName, filePath) = taskFiles[i];
            tasks.Add(BenchmarkCsvParser.ParseTaskCsv(filePath, taskName, i));
        }
        tasks = tasks.OrderByDescending(t => t.ActiveCpuMax).ThenBy(t => t.TaskName, StringComparer.OrdinalIgnoreCase).ToList();

        // 預設把前 2 名有活動的高負載任務勾選於時間軸疊加曲線
        int defaultOverlayCount = 0;
        foreach (var t in tasks)
        {
            if (t.ActiveSamples > 0 && defaultOverlayCount < 2)
            {
                t.IsOverlayVisible = true;
                defaultOverlayCount++;
            }
        }

        // 計算系統統計指標
        double cpuAvg = 0, cpuMin = 0, cpuMax = 0, cpuP50 = 0, cpuP95 = 0, cpuP99 = 0, cpuStd = 0, cpuOver80 = 0;
        double ramAvgGb = 0, ramMinGb = 0, ramMaxGb = 0, ramAvgPct = 0, ramPeakPct = 0, ramOver85 = 0;
        double minAvailableGb = double.NaN;
        int recogEvents = 0;
        double recogAvgMs = 0, recogMaxMs = 0;

        if (sysSamples.Count > 0)
        {
            var cpus = sysSamples.Select(s => s.SystemCpuPercent).ToArray();
            var cpuStats = StatisticsHelper.ComputeDistribution(cpus);
            cpuAvg = cpuStats.Avg;
            cpuMin = cpuStats.Min;
            cpuMax = cpuStats.Max;
            cpuP50 = cpuStats.P50;
            cpuP95 = cpuStats.P95;
            cpuP99 = cpuStats.P99;
            cpuStd = cpuStats.StdDev;
            cpuOver80 = sysSamples.Count(s => s.SystemCpuPercent >= 80.0) * 100.0 / sysSamples.Count;

            var ramGbs = sysSamples.Select(s => s.UsedMemoryGb).ToArray();
            var ramGbStats = StatisticsHelper.ComputeDistribution(ramGbs);
            ramAvgGb = ramGbStats.Avg;
            ramMinGb = ramGbStats.Min;
            ramMaxGb = ramGbStats.Max;

            var ramPcts = sysSamples.Select(s => s.MemoryPercent).ToArray();
            var ramPctStats = StatisticsHelper.ComputeDistribution(ramPcts);
            ramAvgPct = ramPctStats.Avg;
            ramPeakPct = ramPctStats.Max;
            ramOver85 = sysSamples.Count(s => s.MemoryPercent >= 85.0) * 100.0 / sysSamples.Count;
            if (sysSamples.Any(s => s.AvailableMemoryGb > 0))
            {
                minAvailableGb = sysSamples.Min(s => s.AvailableMemoryGb);
            }

            var recogList = sysSamples.Where(s => s.IsRecognizing).ToList();
            recogEvents = recogList.Count;
            if (recogEvents > 0)
            {
                recogAvgMs = recogList.Average(r => r.RecognitionDurationMs);
                recogMaxMs = recogList.Max(r => r.RecognitionDurationMs);
            }
        }
        else if (refParsed != null)
        {
            cpuAvg = refParsed.CpuAvg ?? 0;
            cpuMin = refParsed.CpuMin ?? 0;
            cpuMax = refParsed.CpuMax ?? 0;
            cpuP50 = cpuAvg;
            cpuP95 = cpuMax;
            cpuP99 = cpuMax;
            ramAvgGb = refParsed.RamAvgGb ?? 0;
            ramMinGb = refParsed.RamMinGb ?? 0;
            ramMaxGb = refParsed.RamMaxGb ?? 0;
            ramAvgPct = refParsed.RamAvgPct ?? 0;
            ramPeakPct = refParsed.RamMaxPct ?? 0;
        }

        // 建立或補齊設備硬體設定檔
        DeviceProfile profile;
        double estTotalRamGb = sysSamples.Count > 0
            ? Math.Round(sysSamples[0].UsedMemoryGb + sysSamples[0].AvailableMemoryGb, 1)
            : 0;

        if (refParsed != null)
        {
            var rp = refParsed.HardwareProfile;
            if (rp.MemoryInstalledGb <= 0 && estTotalRamGb > 0)
            {
                profile = new DeviceProfile
                {
                    HasRefTxt = rp.HasRefTxt,
                    MachineName = rp.MachineName,
                    CpuModel = rp.CpuModel,
                    CpuBaseSpeed = rp.CpuBaseSpeed,
                    CpuSockets = rp.CpuSockets,
                    CpuCores = rp.CpuCores,
                    CpuLogicalProcessors = rp.CpuLogicalProcessors,
                    Virtualization = rp.Virtualization,
                    L1Cache = rp.L1Cache,
                    L2Cache = rp.L2Cache,
                    L3Cache = rp.L3Cache,
                    MemorySpecType = rp.MemorySpecType,
                    MemorySpeed = rp.MemorySpeed,
                    MemorySlotsUsed = rp.MemorySlotsUsed,
                    MemoryFormFactor = rp.MemoryFormFactor,
                    MemoryHardwareReserved = rp.MemoryHardwareReserved,
                    MemoryInstalledText = $"推估約 {estTotalRamGb:0.#} GB",
                    MemoryInstalledGb = estTotalRamGb,
                    NpuSummary = rp.NpuSummary,
                    GpuSummary = rp.GpuSummary,
                    TotalProcessesText = rp.TotalProcessesText,
                    OsName = rp.OsName,
                    OsVersion = rp.OsVersion,
                    OsBuild = rp.OsBuild,
                    OsArchitecture = rp.OsArchitecture,
                    UptimeText = rp.UptimeText
                };
            }
            else
            {
                profile = rp;
            }
        }
        else
        {
            profile = new DeviceProfile
            {
                HasRefTxt = false,
                MachineName = "未標記硬體 (無 ref.txt)",
                CpuModel = "未回報 (無 ref.txt)",
                MemoryInstalledGb = estTotalRamGb,
                MemoryInstalledText = estTotalRamGb > 0 ? $"推估約 {estTotalRamGb:0.#} GB" : "--",
                OsName = "未回報 OS (無 ref.txt)"
            };
        }

        // 舊紀錄沒有逐秒可用記憶體時，以「安裝容量 − 最高已用」推估
        if (double.IsNaN(minAvailableGb) && profile.MemoryInstalledGb > 0 && ramMaxGb > 0)
        {
            minAvailableGb = Math.Max(0, profile.MemoryInstalledGb - ramMaxGb);
        }

        string folderName = Path.GetFileName(folderPath);
        DateTime measuredAt = ResolveMeasuredTime(folderName, refParsed, sysSamples, folderPath);
        TimeSpan duration = ResolveDuration(refParsed, sysSamples);
        int sampleCount = sysSamples.Count > 0 ? sysSamples.Count : (refParsed?.SampleCount ?? 0);

        string relHint = string.Empty;
        try
        {
            string? parent = Directory.GetParent(folderPath)?.FullName;
            if (parent != null && !string.Equals(parent, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                relHint = Path.GetRelativePath(rootPath, parent);
            }
        }
        catch
        {
        }

        aliases.TryGetValue(folderPath, out string? customAlias);

        var session = new BenchmarkSession
        {
            SessionName = folderName,
            FolderPath = folderPath,
            RelativeGroupHint = relHint,
            MeasuredAt = measuredAt,
            Duration = duration,
            SampleCount = sampleCount,
            HasRefTxt = hasRef,
            HasSystemCsv = hasSysCsv,
            RefRawText = refParsed?.RawText ?? string.Empty,
            HardwareProfile = profile,
            CpuAvg = cpuAvg,
            CpuMin = cpuMin,
            CpuMax = cpuMax,
            CpuP50 = cpuP50,
            CpuP95 = cpuP95,
            CpuP99 = cpuP99,
            CpuStdDev = cpuStd,
            CpuOver80RatioPercent = cpuOver80,
            RamAvgGb = ramAvgGb,
            RamMinGb = ramMinGb,
            RamMaxGb = ramMaxGb,
            RamAvgPercent = ramAvgPct,
            RamPeakPercent = ramPeakPct,
            RamOver85RatioPercent = ramOver85,
            MinAvailableGb = minAvailableGb,
            RecognitionEventCount = recogEvents,
            AvgRecognitionDurationMs = recogAvgMs,
            MaxRecognitionDurationMs = recogMaxMs,
            SystemSamples = sysSamples,
            Tasks = tasks,
            AssignedDeviceAlias = customAlias
        };

        session.Verdict = SizingAdvisorEngine.EvaluateSession(session, thresholds);

        // 誤觸產生的超短紀錄預設不納入設備彙總，避免以幾秒鐘的資料下結論（仍可手動勾選）
        session.IsIncluded = !SizingAdvisorEngine.IsTooShort(session);
        return session;
    }

    private static DateTime ResolveMeasuredTime(
        string folderName,
        RefParsedResult? refParsed,
        List<SystemSamplePoint> sysSamples,
        string folderPath)
    {
        if (sysSamples.Count > 0 && sysSamples[0].Timestamp != DateTime.MinValue)
        {
            return sysSamples[0].Timestamp;
        }
        if (refParsed?.MeasuredAt != null)
        {
            return refParsed.MeasuredAt.Value;
        }
        if (folderName.Length >= 15 &&
            DateTime.TryParseExact(folderName[^15..], "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var fromName))
        {
            return fromName;
        }
        return Directory.GetLastWriteTime(folderPath);
    }

    private static TimeSpan ResolveDuration(RefParsedResult? refParsed, List<SystemSamplePoint> sysSamples)
    {
        if (sysSamples.Count >= 2 &&
            sysSamples[0].Timestamp != DateTime.MinValue &&
            sysSamples[^1].Timestamp >= sysSamples[0].Timestamp)
        {
            return sysSamples[^1].Timestamp - sysSamples[0].Timestamp;
        }
        if (refParsed?.Duration != null)
        {
            return refParsed.Duration.Value;
        }
        return TimeSpan.FromSeconds(Math.Max(0, sysSamples.Count - 1));
    }
}
