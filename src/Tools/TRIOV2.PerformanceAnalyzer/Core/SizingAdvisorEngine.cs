using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 根據驗收門檻判定單次測試或跨機聚合效能等級，並提供硬體升級方向建議
/// 製作者: Office of William
/// </summary>
public static class SizingAdvisorEngine
{
    /// <summary>短於此秒數的測試視為「資料不足」，預設不納入設備彙總。</summary>
    public const double MinimumValidSeconds = 10;

    public static bool IsTooShort(BenchmarkSession session)
        => session.SampleCount == 0 || session.Duration.TotalSeconds < MinimumValidSeconds;

    public static SizingVerdict EvaluateSession(BenchmarkSession session, EvaluationThresholds thresholds)
    {
        if (session.SampleCount == 0)
        {
            return NoDataVerdict("無有效採樣", "無足夠數據供評估");
        }

        if (session.Duration.TotalSeconds < MinimumValidSeconds)
        {
            return InsufficientVerdict($"測試僅 {session.Duration.TotalSeconds:0} 秒 (< {MinimumValidSeconds:0} 秒)");
        }

        string topTaskHint = BuildTopTaskHint(session.Tasks);
        return EvaluateMetrics(
            session.CpuP95,
            session.CpuMax,
            session.MinAvailableGb,
            session.RamMaxGb,
            session.HardwareProfile.MemoryInstalledGb,
            thresholds,
            topTaskHint);
    }

    public static SizingVerdict EvaluateDevice(
        double maxCpuP95AcrossRuns,
        double peakCpuAcrossRuns,
        double peakRamPercentAcrossRuns,
        double minAvailableGbAcrossRuns,
        double peakUsedGbAcrossRuns,
        double installedMemoryGb,
        EvaluationThresholds thresholds,
        IReadOnlyList<TaskBenchmarkSummary> topTasks,
        bool hasValidRun = true,
        bool onlyShortRunsExist = false)
    {
        if (onlyShortRunsExist)
        {
            return InsufficientVerdict($"此設備只有短於 {MinimumValidSeconds:0} 秒的測試紀錄");
        }

        if (maxCpuP95AcrossRuns <= 0 && peakRamPercentAcrossRuns <= 0)
        {
            return new SizingVerdict
            {
                Level = SizingLevel.NoData,
                BadgeText = "未納入場次",
                ReasonSummary = "請於左側勾選至少 1 筆有效測試",
                Recommendation = "-",
                PlainSummary = "這台設備目前沒有勾選任何測試，請在左側清單勾選要納入判斷的測試。"
            };
        }

        if (!hasValidRun)
        {
            return InsufficientVerdict($"已勾選的測試皆短於 {MinimumValidSeconds:0} 秒");
        }

        string topTaskHint = BuildTopTaskHint(topTasks);
        return EvaluateMetrics(
            maxCpuP95AcrossRuns,
            peakCpuAcrossRuns,
            minAvailableGbAcrossRuns,
            peakUsedGbAcrossRuns,
            installedMemoryGb,
            thresholds,
            topTaskHint);
    }

    private static SizingVerdict EvaluateMetrics(
        double cpuP95,
        double cpuMax,
        double minAvailableGb,
        double peakUsedGb,
        double installedMemoryGb,
        EvaluationThresholds thresholds,
        string topTaskHint)
    {
        // 記憶體以「最低可用記憶體（GB）」判定：TRIO V2 需要的記憶體量固定，百分比在不同容量的機器上意義不同；
        // 記憶體用盡即使一瞬間也會換頁或關閉程式，所以取最低值（CPU 則用 P95 排除瞬間尖峰）。
        bool ramKnown = !double.IsNaN(minAvailableGb);
        bool cpuUpgrade = cpuP95 > thresholds.CpuP95WarnLimit;
        bool ramUpgrade = ramKnown && minAvailableGb <= thresholds.AvailableRamUpgradeGb;
        bool cpuWarn = !cpuUpgrade && cpuP95 >= thresholds.CpuP95PassLimit;
        bool ramWarn = ramKnown && !ramUpgrade && minAvailableGb < thresholds.AvailableRamPassGb;

        if (cpuUpgrade || ramUpgrade)
        {
            var reasons = new List<string>();
            var recs = new List<string>();

            if (ramUpgrade)
            {
                reasons.Add($"最低可用記憶體 {minAvailableGb:0.0} GB (≤ {thresholds.AvailableRamUpgradeGb:0.#} GB)");
                recs.Add(BuildRamRecommendation(peakUsedGb, installedMemoryGb, thresholds));
            }
            if (cpuUpgrade)
            {
                reasons.Add($"CPU P95 達 {cpuP95:0.0}% (> {thresholds.CpuP95WarnLimit:0.#}%)");
                recs.Add("建議更換較高核心數 / 時脈之 CPU 等級");
            }

            // 另一項指標雖未達升級線，但已接近上限時一併列出，避免只升級一項後仍不夠
            if (cpuWarn)
            {
                reasons.Add($"CPU P95 {cpuP95:0.0}% 接近上限 (≥ {thresholds.CpuP95PassLimit:0.#}%)");
                recs.Add("CPU 也已接近上限，換機時建議一併提高 CPU 等級");
            }
            if (ramWarn)
            {
                reasons.Add($"最低可用記憶體 {minAvailableGb:0.0} GB，低於建議餘裕 {thresholds.AvailableRamPassGb:0.#} GB");
                recs.Add($"記憶體也已接近上限，{BuildRamRecommendation(peakUsedGb, installedMemoryGb, thresholds)}");
            }

            if (!string.IsNullOrEmpty(topTaskHint))
            {
                recs.Add(topTaskHint);
            }

            var parts = new List<string>();
            if (cpuUpgrade) parts.Add("處理器");
            if (ramUpgrade) parts.Add("記憶體");
            string alsoTight = cpuWarn ? "；處理器也已接近上限" : ramWarn ? "；記憶體也已接近上限" : string.Empty;

            return new SizingVerdict
            {
                Level = SizingLevel.UpgradeRequired,
                BadgeText = "建議升級機器等級",
                ReasonSummary = string.Join("；", reasons),
                Recommendation = string.Join("。", recs),
                PlainSummary = $"這台電腦的{string.Join("和", parts)}不足以穩定應付目前的工作量，使用時可能變慢或卡頓，建議提高硬體規格{alsoTight}。"
            };
        }

        if (cpuWarn || ramWarn)
        {
            var reasons = new List<string>();
            if (cpuWarn) reasons.Add($"CPU P95 {cpuP95:0.0}% 接近警戒線 ({thresholds.CpuP95PassLimit:0.#}%)");
            if (ramWarn) reasons.Add($"最低可用記憶體 {minAvailableGb:0.0} GB，低於建議餘裕 {thresholds.AvailableRamPassGb:0.#} GB");

            var recParts = new List<string>();
            if (ramWarn) recParts.Add($"新採購{BuildRamRecommendation(peakUsedGb, installedMemoryGb, thresholds)}");
            recParts.Add(string.IsNullOrEmpty(topTaskHint)
                ? $"建議保留緩衝餘裕或評估次一級規格 (CPU Max={cpuMax:0.0}%)"
                : $"效能接近上限，{topTaskHint}");
            string rec = string.Join("。", recParts);

            var parts = new List<string>();
            if (cpuWarn) parts.Add("處理器");
            if (ramWarn) parts.Add("記憶體");

            return new SizingVerdict
            {
                Level = SizingLevel.Warning,
                BadgeText = "接近滿載警戒",
                ReasonSummary = string.Join("；", reasons),
                Recommendation = rec,
                PlainSummary = $"硬體目前夠用，但{string.Join("和", parts)}已接近上限；若之後功能增加或同時執行更多工作，可能會不夠用。"
            };
        }

        return new SizingVerdict
        {
            Level = SizingLevel.Pass,
            BadgeText = "規格餘裕充足 (PASS)",
            ReasonSummary = ramKnown
                ? $"CPU P95 {cpuP95:0.0}% (<{thresholds.CpuP95PassLimit:0.#}%)、最低可用記憶體 {minAvailableGb:0.0} GB (≥ {thresholds.AvailableRamPassGb:0.#} GB)"
                : $"CPU P95 {cpuP95:0.0}% (<{thresholds.CpuP95PassLimit:0.#}%)；無可用記憶體資料，未判定記憶體",
            Recommendation = "現行機器硬體效能足以負擔工作負載",
            PlainSummary = "這台電腦的硬體足以應付目前的工作量，而且還有餘裕。"
        };
    }

    private static readonly double[] MemoryTiersGb = { 4, 8, 16, 32, 64, 128 };

    /// <summary>
    /// 記憶體建議容量 = 測試最高已用 + 建議餘裕，取下一個常見容量規格（且大於現有容量）。
    /// </summary>
    public static string BuildRamRecommendation(double peakUsedGb, double installedMemoryGb, EvaluationThresholds thresholds)
    {
        if (peakUsedGb <= 0)
        {
            return "建議提高記憶體容量";
        }

        double needed = peakUsedGb + thresholds.AvailableRamPassGb;
        double tier = MemoryTiersGb.FirstOrDefault(t => t >= needed && t > installedMemoryGb + 0.5);
        string target = tier > 0 ? $"{tier:0} GB" : "更高記憶體容量";
        return $"建議記憶體至少 {target}（測試最高用到 {peakUsedGb:0.0} GB，另保留 {thresholds.AvailableRamPassGb:0.#} GB 餘裕）";
    }

    private static SizingVerdict NoDataVerdict(string reason, string recommendation) => new()
    {
        Level = SizingLevel.NoData,
        BadgeText = "無數據",
        ReasonSummary = reason,
        Recommendation = recommendation,
        PlainSummary = "沒有可用的測試資料。"
    };

    private static SizingVerdict InsufficientVerdict(string reason) => new()
    {
        Level = SizingLevel.InsufficientData,
        BadgeText = "資料不足",
        ReasonSummary = reason,
        Recommendation = $"請以完整操作流程重新測試（建議 5 分鐘以上）；短於 {MinimumValidSeconds:0} 秒的紀錄預設不納入設備彙總",
        PlainSummary = "測試時間太短，無法判斷硬體是否足夠，請重新進行完整測試。"
    };

    /// <summary>效能記錄器本身，不列為「主要耗能程序」（它是量測工具，不是受測對象）。</summary>
    private const string RecorderProcessName = "TRIOV2.ResourceSimulator";

    /// <summary>
    /// 累計 CPU 用量（運行中平均 CPU × 運行秒數）。比「瞬間最高值」更能反映誰真正吃掉處理器。
    /// </summary>
    public static double CpuWorkScore(TaskBenchmarkSummary task) => task.ActiveCpuAvg * task.ActiveSamples;

    private static string BuildTopTaskHint(IReadOnlyList<TaskBenchmarkSummary> tasks)
    {
        var topCpuTask = tasks
            .Where(t => t.ActiveSamples > 0
                        && !string.Equals(t.TaskName, RecorderProcessName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(CpuWorkScore)
            .FirstOrDefault();
        if (topCpuTask == null || topCpuTask.ActiveCpuMax <= 5.0) return string.Empty;

        return $"主要耗能程序：{topCpuTask.TaskName} (運行中 CPU 平均 {topCpuTask.ActiveCpuAvg:0.0}% / Peak {topCpuTask.ActiveCpuMax:0.0}%, RAM Peak {topCpuTask.ActiveRamMaxMb:0.#} MB)";
    }
}
