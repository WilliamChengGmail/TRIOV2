namespace PerformanceAnalysis.Core.Parsing;

public sealed record CsvRow(int LineNumber, IReadOnlyList<string> Fields);

public sealed class CsvTable
{
    public required IReadOnlyList<string> Headers { get; init; }

    public required IReadOnlyList<CsvRow> Rows { get; init; }

    public int CommentLineCount { get; init; }

    public int MalformedRowCount { get; init; }
}
