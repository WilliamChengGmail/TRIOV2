using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TRIOV2.VisionSimulator.Core;

namespace TRIOV2.VisionSimulator.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>
/// 模擬拍圖辨識視圖模型 (MVVM 狀態中心 - 專注於影像推論負載注入、模式切換與參數排程)
/// 製作者: Office of William
/// </summary>
public sealed class VisionSimulatorViewModel : ViewModelBase, IDisposable
{
    private readonly MemoryPressureEngine _memoryEngine;
    private readonly CpuDutyCycleEngine _cpuEngine;
    private readonly WorkloadScheduler _scheduler;
    private readonly DispatcherTimer _memDebounceTimer;

    // 狀態設定
    private OperatingMode _selectedMode = OperatingMode.SingleShot;
    private float _targetCpuPercent = 15.0f;
    private int _targetMemoryMb = 270;
    private int _inferenceDurationMs = 800;
    private int _periodicIntervalSeconds = 3;
    private bool _isAlwaysOnTop = true;
    private bool _isActive;
    private int _shotCount;
    private string _statusText = "🟢 待機中 (隨時可拍圖辨識)";

    public WorkloadScheduler Scheduler => _scheduler;

    public OperatingMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                _scheduler.SetMode(value);
                UpdateStatus();
                OnPropertyChanged(nameof(IsSingleShotMode));
                OnPropertyChanged(nameof(IsPeriodicMode));
                OnPropertyChanged(nameof(IsContinuousMode));
            }
        }
    }

    public bool IsSingleShotMode
    {
        get => _selectedMode == OperatingMode.SingleShot;
        set { if (value) SelectedMode = OperatingMode.SingleShot; }
    }

    public bool IsPeriodicMode
    {
        get => _selectedMode == OperatingMode.PeriodicBatch;
        set { if (value) SelectedMode = OperatingMode.PeriodicBatch; }
    }

    public bool IsContinuousMode
    {
        get => _selectedMode == OperatingMode.Continuous;
        set { if (value) SelectedMode = OperatingMode.Continuous; }
    }

    public float TargetCpuPercent
    {
        get => _targetCpuPercent;
        set
        {
            if (SetProperty(ref _targetCpuPercent, (float)Math.Round(value, 1)))
            {
                SyncParameters(immediateMemory: false);
            }
        }
    }

    public int TargetMemoryMb
    {
        get => _targetMemoryMb;
        set
        {
            if (SetProperty(ref _targetMemoryMb, value))
            {
                SyncParameters(immediateMemory: false);
            }
        }
    }

    public int InferenceDurationMs
    {
        get => _inferenceDurationMs;
        set
        {
            if (SetProperty(ref _inferenceDurationMs, value))
            {
                SyncParameters(immediateMemory: false);
            }
        }
    }

    public int PeriodicIntervalSeconds
    {
        get => _periodicIntervalSeconds;
        set
        {
            if (SetProperty(ref _periodicIntervalSeconds, Math.Clamp(value, 1, 60)))
            {
                SyncParameters(immediateMemory: false);
            }
        }
    }

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set => SetProperty(ref _isAlwaysOnTop, value);
    }

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                UpdateStatus();
                OnPropertyChanged(nameof(CameraIndicatorColor));
                OnPropertyChanged(nameof(CameraIndicatorText));
            }
        }
    }

    public int ShotCount
    {
        get => _shotCount;
        private set => SetProperty(ref _shotCount, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string CameraIndicatorColor => _isActive ? "#FF3B30" : "#2ECC71";
    public string CameraIndicatorText => _isActive ? "📸 辨識運算中..." : "🟢 待機中";

    // 命令宣告
    public ICommand TriggerSingleShotCommand { get; }
    public ICommand StartPeriodicCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ResetCountCommand { get; }

    public ICommand ApplyPresetBaselineCommand { get; }
    public ICommand ApplyPresetLowCommand { get; }
    public ICommand ApplyPresetHighCommand { get; }

    public VisionSimulatorViewModel()
    {
        _memoryEngine = new MemoryPressureEngine(_targetMemoryMb);
        _cpuEngine = new CpuDutyCycleEngine(_memoryEngine);
        _scheduler = new WorkloadScheduler(_cpuEngine, _memoryEngine);

        _scheduler.ActivityStateChanged += OnActivityStateChanged;

        _memDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _memDebounceTimer.Tick += (s, e) =>
        {
            _memDebounceTimer.Stop();
            _memoryEngine.Allocate(_targetMemoryMb);
        };

        // 命令綁定
        TriggerSingleShotCommand = new RelayCommand(async () =>
        {
            ShotCount++;
            await _scheduler.TriggerSingleShotAsync();
        });

        StartPeriodicCommand = new RelayCommand(() =>
        {
            _scheduler.StartPeriodic();
            UpdateStatus();
        });

        StopCommand = new RelayCommand(() =>
        {
            _scheduler.StopAll();
            UpdateStatus();
        });

        ResetCountCommand = new RelayCommand(() => ShotCount = 0);

        ApplyPresetBaselineCommand = new RelayCommand(() =>
        {
            TargetCpuPercent = 15.0f;
            TargetMemoryMb = 270;
            InferenceDurationMs = 800;
            PeriodicIntervalSeconds = 3;
            SyncParameters(immediateMemory: true);
        });

        ApplyPresetLowCommand = new RelayCommand(() =>
        {
            TargetCpuPercent = 8.0f;
            TargetMemoryMb = 100;
            InferenceDurationMs = 400;
            PeriodicIntervalSeconds = 2;
            SyncParameters(immediateMemory: true);
        });

        ApplyPresetHighCommand = new RelayCommand(() =>
        {
            TargetCpuPercent = 35.0f;
            TargetMemoryMb = 1024;
            InferenceDurationMs = 1500;
            PeriodicIntervalSeconds = 4;
            SyncParameters(immediateMemory: true);
        });

        SyncParameters(immediateMemory: true);
        UpdateStatus();
    }

    private void OnActivityStateChanged(bool active)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            IsActive = active;
        });
    }

    private void SyncParameters(bool immediateMemory)
    {
        if (immediateMemory)
        {
            _memDebounceTimer.Stop();
            _memoryEngine.Allocate(_targetMemoryMb);
        }
        else
        {
            _memDebounceTimer.Stop();
            _memDebounceTimer.Start();
        }

        _scheduler.UpdateParameters(
            _targetCpuPercent,
            _targetMemoryMb,
            _inferenceDurationMs,
            _periodicIntervalSeconds * 1000);
    }

    private void UpdateStatus()
    {
        if (_scheduler.CurrentMode == OperatingMode.Continuous)
        {
            StatusText = $"🔥 極限連續高壓運算中 (CPU 目標 {_targetCpuPercent:F1}%, 記憶體 {_targetMemoryMb} MB)";
        }
        else if (_scheduler.CurrentMode == OperatingMode.PeriodicBatch)
        {
            StatusText = _isActive
                ? $"⚡ 週期辨識中... (每 {_periodicIntervalSeconds}s 觸發一次)"
                : $"⏱ 週期排程待命中 (每 {_periodicIntervalSeconds}s 觸發一次)";
        }
        else
        {
            StatusText = _isActive
                ? $"📸 模擬拍圖辨識推論中 ({_inferenceDurationMs}ms)..."
                : "🟢 待機中 (點擊按鈕或設定排程以觸發辨識)";
        }
    }

    public void Dispose()
    {
        _scheduler.ActivityStateChanged -= OnActivityStateChanged;
        _scheduler.Dispose();
        _cpuEngine.Dispose();
        _memoryEngine.Dispose();
    }
}

