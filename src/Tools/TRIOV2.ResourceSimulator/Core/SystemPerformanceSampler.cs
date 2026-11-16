using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// 系統效能即時採樣快照
/// </summary>
public record SystemPerformanceSnapshot(
    float TotalCpuPercent,
    float SimulatorCpuPercent,
    float RemainingCpuPercent,
    float TotalMemoryGb,
    float UsedMemoryGb,
    float AvailableMemoryGb,
    float MemoryUsagePercent,
    float SimulatorMemoryMb
);

/// <summary>
/// 系統效能採樣器 (精準 1Hz 採樣，使用 Win32 原生 API，CPU 開銷趨近於 0)
/// 製作者: Office of William
/// </summary>
public sealed class SystemPerformanceSampler : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public static MEMORYSTATUSEX Create()
        {
            return new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

    private readonly PerformanceRingBuffer _cpuHistory = new(60);
    private readonly PerformanceRingBuffer _memoryHistory = new(60);

    private long _lastIdleTime;
    private long _lastKernelTime;
    private long _lastUserTime;

    private readonly Process _currentProcess;
    private TimeSpan _lastProcessCpuTime;
    private readonly Stopwatch _processStopwatch = new();
    private readonly int _processorCount;

    private readonly System.Threading.Timer _samplingTimer;
    private readonly object _syncLock = new();

    public event Action<SystemPerformanceSnapshot>? SnapshotUpdated;

    public PerformanceRingBuffer CpuHistory => _cpuHistory;
    public PerformanceRingBuffer MemoryHistory => _memoryHistory;

    public SystemPerformanceSampler()
    {
        _currentProcess = Process.GetCurrentProcess();
        _processorCount = Environment.ProcessorCount;

        // 初始化第一次時間基準
        GetSystemTimes(out _lastIdleTime, out _lastKernelTime, out _lastUserTime);
        _lastProcessCpuTime = _currentProcess.TotalProcessorTime;
        _processStopwatch.Start();

        // 建立 1Hz 定時器 (1000ms 觸發一次)
        _samplingTimer = new System.Threading.Timer(OnSampleTick, null, 1000, 1000);
    }

    private void OnSampleTick(object? state)
    {
        lock (_syncLock)
        {
            var snapshot = CaptureSnapshot();
            _cpuHistory.Enqueue(snapshot.TotalCpuPercent);
            _memoryHistory.Enqueue(snapshot.MemoryUsagePercent);

            try
            {
                SnapshotUpdated?.Invoke(snapshot);
            }
            catch
            {
                // 忽略 UI 派送例外
            }
        }
    }

    public SystemPerformanceSnapshot CaptureSnapshot()
    {
        // 1. 計算全系統 CPU 佔用率 (Task Manager 精準算式)
        float totalCpu = 0f;
        if (GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
        {
            long deltaIdle = idleTime - _lastIdleTime;
            long deltaKernel = kernelTime - _lastKernelTime;
            long deltaUser = userTime - _lastUserTime;

            long sysTotal = deltaKernel + deltaUser;
            if (sysTotal > 0)
            {
                totalCpu = Math.Clamp((float)(sysTotal - deltaIdle) * 100f / sysTotal, 0f, 100f);
            }

            _lastIdleTime = idleTime;
            _lastKernelTime = kernelTime;
            _lastUserTime = userTime;
        }

        // 2. 計算本模擬器行程 CPU 佔用率
        _currentProcess.Refresh();
        TimeSpan currentProcessCpuTime = _currentProcess.TotalProcessorTime;
        double elapsedSeconds = _processStopwatch.Elapsed.TotalSeconds;
        _processStopwatch.Restart();

        float simulatorCpu = 0f;
        if (elapsedSeconds > 0.05)
        {
            double cpuUsedSeconds = (currentProcessCpuTime - _lastProcessCpuTime).TotalSeconds;
            simulatorCpu = Math.Clamp((float)(cpuUsedSeconds / (elapsedSeconds * _processorCount) * 100.0), 0f, 100f);
            _lastProcessCpuTime = currentProcessCpuTime;
        }

        float remainingCpu = Math.Max(0f, 100f - totalCpu);

        // 3. 採樣實體記憶體狀態 (Win32 GlobalMemoryStatusEx)
        float totalMemGb = 16f;
        float usedMemGb = 8f;
        float availMemGb = 8f;
        float memUsagePercent = 50f;

        var memStatus = MEMORYSTATUSEX.Create();
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            totalMemGb = (float)(memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0));
            availMemGb = (float)(memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0));
            usedMemGb = totalMemGb - availMemGb;
            memUsagePercent = (float)memStatus.dwMemoryLoad;
        }

        float simulatorMemoryMb = (float)(_currentProcess.WorkingSet64 / (1024.0 * 1024.0));

        return new SystemPerformanceSnapshot(
            TotalCpuPercent: (float)Math.Round(totalCpu, 1),
            SimulatorCpuPercent: (float)Math.Round(simulatorCpu, 1),
            RemainingCpuPercent: (float)Math.Round(remainingCpu, 1),
            TotalMemoryGb: (float)Math.Round(totalMemGb, 1),
            UsedMemoryGb: (float)Math.Round(usedMemGb, 1),
            AvailableMemoryGb: (float)Math.Round(availMemGb, 1),
            MemoryUsagePercent: (float)Math.Round(memUsagePercent, 1),
            SimulatorMemoryMb: (float)Math.Round(simulatorMemoryMb, 1)
        );
    }

    public void Dispose()
    {
        _samplingTimer.Dispose();
        _currentProcess.Dispose();
    }
}

