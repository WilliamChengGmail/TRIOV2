using System.IO;
using System.Text;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// 單一行程單筆取樣數據記錄 (保留定義以供外部型別參照)
/// </summary>
public record ProcessSampleRecord(
    int SampleIndex,
    DateTime Timestamp,
    string ProcessName,
    int ProcessId,
    string StatusText,
    float CpuPercent,
    double MemoryMb,
    double DiskIoKbPerSec,
    double NetworkKbps,
    string PowerUsage,
    string PowerTrend
);

/// <summary>
/// 單一任務的彙總統計指標
/// </summary>
public sealed class TaskSummaryStats
{
    public string TaskName { get; init; } = string.Empty;
    public int SampleCount { get; set; }
    public float CpuMax { get; set; }
    public float CpuMin { get; set; } = float.MaxValue;
    public double CpuSum { get; set; }
    public double MemMax { get; set; }
    public double MemMin { get; set; } = double.MaxValue;
    public double MemSum { get; set; }
    public double DiskIoMax { get; set; }
    public double NetworkMax { get; set; }

    public float CpuAvg => SampleCount > 0 ? (float)Math.Round(CpuSum / SampleCount, 1) : 0f;
    public double MemAvg => SampleCount > 0 ? Math.Round(MemSum / SampleCount, 1) : 0.0;
}

/// <summary>
/// 多任務階層式目錄與獨立 CSV 串流記錄引擎 (適度分批 Flush 磁碟，杜絕記憶體累積與 Memory Swap)
/// 製作者: Office of William
/// </summary>
public sealed class MultiProcessSessionRecorder
{
    private const string CsvHeader = "樣本序號,時間戳記,處理程序名稱,PID,狀態,CPU使用率(%),記憶體使用量(MB),磁碟讀寫速率(KB/s),網路傳輸速率(Kbps),電源用量,電源用量趨勢";
    private const int FlushBatchSize = 20; // 每累積 20 筆樣本批次寫入一次，兼顧磁碟 I/O 效能與即時落盤

    private readonly object _lock = new();
    private bool _isRecording;
    private DateTime _startTime;
    private DateTime _endTime;
    private int _sampleIndex;
    private string _mainSessionDirectory = string.Empty;

    // TaskName -> 該任務之寫入緩衝 (字串行清單)
    private readonly Dictionary<string, List<string>> _taskBuffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TaskSummaryStats> _taskStats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _taskCsvPaths = new(StringComparer.OrdinalIgnoreCase);

    public bool IsRecording => _isRecording;
    public string MainSessionDirectory => _mainSessionDirectory;
    public DateTime StartTime => _startTime;
    public DateTime EndTime => _endTime;

    /// <summary>
    /// 啟動紀錄工作階段：以 yyyyMMdd_HHmmss 建立主目錄，並立即根據每個勾選的 task name 建立各自子資料夾與初始 CSV
    /// </summary>
    public string Start(IEnumerable<ProcessMonitorItem> selectedTasks, string? baseLogsDir = null)
    {
        lock (_lock)
        {
            _isRecording = true;
            _startTime = DateTime.Now;
            _endTime = _startTime;
            _sampleIndex = 0;
            _taskBuffers.Clear();
            _taskStats.Clear();
            _taskCsvPaths.Clear();

            baseLogsDir ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            string sessionFolderName = _startTime.ToString("yyyyMMdd_HHmmss");
            _mainSessionDirectory = Path.Combine(baseLogsDir, sessionFolderName);

            // 1. 建立主資料夾
            Directory.CreateDirectory(_mainSessionDirectory);

            // 2. 針對每個被勾選的任務名稱，立即建立各自資料夾與 CSV 表頭
            foreach (var task in selectedTasks)
            {
                if (string.IsNullOrWhiteSpace(task.ProcessName)) continue;
                InitTaskTrack(task.ProcessName);
            }

            return _mainSessionDirectory;
        }
    }

    private void InitTaskTrack(string processName)
    {
        string safeTaskName = SanitizeFolderName(processName);
        string taskSubDir = Path.Combine(_mainSessionDirectory, safeTaskName);
        Directory.CreateDirectory(taskSubDir);

        string csvPath = Path.Combine(taskSubDir, $"{safeTaskName}_BenchmarkLog.csv");
        if (!File.Exists(csvPath))
        {
            try
            {
                File.WriteAllText(csvPath, CsvHeader + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MultiProcessSessionRecorder] Init CSV for '{processName}' error: {ex.Message}");
            }
        }

        if (!_taskBuffers.ContainsKey(processName))
        {
            _taskBuffers[processName] = new List<string>(FlushBatchSize);
            _taskStats[processName] = new TaskSummaryStats { TaskName = processName };
            _taskCsvPaths[processName] = csvPath;
        }
    }

    /// <summary>
    /// 1Hz 採樣滴答：記錄各勾選行程之效能數據 (累積純量統計，達 Batch 閾值批次 Flush 並釋放記憶體)
    /// </summary>
    public void AddSample(IEnumerable<ProcessMonitorItem> currentTrackedItems)
    {
        lock (_lock)
        {
            if (!_isRecording) return;

            _sampleIndex++;
            _endTime = DateTime.Now;

            foreach (var item in currentTrackedItems)
            {
                if (!_taskBuffers.TryGetValue(item.ProcessName, out var buffer))
                {
                    // 若是紀錄中途被加入的任務，動態補建子目錄與初值
                    InitTaskTrack(item.ProcessName);
                    buffer = _taskBuffers[item.ProcessName];
                }

                // 更新純量統計
                var stats = _taskStats[item.ProcessName];
                stats.SampleCount++;
                if (item.CpuPercent > stats.CpuMax) stats.CpuMax = item.CpuPercent;
                if (item.CpuPercent < stats.CpuMin) stats.CpuMin = item.CpuPercent;
                stats.CpuSum += item.CpuPercent;

                if (item.MemoryMb > stats.MemMax) stats.MemMax = item.MemoryMb;
                if (item.MemoryMb < stats.MemMin) stats.MemMin = item.MemoryMb;
                stats.MemSum += item.MemoryMb;

                if (item.DiskIoKbPerSec > stats.DiskIoMax) stats.DiskIoMax = item.DiskIoKbPerSec;
                if (item.NetworkKbps > stats.NetworkMax) stats.NetworkMax = item.NetworkKbps;

                // 格式化單行 CSV
                string line = $"{_sampleIndex},{_endTime:yyyy-MM-dd HH:mm:ss},{item.ProcessName},{item.ProcessId},{item.StatusText},{item.CpuPercent:0.0},{item.MemoryMb:0.0},{item.DiskIoKbPerSec:0.0},{item.NetworkKbps:0.0},{item.PowerUsage},{item.PowerTrend}";
                buffer.Add(line);

                // 滿閾值即分批 Flush 至該任務之 CSV，並清空 buffer 釋放記憶體
                if (buffer.Count >= FlushBatchSize)
                {
                    FlushTaskBufferCore(item.ProcessName, buffer);
                }
            }
        }
    }

    private void FlushTaskBufferCore(string taskName, List<string> buffer)
    {
        if (buffer.Count == 0) return;
        if (!_taskCsvPaths.TryGetValue(taskName, out var csvPath)) return;

        try
        {
            File.AppendAllLines(csvPath, buffer, Encoding.UTF8);
            buffer.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MultiProcessSessionRecorder] Flush task '{taskName}' error: {ex.Message}");
        }
    }

    /// <summary>
    /// 停止紀錄並將各任務剩餘緩衝 Flush 寫入 CSV，同時追加各任務獨立效能量測統計摘要區塊
    /// </summary>
    public List<string> StopAndFlush()
    {
        var writtenFiles = new List<string>();

        lock (_lock)
        {
            if (_isRecording)
            {
                _isRecording = false;
                _endTime = DateTime.Now;
            }

            if (string.IsNullOrEmpty(_mainSessionDirectory) || !Directory.Exists(_mainSessionDirectory))
            {
                return writtenFiles;
            }

            TimeSpan totalDuration = _endTime - _startTime;
            string durationStr = totalDuration.TotalHours >= 1
                ? $"{(int)totalDuration.TotalHours:00}:{totalDuration.Minutes:00}:{totalDuration.Seconds:00}"
                : $"{totalDuration.Minutes:00}:{totalDuration.Seconds:00}";

            foreach (var kv in _taskBuffers)
            {
                string taskName = kv.Key;
                var buffer = kv.Value;

                // 1. Flush 該任務最後殘留的零星緩衝
                FlushTaskBufferCore(taskName, buffer);

                if (!_taskCsvPaths.TryGetValue(taskName, out var csvPath)) continue;

                // 2. 附加任務摘要結尾區塊
                if (_taskStats.TryGetValue(taskName, out var stat))
                {
                    float minCpu = stat.CpuMin == float.MaxValue ? 0f : stat.CpuMin;
                    double minMem = stat.MemMin == double.MaxValue ? 0.0 : stat.MemMin;

                    var sb = new StringBuilder();
                    sb.AppendLine();
                    sb.AppendLine("# ========================================================");
                    sb.AppendLine($"# 【任務：{taskName} 效能量測統計摘要】");
                    sb.AppendLine($"# 量測期間: {_startTime:yyyy-MM-dd HH:mm:ss} ~ {_endTime:yyyy-MM-dd HH:mm:ss} (總時長: {durationStr})");
                    sb.AppendLine($"# 總樣本數: {stat.SampleCount} 筆");
                    if (stat.SampleCount > 0)
                    {
                        sb.AppendLine($"# CPU 使用率: 最高 {stat.CpuMax:0.0} %, 最低 {minCpu:0.0} %, 平均 {stat.CpuAvg:0.0} %");
                        sb.AppendLine($"# 記憶體使用量: 最高 {stat.MemMax:0.0} MB, 最低 {minMem:0.0} MB, 平均 {stat.MemAvg:0.0} MB");
                        sb.AppendLine($"# 磁碟讀寫峰值: {stat.DiskIoMax:0.0} KB/s, 網路傳輸峰值: {stat.NetworkMax:0.0} Kbps");
                    }
                    else
                    {
                        sb.AppendLine($"# (此任務在量測期間尚未啟動或無取樣數據)");
                    }
                    sb.AppendLine("# ========================================================");

                    try
                    {
                        File.AppendAllText(csvPath, sb.ToString(), Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MultiProcessSessionRecorder] Append footer for '{taskName}' error: {ex.Message}");
                    }
                }

                writtenFiles.Add(csvPath);
            }
        }

        return writtenFiles;
    }

    /// <summary>
    /// 寫入 ref.txt 檔案至啟動紀錄時的主目錄下
    /// </summary>
    public string WriteRefTextFile(string summaryContent)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(_mainSessionDirectory))
            {
                string baseLogs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                _mainSessionDirectory = Path.Combine(baseLogs, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(_mainSessionDirectory);
            }

            string refPath = Path.Combine(_mainSessionDirectory, "ref.txt");
            File.WriteAllText(refPath, summaryContent, Encoding.UTF8);
            return refPath;
        }
    }

    /// <summary>
    /// 取得所有勾選任務之統計摘要字串
    /// </summary>
    public string GetTasksSummaryText()
    {
        lock (_lock)
        {
            if (_taskStats.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("【勾選任務獨立效能量測統計總覽】");

            foreach (var kv in _taskStats)
            {
                var s = kv.Value;
                if (s.SampleCount == 0)
                {
                    sb.AppendLine($"► 任務名稱：{s.TaskName} (未啟動 / 無樣本)");
                    continue;
                }

                float minCpu = s.CpuMin == float.MaxValue ? 0f : s.CpuMin;
                double minMem = s.MemMin == double.MaxValue ? 0.0 : s.MemMin;

                sb.AppendLine($"► 任務名稱：{s.TaskName} (樣本 {s.SampleCount} 筆)");
                sb.AppendLine($"   - CPU 使用率 : 最高 {s.CpuMax:0.0} % | 最低 {minCpu:0.0} % | 平均 {s.CpuAvg:0.0} %");
                sb.AppendLine($"   - 記憶體用量 : 最高 {s.MemMax:0.0} MB | 最低 {minMem:0.0} MB | 平均 {s.MemAvg:0.0} MB");
                sb.AppendLine($"   - 磁碟 I/O 峰值: {s.DiskIoMax:0.0} KB/s | 網路峰值: {s.NetworkMax:0.0} Kbps");
            }

            return sb.ToString();
        }
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim();
    }
}
