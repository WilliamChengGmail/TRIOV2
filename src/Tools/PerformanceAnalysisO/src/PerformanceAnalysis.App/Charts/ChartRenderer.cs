using System.Globalization;
using PerformanceAnalysis.App.ViewModels;
using PerformanceAnalysis.Core.Models;
using ScottPlot;
using ScottPlot.TickGenerators;
using ScottPlot.WPF;
using PlotColor = ScottPlot.Color;

namespace PerformanceAnalysis.App.Charts;

public static class ChartRenderer
{
    private static readonly PlotColor FigureColor = PlotColor.FromHex("#0D1B30");
    private static readonly PlotColor AxisColor = PlotColor.FromHex("#B0BEC5");
    private static readonly PlotColor GridColor = PlotColor.FromHex("#1F3555");
    private static readonly PlotColor LegendBackground = PlotColor.FromHex("#14243D");
    private static readonly PlotColor LegendOutline = PlotColor.FromHex("#3A5070");
    private static readonly PlotColor LegendText = PlotColor.FromHex("#F0F4F8");

    static ChartRenderer()
    {
        // SkiaSharp's default font does not include CJK glyphs.
        Fonts.Default = "Microsoft JhengHei UI";
    }

    /// <summary>
    /// Triggers the static constructor before any WpfPlot is created so every plot uses the CJK font.
    /// </summary>
    public static void Initialize()
    {
    }

    public static void RenderSystemCpu(WpfPlot control, Session session)
    {
        var plot = control.Plot;
        plot.Clear();

        var samples = session.SystemSamples;
        if (samples.Count == 0)
        {
            RenderEmpty(control, "沒有系統 CPU 樣本");
            return;
        }

        var xs = samples.Select(sample => sample.Timestamp.ToOADate()).ToArray();
        AddSeries(plot, xs, samples.Select(sample => sample.CpuTotalPercent).ToArray(), "全系統", "#42A5F5", 2);
        AddSeries(plot, xs, samples.Select(sample => sample.CpuSimulatorPercent).ToArray(), "模擬器", "#26C6DA", 1.5f);
        AddSeries(plot, xs, samples.Select(sample => sample.CpuFreePercent).ToArray(), "閒置", "#7B8FA8", 1);

        ConfigureTimeAxis(plot, session.Duration);
        plot.Axes.SetLimits(xs.Min(), EnsureRange(xs.Min(), xs.Max()), 0, 100);
        plot.Axes.MarginsX(0.01);
        plot.Title("CPU 使用率 (%)");
        plot.YLabel("%");
        plot.XLabel("時間");
        plot.ShowLegend(Alignment.UpperRight);
        ApplyTheme(plot);
        control.Refresh();
    }

    public static void RenderSystemMemory(WpfPlot control, Session session)
    {
        var plot = control.Plot;
        plot.Clear();

        var samples = session.SystemSamples;
        if (samples.Count == 0)
        {
            RenderEmpty(control, "沒有系統記憶體樣本");
            return;
        }

        var xs = samples.Select(sample => sample.Timestamp.ToOADate()).ToArray();
        var used = samples.Select(sample => sample.MemoryUsedGb).ToArray();
        var available = samples.Select(sample => sample.MemoryAvailableGb).ToArray();
        var yMax = Math.Max(1, Math.Max(used.Max(), available.Max()) * 1.15);

        AddSeries(plot, xs, used, "已用", "#AB47BC", 2);
        AddSeries(plot, xs, available, "可用", "#66BB6A", 1.5f);

        ConfigureTimeAxis(plot, session.Duration);
        plot.Axes.SetLimits(xs.Min(), EnsureRange(xs.Min(), xs.Max()), 0, yMax);
        plot.Axes.MarginsX(0.01);
        plot.Title("記憶體 (GB)");
        plot.YLabel("GB");
        plot.XLabel("時間");
        plot.ShowLegend(Alignment.UpperRight);
        ApplyTheme(plot);
        control.Refresh();
    }

    public static void RenderTaskCpu(
        WpfPlot control,
        Session session,
        IReadOnlyList<TaskRowViewModel> taskRows)
    {
        RenderTaskMetric(
            control,
            session,
            taskRows,
            sample => sample.CpuPercent,
            "任務 CPU (%)",
            "%",
            minimumMax: 10);
    }

    public static void RenderTaskMemory(
        WpfPlot control,
        Session session,
        IReadOnlyList<TaskRowViewModel> taskRows)
    {
        RenderTaskMetric(
            control,
            session,
            taskRows,
            sample => sample.MemoryMb,
            "任務記憶體 (MB)",
            "MB",
            minimumMax: 10);
    }

    public static void RenderOverviewTrend(WpfPlot control, IReadOnlyList<SessionListItem> sessions)
    {
        var plot = control.Plot;
        plot.Clear();

        if (sessions.Count == 0)
        {
            RenderEmpty(control, "尚無場次資料");
            return;
        }

        var ordered = sessions.OrderBy(session => session.StartTime).ToList();
        var xs = ordered.Select(session => session.StartTime.ToOADate()).ToArray();
        var averages = ordered.Select(session => session.CpuAverage).ToArray();
        var maximums = ordered.Select(session => session.CpuMax).ToArray();

        var averageScatter = plot.Add.Scatter(xs, averages);
        averageScatter.LegendText = "CPU 平均";
        averageScatter.Color = PlotColor.FromHex("#42A5F5");
        averageScatter.LineWidth = 1.5f;
        averageScatter.MarkerSize = 5;

        var maxScatter = plot.Add.Scatter(xs, maximums);
        maxScatter.LegendText = "CPU 最高";
        maxScatter.Color = PlotColor.FromHex("#EF5350");
        maxScatter.LineWidth = 1.2f;
        maxScatter.MarkerSize = 4;

        ConfigureTimeAxis(plot, TimeSpan.FromDays(1), "MM-dd HH:mm");
        plot.Axes.AutoScale();
        plot.Axes.MarginsX(0.04);
        plot.Title("歷次測試 CPU 趨勢");
        plot.YLabel("%");
        plot.XLabel("場次開始時間");
        plot.ShowLegend(Alignment.UpperRight);
        ApplyTheme(plot);
        control.Refresh();
    }

    private static void RenderTaskMetric(
        WpfPlot control,
        Session session,
        IReadOnlyList<TaskRowViewModel> taskRows,
        Func<TaskSample, double> valueSelector,
        string title,
        string yLabel,
        double minimumMax)
    {
        var plot = control.Plot;
        plot.Clear();

        var seriesAdded = 0;
        var maximumValue = minimumMax;

        foreach (var taskRow in taskRows)
        {
            var points = taskRow.Task.Samples
                .Where(sample => sample.State == TaskRunState.Running)
                .GroupBy(sample => sample.Timestamp)
                .OrderBy(group => group.Key)
                .Select(group => (
                    X: group.Key.ToOADate(),
                    Y: group.Average(valueSelector)))
                .ToList();

            if (points.Count == 0)
            {
                continue;
            }

            maximumValue = Math.Max(maximumValue, points.Max(point => point.Y));
            AddSeries(
                plot,
                points.Select(point => point.X).ToArray(),
                points.Select(point => point.Y).ToArray(),
                taskRow.Name,
                taskRow.ColorHex,
                1.6f);
            seriesAdded++;
        }

        if (seriesAdded == 0)
        {
            RenderEmpty(control, "沒有任務執行資料");
            return;
        }

        var xMin = session.StartTime.ToOADate();
        var xMax = EnsureRange(xMin, session.EndTime.ToOADate());

        ConfigureTimeAxis(plot, session.Duration);
        plot.Axes.SetLimits(xMin, xMax, 0, maximumValue * 1.15);
        plot.Axes.MarginsX(0.01);
        plot.Title(title);
        plot.YLabel(yLabel);
        plot.XLabel("時間");
        plot.ShowLegend(Alignment.UpperRight);
        ApplyTheme(plot);
        control.Refresh();
    }

    private static void AddSeries(
        Plot plot,
        double[] xs,
        double[] ys,
        string legend,
        string colorHex,
        float lineWidth)
    {
        var scatter = plot.Add.Scatter(xs, ys);
        scatter.LegendText = legend;
        scatter.Color = PlotColor.FromHex(colorHex);
        scatter.LineWidth = lineWidth;
        scatter.MarkerSize = 0;
    }

    private static void RenderEmpty(WpfPlot control, string message)
    {
        var plot = control.Plot;
        plot.Clear();
        plot.Title(message);
        ApplyTheme(plot);
        control.Refresh();
    }

    private static void ConfigureTimeAxis(Plot plot, TimeSpan duration, string? format = null)
    {
        var labelFormat = format ?? (duration < TimeSpan.FromHours(2) ? "HH:mm:ss" : "MM-dd HH:mm");
        plot.Axes.Bottom.TickGenerator = new DateTimeAutomatic
        {
            LabelFormatter = value => value.ToString(labelFormat, CultureInfo.InvariantCulture)
        };
        plot.Axes.Bottom.TickLabelStyle.FontSize = 10;
        plot.Axes.Left.TickLabelStyle.FontSize = 10;
    }

    private static void ApplyTheme(Plot plot)
    {
        const string fontName = "Microsoft JhengHei UI";

        plot.FigureBackground.Color = FigureColor;
        plot.DataBackground.Color = FigureColor;
        plot.Axes.Color(AxisColor);
        plot.Grid.MajorLineColor = GridColor;

        plot.Axes.Title.Label.FontName = fontName;
        plot.Axes.Bottom.Label.FontName = fontName;
        plot.Axes.Left.Label.FontName = fontName;
        plot.Axes.Right.Label.FontName = fontName;
        plot.Axes.Bottom.TickLabelStyle.FontName = fontName;
        plot.Axes.Left.TickLabelStyle.FontName = fontName;

        plot.Legend.BackgroundColor = LegendBackground;
        plot.Legend.OutlineColor = LegendOutline;
        plot.Legend.FontColor = LegendText;
        plot.Legend.FontName = fontName;
        plot.Legend.FontSize = 10;
    }

    private static double EnsureRange(double minimum, double maximum)
        => Math.Abs(maximum - minimum) < 1e-9 ? minimum + 1d / 86400d : maximum;
}
