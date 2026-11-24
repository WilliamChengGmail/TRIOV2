using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using TRIOV2.ResourceSimulator.Core;

namespace TRIOV2.ResourceSimulator.ViewModels;

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
/// 效能資源監控紀錄器視圖模型 (MVVM 狀態中心 - 具備即時雙向更新、工作管理員行程監視與多任務獨立採樣)
/// 製作者: Office of William
/// </summary>
public sealed class SimulatorViewModel : ViewModelBase, IDisposable
{
    private readonly SystemPerformanceSampler _sampler;
    private readonly PerformanceSessionRecorder _recorder = new();

    // 行程監視與多任務記錄器
    private readonly ProcessWatcherEngine _procWatcher = new();
    private readonly MultiProcessSessionRecorder _multiRecorder = new();
    private readonly SystemHardwareInspector _hardwareInspector = new();
    private readonly ICollectionView _processView;

    // 視窗設定
    private bool _isAlwaysOnTop = true;

    // 即時效能讀數 (左側)
    private string _totalCpuText = "0.0 %";
    private string _remainingCpuText = "100.0 %";
    private string _totalMemoryText = "16.0 GB";
    private string _usedMemoryText = "0.0 GB";
    private string _availableMemoryText = "0.0 GB";
    private string _memoryUsagePercentText = "0 %";
    private string _statusText = "🟢 即時監控中 (每秒持續取樣)";

    // 區間統計量測狀態與讀數
    private bool _isRecording;
    private string _recordingDurationText = "⏱ 00:00";
    private string _recordingStatusBadge = "⚪ 待命 (點擊開始紀錄)";
    private string _recordedCpuMaxText = "--";
    private string _recordedCpuMinText = "--";
    private string _recordedCpuAvgText = "--";
    private string _recordedMemMaxText = "--";
    private string _recordedMemMinText = "--";
    private string _recordedMemAvgText = "--";
    private bool _hasRecordedStats;
    private string _lastLogFilePath = string.Empty;
    private string _lastLogFileName = string.Empty;

    // 行程過濾搜尋與額外自訂監控輸入
    private string _processSearchText = string.Empty;
    private string _selectedProcessCountText = "已勾選追蹤：0 個任務";
    private string _manualTaskNameInput = string.Empty;
    private string _customTargetSummaryText = string.Empty;

    public SystemPerformanceSampler Sampler => _sampler;
    public PerformanceSessionRecorder Recorder => _recorder;
    public MultiProcessSessionRecorder MultiRecorder => _multiRecorder;

    public ICollectionView ProcessView => _processView;
    public ObservableCollection<ProcessMonitorItem> ProcessItems => _procWatcher.Items;

    #region Properties

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set => SetProperty(ref _isAlwaysOnTop, value);
    }

    public string TotalCpuText
    {
        get => _totalCpuText;
        private set => SetProperty(ref _totalCpuText, value);
    }

    public string RemainingCpuText
    {
        get => _remainingCpuText;
        private set => SetProperty(ref _remainingCpuText, value);
    }

    public string TotalMemoryText
    {
        get => _totalMemoryText;
        private set => SetProperty(ref _totalMemoryText, value);
    }

    public string UsedMemoryText
    {
        get => _usedMemoryText;
        private set => SetProperty(ref _usedMemoryText, value);
    }

    public string AvailableMemoryText
    {
        get => _availableMemoryText;
        private set => SetProperty(ref _availableMemoryText, value);
    }

    public string MemoryUsagePercentText
    {
        get => _memoryUsagePercentText;
        private set => SetProperty(ref _memoryUsagePercentText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                OnPropertyChanged(nameof(CanStartRecording));
                OnPropertyChanged(nameof(CanStopRecording));
                OnPropertyChanged(nameof(CanChangeProcessSelection));
            }
        }
    }

    public bool CanStartRecording => !IsRecording;
    public bool CanStopRecording => IsRecording;

    /// <summary>
    /// 是否允許變更勾選項目 (開始紀錄後鎖定 Disable，停止後恢復 Enable)
    /// </summary>
    public bool CanChangeProcessSelection => !IsRecording;

    public string RecordingDurationText
    {
        get => _recordingDurationText;
        private set => SetProperty(ref _recordingDurationText, value);
    }

    public string RecordingStatusBadge
    {
        get => _recordingStatusBadge;
        private set => SetProperty(ref _recordingStatusBadge, value);
    }

    public string RecordedCpuMaxText
    {
        get => _recordedCpuMaxText;
        private set => SetProperty(ref _recordedCpuMaxText, value);
    }

    public string RecordedCpuMinText
    {
        get => _recordedCpuMinText;
        private set => SetProperty(ref _recordedCpuMinText, value);
    }

    public string RecordedCpuAvgText
    {
        get => _recordedCpuAvgText;
        private set => SetProperty(ref _recordedCpuAvgText, value);
    }

    public string RecordedMemMaxText
    {
        get => _recordedMemMaxText;
        private set => SetProperty(ref _recordedMemMaxText, value);
    }

    public string RecordedMemMinText
    {
        get => _recordedMemMinText;
        private set => SetProperty(ref _recordedMemMinText, value);
    }

    public string RecordedMemAvgText
    {
        get => _recordedMemAvgText;
        private set => SetProperty(ref _recordedMemAvgText, value);
    }

    public bool HasRecordedStats
    {
        get => _hasRecordedStats;
        private set => SetProperty(ref _hasRecordedStats, value);
    }

    public string LastLogFilePath
    {
        get => _lastLogFilePath;
        private set => SetProperty(ref _lastLogFilePath, value);
    }

    public string LastLogFileName
    {
        get => _lastLogFileName;
        private set => SetProperty(ref _lastLogFileName, value);
    }

    public bool HasLogFile => !string.IsNullOrEmpty(_lastLogFilePath);

    public string ProcessSearchText
    {
        get => _processSearchText;
        set
        {
            if (SetProperty(ref _processSearchText, value))
            {
                _processView.Refresh();
            }
        }
    }

    public string SelectedProcessCountText
    {
        get => _selectedProcessCountText;
        private set => SetProperty(ref _selectedProcessCountText, value);
    }

    public string ManualTaskNameInput
    {
        get => _manualTaskNameInput;
        set
        {
            if (SetProperty(ref _manualTaskNameInput, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string CustomTargetSummaryText
    {
        get => _customTargetSummaryText;
        private set => SetProperty(ref _customTargetSummaryText, value);
    }

    public bool HasCustomTargets => _procWatcher.CustomTargetCount > 0;

    #endregion

    #region Commands

    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand CopyRecordSummaryCommand { get; }
    public ICommand OpenLogFileCommand { get; }
    public ICommand OpenLogFolderCommand { get; }

    public ICommand SelectAllProcessesCommand { get; }
    public ICommand DeselectAllProcessesCommand { get; }
    public ICommand RefreshProcessesCommand { get; }

    public ICommand AddManualTaskCommand { get; }
    public ICommand ImportTaskListCommand { get; }
    public ICommand DownloadSampleTasksCommand { get; }
    public ICommand ClearCustomTasksCommand { get; }
    public ICommand RemoveCustomTaskCommand { get; }

    #endregion

    public event Action? SnapshotRefreshed;

    public SimulatorViewModel()
    {
        _sampler = new SystemPerformanceSampler();
        _hardwareInspector.WarmupAsync();

        // 初始化行程過濾視圖
        _processView = CollectionViewSource.GetDefaultView(_procWatcher.Items);
        _processView.Filter = FilterProcessItem;

        // 監聽項目 PropertyChanged 以即時更新已勾選數量
        _procWatcher.Items.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ProcessMonitorItem item in e.NewItems)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (ProcessMonitorItem item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }
            UpdateSelectedCount();
        };

        // 1Hz 核心採樣滴答回呼
        _sampler.SnapshotUpdated += snapshot =>
        {
            try
            {
                // 1. 全系統效能樣本寫入 (基準監控模式)
                _recorder.AddSample(
                    totalCpu: snapshot.TotalCpuPercent,
                    simulatorCpu: snapshot.SimulatorCpuPercent,
                    remainingCpu: snapshot.RemainingCpuPercent,
                    usedMemGb: snapshot.UsedMemoryGb,
                    totalMemGb: snapshot.TotalMemoryGb,
                    availableMemGb: snapshot.AvailableMemoryGb,
                    memPercent: snapshot.MemoryUsagePercent,
                    simulatorMemMb: snapshot.SimulatorMemoryMb,
                    isInferenceActive: false,
                    inferenceDurationMs: 0,
                    operatingMode: "效能監控"
                );

                // 2. 觸發行程監視器背景計算並派送 UI 原地更新 (每秒)
                _procWatcher.Refresh();

                // 3. 左側即時讀數與波形圖刷新，並於同一 UI 執行緒安全讀取最新已勾選行程寫入多任務記錄器
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    if (_multiRecorder.IsRecording)
                    {
                        var trackedItems = _procWatcher.Items.Where(x => x.IsSelected).ToList();
                        _multiRecorder.AddSample(trackedItems);
                    }

                    TotalCpuText = $"{snapshot.TotalCpuPercent:0.0} %";
                    RemainingCpuText = $"{snapshot.RemainingCpuPercent:0.0} %";
                    TotalMemoryText = $"{snapshot.TotalMemoryGb:0.0} GB";
                    UsedMemoryText = $"{snapshot.UsedMemoryGb:0.0} GB";
                    AvailableMemoryText = $"{snapshot.AvailableMemoryGb:0.0} GB";
                    MemoryUsagePercentText = $"{snapshot.MemoryUsagePercent:0} %";

                    if (_recorder.IsRecording)
                    {
                        UpdateRecordingStatsUI();
                    }

                    SnapshotRefreshed?.Invoke();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimulatorViewModel] Snapshot update error: {ex}");
            }
        };

        // 區間紀錄與 Log 命令
        StartRecordingCommand = new RelayCommand(ExecuteStartRecording, () => !IsRecording);
        StopRecordingCommand = new RelayCommand(ExecuteStopRecording, () => IsRecording);
        CopyRecordSummaryCommand = new RelayCommand(ExecuteCopySummary, () => HasRecordedStats);
        OpenLogFileCommand = new RelayCommand(ExecuteOpenLogFile, () => HasLogFile);
        OpenLogFolderCommand = new RelayCommand(ExecuteOpenLogFolder, () => HasLogFile);

        // 行程管理員控制命令 (紀錄中禁止變更選取與匯入設定)
        SelectAllProcessesCommand = new RelayCommand(() => SetAllSelection(true), () => CanChangeProcessSelection);
        DeselectAllProcessesCommand = new RelayCommand(() => SetAllSelection(false), () => CanChangeProcessSelection);
        RefreshProcessesCommand = new RelayCommand(() => _procWatcher.Refresh());

        AddManualTaskCommand = new RelayCommand(ExecuteAddManualTask, () => CanChangeProcessSelection && !string.IsNullOrWhiteSpace(ManualTaskNameInput));
        ImportTaskListCommand = new RelayCommand(ExecuteImportTaskList, () => CanChangeProcessSelection);
        DownloadSampleTasksCommand = new RelayCommand(ExecuteDownloadSampleTasks);
        ClearCustomTasksCommand = new RelayCommand(ExecuteClearCustomTasks, () => CanChangeProcessSelection && HasCustomTargets);
        RemoveCustomTaskCommand = new RelayCommand(ExecuteRemoveCustomTask, _ => CanChangeProcessSelection);

        // 初次加載行程
        _procWatcher.Refresh();
        UpdateSelectedCount();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessMonitorItem.IsSelected))
        {
            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount()
    {
        int count = _procWatcher.Items.Count(x => x.IsSelected);
        int customCount = _procWatcher.CustomTargetCount;
        SelectedProcessCountText = customCount > 0
            ? $"已勾選追蹤：{count} 個項目 (含自訂 {customCount} 個任務)"
            : $"已勾選追蹤：{count} 個任務";
        CustomTargetSummaryText = customCount > 0 ? $"自訂額外目標：{customCount} 個" : string.Empty;
        OnPropertyChanged(nameof(HasCustomTargets));
        CommandManager.InvalidateRequerySuggested();
    }

    private void ExecuteAddManualTask()
    {
        if (!CanChangeProcessSelection) return;

        var parsedNames = TaskListImporter.ParseManualInput(ManualTaskNameInput);
        if (parsedNames.Count == 0)
        {
            MessageBox.Show("請輸入有效的 Task Name（例如 TRIOV2.App，多筆可使用逗號 ',' 分隔）。", "輸入提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _procWatcher.AddCustomTargets(parsedNames);
        ManualTaskNameInput = string.Empty;
        UpdateSelectedCount();
    }

    private void ExecuteImportTaskList()
    {
        if (!CanChangeProcessSelection) return;

        var dialog = new OpenFileDialog
        {
            Title = "選擇要匯入的額外監控任務清單 (CSV / TXT)",
            Filter = "任務清單檔案 (*.csv;*.txt)|*.csv;*.txt|CSV 檔案 (*.csv)|*.csv|純文字檔案 (*.txt)|*.txt|所有檔案 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            TaskImportResult result = TaskListImporter.ParseFromFile(dialog.FileName);
            if (result.TaskNames.Count == 0)
            {
                MessageBox.Show(
                    "匯入檔案中未找到有效的 Task Name。\n\n請點擊「📄 範例」下載標準格式範本後再填入任務名稱。",
                    "無有效任務名稱",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int added = _procWatcher.AddCustomTargets(result.TaskNames);
            UpdateSelectedCount();

            int notRunningCount = _procWatcher.Items.Count(x => x.IsCustomTarget && !x.IsRunning);
            string previewList = string.Join("、", result.TaskNames.Take(10));
            if (result.TaskNames.Count > 10)
            {
                previewList += $" 等共 {result.TaskNames.Count} 項";
            }

            MessageBox.Show(
                $"成功匯入 {added} 個額外監控任務：\n{previewList}\n\n" +
                $"• 目前尚未執行（待命監視中）：{notRunningCount} 個\n" +
                $"• 已啟動「▶ 開始紀錄」時將同步納管追蹤，待目標啟動後立即收集效能數據。",
                "匯入成功",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"讀取匯入清單失敗：\n{ex.Message}", "匯入錯誤",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteDownloadSampleTasks()
    {
        var dialog = new SaveFileDialog
        {
            Title = "儲存額外監控任務匯入範例檔",
            Filter = "CSV 任務清單 (*.csv)|*.csv|純文字清單 (*.txt)|*.txt",
            DefaultExt = ".csv",
            FileName = TaskListImporter.DefaultSampleFileName,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            TaskListImporter.SaveSampleFile(dialog.FileName);
            var openChoice = MessageBox.Show(
                $"範例檔案已成功下載至：\n{dialog.FileName}\n\n" +
                "【填入格式說明】\n" +
                "• 每行填寫一個處理程序名稱 (如 TRIOV2.App 或 notepad.exe)\n" +
                "• 支援填入 1 到多個任務名稱；'#' 開頭為註解\n\n" +
                "是否立即開啟範例檔案進行編輯？",
                "範例下載完成",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (openChoice == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存範例檔案失敗：\n{ex.Message}", "儲存錯誤",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteClearCustomTasks()
    {
        if (!CanChangeProcessSelection) return;
        _procWatcher.ClearCustomTargets();
        UpdateSelectedCount();
    }

    private void ExecuteRemoveCustomTask(object? parameter)
    {
        if (!CanChangeProcessSelection || parameter is not string taskName) return;
        _procWatcher.RemoveCustomTarget(taskName);
        UpdateSelectedCount();
    }

    private void SetAllSelection(bool select)
    {
        if (!CanChangeProcessSelection) return;

        foreach (var item in _procWatcher.Items)
        {
            if (string.IsNullOrWhiteSpace(_processSearchText) || FilterProcessItem(item))
            {
                item.IsSelected = select;
            }
        }
        UpdateSelectedCount();
    }

    private bool FilterProcessItem(object obj)
    {
        if (obj is not ProcessMonitorItem item) return false;
        if (string.IsNullOrWhiteSpace(_processSearchText)) return true;

        string search = _processSearchText.Trim();
        return item.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               item.MainWindowTitle.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               item.ProcessIdText.Contains(search) ||
               item.StatusText.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void ExecuteStartRecording()
    {
        var selectedTasks = _procWatcher.Items.Where(x => x.IsSelected).ToList();

        // 鎖定紀錄期間要持續追蹤的所有目標名稱 (包含尚未啟動之額外匯入/手動指定 Task Name)
        _procWatcher.SetRecordingWatchTargets(selectedTasks.Select(x => x.ProcessName));

        // 1. 啟動多任務記錄器：以 yyyyMMdd_HHmmss 建立主資料夾，並立即根據每個勾選的 task name 建立各自子資料夾與初始 CSV
        string sessionDir = _multiRecorder.Start(selectedTasks);

        // 2. 啟動全系統記錄器，直接串流至 sessionDir/System_BenchmarkLog.csv (分批適度寫入磁碟，釋放記憶體)
        string systemLogPath = _recorder.Start(sessionDir, "System_BenchmarkLog.csv");

        IsRecording = true;
        HasRecordedStats = true;
        LastLogFilePath = systemLogPath;
        LastLogFileName = Path.GetFileName(systemLogPath);
        OnPropertyChanged(nameof(HasLogFile));

        RecordingStatusBadge = selectedTasks.Count > 0
            ? $"🔴 正在紀錄全系統與 {selectedTasks.Count} 個勾選任務 (資料夾已建立)..."
            : "🔴 正在紀錄全系統效能樣本...";

        StatusText = "🔴 效能日誌量測紀錄中...";
        UpdateRecordingStatsUI();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ExecuteStopRecording()
    {
        string systemLogPath = _recorder.Stop();
        var writtenTaskFiles = _multiRecorder.StopAndFlush();
        _procWatcher.SetRecordingWatchTargets(null);
        IsRecording = false;

        var stats = _recorder.GetStats();

        if (!string.IsNullOrEmpty(systemLogPath))
        {
            LastLogFilePath = systemLogPath;
            LastLogFileName = Path.GetFileName(systemLogPath);
            OnPropertyChanged(nameof(HasLogFile));
            RecordingStatusBadge = $"⏹ 已停止 (共紀錄 {stats.SampleCount} 筆樣本，含 {writtenTaskFiles.Count} 個任務獨立 CSV)";
        }
        else
        {
            RecordingStatusBadge = $"⏹ 已停止 (共紀錄 {stats.SampleCount} 筆樣本)";
        }

        StatusText = "🟢 即時監控中 (已儲存上一筆量測日誌)";
        UpdateRecordingStatsUI();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ExecuteOpenLogFile()
    {
        if (string.IsNullOrEmpty(_lastLogFilePath) || !File.Exists(_lastLogFilePath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_lastLogFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"無法開啟 Log 檔案:\n{ex.Message}", "開啟錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExecuteOpenLogFolder()
    {
        string dirToOpen = !string.IsNullOrEmpty(_multiRecorder.MainSessionDirectory) && Directory.Exists(_multiRecorder.MainSessionDirectory)
            ? _multiRecorder.MainSessionDirectory
            : (!string.IsNullOrEmpty(_lastLogFilePath) ? Path.GetDirectoryName(_lastLogFilePath) : null) ?? string.Empty;

        if (string.IsNullOrEmpty(dirToOpen) || !Directory.Exists(dirToOpen)) return;

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dirToOpen}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"無法開啟 Log 資料夾:\n{ex.Message}", "開啟錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateRecordingStatsUI()
    {
        var stats = _recorder.GetStats();
        string durationStr = stats.Duration.TotalHours >= 1
            ? $"{(int)stats.Duration.TotalHours:00}:{stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}"
            : $"{stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}";
        RecordingDurationText = $"⏱ {durationStr} ({stats.SampleCount} 筆)";

        if (stats.SampleCount > 0)
        {
            RecordedCpuMaxText = $"{stats.CpuMax:0.0} %";
            RecordedCpuMinText = $"{stats.CpuMin:0.0} %";
            RecordedCpuAvgText = $"{stats.CpuAvg:0.0} %";

            RecordedMemMaxText = $"{stats.MemoryGbMax:0.00} GB ({stats.MemoryPercentMax:0}%)";
            RecordedMemMinText = $"{stats.MemoryGbMin:0.00} GB ({stats.MemoryPercentMin:0}%)";
            RecordedMemAvgText = $"{stats.MemoryGbAvg:0.00} GB ({stats.MemoryPercentAvg:0}%)";

            if (IsRecording)
            {
                int trackedCount = _procWatcher.Items.Count(x => x.IsSelected);
                RecordingStatusBadge = trackedCount > 0
                    ? $"🔴 正在採樣 (樣本: {stats.SampleCount} 筆 | 追蹤任務: {trackedCount} 個)"
                    : $"🔴 正在採樣 (樣本: {stats.SampleCount} 筆)";
            }
        }
        else
        {
            RecordedCpuMaxText = "--";
            RecordedCpuMinText = "--";
            RecordedCpuAvgText = "--";
            RecordedMemMaxText = "--";
            RecordedMemMinText = "--";
            RecordedMemAvgText = "--";
        }
    }

    private void ExecuteCopySummary()
    {
        var stats = _recorder.GetStats();
        if (stats.SampleCount == 0) return;

        string sessionDir = _multiRecorder.MainSessionDirectory;
        string tasksSummary = _multiRecorder.GetTasksSummaryText()
            .Replace("\r\n", "\n")
            .TrimEnd('\n');
        int runningUserProcesses = _procWatcher.Items.Count(x => x.IsRunning);
        string hardwareSection = _hardwareInspector
            .BuildTaskManagerHardwareSection(runningUserProcesses)
            .Replace("\r\n", "\n");

        string durationStr = stats.Duration.TotalHours >= 1
            ? $"{(int)stats.Duration.TotalHours:00}:{stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}"
            : $"{stats.Duration.Minutes:00}:{stats.Duration.Seconds:00}";

        string fullSummary =
            $"========================================\n" +
            $"【TRIOV2 區間效能量測統計報告】\n" +
            $"量測時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
            $"樣本數量：共 {stats.SampleCount} 筆樣本 (紀錄時長 {durationStr})\n" +
            $"紀錄目錄：{sessionDir}\n" +
            $"基準日誌：{_lastLogFilePath}\n" +
            $"----------------------------------------\n" +
            $"全系統處理器 CPU 使用率：\n" +
            $"  - 最高 (Max) : {stats.CpuMax:0.0} %\n" +
            $"  - 最低 (Min) : {stats.CpuMin:0.0} %\n" +
            $"  - 平均 (Avg) : {stats.CpuAvg:0.0} %\n" +
            $"全系統實體記憶體 RAM 使用量：\n" +
            $"  - 最高 (Max) : {stats.MemoryGbMax:0.00} GB ({stats.MemoryPercentMax:0} %)\n" +
            $"  - 最低 (Min) : {stats.MemoryGbMin:0.00} GB ({stats.MemoryPercentMin:0} %)\n" +
            $"  - 平均 (Avg) : {stats.MemoryGbAvg:0.00} GB ({stats.MemoryPercentAvg:0} %)\n" +
            (string.IsNullOrEmpty(tasksSummary) ? "" : $"{tasksSummary}\n") +
            (string.IsNullOrEmpty(hardwareSection) ? "" : $"{hardwareSection}\n") +
            $"========================================";

        try
        {
            // 1. 複製至系統剪貼簿
            Clipboard.SetText(fullSummary);

            // 2. 自動在啟動目錄寫入 ref.txt
            string refPath = _multiRecorder.WriteRefTextFile(fullSummary);

            MessageBox.Show(
                $"統計報告已成功複製至剪貼簿！\n\n並已同步寫入摘要檔案：\n{refPath}",
                "複製成功與 ref.txt 產出",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"複製或寫入 ref.txt 失敗:\n{ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void Dispose()
    {
        _procWatcher.Dispose();
        _sampler.Dispose();
    }
}
