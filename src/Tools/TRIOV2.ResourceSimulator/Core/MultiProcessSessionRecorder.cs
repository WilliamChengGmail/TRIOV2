using System.IO;
using System.Text;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// 單一行程單筆取樣數據記錄
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
/// 多任務階層式目錄與獨立 CSV 記錄引擎
/// 製作者: Office of William
/// </summary>
public sealed class MultiProcessSessionRecorder
{
    private const string CsvHeader = "樣本序號,時間戳記,處理程序名稱,PID,狀態,CPU使用率(%),記憶體使用量(MB),磁碟讀寫速率(KB/s),網路傳輸速率(Kbps),電源用量,電源用量趨勢";

    private readonly object _lock = new();
    private bool _isRecording;
    private DateTime _startTime;
    private DateTime _endTime;
    private int _sampleIndex;
    private string _mainSessionDirectory = string.Empty;

    // TaskName -> 該任務之樣本緩衝
    private readonly Dictionary<string, List<ProcessSampleRecord>> _taskSamples = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TaskSummaryStats> _taskStats = new(StringComparer.OrdinalIgnoreCase);

    public bool IsRecording => _isRecording;
    public string MainSessionDirectory => _mainSessionDirectory;
    public DateTime StartTime => _startTime;
    public DateTime EndTime => _endTime;

    /// <summary>
    /// 啟動紀錄工作階段：以 yyyyMMdd_HHmmss 建立主目錄，並立即根據每個勾選的 task name 建立各自子資料夾
    /// </summary>
    public string Start(IEnumerable<ProcessMonitorItem> selectedTasks, string? baseLogsDir = null)
    {
        lock (_lock)
        {
            _isRecording = true;
            _startTime = DateTime.Now;
            _endTime = _startTime;
            _sampleIndex = 0;
            _taskSamples.Clear();
            _taskStats.Clear();

            baseLogsDir ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            string sessionFolderName = _startTime.ToString("yyyyMMdd_HHmmss");
            _mainSessionDirectory = Path.Combine(baseLogsDir, sessionFolderName);

            // 1. 建立主資料夾
            Directory.CreateDirectory(_mainSessionDirectory);

            // 2. 針對每個被勾選的任務名稱，立即建立各自的資料夾
            foreach (var task in selectedTasks)
            {
                if (string.IsNullOrWhiteSpace(task.ProcessName)) continue;

                string safeTaskName = SanitizeFolderName(task.ProcessName);
                string taskSubDir = Path.Combine(_mainSessionDirectory, safeTaskName);
                Directory.CreateDirectory(taskSubDir);

                // 立即預先建立包含表頭的 CSV 檔案，確保啟動當下檔案與資料夾立即可見
                string initialCsvPath = Path.Combine(taskSubDir, $"{safeTaskName}_BenchmarkLog.csv");
                if (!File.Exists(initialCsvPath))
                {
                    File.WriteAllText(initialCsvPath, CsvHeader + Environment.NewLine, Encoding.UTF8);
                }

                if (!_taskSamples.ContainsKey(task.ProcessName))
                {
                    _taskSamples[task.ProcessName] = new List<ProcessSampleRecord>();
                    _taskStats[task.ProcessName] = new TaskSummaryStats { TaskName = task.ProcessName };
                }
            }

            return _mainSessionDirectory;
        }
    }

    /// <summary>
    /// 1Hz 採樣滴答：記錄各勾選行程之效能數據
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
                if (!_taskSamples.TryGetValue(item.ProcessName, out var sampleList))
                {
                    // 若是紀錄中途被加入的任務，動態補建子目錄與初值
                    string safeTaskName = SanitizeFolderName(item.ProcessName);
                    string taskSubDir = Path.Combine(_mainSessionDirectory, safeTaskName);
                    Directory.CreateDirectory(taskSubDir);

                    sampleList = new List<ProcessSampleRecord>();
                    _taskSamples[item.ProcessName] = sampleList;
                    _taskStats[item.ProcessName] = new TaskSummaryStats { TaskName = item.ProcessName };
                }

                var record = new ProcessSampleRecord(
                    SampleIndex: _sampleIndex,
                    Timestamp: _endTime,
                    ProcessName: item.ProcessName,
                    ProcessId: item.ProcessId,
                    StatusText: item.StatusText,
                    CpuPercent: item.CpuPercent,
                    MemoryMb: item.MemoryMb,
                    DiskIoKbPerSec: item.DiskIoKbPerSec,
                    NetworkKbps: item.NetworkKbps,
                    PowerUsage: item.PowerUsage,
                    PowerTrend: item.PowerTrend
                );

                sampleList.Add(record);

                // 更新統計
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
            }
        }
    }

    /// <summary>
    /// 停止紀錄並將各任務數據 Flush 寫入各自子目錄下的 CSV 檔案
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

            foreach (var kv in _taskSamples)
            {
                string taskName = kv.Key;
                var samples = kv.Value;

                string safeTaskName = SanitizeFolderName(taskName);
                string taskSubDir = Path.Combine(_mainSessionDirectory, safeTaskName);
                Directory.CreateDirectory(taskSubDir);

                string csvPath = Path.Combine(taskSubDir, $"{safeTaskName}_BenchmarkLog.csv");

                var sb = new StringBuilder();
                sb.AppendLine(CsvHeader);

                foreach (var s in samples)
                {
                    sb.AppendLine($"{s.SampleIndex},{s.Timestamp:yyyy-MM-dd HH:mm:ss},{s.ProcessName},{s.ProcessId},{s.StatusText},{s.CpuPercent:0.0},{s.MemoryMb:0.0},{s.DiskIoKbPerSec:0.0},{s.NetworkKbps:0.0},{s.PowerUsage},{s.PowerTrend}");
                }

                // 任務摘要結尾
                if (_taskStats.TryGetValue(taskName, out var stat))
                {
                    float minCpu = stat.CpuMin == float.MaxValue ? 0f : stat.CpuMin;
                    double minMem = stat.MemMin == double.MaxValue ? 0.0 : stat.MemMin;

                    sb.AppendLine();
                    sb.AppendLine("# ========================================================");
                    sb.AppendLine($"# 【任務：{taskName} 效能量測統計摘要】");
                    sb.AppendLine($"# 總樣本數: {stat.SampleCount} 筆");
                    sb.AppendLine($"# CPU 使用率: 最高 {stat.CpuMax:0.0} %, 最低 {minCpu:0.0} %, 平均 {stat.CpuAvg:0.0} %");
                    sb.AppendLine($"# 記憶體使用量: 最高 {stat.MemMax:0.0} MB, 最低 {minMem:0.0} MB, 平均 {stat.MemAvg:0.0} MB");
                    sb.AppendLine($"# 磁碟讀寫峰值: {stat.DiskIoMax:0.0} KB/s, 網路傳輸峰值: {stat.NetworkMax:0.0} Kbps");
                    sb.AppendLine("# ========================================================");
                }

                File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
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
