namespace PerformanceAnalysis.Core.Models;

public sealed record SessionAggregate(
    TimeSpan Duration,
    int SampleCount,
    int UniqueTimestampCount,
    int ExpectedSampleCount,
    double DataCompleteness,
    MetricStats CpuTotal,
    MetricStats CpuSimulator,
    MetricStats MemoryUsedGb,
    MetricStats MemoryUsagePercent,
    MetricStats FrameBufferMb,
    MetricStats RecognitionMs,
    double CpuAbove80Ratio,
    double CpuAbove90Ratio,
    double MemoryGrowthMbPerMinute,
    int MaxGapSeconds)
{
    public static SessionAggregate Empty { get; } = new(
        TimeSpan.Zero,
        0,
        0,
        0,
        0,
        MetricStats.Empty,
        MetricStats.Empty,
        MetricStats.Empty,
        MetricStats.Empty,
        MetricStats.Empty,
        MetricStats.Empty,
        0,
        0,
        0,
        0);
}

public sealed record TaskAggregate(
    TimeSpan ActiveDuration,
    double ActiveRatio,
    MetricStats Cpu,
    MetricStats MemoryMb,
    MetricStats DiskKbPerSecond,
    MetricStats NetworkKbps,
    double CpuSeconds,
    double MemoryGrowthMbPerMinute,
    double DiskTotalMb,
    double NetworkTotalMb,
    int RunCount,
    int RestartCount,
    int StateFlaps)
{
    public static TaskAggregate Empty { get; } = new(
        TimeSpan.Zero,
        0,
        MetricStats.Empty,
        MetricStats.Empty,
        MetricStats.Empty,
        MetricStats.Empty,
        0,
        0,
        0,
        0,
        0,
        0,
        0);
}
