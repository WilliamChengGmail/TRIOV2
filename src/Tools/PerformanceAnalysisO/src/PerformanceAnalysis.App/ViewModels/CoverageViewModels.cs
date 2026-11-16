using System.Windows.Media;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class CoverageRowViewModel
{
    public CoverageRowViewModel(
        string sessionLabel,
        string deviceText,
        string startTimeText,
        IReadOnlyList<CoverageCellViewModel> cells)
    {
        SessionLabel = sessionLabel;
        DeviceText = deviceText;
        StartTimeText = startTimeText;
        Cells = cells;
    }

    public string SessionLabel { get; }

    public string DeviceText { get; }

    public string StartTimeText { get; }

    public IReadOnlyList<CoverageCellViewModel> Cells { get; }
}

public sealed class CoverageCellViewModel
{
    private static readonly Brush RanBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x1B, 0x4A, 0x5A)));
    private static readonly Brush RanForeground = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0xF7, 0xFA)));
    private static readonly Brush NotRunBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x14, 0x24, 0x3D)));
    private static readonly Brush NotRunForeground = Freeze(new SolidColorBrush(Color.FromRgb(0x7B, 0x8F, 0xA8)));

    public CoverageCellViewModel(string taskName, bool hasRun, string text, string toolTip)
    {
        TaskName = taskName;
        HasRun = hasRun;
        Text = text;
        ToolTip = toolTip;
    }

    public string TaskName { get; }

    public bool HasRun { get; }

    public string Text { get; }

    public string ToolTip { get; }

    public Brush Background => HasRun ? RanBackground : NotRunBackground;

    public Brush Foreground => HasRun ? RanForeground : NotRunForeground;

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
