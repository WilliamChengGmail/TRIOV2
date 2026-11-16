using System.ComponentModel;
using System.Windows;
using TRIOV2.DbIoSimulator.ViewModels;

namespace TRIOV2.DbIoSimulator;

/// <summary>
/// DB I/O 模擬器主視窗後置程式碼 (驅動即時波形圖表與資源釋放)
/// 製作者: Office of William
/// </summary>
public partial class DbSimulatorMainWindow : Window
{
    private readonly float[] _writeHistoryBuffer = new float[60];
    private readonly float[] _readHistoryBuffer = new float[60];

    public DbSimulatorMainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DbSimulatorViewModel vm)
        {
            vm.MetricsRefreshed += OnMetricsRefreshed;
        }
    }

    private void OnMetricsRefreshed()
    {
        if (DataContext is DbSimulatorViewModel vm)
        {
            vm.Monitor.WriteOpsHistory.CopyTo(_writeHistoryBuffer);
            vm.Monitor.ReadOpsHistory.CopyTo(_readHistoryBuffer);
            IoChart.UpdateChart(_writeHistoryBuffer, _readHistoryBuffer, vm.WriteOpsPerSec, vm.ReadOpsPerSec);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is DbSimulatorViewModel vm)
        {
            vm.MetricsRefreshed -= OnMetricsRefreshed;
            vm.Dispose();
        }
    }
}

