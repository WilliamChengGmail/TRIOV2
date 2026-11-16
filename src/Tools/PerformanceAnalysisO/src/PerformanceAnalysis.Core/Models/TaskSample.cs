namespace PerformanceAnalysis.Core.Models;

public enum TaskRunState
{
    Unknown = 0,
    NotRunning = 1,
    Running = 2
}

public sealed record TaskSample(
    int Sequence,
    DateTime Timestamp,
    string ProcessName,
    int Pid,
    TaskRunState State,
    string RawState,
    double CpuPercent,
    double MemoryMb,
    double DiskKbPerSecond,
    double NetworkKbps,
    string PowerUsage,
    string PowerTrend);
