using System.Windows;
using TRIOV2.ResourceSimulator.ViewModels;

namespace TRIOV2.ResourceSimulator;

/// <summary>
/// 模擬器主視窗後置程式碼 (定時驅動圖表渲染)
/// 製作者: Office of William
/// </summary>
public partial class SimulatorMainWindow : Window
{
    private readonly float[] _cpuHistoryBuffer = new float[60];
    private readonly float[] _memoryHistoryBuffer = new float[60];

    public SimulatorMainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SimulatorViewModel vm)
        {
            vm.SnapshotRefreshed += OnSnapshotRefreshed;
        }
    }

    private void OnSnapshotRefreshed()
    {
        if (DataContext is SimulatorViewModel vm)
        {
            // 複製 60 秒數據至固定陣列並繪製 (零 GC 記憶體分配)
            vm.Sampler.CpuHistory.CopyTo(_cpuHistoryBuffer);
            vm.Sampler.MemoryHistory.CopyTo(_memoryHistoryBuffer);

            CpuChart.UpdateChart(_cpuHistoryBuffer, vm.TotalCpuText);
            MemoryChart.UpdateChart(_memoryHistoryBuffer, vm.MemoryUsagePercentText);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is SimulatorViewModel vm)
        {
            vm.SnapshotRefreshed -= OnSnapshotRefreshed;
            vm.Dispose();
        }
    }
}

