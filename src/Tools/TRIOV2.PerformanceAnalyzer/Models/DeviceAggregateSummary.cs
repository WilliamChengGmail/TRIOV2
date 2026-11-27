namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// 單台設備（跨一到多次測試）的聚合效能量測與規格選型結論
/// 製作者: Office of William
/// </summary>
public sealed class DeviceAggregateSummary
{
    public string DeviceKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DeviceProfile HardwareProfile { get; init; } = new();
    public int IncludedRunCount { get; init; }
    public int TotalRunCount { get; init; }
    public TimeSpan TotalDuration { get; init; }

    public double AvgCpuAcrossRuns { get; init; }
    public double AvgCpuP95AcrossRuns { get; init; }
    public double MaxCpuP95AcrossRuns { get; init; }
    public double PeakCpuAcrossRuns { get; init; }

    public double AvgRamPercentAcrossRuns { get; init; }
    public double PeakRamPercentAcrossRuns { get; init; }
    public double PeakRamGbAcrossRuns { get; init; }

    /// <summary>已勾選測試中最低的可用記憶體（GB）；無資料時為 NaN。</summary>
    public double MinAvailableGbAcrossRuns { get; init; } = double.NaN;

    public SizingVerdict Verdict { get; init; } = new();
    public IReadOnlyList<TaskBenchmarkSummary> TopTasks { get; init; } = Array.Empty<TaskBenchmarkSummary>();
    public IReadOnlyList<BenchmarkSession> Sessions { get; init; } = Array.Empty<BenchmarkSession>();

    public string RunCountDisplay => IncludedRunCount == TotalRunCount
        ? $"{IncludedRunCount} 次"
        : $"{IncludedRunCount} / {TotalRunCount} 次";

    public string CpuSummaryDisplay => $"{AvgCpuAcrossRuns:0.0}% / {MaxCpuP95AcrossRuns:0.0}% / {PeakCpuAcrossRuns:0.0}%";
    public string RamPeakDisplay => $"{PeakRamGbAcrossRuns:0.00} GB ({PeakRamPercentAcrossRuns:0}%)";

    public string MinAvailableDisplay => double.IsNaN(MinAvailableGbAcrossRuns) ? "--" : $"{MinAvailableGbAcrossRuns:0.0} GB";
}
