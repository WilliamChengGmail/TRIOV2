using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TRIOV2.PerformanceAnalyzer.Models;
using TRIOV2.PerformanceAnalyzer.ViewModels;

namespace TRIOV2.PerformanceAnalyzer.Views;

/// <summary>
/// TRIO V2 硬體效能數據分析器主視窗
/// 製作者: Office of William
/// </summary>
public partial class AnalyzerMainWindow : Window
{
    public MainAnalyzerViewModel ViewModel { get; }

    public AnalyzerMainWindow()
    {
        InitializeComponent();
        ViewModel = new MainAnalyzerViewModel();
        DataContext = ViewModel;
    }

    public Task InitializeStartupAsync(string? startupPath)
    {
        return ViewModel.InitializeAsync(startupPath);
    }

    private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = ViewModel.LoadPathAsync(ViewModel.TargetPath);
        }
    }

    private void SessionItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BenchmarkSession session })
        {
            ViewModel.SelectedSession = session;
            if (e.ClickCount == 2)
            {
                ViewModel.OpenSessionDetail(session);
            }
        }
    }

    private void ResetTimelineZoom_Click(object sender, RoutedEventArgs e)
    {
        TimelineChart.ResetZoom();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            string first = paths[0];
            if (Directory.Exists(first))
            {
                _ = ViewModel.LoadPathAsync(first);
            }
            else if (File.Exists(first))
            {
                string? dir = Path.GetDirectoryName(first);
                if (!string.IsNullOrEmpty(dir))
                {
                    _ = ViewModel.LoadPathAsync(dir);
                }
            }
        }
    }
}
