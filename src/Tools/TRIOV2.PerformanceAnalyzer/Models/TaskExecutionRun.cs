namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// 單次執行時段（生命週期區間）：精準記錄程式啟動 (起)、停止 (訖) 與該次持續時長
/// 支援處理程序執行後關閉、又再執行的多次重啟場景
/// 製作者: Office of William
/// </summary>
public sealed class TaskExecutionRun
{
    public string TaskName { get; init; } = string.Empty;
    public int RunIndex { get; init; } = 1;
    public int Pid { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public double StartElapsedSeconds { get; init; }
    public double EndElapsedSeconds { get; init; }
    public TimeSpan Duration { get; init; }
    public int SampleCount { get; init; }

    public double CpuAvg { get; init; }
    public double CpuMax { get; init; }
    public double CpuP95 { get; init; }
    public double RamAvgMb { get; init; }
    public double RamMaxMb { get; init; }
    public double DiskPeakKbps { get; init; }
    public double NetPeakKbps { get; init; }

    public string ColorHex { get; init; } = "#F59E0B";

    public string RunLabel => $"第 {RunIndex} 次";
    public string StartTimeText => StartTime != DateTime.MinValue ? StartTime.ToString("HH:mm:ss") : "--";
    public string EndTimeText => EndTime != DateTime.MinValue ? EndTime.ToString("HH:mm:ss") : "--";
    public string TimeRangeText => $"{StartTimeText} ~ {EndTimeText}";
    public string DurationText => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours:00}:{Duration.Minutes:00}:{Duration.Seconds:00}"
        : $"{Duration.Minutes:00}:{Duration.Seconds:00}";
    public string DurationWithSamplesText => SampleCount > 0 ? $"{DurationText} ({SampleCount} 秒)" : DurationText;
    public string CpuDisplay => CpuMax > 0 ? $"{CpuAvg:0.0}% / {CpuMax:0.0}%" : "0.0%";
    public string RamDisplay => RamMaxMb > 0 ? $"{RamMaxMb:0.0} MB" : "0.0 MB";
    public string DiskDisplay => DiskPeakKbps > 0 ? $"{DiskPeakKbps:0.0} KB/s" : "0.0 KB/s";
}

