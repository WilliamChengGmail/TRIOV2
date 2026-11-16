using System.Windows.Controls;
using System.Windows.Input;
using PerformanceAnalysis.App.Charts;
using PerformanceAnalysis.App.ViewModels;

namespace PerformanceAnalysis.App.Views;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderChart();
        DataContextChanged += (_, _) =>
        {
            if (IsLoaded)
            {
                RenderChart();
            }
        };
    }

    private void RenderChart()
    {
        if (DataContext is OverviewViewModel viewModel)
        {
            ChartRenderer.RenderOverviewTrend(TrendPlot, viewModel.TrendSessions);
        }
    }

    private void SessionGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OverviewViewModel viewModel
            && SessionGrid.SelectedItem is SessionListItem item)
        {
            viewModel.OpenSessionCommand.Execute(item);
        }
    }
}
