using PerformanceAnalysis.Core.Analysis;

namespace PerformanceAnalysis.Core.Tests;

public sealed class StatisticsTests
{
    [Fact]
    public void DescribeComputesExpectedValues()
    {
        var stats = Statistics.Describe(new[] { 1d, 2d, 3d, 4d, 5d });

        Assert.Equal(5, stats.Count);
        Assert.Equal(1, stats.Min, 6);
        Assert.Equal(5, stats.Max, 6);
        Assert.Equal(3, stats.Mean, 6);
        Assert.Equal(3, stats.P50, 6);
        Assert.Equal(4.8, stats.P95, 6);
        Assert.Equal(Math.Sqrt(2), stats.StdDev, 6);
    }

    [Fact]
    public void DescribeReturnsEmptyForNoValues()
    {
        var stats = Statistics.Describe(Array.Empty<double>());

        Assert.Equal(0, stats.Count);
        Assert.False(stats.HasData);
    }

    [Fact]
    public void SlopePerMinuteUsesLeastSquares()
    {
        var slope = Statistics.SlopePerMinute(new[]
        {
            (0d, 10d),
            (60d, 20d)
        });

        Assert.Equal(10, slope, 6);
    }

    [Fact]
    public void SlopePerMinuteHandlesNoSpread()
    {
        var slope = Statistics.SlopePerMinute(new[]
        {
            (5d, 10d),
            (5d, 20d)
        });

        Assert.Equal(0, slope, 6);
    }
}
