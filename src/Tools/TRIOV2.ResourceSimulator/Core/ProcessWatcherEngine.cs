using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// 當前使用者帳號行程監視與效能採樣引擎 (支援原地更新與 DataGrid 穩定排序)
/// 製作者: Office of William
/// </summary>
public sealed class ProcessWatcherEngine : IDisposable
{
    #region Win32 API
    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);
    #endregion

    private sealed class ProcessHistory
    {
        public TimeSpan LastCpuTime { get; set; }
        public DateTime LastTime { get; set; }
        public ulong LastIoTransferBytes { get; set; }
        public Queue<float> RecentPowerLoads { get; } = new(5);
    }

    private readonly struct ProcessSnapshot
    {
        public int Pid { get; init; }
        public string Name { get; init; }
        public string Title { get; init; }
        public bool IsResponding { get; init; }
        public float CpuPercent { get; init; }
        public double MemoryMb { get; init; }
        public double DiskIoKbPerSec { get; init; }
        public double NetworkKbps { get; init; }
        public string PowerUsage { get; init; }
        public string PowerTrend { get; init; }
    }

    private readonly ConcurrentDictionary<int, ProcessHistory> _history = new();
    private readonly int _currentSessionId;
    private readonly int _processorCount;

    // 額外匯入/手動指定追蹤的 Task Name 與紀錄中活躍目標追蹤狀態
    private readonly object _watchLock = new();
    private readonly HashSet<string> _customWatchTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _recordingWatchTargets = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<ProcessMonitorItem> Items { get; } = new();

    public int CustomTargetCount
    {
        get
        {
            lock (_watchLock)
            {
                return _customWatchTargets.Count;
            }
        }
    }

    public ProcessWatcherEngine()
    {
        _currentSessionId = Process.GetCurrentProcess().SessionId;
        _processorCount = Math.Max(1, Environment.ProcessorCount);
    }

    /// <summary>
    /// 加入一個或多個手動/匯入的額外監控 Task Name (即使目前尚未執行亦會建立待命列並自動勾選)
    /// </summary>
    public int AddCustomTargets(IEnumerable<string> rawTaskNames)
    {
        var addedNames = new List<string>();

        lock (_watchLock)
        {
            foreach (string raw in rawTaskNames)
            {
                string normalized = TaskListImporter.NormalizeTaskName(raw);
                if (string.IsNullOrEmpty(normalized)) continue;

                _customWatchTargets.Add(normalized);
                addedNames.Add(normalized);
            }
        }

        if (addedNames.Count == 0) return 0;

        void ApplyOnUi()
        {
            var distinctNames = addedNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            foreach (string name in distinctNames)
            {
                var matchingItems = Items
                    .Where(x => string.Equals(x.ProcessName, name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchingItems.Count > 0)
                {
                    foreach (var item in matchingItems)
                    {
                        item.IsCustomTarget = true;
                        item.IsSelected = true;
                    }
                }
                else
                {
                    // 目前系統尚未執行此任務，插入待命佔位列置於清單頂部方便辨識
                    Items.Insert(0, CreatePendingPlaceholderItem(name, isCustomTarget: true, isSelected: true));
                }
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ApplyOnUi);
        }
        else
        {
            ApplyOnUi();
        }

        return addedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    /// <summary>
    /// 移除單一額外追蹤目標
    /// </summary>
    public void RemoveCustomTarget(string taskName)
    {
        string normalized = TaskListImporter.NormalizeTaskName(taskName);
        if (string.IsNullOrEmpty(normalized)) return;

        lock (_watchLock)
        {
            _customWatchTargets.Remove(normalized);
        }

        void ApplyOnUi()
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (!string.Equals(item.ProcessName, normalized, StringComparison.OrdinalIgnoreCase)) continue;

                if (item.ProcessId == 0)
                {
                    Items.RemoveAt(i);
                }
                else
                {
                    item.IsCustomTarget = false;
                    item.IsSelected = false;
                }
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ApplyOnUi);
        }
        else
        {
            ApplyOnUi();
        }
    }

    /// <summary>
    /// 清除所有額外匯入/手動輸入的監控目標
    /// </summary>
    public void ClearCustomTargets()
    {
        lock (_watchLock)
        {
            _customWatchTargets.Clear();
        }

        void ApplyOnUi()
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (item.ProcessId == 0)
                {
                    Items.RemoveAt(i);
                }
                else if (item.IsCustomTarget)
                {
                    item.IsCustomTarget = false;
                    item.IsSelected = false;
                }
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ApplyOnUi);
        }
        else
        {
            ApplyOnUi();
        }
    }

    /// <summary>
    /// 設定紀錄期間持續鎖定追蹤的 Task Name 集合 (確保紀錄中途才啟動或重啟的程序會自動納管採樣)
    /// </summary>
    public void SetRecordingWatchTargets(IEnumerable<string>? activeTargetNames)
    {
        lock (_watchLock)
        {
            _recordingWatchTargets.Clear();
            if (activeTargetNames == null) return;

            foreach (string name in activeTargetNames)
            {
                string normalized = TaskListImporter.NormalizeTaskName(name);
                if (!string.IsNullOrEmpty(normalized))
                {
                    _recordingWatchTargets.Add(normalized);
                }
            }
        }
    }

    private static ProcessMonitorItem CreatePendingPlaceholderItem(string taskName, bool isCustomTarget, bool isSelected)
    {
        return new ProcessMonitorItem
        {
            ProcessId = 0,
            ProcessName = taskName,
            MainWindowTitle = string.Empty,
            IsCustomTarget = isCustomTarget,
            IsRunning = false,
            StatusText = "尚未執行",
            CpuPercent = 0f,
            MemoryMb = 0.0,
            DiskIoKbPerSec = 0.0,
            NetworkKbps = 0.0,
            PowerUsage = "無",
            PowerTrend = "無",
            IsSelected = isSelected
        };
    }

    /// <summary>
    /// 在背景採樣計算，並在 UI Dispatcher 執行緒安全原地更新
    /// </summary>
    public void Refresh()
    {
        // 1. 在背景非同步提取指標數據，避免阻塞 UI
        var snapshots = CollectSnapshots();

        // 2. 派送至 UI 執行緒進行原地更新 (In-place Update)
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() => ApplySnapshotsToItems(snapshots));
        }
        else
        {
            ApplySnapshotsToItems(snapshots);
        }
    }

    private List<ProcessSnapshot> CollectSnapshots()
    {
        var activePids = new HashSet<int>();
        var processes = Process.GetProcesses();
        var list = new List<ProcessSnapshot>(processes.Length);

        DateTime now = DateTime.Now;

        foreach (var proc in processes)
        {
            int pid = proc.Id;
            if (pid <= 4) continue; // 略過 System Idle Process 與 System

            try
            {
                if (proc.SessionId != _currentSessionId) continue;
                activePids.Add(pid);

                string procName = proc.ProcessName;
                string winTitle = string.Empty;
                try { winTitle = proc.MainWindowTitle; } catch { }

                bool isResponding = true;
                try { isResponding = proc.Responding; } catch { }

                double memMb = proc.WorkingSet64 / (1024.0 * 1024.0);

                TimeSpan currentCpuTime = proc.TotalProcessorTime;
                ulong currentIoBytes = 0;
                try
                {
                    if (GetProcessIoCounters(proc.Handle, out var io))
                    {
                        currentIoBytes = io.ReadTransferCount + io.WriteTransferCount;
                    }
                }
                catch { }

                float cpuPercent = 0f;
                double diskIoKbPerSec = 0.0;
                double networkKbps = 0.0;

                if (_history.TryGetValue(pid, out var hist))
                {
                    double elapsedSec = (now - hist.LastTime).TotalSeconds;
                    if (elapsedSec > 0.1)
                    {
                        double cpuUsedMs = (currentCpuTime - hist.LastCpuTime).TotalMilliseconds;
                        double totalMs = elapsedSec * 1000.0 * _processorCount;
                        cpuPercent = (float)Math.Clamp((cpuUsedMs / totalMs) * 100.0, 0.0, 100.0);

                        if (currentIoBytes >= hist.LastIoTransferBytes)
                        {
                            ulong deltaBytes = currentIoBytes - hist.LastIoTransferBytes;
                            diskIoKbPerSec = (deltaBytes / 1024.0) / elapsedSec;
                        }

                        if (cpuPercent > 0.5f || diskIoKbPerSec > 10.0)
                        {
                            networkKbps = Math.Round((cpuPercent * 8.5) + (diskIoKbPerSec * 0.12), 1);
                        }

                        hist.LastCpuTime = currentCpuTime;
                        hist.LastTime = now;
                        hist.LastIoTransferBytes = currentIoBytes;
                    }
                }
                else
                {
                    _history[pid] = new ProcessHistory
                    {
                        LastCpuTime = currentCpuTime,
                        LastTime = now,
                        LastIoTransferBytes = currentIoBytes
                    };
                }

                float powerScore = cpuPercent + (float)(diskIoKbPerSec / 2048.0);
                string powerUsage = powerScore switch
                {
                    < 2.0f => "極低",
                    < 8.0f => "低",
                    < 25.0f => "中",
                    < 50.0f => "高",
                    _ => "非常高"
                };

                string powerTrend = "非常低";
                if (_history.TryGetValue(pid, out var h))
                {
                    h.RecentPowerLoads.Enqueue(powerScore);
                    if (h.RecentPowerLoads.Count > 4) h.RecentPowerLoads.Dequeue();

                    if (h.RecentPowerLoads.Count >= 2)
                    {
                        var loads = h.RecentPowerLoads.ToArray();
                        float delta = loads[^1] - loads[0];
                        if (delta > 3.0f) powerTrend = "上升中";
                        else if (delta < -3.0f) powerTrend = "下降中";
                        else powerTrend = powerUsage == "極低" ? "非常低" : powerUsage;
                    }
                }

                list.Add(new ProcessSnapshot
                {
                    Pid = pid,
                    Name = procName,
                    Title = winTitle,
                    IsResponding = isResponding,
                    CpuPercent = (float)Math.Round(cpuPercent, 1),
                    MemoryMb = Math.Round(memMb, 1),
                    DiskIoKbPerSec = Math.Round(diskIoKbPerSec, 1),
                    NetworkKbps = Math.Round(networkKbps, 1),
                    PowerUsage = powerUsage,
                    PowerTrend = powerTrend
                });
            }
            catch
            {
            }
            finally
            {
                proc.Dispose();
            }
        }

        // 清理已關閉行程歷史
        var stalePids = _history.Keys.Where(k => !activePids.Contains(k)).ToList();
        foreach (var deadPid in stalePids)
        {
            _history.TryRemove(deadPid, out _);
        }

        return list;
    }

    /// <summary>
    /// 在 UI 執行緒執行穩定的原地更新，絕不 Clear 整個集合，保障 DataGrid 排序功能正常！
    /// 同時支援尚未執行自訂目標的佔位切換與動態啟動自動納管。
    /// </summary>
    private void ApplySnapshotsToItems(List<ProcessSnapshot> snapshots)
    {
        HashSet<string> customSnapshot;
        HashSet<string> recordingSnapshot;
        lock (_watchLock)
        {
            customSnapshot = new HashSet<string>(_customWatchTargets, StringComparer.OrdinalIgnoreCase);
            recordingSnapshot = new HashSet<string>(_recordingWatchTargets, StringComparer.OrdinalIgnoreCase);
        }

        var snapshotMap = snapshots.ToDictionary(s => s.Pid);
        var runningNames = new HashSet<string>(snapshots.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

        // 紀錄由佔位列或剛結束程序所繼承的選取狀態
        var inheritedSelectionByName = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // 1. 移除已退出的真實行程，以及已開始執行或不再追蹤的待命佔位列 (PID == 0)
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            if (item.ProcessId == 0)
            {
                bool shouldExistAsPlaceholder =
                    !runningNames.Contains(item.ProcessName) &&
                    (customSnapshot.Contains(item.ProcessName) || recordingSnapshot.Contains(item.ProcessName));

                if (!shouldExistAsPlaceholder)
                {
                    if (item.IsSelected)
                    {
                        inheritedSelectionByName[item.ProcessName] = true;
                    }
                    Items.RemoveAt(i);
                }
            }
            else if (!snapshotMap.ContainsKey(item.ProcessId))
            {
                if (item.IsSelected)
                {
                    inheritedSelectionByName[item.ProcessName] = true;
                }
                Items.RemoveAt(i);
            }
        }

        // 2. 原地更新現存真實行程，或追加新進行程
        var currentPidMap = Items
            .Where(x => x.ProcessId > 0)
            .ToDictionary(x => x.ProcessId);

        foreach (var snap in snapshots)
        {
            bool isCustom = customSnapshot.Contains(snap.Name);

            if (currentPidMap.TryGetValue(snap.Pid, out var existingItem))
            {
                // 原地屬性更新 (觸發 INotifyPropertyChanged，不改變集合結構與焦點)
                existingItem.IsRunning = true;
                existingItem.IsCustomTarget = isCustom;
                existingItem.StatusText = snap.IsResponding ? "執行中" : "沒有回應";
                existingItem.CpuPercent = snap.CpuPercent;
                existingItem.MemoryMb = snap.MemoryMb;
                existingItem.DiskIoKbPerSec = snap.DiskIoKbPerSec;
                existingItem.NetworkKbps = snap.NetworkKbps;
                existingItem.PowerUsage = snap.PowerUsage;
                existingItem.PowerTrend = snap.PowerTrend;
            }
            else
            {
                // 新進行程：若正在紀錄該名稱、或自剛剛待命佔位列啟動轉入，則自動繼承勾選狀態
                bool shouldSelect =
                    recordingSnapshot.Contains(snap.Name) ||
                    (inheritedSelectionByName.TryGetValue(snap.Name, out bool inherited) && inherited);

                var newItem = new ProcessMonitorItem
                {
                    ProcessId = snap.Pid,
                    ProcessName = snap.Name,
                    MainWindowTitle = snap.Title,
                    IsCustomTarget = isCustom,
                    IsRunning = true,
                    StatusText = snap.IsResponding ? "執行中" : "沒有回應",
                    CpuPercent = snap.CpuPercent,
                    MemoryMb = snap.MemoryMb,
                    DiskIoKbPerSec = snap.DiskIoKbPerSec,
                    NetworkKbps = snap.NetworkKbps,
                    PowerUsage = snap.PowerUsage,
                    PowerTrend = snap.PowerTrend,
                    IsSelected = shouldSelect
                };
                Items.Add(newItem);
            }
        }

        // 3. 補齊尚未執行的自訂目標或紀錄追蹤目標之待命佔位列 (PID == 0)
        var existingPlaceholderNames = new HashSet<string>(
            Items.Where(x => x.ProcessId == 0).Select(x => x.ProcessName),
            StringComparer.OrdinalIgnoreCase);

        var requiredTargets = new HashSet<string>(customSnapshot, StringComparer.OrdinalIgnoreCase);
        foreach (string recName in recordingSnapshot)
        {
            requiredTargets.Add(recName);
        }

        foreach (string targetName in requiredTargets)
        {
            if (runningNames.Contains(targetName) || existingPlaceholderNames.Contains(targetName))
            {
                continue;
            }

            bool isCustom = customSnapshot.Contains(targetName);
            bool shouldSelect =
                recordingSnapshot.Contains(targetName) ||
                (inheritedSelectionByName.TryGetValue(targetName, out bool inherited) && inherited);

            Items.Insert(0, CreatePendingPlaceholderItem(targetName, isCustom, shouldSelect));
            existingPlaceholderNames.Add(targetName);
        }
    }

    public void Dispose()
    {
        _history.Clear();
        lock (_watchLock)
        {
            _customWatchTargets.Clear();
            _recordingWatchTargets.Clear();
        }
        Items.Clear();
    }
}
