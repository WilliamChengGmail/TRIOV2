using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PerformanceAnalysis.App.Infrastructure;
using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Caching;
using PerformanceAnalysis.Core.Configuration;
using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Models;
using PerformanceAnalysis.Core.Reporting;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SessionMetadataStore _metadataStore;
    private readonly AnalysisSettingsStore _settingsStore;
    private readonly SessionCache _sessionCache = new();
    private readonly AsyncRelayCommand _scanCommand;
    private readonly AsyncRelayCommand _exportCommand;
    private readonly RelayCommand _backCommand;
    private readonly List<object> _history = new();
    private AnalysisOptions _analysisOptions;

    private string _sourcePath = string.Empty;
    private string _statusText = string.Empty;
    private bool _isBusy;
    private object? _currentPage;
    private IReadOnlyList<Session> _sessions = Array.Empty<Session>();

    public MainViewModel(string? metadataDirectory = null)
    {
        var metadataRoot = metadataDirectory ?? Path.Combine(AppContext.BaseDirectory, "Metadata");
        _metadataStore = new SessionMetadataStore(metadataRoot);
        _settingsStore = new AnalysisSettingsStore(metadataRoot);
        _analysisOptions = _settingsStore.Load();
        _scanCommand = new AsyncRelayCommand(
            ScanAsync,
            () => !IsBusy && !string.IsNullOrWhiteSpace(SourcePath));

        _scanCommand.ExecutionFailed += (_, exception) =>
        {
            StatusText = $"掃描失敗：{exception.Message}";
            MessageBox.Show(exception.ToString(), "掃描失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        _exportCommand = new AsyncRelayCommand(
            ExportHtmlAsync,
            () => !IsBusy && _sessions.Count > 0);

        _backCommand = new RelayCommand(
            _ => Back(),
            _ => _history.Count > 0 || (CurrentPage is not null && CurrentPage is not OverviewViewModel));

        BrowseCommand = new RelayCommand(_ => Browse());
        BackCommand = _backCommand;
        HomeCommand = new RelayCommand(_ => Home());
        OpenSessionCommand = new RelayCommand(parameter => OpenSession(parameter));
        DevicesCommand = new RelayCommand(_ => ShowDevices());
        CompareCommand = new RelayCommand(_ => ShowCompare());
        SettingsCommand = new RelayCommand(_ => ShowSettings());
        ClearCacheCommand = new RelayCommand(_ => ClearSessionCache());

        SourcePath = Directory.Exists(@"D:\TRIOV2\publish_tools\Logs")
            ? @"D:\TRIOV2\publish_tools\Logs"
            : string.Empty;
        StatusText = "選擇資料夾後按「掃描」，或直接掃描預設路徑。";
    }

    public ICommand BrowseCommand { get; }

    public ICommand ScanCommand => _scanCommand;

    public ICommand ExportHtmlCommand => _exportCommand;

    public ICommand BackCommand { get; }

    public ICommand HomeCommand { get; }

    public ICommand OpenSessionCommand { get; }

    public ICommand DevicesCommand { get; }

    public ICommand CompareCommand { get; }

    public ICommand SettingsCommand { get; }

    public ICommand ClearCacheCommand { get; }

    public int CacheCount => _sessionCache.Count;

    public string CacheCountText => $"{CacheCount} 場（記憶體）";

    public string SourcePath
    {
        get => _sourcePath;
        set
        {
            if (SetProperty(ref _sourcePath, value))
            {
                _scanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _scanCommand.RaiseCanExecuteChanged();
                _exportCommand.RaiseCanExecuteChanged();
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public object? CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsOverview));
                OnPropertyChanged(nameof(IsDetail));
                _backCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<Session> Sessions => _sessions;

    public int SessionCount => _sessions.Count;

    public int DeviceCount => _sessions
        .Select(session => session.Device.DeviceKey)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int TaskCount => _sessions
        .SelectMany(session => session.Tasks.Select(task => task.Name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public bool IsEmpty => CurrentPage is null;

    public bool IsOverview => CurrentPage is OverviewViewModel;

    public bool IsDetail => CurrentPage is SessionDetailViewModel;

    public string SummaryText => _sessions.Count == 0
        ? "尚未載入資料"
        : $"場次 {SessionCount} ・ 裝置 {DeviceCount} ・ 任務 {TaskCount}";

    public async Task ScanAsync()
    {
        var path = SourcePath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "請先選擇資料夾。";
            return;
        }

        if (!Directory.Exists(path))
        {
            StatusText = $"路徑不存在：{path}";
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "掃描中…";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await Task.Run(() =>
            {
                var scan = SessionScanner.Scan(path);
                var analyzer = new SessionAnalyzer(
                    _metadataStore,
                    writeDerivedMetadata: true,
                    _analysisOptions);
                var sessions = new List<Session>();
                var failures = new List<string>();

                foreach (var candidate in scan.Candidates)
                {
                    try
                    {
                        if (_sessionCache.TryGet(candidate, out var cached) && cached is not null)
                        {
                            sessions.Add(cached);
                            continue;
                        }

                        var session = analyzer.Analyze(candidate);
                        sessions.Add(session);
                        _sessionCache.Set(candidate, session);
                    }
                    catch (Exception exception)
                    {
                        failures.Add($"{Path.GetFileName(candidate.Path)}: {exception.Message}");
                    }
                }

                return (Scan: scan, Sessions: sessions, Failures: failures);
            });

            stopwatch.Stop();
            _sessions = result.Sessions
                .OrderByDescending(session => session.StartTime)
                .ToList();
            _history.Clear();

            OnPropertyChanged(nameof(Sessions));
            OnPropertyChanged(nameof(SessionCount));
            OnPropertyChanged(nameof(DeviceCount));
            OnPropertyChanged(nameof(TaskCount));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(CacheCountText));
            _exportCommand.RaiseCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();

            if (_sessions.Count == 0)
            {
                CurrentPage = null;
                StatusText = result.Scan.Candidates.Count == 0
                    ? "找不到任何場次。"
                    : "場次解析失敗。";
                return;
            }

            if (_sessions.Count == 1)
            {
                OpenSession(_sessions[0], addToHistory: false);
            }
            else
            {
                ShowOverview(addToHistory: false);
            }

            var failureText = result.Failures.Count == 0
                ? string.Empty
                : $"，{result.Failures.Count} 場解析失敗";
            StatusText = $"已載入 {_sessions.Count} 場次（{stopwatch.Elapsed.TotalSeconds:0.00}s）{failureText}";
        }
        catch (Exception exception)
        {
            StatusText = $"掃描失敗：{exception.Message}";
            MessageBox.Show(exception.ToString(), "掃描失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    private async Task ExportHtmlAsync()
    {
        if (_sessions.Count == 0)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "匯出 HTML 報告",
            Filter = "HTML 報告 (*.html)|*.html",
            DefaultExt = ".html",
            AddExtension = true,
            FileName = $"TRIOV2-Performance-{DateTime.Now:yyyyMMdd-HHmm}.html"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var path = dialog.FileName;
        var source = SourcePath;
        var sessions = _sessions.ToList();

        IsBusy = true;
        StatusText = "正在產生 HTML 報告…";

        try
        {
            await Task.Run(() =>
            {
                var html = HtmlReportBuilder.Build(sessions, source);
                File.WriteAllText(path, html, new UTF8Encoding(false));
            });

            StatusText = $"HTML 報告已輸出：{path}";

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                // Opening the browser is best effort.
            }
        }
        catch (Exception exception)
        {
            StatusText = $"HTML 報告輸出失敗：{exception.Message}";
            MessageBox.Show(
                exception.ToString(),
                "HTML 報告輸出失敗",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void OpenSession(object? parameter)
    {
        var session = parameter switch
        {
            Session value => value,
            SessionListItem item => item.Session,
            _ => null
        };

        if (session is not null)
        {
            OpenSession(session, addToHistory: true);
        }
    }

    public void OpenSession(Session session, bool addToHistory = true)
    {
        if (addToHistory && CurrentPage is not null)
        {
            _history.Add(CurrentPage);
        }

        CurrentPage = new SessionDetailViewModel(this, session);
        _backCommand.RaiseCanExecuteChanged();
    }

    public void ShowOverview(bool addToHistory = true)
    {
        if (_sessions.Count == 0)
        {
            CurrentPage = null;
            return;
        }

        if (addToHistory && CurrentPage is not null)
        {
            _history.Add(CurrentPage);
        }

        CurrentPage = new OverviewViewModel(this, _sessions);
        _backCommand.RaiseCanExecuteChanged();
    }

    public void Back()
    {
        if (_history.Count == 0)
        {
            ShowOverview(addToHistory: false);
            return;
        }

        var page = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        CurrentPage = page;
        _backCommand.RaiseCanExecuteChanged();
    }

    public void Home()
    {
        _history.Clear();
        ShowOverview(addToHistory: false);
    }

    public void ShowDevices(bool addToHistory = true)
    {
        if (_sessions.Count == 0)
        {
            StatusText = "尚未載入資料。";
            return;
        }

        if (addToHistory && CurrentPage is not null)
        {
            _history.Add(CurrentPage);
        }

        CurrentPage = new DevicesViewModel(this, _sessions);
        _backCommand.RaiseCanExecuteChanged();
    }

    public void ShowCompare(bool addToHistory = true)
    {
        if (_sessions.Count == 0)
        {
            StatusText = "尚未載入資料。";
            return;
        }

        if (addToHistory && CurrentPage is not null)
        {
            _history.Add(CurrentPage);
        }

        CurrentPage = new CompareViewModel(this, _sessions);
        _backCommand.RaiseCanExecuteChanged();
    }

    public void ShowSettings(bool addToHistory = true)
    {
        if (addToHistory && CurrentPage is not null)
        {
            _history.Add(CurrentPage);
        }

        CurrentPage = new SettingsViewModel(this, _analysisOptions.Clone());
        _backCommand.RaiseCanExecuteChanged();
    }

    public async Task ApplySettingsAsync(AnalysisOptions options)
    {
        try
        {
            options.Normalize();
            _settingsStore.Save(options);
            _analysisOptions = options.Clone();
            _sessionCache.Clear();
            OnPropertyChanged(nameof(CacheCountText));

            await ReloadAsync();
            StatusText = "分析設定已儲存，已重新分析。";
            if (CurrentPage is SettingsViewModel settings)
            {
                settings.SetStatus(StatusText);
            }
        }
        catch (Exception exception)
        {
            StatusText = $"儲存設定失敗：{exception.Message}";
        }
    }

    public void ClearSessionCache()
    {
        var count = _sessionCache.Clear();
        OnPropertyChanged(nameof(CacheCountText));
        StatusText = count == 0
            ? "解析快取目前是空的。"
            : $"已清除解析快取（{count} 場）。";
    }

    public async Task SetBaselineAsync(Session session)
    {
        try
        {
            var metadata = GetOrCreateMetadata(session);
            if (metadata is null)
            {
                StatusText = "找不到此場次的中繼資料，無法設定基準。";
                return;
            }

            metadata.DeviceId ??= session.Device.DeviceKey;
            metadata.DeviceName ??= session.Device.DeviceName;

            foreach (var other in _metadataStore.LoadAll()
                         .Where(other => string.Equals(
                                            other.DeviceId,
                                            metadata.DeviceId,
                                            StringComparison.OrdinalIgnoreCase)
                                        && !string.Equals(
                                            other.SessionKey,
                                            metadata.SessionKey,
                                            StringComparison.OrdinalIgnoreCase)))
            {
                if (other.IsBaseline)
                {
                    other.IsBaseline = false;
                    other.UpdatedAt = DateTimeOffset.Now;
                    _metadataStore.Save(other);
                }
            }

            metadata.IsBaseline = true;
            metadata.UpdatedAt = DateTimeOffset.Now;
            _metadataStore.Save(metadata);

            await ReloadAsync();
            StatusText = $"已將 {session.FolderName} 設為基準。";
        }
        catch (Exception exception)
        {
            StatusText = $"設定基準失敗：{exception.Message}";
        }
    }

    public async Task UpdateDeviceAliasAsync(string deviceId, string alias)
    {
        try
        {
            var metadataList = _metadataStore.LoadAll()
                .Where(metadata => string.Equals(metadata.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (metadataList.Count == 0)
            {
                metadataList = _sessions
                    .Where(session => string.Equals(
                        session.Device.DeviceKey,
                        deviceId,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(GetOrCreateMetadata)
                    .Where(metadata => metadata is not null)
                    .Select(metadata => metadata!)
                    .ToList();
            }

            var cleanAlias = alias.Trim();
            foreach (var metadata in metadataList)
            {
                metadata.Alias = string.IsNullOrWhiteSpace(cleanAlias) ? null : cleanAlias;
                metadata.UpdatedAt = DateTimeOffset.Now;
                _metadataStore.Save(metadata);
            }

            await ReloadAsync();
            StatusText = string.IsNullOrWhiteSpace(cleanAlias)
                ? "已清除裝置別名。"
                : $"已將裝置別名更新為「{cleanAlias}」。";
        }
        catch (Exception exception)
        {
            StatusText = $"更新裝置別名失敗：{exception.Message}";
        }
    }

    public async Task MoveSessionAsync(Session session, string targetDeviceId)
    {
        if (string.Equals(session.Device.DeviceKey, targetDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "此場次已經在目標裝置中。";
            return;
        }

        var target = _sessions.FirstOrDefault(candidate =>
            string.Equals(candidate.Device.DeviceKey, targetDeviceId, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            StatusText = "找不到目標裝置。";
            return;
        }

        try
        {
            var metadata = GetOrCreateMetadata(session);
            if (metadata is null)
            {
                StatusText = "找不到此場次的中繼資料。";
                return;
            }

            metadata.DeviceId = target.Device.DeviceKey;
            metadata.DeviceName = target.Device.DeviceName;
            metadata.Alias = target.Device.Alias;
            metadata.DeviceOverridden = true;
            metadata.UpdatedAt = DateTimeOffset.Now;
            _metadataStore.Save(metadata);

            await ReloadAsync();
            StatusText = $"已將 {session.FolderName} 移到「{target.Device.DisplayName}」。";
        }
        catch (Exception exception)
        {
            StatusText = $"移動場次失敗：{exception.Message}";
        }
    }

    public async Task SplitSessionAsync(Session session, string newDeviceName)
    {
        var name = newDeviceName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText = "請輸入新裝置名稱。";
            return;
        }

        try
        {
            var metadata = GetOrCreateMetadata(session);
            if (metadata is null)
            {
                StatusText = "找不到此場次的中繼資料。";
                return;
            }

            metadata.DeviceId = "dev-" + Guid.NewGuid().ToString("N")[..12];
            metadata.DeviceName = name;
            metadata.Alias = name;
            metadata.DeviceOverridden = true;
            metadata.UpdatedAt = DateTimeOffset.Now;
            _metadataStore.Save(metadata);

            await ReloadAsync();
            StatusText = $"已建立新裝置「{name}」。";
        }
        catch (Exception exception)
        {
            StatusText = $"建立新裝置失敗：{exception.Message}";
        }
    }

    public Task<bool> UpdateAnnotationsAsync(Session session, string notes, string labelsText)
    {
        try
        {
            var metadata = GetOrCreateMetadata(session);
            if (metadata is null)
            {
                StatusText = "找不到此場次的中繼資料。";
                return Task.FromResult(false);
            }

            metadata.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            metadata.Labels = SplitLabels(labelsText);
            metadata.UpdatedAt = DateTimeOffset.Now;
            _metadataStore.Save(metadata);

            if (session.Metadata is not null)
            {
                session.Metadata.Notes = metadata.Notes;
                session.Metadata.Labels = metadata.Labels;
            }

            StatusText = $"已儲存 {session.FolderName} 的備註與標籤。";
            return Task.FromResult(true);
        }
        catch (Exception exception)
        {
            StatusText = $"儲存備註失敗：{exception.Message}";
            return Task.FromResult(false);
        }
    }

    private async Task ReloadAsync(bool clearCache = true)
    {
        var pageType = CurrentPage?.GetType();
        var detailFolder = (CurrentPage as SessionDetailViewModel)?.Session.FolderName;

        if (clearCache)
        {
            _sessionCache.Clear();
            OnPropertyChanged(nameof(CacheCountText));
        }

        await ScanAsync();
        if (_sessions.Count == 0)
        {
            return;
        }

        if (pageType == typeof(SettingsViewModel))
        {
            ShowSettings(addToHistory: false);
            return;
        }

        if (pageType == typeof(SessionDetailViewModel) && detailFolder is not null)
        {
            var target = _sessions.FirstOrDefault(session =>
                string.Equals(session.FolderName, detailFolder, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
            {
                OpenSession(target, addToHistory: false);
                return;
            }
        }

        if (pageType == typeof(DevicesViewModel))
        {
            ShowDevices(addToHistory: false);
            return;
        }

        if (pageType == typeof(CompareViewModel))
        {
            ShowCompare(addToHistory: false);
            return;
        }

        ShowOverview(addToHistory: false);
    }

    private SessionMetadata? GetOrCreateMetadata(Session session)
    {
        var metadata = _metadataStore.TryLoad(session.SessionKey)
                       ?? session.Metadata
                       ?? _metadataStore.TryLoadByFolderName(session.FolderName, session.StartTime);

        if (metadata is not null)
        {
            return metadata;
        }

        try
        {
            metadata = DerivedMetadataFactory.Create(
                session.SessionKey,
                session.FolderName,
                session.Device,
                session.StartTime,
                session.EndTime,
                MostCommonMode(session),
                session.Tasks.Select(task => task.Name));
            _metadataStore.Save(metadata);
            return metadata;
        }
        catch
        {
            return null;
        }
    }

    private static string? MostCommonMode(Session session)
    {
        return session.SystemSamples
            .Select(sample => sample.OperationMode)
            .Where(mode => !string.IsNullOrWhiteSpace(mode))
            .GroupBy(mode => mode, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static List<string> SplitLabels(string labelsText)
    {
        if (string.IsNullOrWhiteSpace(labelsText))
        {
            return new List<string>();
        }

        return labelsText
            .Split(new[] { ',', '，', ';', '；', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(label => label.Trim())
            .Where(label => label.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void Browse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇 Logs 資料夾",
            Multiselect = false
        };

        if (Directory.Exists(SourcePath))
        {
            dialog.InitialDirectory = SourcePath;
        }

        if (dialog.ShowDialog() == true)
        {
            SourcePath = dialog.FolderName;
            _ = ScanAsync();
        }
    }
}
