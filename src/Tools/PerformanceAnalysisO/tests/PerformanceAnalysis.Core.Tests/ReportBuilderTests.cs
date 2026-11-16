using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Reporting;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Tests;

public sealed class ReportBuilderTests
{
    [Fact]
    public void BuildsReportFromExistingLogs()
    {
        const string root = @"D:\TRIOV2\publish_tools\Logs";
        if (!Directory.Exists(root))
        {
            return;
        }

        var scan = SessionScanner.Scan(root);
        var store = new SessionMetadataStore(Path.Combine(
            Path.GetTempPath(),
            "PerformanceAnalysisTests",
            "report-metadata"));
        var analyzer = new SessionAnalyzer(store);
        var sessions = scan.Candidates
            .Select(analyzer.Analyze)
            .ToList();

        var report = ReportBuilder.Build(
            sessions,
            root,
            new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.FromHours(8)));

        Assert.Equal(10, report.Sessions.Count);
        Assert.Equal(3, report.Devices.Count);
        Assert.Equal(8, report.TaskCatalog.Count);

        var target = Assert.Single(report.Sessions, session => session.FolderName == "20261112_150751");
        Assert.Equal(625, target.SampleCount);
        Assert.True(target.Series.Count >= 8);
        Assert.NotEmpty(target.Tasks);

        var cpuSeries = Assert.Single(target.Series, series => series.Key == "cpuTotal");
        Assert.True(cpuSeries.Points.Count > 100);
        Assert.All(cpuSeries.Points, point => Assert.Equal(2, point.Length));

        var task = Assert.Single(target.Tasks, task => task.Name == "consumables_benchmark");
        Assert.NotEmpty(task.CpuPoints);
        Assert.NotEmpty(task.MemoryPoints);
        Assert.True(task.RunCount > 0);
    }

    [Fact]
    public void BuildsSingleFileHtmlWithoutExternalScripts()
    {
        const string root = @"D:\TRIOV2\publish_tools\Logs";
        if (!Directory.Exists(root))
        {
            return;
        }

        var scan = SessionScanner.Scan(root);
        var store = new SessionMetadataStore(Path.Combine(
            Path.GetTempPath(),
            "PerformanceAnalysisTests",
            "report-metadata-html"));
        var analyzer = new SessionAnalyzer(store);
        var sessions = scan.Candidates
            .Select(analyzer.Analyze)
            .ToList();

        var report = ReportBuilder.Build(sessions, root);
        var html = HtmlReportBuilder.Build(report);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("id=\"report-data\"", html);
        Assert.Contains("echarts", html);
        Assert.DoesNotContain("{{DATA}}", html);
        Assert.DoesNotContain("{{CSS}}", html);
        Assert.DoesNotContain("{{ECHARTS}}", html);
        Assert.DoesNotContain("{{APP_JS}}", html);
        Assert.DoesNotContain("<script src=", html);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", html);
        Assert.True(html.Length > 500_000);
    }
}
