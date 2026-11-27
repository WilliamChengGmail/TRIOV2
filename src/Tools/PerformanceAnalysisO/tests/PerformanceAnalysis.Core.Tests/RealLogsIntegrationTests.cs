using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Models;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Tests;

public sealed class RealLogsIntegrationTests
{
    private static List<Session> AnalyzeFixtures()
    {
        var scan = SessionScanner.Scan(FixtureLogs.Root);
        var store = new SessionMetadataStore(Path.Combine(
            Path.GetTempPath(),
            "PerformanceAnalysisTests",
            "metadata-" + Guid.NewGuid().ToString("N")));
        var analyzer = new SessionAnalyzer(store);
        return scan.Candidates
            .Select(analyzer.Analyze)
            .OrderByDescending(session => session.StartTime)
            .ToList();
    }

    [Fact]
    public void ParsesFixtureSessions()
    {
        var sessions = AnalyzeFixtures();

        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions.Select(session => session.Device.DeviceKey).Distinct());

        var target = Assert.Single(sessions, session => session.FolderName == FixtureLogs.GapSession);
        Assert.True(target.HasRefTxt);
        Assert.Equal(1002, target.SystemSamples.Count);
        Assert.Equal(8, target.Tasks.Count);
        Assert.InRange(target.Aggregate.CpuTotal.Mean, 55.6, 55.8);
        Assert.InRange(target.Aggregate.CpuTotal.Max, 97.1, 97.3);
        Assert.Contains("J4125", target.Device.CpuModel);
    }

    [Fact]
    public void RecorderGapsAreNotCountedAsRestarts()
    {
        var target = Assert.Single(AnalyzeFixtures(), session => session.FolderName == FixtureLogs.GapSession);

        // 記錄器每約 20 秒漏記 2–3 秒，但 PID 不變：每個任務應只有 1 段執行區間
        Assert.All(
            target.Tasks.Where(task => task.Runs.Count > 0),
            task => Assert.Equal(0, task.Aggregate.RestartCount));
        Assert.DoesNotContain(target.Diagnostics, diagnostic => diagnostic.Code == "TASK_RESTARTS");
    }

    [Fact]
    public void RealPidChangesAreStillCountedAsRestarts()
    {
        var load = Assert.Single(AnalyzeFixtures(), session => session.FolderName == FixtureLogs.LoadSession);

        var trio = Assert.Single(load.Tasks, task => task.Name == "TRIO");
        Assert.True(trio.Aggregate.RestartCount > 0);
        Assert.True(trio.Runs.Select(run => run.Pid).Distinct().Count() > 1);
    }

    [Fact]
    public void FlagsSustainedCpuLoadAndLowMemoryOnWeakDevice()
    {
        var sessions = AnalyzeFixtures();

        var gap = Assert.Single(sessions, session => session.FolderName == FixtureLogs.GapSession);
        Assert.Contains(gap.Diagnostics, diagnostic => diagnostic.Code == "CPU_P95_ELEVATED");
        Assert.Contains(gap.Diagnostics, diagnostic => diagnostic.Code == "RAM_HIGH");
        Assert.Equal(HealthLevel.Problem, HealthAssessor.Assess(gap).Level);

        var load = Assert.Single(sessions, session => session.FolderName == FixtureLogs.LoadSession);
        Assert.Contains(load.Diagnostics, diagnostic => diagnostic.Code == "CPU_P95_ELEVATED");
        Assert.Equal(HealthLevel.Attention, HealthAssessor.Assess(load).Level);
    }

    [Fact]
    public void NeverRunAlternateTasksDoNotRaiseTheLevel()
    {
        var gap = Assert.Single(AnalyzeFixtures(), session => session.FolderName == FixtureLogs.GapSession);
        var health = HealthAssessor.Assess(gap);

        var neverRun = health.Findings.Where(finding => finding.Code == "TASK_NEVER_RUN").ToList();
        Assert.Equal(2, neverRun.Count);
        Assert.All(neverRun, finding => Assert.Equal(FindingImpact.ConfidenceOnly, finding.Impact));
    }
}
