using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Tests;

public sealed class RealLogsIntegrationTests
{
    [Fact]
    public void ParsesExistingTenSessions()
    {
        const string root = @"D:\TRIOV2\publish_tools\Logs";
        if (!Directory.Exists(root))
        {
            return;
        }

        var scan = SessionScanner.Scan(root);
        Assert.Equal(10, scan.Candidates.Count);

        var store = new SessionMetadataStore(Path.Combine(Path.GetTempPath(), "PerformanceAnalysisTests", "metadata"));
        var analyzer = new SessionAnalyzer(store);
        var sessions = scan.Candidates
            .Select(analyzer.Analyze)
            .OrderByDescending(session => session.StartTime)
            .ToList();

        var target = Assert.Single(sessions, session => session.FolderName == "20261112_150751");
        Assert.Equal(625, target.SystemSamples.Count);
        Assert.Equal(6, target.Tasks.Count);
        Assert.Equal(625, target.Aggregate.ExpectedSampleCount);
        Assert.InRange(target.Aggregate.CpuTotal.Mean, 29.3, 29.5);
        Assert.InRange(target.Aggregate.CpuTotal.Max, 97.9, 98.1);
        Assert.InRange(target.Aggregate.MemoryUsedGb.Max, 11.09, 11.11);
        Assert.Equal(10, sessions.Count);

        var oldSession = Assert.Single(sessions, session => session.FolderName == "20261109_162306");
        Assert.False(oldSession.HasRefTxt);
        Assert.Empty(oldSession.Tasks);
    }
}
