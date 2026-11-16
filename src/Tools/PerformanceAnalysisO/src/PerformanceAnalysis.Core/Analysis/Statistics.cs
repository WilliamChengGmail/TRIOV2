using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Analysis;

public static class Statistics
{
    public static MetricStats Describe(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return MetricStats.Empty;
        }

        list.Sort();
        var mean = list.Average();
        var variance = list.Sum(value => (value - mean) * (value - mean)) / list.Count;

        return new MetricStats(
            list.Count,
            list[0],
            list[^1],
            mean,
            PercentileSorted(list, 50),
            PercentileSorted(list, 95),
            Math.Sqrt(variance));
    }

    public static double Percentile(IEnumerable<double> values, double percentile)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        list.Sort();
        return PercentileSorted(list, percentile);
    }

    /// <summary>
    /// Least-squares slope. X is in seconds; the result is converted to units per minute.
    /// Returns 0 when there are fewer than two points or X has no spread.
    /// </summary>
    public static double SlopePerMinute(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 2)
        {
            return 0;
        }

        var meanX = points.Average(point => point.X);
        var meanY = points.Average(point => point.Y);
        var varianceX = 0d;
        var covariance = 0d;

        foreach (var (x, y) in points)
        {
            varianceX += (x - meanX) * (x - meanX);
            covariance += (x - meanX) * (y - meanY);
        }

        if (varianceX <= double.Epsilon)
        {
            return 0;
        }

        return covariance / varianceX * 60d;
    }

    public static IReadOnlyList<(double X, double Y)> ToElapsedPerSecondSeries<T>(
        IEnumerable<T> samples,
        Func<T, DateTime> timestamp,
        Func<T, double> value,
        DateTime origin)
    {
        return samples
            .GroupBy(sample => timestamp(sample))
            .OrderBy(group => group.Key)
            .Select(group => (
                X: (group.Key - origin).TotalSeconds,
                Y: group.Average(value)))
            .ToList();
    }

    private static double PercentileSorted(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var clamped = Math.Clamp(percentile, 0, 100);
        var position = (sortedValues.Count - 1) * clamped / 100d;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var fraction = position - lower;
        return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * fraction;
    }
}
