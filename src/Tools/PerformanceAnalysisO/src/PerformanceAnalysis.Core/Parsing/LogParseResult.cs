using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Parsing;

public sealed record LogParseResult<T>(
    IReadOnlyList<T> Items,
    IReadOnlyList<Diagnostic> Diagnostics,
    int InvalidRowCount)
{
    public static LogParseResult<T> Empty(params Diagnostic[] diagnostics)
        => new(Array.Empty<T>(), diagnostics, 0);
}
