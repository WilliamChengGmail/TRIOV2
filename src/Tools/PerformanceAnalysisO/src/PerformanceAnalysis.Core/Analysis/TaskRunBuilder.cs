using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Analysis;

public static class TaskRunBuilder
{
    /// <summary>
    /// Splits running samples into runs. A gap larger than <paramref name="maxGap"/> or a PID change
    /// starts a new run. Timestamps are inclusive; a run of one sample lasts one second.
    /// </summary>
    public static IReadOnlyList<TaskRun> Build(
        IReadOnlyList<TaskSample> samples,
        TimeSpan? maxGap = null)
    {
        var gap = maxGap ?? TimeSpan.FromSeconds(2);
        var runs = new List<TaskRun>();

        var hasCurrent = false;
        DateTime currentStart = default;
        DateTime currentEnd = default;
        var currentPid = 0;
        var currentSamples = 0;

        void CloseCurrent()
        {
            if (!hasCurrent)
            {
                return;
            }

            var durationSeconds = Math.Max(1d, (currentEnd - currentStart).TotalSeconds + 1d);
            runs.Add(new TaskRun(
                currentPid,
                currentStart,
                currentEnd,
                TimeSpan.FromSeconds(durationSeconds),
                currentSamples));
            hasCurrent = false;
        }

        foreach (var sample in samples.OrderBy(sample => sample.Timestamp))
        {
            if (sample.State != TaskRunState.Running)
            {
                CloseCurrent();
                continue;
            }

            if (hasCurrent
                && currentPid == sample.Pid
                && sample.Timestamp - currentEnd <= gap)
            {
                currentEnd = sample.Timestamp;
                currentSamples++;
                continue;
            }

            CloseCurrent();
            hasCurrent = true;
            currentStart = sample.Timestamp;
            currentEnd = sample.Timestamp;
            currentPid = sample.Pid;
            currentSamples = 1;
        }

        CloseCurrent();
        return runs;
    }
}
