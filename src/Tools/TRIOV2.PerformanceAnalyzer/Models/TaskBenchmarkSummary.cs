using System.ComponentModel;

namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// 單一追蹤任務（Process）的完整效能統計摘要（含全時段與僅運行中雙軌數據）
/// 製作者: Office of William
/// </summary>
public sealed class TaskBenchmarkSummary : INotifyPropertyChanged
{
    private bool _isOverlayVisible;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TaskName { get; init; } = string.Empty;
    public int TotalSamples { get; init; }
    public int ActiveSamples { get; init; }
    public double ActiveRatioPercent => TotalSamples > 0 ? ActiveSamples * 100.0 / TotalSamples : 0.0;

    // 全程統計 (含尚未執行時段)
    public double AllCpuAvg { get; init; }
    public double AllCpuMin { get; init; }
    public double AllCpuMax { get; init; }
    public double AllCpuP95 { get; init; }
    public double AllRamAvgMb { get; init; }
    public double AllRamMaxMb { get; init; }
    public double AllDiskPeakKbps { get; init; }
    public double AllNetPeakKbps { get; init; }

    // 僅限「執行中 / PID > 0」期間真實統計
    public double ActiveCpuAvg { get; init; }
    public double ActiveCpuP95 { get; init; }
    public double ActiveCpuMax { get; init; }
    public double ActiveRamAvgMb { get; init; }
    public double ActiveRamMaxMb { get; init; }
    public double ActiveDiskAvgKbps { get; init; }
    public double ActiveDiskPeakKbps { get; init; }
    public double ActiveNetPeakKbps { get; init; }

    public string ColorHex { get; set; } = "#F59E0B";
    public IReadOnlyList<TaskSamplePoint> Samples { get; init; } = Array.Empty<TaskSamplePoint>();
    public IReadOnlyList<TaskExecutionRun> ExecutionRuns { get; init; } = Array.Empty<TaskExecutionRun>();

    public int ExecutionCount => ExecutionRuns.Count;

    public string ExecutionCountText => ExecutionCount switch
    {
        0 => "未執行 (0 次)",
        1 => "執行 1 次",
        _ => $"執行 {ExecutionCount} 次 (多次啟動)"
    };

    public string TotalActiveDurationText
    {
        get
        {
            var ts = TimeSpan.FromSeconds(ActiveSamples);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes:00}:{ts.Seconds:00}";
            return $"{ActiveSamples} 秒";
        }
    }

    public string ExecutionTimeRangeText
    {
        get
        {
            if (ExecutionRuns.Count == 0) return "--";
            if (ExecutionRuns.Count == 1) return $"{ExecutionRuns[0].TimeRangeText} ({ExecutionRuns[0].DurationText})";
            return $"首次 {ExecutionRuns[0].StartTimeText} ~ 終次 {ExecutionRuns[^1].EndTimeText} (共 {ExecutionCount} 次 / 總長 {TotalActiveDurationText})";
        }
    }

    /// <summary>
    /// 控制是否將該任務曲線疊加繪製於單次時間軸圖表上
    /// </summary>
    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        set
        {
            if (_isOverlayVisible != value)
            {
                _isOverlayVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOverlayVisible)));
            }
        }
    }

    public string ActiveSamplesDisplay => $"{ActiveSamples} / {TotalSamples} ({ActiveRatioPercent:0.#}%)";
    public string CpuAvgDualDisplay => $"{AllCpuAvg:0.0}% / {ActiveCpuAvg:0.0}%";
    public string RamDualDisplay => $"{ActiveRamAvgMb:0.0} / {AllRamMaxMb:0.0} MB";
}

