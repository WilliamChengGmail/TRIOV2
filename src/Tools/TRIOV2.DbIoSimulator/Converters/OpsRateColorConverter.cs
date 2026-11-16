using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TRIOV2.DbIoSimulator.Converters;

/// <summary>
/// 依據寫入速率 (op/s) 或錯誤率顯示對應警示顏色 (Normal: Cyan / High: Amber / Extreme: Red)
/// 製作者: Office of William
/// </summary>
public sealed class OpsRateColorConverter : IValueConverter
{
    private static readonly SolidColorBrush IdleBrush = FreezeBrush(Color.FromRgb(136, 136, 136));
    private static readonly SolidColorBrush NormalBrush = FreezeBrush(Color.FromRgb(0, 188, 212));   // #00BCD4 Cyan
    private static readonly SolidColorBrush HighBrush = FreezeBrush(Color.FromRgb(255, 193, 7));     // #FFC107 Amber
    private static readonly SolidColorBrush ExtremeBrush = FreezeBrush(Color.FromRgb(244, 67, 54));  // #F44336 Red

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double rate = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => 0.0
        };

        // 若 parameter == "ErrorRate"，則按錯誤率門檻著色
        if (parameter is string p && p.Equals("ErrorRate", StringComparison.OrdinalIgnoreCase))
        {
            return rate switch
            {
                <= 0.0 => NormalBrush,
                < 1.0 => HighBrush,
                _ => ExtremeBrush
            };
        }

        return rate switch
        {
            <= 0.05 => IdleBrush,
            < 40.0 => NormalBrush,
            < 100.0 => HighBrush,
            _ => ExtremeBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush FreezeBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

