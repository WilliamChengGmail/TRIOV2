using System.ComponentModel;

namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// 單次量測場次（Session Folder）之完整解析結果與二階統計指標
/// 製作者: Office of William
/// </summary>
public sealed class BenchmarkSession : INotifyPropertyChanged
{
    private bool _isIncluded = true;
    private string? _assignedDeviceAlias;
    private SizingVerdict _verdict = new() { Level = SizingLevel.NoData };

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SessionName { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public string RelativeGroupHint { get; init; } = string.Empty;
    public DateTime MeasuredAt { get; init; }
    public TimeSpan Duration { get; init; }
    public int SampleCount { get; init; }
    public bool HasRefTxt { get; init; }
    public bool HasSystemCsv { get; init; }
    public string RefRawText { get; init; } = string.Empty;

    public DeviceProfile HardwareProfile { get; init; } = new();

    // 二階精算系統統計 (CPU & RAM)
    public double CpuAvg { get; init; }
    public double CpuMin { get; init; }
    public double CpuMax { get; init; }
    public double CpuP50 { get; init; }
    public double CpuP95 { get; init; }
    public double CpuP99 { get; init; }
    public double CpuStdDev { get; init; }
    public double CpuOver80RatioPercent { get; init; }

    public double RamAvgGb { get; init; }
    public double RamMinGb { get; init; }
    public double RamMaxGb { get; init; }
    public double RamAvgPercent { get; init; }
    public double RamPeakPercent { get; init; }
    public double RamOver85RatioPercent { get; init; }

    /// <summary>測試期間最低可用記憶體（GB）；無資料時為 NaN。記憶體判定以此為準。</summary>
    public double MinAvailableGb { get; init; } = double.NaN;

    public bool HasAvailableMemory => !double.IsNaN(MinAvailableGb);

    public string MinAvailableText => HasAvailableMemory ? $"剩 {MinAvailableGb:0.0} GB" : "RAM --";

    // 辨識運算事件統計
    public int RecognitionEventCount { get; init; }
    public double AvgRecognitionDurationMs { get; init; }
    public double MaxRecognitionDurationMs { get; init; }

    public IReadOnlyList<SystemSamplePoint> SystemSamples { get; init; } = Array.Empty<SystemSamplePoint>();
    public IReadOnlyList<TaskBenchmarkSummary> Tasks { get; init; } = Array.Empty<TaskBenchmarkSummary>();

    public string EffectiveDeviceKey => !string.IsNullOrWhiteSpace(AssignedDeviceAlias)
        ? $"CUSTOM|{AssignedDeviceAlias.Trim()}"
        : HardwareProfile.ComputeDeviceKey();

    public string EffectiveDeviceDisplayName => !string.IsNullOrWhiteSpace(AssignedDeviceAlias)
        ? AssignedDeviceAlias.Trim()
        : HardwareProfile.HasRefTxt
            ? (!string.Equals(HardwareProfile.MachineName, "未知設備", StringComparison.OrdinalIgnoreCase)
                ? HardwareProfile.MachineName
                : (!string.Equals(HardwareProfile.CpuModel, "未知 CPU", StringComparison.OrdinalIgnoreCase)
                    ? $"未命名設備 ({HardwareProfile.CpuModel})"
                    : $"舊版摘要紀錄 ({HardwareProfile.MemoryInstalledGb:0.#} GB)"))
            : $"未標記硬體設備 ({HardwareProfile.MemoryInstalledGb:0.#} GB)";

    public string DurationText => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours:00}:{Duration.Minutes:00}:{Duration.Seconds:00}"
        : $"{Duration.Minutes:00}:{Duration.Seconds:00}";

    public string TimeAndDurationDisplay => $"{MeasuredAt:yyyy-MM-dd HH:mm} ({DurationText})";

    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (_isIncluded != value)
            {
                _isIncluded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded)));
            }
        }
    }

    public string? AssignedDeviceAlias
    {
        get => _assignedDeviceAlias;
        set
        {
            if (!string.Equals(_assignedDeviceAlias, value, StringComparison.Ordinal))
            {
                _assignedDeviceAlias = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AssignedDeviceAlias)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveDeviceKey)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveDeviceDisplayName)));
            }
        }
    }

    public SizingVerdict Verdict
    {
        get => _verdict;
        set
        {
            _verdict = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Verdict)));
        }
    }
}
