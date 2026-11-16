namespace PerformanceAnalysis.Core.Models;

public sealed record MetricStats(
    int Count,
    double Min,
    double Max,
    double Mean,
    double P50,
    double P95,
    double StdDev)
{
    public static MetricStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);

    public bool HasData => Count > 0;
}
