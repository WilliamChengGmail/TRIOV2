using System.Collections.Concurrent;
using TRIOV2.DbIoSimulator.Core.Schema;

namespace TRIOV2.DbIoSimulator.Core;

/// <summary>
/// 操作日誌單筆項目 (顯示於 UI 最近 100 筆預覽區)
/// 製作者: Office of William
/// </summary>
public sealed record OperationLogEntry(
    DateTime TimestampLocal,
    string SourceTag,
    string Action,
    string Detail,
    bool IsSuccess);

/// <summary>
/// 單一資料表的即時指標快照
/// 製作者: Office of William
/// </summary>
public sealed record TableMetricsSnapshot(
    SimulatedTableType Table,
    long TotalRows,
    DateTime? LastWriteTimeLocal,
    double WriteOpsPerSec,
    bool LastWriteSuccess);

/// <summary>
/// 1 秒採樣區間的整合效能快照
/// 製作者: Office of William
/// </summary>
public sealed record RateMonitorSnapshot(
    double WriteOpsPerSec,
    double ReadOpsPerSec,
    double KbPerSec,
    long TotalWriteOps,
    long TotalReadOps,
    long TotalErrors,
    double ErrorRatePct,
    IReadOnlyDictionary<SimulatedTableType, TableMetricsSnapshot> Tables,
    IReadOnlyList<OperationLogEntry> RecentLogs);

/// <summary>
/// 高併發執行緒安全之 SQLite I/O 速率與日誌監控器
/// 製作者: Office of William
/// </summary>
public sealed class WriteRateMonitor
{
    private const int MaxLogEntries = 100;

    private long _totalWriteOps;
    private long _totalReadOps;
    private long _totalErrors;

    private long _windowWriteOps;
    private long _windowReadOps;
    private long _windowBytes;

    private readonly Dictionary<SimulatedTableType, TableCounter> _tableCounters = new();
    private readonly ConcurrentQueue<OperationLogEntry> _logQueue = new();
    private readonly object _logTrimLock = new();
    private DateTime _lastWindowSampleUtc = DateTime.UtcNow;

    public PerformanceRingBuffer WriteOpsHistory { get; } = new(60);
    public PerformanceRingBuffer ReadOpsHistory { get; } = new(60);

    public WriteRateMonitor()
    {
        foreach (SimulatedTableType table in Enum.GetValues<SimulatedTableType>())
        {
            _tableCounters[table] = new TableCounter();
        }
    }

    /// <summary>
    /// 從現有資料庫初始化各資料表既有筆數
    /// </summary>
    public void SeedExistingRowCounts(IReadOnlyDictionary<SimulatedTableType, long> counts)
    {
        foreach (var kv in counts)
        {
            if (_tableCounters.TryGetValue(kv.Key, out var counter))
            {
                Interlocked.Exchange(ref counter.TotalRows, kv.Value);
            }
        }
    }

    /// <summary>
    /// 重置所有即時速率、錯誤計數、波形圖與日誌
    /// </summary>
    public void ResetCounters(IReadOnlyDictionary<SimulatedTableType, long>? initialCounts = null)
    {
        Interlocked.Exchange(ref _totalWriteOps, 0);
        Interlocked.Exchange(ref _totalReadOps, 0);
        Interlocked.Exchange(ref _totalErrors, 0);
        Interlocked.Exchange(ref _windowWriteOps, 0);
        Interlocked.Exchange(ref _windowReadOps, 0);
        Interlocked.Exchange(ref _windowBytes, 0);

        foreach (var kv in _tableCounters)
        {
            long seed = initialCounts != null && initialCounts.TryGetValue(kv.Key, out long val) ? val : 0;
            Interlocked.Exchange(ref kv.Value.TotalRows, seed);
            Interlocked.Exchange(ref kv.Value.WindowWriteOps, 0);
            kv.Value.ResetTimestamp();
        }

        while (_logQueue.TryDequeue(out _)) { }
        WriteOpsHistory.Clear();
        ReadOpsHistory.Clear();
        _lastWindowSampleUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// 記錄一次成功寫入 (可指定寫入筆數，例如交易批次寫入)
    /// </summary>
    public void RecordWrite(SimulatedTableType table, int rowCount, long approxBytes, string detail)
    {
        Interlocked.Add(ref _totalWriteOps, rowCount);
        Interlocked.Add(ref _windowWriteOps, rowCount);
        Interlocked.Add(ref _windowBytes, approxBytes);

        if (_tableCounters.TryGetValue(table, out var counter))
        {
            Interlocked.Add(ref counter.TotalRows, rowCount);
            Interlocked.Add(ref counter.WindowWriteOps, rowCount);
            counter.MarkWriteResult(DateTime.Now, isSuccess: true);
        }

        string action = rowCount > 1 ? $"INSERT x{rowCount}" : "INSERT";
        EnqueueLog(table.ToString(), action, detail, isSuccess: true);
    }

    /// <summary>
    /// 記錄一次資料庫查詢讀取
    /// </summary>
    public void RecordRead(string sourceTag, string detail)
    {
        Interlocked.Increment(ref _totalReadOps);
        Interlocked.Increment(ref _windowReadOps);
        EnqueueLog(sourceTag, "SELECT", detail, isSuccess: true);
    }

    /// <summary>
    /// 記錄一次資料庫操作錯誤
    /// </summary>
    public void RecordError(SimulatedTableType? table, string action, string errorMessage)
    {
        Interlocked.Increment(ref _totalErrors);
        if (table.HasValue && _tableCounters.TryGetValue(table.Value, out var counter))
        {
            counter.MarkWriteResult(DateTime.Now, isSuccess: false);
        }

        string tag = table?.ToString() ?? "Query";
        EnqueueLog(tag, $"{action} ERR", errorMessage, isSuccess: false);
    }

    /// <summary>
    /// 取樣最近一段視窗 (約 1 秒) 的統計數據並推進歷史環形緩衝區
    /// </summary>
    public RateMonitorSnapshot CaptureSampleAndAdvance()
    {
        DateTime nowUtc = DateTime.UtcNow;
        double elapsedSec = Math.Max(0.25, (nowUtc - _lastWindowSampleUtc).TotalSeconds);
        _lastWindowSampleUtc = nowUtc;

        long wOps = Interlocked.Exchange(ref _windowWriteOps, 0);
        long rOps = Interlocked.Exchange(ref _windowReadOps, 0);
        long bytes = Interlocked.Exchange(ref _windowBytes, 0);

        double writeOpsPerSec = Math.Round(wOps / elapsedSec, 1);
        double readOpsPerSec = Math.Round(rOps / elapsedSec, 1);
        double kbPerSec = Math.Round((bytes / 1024.0) / elapsedSec, 1);

        WriteOpsHistory.Enqueue((float)writeOpsPerSec);
        ReadOpsHistory.Enqueue((float)readOpsPerSec);

        long totalW = Interlocked.Read(ref _totalWriteOps);
        long totalR = Interlocked.Read(ref _totalReadOps);
        long totalErr = Interlocked.Read(ref _totalErrors);
        long totalOps = totalW + totalR + totalErr;
        double errorRate = totalOps > 0 ? Math.Round(totalErr * 100.0 / totalOps, 2) : 0.0;

        var tableSnapshots = new Dictionary<SimulatedTableType, TableMetricsSnapshot>(_tableCounters.Count);
        foreach (var kv in _tableCounters)
        {
            long rows = Interlocked.Read(ref kv.Value.TotalRows);
            long tOps = Interlocked.Exchange(ref kv.Value.WindowWriteOps, 0);
            var (lastLocal, lastOk) = kv.Value.GetTimestampSnapshot();

            tableSnapshots[kv.Key] = new TableMetricsSnapshot(
                kv.Key,
                rows,
                lastLocal,
                Math.Round(tOps / elapsedSec, 2),
                lastOk);
        }

        // 由新到舊排序最近日誌
        OperationLogEntry[] logs = _logQueue.ToArray();
        Array.Reverse(logs);

        return new RateMonitorSnapshot(
            writeOpsPerSec,
            readOpsPerSec,
            kbPerSec,
            totalW,
            totalR,
            totalErr,
            errorRate,
            tableSnapshots,
            logs);
    }

    private void EnqueueLog(string sourceTag, string action, string detail, bool isSuccess)
    {
        _logQueue.Enqueue(new OperationLogEntry(DateTime.Now, sourceTag, action, detail, isSuccess));

        if (_logQueue.Count > MaxLogEntries)
        {
            lock (_logTrimLock)
            {
                while (_logQueue.Count > MaxLogEntries)
                {
                    _logQueue.TryDequeue(out _);
                }
            }
        }
    }

    private sealed class TableCounter
    {
        public long TotalRows;
        public long WindowWriteOps;
        private readonly object _tsLock = new();
        private DateTime? _lastWriteLocal;
        private bool _lastWriteSuccess = true;

        public void MarkWriteResult(DateTime localTime, bool isSuccess)
        {
            lock (_tsLock)
            {
                _lastWriteLocal = localTime;
                _lastWriteSuccess = isSuccess;
            }
        }

        public void ResetTimestamp()
        {
            lock (_tsLock)
            {
                _lastWriteLocal = null;
                _lastWriteSuccess = true;
            }
        }

        public (DateTime? LastLocal, bool IsSuccess) GetTimestampSnapshot()
        {
            lock (_tsLock)
            {
                return (_lastWriteLocal, _lastWriteSuccess);
            }
        }
    }
}

