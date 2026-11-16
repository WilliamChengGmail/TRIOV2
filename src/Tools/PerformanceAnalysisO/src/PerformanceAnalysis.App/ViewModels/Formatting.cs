using System.Globalization;

namespace PerformanceAnalysis.App.ViewModels;

public static class Formatting
{
    public static string Duration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "00:00";
        }

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    public static string Rate(double value, string unit)
    {
        if (value <= 0)
        {
            return "—";
        }

        return unit switch
        {
            "KB/s" when value >= 1024 => $"{value / 1024:0.00} MB/s",
            "Kbps" when value >= 1000 => $"{value / 1000:0.00} Mbps",
            _ => $"{value:0.0} {unit}"
        };
    }

    public static string Number(double value, string format = "0.0")
        => value.ToString(format, CultureInfo.InvariantCulture);
}
