using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PerformanceAnalysis.App.ViewModels;

namespace PerformanceAnalysis.App.Controls;

/// <summary>
/// Lightweight Gantt renderer. Rows contain bars whose start is relative to SessionStart.
/// </summary>
public sealed class TaskGantt : FrameworkElement
{
    private const double LabelWidth = 190;
    private const double RowHeight = 28;
    private const double HeaderHeight = 24;
    private const double AxisHeight = 24;
    private const double Padding = 8;

    private static readonly Brush BackgroundBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x0D, 0x1B, 0x30)));
    private static readonly Brush RowAltBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x10, 0x20, 0x38)));
    private static readonly Brush SeparatorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x2F, 0x4D)));
    private static readonly Brush GridBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x35, 0x55)));
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF8)));
    private static readonly Brush MutedTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x7B, 0x8F, 0xA8)));
    private static readonly Brush SecondaryTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)));
    private static readonly Typeface Typeface = new(
        new FontFamily("Microsoft JhengHei UI"),
        FontStyles.Normal,
        FontWeights.Normal,
        FontStretches.Normal);

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows),
        typeof(IEnumerable<GanttRowViewModel>),
        typeof(TaskGantt),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SessionStartProperty = DependencyProperty.Register(
        nameof(SessionStart),
        typeof(DateTime),
        typeof(TaskGantt),
        new FrameworkPropertyMetadata(
            default(DateTime),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SessionEndProperty = DependencyProperty.Register(
        nameof(SessionEnd),
        typeof(DateTime),
        typeof(TaskGantt),
        new FrameworkPropertyMetadata(
            default(DateTime),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public TaskGantt()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    public IEnumerable<GanttRowViewModel>? Rows
    {
        get => (IEnumerable<GanttRowViewModel>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public DateTime SessionStart
    {
        get => (DateTime)GetValue(SessionStartProperty);
        set => SetValue(SessionStartProperty, value);
    }

    public DateTime SessionEnd
    {
        get => (DateTime)GetValue(SessionEndProperty);
        set => SetValue(SessionEndProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var rows = Rows?.ToList() ?? new List<GanttRowViewModel>();
        var height = rows.Count == 0
            ? 86
            : HeaderHeight + rows.Count * RowHeight + AxisHeight + Padding * 2;

        var width = double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, width, height));

        var rows = Rows?.ToList() ?? new List<GanttRowViewModel>();
        var timelineX = LabelWidth;
        var timelineWidth = Math.Max(10, width - LabelWidth - Padding);
        var totalSeconds = Math.Max(1, (SessionEnd - SessionStart).TotalSeconds);

        DrawHeader(dc, width, timelineX, timelineWidth);

        if (rows.Count == 0)
        {
            var empty = CreateText("沒有任務執行區間", 12, MutedTextBrush);
            dc.DrawText(empty, new Point(
                LabelWidth + Math.Max(0, (timelineWidth - empty.Width) / 2),
                (height - empty.Height) / 2));
            return;
        }

        const int tickCount = 6;
        for (var tick = 0; tick <= tickCount; tick++)
        {
            var fraction = tick / (double)tickCount;
            var x = timelineX + fraction * timelineWidth;
            dc.DrawLine(new Pen(GridBrush, 1), new Point(x, HeaderHeight), new Point(x, height - AxisHeight));
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var y = HeaderHeight + Padding + index * RowHeight;

            if (index % 2 == 1)
            {
                dc.DrawRectangle(RowAltBrush, null, new Rect(0, y, width, RowHeight));
            }

            dc.DrawLine(new Pen(SeparatorBrush, 1), new Point(0, y + RowHeight), new Point(width, y + RowHeight));

            var label = CreateText(row.Name, 11.5, row.IsEmpty ? MutedTextBrush : SecondaryTextBrush);
            label.MaxTextWidth = LabelWidth - 20;
            label.MaxLineCount = 1;
            label.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(label, new Point(10, y + (RowHeight - label.Height) / 2));

            if (row.IsEmpty)
            {
                var note = CreateText("未執行", 10.5, MutedTextBrush);
                dc.DrawText(note, new Point(LabelWidth + 8, y + (RowHeight - note.Height) / 2));
                continue;
            }

            foreach (var bar in row.Bars)
            {
                var barX = timelineX + Math.Clamp(bar.StartSeconds / totalSeconds, 0, 1) * timelineWidth;
                var barWidth = Math.Max(2, bar.DurationSeconds / totalSeconds * timelineWidth);
                barWidth = Math.Min(barWidth, timelineX + timelineWidth - barX);

                var rect = new Rect(barX, y + 5, barWidth, RowHeight - 10);
                dc.DrawRoundedRectangle(bar.Brush, null, rect, 3, 3);
            }
        }

        DrawTimeAxis(dc, width, timelineX, timelineWidth, totalSeconds);
    }

    private void DrawHeader(DrawingContext dc, double width, double timelineX, double timelineWidth)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x14, 0x24, 0x3D)), null, new Rect(0, 0, width, HeaderHeight));

        var label = CreateText("任務", 11, MutedTextBrush);
        dc.DrawText(label, new Point(10, (HeaderHeight - label.Height) / 2));

        if (SessionStart == default || SessionEnd == default)
        {
            return;
        }

        var range = CreateText(
            $"{SessionStart:HH:mm:ss}  →  {SessionEnd:HH:mm:ss}",
            10.5,
            MutedTextBrush);

        dc.DrawText(range, new Point(
            timelineX + Math.Max(0, timelineWidth - range.Width),
            (HeaderHeight - range.Height) / 2));
    }

    private void DrawTimeAxis(
        DrawingContext dc,
        double width,
        double timelineX,
        double timelineWidth,
        double totalSeconds)
    {
        if (SessionStart == default || SessionEnd == default)
        {
            return;
        }

        dc.DrawLine(
            new Pen(SeparatorBrush, 1),
            new Point(0, ActualHeight - AxisHeight),
            new Point(width, ActualHeight - AxisHeight));

        const int tickCount = 6;
        var format = totalSeconds < 120 ? "HH:mm:ss" : "HH:mm";

        for (var tick = 0; tick <= tickCount; tick++)
        {
            var fraction = tick / (double)tickCount;
            var time = SessionStart.AddSeconds(totalSeconds * fraction);
            var text = CreateText(
                time.ToString(format),
                10,
                MutedTextBrush);

            var x = timelineX + fraction * timelineWidth;
            x = Math.Clamp(x - text.Width / 2, timelineX, timelineX + timelineWidth - text.Width);
            dc.DrawText(text, new Point(x, ActualHeight - AxisHeight + 5));
        }
    }

    private FormattedText CreateText(string text, double size, Brush brush)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            Typeface,
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
