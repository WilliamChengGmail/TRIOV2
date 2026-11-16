namespace PerformanceAnalysis.Core.Models;

public sealed class TaskLog
{
    public required string Name { get; init; }

    public required string FilePath { get; init; }

    public required IReadOnlyList<TaskSample> Samples { get; init; }

    public required IReadOnlyList<TaskRun> Runs { get; init; }

    public required TaskAggregate Aggregate { get; init; }

    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }
}
