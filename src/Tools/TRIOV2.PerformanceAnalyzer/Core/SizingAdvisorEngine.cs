using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 根據驗收門檻判定單次測試或跨機聚合效能等級，並提供硬體升級方向建議
/// 製作者: Office of William
/// </summary>
public static class SizingAdvisorEngine
{
    public static SizingVerdict EvaluateSession(BenchmarkSession session, EvaluationThresholds thresholds)
    {
        if (session.SampleCount == 0)
        {
            return new SizingVerdict
            {
                Level = SizingLevel.NoData,
                BadgeText = "無數據",
                ReasonSummary = "無有效採樣",
                Recommendation = "無足夠數據供評估"
            };
        }

        string topTaskHint = BuildTopTaskHint(session.Tasks);
        return EvaluateMetrics(
            session.CpuP95,
            session.CpuMax,
            session.RamPeakPercent,
            session.HardwareProfile.MemoryInstalledGb,
            thresholds,
            topTaskHint);
    }

    public static SizingVerdict EvaluateDevice(
        double maxCpuP95AcrossRuns,
        double peakCpuAcrossRuns,
        double peakRamPercentAcrossRuns,
        double installedMemoryGb,
        EvaluationThresholds thresholds,
        IReadOnlyList<TaskBenchmarkSummary> topTasks)
    {
        if (maxCpuP95AcrossRuns <= 0 && peakRamPercentAcrossRuns <= 0)
        {
            return new SizingVerdict
            {
                Level = SizingLevel.NoData,
                BadgeText = "未納入場次",
                ReasonSummary = "請於左側勾選至少 1 筆有效測試",
                Recommendation = "-"
            };
        }

        string topTaskHint = BuildTopTaskHint(topTasks);
        return EvaluateMetrics(
            maxCpuP95AcrossRuns,
            peakCpuAcrossRuns,
            peakRamPercentAcrossRuns,
            installedMemoryGb,
            thresholds,
            topTaskHint);
    }

    private static SizingVerdict EvaluateMetrics(
        double cpuP95,
        double cpuMax,
        double ramPeakPct,
        double installedMemoryGb,
        EvaluationThresholds thresholds,
        string topTaskHint)
    {
        bool cpuUpgrade = cpuP95 > thresholds.CpuP95WarnLimit;
        bool ramUpgrade = ramPeakPct > thresholds.RamPeakWarnLimit;
        bool cpuWarn = !cpuUpgrade && cpuP95 >= thresholds.CpuP95PassLimit;
        bool ramWarn = !ramUpgrade && ramPeakPct >= thresholds.RamPeakPassLimit;

        if (cpuUpgrade || ramUpgrade)
        {
            var reasons = new List<string>();
            var recs = new List<string>();

            if (ramUpgrade)
            {
                reasons.Add($"RAM 峰值達 {ramPeakPct:0.#}% (> {thresholds.RamPeakWarnLimit:0.#}%)");
                string targetRam = installedMemoryGb > 0 && installedMemoryGb <= 8.5 ? "16 GB" :
                                   installedMemoryGb > 8.5 && installedMemoryGb <= 16.5 ? "32 GB" : "更高記憶體容量";
                recs.Add($"建議升級記憶體至 {targetRam}");
            }
            if (cpuUpgrade)
            {
                reasons.Add($"CPU P95 達 {cpuP95:0.0}% (> {thresholds.CpuP95WarnLimit:0.#}%)");
                recs.Add("建議更換較高核心數 / 時脈之 CPU 等級");
            }

            if (!string.IsNullOrEmpty(topTaskHint))
            {
                recs.Add(topTaskHint);
            }

            return new SizingVerdict
            {
                Level = SizingLevel.UpgradeRequired,
                BadgeText = "建議升級機器等級",
                ReasonSummary = string.Join("；", reasons),
                Recommendation = string.Join("。", recs)
            };
        }

        if (cpuWarn || ramWarn)
        {
            var reasons = new List<string>();
            if (cpuWarn) reasons.Add($"CPU P95 {cpuP95:0.0}% 接近警戒線 ({thresholds.CpuP95PassLimit:0.#}%)");
            if (ramWarn) reasons.Add($"RAM 峰值 {ramPeakPct:0.#}% 接近警戒線 ({thresholds.RamPeakPassLimit:0.#}%)");

            string rec = string.IsNullOrEmpty(topTaskHint)
                ? $"建議保留緩衝餘裕或評估次一級規格 (CPU Max={cpuMax:0.0}%)"
                : $"效能接近上限，{topTaskHint}";

            return new SizingVerdict
            {
                Level = SizingLevel.Warning,
                BadgeText = "接近滿載警戒",
                ReasonSummary = string.Join("；", reasons),
                Recommendation = rec
            };
        }

        return new SizingVerdict
        {
            Level = SizingLevel.Pass,
            BadgeText = "規格餘裕充足 (PASS)",
            ReasonSummary = $"CPU P95 {cpuP95:0.0}% (<{thresholds.CpuP95PassLimit:0.#}%)、RAM 峰值 {ramPeakPct:0.#}% (<{thresholds.RamPeakPassLimit:0.#}%)",
            Recommendation = "現行機器硬體效能足以負擔工作負載"
        };
    }

    private static string BuildTopTaskHint(IReadOnlyList<TaskBenchmarkSummary> tasks)
    {
        var topCpuTask = tasks.Where(t => t.ActiveSamples > 0).OrderByDescending(t => t.ActiveCpuMax).FirstOrDefault();
        if (topCpuTask == null || topCpuTask.ActiveCpuMax <= 5.0) return string.Empty;

        return $"主要耗能程序：{topCpuTask.TaskName} (CPU Peak {topCpuTask.ActiveCpuMax:0.0}%, RAM Peak {topCpuTask.ActiveRamMaxMb:0.#} MB)";
    }
}
