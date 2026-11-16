using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using PerformanceAnalysis.App.Infrastructure;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class SessionDetailViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private string _annotationStatus = "修改後按儲存";
    private string _notes = string.Empty;
    private string _labelsText = string.Empty;

    public SessionDetailViewModel(MainViewModel main, Session session)
    {
        _main = main;
        Session = session;
        _notes = session.Metadata?.Notes ?? string.Empty;
        _labelsText = session.Metadata?.Labels is { Count: > 0 } labels
            ? string.Join(", ", labels)
            : string.Empty;
        _annotationStatus = !string.IsNullOrWhiteSpace(_notes) || !string.IsNullOrWhiteSpace(_labelsText)
            ? "已載入"
            : "修改後按儲存";

        TaskRows = session.Tasks
            .Select((task, index) => new TaskRowViewModel(task, index))
            .ToList();

        GanttRows = BuildGanttRows(session, TaskRows);
        Diagnostics = session.Diagnostics
            .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Info)
            .Select(diagnostic => new DiagnosticRowViewModel(diagnostic))
            .OrderByDescending(diagnostic => diagnostic.SeverityOrder)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        InfoDiagnostics = session.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Info)
            .Select(diagnostic => new DiagnosticRowViewModel(diagnostic))
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        SystemRows = session.SystemSamples
            .Select(sample => new SystemRowViewModel(sample))
            .ToList();

        BackCommand = main.BackCommand;
        OpenFolderCommand = new RelayCommand(_ => OpenFolder());
        SetBaselineCommand = new RelayCommand(_ => _ = _main.SetBaselineAsync(Session));
        SaveAnnotationsCommand = new RelayCommand(_ => _ = SaveAnnotationsAsync());
    }

    public Session Session { get; }

    public IReadOnlyList<TaskRowViewModel> TaskRows { get; }

    public IReadOnlyList<GanttRowViewModel> GanttRows { get; }

    public IReadOnlyList<DiagnosticRowViewModel> Diagnostics { get; }

    public IReadOnlyList<DiagnosticRowViewModel> InfoDiagnostics { get; }

    public IReadOnlyList<SystemRowViewModel> SystemRows { get; }

    public ICommand BackCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand SetBaselineCommand { get; }

    public ICommand SaveAnnotationsCommand { get; }

    public string Notes
    {
        get => _notes;
        set
        {
            if (SetProperty(ref _notes, value))
            {
                AnnotationStatus = "有未儲存的變更";
            }
        }
    }

    public string LabelsText
    {
        get => _labelsText;
        set
        {
            if (SetProperty(ref _labelsText, value))
            {
                AnnotationStatus = "有未儲存的變更";
            }
        }
    }

    public string AnnotationStatus
    {
        get => _annotationStatus;
        private set => SetProperty(ref _annotationStatus, value);
    }

    public bool IsBaseline => Session.Metadata?.IsBaseline == true;

    public bool IsNotBaseline => !IsBaseline;

    public string BaselineComparisonText
    {
        get
        {
            var baseline = _main.Sessions.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Device.DeviceKey,
                    Session.Device.DeviceKey,
                    StringComparison.OrdinalIgnoreCase)
                && candidate.Metadata?.IsBaseline == true);

            if (baseline is null)
            {
                return "尚未設定基準場次";
            }

            if (string.Equals(baseline.SessionKey, Session.SessionKey, StringComparison.OrdinalIgnoreCase))
            {
                return "此場次為基準";
            }

            var cpuDelta = Session.Aggregate.CpuTotal.Mean - baseline.Aggregate.CpuTotal.Mean;
            var ramDelta = Session.Aggregate.MemoryUsedGb.Mean - baseline.Aggregate.MemoryUsedGb.Mean;

            var parts = new List<string>
            {
                $"CPU {Signed(cpuDelta, "0.0", "%")}",
                $"RAM {Signed(ramDelta, "0.00", " GB")}"
            };

            if (Session.Aggregate.RecognitionMs.HasData && baseline.Aggregate.RecognitionMs.HasData)
            {
                var recognitionDelta = Session.Aggregate.RecognitionMs.Mean
                                       - baseline.Aggregate.RecognitionMs.Mean;
                parts.Add($"辨識 {Signed(recognitionDelta, "0", " ms")}");
            }

            return $"vs 基準 {baseline.FolderName}：{string.Join(" ・ ", parts)}";
        }
    }

    public string FolderName => Session.FolderName;

    public string DeviceText
    {
        get
        {
            var alias = Session.Device.Alias;
            var name = string.IsNullOrWhiteSpace(alias)
                ? Session.Device.DeviceName
                : $"{alias}（{Session.Device.DeviceName}）";

            if (Session.Device.IsIdentified)
            {
                return name;
            }

            var key = Session.Device.DeviceKey;
            var suffix = key.Length > 4 ? key[^4..] : key;
            return $"{name} ({suffix})";
        }
    }

    public string HardwareText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Session.Device.CpuModel))
            {
                parts.Add(Session.Device.CpuModel!);
            }

            if (Session.Device.Cores is int cores)
            {
                parts.Add($"{cores}C");
            }

            if (Session.Device.RamGb is double ram)
            {
                parts.Add($"{ram:0.#} GB");
            }

            if (!string.IsNullOrWhiteSpace(Session.Device.OsName))
            {
                parts.Add(Session.Device.OsName!);
            }

            if (!string.IsNullOrWhiteSpace(Session.Device.OsBuild))
            {
                parts.Add($"Build {Session.Device.OsBuild}");
            }

            return parts.Count == 0 ? "硬體資訊不完整" : string.Join(" ・ ", parts);
        }
    }

    public string FingerprintText => Session.Device.Fingerprint;

    public string TimeRangeText
        => $"{Session.StartTime:yyyy-MM-dd HH:mm:ss}  →  {Session.EndTime:HH:mm:ss}";

    public string DurationText => Formatting.Duration(Session.Duration);

    public string SampleText => Session.Aggregate.SampleCount.ToString();

    public string SampleDetailText
        => $"唯一秒 {Session.Aggregate.UniqueTimestampCount} ・ 預期 {Session.Aggregate.ExpectedSampleCount}";

    public string ModeText
    {
        get
        {
            var mode = Session.Metadata?.Mode;
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = Session.SystemSamples
                    .Select(sample => sample.OperationMode)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .Select(group => group.Key)
                    .FirstOrDefault();
            }

            return string.IsNullOrWhiteSpace(mode) ? "—" : mode!;
        }
    }

    public string TaskCountText => Session.Tasks.Count.ToString();

    public string WarningCount => (WarningCountValue).ToString();

    public string WarningDetailText
    {
        get
        {
            var warnings = Session.Diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
            var errors = Session.Diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            var infos = Session.Diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Info);
            var text = $"{warnings} 警告 ・ {errors} 錯誤";
            return infos > 0 ? $"{text} ・ {infos} 資訊" : text;
        }
    }

    public bool HasTasks => Session.Tasks.Count > 0;

    public bool HasNoTasks => !HasTasks;

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public bool HasNoDiagnostics => !HasDiagnostics;

    public bool HasInfoDiagnostics => InfoDiagnostics.Count > 0;

    public string InfoDiagnosticsHeader => $"資訊（{InfoDiagnostics.Count}）";

    public bool HasSystemSamples => Session.SystemSamples.Count > 0;

    public bool HasNoSystemSamples => !HasSystemSamples;

    public bool HasWarnings => WarningCountValue > 0;

    public string CpuAverageText => $"{Session.Aggregate.CpuTotal.Mean:0.0}%";

    public string CpuDetailText
        => $"最高 {Session.Aggregate.CpuTotal.Max:0.0}% ・ p95 {Session.Aggregate.CpuTotal.P95:0.0}%";

    public string RamAverageText => $"{Session.Aggregate.MemoryUsedGb.Mean:0.00} GB";

    public string RamDetailText
        => $"最高 {Session.Aggregate.MemoryUsedGb.Max:0.00} GB ・ 使用率 {Session.Aggregate.MemoryUsagePercent.Max:0}%";

    public string DiskPeakText
    {
        get
        {
            var peak = Session.Tasks.Count == 0
                ? 0
                : Session.Tasks.Max(task => task.Aggregate.DiskKbPerSecond.Max);
            return Formatting.Rate(peak, "KB/s");
        }
    }

    public string DiskDetailText
    {
        get
        {
            var total = Session.Tasks.Sum(task => task.Aggregate.DiskTotalMb);
            return total <= 0 ? "沒有磁碟資料" : $"總量 {total:0.0} MB";
        }
    }

    public string NetworkPeakText
    {
        get
        {
            var peak = Session.Tasks.Count == 0
                ? 0
                : Session.Tasks.Max(task => task.Aggregate.NetworkKbps.Max);
            return Formatting.Rate(peak, "Kbps");
        }
    }

    public string NetworkDetailText
    {
        get
        {
            var total = Session.Tasks.Sum(task => task.Aggregate.NetworkTotalMb);
            return total <= 0 ? "沒有網路資料" : $"總量 {total:0.0} MB";
        }
    }

    public string RecognitionText
        => Session.Aggregate.RecognitionMs.HasData
            ? $"{Session.Aggregate.RecognitionMs.Mean:0} ms"
            : "—";

    public string RecognitionDetailText
        => Session.Aggregate.RecognitionMs.HasData
            ? $"最高 {Session.Aggregate.RecognitionMs.Max:0} ms ・ p95 {Session.Aggregate.RecognitionMs.P95:0} ms"
            : "沒有辨識樣本";

    public string CompletenessText => $"{Session.Aggregate.DataCompleteness:P0}";

    public string CompletenessDetailText
        => $"樣本 {Session.Aggregate.SampleCount} / 預期 {Session.Aggregate.ExpectedSampleCount}";

    public string RawDataHeader => $"原始資料（{Session.SystemSamples.Count} 筆）";

    private int WarningCountValue
        => Session.Diagnostics.Count(diagnostic => diagnostic.Severity != DiagnosticSeverity.Info);

    private static IReadOnlyList<GanttRowViewModel> BuildGanttRows(
        Session session,
        IReadOnlyList<TaskRowViewModel> taskRows)
    {
        var rows = new List<GanttRowViewModel>(session.Tasks.Count);

        for (var index = 0; index < session.Tasks.Count; index++)
        {
            var task = session.Tasks[index];
            var row = taskRows[index];

            var bars = task.Runs
                .Select(run => new GanttBarViewModel
                {
                    StartSeconds = Math.Max(0, (run.Start - session.StartTime).TotalSeconds),
                    DurationSeconds = Math.Max(1, run.Duration.TotalSeconds),
                    Brush = row.Brush
                })
                .ToList();

            rows.Add(new GanttRowViewModel(task.Name, row.Brush, bars));
        }

        return rows;
    }

    private void OpenFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Session.FolderPath}\"")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Best effort; the status bar will not show an error for this.
        }
    }

    private async Task SaveAnnotationsAsync()
    {
        var saved = await _main.UpdateAnnotationsAsync(Session, Notes, LabelsText);
        AnnotationStatus = saved ? "已儲存" : "儲存失敗";
    }

    private static string Signed(double value, string format, string suffix)
        => value.ToString($"+{format};-{format};0", CultureInfo.InvariantCulture) + suffix;
}
