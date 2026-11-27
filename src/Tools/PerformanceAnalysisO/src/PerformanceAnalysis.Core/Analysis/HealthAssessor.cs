using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Analysis;

/// <summary>
/// 把技術診斷（Diagnostic）轉成非技術人員也看得懂的「結論燈號」與白話說明。
/// 判定只依據既有診斷與場次資料，不另外計算新門檻；門檻仍由「設定」頁控制。
/// </summary>
public static class HealthAssessor
{
    private sealed record Rule(
        FindingImpact Impact,
        FindingCategory Category,
        string Title,
        string Explanation,
        string Suggestion);

    private static readonly IReadOnlyDictionary<string, Rule> Rules = new Dictionary<string, Rule>(StringComparer.Ordinal)
    {
        ["CPU_HIGH_SUSTAINED"] = new(
            FindingImpact.Problem,
            FindingCategory.Performance,
            "處理器長時間滿載",
            "電腦的運算能力有一段時間幾乎被用完，使用者可能感覺操作變慢、畫面卡頓。",
            "請 SW 對照「任務 CPU」圖，找出該時段占用最高的程式並評估優化。"),
        ["CPU_P95_HIGH"] = new(
            FindingImpact.Problem,
            FindingCategory.Performance,
            "處理器負載過高",
            "測試期間處理器大部分時間都很忙，幾乎沒有餘裕，使用時容易變慢或卡頓。",
            "請 SW 對照「任務 CPU」圖找出主要耗用的程式；若程式已無優化空間，評估提高 CPU 等級。"),
        ["CPU_P95_ELEVATED"] = new(
            FindingImpact.Attention,
            FindingCategory.Performance,
            "處理器負載偏高",
            "處理器忙碌的時間偏多，目前仍可運作，但餘裕不多；之後功能增加可能會不夠用。",
            "以完整操作流程再測一次確認；新採購機台建議選擇較高等級的 CPU。"),
        ["RAM_HIGH"] = new(
            FindingImpact.Problem,
            FindingCategory.Performance,
            "記憶體接近用盡",
            "可用記憶體所剩無幾，系統可能明顯變慢，甚至造成程式異常結束。",
            "請 SW 檢查記憶體用量最高的任務，評估優化程式或提高硬體規格。"),
        ["TASK_RESTARTS"] = new(
            FindingImpact.Problem,
            FindingCategory.Performance,
            "程式反覆重新啟動",
            "有程式在測試中多次中止後又重新啟動，代表執行不穩定。",
            "請 SW 查看該程式的錯誤紀錄（EventLog），確認中止原因。"),
        ["MEMORY_GROWTH"] = new(
            FindingImpact.Attention,
            FindingCategory.Performance,
            "記憶體用量持續上升",
            "測試期間記憶體用量一路增加；若長時間運作，可能逐漸把記憶體耗盡（常見原因為記憶體洩漏）。",
            "安排更長時間的測試，確認是否持續上升或會趨於平穩。"),
        ["TASK_NEVER_RUN"] = new(
            FindingImpact.ConfidenceOnly,
            FindingCategory.Coverage,
            "有受監測的程式沒有執行",
            "清單中的程式在整場測試都沒有啟動，這部分的效能無法評估。常見原因是監測清單同時列了替代程式（例如 TRIOV2.App 與 TRIO2026.App），每次只會執行其中一個。",
            "若該程式本來就不需要啟動可忽略；若應該啟動，請檢查測試流程。"),
        ["NO_TASK_LOGS"] = new(
            FindingImpact.Attention,
            FindingCategory.Coverage,
            "沒有個別程式的紀錄",
            "只有整台電腦的資源紀錄，看不到是哪個程式在使用資源。",
            "下次測試請在記錄器中勾選要監測的程式。"),
        ["SHORT_SESSION"] = new(
            FindingImpact.Insufficient,
            FindingCategory.DataQuality,
            "測試時間太短",
            "紀錄只有短短幾秒，不足以代表實際運作情況。",
            "請以完整流程（建議 5 分鐘以上）的測試結果作為判斷依據。"),
        ["NO_SYSTEM_LOG"] = new(
            FindingImpact.Insufficient,
            FindingCategory.DataQuality,
            "缺少主要紀錄檔",
            "找不到整台電腦的資源紀錄（System_BenchmarkLog.csv），無法評估效能。",
            "確認複製資料時是否漏掉檔案，或記錄器是否正常結束。"),
        ["NO_SAMPLES"] = new(
            FindingImpact.Insufficient,
            FindingCategory.DataQuality,
            "紀錄檔內沒有資料",
            "紀錄檔存在但沒有任何有效數據。",
            "確認記錄器是否有實際開始記錄。"),
        ["NO_REF"] = new(
            FindingImpact.ConfidenceOnly,
            FindingCategory.DataQuality,
            "缺少硬體摘要檔",
            "缺少 ref.txt，電腦型號等資訊改由其他檔案推測，可能無法正確辨識是哪台電腦。",
            "複製資料時請整個場次資料夾一起複製。"),
        ["SAMPLE_GAP"] = new(
            FindingImpact.ConfidenceOnly,
            FindingCategory.DataQuality,
            "紀錄中途有中斷",
            "有幾段時間沒有被記錄到，該時段內發生的狀況可能看不到。",
            "若中斷時間很長，建議重新測試。"),
        ["DUPLICATE_TIMESTAMPS"] = new(
            FindingImpact.ConfidenceOnly,
            FindingCategory.DataQuality,
            "部分時間點重複記錄",
            "同一秒出現多筆紀錄，圖表已自動合併，對結論影響通常不大。",
            "不需特別處理；若比例很高，請 SW 檢查記錄器設定。"),
        ["REF_MISMATCH"] = new(
            FindingImpact.ConfidenceOnly,
            FindingCategory.DataQuality,
            "摘要與明細數字不一致",
            "記錄器自己寫的摘要（ref.txt）與本工具從明細重算的數字有落差，本工具以明細重算為準。",
            "不需特別處理；若落差很大，請 SW 檢查紀錄檔是否完整。")
    };

    private static readonly Rule UnknownRule = new(
        FindingImpact.ConfidenceOnly,
        FindingCategory.DataQuality,
        "讀取資料時有小問題",
        "部分紀錄格式異常或無法讀取，已略過這些資料繼續分析。",
        "通常不影響結論；細節請看「診斷與資料品質」。");

    public static SessionHealth Assess(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var findings = new List<HealthFinding>();
        foreach (var diagnostic in session.Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Info)
            {
                continue;
            }

            var rule = Rules.GetValueOrDefault(diagnostic.Code) ?? UnknownRule;
            findings.Add(new HealthFinding(
                rule.Impact,
                rule.Category,
                diagnostic.Code,
                rule.Title,
                rule.Explanation,
                rule.Suggestion,
                diagnostic.Message));
        }

        var insufficient = findings.Any(finding => finding.Impact == FindingImpact.Insufficient)
                           || !session.HasSystemLog
                           || session.Aggregate.SampleCount == 0;

        var level = insufficient
            ? HealthLevel.Insufficient
            : findings.Any(finding => finding.Impact == FindingImpact.Problem)
                ? HealthLevel.Problem
                : findings.Any(finding => finding.Impact == FindingImpact.Attention)
                    ? HealthLevel.Attention
                    : HealthLevel.Good;

        var confidence = insufficient
            ? DataConfidence.Low
            : findings.Any(finding => finding.Impact == FindingImpact.ConfidenceOnly)
                ? DataConfidence.Medium
                : DataConfidence.High;

        var ordered = findings
            .OrderBy(finding => ImpactRank(finding.Impact))
            .ThenBy(finding => finding.Category)
            .ToList();

        return new SessionHealth(level, confidence, BuildHeadline(level, ordered), ordered);
    }

    public static string LevelText(HealthLevel level) => level switch
    {
        HealthLevel.Good => "良好",
        HealthLevel.Attention => "注意",
        HealthLevel.Problem => "異常",
        _ => "資料不足"
    };

    public static string ConfidenceText(DataConfidence confidence) => confidence switch
    {
        DataConfidence.High => "高",
        DataConfidence.Medium => "中",
        _ => "低"
    };

    public static string CategoryText(FindingCategory category) => category switch
    {
        FindingCategory.Performance => "效能",
        FindingCategory.Coverage => "測試涵蓋",
        _ => "資料品質"
    };

    private static int ImpactRank(FindingImpact impact) => impact switch
    {
        FindingImpact.Insufficient => 0,
        FindingImpact.Problem => 1,
        FindingImpact.Attention => 2,
        _ => 3
    };

    private static string BuildHeadline(HealthLevel level, IReadOnlyList<HealthFinding> findings)
    {
        var problems = findings.Where(finding => finding.Impact == FindingImpact.Problem).Select(f => f.Title).Distinct().ToList();
        var attentions = findings.Where(finding => finding.Impact == FindingImpact.Attention).Select(f => f.Title).Distinct().ToList();

        return level switch
        {
            HealthLevel.Good => "效能表現正常，沒有發現需要處理的問題。",
            HealthLevel.Attention => $"整體運作正常，有 {attentions.Count} 項建議留意：{string.Join("、", attentions)}。",
            HealthLevel.Problem => $"發現 {problems.Count} 項效能問題：{string.Join("、", problems)}，建議 SW 進一步檢查。",
            _ => "資料不足，這場紀錄不適合用來判斷效能。"
        };
    }
}

public enum HealthLevel
{
    Good,
    Attention,
    Problem,
    Insufficient
}

public enum DataConfidence
{
    High,
    Medium,
    Low
}

public enum FindingImpact
{
    /// <summary>資料不足以下結論。</summary>
    Insufficient,

    /// <summary>效能問題，需要處理。</summary>
    Problem,

    /// <summary>建議留意。</summary>
    Attention,

    /// <summary>不影響結論燈號，只降低資料可信度。</summary>
    ConfidenceOnly
}

public enum FindingCategory
{
    Performance,
    Coverage,
    DataQuality
}

public sealed record HealthFinding(
    FindingImpact Impact,
    FindingCategory Category,
    string Code,
    string Title,
    string Explanation,
    string Suggestion,
    string Detail);

public sealed record SessionHealth(
    HealthLevel Level,
    DataConfidence Confidence,
    string Headline,
    IReadOnlyList<HealthFinding> Findings)
{
    public string LevelText => HealthAssessor.LevelText(Level);

    public string ConfidenceText => HealthAssessor.ConfidenceText(Confidence);
}
