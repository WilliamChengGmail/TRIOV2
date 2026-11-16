namespace PerformanceAnalysis.Core.Models;

public sealed record TaskRun(
    int Pid,
    DateTime Start,
    DateTime End,
    TimeSpan Duration,
    int SampleCount)
{
    public bool Contains(DateTime timestamp) => timestamp >= Start && timestamp <= End;
}
