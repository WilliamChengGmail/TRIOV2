using System.Windows;

namespace TRIOV2.ResourceSimulator;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show($"發生非預期錯誤 (UI Dispatcher):\n\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}",
                "TRIOV2.ResourceSimulator 錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            if (ex.ExceptionObject is Exception e2)
            {
                MessageBox.Show($"發生核心例外狀況 (AppDomain):\n\n{e2.Message}\n\n{e2.StackTrace}",
                    "TRIOV2.ResourceSimulator 嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }
}
