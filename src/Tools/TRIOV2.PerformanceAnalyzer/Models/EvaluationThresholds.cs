namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// 機器硬體適配評等層級
/// </summary>
public enum SizingLevel
{
    NoData,
    Pass,
    Warning,
    UpgradeRequired
}

/// <summary>
/// 機器效能判定與升級建議結論
/// 製作者: Office of William
/// </summary>
public sealed class SizingVerdict
{
    public SizingLevel Level { get; init; }
    public string BadgeText { get; init; } = "未判定";
    public string ReasonSummary { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
    public string BadgeColorHex => Level switch
    {
        SizingLevel.Pass => "#10B981",
        SizingLevel.Warning => "#F59E0B",
        SizingLevel.UpgradeRequired => "#EF4444",
        _ => "#64748B"
    };
}

/// <summary>
/// TRIO V2 硬體規格驗收門檻設定
/// </summary>
public sealed class EvaluationThresholds
{
    public double CpuP95PassLimit { get; set; } = 70.0;
    public double CpuP95WarnLimit { get; set; } = 85.0;
    public double RamPeakPassLimit { get; set; } = 80.0;
    public double RamPeakWarnLimit { get; set; } = 90.0;

    public EvaluationThresholds Clone() => new()
    {
        CpuP95PassLimit = CpuP95PassLimit,
        CpuP95WarnLimit = CpuP95WarnLimit,
        RamPeakPassLimit = RamPeakPassLimit,
        RamPeakWarnLimit = RamPeakWarnLimit
    };
}

