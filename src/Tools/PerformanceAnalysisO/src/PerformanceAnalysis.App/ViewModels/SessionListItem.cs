using System.Globalization;
using System.Windows.Media;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class SessionListItem
{
    private static readonly Brush PositiveBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)));
    private static readonly Brush NegativeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)));
    private static readonly Brush NeutralBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x7B, 0x8F, 0xA8)));

    private readonly Session? _baseline;

    public SessionListItem(Session session, Session? baseline = null)
    {
        Session = session;
        _baseline = baseline;
    }

    public Session Session { get; }

    public string FolderName => Session.FolderName;

    public string DeviceDisplay
    {
        get
        {
            var name = Session.Device.DisplayName;
            if (Session.Device.IsIdentified)
            {
                return name;
            }

            var key = Session.Device.DeviceKey;
            var suffix = key.Length > 4 ? key[^4..] : key;
            return $"{name} ({suffix})";
        }
    }

    public string DeviceKey => Session.Device.DeviceKey;

    public DateTime StartTime => Session.StartTime;

    public string StartTimeText => Session.StartTime.ToString("MM-dd HH:mm:ss");

    public string DurationText => Formatting.Duration(Session.Duration);

    public int SampleCount => Session.Aggregate.SampleCount;

    public string SampleCountText => SampleCount.ToString();

    public double CpuAverage => Session.Aggregate.CpuTotal.Mean;

    public double CpuMax => Session.Aggregate.CpuTotal.Max;

    public double RamAverage => Session.Aggregate.MemoryUsedGb.Mean;

    public double RamMax => Session.Aggregate.MemoryUsedGb.Max;

    public double RecognitionAverage => Session.Aggregate.RecognitionMs.Mean;

    public double RecognitionMax => Session.Aggregate.RecognitionMs.Max;

    public double Completeness => Session.Aggregate.DataCompleteness;

    public string CpuAverageText => Formatting.Number(CpuAverage);

    public string CpuMaxText => Formatting.Number(CpuMax);

    public string RamAverageText => Formatting.Number(RamAverage, "0.00");

    public string RamMaxText => Formatting.Number(RamMax, "0.00");

    public string RecognitionText
        => Session.Aggregate.RecognitionMs.HasData ? $"{RecognitionAverage:0} ms" : "—";

    public string CompletenessText => $"{Completeness:P0}";

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

    public int TaskCount => Session.Tasks.Count;

    public int WarningCount => Session.Diagnostics.Count(diagnostic => diagnostic.Severity != DiagnosticSeverity.Info);

    public string WarningText => WarningCount == 0 ? "—" : WarningCount.ToString();

    public bool HasWarnings => WarningCount > 0;

    public bool IsBaseline => Session.Metadata?.IsBaseline == true;

    public bool IsCompareBaseline => _baseline is not null && ReferenceEquals(_baseline, Session);

    public string BaselineMarker => IsBaseline ? "★" : string.Empty;

    public string CompareBaselineMarker => IsCompareBaseline ? "★" : string.Empty;

    public string BaselineText => IsBaseline ? "基準" : "—";

    public string LabelsText => Session.Metadata?.Labels is { Count: > 0 } labels
        ? string.Join(", ", labels)
        : "—";

    public bool HasBaseline => _baseline is not null && !ReferenceEquals(_baseline, Session);

    public string CpuDeltaText => IsCompareBaseline ? "基準" : FormatDelta(CpuDelta, "0.0", "%");

    public Brush CpuDeltaBrush => IsCompareBaseline ? NeutralBrush : DeltaBrush(CpuDelta);

    public string RamDeltaText => IsCompareBaseline ? "基準" : FormatDelta(RamDelta, "0.00", " GB");

    public Brush RamDeltaBrush => IsCompareBaseline ? NeutralBrush : DeltaBrush(RamDelta);

    public string RecognitionDeltaText
        => IsCompareBaseline ? "基準" : FormatDelta(RecognitionDelta, "0", " ms");

    public Brush RecognitionDeltaBrush
        => IsCompareBaseline ? NeutralBrush : DeltaBrush(RecognitionDelta);

    public string CompletenessDeltaText
        => IsCompareBaseline ? "基準" : FormatDelta(CompletenessDelta * 100, "0.0", "%");

    public Brush CompletenessDeltaBrush
        => IsCompareBaseline ? NeutralBrush : DeltaBrush(CompletenessDelta, lowerIsBetter: false);

    private double? CpuDelta => HasBaseline ? CpuAverage - _baseline!.Aggregate.CpuTotal.Mean : null;

    private double? RamDelta => HasBaseline ? RamAverage - _baseline!.Aggregate.MemoryUsedGb.Mean : null;

    private double? RecognitionDelta
        => HasBaseline
           && Session.Aggregate.RecognitionMs.HasData
           && _baseline!.Aggregate.RecognitionMs.HasData
            ? RecognitionAverage - _baseline.Aggregate.RecognitionMs.Mean
            : null;

    private double? CompletenessDelta
        => HasBaseline ? Completeness - _baseline!.Aggregate.DataCompleteness : null;

    private static string FormatDelta(double? delta, string format, string suffix)
    {
        if (delta is null)
        {
            return "—";
        }

        var value = delta.Value;
        if (Math.Abs(value) < 0.0005)
        {
            return $"0{suffix}";
        }

        return value.ToString($"+{format};-{format};0", CultureInfo.InvariantCulture) + suffix;
    }

    private static Brush DeltaBrush(double? delta, bool lowerIsBetter = true)
    {
        if (delta is null || Math.Abs(delta.Value) < 0.0005)
        {
            return NeutralBrush;
        }

        var improved = lowerIsBetter ? delta.Value < 0 : delta.Value > 0;
        return improved ? NegativeBrush : PositiveBrush;
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
