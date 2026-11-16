using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PerformanceAnalysis.App.Charts;
using PerformanceAnalysis.App.ViewModels;
using PerformanceAnalysis.Core.Analysis;

namespace PerformanceAnalysis.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            TryWriteCrash(args.Exception);
            args.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                TryWriteCrash(exception);
            }
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            ChartRenderer.Initialize();

            var window = new MainWindow(GetArgument(e.Args, "--metadata-dir"));
            MainWindow = window;

            var pathIndex = Array.IndexOf(e.Args, "--path");
            if (pathIndex >= 0 && pathIndex + 1 < e.Args.Length)
            {
                window.ViewModel.SourcePath = e.Args[pathIndex + 1];
            }

            window.Show();

            if (Directory.Exists(window.ViewModel.SourcePath))
            {
                await window.ViewModel.ScanAsync();
            }

            var baselineFolder = GetArgument(e.Args, "--set-baseline");
            if (baselineFolder is not null && window.ViewModel.Sessions.Count > 0)
            {
                var baselineSession = window.ViewModel.Sessions.FirstOrDefault(session =>
                    string.Equals(session.FolderName, baselineFolder, StringComparison.OrdinalIgnoreCase));
                if (baselineSession is not null)
                {
                    await window.ViewModel.SetBaselineAsync(baselineSession);
                }
            }

            if (GetArgument(e.Args, "--demo-ops") is not null && window.ViewModel.Sessions.Count > 0)
            {
                await RunDemoOperationsAsync(window.ViewModel);
            }

            if (GetArgument(e.Args, "--demo-settings") is not null && window.ViewModel.Sessions.Count > 0)
            {
                await window.ViewModel.ApplySettingsAsync(new AnalysisOptions
                {
                    CpuHighPercent = 80,
                    CpuHighSustainedSeconds = 5,
                    RamHighPercent = 85,
                    AvailableRamMinimumGb = 2,
                    MemoryGrowthMbPerMinute = 10,
                    MemoryGrowthMinimumSeconds = 60,
                    ShortSessionSeconds = 5,
                    SampleGapSeconds = 3,
                    DuplicateRatioPercent = 0.5,
                    TaskRestartThreshold = 1
                });
            }

            var page = GetArgument(e.Args, "--page");
            if (page is not null && window.ViewModel.Sessions.Count > 0)
            {
                if (string.Equals(page, "devices", StringComparison.OrdinalIgnoreCase))
                {
                    window.ViewModel.ShowDevices(addToHistory: false);
                }
                else if (string.Equals(page, "compare", StringComparison.OrdinalIgnoreCase))
                {
                    window.ViewModel.ShowCompare(addToHistory: false);
                }
            }

            var screenshotIndex = Array.IndexOf(e.Args, "--screenshot");
            if (screenshotIndex >= 0 && screenshotIndex + 1 < e.Args.Length)
            {
                await CaptureAsync(window, e.Args[screenshotIndex + 1]);
                Shutdown(0);
            }
        }
        catch (Exception exception)
        {
            TryWriteCrash(exception);
            MessageBox.Show(exception.ToString(), "啟動失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void TryWriteCrash(Exception exception)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "startup-error.txt"),
                exception.ToString());
        }
        catch
        {
            // Last-resort diagnostics; ignore.
        }
    }

    private static async Task CaptureAsync(MainWindow window, string target)
    {
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(400);

        var overviewPath = target.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? target
            : Path.Combine(target, "overview.png");

        var directory = Path.GetDirectoryName(overviewPath) ?? ".";
        Directory.CreateDirectory(directory);
        SaveScreenshot(window, overviewPath);

        if (window.ViewModel.Sessions.Count > 0
            && window.ViewModel.CurrentPage is not SessionDetailViewModel)
        {
            window.ViewModel.OpenSession(window.ViewModel.Sessions[0]);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(400);

            SaveScreenshot(window, Path.Combine(directory, "session.png"));

            var scrollViewer = FindLargestScrollViewer(window);
            if (scrollViewer is not null && scrollViewer.ScrollableHeight > 0)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight * 0.45);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(300);
                SaveScreenshot(window, Path.Combine(directory, "session-middle.png"));

                scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(300);
                SaveScreenshot(window, Path.Combine(directory, "session-bottom.png"));
            }

            window.ViewModel.Back();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(300);
            SaveScreenshot(window, Path.Combine(directory, "back.png"));

            window.ViewModel.ShowDevices();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(400);
            SaveScreenshot(window, Path.Combine(directory, "devices.png"));

            window.ViewModel.ShowCompare();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(400);
            SaveScreenshot(window, Path.Combine(directory, "compare.png"));

            window.ViewModel.ShowSettings();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(400);
            SaveScreenshot(window, Path.Combine(directory, "settings.png"));

            window.ViewModel.Home();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(400);

            var tabControl = FindVisualChild<TabControl>(window);
            if (tabControl is not null)
            {
                tabControl.SelectedIndex = 1;
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(400);
                SaveScreenshot(window, Path.Combine(directory, "overview-coverage.png"));
            }
        }
    }

    private static void SaveScreenshot(Window window, string path)
    {
        var width = (int)Math.Ceiling(window.ActualWidth);
        var height = (int)Math.Ceiling(window.ActualHeight);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static ScrollViewer? FindLargestScrollViewer(DependencyObject root)
    {
        ScrollViewer? best = null;

        void Walk(DependencyObject node)
        {
            if (node is ScrollViewer scrollViewer
                && scrollViewer.ScrollableHeight > (best?.ScrollableHeight ?? 0))
            {
                best = scrollViewer;
            }

            var children = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < children; i++)
            {
                Walk(VisualTreeHelper.GetChild(node, i));
            }
        }

        Walk(root);
        return best;
    }

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            return match;
        }

        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            var found = FindVisualChild<T>(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string? GetArgument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static async Task RunDemoOperationsAsync(MainViewModel viewModel)
    {
        var desktop = viewModel.Sessions.FirstOrDefault(session =>
            string.Equals(session.Device.DeviceName, "DESKTOP-S136SQE", StringComparison.OrdinalIgnoreCase));

        if (desktop is null)
        {
            return;
        }

        var desktopDeviceId = desktop.Device.DeviceKey;
        await viewModel.UpdateDeviceAliasAsync(desktopDeviceId, "LAB-A");

        var baseline = viewModel.Sessions.FirstOrDefault(session =>
            string.Equals(session.FolderName, "20261112_150751", StringComparison.OrdinalIgnoreCase));
        if (baseline is not null)
        {
            await viewModel.SetBaselineAsync(baseline);
        }

        var move = viewModel.Sessions.FirstOrDefault(session =>
            string.Equals(session.FolderName, "20261112_102803", StringComparison.OrdinalIgnoreCase));
        var target = viewModel.Sessions.FirstOrDefault(session =>
            string.Equals(session.FolderName, "20261112_102448", StringComparison.OrdinalIgnoreCase));
        if (move is not null && target is not null)
        {
            await viewModel.MoveSessionAsync(move, target.Device.DeviceKey);
        }

        var split = viewModel.Sessions.FirstOrDefault(session =>
            string.Equals(session.FolderName, "20261109_162306", StringComparison.OrdinalIgnoreCase));
        if (split is not null)
        {
            await viewModel.SplitSessionAsync(split, "NEW-LAB-B");
        }

        var annotation = viewModel.Sessions.FirstOrDefault(session =>
            string.Equals(session.FolderName, "20261112_150751", StringComparison.OrdinalIgnoreCase));
        if (annotation is not null)
        {
            await viewModel.UpdateAnnotationsAsync(annotation, "夜間測試", "baseline, night");
        }
    }
}
