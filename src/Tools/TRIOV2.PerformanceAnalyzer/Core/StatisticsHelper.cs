namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 效能數據統計計算工具（平均值、標準差、P50/P95/P99 分位數）
/// 製作者: Office of William
/// </summary>
public static class StatisticsHelper
{
    public static (double Avg, double Min, double Max, double P50, double P95, double P99, double StdDev) ComputeDistribution(
        IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return (0, 0, 0, 0, 0, 0, 0);
        }

        int n = values.Count;
        double sum = 0;
        double min = double.MaxValue;
        double max = double.MinValue;

        for (int i = 0; i < n; i++)
        {
            double v = values[i];
            sum += v;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        double avg = sum / n;
        double sqDiffSum = 0;
        for (int i = 0; i < n; i++)
        {
            double d = values[i] - avg;
            sqDiffSum += d * d;
        }
        double stdDev = Math.Sqrt(sqDiffSum / n);

        double[] sorted = new double[n];
        for (int i = 0; i < n; i++) sorted[i] = values[i];
        Array.Sort(sorted);

        double p50 = PercentileSorted(sorted, 0.50);
        double p95 = PercentileSorted(sorted, 0.95);
        double p99 = PercentileSorted(sorted, 0.99);

        return (avg, min, max, p50, p95, p99, stdDev);
    }

    public static double PercentileSorted(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        if (sorted.Length == 1) return sorted[0];

        double index = p * (sorted.Length - 1);
        int lo = (int)Math.Floor(index);
        int hi = (int)Math.Ceiling(index);
        if (lo == hi) return sorted[lo];

        double fraction = index - lo;
        return sorted[lo] + (sorted[hi] - sorted[lo]) * fraction;
    }
}

