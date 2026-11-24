using System.IO;
using System.Text;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// 單筆樣本詳細效能日誌記錄
/// </summary>
public record DetailedSampleRecord(
    int Index,
    DateTime Timestamp,
    float TotalCpuPercent,
    float SimulatorCpuPercent,
    float RemainingCpuPercent,
    float UsedMemoryGb,
    float TotalMemoryGb,
    float AvailableMemoryGb,
    float MemoryUsagePercent,
    float SimulatorMemoryMb,
    bool IsInferenceActive,
    int InferenceDurationMs,
    string OperatingMode
);

/// <summary>
/// 區間效能量測統計快照
/// </summary>
public record PerformanceSessionStats(
    bool IsRecording,
    int SampleCount,
    TimeSpan Duration,
    float CpuMax,
    float CpuMin,
    float CpuAvg,
    float MemoryGbMax,
    float MemoryGbMin,
    float MemoryGbAvg,
    float MemoryPercentMax,
    float MemoryPercentMin,
    float MemoryPercentAvg
);

/// <summary>
/// 區間效能量測記錄器 (串流分批寫入磁碟，維持增量純量統計，杜絕記憶體累積與 Memory Swap)
/// 製作者: Office of William
/// </summary>
public sealed class PerformanceSessionRecorder
{
    private const string CsvHeader = "樣本序號,時間戳記,全系統CPU(%),模擬器CPU(%),剩餘空閒CPU(%),已用記憶體(GB),可用記憶體(GB),記憶體使用率(%),模擬影像緩衝(MB),底層辨識狀態,辨識運算時長(ms),運作模式";
    private const int FlushBatchSize = 20; // 每累積 20 筆樣本批次寫入一次，兼顧磁碟 I/O 效能與即時落盤

    private readonly object _lock = new();
    private bool _isRecording;
    private DateTime _startTime;
    private DateTime _endTime;
    private int _sampleCount;

    private float _cpuMax;
    private float _cpuMin = float.MaxValue;
    private double _cpuSum;

    private float _memGbMax;
    private float _memGbMin = float.MaxValue;
    private double _memGbSum;

    private float _memPercentMax;
    private float _memPercentMin = float.MaxValue;
    private double _memPercentSum;

    private string _currentLogFilePath = string.Empty;
    private readonly List<string> _buffer = new(FlushBatchSize);

    public bool IsRecording => _isRecording;
    public string CurrentLogFilePath => _currentLogFilePath;

    /// <summary>
    /// 開始新的區間量測紀錄 (重置所有統計計數器與緩衝區，並預先建立 CSV 檔案與表頭)
    /// </summary>
    public string Start(string? targetDir = null, string? customFileName = null)
    {
        lock (_lock)
        {
            _isRecording = true;
            _startTime = DateTime.Now;
            _endTime = _startTime;
            _sampleCount = 0;
            _cpuMax = 0f;
            _cpuMin = float.MaxValue;
            _cpuSum = 0;
            _memGbMax = 0f;
            _memGbMin = float.MaxValue;
            _memGbSum = 0;
            _memPercentMax = 0f;
            _memPercentMin = float.MaxValue;
            _memPercentSum = 0;
            _buffer.Clear();

            targetDir ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(targetDir);

            string fileName = !string.IsNullOrWhiteSpace(customFileName)
                ? customFileName
                : $"BenchmarkLog_{_startTime:yyyyMMdd_HHmmss}.csv";
            _currentLogFilePath = Path.Combine(targetDir, fileName);

            try
            {
                File.WriteAllText(_currentLogFilePath, CsvHeader + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PerformanceSessionRecorder] Init CSV error: {ex.Message}");
            }

            return _currentLogFilePath;
        }
    }

    /// <summary>
    /// 停止紀錄並結算區間統計，同時 Flush 剩餘緩衝並於 CSV 檔案尾端追加統計摘要區塊
    /// </summary>
    public string Stop()
    {
        lock (_lock)
        {
            if (_isRecording)
            {
                _isRecording = false;
                _endTime = DateTime.Now;
            }

            // 1. 將剩餘未寫入之緩衝刷入磁碟
            FlushBufferCore();

            // 2. 追加統計摘要結尾區塊
            if (!string.IsNullOrEmpty(_currentLogFilePath) && File.Exists(_currentLogFilePath))
            {
                var stats = GetStats();
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("# ========================================================");
                sb.AppendLine($"# 【TRIOV2 區間效能量測統計摘要】");
                sb.AppendLine($"# 量測開始時間: {_startTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# 量測結束時間: {_endTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# 總樣本筆數: {stats.SampleCount} 筆");

                string durationStr = stats.Duration.TotalHours >= 1
                    ? $"{(int)stats.Duration.TotalHours:00}:{stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}"
                    : $"{stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}";
                sb.AppendLine($"# 總紀錄時長: {durationStr}");
                sb.AppendLine($"# CPU 使用率: 最高 {stats.CpuMax:0.0} %, 最低 {stats.CpuMin:0.0} %, 平均 {stats.CpuAvg:0.0} %");
                sb.AppendLine($"# 記憶體使用量: 最高 {stats.MemoryGbMax:0.00} GB ({stats.MemoryPercentMax:0}%), 最低 {stats.MemoryGbMin:0.00} GB ({stats.MemoryPercentMin:0}%), 平均 {stats.MemoryGbAvg:0.00} GB ({stats.MemoryPercentAvg:0}%)");
                sb.AppendLine("# ========================================================");

                try
                {
                    File.AppendAllText(_currentLogFilePath, sb.ToString(), Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PerformanceSessionRecorder] Append footer error: {ex.Message}");
                }
            }

            return _currentLogFilePath;
        }
    }

    /// <summary>
    /// 1Hz 採樣滴答時傳入一筆完整樣本數據 (累積純量統計，並在滿 Batch 閾值時自動寫入磁碟釋放記憶體)
    /// </summary>
    public void AddSample(
        float totalCpu,
        float simulatorCpu,
        float remainingCpu,
        float usedMemGb,
        float totalMemGb,
        float availableMemGb,
        float memPercent,
        float simulatorMemMb,
        bool isInferenceActive,
        int inferenceDurationMs,
        string operatingMode)
    {
        lock (_lock)
        {
            if (!_isRecording) return;

            _sampleCount++;
            _endTime = DateTime.Now;

            // CPU 統計
            if (totalCpu > _cpuMax) _cpuMax = totalCpu;
            if (totalCpu < _cpuMin) _cpuMin = totalCpu;
            _cpuSum += totalCpu;

            // 記憶體 GB 統計
            if (usedMemGb > _memGbMax) _memGbMax = usedMemGb;
            if (usedMemGb < _memGbMin) _memGbMin = usedMemGb;
            _memGbSum += usedMemGb;

            // 記憶體 % 統計
            if (memPercent > _memPercentMax) _memPercentMax = memPercent;
            if (memPercent < _memPercentMin) _memPercentMin = memPercent;
            _memPercentSum += memPercent;

            // 格式化單行 CSV 紀錄
            string stateText = isInferenceActive ? "辨識運算中" : "待機";
            string line = $"{_sampleCount},{_endTime:yyyy-MM-dd HH:mm:ss},{totalCpu:0.0},{simulatorCpu:0.0},{remainingCpu:0.0},{usedMemGb:0.00},{availableMemGb:0.00},{memPercent:0},{simulatorMemMb:0},{stateText},{inferenceDurationMs},{operatingMode}";
            _buffer.Add(line);

            // 適度分批 Flush 至磁碟，並立即釋放記憶體緩衝
            if (_buffer.Count >= FlushBatchSize)
            {
                FlushBufferCore();
            }
        }
    }

    private void FlushBufferCore()
    {
        if (_buffer.Count == 0 || string.IsNullOrEmpty(_currentLogFilePath)) return;

        try
        {
            File.AppendAllLines(_currentLogFilePath, _buffer, Encoding.UTF8);
            _buffer.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerformanceSessionRecorder] Flush log error: {ex.Message}");
        }
    }

    /// <summary>
    /// 獲取當前統計數據 (純量累加計算，不依賴歷史樣本物件)
    /// </summary>
    public PerformanceSessionStats GetStats()
    {
        lock (_lock)
        {
            if (_sampleCount == 0)
            {
                return new PerformanceSessionStats(
                    IsRecording: _isRecording,
                    SampleCount: 0,
                    Duration: TimeSpan.Zero,
                    CpuMax: 0f, CpuMin: 0f, CpuAvg: 0f,
                    MemoryGbMax: 0f, MemoryGbMin: 0f, MemoryGbAvg: 0f,
                    MemoryPercentMax: 0f, MemoryPercentMin: 0f, MemoryPercentAvg: 0f
                );
            }

            TimeSpan duration = _endTime - _startTime;
            float cpuMin = _cpuMin == float.MaxValue ? 0f : _cpuMin;
            float memGbMin = _memGbMin == float.MaxValue ? 0f : _memGbMin;
            float memPercentMin = _memPercentMin == float.MaxValue ? 0f : _memPercentMin;

            return new PerformanceSessionStats(
                IsRecording: _isRecording,
                SampleCount: _sampleCount,
                Duration: duration,
                CpuMax: (float)Math.Round(_cpuMax, 1),
                CpuMin: (float)Math.Round(cpuMin, 1),
                CpuAvg: (float)Math.Round(_cpuSum / _sampleCount, 1),
                MemoryGbMax: (float)Math.Round(_memGbMax, 2),
                MemoryGbMin: (float)Math.Round(memGbMin, 2),
                MemoryGbAvg: (float)Math.Round(_memGbSum / _sampleCount, 2),
                MemoryPercentMax: (float)Math.Round(_memPercentMax, 1),
                MemoryPercentMin: (float)Math.Round(memPercentMin, 1),
                MemoryPercentAvg: (float)Math.Round(_memPercentSum / _sampleCount, 1)
            );
        }
    }

    /// <summary>
    /// 取得目前日誌檔案路徑，或另存至指定目錄 (向後相容保留介面)
    /// </summary>
    public string SaveDetailedLogToFile(string? targetDir = null, string? customFileName = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(_currentLogFilePath) || !File.Exists(_currentLogFilePath))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(targetDir) && string.IsNullOrEmpty(customFileName))
            {
                return _currentLogFilePath;
            }

            targetDir ??= Path.GetDirectoryName(_currentLogFilePath) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(targetDir);
            string fileName = !string.IsNullOrWhiteSpace(customFileName) ? customFileName : Path.GetFileName(_currentLogFilePath);
            string destination = Path.Combine(targetDir, fileName);

            if (!string.Equals(Path.GetFullPath(_currentLogFilePath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(_currentLogFilePath, destination, overwrite: true);
            }

            return destination;
        }
    }
}
