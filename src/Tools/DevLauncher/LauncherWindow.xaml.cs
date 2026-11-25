using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using MessageBox = System.Windows.MessageBox;

namespace TRIO2026.DevLauncher;

public partial class LauncherWindow : Window
{
    // === Win32 Interop ===
    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // 取得螢幕真實物理尺寸
    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private const int HORZSIZE = 4;    // 螢幕物理寬度 (mm)
    private const int VERTSIZE = 6;    // 螢幕物理高度 (mm)
    private const int HORZRES = 8;     // 螢幕像素寬度
    private const int VERTRES = 10;    // 螢幕像素高度

    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int SW_SHOW = 5;

    // 截圖用
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    private const uint SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    // === 狀態變數 ===
    private Process? _appProcess;
    private IntPtr _appWindowHandle = IntPtr.Zero;
    private string? _customAppExePath;

    private int _simWidth = 1920;
    private int _simHeight = 1080;
    private double _scaleFactor = 1.0;
    private readonly DispatcherTimer _embedTimer = new();

    // 預設面板尺寸 (15.6 吋 16:9 物理尺寸)
    private double _panelDiagonalInch = 15.6;
    private double _panelWidthCm = 34.5;
    private double _panelHeightCm = 19.4;
    private const double MinPanelWidthCm = 5.0;
    private const double MinPanelHeightCm = 5.0;

    // 當前開發機螢幕參數 (校準用)
    private double _devMonitorInch = 27.0;
    private int _devMonitorResW = 2560;
    private int _devMonitorResH = 1440;

    // 螢幕真實物理像素密度（pixels per cm）
    private double _realPixelsPerCmX = 96.0 / 2.54;  // 預設 96 DPI
    private double _realPixelsPerCmY = 96.0 / 2.54;
    private double _wpfScaleX = 1.0; // WPF DPI 縮放

    // 工具列 + 狀態列高度估算
    private const double ToolbarHeight = 135;
    private const double StatusBarHeight = 32;
    private const double ChromeHeight = 38;

    // Win32 嵌入容器
    private readonly System.Windows.Forms.Panel _hostPanel = new()
    {
        Dock = System.Windows.Forms.DockStyle.Fill,
        BackColor = System.Drawing.Color.FromArgb(17, 24, 39)
    };

    public LauncherWindow()
    {
        InitializeComponent();

        // 將 WinForms Panel 放入 WindowsFormsHost
        WfHost.Child = _hostPanel;

        _embedTimer.Interval = TimeSpan.FromMilliseconds(50);
        _embedTimer.Tick += EmbedTimer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. 自動偵測開發機螢幕
        DetectRealScreenSize();

        // 2. 依預設開發機螢幕參數精算 PPI
        RecalculateDevMonitorPpi();

        // 3. 嘗試尋找預設 App.exe
        UpdateAppExeStatus();

        // 4. 套用初始吋數與長寬計算
        UpdatePanelDimensionFromInch();

        // 5. 刷新解析度與視窗佈局
        ApplyResolution();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        KillAppProcess();
    }

    /// <summary>依據當前開發機輸入的螢幕吋數與解析度精算真實 PPI</summary>
    private void RecalculateDevMonitorPpi()
    {
        if (DevMonitorInchBox == null || DevMonitorResWBox == null || DevMonitorResHBox == null) return;

        if (double.TryParse(DevMonitorInchBox.Text, out var inch) &&
            int.TryParse(DevMonitorResWBox.Text, out var resW) &&
            int.TryParse(DevMonitorResHBox.Text, out var resH) &&
            inch > 0 && resW > 0 && resH > 0)
        {
            _devMonitorInch = inch;
            _devMonitorResW = resW;
            _devMonitorResH = resH;

            var aspect = (double)resW / resH;
            var diagMm = inch * 25.4; // 1 inch = 25.4 mm

            var widthMm = Math.Sqrt((diagMm * diagMm) / (1.0 + (1.0 / (aspect * aspect))));
            var heightMm = widthMm / aspect;

            _realPixelsPerCmX = resW / (widthMm / 10.0);
            _realPixelsPerCmY = resH / (heightMm / 10.0);

            var ppi = _realPixelsPerCmX * 2.54;
            if (DevPpiText != null)
            {
                DevPpiText.Text = $"計算 PPI: {ppi:F1}";
            }

            var physWcm = widthMm / 10.0;
            var physHcm = heightMm / 10.0;
            DevScreenInfo.Text = $"當前開發機: {resW}×{resH} | {physWcm:F1}×{physHcm:F1} cm ({inch:F1}\") | PPI: {ppi:F0}";
        }
    }

    /// <summary>點擊套用開發機螢幕設定</summary>
    private void ApplyDevMonitor_Click(object sender, RoutedEventArgs e)
    {
        RecalculateDevMonitorPpi();
        ApplyResolution();
    }

    /// <summary>自動偵測開發機螢幕 EDID</summary>
    private void AutoDetectDevMonitor_Click(object sender, RoutedEventArgs e)
    {
        DetectRealScreenSize();
        ApplyResolution();
    }

    /// <summary>自動搜尋或更新 App.exe 狀態</summary>
    private void UpdateAppExeStatus()
    {
        var appExe = FindAppExe();
        if (appExe != null)
        {
            AppPathText.Text = $"目標 App: {Path.GetFileName(appExe)} ({appExe})";
            AppPathText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#81D4FA"));
        }
        else
        {
            AppPathText.Text = "⚠ 找不到 TRIOV2.App.exe，請點擊「選擇 App...」指明路徑";
            AppPathText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF8A80"));
        }
    }

    /// <summary>選擇可執行檔按鈕</summary>
    private void BrowseApp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "可執行檔 (*.exe)|*.exe",
            Title = "選擇 TRIOV2.App.exe 檔案"
        };

        if (dialog.ShowDialog() == true)
        {
            _customAppExePath = dialog.FileName;
            UpdateAppExeStatus();
        }
    }

    /// <summary>解析度下拉選單變更</summary>
    private void Resolution_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (ResolutionCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var parts = tag.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
            {
                _simWidth = w;
                _simHeight = h;
                UpdatePanelDimensionFromInch();
                ApplyResolution();
            }
        }
    }

    /// <summary>吋數下拉選擇事件</summary>
    private void InchCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (InchCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            if (double.TryParse(tag, out var inch))
            {
                _panelDiagonalInch = inch;
                UpdatePanelDimensionFromInch();
                ApplyResolution();
            }
        }
    }

    /// <summary>依據吋數 (Diagonal Inch) 與目前比例算物理長寬 (cm)</summary>
    private void UpdatePanelDimensionFromInch()
    {
        if (_panelDiagonalInch <= 0) return;

        // 特殊預設集精確設定：10.1 吋標準觸控面板 (物理寬 21.8cm × 物理高 13.7cm)
        if (Math.Abs(_panelDiagonalInch - 10.1) < 0.05)
        {
            _panelWidthCm = 21.8;
            _panelHeightCm = 13.7;
        }
        else
        {
            var aspect = (double)_simWidth / _simHeight;
            var diagMm = _panelDiagonalInch * 25.4; // 1 inch = 25.4 mm

            var widthMm = Math.Sqrt((diagMm * diagMm) / (1.0 + (1.0 / (aspect * aspect))));
            var heightMm = widthMm / aspect;

            _panelWidthCm = Math.Round(widthMm / 10.0, 1);
            _panelHeightCm = Math.Round(heightMm / 10.0, 1);
        }

        PanelWidthBox.Text = _panelWidthCm.ToString("F1");
        PanelHeightBox.Text = _panelHeightCm.ToString("F1");
    }

    /// <summary>勾選 1:1 實物物理尺寸切換</summary>
    private void Real1To1_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyResolution();
    }

    /// <summary>計算並套用模擬器畫面與視窗尺寸</summary>
    private void ApplyResolution()
    {
        double displayW;
        double displayH;

        bool isReal1To1 = Real1To1Check?.IsChecked == true;

        if (isReal1To1)
        {
            // 模式 1：1:1 精準毫米實物物理大小
            // 使用開發者顯示器的真實 pixels-per-cm 轉換為像素
            var physPixelW = _panelWidthCm * _realPixelsPerCmX;
            var physPixelH = _panelHeightCm * _realPixelsPerCmY;

            // 轉換為 WPF 邏輯 Dip
            displayW = physPixelW / _wpfScaleX;
            displayH = physPixelH / _wpfScaleX;

            var maxW = SystemParameters.WorkArea.Width - 40;
            var maxH = SystemParameters.WorkArea.Height - ToolbarHeight - StatusBarHeight - ChromeHeight - 40;

            if (displayW > maxW || displayH > maxH)
            {
                _scaleFactor = Math.Min(maxW / displayW, maxH / displayH);
                displayW *= _scaleFactor;
                displayH *= _scaleFactor;
            }
            else
            {
                _scaleFactor = 1.0;
            }
        }
        else
        {
            // 模式 2：比例展現適應
            var panelWpfW = (double)_simWidth;
            var panelWpfH = (double)_simHeight;

            var maxW = SystemParameters.WorkArea.Width - 60;
            var maxH = SystemParameters.WorkArea.Height - ToolbarHeight - StatusBarHeight - ChromeHeight - 60;

            _scaleFactor = Math.Min(maxW / panelWpfW, maxH / panelWpfH);
            if (_scaleFactor > 1.0) _scaleFactor = 1.0;

            displayW = panelWpfW * _scaleFactor;
            displayH = panelWpfH * _scaleFactor;
        }

        // 設定容器尺寸
        ScreenContainer.Width = displayW;
        ScreenContainer.Height = displayH;

        // 更新 UI 提示文字
        var scaleInfo = _scaleFactor < 1.0 ? $" (視窗縮放 {_scaleFactor * 100:F0}%)" : "";
        SimResText.Text = $"模擬解析度: {_simWidth} × {_simHeight} | 物理面板: {_panelWidthCm:F1} × {_panelHeightCm:F1} cm ({_panelDiagonalInch:F1}\"){scaleInfo}";
        ScaleText.Text = isReal1To1 ? $"1:1 實物 ({_scaleFactor * 100:F0}%)" : $"{_scaleFactor * 100:F0}%";

        // 調整外層主視窗大小
        SizeToContent = SizeToContent.Manual;
        Width = Math.Max(820, displayW + 36);
        Height = displayH + ToolbarHeight + StatusBarHeight + ChromeHeight + 24;

        // 置中
        Left = (SystemParameters.WorkArea.Width - Width) / 2;
        Top = (SystemParameters.WorkArea.Height - Height) / 2;

        // 若 App 已嵌入，調整子視窗尺寸
        if (_appWindowHandle != IntPtr.Zero)
        {
            MoveWindow(_appWindowHandle, 0, 0, (int)(displayW * _wpfScaleX), (int)(displayH * _wpfScaleX), true);
        }

        StatusText.Text = $"模擬: {_simWidth}×{_simHeight} | 面板: {_panelWidthCm:F1}×{_panelHeightCm:F1} cm ({_panelDiagonalInch:F1}\") | 當前 PPI: {_realPixelsPerCmX * 2.54:F0}";
    }

    /// <summary>偵測開發者螢幕真實物理 PPI</summary>
    private void DetectRealScreenSize()
    {
        try
        {
            var hdc = GetDC(IntPtr.Zero);
            var physWidthMm = GetDeviceCaps(hdc, HORZSIZE);   // 物理寬 (mm)
            var physHeightMm = GetDeviceCaps(hdc, VERTSIZE);  // 物理高 (mm)
            var pixelWidth = GetDeviceCaps(hdc, HORZRES);     // 像素寬
            var pixelHeight = GetDeviceCaps(hdc, VERTRES);    // 像素高
            ReleaseDC(IntPtr.Zero, hdc);

            var src = PresentationSource.FromVisual(this);
            if (src?.CompositionTarget != null)
                _wpfScaleX = src.CompositionTarget.TransformToDevice.M11;

            if (DevMonitorResWBox != null) DevMonitorResWBox.Text = pixelWidth.ToString();
            if (DevMonitorResHBox != null) DevMonitorResHBox.Text = pixelHeight.ToString();

            if (physWidthMm > 0 && physHeightMm > 0)
            {
                _realPixelsPerCmX = pixelWidth / (physWidthMm / 10.0);
                _realPixelsPerCmY = pixelHeight / (physHeightMm / 10.0);

                var physWcm = physWidthMm / 10.0;
                var physHcm = physHeightMm / 10.0;
                var diagInch = Math.Round(Math.Sqrt(physWcm * physWcm + physHcm * physHcm) / 2.54, 1);
                if (DevMonitorInchBox != null) DevMonitorInchBox.Text = diagInch.ToString("F1");
            }
        }
        catch
        {
            if (DevMonitorInchBox != null) DevMonitorInchBox.Text = "27.0";
            if (DevMonitorResWBox != null) DevMonitorResWBox.Text = "2560";
            if (DevMonitorResHBox != null) DevMonitorResHBox.Text = "1440";
        }
    }

    private void PanelSize_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender is WpfTextBox tb)
        {
            tb.Foreground = double.TryParse(tb.Text, out _)
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White)
                : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF5350"));
        }
    }

    private void ApplySize_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(PanelWidthBox.Text, out var w) || !double.TryParse(PanelHeightBox.Text, out var h))
        {
            MessageBox.Show("請輸入有效的數值（例: 34.5）", "輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (w < MinPanelWidthCm) { w = MinPanelWidthCm; PanelWidthBox.Text = w.ToString("F1"); }
        if (h < MinPanelHeightCm) { h = MinPanelHeightCm; PanelHeightBox.Text = h.ToString("F1"); }

        _panelWidthCm = w;
        _panelHeightCm = h;

        // 計算物理對角線吋數
        _panelDiagonalInch = Math.Round(Math.Sqrt(w * w + h * h) / 2.54, 1);
        ApplyResolution();
    }

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
            var dir = Path.Combine(baseDir, "Screenshots");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var helper = new WindowInteropHelper(this);
            GetWindowRect(helper.Handle, out var rect);
            var w = rect.Right - rect.Left;
            var h = rect.Bottom - rect.Top;

            var hdcScreen = GetDC(IntPtr.Zero);
            var hdcMem = CreateCompatibleDC(hdcScreen);
            var hBitmap = CreateCompatibleBitmap(hdcScreen, w, h);
            var hOld = SelectObject(hdcMem, hBitmap);

            BitBlt(hdcMem, 0, 0, w, h, hdcScreen, rect.Left, rect.Top, SRCCOPY);
            SelectObject(hdcMem, hOld);

            var bmpSrc = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmpSrc));

            var filename = $"sim_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filepath = Path.Combine(dir, filename);
            using (var fs = new FileStream(filepath, FileMode.Create))
                encoder.Save(fs);

            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);

            StatusText.Text = $"📷 截圖已儲存至: {filepath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"截圖失敗: {ex.Message}";
        }
    }

    /// <summary>啟動 TRIOV2.App</summary>
    private void Run_Click(object sender, RoutedEventArgs e)
    {
        var appExe = FindAppExe();
        if (appExe == null)
        {
            MessageBox.Show(
                "找不到 TRIOV2.App.exe！\n\n請確認專案已順利建置，或點擊「選擇 App...」指定可執行檔位置。",
                "找不到應用程式", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            PlaceholderGrid.Visibility = Visibility.Collapsed;
            WfHost.Visibility = Visibility.Visible;

            var logicalW = (int)ScreenContainer.ActualWidth;
            var logicalH = (int)ScreenContainer.ActualHeight;

            var psi = new ProcessStartInfo
            {
                FileName = appExe,
                Arguments = $"--sim-width {logicalW} --sim-height {logicalH} --sim-touch 1 --sim-fullscreen 0 --sim-embedded 1",
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(appExe)!
            };

            _appProcess = Process.Start(psi);
            _embedTimer.Start();

            RunButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            ResolutionCombo.IsEnabled = false;
            InchCombo.IsEnabled = false;
            StatusText.Text = $"正在啟動 {Path.GetFileName(appExe)} ...";
        }
        catch (Exception ex)
        {
            PlaceholderGrid.Visibility = Visibility.Visible;
            WfHost.Visibility = Visibility.Collapsed;
            MessageBox.Show($"啟動失敗:\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EmbedTimer_Tick(object? sender, EventArgs e)
    {
        if (_appProcess == null || _appProcess.HasExited)
        {
            _embedTimer.Stop();
            OnAppExited();
            return;
        }

        _appProcess.Refresh();
        var hWnd = _appProcess.MainWindowHandle;

        if (hWnd == IntPtr.Zero) return;

        _embedTimer.Stop();
        _appWindowHandle = hWnd;

        // 1. 立即隱藏原生視窗，防止在中央移動過程中閃爍
        ShowWindow(hWnd, 0 /* SW_HIDE */);

        var containerHwnd = _hostPanel.Handle;
        var style = GetWindowLong(hWnd, GWL_STYLE);
        SetWindowLong(hWnd, GWL_STYLE, (style & ~0x00C00000 & ~0x00040000) | WS_CHILD);

        // 2. 設置父容器為 DevLauncher 的 Panel
        SetParent(hWnd, containerHwnd);

        // 3. 調整尺寸與位置
        var displayW = (int)(ScreenContainer.ActualWidth * _wpfScaleX);
        var displayH = (int)(ScreenContainer.ActualHeight * _wpfScaleX);
        MoveWindow(hWnd, 0, 0, displayW, displayH, true);

        // 4. 完美顯示於 DevLauncher 模擬器容器內
        ShowWindow(hWnd, SW_SHOW);

        StatusText.Text = $"✅ TRIOV2.App 運行中 — 面板 {_panelDiagonalInch:F1}\" ({_panelWidthCm}×{_panelHeightCm} cm)";

        _appProcess.EnableRaisingEvents = true;
        _appProcess.Exited += (s, args) => Dispatcher.Invoke(OnAppExited);
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        KillAppProcess();
    }

    private void KillAppProcess()
    {
        _embedTimer.Stop();
        if (_appProcess != null && !_appProcess.HasExited)
        {
            try { _appProcess.Kill(); } catch { }
        }
        _appProcess = null;
        _appWindowHandle = IntPtr.Zero;
        OnAppExited();
    }

    private void OnAppExited()
    {
        PlaceholderGrid.Visibility = Visibility.Visible;
        WfHost.Visibility = Visibility.Collapsed;
        RunButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        ResolutionCombo.IsEnabled = true;
        InchCombo.IsEnabled = true;
        StatusText.Text = "已停止";
    }

    /// <summary>搜尋 TRIOV2.App.exe 位置</summary>
    private string? FindAppExe()
    {
        if (!string.IsNullOrEmpty(_customAppExePath) && File.Exists(_customAppExePath))
            return _customAppExePath;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidates = new[]
            {
                Path.Combine(dir, "src", "TRIOV2.App", "bin", "Debug", "net8.0-windows", "TRIOV2.App.exe"),
                Path.Combine(dir, "src", "TRIOV2.App", "bin", "Release", "net8.0-windows", "TRIOV2.App.exe"),
                Path.Combine(dir, "App", "TRIOV2.App.exe"),
                Path.Combine(dir, "publish", "TRIOV2.App.exe"),
            };

            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        return null;
    }
}
