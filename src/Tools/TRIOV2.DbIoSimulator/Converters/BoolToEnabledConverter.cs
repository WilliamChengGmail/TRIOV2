using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TRIOV2.DbIoSimulator.Converters;

/// <summary>
/// 布林值轉換器 (支援反轉與 Visibility 轉換，供 XAML 綁定控制項啟用狀態或面板顯示)
/// 製作者: Office of William
/// </summary>
public sealed class BoolToEnabledConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (Invert || (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase)))
        {
            flag = !flag;
        }

        if (targetType == typeof(Visibility))
        {
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        return flag;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is Visibility v ? v == Visibility.Visible : value is bool b && b;
        if (Invert || (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase)))
        {
            flag = !flag;
        }
        return flag;
    }
}

