using System.Windows;

namespace TRIOV2.DbIoSimulator;

/// <summary>
/// TRIO V2 底層資料庫 I/O 模擬器應用程式進入點
/// 製作者: Office of William
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(
                $"發生非預期錯誤 (UI Dispatcher):\n\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}",
                "TRIOV2.DbIoSimulator 錯誤",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            if (ex.ExceptionObject is Exception err)
            {
                MessageBox.Show(
                    $"發生核心例外狀況 (AppDomain):\n\n{err.Message}\n\n{err.StackTrace}",
                    "TRIOV2.DbIoSimulator 嚴重錯誤",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }
}

