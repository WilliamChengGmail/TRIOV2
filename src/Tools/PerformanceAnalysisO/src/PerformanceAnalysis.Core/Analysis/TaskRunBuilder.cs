using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Analysis;

public static class TaskRunBuilder
{
    /// <summary>
    /// 同一 PID 中間沒有紀錄的秒數不超過此值時，視為記錄器漏記，不視為重啟。
    /// 記錄器實測會週期性漏記 2–3 秒；真正的重啟會改變 PID 或出現「未執行」狀態。
    /// </summary>
    public static readonly TimeSpan DefaultMaxGap = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Splits running samples into runs. A PID change, a non-running state, or a gap larger than
    /// <paramref name="maxGap"/> (default <see cref="DefaultMaxGap"/>) starts a new run.
    /// Timestamps are inclusive; a run of one sample lasts one second.
    /// </summary>
    public static IReadOnlyList<TaskRun> Build(
        IReadOnlyList<TaskSample> samples,
        TimeSpan? maxGap = null)
    {
        var gap = maxGap ?? DefaultMaxGap;
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
