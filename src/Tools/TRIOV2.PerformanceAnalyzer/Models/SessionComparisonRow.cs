namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// 並排差異比對列（相對於選取之基準場次計算 CPU / RAM 增減幅度）
/// 製作者: Office of William
/// </summary>
public sealed class SessionComparisonRow
{
    public BenchmarkSession Session { get; init; } = null!;
    public string SessionLabel { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string CpuModel { get; init; } = string.Empty;
    public string RamSpec { get; init; } = string.Empty;
    public string DurationText { get; init; } = string.Empty;

    public double CpuAvg { get; init; }
    public double CpuP95 { get; init; }
    public double CpuMax { get; init; }
    public double RamPeakPercent { get; init; }
    public double RamPeakGb { get; init; }
    public string MinAvailableText { get; init; } = "--";

    public string CpuP95DeltaText { get; init; } = "--";
    public string RamPeakDeltaText { get; init; } = "--";
    public string VerdictBadge { get; init; } = string.Empty;
    public string VerdictPlain { get; init; } = string.Empty;
    public string VerdictColorHex { get; init; } = "#64748B";
}

