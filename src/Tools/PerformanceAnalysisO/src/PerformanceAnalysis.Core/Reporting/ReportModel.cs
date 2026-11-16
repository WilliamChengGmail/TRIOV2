namespace PerformanceAnalysis.Core.Reporting;

public sealed class ReportModel
{
    public int SchemaVersion { get; init; } = 1;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

    public string SourcePath { get; init; } = string.Empty;

    public string Title { get; init; } = "TRIOV2 效能報告";

    public IReadOnlyList<ReportDevice> Devices { get; init; } = Array.Empty<ReportDevice>();

    public IReadOnlyList<ReportSession> Sessions { get; init; } = Array.Empty<ReportSession>();

    public IReadOnlyList<string> TaskCatalog { get; init; } = Array.Empty<string>();
}

public sealed class ReportDevice
{
    public string DeviceKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string? Alias { get; init; }

    public string? CpuModel { get; init; }

    public int? Cores { get; init; }

    public int? LogicalCpus { get; init; }

    public double? RamGb { get; init; }

    public string? RamSpeed { get; init; }

    public string? OsName { get; init; }

    public string? OsVersion { get; init; }

    public string? OsBuild { get; init; }

    public string? Gpu { get; init; }

    public string Fingerprint { get; init; } = string.Empty;

    public bool IsIdentified { get; init; }

    public string? BaselineSessionKey { get; init; }

    public int SessionCount { get; init; }
}

public sealed class ReportSession
{
    public string SessionKey { get; init; } = string.Empty;

    public string FolderName { get; init; } = string.Empty;

    public string DeviceKey { get; init; } = string.Empty;

    public string DeviceDisplay { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public double DurationSeconds { get; init; }

    public int SampleCount { get; init; }

    public int ExpectedSampleCount { get; init; }

    public bool HasRefTxt { get; init; }

    public bool HasSystemLog { get; init; }

    public string? Mode { get; init; }

    public bool IsBaseline { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();

    public ReportSessionMetrics Metrics { get; init; } = new();

    public IReadOnlyList<ReportSeries> Series { get; init; } = Array.Empty<ReportSeries>();

    public IReadOnlyList<ReportTask> Tasks { get; init; } = Array.Empty<ReportTask>();

    public IReadOnlyList<ReportDiagnostic> Diagnostics { get; init; } = Array.Empty<ReportDiagnostic>();

    public int WarningCount { get; init; }

    public int ErrorCount { get; init; }
}

public sealed class ReportSessionMetrics
{
    public double CpuAvg { get; init; }

    public double CpuMin { get; init; }

    public double CpuMax { get; init; }

    public double CpuP95 { get; init; }

    public double CpuSimAvg { get; init; }

    public double CpuSimMax { get; init; }

    public double RamAvg { get; init; }

    public double RamMin { get; init; }

    public double RamMax { get; init; }

    public double RamPercentMax { get; init; }

    public double FrameBufferMax { get; init; }

    public double RecognitionAvg { get; init; }

    public double RecognitionMax { get; init; }

    public double DiskPeak { get; init; }

    public double NetworkPeak { get; init; }

    public double Completeness { get; init; }

    public int MaxGapSeconds { get; init; }
}

public sealed class ReportSeries
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;

    /// <summary>Each point is [elapsedSeconds, value].</summary>
    public IReadOnlyList<double[]> Points { get; init; } = Array.Empty<double[]>();
}

public sealed class ReportTask
{
    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;

    public double ActiveSeconds { get; init; }

    public double ActiveRatio { get; init; }

    public double CpuAvg { get; init; }

    public double CpuMax { get; init; }

    public double CpuP95 { get; init; }

    public double MemoryAvg { get; init; }

    public double MemoryMax { get; init; }

    public double CpuSeconds { get; init; }

    public double MemoryGrowth { get; init; }

    public double DiskPeak { get; init; }

    public double NetworkPeak { get; init; }

    public int RunCount { get; init; }

    public int RestartCount { get; init; }

    public IReadOnlyList<ReportRun> Runs { get; init; } = Array.Empty<ReportRun>();

    public IReadOnlyList<double[]> CpuPoints { get; init; } = Array.Empty<double[]>();

    public IReadOnlyList<double[]> MemoryPoints { get; init; } = Array.Empty<double[]>();
}

public sealed class ReportRun
{
    public double StartSeconds { get; init; }

    public double DurationSeconds { get; init; }
}

public sealed class ReportDiagnostic
{
    public string Severity { get; init; } = "Info";

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
