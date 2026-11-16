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
/// 區間效能量測記錄器 (追蹤指定時間區間內 CPU 與記憶體之最高/最低/平均值，並保存每筆樣本之詳細 Log)
/// 製作者: Office of William
/// </summary>
public sealed class PerformanceSessionRecorder
{
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

    private readonly List<DetailedSampleRecord> _samples = new();

    public bool IsRecording => _isRecording;
    public IReadOnlyList<DetailedSampleRecord> Samples
    {
        get
        {
            lock (_lock)
            {
                return _samples.ToList();
            }
        }
    }

    /// <summary>
    /// 開始新的區間量測紀錄 (重置所有統計計數器與樣本清單)
    /// </summary>
    public void Start()
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
            _samples.Clear();
        }
    }

    /// <summary>
    /// 停止紀錄並結算區間統計
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_isRecording)
            {
                _isRecording = false;
                _endTime = DateTime.Now;
            }
        }
    }

    /// <summary>
    /// 1Hz 採樣滴答時傳入一筆完整樣本數據
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

            // 紀錄詳細樣本日誌
            _samples.Add(new DetailedSampleRecord(
                Index: _sampleCount,
                Timestamp: _endTime,
                TotalCpuPercent: totalCpu,
                SimulatorCpuPercent: simulatorCpu,
                RemainingCpuPercent: remainingCpu,
                UsedMemoryGb: usedMemGb,
                TotalMemoryGb: totalMemGb,
                AvailableMemoryGb: availableMemGb,
                MemoryUsagePercent: memPercent,
                SimulatorMemoryMb: simulatorMemMb,
                IsInferenceActive: isInferenceActive,
                InferenceDurationMs: inferenceDurationMs,
                OperatingMode: operatingMode
            ));
        }
    }

    /// <summary>
    /// 獲取當前統計數據
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
    /// 將本區間所有樣本輸出為 CSV 格式詳細日誌 (自動建立 Logs 目錄，含 UTF-8 BOM 支援 Excel 開啟)
    /// </summary>
    public string SaveDetailedLogToFile(string? targetDir = null, string? customFileName = null)
    {
        List<DetailedSampleRecord> samples;
        PerformanceSessionStats stats;

        lock (_lock)
        {
            samples = _samples.ToList();
            stats = GetStats();
        }

        if (samples.Count == 0) return string.Empty;

        targetDir ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(targetDir);

        string fileName = !string.IsNullOrWhiteSpace(customFileName)
            ? customFileName
            : $"BenchmarkLog_{_startTime:yyyyMMdd_HHmmss}.csv";
        string filePath = Path.Combine(targetDir, fileName);

        var sb = new StringBuilder();

        // 標題表頭
        sb.AppendLine("樣本序號,時間戳記,全系統CPU(%),模擬器CPU(%),剩餘空閒CPU(%),已用記憶體(GB),可用記憶體(GB),記憶體使用率(%),模擬影像緩衝(MB),底層辨識狀態,辨識運算時長(ms),運作模式");

        // 樣本數據行
        foreach (var s in samples)
        {
            string stateText = s.IsInferenceActive ? "辨識運算中" : "待機";
            sb.AppendLine($"{s.Index},{s.Timestamp:yyyy-MM-dd HH:mm:ss},{s.TotalCpuPercent:0.0},{s.SimulatorCpuPercent:0.0},{s.RemainingCpuPercent:0.0},{s.UsedMemoryGb:0.00},{s.AvailableMemoryGb:0.00},{s.MemoryUsagePercent:0},{s.SimulatorMemoryMb:0},{stateText},{s.InferenceDurationMs},{s.OperatingMode}");
        }

        // 區間統計摘要結尾
        sb.AppendLine();
        sb.AppendLine("# ========================================================");
        sb.AppendLine($"# 【TRIOV2 區間效能量測統計摘要】");
        sb.AppendLine($"# 量測開始時間: {_startTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# 量測結束時間: {_endTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# 總樣本筆數: {stats.SampleCount} 筆");
        sb.AppendLine($"# 總紀錄時長: {stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}");
        sb.AppendLine($"# CPU 使用率: 最高 {stats.CpuMax:0.0} %, 最低 {stats.CpuMin:0.0} %, 平均 {stats.CpuAvg:0.0} %");
        sb.AppendLine($"# 記憶體使用量: 最高 {stats.MemoryGbMax:0.00} GB ({stats.MemoryPercentMax:0}%), 最低 {stats.MemoryGbMin:0.00} GB ({stats.MemoryPercentMin:0}%), 平均 {stats.MemoryGbAvg:0.00} GB ({stats.MemoryPercentAvg:0}%)");
        sb.AppendLine("# ========================================================");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }
}
