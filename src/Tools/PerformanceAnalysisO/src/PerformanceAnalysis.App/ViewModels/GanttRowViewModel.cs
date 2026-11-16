using System.Windows.Media;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class GanttRowViewModel
{
    public GanttRowViewModel(string name, Brush brush, IReadOnlyList<GanttBarViewModel> bars)
    {
        Name = name;
        Brush = brush;
        Bars = bars;
    }

    public string Name { get; }

    public Brush Brush { get; }

    public IReadOnlyList<GanttBarViewModel> Bars { get; }

    public bool IsEmpty => Bars.Count == 0;
}

public sealed class GanttBarViewModel
{
    public double StartSeconds { get; init; }

    public double DurationSeconds { get; init; }

    public Brush Brush { get; init; } = Brushes.DodgerBlue;
}
