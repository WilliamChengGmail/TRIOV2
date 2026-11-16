using System.Globalization;
using System.Windows.Input;
using PerformanceAnalysis.App.Infrastructure;
using PerformanceAnalysis.Core.Analysis;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private string _cpuHighPercentText = string.Empty;
    private string _cpuHighSustainedSecondsText = string.Empty;
    private string _ramHighPercentText = string.Empty;
    private string _availableRamMinimumGbText = string.Empty;
    private string _memoryGrowthMbPerMinuteText = string.Empty;
    private string _memoryGrowthMinimumSecondsText = string.Empty;
    private string _shortSessionSecondsText = string.Empty;
    private string _sampleGapSecondsText = string.Empty;
    private string _duplicateRatioPercentText = string.Empty;
    private string _taskRestartThresholdText = string.Empty;
    private string _statusText = "修改後按「儲存並重新分析」。";

    public SettingsViewModel(MainViewModel main, AnalysisOptions options)
    {
        _main = main;
        Load(options);

        SaveCommand = new RelayCommand(_ => _ = SaveAsync());
        ResetCommand = new RelayCommand(_ =>
        {
            Load(AnalysisOptions.Default);
            StatusText = "已載入預設值，尚未儲存。";
        });
        ClearCacheCommand = new RelayCommand(_ =>
        {
            _main.ClearSessionCache();
            OnPropertyChanged(nameof(CacheCountText));
            StatusText = "已清除解析快取，下次掃描會重新解析。";
        });

        BackCommand = main.BackCommand;
        HomeCommand = main.HomeCommand;
    }

    public string CpuHighPercentText
    {
        get => _cpuHighPercentText;
        set => SetProperty(ref _cpuHighPercentText, value);
    }

    public string CpuHighSustainedSecondsText
    {
        get => _cpuHighSustainedSecondsText;
        set => SetProperty(ref _cpuHighSustainedSecondsText, value);
    }

    public string RamHighPercentText
    {
        get => _ramHighPercentText;
        set => SetProperty(ref _ramHighPercentText, value);
    }

    public string AvailableRamMinimumGbText
    {
        get => _availableRamMinimumGbText;
        set => SetProperty(ref _availableRamMinimumGbText, value);
    }

    public string MemoryGrowthMbPerMinuteText
    {
        get => _memoryGrowthMbPerMinuteText;
        set => SetProperty(ref _memoryGrowthMbPerMinuteText, value);
    }

    public string MemoryGrowthMinimumSecondsText
    {
        get => _memoryGrowthMinimumSecondsText;
        set => SetProperty(ref _memoryGrowthMinimumSecondsText, value);
    }

    public string ShortSessionSecondsText
    {
        get => _shortSessionSecondsText;
        set => SetProperty(ref _shortSessionSecondsText, value);
    }

    public string SampleGapSecondsText
    {
        get => _sampleGapSecondsText;
        set => SetProperty(ref _sampleGapSecondsText, value);
    }

    public string DuplicateRatioPercentText
    {
        get => _duplicateRatioPercentText;
        set => SetProperty(ref _duplicateRatioPercentText, value);
    }

    public string TaskRestartThresholdText
    {
        get => _taskRestartThresholdText;
        set => SetProperty(ref _taskRestartThresholdText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string CacheCountText => _main.CacheCountText;

    public void SetStatus(string status) => StatusText = status;

    public ICommand SaveCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand ClearCacheCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand HomeCommand { get; }

    private void Load(AnalysisOptions options)
    {
        CpuHighPercentText = options.CpuHighPercent.ToString("0.##", CultureInfo.InvariantCulture);
        CpuHighSustainedSecondsText = options.CpuHighSustainedSeconds.ToString(CultureInfo.InvariantCulture);
        RamHighPercentText = options.RamHighPercent.ToString("0.##", CultureInfo.InvariantCulture);
        AvailableRamMinimumGbText = options.AvailableRamMinimumGb.ToString("0.##", CultureInfo.InvariantCulture);
        MemoryGrowthMbPerMinuteText = options.MemoryGrowthMbPerMinute.ToString("0.##", CultureInfo.InvariantCulture);
        MemoryGrowthMinimumSecondsText = options.MemoryGrowthMinimumSeconds.ToString(CultureInfo.InvariantCulture);
        ShortSessionSecondsText = options.ShortSessionSeconds.ToString(CultureInfo.InvariantCulture);
        SampleGapSecondsText = options.SampleGapSeconds.ToString(CultureInfo.InvariantCulture);
        DuplicateRatioPercentText = options.DuplicateRatioPercent.ToString("0.##", CultureInfo.InvariantCulture);
        TaskRestartThresholdText = options.TaskRestartThreshold.ToString(CultureInfo.InvariantCulture);
    }

    private async Task SaveAsync()
    {
        if (!TryBuildOptions(out var options, out var error))
        {
            StatusText = error;
            return;
        }

        await _main.ApplySettingsAsync(options);
        Load(options);
        OnPropertyChanged(nameof(CacheCountText));
        StatusText = "設定已儲存，已重新分析。";
    }

    private bool TryBuildOptions(out AnalysisOptions options, out string error)
    {
        options = new AnalysisOptions();

        if (!TryDouble(CpuHighPercentText, out var cpuHigh) || cpuHigh is < 1 or > 100)
        {
            error = "CPU 門檻必須是 1–100 的數字。";
            return false;
        }

        if (!TryInt(CpuHighSustainedSecondsText, out var cpuSeconds) || cpuSeconds is < 1 or > 86400)
        {
            error = "CPU 持續秒數必須是 1–86400 的整數。";
            return false;
        }

        if (!TryDouble(RamHighPercentText, out var ramHigh) || ramHigh is < 1 or > 100)
        {
            error = "記憶體使用率門檻必須是 1–100 的數字。";
            return false;
        }

        if (!TryDouble(AvailableRamMinimumGbText, out var availableRam) || availableRam is < 0 or > 1024)
        {
            error = "可用記憶體門檻必須是 0–1024 的數字。";
            return false;
        }

        if (!TryDouble(MemoryGrowthMbPerMinuteText, out var growth) || growth < 0)
        {
            error = "記憶體成長門檻必須大於或等於 0。";
            return false;
        }

        if (!TryInt(MemoryGrowthMinimumSecondsText, out var growthSeconds) || growthSeconds is < 0 or > 86400)
        {
            error = "記憶體成長觀察秒數必須是 0–86400 的整數。";
            return false;
        }

        if (!TryInt(ShortSessionSecondsText, out var shortSeconds) || shortSeconds is < 0 or > 86400)
        {
            error = "短場次門檻必須是 0–86400 的整數。";
            return false;
        }

        if (!TryInt(SampleGapSecondsText, out var gapSeconds) || gapSeconds is < 0 or > 3600)
        {
            error = "跳秒門檻必須是 0–3600 的整數。";
            return false;
        }

        if (!TryDouble(DuplicateRatioPercentText, out var duplicatePercent) || duplicatePercent is < 0 or > 100)
        {
            error = "重複樣本比例必須是 0–100 的數字。";
            return false;
        }

        if (!TryInt(TaskRestartThresholdText, out var restartThreshold) || restartThreshold is < 0 or > 10000)
        {
            error = "任務重啟門檻必須是 0–10000 的整數。";
            return false;
        }

        options.CpuHighPercent = cpuHigh;
        options.CpuHighSustainedSeconds = cpuSeconds;
        options.RamHighPercent = ramHigh;
        options.AvailableRamMinimumGb = availableRam;
        options.MemoryGrowthMbPerMinute = growth;
        options.MemoryGrowthMinimumSeconds = growthSeconds;
        options.ShortSessionSeconds = shortSeconds;
        options.SampleGapSeconds = gapSeconds;
        options.DuplicateRatioPercent = duplicatePercent;
        options.TaskRestartThreshold = restartThreshold;
        options.Normalize();

        error = string.Empty;
        return true;
    }

    private static bool TryDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static bool TryInt(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               || int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
    }
}
