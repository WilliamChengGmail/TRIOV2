using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TRIOV2.DbIoSimulator.Controls;

/// <summary>
/// 高效向量雙曲線折線圖元件 (顯示寫入與讀取 op/s，支援動態 Y 軸縮放與零 GC 繪圖)
/// 製作者: Office of William
/// </summary>
public partial class RealtimeIoChartControl : UserControl
{
    private static readonly Color CyanColor = Color.FromRgb(0, 188, 212);
    private static readonly float[] ScaleSteps = [20f, 50f, 100f, 200f, 400f, 800f, 1500f, 3000f, 5000f];

    private readonly float[] _writeBuffer = new float[60];
    private readonly float[] _readBuffer = new float[60];
    private float _currentMaxScale = 50f;

    public RealtimeIoChartControl()
    {
        InitializeComponent();
        InitAreaBrush();
        Loaded += (_, _) => DrawGrid();
        SizeChanged += (_, _) =>
        {
            DrawGrid();
            RenderChart();
        };
    }

    private void InitAreaBrush()
    {
        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(85, CyanColor.R, CyanColor.G, CyanColor.B), 0.0));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(12, CyanColor.R, CyanColor.G, CyanColor.B), 0.92));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, CyanColor.R, CyanColor.G, CyanColor.B), 1.0));
        gradientBrush.Freeze();
        WriteAreaPath.Fill = gradientBrush;
    }

    /// <summary>
    /// 更新並重新繪製寫入與讀取歷史曲線
    /// </summary>
    public void UpdateChart(float[] writeHistory, float[] readHistory, double currentWriteOps, double currentReadOps)
    {
        if (writeHistory.Length == _writeBuffer.Length)
        {
            Array.Copy(writeHistory, _writeBuffer, _writeBuffer.Length);
        }
        if (readHistory.Length == _readBuffer.Length)
        {
            Array.Copy(readHistory, _readBuffer, _readBuffer.Length);
        }

        TxtWriteVal.Text = $"{currentWriteOps:F1} op/s";
        TxtReadVal.Text = $"{currentReadOps:F1} op/s";

        UpdateDynamicScale();
        RenderChart();
    }

    private void UpdateDynamicScale()
    {
        float peak = 1f;
        for (int i = 0; i < _writeBuffer.Length; i++)
        {
            if (_writeBuffer[i] > peak) peak = _writeBuffer[i];
            if (_readBuffer[i] > peak) peak = _readBuffer[i];
        }

        float targetScale = ScaleSteps[^1];
        foreach (float step in ScaleSteps)
        {
            if (peak <= step * 0.88f)
            {
                targetScale = step;
                break;
            }
        }

        _currentMaxScale = targetScale;
        TxtMaxScale.Text = $"{targetScale:0} op/s";
        TxtMidScale.Text = $"{targetScale / 2f:0} op/s";
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();
        double w = ChartGrid.ActualWidth;
        double h = ChartGrid.ActualHeight;
        if (w <= 10 || h <= 10) return;

        var gridBrush = new SolidColorBrush(Color.FromArgb(55, 110, 110, 110));
        gridBrush.Freeze();

        for (int i = 0; i <= 4; i++)
        {
            double y = h * (i / 4.0);
            GridCanvas.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = w,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = i is 0 or 4 ? null : new DoubleCollection { 2, 2 }
            });
        }

        for (int i = 1; i < 6; i++)
        {
            double x = w * (i / 6.0);
            GridCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = h,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            });
        }
    }

    private void RenderChart()
    {
        double w = ChartGrid.ActualWidth;
        double h = ChartGrid.ActualHeight;
        if (w <= 10 || h <= 10) return;

        int count = _writeBuffer.Length;
        double stepX = w / (count - 1);
        double max = _currentMaxScale;

        var writeLineGeo = new StreamGeometry();
        var writeAreaGeo = new StreamGeometry();
        var readLineGeo = new StreamGeometry();

        using (var wLineCtx = writeLineGeo.Open())
        using (var wAreaCtx = writeAreaGeo.Open())
        using (var rLineCtx = readLineGeo.Open())
        {
            Point firstW = ToChartPoint(0, _writeBuffer[0], stepX, h, max);
            Point firstR = ToChartPoint(0, _readBuffer[0], stepX, h, max);

            wLineCtx.BeginFigure(firstW, isFilled: false, isClosed: false);
            wAreaCtx.BeginFigure(new Point(0, h), isFilled: true, isClosed: true);
            wAreaCtx.LineTo(firstW, isStroked: false, isSmoothJoin: false);
            rLineCtx.BeginFigure(firstR, isFilled: false, isClosed: false);

            for (int i = 1; i < count; i++)
            {
                Point ptW = ToChartPoint(i, _writeBuffer[i], stepX, h, max);
                Point ptR = ToChartPoint(i, _readBuffer[i], stepX, h, max);
                wLineCtx.LineTo(ptW, isStroked: true, isSmoothJoin: true);
                wAreaCtx.LineTo(ptW, isStroked: false, isSmoothJoin: true);
                rLineCtx.LineTo(ptR, isStroked: true, isSmoothJoin: true);
            }

            wAreaCtx.LineTo(new Point((count - 1) * stepX, h), isStroked: false, isSmoothJoin: false);
        }

        writeLineGeo.Freeze();
        writeAreaGeo.Freeze();
        readLineGeo.Freeze();

        WriteLinePath.Data = writeLineGeo;
        WriteAreaPath.Data = writeAreaGeo;
        ReadLinePath.Data = readLineGeo;
    }

    private static Point ToChartPoint(int index, float rawValue, double stepX, double height, double maxScale)
    {
        double clamped = Math.Clamp(rawValue, 0.0, maxScale);
        double y = height - (clamped / maxScale * height);
        return new Point(index * stepX, y);
    }
}

