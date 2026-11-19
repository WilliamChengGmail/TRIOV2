using System.Windows;
using TRIOV2.VisionSimulator.ViewModels;

namespace TRIOV2.VisionSimulator;

/// <summary>
/// 模擬拍圖辨識器主視窗後置程式碼
/// 製作者: Office of William
/// </summary>
public partial class VisionMainWindow : Window
{
    public VisionMainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is VisionSimulatorViewModel vm)
        {
            vm.Dispose();
        }
    }
}

