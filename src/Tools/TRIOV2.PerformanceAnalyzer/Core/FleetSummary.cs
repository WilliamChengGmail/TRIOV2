using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 多台設備的白話總結（畫面與匯出報告共用同一套說法）。
/// </summary>
public sealed record FleetSummary(
    int UpgradeCount,
    int WarnCount,
    int PassCount,
    int InsufficientCount,
    string Sentence)
{
    public const string LevelLegend =
        "通過＝硬體夠用且有餘裕；接近上限＝目前夠用但餘裕不多；建議升級＝硬體不足，使用時可能卡頓；資料不足＝測試太短無法判斷。";

    public static FleetSummary Build(IReadOnlyList<DeviceAggregateSummary> groups)
    {
        var active = groups.Where(g => g.IncludedRunCount > 0).ToList();
        int upgrade = active.Count(g => g.Verdict.Level == SizingLevel.UpgradeRequired);
        int warn = active.Count(g => g.Verdict.Level == SizingLevel.Warning);
        int pass = active.Count(g => g.Verdict.Level == SizingLevel.Pass);
        int insufficient = groups.Count - upgrade - warn - pass;
        int evaluated = upgrade + warn + pass;

        string sentence;
        if (groups.Count == 0)
        {
            sentence = "尚未載入測試資料。請在上方選擇記錄器產生的 Logs 資料夾，或直接把資料夾拖進視窗。";
        }
        else if (evaluated == 0)
        {
            sentence = "目前沒有足夠的測試資料可以判斷硬體是否足夠（測試太短或未勾選）。";
        }
        else if (upgrade > 0)
        {
            sentence = $"已評估 {evaluated} 台設備，其中 {upgrade} 台硬體不足、建議升級"
                       + (warn > 0 ? $"，{warn} 台接近上限" : string.Empty)
                       + (pass > 0 ? $"，{pass} 台通過" : string.Empty) + "。";
        }
        else if (warn > 0)
        {
            sentence = $"已評估 {evaluated} 台設備，硬體都夠用；其中 {warn} 台已接近上限，需要留意。";
        }
        else
        {
            sentence = $"已評估 {evaluated} 台設備，硬體都夠用，而且還有餘裕。";
        }

        return new FleetSummary(upgrade, warn, pass, insufficient, sentence);
    }
}
