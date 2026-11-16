namespace TRIOV2.ResourceSimulator.Core;

public enum OperatingMode
{
    SingleShot,     // 模式一：單次拍照辨識觸發 (核酸盤面檢測推薦)
    PeriodicBatch,  // 模式二：週期連續排程辨識 (模擬批次流水檢測)
    Continuous      // 模式三：極限連續高壓模式 (尋找崩潰臨界點)
}

/// <summary>
/// 核酸盤面影像工作負載排程器 (管理三種運作模式之狀態轉換與觸發)
/// 製作者: Office of William
/// </summary>
public sealed class WorkloadScheduler : IDisposable
{
    private readonly CpuDutyCycleEngine _cpuEngine;
    private readonly MemoryPressureEngine _memoryEngine;
    private readonly object _stateLock = new();

    private OperatingMode _currentMode = OperatingMode.SingleShot;
    private float _targetCpuPercent = 15.0f;
    private int _targetMemoryMb = 270;
    private int _inferenceDurationMs = 800; // 單次拍圖辨識時長
    private int _periodicIntervalMs = 3000; // 週期模式間隔時間

    private CancellationTokenSource? _periodicCts;
    private Task? _periodicTask;
    private volatile bool _isSingleShotActive;

    public event Action<bool>? ActivityStateChanged; // 告知外部目前是否正在「辨識運算中」

    public OperatingMode CurrentMode => _currentMode;
    public float TargetCpuPercent => _targetCpuPercent;
    public int TargetMemoryMb => _targetMemoryMb;
    public int InferenceDurationMs => _inferenceDurationMs;
    public int PeriodicIntervalMs => _periodicIntervalMs;
    public bool IsActive => _isSingleShotActive || (_currentMode == OperatingMode.Continuous && _cpuEngine.IsRunning);

    public WorkloadScheduler(CpuDutyCycleEngine cpuEngine, MemoryPressureEngine memoryEngine)
    {
        _cpuEngine = cpuEngine;
        _memoryEngine = memoryEngine;
        _memoryEngine.Allocate(_targetMemoryMb);
    }

    public void SetMode(OperatingMode mode)
    {
        lock (_stateLock)
        {
            StopPeriodicLoop();
            _currentMode = mode;

            if (_currentMode == OperatingMode.Continuous)
            {
                _cpuEngine.Start(_targetCpuPercent);
                ActivityStateChanged?.Invoke(true);
            }
            else
            {
                _cpuEngine.Stop();
                ActivityStateChanged?.Invoke(false);
            }
        }
    }

    public void UpdateParameters(float cpuPercent, int memoryMb, int durationMs, int intervalMs)
    {
        lock (_stateLock)
        {
            _targetCpuPercent = Math.Clamp(cpuPercent, 0f, 100f);
            _targetMemoryMb = Math.Clamp(memoryMb, 50, 8192);
            _inferenceDurationMs = Math.Clamp(durationMs, 100, 10000);
            _periodicIntervalMs = Math.Clamp(intervalMs, 500, 30000);

            _memoryEngine.Allocate(_targetMemoryMb);

            if (_currentMode == OperatingMode.Continuous && _cpuEngine.IsRunning)
            {
                _cpuEngine.TargetCpuPercent = _targetCpuPercent;
            }
        }
    }

    /// <summary>
    /// 觸發單次拍圖辨識運算 (Single-Shot Trigger)
    /// </summary>
    public async Task TriggerSingleShotAsync()
    {
        if (_isSingleShotActive) return;

        _isSingleShotActive = true;
        ActivityStateChanged?.Invoke(true);

        try
        {
            // 衝高至目標負載
            _cpuEngine.Start(_targetCpuPercent);
            await Task.Delay(_inferenceDurationMs);
        }
        finally
        {
            if (_currentMode != OperatingMode.Continuous)
            {
                _cpuEngine.Stop();
            }
            _isSingleShotActive = false;
            ActivityStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// 啟動週期連續排程運算
    /// </summary>
    public void StartPeriodic()
    {
        lock (_stateLock)
        {
            StopPeriodicLoop();
            _periodicCts = new CancellationTokenSource();
            var token = _periodicCts.Token;

            _periodicTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await TriggerSingleShotAsync();
                    try
                    {
                        await Task.Delay(_periodicIntervalMs, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }
    }

    /// <summary>
    /// 停止運算 (回歸待機)
    /// </summary>
    public void StopAll()
    {
        lock (_stateLock)
        {
            StopPeriodicLoop();
            _cpuEngine.Stop();
            _isSingleShotActive = false;
            ActivityStateChanged?.Invoke(false);
        }
    }

    private void StopPeriodicLoop()
    {
        if (_periodicCts != null)
        {
            _periodicCts.Cancel();
            _periodicCts.Dispose();
            _periodicCts = null;
        }
        _periodicTask = null;
    }

    public void Dispose()
    {
        StopAll();
    }
}

