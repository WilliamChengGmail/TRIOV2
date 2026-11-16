using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TRIOV2.ResourceSimulator.Controls;

/// <summary>
/// Windows 工作管理員風格之高效向量折線/面積圖表元件 (StreamGeometry 繪製，零 GC 壓力)
/// 製作者: Office of William
/// </summary>
public partial class RealtimeChartControl : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(RealtimeChartControl),
            new PropertyMetadata("效能圖表", (d, e) => ((RealtimeChartControl)d).TxtTitle.Text = (string)e.NewValue));

    public static readonly DependencyProperty ThemeColorProperty =
        DependencyProperty.Register(nameof(ThemeColor), typeof(Color), typeof(RealtimeChartControl),
            new PropertyMetadata(Color.FromRgb(0, 188, 249), (d, e) => ((RealtimeChartControl)d).UpdateThemeColors((Color)e.NewValue)));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public Color ThemeColor
    {
        get => (Color)GetValue(ThemeColorProperty);
        set => SetValue(ThemeColorProperty, value);
    }

    private readonly float[] _renderData = new float[60];

    public RealtimeChartControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        UpdateThemeColors(ThemeColor);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawGrid();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawGrid();
        RenderChart();
    }

    private void UpdateThemeColors(Color color)
    {
        var lineBrush = new SolidColorBrush(color);
        LinePath.Stroke = lineBrush;
        TxtCurrentValue.Foreground = lineBrush;

        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(90, color.R, color.G, color.B), 0.0));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(10, color.R, color.G, color.B), 0.95));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0));
        AreaPath.Fill = gradientBrush;
    }

    /// <summary>
    /// 繪製背景網格線 (0%, 25%, 50%, 75%, 100% 橫線，以及每 10 秒直線)
    /// </summary>
    private void DrawGrid()
    {
        GridCanvas.Children.Clear();
        double w = ChartGrid.ActualWidth;
        double h = ChartGrid.ActualHeight;
        if (w <= 10 || h <= 10) return;

        var gridBrush = new SolidColorBrush(Color.FromArgb(60, 100, 100, 100));

        // 橫線
        for (int i = 0; i <= 4; i++)
        {
            double y = h * (i / 4.0);
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = w,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = i is 0 or 4 ? null : new DoubleCollection { 2, 2 }
            };
            GridCanvas.Children.Add(line);
        }

        // 直線 (每 10 秒一條，共 6 格)
        for (int i = 1; i < 6; i++)
        {
            double x = w * (i / 6.0);
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = h,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    /// <summary>
    /// 外部推送新數據並觸發繪製
    /// </summary>
    public void UpdateChart(float[] historyData, string currentValueText)
    {
        if (historyData != null && historyData.Length == _renderData.Length)
        {
            Array.Copy(historyData, _renderData, _renderData.Length);
        }

        TxtCurrentValue.Text = currentValueText;
        RenderChart();
    }

    private void RenderChart()
    {
        double w = ChartGrid.ActualWidth;
        double h = ChartGrid.ActualHeight;
        if (w <= 10 || h <= 10) return;

        int count = _renderData.Length; // 60 點
        if (count < 2) return;

        double stepX = w / (count - 1);

        var lineGeometry = new StreamGeometry();
        var areaGeometry = new StreamGeometry();

        using (StreamGeometryContext lineCtx = lineGeometry.Open())
        using (StreamGeometryContext areaCtx = areaGeometry.Open())
        {
            // 第一點
            double firstVal = Math.Clamp(_renderData[0], 0f, 100f);
            double firstY = h - (firstVal / 100.0 * h);
            var firstPt = new Point(0, firstY);

            lineCtx.BeginFigure(firstPt, isFilled: false, isClosed: false);
            areaCtx.BeginFigure(new Point(0, h), isFilled: true, isClosed: true);
            areaCtx.LineTo(firstPt, isStroked: false, isSmoothJoin: false);

            for (int i = 1; i < count; i++)
            {
                double val = Math.Clamp(_renderData[i], 0f, 100f);
                double y = h - (val / 100.0 * h);
                var pt = new Point(i * stepX, y);

                lineCtx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                areaCtx.LineTo(pt, isStroked: false, isSmoothJoin: false);
            }

            // 閉合 AreaPath 回到底部
            areaCtx.LineTo(new Point(w, h), isStroked: false, isSmoothJoin: false);
        }

        lineGeometry.Freeze();
        areaGeometry.Freeze();

        LinePath.Data = lineGeometry;
        AreaPath.Data = areaGeometry;
    }
}

