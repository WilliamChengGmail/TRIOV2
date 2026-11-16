using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Controls;

/// <summary>
/// 單次量測秒級同步時間軸互動圖表（支援多任務曲線疊加、滑鼠十字準線讀值與區間拖曳縮放）
/// 製作者: Office of William
/// </summary>
public partial class InteractiveTimelineChart : UserControl
{
    public static readonly DependencyProperty SessionProperty =
        DependencyProperty.Register(nameof(Session), typeof(BenchmarkSession), typeof(InteractiveTimelineChart),
            new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty CpuThresholdProperty =
        DependencyProperty.Register(nameof(CpuThreshold), typeof(double), typeof(InteractiveTimelineChart),
            new PropertyMetadata(70.0, OnDataChanged));

    public static readonly DependencyProperty RamThresholdProperty =
        DependencyProperty.Register(nameof(RamThreshold), typeof(double), typeof(InteractiveTimelineChart),
            new PropertyMetadata(80.0, OnDataChanged));

    private const double PadL = 46;
    private const double PadR = 16;
    private const double PadT = 18;
    private const double PadB = 26;

    private int _zoomStartIndex;
    private int _zoomEndIndex = -1;
    private bool _isDragging;
    private double _dragStartX;
    private Rectangle? _dragBox;
    private Line? _crosshairLine;
    private readonly List<TaskBenchmarkSummary> _subscribedTasks = new();

    public BenchmarkSession? Session
    {
        get => (BenchmarkSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public double CpuThreshold
    {
        get => (double)GetValue(CpuThresholdProperty);
        set => SetValue(CpuThresholdProperty, value);
    }

    public double RamThreshold
    {
        get => (double)GetValue(RamThresholdProperty);
        set => SetValue(RamThresholdProperty, value);
    }

    public InteractiveTimelineChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public void ResetZoom()
    {
        _zoomStartIndex = 0;
        _zoomEndIndex = -1;
        Redraw();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not InteractiveTimelineChart chart) return;
        if (e.Property == SessionProperty)
        {
            chart.HookTaskSubscriptions();
            chart._zoomStartIndex = 0;
            chart._zoomEndIndex = -1;
        }
        chart.Redraw();
    }

    private void HookTaskSubscriptions()
    {
        foreach (var t in _subscribedTasks)
        {
            t.PropertyChanged -= Task_PropertyChanged;
        }
        _subscribedTasks.Clear();

        if (Session?.Tasks == null) return;
        foreach (var t in Session.Tasks)
        {
            t.PropertyChanged += Task_PropertyChanged;
            _subscribedTasks.Add(t);
        }
    }

    private void Task_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaskBenchmarkSummary.IsOverlayVisible))
        {
            Redraw();
        }
    }

    private (int Start, int End) GetVisibleRange(int total)
    {
        if (total <= 1) return (0, Math.Max(0, total - 1));
        int s = Math.Clamp(_zoomStartIndex, 0, total - 2);
        int e = _zoomEndIndex < 0 ? total - 1 : Math.Clamp(_zoomEndIndex, s + 1, total - 1);
        return (s, e);
    }

    public void Redraw()
    {
        PlotCanvas.Children.Clear();
        _crosshairLine = null;
        _dragBox = null;

        double width = PlotCanvas.ActualWidth;
        double height = PlotCanvas.ActualHeight;
        if (width < 120 || height < 80) return;

        var samples = Session?.SystemSamples;
        if (samples == null || samples.Count == 0)
        {
            DrawEmptyPrompt(width, height);
            return;
        }

        double plotW = width - PadL - PadR;
        double plotH = height - PadT - PadB;
        var (startIdx, endIdx) = GetVisibleRange(samples.Count);
        int visibleCount = endIdx - startIdx + 1;

        // 背景格線與 0/25/50/75/100 刻度
        var gridBrush = new SolidColorBrush(Color.FromRgb(40, 40, 44));
        var labelBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        for (int pct = 0; pct <= 100; pct += 25)
        {
            double y = PadT + plotH * (1.0 - pct / 100.0);
            PlotCanvas.Children.Add(new Line
            {
                X1 = PadL, Y1 = y, X2 = width - PadR, Y2 = y,
                Stroke = gridBrush, StrokeThickness = 1
            });

            var lbl = new TextBlock
            {
                Text = $"{pct}%",
                Foreground = labelBrush,
                FontSize = 11
            };
            Canvas.SetLeft(lbl, 6);
            Canvas.SetTop(lbl, y - 8);
            PlotCanvas.Children.Add(lbl);
        }

        // 辨識運算觸發背景高亮條 (底層辨識狀態 != 待機)
        var recogBrush = new SolidColorBrush(Color.FromArgb(42, 245, 158, 11));
        for (int i = startIdx; i <= endIdx; i++)
        {
            if (!samples[i].IsRecognizing) continue;
            double x = MapIndexToX(i, startIdx, endIdx, plotW);
            double barW = Math.Max(2.0, plotW / Math.Max(1, visibleCount));
            var bar = new Rectangle
            {
                Width = barW,
                Height = plotH,
                Fill = recogBrush
            };
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, PadT);
            PlotCanvas.Children.Add(bar);
        }

        // 驗收警戒線
        DrawThresholdLine(CpuThreshold, Color.FromArgb(140, 239, 68, 68), $"CPU 基準 {CpuThreshold:0}%", plotW);
        DrawThresholdLine(RamThreshold, Color.FromArgb(140, 16, 185, 129), $"RAM 基準 {RamThreshold:0}%", plotW);

        // X 軸起訖時間標籤
        var startLbl = new TextBlock { Text = samples[startIdx].Timestamp.ToString("HH:mm:ss"), Foreground = labelBrush, FontSize = 11 };
        Canvas.SetLeft(startLbl, PadL);
        Canvas.SetTop(startLbl, height - 20);
        PlotCanvas.Children.Add(startLbl);

        var midLbl = new TextBlock
        {
            Text = startIdx == 0 && endIdx == samples.Count - 1 ? "提示：拖曳框選可局部放大，雙擊還原" : "已局部放大（雙擊圖表還原）",
            Foreground = new SolidColorBrush(Color.FromRgb(113, 113, 122)),
            FontSize = 11
        };
        Canvas.SetLeft(midLbl, PadL + Math.Max(0, plotW / 2 - 105));
        Canvas.SetTop(midLbl, height - 20);
        PlotCanvas.Children.Add(midLbl);

        var endLbl = new TextBlock { Text = samples[endIdx].Timestamp.ToString("HH:mm:ss"), Foreground = labelBrush, FontSize = 11 };
        Canvas.SetLeft(endLbl, Math.Max(PadL + 60, width - PadR - 54));
        Canvas.SetTop(endLbl, height - 20);
        PlotCanvas.Children.Add(endLbl);

        // 疊加已勾選之各任務 CPU 曲線與執行時段視覺標記
        if (Session?.Tasks != null)
        {
            int overlayIdx = 0;
            foreach (var task in Session.Tasks.Where(t => t.IsOverlayVisible && t.Samples.Count > 0))
            {
                Color col = ParseColor(task.ColorHex, Colors.Orange);
                DrawSeriesPolyline(
                    startIdx, endIdx, plotW, plotH,
                    idx => idx < task.Samples.Count ? task.Samples[idx].CpuPercent : 0,
                    col, 1.6, false);

                DrawTaskExecutionSpans(task, col, overlayIdx++, startIdx, endIdx, plotW, plotH, samples);
            }
        }

        // 全系統記憶體 (%) 曲線
        DrawSeriesPolyline(startIdx, endIdx, plotW, plotH, i => samples[i].MemoryPercent, Color.FromRgb(16, 185, 129), 2.0, false);

        // 全系統 CPU (%) 主曲線（附淡藍漸層面積）
        DrawSeriesPolyline(startIdx, endIdx, plotW, plotH, i => samples[i].SystemCpuPercent, Color.FromRgb(0, 188, 249), 2.2, true);

        // 十字準線（預設隱藏）
        _crosshairLine = new Line
        {
            Y1 = PadT,
            Y2 = PadT + plotH,
            Stroke = new SolidColorBrush(Color.FromArgb(180, 244, 244, 245)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Visibility = Visibility.Collapsed
        };
        PlotCanvas.Children.Add(_crosshairLine);
    }

    private void DrawTaskExecutionSpans(
        TaskBenchmarkSummary task, Color color, int overlayIndex,
        int startIdx, int endIdx, double plotW, double plotH, IReadOnlyList<SystemSamplePoint> samples)
    {
        if (task.ExecutionRuns.Count == 0 || samples.Count == 0) return;

        double barH = 4.0;
        double barY = PadT + plotH - 5.0 - (overlayIndex * 6.0);

        foreach (var run in task.ExecutionRuns)
        {
            int rStart = FindFirstIndex(samples, s => s.Timestamp >= run.StartTime);
            int rEnd = FindLastIndex(samples, s => s.Timestamp <= run.EndTime);
            if (rStart < 0) rStart = 0;
            if (rEnd < 0) rEnd = samples.Count - 1;

            if (rEnd < startIdx || rStart > endIdx) continue;

            int visStart = Math.Max(startIdx, rStart);
            int visEnd = Math.Min(endIdx, rEnd);
            double x1 = MapIndexToX(visStart, startIdx, endIdx, plotW);
            double x2 = MapIndexToX(visEnd, startIdx, endIdx, plotW);
            double spanW = Math.Max(4.0, x2 - x1);

            var runBar = new Rectangle
            {
                Width = spanW,
                Height = barH,
                Fill = new SolidColorBrush(color),
                RadiusX = 2,
                RadiusY = 2,
                ToolTip = $"{task.TaskName} {run.RunLabel}: 起 {run.StartTimeText} ~ 訖 {run.EndTimeText} (時長 {run.DurationText})"
            };
            Canvas.SetLeft(runBar, x1);
            Canvas.SetTop(runBar, barY);
            PlotCanvas.Children.Add(runBar);

            // 啟動點邊界虛線
            if (rStart >= startIdx && rStart <= endIdx)
            {
                PlotCanvas.Children.Add(new Line
                {
                    X1 = x1, Y1 = PadT, X2 = x1, Y2 = PadT + plotH,
                    Stroke = new SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                });
            }
        }
    }

    private void DrawThresholdLine(double pct, Color color, string text, double plotW)
    {
        double clamped = Math.Clamp(pct, 0, 100);
        double y = PadT + (PlotCanvas.ActualHeight - PadT - PadB) * (1.0 - clamped / 100.0);
        PlotCanvas.Children.Add(new Line
        {
            X1 = PadL,
            Y1 = y,
            X2 = PadL + plotW,
            Y2 = y,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.1,
            StrokeDashArray = new DoubleCollection { 5, 4 }
        });

        var lbl = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontSize = 10.5
        };
        Canvas.SetLeft(lbl, PadL + 6);
        Canvas.SetTop(lbl, Math.Max(2, y - 15));
        PlotCanvas.Children.Add(lbl);
    }

    private void DrawSeriesPolyline(
        int startIdx, int endIdx, double plotW, double plotH,
        Func<int, double> valueSelector, Color strokeColor, double thickness, bool fillArea)
    {
        int count = endIdx - startIdx + 1;
        if (count <= 0) return;

        // 降採樣保護：若點數遠大於像素寬度，取區段分桶最大值/最小值以保留尖峰
        int maxBuckets = Math.Max(200, (int)plotW);
        var points = new PointCollection();

        if (count <= maxBuckets)
        {
            for (int i = startIdx; i <= endIdx; i++)
            {
                double x = MapIndexToX(i, startIdx, endIdx, plotW);
                double y = MapValueToY(valueSelector(i), plotH);
                points.Add(new Point(x, y));
            }
        }
        else
        {
            double step = (double)count / maxBuckets;
            for (int b = 0; b < maxBuckets; b++)
            {
                int bStart = startIdx + (int)(b * step);
                int bEnd = Math.Min(endIdx, startIdx + (int)((b + 1) * step) - 1);
                if (bEnd < bStart) bEnd = bStart;

                double peak = double.MinValue;
                int peakIdx = bStart;
                for (int k = bStart; k <= bEnd; k++)
                {
                    double v = valueSelector(k);
                    if (v > peak) { peak = v; peakIdx = k; }
                }

                points.Add(new Point(MapIndexToX(peakIdx, startIdx, endIdx, plotW), MapValueToY(peak, plotH)));
            }
        }

        if (fillArea && points.Count >= 2)
        {
            var areaPts = new PointCollection(points)
            {
                new Point(points[^1].X, PadT + plotH),
                new Point(points[0].X, PadT + plotH)
            };
            PlotCanvas.Children.Add(new Polygon
            {
                Points = areaPts,
                Fill = new SolidColorBrush(Color.FromArgb(38, strokeColor.R, strokeColor.G, strokeColor.B))
            });
        }

        PlotCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round
        });
    }

    private void DrawEmptyPrompt(double width, double height)
    {
        var tb = new TextBlock
        {
            Text = "此測試紀錄未包含 System_BenchmarkLog.csv 秒級時間序列資料",
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 13
        };
        Canvas.SetLeft(tb, Math.Max(20, width / 2 - 190));
        Canvas.SetTop(tb, Math.Max(20, height / 2 - 10));
        PlotCanvas.Children.Add(tb);
    }

    private void PlotCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var samples = Session?.SystemSamples;
        if (samples == null || samples.Count == 0 || PlotCanvas.ActualWidth < 120) return;

        Point pos = e.GetPosition(PlotCanvas);
        double plotW = PlotCanvas.ActualWidth - PadL - PadR;
        double plotH = PlotCanvas.ActualHeight - PadT - PadB;

        if (_isDragging && _dragBox != null)
        {
            double x1 = Math.Clamp(_dragStartX, PadL, PadL + plotW);
            double x2 = Math.Clamp(pos.X, PadL, PadL + plotW);
            Canvas.SetLeft(_dragBox, Math.Min(x1, x2));
            _dragBox.Width = Math.Abs(x2 - x1);
            return;
        }

        if (pos.X < PadL || pos.X > PadL + plotW || pos.Y < PadT || pos.Y > PadT + plotH)
        {
            HideHover();
            return;
        }

        var (startIdx, endIdx) = GetVisibleRange(samples.Count);
        double ratio = (pos.X - PadL) / Math.Max(1.0, plotW);
        int idx = Math.Clamp(startIdx + (int)Math.Round(ratio * (endIdx - startIdx)), 0, samples.Count - 1);
        double snapX = MapIndexToX(idx, startIdx, endIdx, plotW);

        if (_crosshairLine != null)
        {
            _crosshairLine.X1 = snapX;
            _crosshairLine.X2 = snapX;
            _crosshairLine.Visibility = Visibility.Visible;
        }

        var s = samples[idx];
        var sb = new StringBuilder();
        sb.AppendLine($"[{s.Timestamp:HH:mm:ss}] (+{s.ElapsedSeconds:0}s)  狀態: {s.RecognitionStatus}");
        sb.AppendLine($"全系統 CPU: {s.SystemCpuPercent:0.0}%  |  RAM: {s.UsedMemoryGb:0.00} GB ({s.MemoryPercent:0.#}%)");

        if (Session?.Tasks != null)
        {
            foreach (var task in Session.Tasks.Where(t => t.IsOverlayVisible))
            {
                if (idx < task.Samples.Count)
                {
                    var ts = task.Samples[idx];
                    var matchingRun = task.ExecutionRuns.FirstOrDefault(r =>
                        s.Timestamp >= r.StartTime && s.Timestamp <= r.EndTime);

                    if (matchingRun != null)
                    {
                        sb.AppendLine($"• {task.TaskName} [{matchingRun.RunLabel}執行中 | 起 {matchingRun.StartTimeText} ~ 訖 {matchingRun.EndTimeText} | 時長 {matchingRun.DurationText} | PID {matchingRun.Pid}]: CPU {ts.CpuPercent:0.0}% | RAM {ts.MemoryMb:0.#} MB");
                    }
                    else if (ts.IsActive)
                    {
                        sb.AppendLine($"• {task.TaskName} [執行中 | PID {ts.Pid}]: CPU {ts.CpuPercent:0.0}% | RAM {ts.MemoryMb:0.#} MB");
                    }
                    else
                    {
                        var lastRun = task.ExecutionRuns.LastOrDefault(r => r.EndTime <= s.Timestamp);
                        var nextRun = task.ExecutionRuns.FirstOrDefault(r => r.StartTime >= s.Timestamp);
                        string statusNote = lastRun != null
                            ? (nextRun != null ? $"已停止 (前次第 {lastRun.RunIndex} 次於 {lastRun.EndTimeText} 結束，下次於 {nextRun.StartTimeText} 重啟)" : $"已關閉停止 (前次第 {lastRun.RunIndex} 次於 {lastRun.EndTimeText} 結束)")
                            : "尚未啟動執行";
                        sb.AppendLine($"• {task.TaskName} [{statusNote}]: 0.0%");
                    }
                }
            }
        }

        TooltipText.Text = sb.ToString().TrimEnd();
        TooltipCard.Visibility = Visibility.Visible;
    }

    private void PlotCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging) HideHover();
    }

    private void PlotCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ResetZoom();
            return;
        }

        var samples = Session?.SystemSamples;
        if (samples == null || samples.Count < 4) return;

        Point pos = e.GetPosition(PlotCanvas);
        double plotW = PlotCanvas.ActualWidth - PadL - PadR;
        if (pos.X < PadL || pos.X > PadL + plotW) return;

        _isDragging = true;
        _dragStartX = pos.X;
        PlotCanvas.CaptureMouse();

        _dragBox = new Rectangle
        {
            Height = Math.Max(10, PlotCanvas.ActualHeight - PadT - PadB),
            Width = 0,
            Fill = new SolidColorBrush(Color.FromArgb(45, 0, 188, 249)),
            Stroke = new SolidColorBrush(Color.FromArgb(180, 0, 188, 249)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(_dragBox, _dragStartX);
        Canvas.SetTop(_dragBox, PadT);
        PlotCanvas.Children.Add(_dragBox);
    }

    private void PlotCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        PlotCanvas.ReleaseMouseCapture();

        Point pos = e.GetPosition(PlotCanvas);
        if (_dragBox != null)
        {
            PlotCanvas.Children.Remove(_dragBox);
            _dragBox = null;
        }

        double plotW = PlotCanvas.ActualWidth - PadL - PadR;
        double dx = Math.Abs(pos.X - _dragStartX);
        if (dx < 14 || Session?.SystemSamples == null || Session.SystemSamples.Count < 4) return;

        var (curStart, curEnd) = GetVisibleRange(Session.SystemSamples.Count);
        double r1 = Math.Clamp((Math.Min(_dragStartX, pos.X) - PadL) / Math.Max(1, plotW), 0, 1);
        double r2 = Math.Clamp((Math.Max(_dragStartX, pos.X) - PadL) / Math.Max(1, plotW), 0, 1);

        int newStart = curStart + (int)Math.Floor(r1 * (curEnd - curStart));
        int newEnd = curStart + (int)Math.Ceiling(r2 * (curEnd - curStart));
        if (newEnd - newStart >= 3)
        {
            _zoomStartIndex = newStart;
            _zoomEndIndex = newEnd;
            Redraw();
        }
    }

    private void HideHover()
    {
        if (_crosshairLine != null) _crosshairLine.Visibility = Visibility.Collapsed;
        TooltipCard.Visibility = Visibility.Collapsed;
    }

    private static double MapIndexToX(int index, int startIdx, int endIdx, double plotW)
    {
        if (endIdx <= startIdx) return PadL + plotW / 2.0;
        return PadL + (index - startIdx) * plotW / (endIdx - startIdx);
    }

    private static double MapValueToY(double percentValue, double plotH)
    {
        double clamped = Math.Clamp(percentValue, 0, 100);
        return PadT + plotH * (1.0 - clamped / 100.0);
    }

    private static int FindFirstIndex(IReadOnlyList<SystemSamplePoint> list, Func<SystemSamplePoint, bool> predicate)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (predicate(list[i])) return i;
        }
        return -1;
    }

    private static int FindLastIndex(IReadOnlyList<SystemSamplePoint> list, Func<SystemSamplePoint, bool> predicate)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (predicate(list[i])) return i;
        }
        return -1;
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return fallback;
        }
    }
}
