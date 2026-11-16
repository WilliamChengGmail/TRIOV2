using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PerformanceAnalysis.App.ViewModels;

namespace PerformanceAnalysis.App;

public partial class MainWindow : Window
{
    public MainWindow(string? metadataDirectory = null)
    {
        InitializeComponent();
        ViewModel = new MainViewModel(metadataDirectory);
        DataContext = ViewModel;
    }

    public MainViewModel ViewModel { get; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        TryEnableDarkTitleBar();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        var path = paths.FirstOrDefault(Directory.Exists)
                   ?? paths.FirstOrDefault(File.Exists);

        if (path is null)
        {
            return;
        }

        ViewModel.SourcePath = path;
        await ViewModel.ScanAsync();
    }

    private void TryEnableDarkTitleBar()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var enabled = 1;
            var result = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
            if (result != 0)
            {
                DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
            }
        }
        catch
        {
            // Dark title bar is cosmetic; ignore failures on older Windows builds.
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
