using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TRIOV2.DbIoSimulator.Core;
using TRIOV2.DbIoSimulator.Core.Schema;

namespace TRIOV2.DbIoSimulator.ViewModels;

/// <summary>
/// 情境下拉選項包裝項目
/// 製作者: Office of William
/// </summary>
public sealed record ScenarioOption(SimulationMode Mode, string DisplayName, string Description);

/// <summary>
/// 操作日誌顯示包裝項目 (供 ListView / DataGrid 呈現配色與格式化字串)
/// 製作者: Office of William
/// </summary>
public sealed class OperationLogItemViewModel
{
    public string TimestampText { get; }
    public string SourceTag { get; }
    public string Action { get; }
    public string Detail { get; }
    public bool IsSuccess { get; }

    public OperationLogItemViewModel(OperationLogEntry entry)
    {
        TimestampText = entry.TimestampLocal.ToString("HH:mm:ss.fff");
        SourceTag = entry.SourceTag;
        Action = entry.Action;
        Detail = entry.Detail;
        IsSuccess = entry.IsSuccess;
    }
}

/// <summary>
/// DB I/O 模擬器主視窗 ViewModel
/// 製作者: Office of William
/// </summary>
public sealed class DbSimulatorViewModel : ViewModelBase, IDisposable
{
    private readonly WriteRateMonitor _monitor;
    private readonly DbWriteEngine _writeEngine;
    private readonly DataPayloadGenerator _payloadGenerator;
    private readonly ScenarioRunner _runner;
    private readonly DispatcherTimer _uiTimer;
    private readonly Stopwatch _runStopwatch = new();
    private readonly Dispatcher _dispatcher;

    private string _dbFilePath;
    private ScenarioOption _selectedScenario;
    private ScenarioConfig _activeConfig;
    private bool _isRunning;
    private bool _isBusy;
    private bool _isCustomMode;
    private bool _pauseLogScroll;
    private string _statusMessage = "就緒 — 請選擇模擬情境並點擊「開始模擬」";
    private string _elapsedText = "00:00:00";

    // 即時指標欄位
    private double _writeOpsPerSec;
    private double _readOpsPerSec;
    private double _kbPerSec;
    private long _totalWriteOps;
    private long _totalReadOps;
    private long _totalErrors;
    private double _errorRatePct;
    private long _dbSizeBytes;
    private long _diskFreeBytes;

    public event Action? MetricsRefreshed;

    public WriteRateMonitor Monitor => _monitor;

    public ObservableCollection<ScenarioOption> ScenarioOptions { get; } =
    [
        new ScenarioOption(SimulationMode.Idle, "待機模式 (Idle)", "僅儀器心跳與低頻感測器 (~5 ops/min)"),
        new ScenarioOption(SimulationMode.Normal, "正常運行 (Normal)", "模擬完整萃取流程與盤面偵測 (~15 op/s)"),
        new ScenarioOption(SimulationMode.Stress, "高負載壓力 (Stress)", "4 執行緒並行、10ms Modbus、2MB JSON (100+ op/s)"),
        new ScenarioOption(SimulationMode.Custom, "自訂模式 (Custom)", "自由微調各項讀寫間隔與負載參數")
    ];

    public ObservableCollection<TableStatusViewModel> TableStatuses { get; }
    public ObservableCollection<OperationLogItemViewModel> RecentLogs { get; } = new();

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ResetDbCommand { get; }
    public ICommand BrowseDbPathCommand { get; }
    public ICommand OpenDbFolderCommand { get; }
    public ICommand ClearLogViewCommand { get; }

    public DbSimulatorViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _monitor = new WriteRateMonitor();
        _writeEngine = new DbWriteEngine(_monitor);
        _payloadGenerator = new DataPayloadGenerator();
        _runner = new ScenarioRunner(_writeEngine, _payloadGenerator);
        _runner.AutoStoppedByLimit += OnRunnerAutoStopped;

        // 決定預設 DB 輸出路徑 (若執行檔同目錄已有測試資料庫則優先使用，其次 D:\trio_v2_sim，再其次本機 sim_data)
        string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        string? colocatedDb = !string.IsNullOrWhiteSpace(exeDir)
            ? Path.Combine(exeDir, "trio_v2_firmware.db")
            : null;

        if (!string.IsNullOrWhiteSpace(colocatedDb) && File.Exists(colocatedDb))
        {
            _dbFilePath = colocatedDb;
        }
        else
        {
            _dbFilePath = Directory.Exists(@"D:\")
                ? @"D:\trio_v2_sim\trio_v2_firmware.db"
                : Path.Combine(AppContext.BaseDirectory, "sim_data", "trio_v2_firmware.db");
        }

        // 初始化五張表監控狀態列
        TableStatuses =
        [
            new TableStatusViewModel(SimulatedTableType.InstrumentState, "萃取步驟與心跳狀態"),
            new TableStatusViewModel(SimulatedTableType.DeckDetectionLog, "工業相機盤面偵測與 JSON"),
            new TableStatusViewModel(SimulatedTableType.ModbusCommandLog, "Modbus 高頻硬體通訊指令"),
            new TableStatusViewModel(SimulatedTableType.AlarmLog, "設備異常與警示事件紀錄"),
            new TableStatusViewModel(SimulatedTableType.SensorReading, "溫濕度/壓力/流量感測器")
        ];

        // 預設選取「正常運行」模式
        _selectedScenario = ScenarioOptions[1];
        _activeConfig = ScenarioConfig.FromMode(SimulationMode.Normal);

        StartCommand = new RelayCommand(ExecuteStart, () => !IsRunning && !IsBusy);
        StopCommand = new RelayCommand(ExecuteStop, () => IsRunning && !IsBusy);
        ResetDbCommand = new RelayCommand(ExecuteResetDb, () => !IsRunning && !IsBusy);
        BrowseDbPathCommand = new RelayCommand(ExecuteBrowseDbPath, () => !IsRunning && !IsBusy);
        OpenDbFolderCommand = new RelayCommand(ExecuteOpenDbFolder);
        ClearLogViewCommand = new RelayCommand(() => RecentLogs.Clear());

        // 1 Hz UI 刷新定時器
        _uiTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();

        // 非同步載入既有 DB 資訊 (不阻塞視窗開啟)
        _ = LoadExistingDbSummaryAsync();
    }

    #region 綁定屬性

    public string DbFilePath
    {
        get => _dbFilePath;
        set
        {
            if (SetProperty(ref _dbFilePath, value))
            {
                RefreshStorageStats();
            }
        }
    }

    public ScenarioOption SelectedScenario
    {
        get => _selectedScenario;
        set
        {
            if (SetProperty(ref _selectedScenario, value) && value != null)
            {
                ApplyScenarioPreset(value.Mode);
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanEditSettings));
                OnPropertyChanged(nameof(RunStateText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanEditSettings));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CanEditSettings => !IsRunning && !IsBusy;

    public bool IsCustomMode
    {
        get => _isCustomMode;
        private set => SetProperty(ref _isCustomMode, value);
    }

    public bool PauseLogScroll
    {
        get => _pauseLogScroll;
        set => SetProperty(ref _pauseLogScroll, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string RunStateText => IsRunning ? "模擬運行中" : "已停止";

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    public string ScenarioSummaryText => _activeConfig.Description;

    // --- Custom 自訂參數雙向綁定屬性 ---
    public int ModbusIntervalMs
    {
        get => _activeConfig.ModbusIntervalMs;
        set
        {
            if (_activeConfig.ModbusIntervalMs != value)
            {
                _activeConfig.ModbusIntervalMs = value;
                OnPropertyChanged();
            }
        }
    }

    public int ModbusBatchSize
    {
        get => _activeConfig.ModbusBatchSize;
        set
        {
            if (_activeConfig.ModbusBatchSize != value)
            {
                _activeConfig.ModbusBatchSize = value;
                OnPropertyChanged();
            }
        }
    }

    public int DeckPayloadSizeKb
    {
        get => _activeConfig.DeckPayloadSizeKb;
        set
        {
            if (_activeConfig.DeckPayloadSizeKb != value)
            {
                _activeConfig.DeckPayloadSizeKb = value;
                OnPropertyChanged();
            }
        }
    }

    public int SensorCount
    {
        get => _activeConfig.SensorCount;
        set
        {
            if (_activeConfig.SensorCount != value)
            {
                _activeConfig.SensorCount = value;
                OnPropertyChanged();
            }
        }
    }

    public double AlarmProbabilityPctPerSec
    {
        get => _activeConfig.AlarmProbabilityPctPerSec;
        set
        {
            if (Math.Abs(_activeConfig.AlarmProbabilityPctPerSec - value) > 0.001)
            {
                _activeConfig.AlarmProbabilityPctPerSec = value;
                OnPropertyChanged();
            }
        }
    }

    public int ConcurrentWriterThreads
    {
        get => _activeConfig.ConcurrentWriterThreads;
        set
        {
            if (_activeConfig.ConcurrentWriterThreads != value)
            {
                _activeConfig.ConcurrentWriterThreads = value;
                OnPropertyChanged();
            }
        }
    }

    public int ReadQueryIntervalMs
    {
        get => _activeConfig.ReadQueryIntervalMs;
        set
        {
            if (_activeConfig.ReadQueryIntervalMs != value)
            {
                _activeConfig.ReadQueryIntervalMs = value;
                OnPropertyChanged();
            }
        }
    }

    public int MaxDbSizeMb
    {
        get => _activeConfig.MaxDbSizeMb;
        set
        {
            if (_activeConfig.MaxDbSizeMb != value)
            {
                _activeConfig.MaxDbSizeMb = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MaxDbSizeText));
            }
        }
    }

    public string MaxDbSizeText => _activeConfig.MaxDbSizeMb <= 0 ? "無限制" : $"{_activeConfig.MaxDbSizeMb} MB";

    // --- KPI 即時統計屬性 ---
    public double WriteOpsPerSec
    {
        get => _writeOpsPerSec;
        private set
        {
            if (SetProperty(ref _writeOpsPerSec, value))
            {
                OnPropertyChanged(nameof(WriteRateText));
            }
        }
    }

    public string WriteRateText => $"{_writeOpsPerSec:F1} op/s";

    public double ReadOpsPerSec
    {
        get => _readOpsPerSec;
        private set
        {
            if (SetProperty(ref _readOpsPerSec, value))
            {
                OnPropertyChanged(nameof(ReadRateText));
            }
        }
    }

    public string ReadRateText => $"{_readOpsPerSec:F1} op/s";

    public double KbPerSec
    {
        get => _kbPerSec;
        private set
        {
            if (SetProperty(ref _kbPerSec, value))
            {
                OnPropertyChanged(nameof(ThroughputText));
            }
        }
    }

    public string ThroughputText => _kbPerSec >= 1024.0
        ? $"{_kbPerSec / 1024.0:F2} MB/s"
        : $"{_kbPerSec:F1} KB/s";

    public string DbSizeText => _dbSizeBytes >= 1024L * 1024L * 1024L
        ? $"{_dbSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        : $"{_dbSizeBytes / (1024.0 * 1024.0):F2} MB";

    public string DiskFreeText => _diskFreeBytes >= 0
        ? $"可用空間 {_diskFreeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB"
        : "可用空間未知";

    public string TotalWriteOpsText => _totalWriteOps.ToString("N0");
    public string TotalReadSummaryText => $"讀取 {_totalReadOps:N0} 次 | 錯誤 {_totalErrors:N0}";

    public double ErrorRatePct
    {
        get => _errorRatePct;
        private set
        {
            if (SetProperty(ref _errorRatePct, value))
            {
                OnPropertyChanged(nameof(ErrorRateText));
            }
        }
    }

    public string ErrorRateText => $"{_errorRatePct:F2} %";

    #endregion

    private void ApplyScenarioPreset(SimulationMode mode)
    {
        // 從目前值保留使用者設定的上限大小
        int currentMaxMb = _activeConfig?.MaxDbSizeMb ?? 1024;
        _activeConfig = ScenarioConfig.FromMode(mode);
        _activeConfig.MaxDbSizeMb = currentMaxMb;

        IsCustomMode = mode == SimulationMode.Custom;
        NotifyAllConfigPropertiesChanged();
    }

    private void NotifyAllConfigPropertiesChanged()
    {
        OnPropertyChanged(nameof(ScenarioSummaryText));
        OnPropertyChanged(nameof(ModbusIntervalMs));
        OnPropertyChanged(nameof(ModbusBatchSize));
        OnPropertyChanged(nameof(DeckPayloadSizeKb));
        OnPropertyChanged(nameof(SensorCount));
        OnPropertyChanged(nameof(AlarmProbabilityPctPerSec));
        OnPropertyChanged(nameof(ConcurrentWriterThreads));
        OnPropertyChanged(nameof(ReadQueryIntervalMs));
        OnPropertyChanged(nameof(MaxDbSizeMb));
        OnPropertyChanged(nameof(MaxDbSizeText));
    }

    private async void ExecuteStart()
    {
        if (IsRunning || IsBusy) return;

        IsBusy = true;
        StatusMessage = $"正在初始化資料庫 ({DbFilePath})...";

        try
        {
            await DbInitializer.InitializeAsync(DbFilePath);
            var existingCounts = await DbInitializer.GetExistingRowCountsAsync(DbFilePath);
            _monitor.ResetCounters(existingCounts);

            await _runner.StartAsync(DbFilePath, _activeConfig);

            _runStopwatch.Restart();
            IsRunning = true;
            StatusMessage = $"正在執行 [{_selectedScenario.DisplayName}] — 目標: {DbFilePath}";
        }
        catch (Exception ex)
        {
            IsRunning = false;
            StatusMessage = $"啟動失敗: {ex.Message}";
            MessageBox.Show($"無法啟動 SQLite 模擬器：\n\n{ex.Message}", "啟動錯誤",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteStop()
    {
        if (!IsRunning || IsBusy) return;

        IsBusy = true;
        StatusMessage = "正在安全停止所有背景讀寫工作...";

        try
        {
            await _runner.StopAsync();
            _runStopwatch.Stop();
            IsRunning = false;
            StatusMessage = $"模擬已停止 — 累計寫入 {_totalWriteOps:N0} 筆，DB 大小 {DbSizeText}";
            RefreshAllSnapshots();
        }
        catch (Exception ex)
        {
            StatusMessage = $"停止時發生異常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExecuteResetDb()
    {
        if (IsRunning || IsBusy) return;

        var confirm = MessageBox.Show(
            $"確定要清除並重建以下資料庫檔案嗎？\n\n{DbFilePath}\n\n此操作將刪除所有模擬歷史資料 (.db / -wal / -shm)。",
            "確認清除資料庫",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusMessage = "正在清除並重建 SQLite 資料庫...";

        try
        {
            await DbInitializer.ResetDatabaseAsync(DbFilePath);
            _monitor.ResetCounters();
            _runStopwatch.Reset();
            ElapsedText = "00:00:00";
            RecentLogs.Clear();
            RefreshAllSnapshots();
            StatusMessage = $"資料庫已清除並重建完成 ({DbFilePath})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"清除資料庫失敗: {ex.Message}";
            MessageBox.Show($"清除資料庫時發生錯誤：\n\n{ex.Message}", "清除失敗",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ExecuteBrowseDbPath()
    {
        if (IsRunning) return;

        var dialog = new SaveFileDialog
        {
            Title = "選擇或建立模擬 SQLite 資料庫檔案",
            Filter = "SQLite 資料庫 (*.db)|*.db|所有檔案 (*.*)|*.*",
            DefaultExt = ".db",
            FileName = Path.GetFileName(DbFilePath),
            InitialDirectory = Path.GetDirectoryName(DbFilePath) ?? AppContext.BaseDirectory,
            OverwritePrompt = false
        };

        if (dialog.ShowDialog() == true)
        {
            DbFilePath = dialog.FileName;
            StatusMessage = $"已更新資料庫路徑: {DbFilePath}";
            _ = LoadExistingDbSummaryAsync();
        }
    }

    private void ExecuteOpenDbFolder()
    {
        try
        {
            string? dir = Path.GetDirectoryName(DbFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"無法開啟資料夾: {ex.Message}";
        }
    }

    private async Task LoadExistingDbSummaryAsync()
    {
        try
        {
            if (File.Exists(DbFilePath))
            {
                var counts = await DbInitializer.GetExistingRowCountsAsync(DbFilePath);
                _monitor.SeedExistingRowCounts(counts);
            }
            _dispatcher.Invoke(RefreshAllSnapshots);
        }
        catch
        {
            // 若檔案尚未建立或被佔用則忽略初期讀取
        }
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        if (IsRunning)
        {
            ElapsedText = _runStopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        }
        RefreshAllSnapshots();
    }

    private void RefreshAllSnapshots()
    {
        RateMonitorSnapshot snap = _monitor.CaptureSampleAndAdvance();

        WriteOpsPerSec = snap.WriteOpsPerSec;
        ReadOpsPerSec = snap.ReadOpsPerSec;
        KbPerSec = snap.KbPerSec;
        ErrorRatePct = snap.ErrorRatePct;

        if (_totalWriteOps != snap.TotalWriteOps)
        {
            _totalWriteOps = snap.TotalWriteOps;
            OnPropertyChanged(nameof(TotalWriteOpsText));
        }

        if (_totalReadOps != snap.TotalReadOps || _totalErrors != snap.TotalErrors)
        {
            _totalReadOps = snap.TotalReadOps;
            _totalErrors = snap.TotalErrors;
            OnPropertyChanged(nameof(TotalReadSummaryText));
        }

        foreach (var tableVm in TableStatuses)
        {
            if (snap.Tables.TryGetValue(tableVm.TableType, out var tableSnap))
            {
                tableVm.UpdateFromSnapshot(tableSnap, IsRunning);
            }
        }

        if (!PauseLogScroll && IsRunning)
        {
            UpdateRecentLogItems(snap.RecentLogs);
        }

        RefreshStorageStats();
        MetricsRefreshed?.Invoke();
    }

    private void UpdateRecentLogItems(IReadOnlyList<OperationLogEntry> entries)
    {
        RecentLogs.Clear();
        foreach (var entry in entries)
        {
            RecentLogs.Add(new OperationLogItemViewModel(entry));
        }
    }

    private void RefreshStorageStats()
    {
        long dbBytes = DbWriteEngine.GetDatabaseTotalBytes(DbFilePath);
        if (_dbSizeBytes != dbBytes)
        {
            _dbSizeBytes = dbBytes;
            OnPropertyChanged(nameof(DbSizeText));
        }

        long freeBytes = DbWriteEngine.GetAvailableDiskFreeBytes(DbFilePath);
        if (_diskFreeBytes != freeBytes)
        {
            _diskFreeBytes = freeBytes;
            OnPropertyChanged(nameof(DiskFreeText));
        }
    }

    private void OnRunnerAutoStopped(string reason)
    {
        _dispatcher.InvokeAsync(() =>
        {
            _runStopwatch.Stop();
            IsRunning = false;
            StatusMessage = reason;
            RefreshAllSnapshots();
            MessageBox.Show(reason, "已達 DB 容量保護上限", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    public void Dispose()
    {
        _uiTimer.Stop();
        _uiTimer.Tick -= OnUiTimerTick;
        _runner.AutoStoppedByLimit -= OnRunnerAutoStopped;
        _runner.Dispose();
    }
}

