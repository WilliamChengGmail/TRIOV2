namespace TRIOV2.DbIoSimulator.Core;

/// <summary>
/// 依據模擬情境協調啟動各資料表定期寫入與查詢之並行 Task 排程器
/// 製作者: Office of William
/// </summary>
public sealed class ScenarioRunner : IDisposable
{
    private readonly DbWriteEngine _engine;
    private readonly DataPayloadGenerator _generator;
    private readonly object _lifecycleLock = new();

    private CancellationTokenSource? _cts;
    private readonly List<Task> _workerTasks = new();
    private bool _isRunning;
    private int _stepCounter;
    private int _slotCounter;
    private string _currentCycleId = Guid.NewGuid().ToString("N");

    public event Action<string>? AutoStoppedByLimit;

    public bool IsRunning
    {
        get
        {
            lock (_lifecycleLock)
            {
                return _isRunning;
            }
        }
    }

    public ScenarioRunner(DbWriteEngine engine, DataPayloadGenerator generator)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    /// <summary>
    /// 啟動資料庫 I/O 模擬作業
    /// </summary>
    public async Task StartAsync(string dbPath, ScenarioConfig config)
    {
        await StopAsync().ConfigureAwait(false);
        await DbInitializer.InitializeAsync(dbPath).ConfigureAwait(false);

        lock (_lifecycleLock)
        {
            _cts = new CancellationTokenSource();
            _isRunning = true;
            _currentCycleId = Guid.NewGuid().ToString("N");
            _stepCounter = 0;
            _slotCounter = 0;

            CancellationToken ct = _cts.Token;
            ScenarioConfig activeConfig = config.Clone();

            // 1. 儀器狀態迴圈
            if (activeConfig.InstrumentStateIntervalMs > 0)
            {
                _workerTasks.Add(RunPeriodicLoopAsync(
                    activeConfig.InstrumentStateIntervalMs,
                    () => ExecuteInstrumentStepAsync(dbPath, activeConfig, ct),
                    ct));
            }

            // 2. 工業相機盤面偵測迴圈
            if (activeConfig.DeckDetectionIntervalMs > 0)
            {
                _workerTasks.Add(RunPeriodicLoopAsync(
                    activeConfig.DeckDetectionIntervalMs,
                    () => ExecuteDeckDetectionAsync(dbPath, activeConfig, ct),
                    ct));
            }

            // 3. Modbus 多執行緒高頻寫入迴圈
            if (activeConfig.ModbusIntervalMs > 0)
            {
                int threadCount = Math.Clamp(activeConfig.ConcurrentWriterThreads, 1, 8);
                for (int i = 0; i < threadCount; i++)
                {
                    // 錯開初始啟動時間，分散 I/O 尖峰
                    int staggerMs = i * Math.Max(2, activeConfig.ModbusIntervalMs / threadCount);
                    _workerTasks.Add(RunPeriodicLoopAsync(
                        activeConfig.ModbusIntervalMs,
                        () => ExecuteModbusBatchAsync(dbPath, activeConfig, ct),
                        ct,
                        initialDelayMs: staggerMs));
                }
            }

            // 4. 感測器數據迴圈
            if (activeConfig.SensorIntervalMs > 0 && activeConfig.SensorCount > 0)
            {
                _workerTasks.Add(RunPeriodicLoopAsync(
                    activeConfig.SensorIntervalMs,
                    () => ExecuteSensorBatchAsync(dbPath, activeConfig, ct),
                    ct));
            }

            // 5. 隨機警報迴圈 (每秒判斷一次機率)
            if (activeConfig.AlarmProbabilityPctPerSec > 0)
            {
                _workerTasks.Add(RunPeriodicLoopAsync(
                    1000,
                    () => ExecuteAlarmRollAsync(dbPath, activeConfig, ct),
                    ct));
            }

            // 6. 定期查詢讀取迴圈 (模擬上層 UI/API 讀取)
            if (activeConfig.ReadQueryIntervalMs > 0)
            {
                _workerTasks.Add(RunPeriodicLoopAsync(
                    activeConfig.ReadQueryIntervalMs,
                    () => _engine.ExecuteReadSampleAsync(dbPath, ct),
                    ct));
            }

            // 7. DB 檔案大小上限保護監視器 (每 2 秒檢查一次)
            if (activeConfig.MaxDbSizeMb > 0)
            {
                _workerTasks.Add(RunPeriodicLoopAsync(
                    2000,
                    () => CheckDbSizeLimitAsync(dbPath, activeConfig.MaxDbSizeMb, ct),
                    ct));
            }
        }
    }

    /// <summary>
    /// 安全停止所有模擬背景執行緒
    /// </summary>
    public async Task StopAsync()
    {
        CancellationTokenSource? ctsToCancel;
        Task[] tasksToWait;

        lock (_lifecycleLock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            ctsToCancel = _cts;
            _cts = null;
            tasksToWait = _workerTasks.ToArray();
            _workerTasks.Clear();
        }

        if (ctsToCancel != null)
        {
            await ctsToCancel.CancelAsync().ConfigureAwait(false);
            try
            {
                await Task.WhenAll(tasksToWait).ConfigureAwait(false);
            }
            catch
            {
                // 背景工作取消所引起的預期例外
            }
            ctsToCancel.Dispose();
        }
    }

    private static async Task RunPeriodicLoopAsync(
        int intervalMs,
        Func<Task> action,
        CancellationToken ct,
        int initialDelayMs = 0)
    {
        try
        {
            if (initialDelayMs > 0)
            {
                await Task.Delay(initialDelayMs, ct).ConfigureAwait(false);
            }

            // 立即執行第一次，讓使用者按下開始時馬上看到寫入變化
            await action().ConfigureAwait(false);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(5, intervalMs)));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await action().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
    }

    private async Task ExecuteInstrumentStepAsync(string dbPath, ScenarioConfig config, CancellationToken ct)
    {
        bool isIdle = config.Mode == SimulationMode.Idle;
        int step = Interlocked.Increment(ref _stepCounter) - 1;
        if (!isIdle && step % _generator.TotalSteps == 0 && step > 0)
        {
            // 完成一個萃取循環，切換新的 CycleId
            _currentCycleId = Guid.NewGuid().ToString("N");
        }

        var state = _generator.GenerateInstrumentState(_currentCycleId, step, isIdle);
        await _engine.WriteInstrumentStateAsync(dbPath, state, ct).ConfigureAwait(false);
    }

    private async Task ExecuteDeckDetectionAsync(string dbPath, ScenarioConfig config, CancellationToken ct)
    {
        int slot = Interlocked.Increment(ref _slotCounter) - 1;
        var log = _generator.GenerateDeckDetectionLog(_currentCycleId, slot, config.DeckPayloadSizeKb);
        await _engine.WriteDeckDetectionAsync(dbPath, log, ct).ConfigureAwait(false);
    }

    private async Task ExecuteModbusBatchAsync(string dbPath, ScenarioConfig config, CancellationToken ct)
    {
        int count = Math.Clamp(config.ModbusBatchSize, 1, 50);
        var batch = new List<Core.Schema.ModbusCommandLog>(count);
        for (int i = 0; i < count; i++)
        {
            batch.Add(_generator.GenerateModbusCommandLog());
        }
        await _engine.WriteModbusCommandsBatchAsync(dbPath, batch, ct).ConfigureAwait(false);
    }

    private async Task ExecuteSensorBatchAsync(string dbPath, ScenarioConfig config, CancellationToken ct)
    {
        var readings = _generator.GenerateSensorReadings(config.SensorCount);
        await _engine.WriteSensorReadingsBatchAsync(dbPath, readings, ct).ConfigureAwait(false);
    }

    private async Task ExecuteAlarmRollAsync(string dbPath, ScenarioConfig config, CancellationToken ct)
    {
        double roll = Random.Shared.NextDouble() * 100.0;
        if (roll < config.AlarmProbabilityPctPerSec)
        {
            var alarm = _generator.GenerateAlarmLog(_currentCycleId);
            await _engine.WriteAlarmLogAsync(dbPath, alarm, ct).ConfigureAwait(false);
        }
    }

    private async Task CheckDbSizeLimitAsync(string dbPath, int maxDbSizeMb, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || maxDbSizeMb <= 0) return;

        long currentBytes = DbWriteEngine.GetDatabaseTotalBytes(dbPath);
        long limitBytes = (long)maxDbSizeMb * 1024L * 1024L;
        if (currentBytes >= limitBytes)
        {
            string reason = $"資料庫已達保護上限 ({currentBytes / (1024.0 * 1024.0):F1} MB / {maxDbSizeMb} MB)，已自動停止寫入以保護磁碟空間。";
            _ = Task.Run(async () =>
            {
                await StopAsync().ConfigureAwait(false);
                AutoStoppedByLimit?.Invoke(reason);
            }, CancellationToken.None);
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

