using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PerformanceAnalysis.App.ViewModels;

namespace PerformanceAnalysis.App.Controls;

/// <summary>
/// Draws a sessions × tasks coverage matrix with fixed-size columns.
/// </summary>
public sealed class TaskCoverageMatrix : FrameworkElement
{
    private const double FirstColumnWidth = 300;
    private const double TaskColumnWidth = 110;
    private const double RowHeight = 38;
    private const double HeaderHeight = 36;
    private const double Padding = 8;

    private static readonly Brush BackgroundBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x0D, 0x1B, 0x30)));
    private static readonly Brush HeaderBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x2D, 0x4A)));
    private static readonly Brush RowAltBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x10, 0x20, 0x38)));
    private static readonly Brush SeparatorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x2F, 0x4D)));
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF8)));
    private static readonly Brush SecondaryTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)));
    private static readonly Brush MutedTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x7B, 0x8F, 0xA8)));
    private static readonly Typeface Typeface = new(
        new FontFamily("Microsoft JhengHei UI"),
        FontStyles.Normal,
        FontWeights.Normal,
        FontStretches.Normal);

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows),
        typeof(IEnumerable<CoverageRowViewModel>),
        typeof(TaskCoverageMatrix),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TasksProperty = DependencyProperty.Register(
        nameof(Tasks),
        typeof(IEnumerable<string>),
        typeof(TaskCoverageMatrix),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public TaskCoverageMatrix()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    public IEnumerable<CoverageRowViewModel>? Rows
    {
        get => (IEnumerable<CoverageRowViewModel>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public IEnumerable<string>? Tasks
    {
        get => (IEnumerable<string>?)GetValue(TasksProperty);
        set => SetValue(TasksProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var taskCount = Tasks?.Count() ?? 0;
        var rowCount = Rows?.Count() ?? 0;

        var width = Padding * 2 + FirstColumnWidth + taskCount * TaskColumnWidth;
        var height = Padding * 2 + HeaderHeight + rowCount * RowHeight;

        return new Size(
            Math.Max(420, width),
            Math.Max(100, height));
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

        var rows = Rows?.ToList() ?? new List<CoverageRowViewModel>();
        var tasks = Tasks?.ToList() ?? new List<string>();

        DrawHeader(dc, tasks);

        if (rows.Count == 0 || tasks.Count == 0)
        {
            var message = CreateText(
                rows.Count == 0 ? "沒有場次資料" : "沒有任務資料",
                12,
                MutedTextBrush);
            dc.DrawText(message, new Point(
                Padding + Math.Max(0, (width - Padding * 2 - message.Width) / 2),
                Padding + HeaderHeight + 20));
            return;
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var y = Padding + HeaderHeight + rowIndex * RowHeight;

            if (rowIndex % 2 == 1)
            {
                dc.DrawRectangle(RowAltBrush, null, new Rect(0, y, width, RowHeight));
            }

            var label = CreateText(row.SessionLabel, 11.5, TextBrush);
            label.MaxTextWidth = FirstColumnWidth - 20;
            label.MaxLineCount = 1;
            label.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(label, new Point(Padding + 10, y + 5));

            var subtitle = CreateText(
                $"{row.DeviceText} ・ {row.StartTimeText}",
                9.5,
                MutedTextBrush);
            subtitle.MaxTextWidth = FirstColumnWidth - 20;
            subtitle.MaxLineCount = 1;
            subtitle.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(subtitle, new Point(Padding + 10, y + 21));

            for (var taskIndex = 0; taskIndex < tasks.Count; taskIndex++)
            {
                var cell = taskIndex < row.Cells.Count ? row.Cells[taskIndex] : null;
                var x = Padding + FirstColumnWidth + taskIndex * TaskColumnWidth;
                var cellRect = new Rect(x + 4, y + 5, TaskColumnWidth - 8, RowHeight - 10);

                dc.DrawRoundedRectangle(
                    cell?.Background ?? RowAltBrush,
                    null,
                    cellRect,
                    4,
                    4);

                if (cell is null)
                {
                    continue;
                }

                var text = CreateText(cell.Text, 10.5, cell.Foreground);
                text.MaxTextWidth = TaskColumnWidth - 16;
                text.MaxLineCount = 1;
                text.Trimming = TextTrimming.CharacterEllipsis;

                var textX = cellRect.X + Math.Max(0, (cellRect.Width - text.Width) / 2);
                var textY = cellRect.Y + Math.Max(0, (cellRect.Height - text.Height) / 2);
                dc.DrawText(text, new Point(textX, textY));
            }

            dc.DrawLine(
                new Pen(SeparatorBrush, 1),
                new Point(0, y + RowHeight),
                new Point(width, y + RowHeight));
        }

        var dividerX = Padding + FirstColumnWidth;
        dc.DrawLine(
            new Pen(SeparatorBrush, 1),
            new Point(dividerX, Padding),
            new Point(dividerX, Padding + HeaderHeight + rows.Count * RowHeight));
    }

    private void DrawHeader(DrawingContext dc, IReadOnlyList<string> tasks)
    {
        dc.DrawRectangle(
            HeaderBrush,
            null,
            new Rect(0, 0, ActualWidth, Padding + HeaderHeight));

        var sessionHeader = CreateText("場次 / 裝置 / 開始", 11, SecondaryTextBrush);
        dc.DrawText(sessionHeader, new Point(Padding + 10, Padding + (HeaderHeight - sessionHeader.Height) / 2));

        for (var index = 0; index < tasks.Count; index++)
        {
            var x = Padding + FirstColumnWidth + index * TaskColumnWidth;
            var header = CreateText(tasks[index], 10.5, SecondaryTextBrush);
            header.MaxTextWidth = TaskColumnWidth - 16;
            header.MaxLineCount = 1;
            header.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(header, new Point(x + 8, Padding + (HeaderHeight - header.Height) / 2));
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
