using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PerformanceAnalysis.App.Charts;
using PerformanceAnalysis.App.ViewModels;

namespace PerformanceAnalysis.App.Views;

public partial class SessionDetailView : UserControl
{
    public SessionDetailView()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderCharts();
        DataContextChanged += (_, _) =>
        {
            if (IsLoaded)
            {
                RenderCharts();
            }
        };
    }

    private void RenderCharts()
    {
        if (DataContext is not SessionDetailViewModel viewModel)
        {
            return;
        }

        ChartRenderer.RenderSystemCpu(CpuPlot, viewModel.Session);
        ChartRenderer.RenderSystemMemory(MemoryPlot, viewModel.Session);
        ChartRenderer.RenderTaskCpu(TaskCpuPlot, viewModel.Session, viewModel.TaskRows);
        ChartRenderer.RenderTaskMemory(TaskMemoryPlot, viewModel.Session, viewModel.TaskRows);
    }

    private void Chart_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Wheel scrolls the page; Ctrl+wheel keeps ScottPlot's zoom behavior.
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            return;
        }

        var scrollViewer = FindAncestor<ScrollViewer>(sender as DependencyObject);
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
            {
                return match;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }
}
