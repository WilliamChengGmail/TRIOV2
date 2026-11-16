using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Tests;

public sealed class TaskRunBuilderTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 0, 0, 0);

    [Fact]
    public void SplitsRunsOnStopGapAndPidChange()
    {
        var samples = new[]
        {
            Sample(0, TaskRunState.NotRunning),
            Sample(1, TaskRunState.Running, 100),
            Sample(2, TaskRunState.Running, 100),
            Sample(3, TaskRunState.Running, 100),
            Sample(4, TaskRunState.NotRunning),
            Sample(5, TaskRunState.Running, 100),
            Sample(6, TaskRunState.Running, 200),
            Sample(7, TaskRunState.Running, 200)
        };

        var runs = TaskRunBuilder.Build(samples);

        Assert.Equal(3, runs.Count);
        Assert.Equal(TimeSpan.FromSeconds(3), runs[0].Duration);
        Assert.Equal(TimeSpan.FromSeconds(1), runs[1].Duration);
        Assert.Equal(TimeSpan.FromSeconds(2), runs[2].Duration);
        Assert.Equal(200, runs[2].Pid);
    }

    [Fact]
    public void SplitsWhenGapExceedsThreshold()
    {
        var samples = new[]
        {
            Sample(0, TaskRunState.Running, 100),
            Sample(1, TaskRunState.Running, 100),
            Sample(10, TaskRunState.Running, 100)
        };

        var runs = TaskRunBuilder.Build(samples, TimeSpan.FromSeconds(2));

        Assert.Equal(2, runs.Count);
        Assert.Equal(TimeSpan.FromSeconds(2), runs[0].Duration);
        Assert.Equal(TimeSpan.FromSeconds(1), runs[1].Duration);
    }

    private static TaskSample Sample(int second, TaskRunState state, int pid = 100)
    {
        return new TaskSample(
            second + 1,
            Origin.AddSeconds(second),
            "task",
            pid,
            state,
            state.ToString(),
            10,
            100,
            0,
            0,
            "低",
            "低");
    }
}
