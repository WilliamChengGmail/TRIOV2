using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TRIOV2.PerformanceAnalyzer.Views;

namespace TRIOV2.PerformanceAnalyzer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 參數：[資料夾路徑] [--screenshot <輸出資料夾>] [--export <輸出資料夾>]
        string? initialPath = null;
        string? screenshotDir = null;
        string? exportDir = null;
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (string.Equals(e.Args[i], "--screenshot", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                screenshotDir = e.Args[++i];
            }
            else if (string.Equals(e.Args[i], "--export", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                exportDir = e.Args[++i];
            }
            else if (initialPath == null)
            {
                initialPath = e.Args[i];
            }
        }

        var window = new AnalyzerMainWindow();
        MainWindow = window;
        window.Show();

        await window.InitializeStartupAsync(initialPath);

        if (exportDir != null)
        {
            window.ViewModel.ExportAllTo(exportDir);
        }

        if (screenshotDir != null)
        {
            await CaptureScreenshotsAsync(window, screenshotDir);
        }

        if (exportDir != null || screenshotDir != null)
        {
            Shutdown();
        }
    }

    /// <summary>
    /// 文件／驗收用：依序切換四個分頁並輸出 PNG 截圖。
    /// </summary>
    private static async Task CaptureScreenshotsAsync(AnalyzerMainWindow window, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        string[] names = { "tab1-overview", "tab2-trend", "tab3-session", "tab4-compare" };

        for (int tab = 0; tab < names.Length; tab++)
        {
            window.ViewModel.SelectedTabIndex = tab;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(600);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SaveWindow(window, Path.Combine(outputDir, names[tab] + ".png"));
        }
    }

    private static void SaveWindow(Window window, string path)
    {
        if (window.Content is not FrameworkElement root) return;
        double dpi = VisualTreeHelper.GetDpi(window).PixelsPerInchX;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(root.ActualWidth * dpi / 96.0),
            (int)Math.Ceiling(root.ActualHeight * dpi / 96.0),
            dpi,
            dpi,
            PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(window.Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            context.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
