using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Controls;

/// <summary>
/// 支援「跨設備效能比較柱狀圖」與「單機多輪測試趨勢折線圖」之向量繪圖元件
/// 製作者: Office of William
/// </summary>
public partial class FleetComparisonChart : UserControl
{
    public static readonly DependencyProperty DevicesProperty =
        DependencyProperty.Register(nameof(Devices), typeof(IReadOnlyList<DeviceAggregateSummary>), typeof(FleetComparisonChart),
            new PropertyMetadata(null, (d, _) => ((FleetComparisonChart)d).Redraw()));

    public static readonly DependencyProperty TrendSessionsProperty =
        DependencyProperty.Register(nameof(TrendSessions), typeof(IReadOnlyList<BenchmarkSession>), typeof(FleetComparisonChart),
            new PropertyMetadata(null, (d, _) => ((FleetComparisonChart)d).Redraw()));

    public static readonly DependencyProperty ChartModeProperty =
        DependencyProperty.Register(nameof(ChartMode), typeof(string), typeof(FleetComparisonChart),
            new PropertyMetadata("Fleet", (d, _) => ((FleetComparisonChart)d).Redraw()));

    public static readonly DependencyProperty CpuPassLimitProperty =
        DependencyProperty.Register(nameof(CpuPassLimit), typeof(double), typeof(FleetComparisonChart),
            new PropertyMetadata(70.0, (d, _) => ((FleetComparisonChart)d).Redraw()));

    public static readonly DependencyProperty RamPassLimitProperty =
        DependencyProperty.Register(nameof(RamPassLimit), typeof(double), typeof(FleetComparisonChart),
            new PropertyMetadata(0.0, (d, _) => ((FleetComparisonChart)d).Redraw()));

    public IReadOnlyList<DeviceAggregateSummary>? Devices
    {
        get => (IReadOnlyList<DeviceAggregateSummary>?)GetValue(DevicesProperty);
        set => SetValue(DevicesProperty, value);
    }

    public IReadOnlyList<BenchmarkSession>? TrendSessions
    {
        get => (IReadOnlyList<BenchmarkSession>?)GetValue(TrendSessionsProperty);
        set => SetValue(TrendSessionsProperty, value);
    }

    public string ChartMode
    {
        get => (string)GetValue(ChartModeProperty);
        set => SetValue(ChartModeProperty, value);
    }

    public double CpuPassLimit
    {
        get => (double)GetValue(CpuPassLimitProperty);
        set => SetValue(CpuPassLimitProperty, value);
    }

    public double RamPassLimit
    {
        get => (double)GetValue(RamPassLimitProperty);
        set => SetValue(RamPassLimitProperty, value);
    }

    public FleetComparisonChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public void Redraw()
    {
        ChartCanvas.Children.Clear();
        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;
        if (width < 160 || height < 100) return;

        if (string.Equals(ChartMode, "Trend", StringComparison.OrdinalIgnoreCase))
        {
            DrawTrendChart(width, height);
        }
        else
        {
            DrawFleetBarChart(width, height);
        }
    }

    private void DrawFleetBarChart(double width, double height)
    {
        var items = Devices?.Where(d => d.IncludedRunCount > 0).ToList();
        if (items == null || items.Count == 0)
        {
            DrawCenterText("尚無已勾選的設備測試紀錄可供繪製對比圖", width, height);
            return;
        }

        const double padL = 44, padR = 20, padT = 28, padB = 42;
        double plotW = width - padL - padR;
        double plotH = height - padT - padB;

        DrawGridAndThresholds(padL, padT, plotW, plotH);

        int count = items.Count;
        double slotW = plotW / count;
        double barW = Math.Clamp(slotW * 0.22, 12, 34);
        double gap = 4;

        var cpuAvgBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        var cpuP95Brush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        var ramPeakBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        var labelBrush = new SolidColorBrush(Color.FromRgb(212, 212, 216));

        for (int i = 0; i < count; i++)
        {
            var dev = items[i];
            double centerX = padL + slotW * (i + 0.5);
            double groupStart = centerX - (barW * 3 + gap * 2) / 2.0;

            DrawBarWithValue(groupStart, dev.AvgCpuAcrossRuns, barW, padT, plotH, cpuAvgBrush);
            DrawBarWithValue(groupStart + barW + gap, dev.MaxCpuP95AcrossRuns, barW, padT, plotH, cpuP95Brush);
            DrawBarWithValue(groupStart + (barW + gap) * 2, dev.PeakRamPercentAcrossRuns, barW, padT, plotH, ramPeakBrush);

            string shortName = dev.DisplayName.Length > 18 ? dev.DisplayName[..18] + "…" : dev.DisplayName;
            var nameLbl = new TextBlock
            {
                Text = shortName,
                Foreground = labelBrush,
                FontSize = 11,
                Width = Math.Max(60, slotW - 6),
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(nameLbl, centerX - nameLbl.Width / 2);
            Canvas.SetTop(nameLbl, padT + plotH + 8);
            ChartCanvas.Children.Add(nameLbl);
        }

        // 圖例
        DrawLegend(width - 330, 6, new[]
        {
            ("CPU 平均%", Color.FromRgb(56, 189, 248)),
            ("CPU P95%", Color.FromRgb(245, 158, 11)),
            ("RAM 峰值%", Color.FromRgb(16, 185, 129))
        });
    }

    private void DrawTrendChart(double width, double height)
    {
        var runs = TrendSessions?.Where(s => s.IsIncluded).OrderBy(s => s.MeasuredAt).ToList();
        if (runs == null || runs.Count == 0)
        {
            DrawCenterText("此設備目前未勾選任何測試場次", width, height);
            return;
        }

        const double padL = 44, padR = 24, padT = 28, padB = 42;
        double plotW = width - padL - padR;
        double plotH = height - padT - padB;

        DrawGridAndThresholds(padL, padT, plotW, plotH);

        var cpuAvgPts = new PointCollection();
        var cpuP95Pts = new PointCollection();
        var ramPts = new PointCollection();
        var labelBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));

        for (int i = 0; i < runs.Count; i++)
        {
            var r = runs[i];
            double x = runs.Count == 1 ? padL + plotW / 2.0 : padL + i * plotW / (runs.Count - 1);
            double yAvg = padT + plotH * (1.0 - Math.Clamp(r.CpuAvg, 0, 100) / 100.0);
            double yP95 = padT + plotH * (1.0 - Math.Clamp(r.CpuP95, 0, 100) / 100.0);
            double yRam = padT + plotH * (1.0 - Math.Clamp(r.RamPeakPercent, 0, 100) / 100.0);

            cpuAvgPts.Add(new Point(x, yAvg));
            cpuP95Pts.Add(new Point(x, yP95));
            ramPts.Add(new Point(x, yRam));

            var lbl = new TextBlock
            {
                Text = $"#{i + 1} {r.MeasuredAt:MM/dd HH:mm}",
                Foreground = labelBrush,
                FontSize = 11
            };
            Canvas.SetLeft(lbl, Math.Clamp(x - 36, padL, padL + plotW - 76));
            Canvas.SetTop(lbl, padT + plotH + 8);
            ChartCanvas.Children.Add(lbl);
        }

        if (runs.Count >= 2)
        {
            ChartCanvas.Children.Add(new Polyline { Points = cpuAvgPts, Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)), StrokeThickness = 2 });
            ChartCanvas.Children.Add(new Polyline { Points = cpuP95Pts, Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)), StrokeThickness = 2.2 });
            ChartCanvas.Children.Add(new Polyline { Points = ramPts, Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129)), StrokeThickness = 2 });
        }

        for (int i = 0; i < runs.Count; i++)
        {
            DrawDotWithLabel(cpuAvgPts[i], Color.FromRgb(56, 189, 248), $"{runs[i].CpuAvg:0.#}%", -18);
            DrawDotWithLabel(cpuP95Pts[i], Color.FromRgb(245, 158, 11), $"{runs[i].CpuP95:0.#}%", -18);
            DrawDotWithLabel(ramPts[i], Color.FromRgb(16, 185, 129), $"{runs[i].RamPeakPercent:0.#}%", 6);
        }

        DrawLegend(width - 330, 6, new[]
        {
            ("CPU 平均%", Color.FromRgb(56, 189, 248)),
            ("CPU P95%", Color.FromRgb(245, 158, 11)),
            ("RAM 峰值%", Color.FromRgb(16, 185, 129))
        });
    }

    private void DrawGridAndThresholds(double padL, double padT, double plotW, double plotH)
    {
        var gridBrush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
        var axisLabelBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));

        for (int pct = 0; pct <= 100; pct += 25)
        {
            double y = padT + plotH * (1.0 - pct / 100.0);
            ChartCanvas.Children.Add(new Line { X1 = padL, Y1 = y, X2 = padL + plotW, Y2 = y, Stroke = gridBrush, StrokeThickness = 1 });
            var lbl = new TextBlock { Text = $"{pct}%", Foreground = axisLabelBrush, FontSize = 11 };
            Canvas.SetLeft(lbl, 6);
            Canvas.SetTop(lbl, y - 8);
            ChartCanvas.Children.Add(lbl);
        }

        // 驗收參考虛線
        DrawHorizontalLimit(padL, padT, plotW, plotH, CpuPassLimit, Color.FromArgb(145, 245, 158, 11));
        DrawHorizontalLimit(padL, padT, plotW, plotH, RamPassLimit, Color.FromArgb(145, 239, 68, 68));
    }

    private void DrawHorizontalLimit(double padL, double padT, double plotW, double plotH, double pct, Color color)
    {
        if (pct <= 0) return;
        double y = padT + plotH * (1.0 - Math.Clamp(pct, 0, 100) / 100.0);
        ChartCanvas.Children.Add(new Line
        {
            X1 = padL, Y1 = y, X2 = padL + plotW, Y2 = y,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection { 5, 4 }
        });
    }

    private void DrawBarWithValue(double x, double pct, double barW, double padT, double plotH, Brush brush)
    {
        double clamped = Math.Clamp(pct, 0, 100);
        double barH = Math.Max(2, plotH * (clamped / 100.0));
        double y = padT + plotH - barH;

        var rect = new Rectangle
        {
            Width = barW,
            Height = barH,
            Fill = brush,
            RadiusX = 3,
            RadiusY = 3
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        ChartCanvas.Children.Add(rect);

        var val = new TextBlock
        {
            Text = $"{pct:0.#}",
            Foreground = new SolidColorBrush(Color.FromRgb(228, 228, 231)),
            FontSize = 10.5
        };
        Canvas.SetLeft(val, x);
        Canvas.SetTop(val, Math.Max(padT - 14, y - 16));
        ChartCanvas.Children.Add(val);
    }

    private void DrawDotWithLabel(Point pt, Color color, string text, double yOffset)
    {
        var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(color) };
        Canvas.SetLeft(dot, pt.X - 4);
        Canvas.SetTop(dot, pt.Y - 4);
        ChartCanvas.Children.Add(dot);

        var lbl = new TextBlock { Text = text, Foreground = new SolidColorBrush(color), FontSize = 11 };
        Canvas.SetLeft(lbl, pt.X - 14);
        Canvas.SetTop(lbl, pt.Y + yOffset);
        ChartCanvas.Children.Add(lbl);
    }

    private void DrawLegend(double startX, double y, IReadOnlyList<(string Label, Color Color)> entries)
    {
        double curX = Math.Max(60, startX);
        foreach (var (label, color) in entries)
        {
            var sw = new Rectangle { Width = 10, Height = 10, Fill = new SolidColorBrush(color), RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(sw, curX);
            Canvas.SetTop(sw, y + 3);
            ChartCanvas.Children.Add(sw);

            var tb = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)), FontSize = 11 };
            Canvas.SetLeft(tb, curX + 14);
            Canvas.SetTop(tb, y);
            ChartCanvas.Children.Add(tb);
            curX += 100;
        }
    }

    private void DrawCenterText(string text, double width, double height)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 13
        };
        Canvas.SetLeft(tb, Math.Max(20, width / 2 - 150));
        Canvas.SetTop(tb, Math.Max(20, height / 2 - 10));
        ChartCanvas.Children.Add(tb);
    }
}
