using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 依設備指紋自動分組與聚合多場次數據
/// 製作者: Office of William
/// </summary>
public static class DeviceGroupingEngine
{
    public static List<DeviceAggregateSummary> BuildDeviceAggregates(
        IEnumerable<BenchmarkSession> allVisibleSessions,
        EvaluationThresholds thresholds)
    {
        var list = new List<DeviceAggregateSummary>();

        var groups = allVisibleSessions
            .GroupBy(s => s.EffectiveDeviceKey, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var allRuns = group.OrderBy(s => s.MeasuredAt).ToList();
            if (allRuns.Count == 0) continue;

            // 優先挑選含 ref.txt 的最新硬體設定檔作為該機代表規格
            var representative = allRuns
                .OrderByDescending(s => s.HasRefTxt)
                .ThenByDescending(s => s.MeasuredAt)
                .First();

            var includedRuns = allRuns.Where(s => s.IsIncluded).ToList();
            foreach (var run in allRuns)
            {
                run.Verdict = SizingAdvisorEngine.EvaluateSession(run, thresholds);
            }

            double avgCpu = includedRuns.Count > 0 ? includedRuns.Average(s => s.CpuAvg) : 0;
            double avgCpuP95 = includedRuns.Count > 0 ? includedRuns.Average(s => s.CpuP95) : 0;
            double maxCpuP95 = includedRuns.Count > 0 ? includedRuns.Max(s => s.CpuP95) : 0;
            double peakCpu = includedRuns.Count > 0 ? includedRuns.Max(s => s.CpuMax) : 0;

            double avgRamPct = includedRuns.Count > 0 ? includedRuns.Average(s => s.RamAvgPercent) : 0;
            double peakRamPct = includedRuns.Count > 0 ? includedRuns.Max(s => s.RamPeakPercent) : 0;
            double peakRamGb = includedRuns.Count > 0 ? includedRuns.Max(s => s.RamMaxGb) : 0;
            var availableRuns = includedRuns.Where(s => s.HasAvailableMemory).ToList();
            double minAvailableGb = availableRuns.Count > 0 ? availableRuns.Min(s => s.MinAvailableGb) : double.NaN;

            TimeSpan totalDur = TimeSpan.FromSeconds(includedRuns.Sum(s => s.Duration.TotalSeconds));

            // 彙整該台設備所有已勾選場次中最消耗資源的任務排行
            var topTasks = AggregateTasksAcrossRuns(includedRuns);

            var verdict = SizingAdvisorEngine.EvaluateDevice(
                maxCpuP95,
                peakCpu,
                peakRamPct,
                minAvailableGb,
                peakRamGb,
                representative.HardwareProfile.MemoryInstalledGb,
                thresholds,
                topTasks,
                hasValidRun: includedRuns.Any(s => !SizingAdvisorEngine.IsTooShort(s)),
                onlyShortRunsExist: includedRuns.Count == 0 && allRuns.All(SizingAdvisorEngine.IsTooShort));

            list.Add(new DeviceAggregateSummary
            {
                DeviceKey = group.Key,
                DisplayName = representative.EffectiveDeviceDisplayName,
                HardwareProfile = representative.HardwareProfile,
                IncludedRunCount = includedRuns.Count,
                TotalRunCount = allRuns.Count,
                TotalDuration = totalDur,
                AvgCpuAcrossRuns = avgCpu,
                AvgCpuP95AcrossRuns = avgCpuP95,
                MaxCpuP95AcrossRuns = maxCpuP95,
                PeakCpuAcrossRuns = peakCpu,
                AvgRamPercentAcrossRuns = avgRamPct,
                PeakRamPercentAcrossRuns = peakRamPct,
                PeakRamGbAcrossRuns = peakRamGb,
                MinAvailableGbAcrossRuns = minAvailableGb,
                Verdict = verdict,
                TopTasks = topTasks,
                Sessions = allRuns
            });
        }

        return list
            .OrderByDescending(d => d.HardwareProfile.HasRefTxt)
            .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<TaskBenchmarkSummary> AggregateTasksAcrossRuns(IReadOnlyList<BenchmarkSession> runs)
    {
        if (runs.Count == 0) return new List<TaskBenchmarkSummary>();

        var taskGroups = runs
            .SelectMany(r => r.Tasks)
            .GroupBy(t => t.TaskName, StringComparer.OrdinalIgnoreCase);

        var result = new List<TaskBenchmarkSummary>();
        foreach (var tg in taskGroups)
        {
            var items = tg.ToList();
            var activeItems = items.Where(x => x.ActiveSamples > 0).ToList();

            result.Add(new TaskBenchmarkSummary
            {
                TaskName = items[0].TaskName,
                TotalSamples = items.Sum(x => x.TotalSamples),
                ActiveSamples = items.Sum(x => x.ActiveSamples),
                AllCpuAvg = items.Average(x => x.AllCpuAvg),
                AllCpuMin = items.Min(x => x.AllCpuMin),
                AllCpuMax = items.Max(x => x.AllCpuMax),
                AllCpuP95 = items.Max(x => x.AllCpuP95),
                AllRamAvgMb = items.Average(x => x.AllRamAvgMb),
                AllRamMaxMb = items.Max(x => x.AllRamMaxMb),
                AllDiskPeakKbps = items.Max(x => x.AllDiskPeakKbps),
                AllNetPeakKbps = items.Max(x => x.AllNetPeakKbps),
                ActiveCpuAvg = activeItems.Count > 0 ? activeItems.Average(x => x.ActiveCpuAvg) : 0,
                ActiveCpuP95 = activeItems.Count > 0 ? activeItems.Max(x => x.ActiveCpuP95) : 0,
                ActiveCpuMax = activeItems.Count > 0 ? activeItems.Max(x => x.ActiveCpuMax) : 0,
                ActiveRamAvgMb = activeItems.Count > 0 ? activeItems.Average(x => x.ActiveRamAvgMb) : 0,
                ActiveRamMaxMb = activeItems.Count > 0 ? activeItems.Max(x => x.ActiveRamMaxMb) : 0,
                ActiveDiskAvgKbps = activeItems.Count > 0 ? activeItems.Average(x => x.ActiveDiskAvgKbps) : 0,
                ActiveDiskPeakKbps = activeItems.Count > 0 ? activeItems.Max(x => x.ActiveDiskPeakKbps) : 0,
                ActiveNetPeakKbps = activeItems.Count > 0 ? activeItems.Max(x => x.ActiveNetPeakKbps) : 0,
                ExecutionRuns = items.SelectMany(x => x.ExecutionRuns).OrderBy(r => r.StartTime).ToList()
            });
        }

        // 依累計 CPU 用量排序（平均 × 運行時間），避免偶發尖峰的程式排在最前面
        return result
            .OrderByDescending(SizingAdvisorEngine.CpuWorkScore)
            .ThenByDescending(t => t.ActiveRamMaxMb)
            .ToList();
    }
}

