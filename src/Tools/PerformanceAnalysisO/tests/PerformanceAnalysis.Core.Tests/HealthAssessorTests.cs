using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Tests;

public sealed class HealthAssessorTests
{
    [Fact]
    public void NoWarningsIsGoodWithHighConfidence()
    {
        var health = HealthAssessor.Assess(CreateSession(
            new Diagnostic(DiagnosticSeverity.Info, "CSV_COMMENTS", "ignored")));

        Assert.Equal(HealthLevel.Good, health.Level);
        Assert.Equal(DataConfidence.High, health.Confidence);
        Assert.Empty(health.Findings);
    }

    [Fact]
    public void PerformanceProblemOutranksAttention()
    {
        var health = HealthAssessor.Assess(CreateSession(
            new Diagnostic(DiagnosticSeverity.Warning, "MEMORY_GROWTH", "growth"),
            new Diagnostic(DiagnosticSeverity.Warning, "CPU_HIGH_SUSTAINED", "cpu")));

        Assert.Equal(HealthLevel.Problem, health.Level);
        Assert.Equal("CPU_HIGH_SUSTAINED", health.Findings[0].Code);
        Assert.Contains("處理器長時間滿載", health.Headline);
    }

    [Fact]
    public void MemoryGrowthAloneIsAttention()
    {
        var health = HealthAssessor.Assess(CreateSession(
            new Diagnostic(DiagnosticSeverity.Warning, "MEMORY_GROWTH", "growth")));

        Assert.Equal(HealthLevel.Attention, health.Level);
        Assert.Equal(DataConfidence.High, health.Confidence);
    }

    [Fact]
    public void NeverRunTaskDoesNotChangeLevelButLowersConfidence()
    {
        var health = HealthAssessor.Assess(CreateSession(
            new Diagnostic(DiagnosticSeverity.Warning, "TASK_NEVER_RUN", "alt task")));

        Assert.Equal(HealthLevel.Good, health.Level);
        Assert.Equal(DataConfidence.Medium, health.Confidence);
    }

    [Theory]
    [InlineData("CPU_P95_ELEVATED", HealthLevel.Attention)]
    [InlineData("CPU_P95_HIGH", HealthLevel.Problem)]
    [InlineData("RAM_LOW_HEADROOM", HealthLevel.Attention)]
    [InlineData("RAM_HIGH", HealthLevel.Problem)]
    public void CpuP95FindingsMapToLevels(string code, HealthLevel expected)
    {
        var health = HealthAssessor.Assess(CreateSession(
            new Diagnostic(DiagnosticSeverity.Warning, code, "cpu")));

        Assert.Equal(expected, health.Level);
    }

    [Fact]
    public void ShortSessionIsInsufficientEvenWithProblems()
    {
        var health = HealthAssessor.Assess(CreateSession(
            new Diagnostic(DiagnosticSeverity.Warning, "CPU_HIGH_SUSTAINED", "cpu"),
            new Diagnostic(DiagnosticSeverity.Warning, "SHORT_SESSION", "short")));

        Assert.Equal(HealthLevel.Insufficient, health.Level);
        Assert.Equal(DataConfidence.Low, health.Confidence);
    }

    [Fact]
    public void MissingSystemLogIsInsufficient()
    {
        var health = HealthAssessor.Assess(CreateSession(hasSystemLog: false));

        Assert.Equal(HealthLevel.Insufficient, health.Level);
    }

    [Fact]
    public void DataQualityIssuesOnlyLowerConfidence()
    {
        var health = HealthAssessor.Assess(CreateSession(
            new Diagnostic(DiagnosticSeverity.Warning, "NO_REF", "no ref"),
            new Diagnostic(DiagnosticSeverity.Error, "SOMETHING_NEW", "unknown")));

        Assert.Equal(HealthLevel.Good, health.Level);
        Assert.Equal(DataConfidence.Medium, health.Confidence);
        Assert.Equal(2, health.Findings.Count);
        Assert.All(health.Findings, finding => Assert.False(string.IsNullOrWhiteSpace(finding.Explanation)));
    }

    private static Session CreateSession(params Diagnostic[] diagnostics)
        => CreateSession(true, diagnostics);

    private static Session CreateSession(bool hasSystemLog, params Diagnostic[] diagnostics)
    {
        var start = new DateTime(2026, 11, 12, 15, 0, 0);
        var aggregate = SessionAggregate.Empty with { SampleCount = hasSystemLog ? 600 : 0 };

        return new Session
        {
            SessionKey = "test",
            FolderPath = @"C:\logs\20261112_150000",
            FolderName = "20261112_150000",
            StartTime = start,
            EndTime = start.AddMinutes(10),
            Duration = TimeSpan.FromMinutes(10),
            SystemSamples = Array.Empty<SystemSample>(),
            Tasks = Array.Empty<TaskLog>(),
            Device = new DeviceProfile("dev", "PC", null, null, null, null, null, null, null, null, null, null, "fp", true),
            Aggregate = aggregate,
            Diagnostics = diagnostics,
            HasRefTxt = true,
            HasSystemLog = hasSystemLog
        };
    }
}
