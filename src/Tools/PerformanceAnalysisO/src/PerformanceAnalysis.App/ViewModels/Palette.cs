using System.Windows.Media;

namespace PerformanceAnalysis.App.ViewModels;

public static class Palette
{
    private static readonly string[] HexColors =
    {
        "#42A5F5",
        "#26C6DA",
        "#66BB6A",
        "#FFCA28",
        "#EF5350",
        "#AB47BC",
        "#FF7043",
        "#8D6E63"
    };

    public static string HexAt(int index) => HexColors[index % HexColors.Length];

    public static Color ColorAt(int index)
        => (Color)ColorConverter.ConvertFromString(HexAt(index))!;

    public static SolidColorBrush BrushAt(int index)
    {
        var brush = new SolidColorBrush(ColorAt(index));
        brush.Freeze();
        return brush;
    }
}
