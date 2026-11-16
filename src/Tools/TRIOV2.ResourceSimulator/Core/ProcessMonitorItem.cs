using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// 工作管理員單一行程效能監控項目
/// 製作者: Office of William
/// </summary>
public sealed class ProcessMonitorItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isCustomTarget;
    private bool _isRunning = true;
    private string _statusText = "執行中";
    private float _cpuPercent;
    private double _memoryMb;
    private double _diskIoKbPerSec;
    private double _networkKbps;
    private string _powerUsage = "極低";
    private string _powerTrend = "非常低";

    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string MainWindowTitle { get; init; } = string.Empty;

    /// <summary>顯示於 UI 的 PID 文字 (尚未執行的額外監控任務顯示 "—")</summary>
    public string ProcessIdText => ProcessId > 0 ? ProcessId.ToString() : "—";

    /// <summary>是否為使用者手動輸入或匯入的額外追蹤任務</summary>
    public bool IsCustomTarget
    {
        get => _isCustomTarget;
        set => SetProperty(ref _isCustomTarget, value);
    }

    /// <summary>該任務目前是否已在系統中實際執行</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>顯示於 UI 的易讀名稱 (若有視窗標題則附帶)</summary>
    public string DisplayName => !IsRunning
        ? $"{ProcessName} (額外監控目標 — 尚未執行)"
        : string.IsNullOrWhiteSpace(MainWindowTitle)
            ? ProcessName
            : $"{ProcessName} - {MainWindowTitle}";

    /// <summary>是否勾選作為效能量測紀錄目標</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>狀態 (執行中 / 沒有回應)</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>CPU 使用率百分比</summary>
    public float CpuPercent
    {
        get => _cpuPercent;
        set
        {
            if (SetProperty(ref _cpuPercent, value))
            {
                OnPropertyChanged(nameof(CpuPercentText));
            }
        }
    }

    public string CpuPercentText => $"{_cpuPercent:0.0} %";

    /// <summary>記憶體使用量 (MB)</summary>
    public double MemoryMb
    {
        get => _memoryMb;
        set
        {
            if (SetProperty(ref _memoryMb, value))
            {
                OnPropertyChanged(nameof(MemoryText));
            }
        }
    }

    public string MemoryText => _memoryMb >= 1024.0
        ? $"{(_memoryMb / 1024.0):0.00} GB"
        : $"{_memoryMb:0.0} MB";

    /// <summary>磁碟讀寫速率 (KB/s)</summary>
    public double DiskIoKbPerSec
    {
        get => _diskIoKbPerSec;
        set
        {
            if (SetProperty(ref _diskIoKbPerSec, value))
            {
                OnPropertyChanged(nameof(DiskIoText));
            }
        }
    }

    public string DiskIoText => _diskIoKbPerSec >= 1024.0
        ? $"{(_diskIoKbPerSec / 1024.0):0.0} MB/s"
        : $"{_diskIoKbPerSec:0.0} KB/s";

    /// <summary>網路傳輸速率 (Kbps)</summary>
    public double NetworkKbps
    {
        get => _networkKbps;
        set
        {
            if (SetProperty(ref _networkKbps, value))
            {
                OnPropertyChanged(nameof(NetworkText));
            }
        }
    }

    public string NetworkText => _networkKbps >= 1024.0
        ? $"{(_networkKbps / 1024.0):0.0} Mbps"
        : $"{_networkKbps:0.0} Kbps";

    /// <summary>電源用量 (極低 / 低 / 中 / 高 / 非常高)</summary>
    public string PowerUsage
    {
        get => _powerUsage;
        set => SetProperty(ref _powerUsage, value);
    }

    /// <summary>電源用量趨勢 (非常低 / 低 / 中 / 高 / 上升中 / 下降中)</summary>
    public string PowerTrend
    {
        get => _powerTrend;
        set => SetProperty(ref _powerTrend, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

