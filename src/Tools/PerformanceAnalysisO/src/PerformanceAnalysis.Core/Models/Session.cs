namespace PerformanceAnalysis.Core.Models;

public sealed class Session
{
    public required string SessionKey { get; init; }

    public required string FolderPath { get; init; }

    public required string FolderName { get; init; }

    public required DateTime StartTime { get; init; }

    public required DateTime EndTime { get; init; }

    public required TimeSpan Duration { get; init; }

    public required IReadOnlyList<SystemSample> SystemSamples { get; init; }

    public required IReadOnlyList<TaskLog> Tasks { get; init; }

    public required DeviceProfile Device { get; init; }

    public required SessionAggregate Aggregate { get; init; }

    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }

    public SessionMetadata? Metadata { get; init; }

    public bool HasRefTxt { get; init; }

    public bool HasSystemLog { get; init; }

    public string? DeviceFolderHint { get; init; }
}
