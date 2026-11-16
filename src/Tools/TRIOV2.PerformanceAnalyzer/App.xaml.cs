using System.Windows;
using TRIOV2.PerformanceAnalyzer.Views;

namespace TRIOV2.PerformanceAnalyzer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? initialPath = e.Args.Length > 0 ? e.Args[0] : null;
        var window = new AnalyzerMainWindow();
        MainWindow = window;
        window.Show();

        await window.InitializeStartupAsync(initialPath);
    }
}

