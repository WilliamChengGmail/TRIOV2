using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using TRIOV2.PerformanceAnalyzer.Core;
using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.ViewModels;

/// <summary>
/// TRIO V2 硬體效能數據分析器主 ViewModel
/// 製作者: Office of William
/// </summary>
public sealed class MainAnalyzerViewModel : ViewModelBase
{
    private readonly List<BenchmarkSession> _allLoadedSessions = new();
    private CancellationTokenSource? _scanCts;
    private bool _suppressRecalculate;

    private string _targetPath = string.Empty;
    private string _scannedRootPath = string.Empty;
    private bool _isSingleSessionMode;
    private bool _isScanning;
    private bool _hideShortRuns;
    private string _searchKeyword = string.Empty;
    private int _selectedTabIndex;
    private string _statusMessage = "請指定 Log 資料夾路徑或直接拖放資料夾至此視窗";
    private string _modeBadgeText = "尚未載入";

    private DeviceAggregateSummary? _selectedTrendDevice;
    private BenchmarkSession? _selectedSession;
    private BenchmarkSession? _baselineSession;
    private string _customAliasInput = string.Empty;

    // 驗收門檻設定
    private double _cpuP95PassLimit = 70.0;
    private double _cpuP95WarnLimit = 85.0;
    private double _availableRamPassGb = 2.0;
    private double _availableRamUpgradeGb = 1.0;

    // 總覽 KPI
    private string _kpiDeviceCountText = "0 台";
    private string _kpiRunCountText = "0 次";
    private string _kpiPassCountText = "0 台";
    private string _kpiAttentionCountText = "0 台";
    private string _fleetSummaryText = "尚未載入測試資料。";
    private string _fleetSummaryDetailText = string.Empty;
    private int _upgradeDeviceCount;
    private int _warnDeviceCount;
    private int _passDeviceCount;
    private int _insufficientDeviceCount;
    private string _lastSavedThresholds = string.Empty;
    private IReadOnlyList<DeviceAggregateSummary> _chartDevices = Array.Empty<DeviceAggregateSummary>();

    public ObservableCollection<DeviceAggregateSummary> DeviceGroups { get; } = new();
    public ObservableCollection<BenchmarkSession> VisibleSessions { get; } = new();
    public ObservableCollection<SessionComparisonRow> ComparisonRows { get; } = new();
    public ObservableCollection<TaskExecutionRun> CheckedTaskExecutionRuns { get; } = new();

    private TaskBenchmarkSummary? _selectedTaskSummary;
    public TaskBenchmarkSummary? SelectedTaskSummary
    {
        get => _selectedTaskSummary;
        set
        {
            if (SetProperty(ref _selectedTaskSummary, value))
            {
                UpdateCheckedTaskExecutionRuns();
            }
        }
    }

    public IReadOnlyList<DeviceAggregateSummary> ChartDevices
    {
        get => _chartDevices;
        private set => SetProperty(ref _chartDevices, value);
    }

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value);
    }

    public bool IsSingleSessionMode
    {
        get => _isSingleSessionMode;
        private set
        {
            if (SetProperty(ref _isSingleSessionMode, value))
            {
                OnPropertyChanged(nameof(CanGoUpLevel));
            }
        }
    }

    public bool CanGoUpLevel => !string.IsNullOrWhiteSpace(_scannedRootPath) &&
                                Directory.GetParent(_scannedRootPath) != null;

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetProperty(ref _isScanning, value);
    }

    public bool HideShortRuns
    {
        get => _hideShortRuns;
        set
        {
            if (SetProperty(ref _hideShortRuns, value))
            {
                RebuildViews();
            }
        }
    }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (SetProperty(ref _searchKeyword, value))
            {
                RebuildViews();
            }
        }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ModeBadgeText
    {
        get => _modeBadgeText;
        private set => SetProperty(ref _modeBadgeText, value);
    }

    public DeviceAggregateSummary? SelectedTrendDevice
    {
        get => _selectedTrendDevice;
        set => SetProperty(ref _selectedTrendDevice, value);
    }

    public BenchmarkSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                CustomAliasInput = value?.AssignedDeviceAlias ?? string.Empty;
                HookTaskListeners(value);
                _selectedTaskSummary = value?.Tasks.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedTaskSummary));
                UpdateCheckedTaskExecutionRuns();
            }
        }
    }

    public string CheckedRunsCountBadge
    {
        get
        {
            int checkedTaskCount = SelectedSession?.Tasks.Count(t => t.IsOverlayVisible) ?? 0;
            if (checkedTaskCount > 0)
            {
                return $"已勾選 {checkedTaskCount} 個任務，共抓取到 {CheckedTaskExecutionRuns.Count} 次執行時段";
            }
            if (SelectedTaskSummary != null && SelectedTaskSummary.ExecutionCount > 0)
            {
                return $"當前選取 [{SelectedTaskSummary.TaskName}]，共 {CheckedTaskExecutionRuns.Count} 次執行時段 (勾選核取框可合併比對)";
            }
            return "未勾選任務";
        }
    }

    public BenchmarkSession? BaselineSession
    {
        get => _baselineSession;
        set
        {
            if (SetProperty(ref _baselineSession, value))
            {
                RebuildComparisonRows();
            }
        }
    }

    public string CustomAliasInput
    {
        get => _customAliasInput;
        set => SetProperty(ref _customAliasInput, value);
    }

    public double CpuP95PassLimit
    {
        get => _cpuP95PassLimit;
        set
        {
            double clamped = Math.Clamp(value, 30, 95);
            if (SetProperty(ref _cpuP95PassLimit, clamped))
            {
                if (_cpuP95WarnLimit < clamped) CpuP95WarnLimit = Math.Min(98, clamped + 10);
                RecalculateAggregates();
            }
        }
    }

    public double CpuP95WarnLimit
    {
        get => _cpuP95WarnLimit;
        set
        {
            double clamped = Math.Clamp(value, _cpuP95PassLimit, 99);
            if (SetProperty(ref _cpuP95WarnLimit, clamped))
            {
                RecalculateAggregates();
            }
        }
    }

    /// <summary>最低可用記憶體 ≥ 此值（GB）才算有餘裕。</summary>
    public double AvailableRamPassGb
    {
        get => _availableRamPassGb;
        set
        {
            double clamped = Math.Clamp(value, 0.5, 16);
            if (SetProperty(ref _availableRamPassGb, clamped))
            {
                if (_availableRamUpgradeGb > clamped) AvailableRamUpgradeGb = clamped;
                RecalculateAggregates();
            }
        }
    }

    /// <summary>最低可用記憶體 ≤ 此值（GB）建議升級。</summary>
    public double AvailableRamUpgradeGb
    {
        get => _availableRamUpgradeGb;
        set
        {
            double clamped = Math.Clamp(value, 0, _availableRamPassGb);
            if (SetProperty(ref _availableRamUpgradeGb, clamped))
            {
                RecalculateAggregates();
            }
        }
    }

    public string KpiDeviceCountText { get => _kpiDeviceCountText; private set => SetProperty(ref _kpiDeviceCountText, value); }
    public string KpiRunCountText { get => _kpiRunCountText; private set => SetProperty(ref _kpiRunCountText, value); }
    public string KpiPassCountText { get => _kpiPassCountText; private set => SetProperty(ref _kpiPassCountText, value); }
    public string KpiAttentionCountText { get => _kpiAttentionCountText; private set => SetProperty(ref _kpiAttentionCountText, value); }

    /// <summary>給主管／非技術讀者的一句話總結。</summary>
    public string FleetSummaryText { get => _fleetSummaryText; private set => SetProperty(ref _fleetSummaryText, value); }

    /// <summary>總結下方的補充說明（排除了哪些資料）。</summary>
    public string FleetSummaryDetailText { get => _fleetSummaryDetailText; private set => SetProperty(ref _fleetSummaryDetailText, value); }

    public int UpgradeDeviceCount { get => _upgradeDeviceCount; private set => SetProperty(ref _upgradeDeviceCount, value); }
    public int WarnDeviceCount { get => _warnDeviceCount; private set => SetProperty(ref _warnDeviceCount, value); }
    public int PassDeviceCount { get => _passDeviceCount; private set => SetProperty(ref _passDeviceCount, value); }
    public int InsufficientDeviceCount { get => _insufficientDeviceCount; private set => SetProperty(ref _insufficientDeviceCount, value); }

    public ICommand BrowseFolderCommand { get; }
    public ICommand ScanPathCommand { get; }
    public ICommand GoUpLevelCommand { get; }
    public ICommand IncludeAllCommand { get; }
    public ICommand ExcludeAllCommand { get; }
    public ICommand ApplyAliasCommand { get; }
    public ICommand ClearAliasCommand { get; }
    public ICommand ResetThresholdsCommand { get; }
    public ICommand ExportHtmlCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand CopyMarkdownCommand { get; }

    public MainAnalyzerViewModel()
    {
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        ScanPathCommand = new RelayCommand(async _ => await LoadPathAsync(TargetPath));
        GoUpLevelCommand = new RelayCommand(async _ => await GoUpOneLevelAsync(), _ => CanGoUpLevel);
        IncludeAllCommand = new RelayCommand(_ => SetAllVisibleIncluded(true));
        ExcludeAllCommand = new RelayCommand(_ => SetAllVisibleIncluded(false));
        ApplyAliasCommand = new RelayCommand(_ => SaveSessionAlias(CustomAliasInput));
        ClearAliasCommand = new RelayCommand(_ => SaveSessionAlias(null));
        ResetThresholdsCommand = new RelayCommand(_ => ResetDefaultThresholds());
        ExportHtmlCommand = new RelayCommand(_ => ExportHtmlReport());
        ExportCsvCommand = new RelayCommand(_ => ExportCsvReport());
        CopyMarkdownCommand = new RelayCommand(_ => CopyMarkdownReport());

        // 載入上次保存的判定門檻（執行檔旁 analyzer-settings.json）
        var saved = AnalyzerSettingsStore.Load();
        _cpuP95PassLimit = Math.Clamp(saved.CpuP95PassLimit, 30, 95);
        _cpuP95WarnLimit = Math.Clamp(saved.CpuP95WarnLimit, _cpuP95PassLimit, 99);
        _availableRamPassGb = Math.Clamp(saved.AvailableRamPassGb, 0.5, 16);
        _availableRamUpgradeGb = Math.Clamp(saved.AvailableRamUpgradeGb, 0, _availableRamPassGb);
        _lastSavedThresholds = ThresholdSignature(CurrentThresholds());
    }

    public async Task InitializeAsync(string? startupPath)
    {
        string? candidate = null;
        if (!string.IsNullOrWhiteSpace(startupPath) && Directory.Exists(startupPath))
        {
            candidate = startupPath;
        }
        else
        {
            string localLogs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            string publishLogs = @"D:\TRIOV2\publish_tools\Logs";
            if (Directory.Exists(localLogs)) candidate = localLogs;
            else if (Directory.Exists(publishLogs)) candidate = publishLogs;
        }

        if (candidate != null)
        {
            TargetPath = candidate;
            await LoadPathAsync(candidate);
        }
    }

    public async Task LoadPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            StatusMessage = "找不到指定路徑，請確認資料夾是否存在";
            return;
        }

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;
        TargetPath = Path.GetFullPath(path);
        StatusMessage = $"正在掃描：{TargetPath} ...";

        try
        {
            var progress = new Progress<(int Done, int Total, string Folder)>(p =>
            {
                StatusMessage = $"正在解析測試紀錄 ({p.Done}/{p.Total})：{p.Folder}";
            });

            var result = await LogSessionScanner.ScanPathAsync(TargetPath, CurrentThresholds(), progress, ct);

            DetachSessionListeners();
            _allLoadedSessions.Clear();
            _allLoadedSessions.AddRange(result.Sessions);
            foreach (var s in _allLoadedSessions)
            {
                s.PropertyChanged += Session_PropertyChanged;
            }

            _scannedRootPath = result.ScannedRootPath;
            IsSingleSessionMode = result.IsSingleSessionMode;
            OnPropertyChanged(nameof(CanGoUpLevel));

            RebuildViews();

            // 單次路徑預設跳至 Tab 3（單次時間軸與任務剖析），總目錄模式跳至 Tab 1（設備總覽與選型建議）
            SelectedTabIndex = result.IsSingleSessionMode ? 2 : 0;

            if (_allLoadedSessions.Count == 0)
            {
                StatusMessage = $"於 {TargetPath} 未找到包含 System_BenchmarkLog.csv 或 ref.txt 的測試資料夾";
            }
            else
            {
                StatusMessage = result.IsSingleSessionMode
                    ? $"已載入單次量測：{_allLoadedSessions[0].SessionName} ({_allLoadedSessions[0].SampleCount} 筆樣本，雙擊左側場次或於右上按「上層目錄」檢視多機彙總)"
                    : $"掃描完成：共找到 {_allLoadedSessions.Count} 筆測試紀錄，已自動分組為 {DeviceGroups.Count} 台設備";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"掃描失敗：{ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void OpenSessionDetail(BenchmarkSession session)
    {
        SelectedSession = session;
        SelectedTabIndex = 2;
    }

    private void BrowseFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "選擇 TRIOV2 單次測試資料夾或跨設備合併 Log 總目錄"
        };
        if (Directory.Exists(TargetPath))
        {
            dlg.InitialDirectory = TargetPath;
        }
        if (dlg.ShowDialog() == true)
        {
            TargetPath = dlg.FolderName;
            _ = LoadPathAsync(TargetPath);
        }
    }

    private async Task GoUpOneLevelAsync()
    {
        if (string.IsNullOrWhiteSpace(_scannedRootPath)) return;
        var parent = Directory.GetParent(_scannedRootPath);
        if (parent != null && parent.Exists)
        {
            await LoadPathAsync(parent.FullName);
        }
    }

    private void SetAllVisibleIncluded(bool include)
    {
        _suppressRecalculate = true;
        foreach (var s in VisibleSessions)
        {
            s.IsIncluded = include;
        }
        _suppressRecalculate = false;
        RecalculateAggregates();
    }

    private void SaveSessionAlias(string? alias)
    {
        if (SelectedSession == null || string.IsNullOrWhiteSpace(_scannedRootPath)) return;

        string? normalized = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
        SelectedSession.AssignedDeviceAlias = normalized;

        var aliases = LogSessionScanner.LoadAliases(_scannedRootPath);
        if (normalized == null)
        {
            aliases.Remove(SelectedSession.FolderPath);
        }
        else
        {
            aliases[SelectedSession.FolderPath] = normalized;
        }
        bool saved = LogSessionScanner.SaveAliases(_scannedRootPath, aliases);

        RecalculateAggregates();
        string message = normalized == null
            ? $"已清除 {SelectedSession.SessionName} 的自訂設備標籤"
            : $"已將 {SelectedSession.SessionName} 歸類為「{normalized}」";
        StatusMessage = saved
            ? message
            : $"{message}（注意：無法寫入 {_scannedRootPath}，資料夾可能為唯讀，重新開啟後設定會遺失）";
    }

    private void ResetDefaultThresholds()
    {
        _cpuP95PassLimit = 70.0;
        _cpuP95WarnLimit = 85.0;
        _availableRamPassGb = 2.0;
        _availableRamUpgradeGb = 1.0;
        OnPropertyChanged(nameof(CpuP95PassLimit));
        OnPropertyChanged(nameof(CpuP95WarnLimit));
        OnPropertyChanged(nameof(AvailableRamPassGb));
        OnPropertyChanged(nameof(AvailableRamUpgradeGb));
        RecalculateAggregates();
    }

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressRecalculate) return;
        if (e.PropertyName == nameof(BenchmarkSession.IsIncluded))
        {
            RecalculateAggregates();
        }
    }

    private void RebuildViews()
    {
        string kw = SearchKeyword.Trim();
        var filtered = _allLoadedSessions.Where(s =>
        {
            if (HideShortRuns && s.Duration.TotalSeconds < 10) return false;
            if (string.IsNullOrEmpty(kw)) return true;

            return s.SessionName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || s.EffectiveDeviceDisplayName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || s.HardwareProfile.CpuModel.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || s.HardwareProfile.OsName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || s.Tasks.Any(t => t.TaskName.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        VisibleSessions.Clear();
        foreach (var s in filtered) VisibleSessions.Add(s);

        if (SelectedSession == null || !VisibleSessions.Contains(SelectedSession))
        {
            SelectedSession = VisibleSessions.FirstOrDefault();
        }
        if (BaselineSession == null || !VisibleSessions.Contains(BaselineSession))
        {
            BaselineSession = VisibleSessions.FirstOrDefault();
        }

        RecalculateAggregates();
    }

    private void RecalculateAggregates()
    {
        var thresholds = CurrentThresholds();
        var groups = DeviceGroupingEngine.BuildDeviceAggregates(VisibleSessions, thresholds);

        string? prevTrendKey = SelectedTrendDevice?.DeviceKey;

        DeviceGroups.Clear();
        foreach (var g in groups) DeviceGroups.Add(g);
        ChartDevices = groups.Where(g => g.IncludedRunCount > 0).ToList();

        SelectedTrendDevice = groups.FirstOrDefault(g => string.Equals(g.DeviceKey, prevTrendKey, StringComparison.OrdinalIgnoreCase))
                              ?? groups.FirstOrDefault();

        int activeDevs = groups.Count(g => g.IncludedRunCount > 0);
        int activeRuns = groups.Sum(g => g.IncludedRunCount);
        int passDevs = groups.Count(g => g.IncludedRunCount > 0 && g.Verdict.Level == SizingLevel.Pass);
        int warnDevs = groups.Count(g => g.IncludedRunCount > 0 && (g.Verdict.Level == SizingLevel.Warning || g.Verdict.Level == SizingLevel.UpgradeRequired));

        KpiDeviceCountText = $"{activeDevs} 台";
        KpiRunCountText = $"{activeRuns} 次";
        KpiPassCountText = $"{passDevs} 台";
        KpiAttentionCountText = $"{warnDevs} 台";

        UpdateFleetSummary(groups);
        SaveThresholdsIfChanged(thresholds);

        ModeBadgeText = IsSingleSessionMode
            ? $"單次分析模式 | {SelectedSession?.SessionName}"
            : $"多設備總覽模式 | {activeDevs} 台設備 / {activeRuns} 次納入分析";

        RebuildComparisonRows();
    }

    private void RebuildComparisonRows()
    {
        ComparisonRows.Clear();
        var included = VisibleSessions.Where(s => s.IsIncluded).ToList();
        if (included.Count == 0) return;

        var baseline = BaselineSession != null && included.Contains(BaselineSession)
            ? BaselineSession
            : included[0];

        foreach (var s in included)
        {
            string cpuDelta = s == baseline
                ? "基準 (0.0%)"
                : FormatSignedDelta(s.CpuP95 - baseline.CpuP95, "%");
            string ramDelta = s == baseline
                ? "基準 (0.0%)"
                : FormatSignedDelta(s.RamPeakPercent - baseline.RamPeakPercent, "%");

            ComparisonRows.Add(new SessionComparisonRow
            {
                Session = s,
                SessionLabel = s.SessionName,
                DeviceName = s.EffectiveDeviceDisplayName,
                CpuModel = s.HardwareProfile.CpuModel,
                RamSpec = s.HardwareProfile.ShortMemoryLabel,
                DurationText = s.DurationText,
                CpuAvg = s.CpuAvg,
                CpuP95 = s.CpuP95,
                CpuMax = s.CpuMax,
                RamPeakPercent = s.RamPeakPercent,
                RamPeakGb = s.RamMaxGb,
                MinAvailableText = s.HasAvailableMemory ? $"{s.MinAvailableGb:0.0} GB" : "--",
                CpuP95DeltaText = cpuDelta,
                RamPeakDeltaText = ramDelta,
                VerdictBadge = s.Verdict.ShortLabel,
                VerdictPlain = s.Verdict.PlainSummary,
                VerdictColorHex = s.Verdict.BadgeColorHex
            });
        }
    }

    private void ExportHtmlReport()
    {
        if (DeviceGroups.Count == 0) return;
        var dlg = new SaveFileDialog
        {
            Filter = "HTML 視覺化報表 (*.html)|*.html",
            FileName = $"TRIOV2_Benchmark_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };
        if (dlg.ShowDialog() == true)
        {
            ReportExporter.ExportHtml(dlg.FileName, TargetPath, DeviceGroups.ToList(), CurrentThresholds());
            StatusMessage = $"已匯出 HTML 報告：{dlg.FileName}";
        }
    }

    private void ExportCsvReport()
    {
        if (DeviceGroups.Count == 0) return;
        var dlg = new SaveFileDialog
        {
            Filter = "CSV 統計總表 (*.csv)|*.csv",
            FileName = $"TRIOV2_Device_Summary_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dlg.ShowDialog() == true)
        {
            ReportExporter.ExportCsv(dlg.FileName, DeviceGroups);
            StatusMessage = $"已匯出 CSV 總表：{dlg.FileName}";
        }
    }

    /// <summary>
    /// 批次匯出（命令列 --export 使用）：在指定資料夾輸出 HTML、CSV、Markdown 三種報告。
    /// </summary>
    public IReadOnlyList<string> ExportAllTo(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var devices = DeviceGroups.ToList();
        var thresholds = CurrentThresholds();

        string html = Path.Combine(outputDir, $"TRIOV2_Benchmark_Report_{stamp}.html");
        string csv = Path.Combine(outputDir, $"TRIOV2_Device_Summary_{stamp}.csv");
        string md = Path.Combine(outputDir, $"TRIOV2_Benchmark_Report_{stamp}.md");

        ReportExporter.ExportHtml(html, TargetPath, devices, thresholds);
        ReportExporter.ExportCsv(csv, devices);
        File.WriteAllText(md, ReportExporter.BuildMarkdownReport(TargetPath, devices, thresholds), new System.Text.UTF8Encoding(true));
        return new[] { html, csv, md };
    }

    private void CopyMarkdownReport()
    {
        if (DeviceGroups.Count == 0) return;
        string md = ReportExporter.BuildMarkdownReport(TargetPath, DeviceGroups.ToList(), CurrentThresholds());
        Clipboard.SetText(md);
        StatusMessage = "已將完整 Markdown 效能比較摘要複製到系統剪貼簿！";
    }

    private void UpdateFleetSummary(IReadOnlyList<DeviceAggregateSummary> groups)
    {
        var summary = FleetSummary.Build(groups);
        UpgradeDeviceCount = summary.UpgradeCount;
        WarnDeviceCount = summary.WarnCount;
        PassDeviceCount = summary.PassCount;
        InsufficientDeviceCount = summary.InsufficientCount;
        FleetSummaryText = summary.Sentence;

        var notes = new List<string>();
        if (InsufficientDeviceCount > 0)
        {
            notes.Add($"另有 {InsufficientDeviceCount} 台設備資料不足，未列入判斷");
        }

        int excludedShort = VisibleSessions.Count(s => !s.IsIncluded && SizingAdvisorEngine.IsTooShort(s));
        if (excludedShort > 0)
        {
            notes.Add($"{excludedShort} 筆短於 {SizingAdvisorEngine.MinimumValidSeconds:0} 秒的試跑紀錄已自動排除（可在左側手動勾選）");
        }

        FleetSummaryDetailText = notes.Count == 0 ? string.Empty : string.Join("；", notes) + "。";
    }

    private void SaveThresholdsIfChanged(EvaluationThresholds thresholds)
    {
        string signature = ThresholdSignature(thresholds);
        if (signature == _lastSavedThresholds) return;
        if (AnalyzerSettingsStore.TrySave(thresholds))
        {
            _lastSavedThresholds = signature;
        }
    }

    private static string ThresholdSignature(EvaluationThresholds t)
        => $"{t.CpuP95PassLimit}|{t.CpuP95WarnLimit}|{t.AvailableRamPassGb}|{t.AvailableRamUpgradeGb}";

    private EvaluationThresholds CurrentThresholds() => new()
    {
        CpuP95PassLimit = CpuP95PassLimit,
        CpuP95WarnLimit = CpuP95WarnLimit,
        AvailableRamPassGb = AvailableRamPassGb,
        AvailableRamUpgradeGb = AvailableRamUpgradeGb
    };

    private static string FormatSignedDelta(double delta, string unit)
    {
        if (Math.Abs(delta) < 0.05) return $"±0.0{unit}";
        return delta > 0 ? $"+{delta:0.0}{unit} (較高)" : $"{delta:0.0}{unit} (較省)";
    }

    private void DetachSessionListeners()
    {
        foreach (var s in _allLoadedSessions)
        {
            s.PropertyChanged -= Session_PropertyChanged;
        }
        HookTaskListeners(null);
    }

    private readonly List<TaskBenchmarkSummary> _hookedTasks = new();

    private void HookTaskListeners(BenchmarkSession? session)
    {
        foreach (var t in _hookedTasks)
        {
            t.PropertyChanged -= Task_PropertyChanged;
        }
        _hookedTasks.Clear();

        if (session?.Tasks == null) return;
        foreach (var t in session.Tasks)
        {
            t.PropertyChanged += Task_PropertyChanged;
            _hookedTasks.Add(t);
        }
    }

    private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaskBenchmarkSummary.IsOverlayVisible))
        {
            UpdateCheckedTaskExecutionRuns();
        }
    }

    public void UpdateCheckedTaskExecutionRuns()
    {
        CheckedTaskExecutionRuns.Clear();
        if (SelectedSession?.Tasks == null)
        {
            OnPropertyChanged(nameof(CheckedRunsCountBadge));
            return;
        }

        var checkedTasks = SelectedSession.Tasks.Where(t => t.IsOverlayVisible).ToList();
        var targetTasks = checkedTasks.Count > 0
            ? checkedTasks
            : (SelectedTaskSummary != null ? new List<TaskBenchmarkSummary> { SelectedTaskSummary } : SelectedSession.Tasks.Where(t => t.ExecutionCount > 0).Take(2).ToList());

        var allRuns = targetTasks
            .SelectMany(t => t.ExecutionRuns)
            .OrderBy(r => r.StartTime)
            .ThenBy(r => r.TaskName);

        foreach (var r in allRuns)
        {
            CheckedTaskExecutionRuns.Add(r);
        }

        OnPropertyChanged(nameof(CheckedRunsCountBadge));
    }
}
