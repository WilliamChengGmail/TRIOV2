using System.Diagnostics;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// 高精度微秒級 CPU Duty-Cycle 負載引擎 (支援多核心均勻調度與微秒級混合等待)
/// 製作者: Office of William
/// </summary>
public sealed class CpuDutyCycleEngine : IDisposable
{
    private readonly MemoryPressureEngine _memoryEngine;
    private readonly int _processorCount;
    private readonly List<Thread> _workerThreads = new();
    private readonly CancellationTokenSource _cts = new();

    private volatile float _targetCpuPercent; // 0.0 ~ 100.0
    private volatile bool _isRunning;

    public float TargetCpuPercent
    {
        get => _targetCpuPercent;
        set => _targetCpuPercent = Math.Clamp(value, 0f, 100f);
    }

    public bool IsRunning => _isRunning;

    public CpuDutyCycleEngine(MemoryPressureEngine memoryEngine)
    {
        _memoryEngine = memoryEngine;
        _processorCount = Environment.ProcessorCount;

        // 啟動工作執行緒 (執行緒數等於邏輯核心數，全核均勻負載)
        for (int i = 0; i < _processorCount; i++)
        {
            int threadId = i;
            var thread = new Thread(() => WorkerLoop(threadId, _cts.Token))
            {
                Name = $"ResourceSim_Worker_{threadId}",
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            _workerThreads.Add(thread);
            thread.Start();
        }
    }

    public void Start(float targetPercent)
    {
        TargetCpuPercent = targetPercent;
        _isRunning = true;
    }

    public void Stop()
    {
        _isRunning = false;
    }

    private void WorkerLoop(int threadId, CancellationToken ct)
    {
        var sw = new Stopwatch();
        long periodMs = 100; // 每個調控週期為 100ms
        long periodTicks = periodMs * Stopwatch.Frequency / 1000;

        while (!ct.IsCancellationRequested)
        {
            float targetPercent = _isRunning ? _targetCpuPercent : 0f;

            if (targetPercent <= 0.5f)
            {
                // 空閒待機，極低開銷休眠
                Thread.Sleep(50);
                continue;
            }

            sw.Restart();
            double dutyRatio = Math.Clamp(targetPercent / 100.0, 0.0, 1.0);
            long busyTicks = (long)(periodTicks * dutyRatio);

            // 1. Busy 運算階段：密集計算 + 記憶體走訪
            while (sw.ElapsedTicks < busyTicks)
            {
                // 每批次做 500 次記憶體步進讀寫與矩陣混合計算
                _memoryEngine.PerformMemorySweep(200);

                // 補充浮點運算以逼近真實推理計算
                double temp = 0.0;
                for (int j = 0; j < 500; j++)
                {
                    temp += Math.Sqrt(j * 3.14159);
                }
            }

            // 2. Sleep 階段：混合休眠 (Hybrid Wait)
            long elapsed = sw.ElapsedTicks;
            long remainingTicks = periodTicks - elapsed;
            if (remainingTicks > 0)
            {
                double remainingMs = (double)remainingTicks * 1000.0 / Stopwatch.Frequency;
                if (remainingMs > 4.0)
                {
                    // 長休眠調用 OS 排程休眠
                    Thread.Sleep((int)(remainingMs - 2.5));
                }

                // 最後 2ms 使用微輪詢確保時脈精準度，避免 Windows 排程抖動
                while (sw.ElapsedTicks < periodTicks)
                {
                    Thread.SpinWait(10);
                }
            }
        }
    }

    public void Dispose()
    {
        _isRunning = false;
        _cts.Cancel();
        foreach (var t in _workerThreads)
        {
            t.Join(200);
        }
        _cts.Dispose();
    }
}

