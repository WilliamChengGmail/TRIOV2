namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// 機器硬體適配評等層級
/// </summary>
public enum SizingLevel
{
    NoData,
    /// <summary>測試時間過短（例如誤觸開始／停止），不足以判斷硬體是否足夠。</summary>
    InsufficientData,
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

    /// <summary>給非技術讀者的一句白話結論（不含 P95 等術語）。</summary>
    public string PlainSummary { get; init; } = string.Empty;

    /// <summary>精簡燈號文字（側欄、表格用）。</summary>
    public string ShortLabel => Level switch
    {
        SizingLevel.Pass => "通過",
        SizingLevel.Warning => "接近上限",
        SizingLevel.UpgradeRequired => "建議升級",
        SizingLevel.InsufficientData => "資料不足",
        _ => "無數據"
    };

    public string BadgeColorHex => Level switch
    {
        SizingLevel.Pass => "#10B981",
        SizingLevel.Warning => "#F59E0B",
        SizingLevel.UpgradeRequired => "#EF4444",
        SizingLevel.InsufficientData => "#52525B",
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
    /// <summary>測試期間最低可用記憶體 ≥ 此值（GB）才算有餘裕；低於此值為「接近上限」。</summary>
    public double AvailableRamPassGb { get; set; } = 2.0;

    /// <summary>測試期間最低可用記憶體 ≤ 此值（GB）為「建議升級」。</summary>
    public double AvailableRamUpgradeGb { get; set; } = 1.0;

    public EvaluationThresholds Clone() => new()
    {
        CpuP95PassLimit = CpuP95PassLimit,
        CpuP95WarnLimit = CpuP95WarnLimit,
        AvailableRamPassGb = AvailableRamPassGb,
        AvailableRamUpgradeGb = AvailableRamUpgradeGb
    };
}

