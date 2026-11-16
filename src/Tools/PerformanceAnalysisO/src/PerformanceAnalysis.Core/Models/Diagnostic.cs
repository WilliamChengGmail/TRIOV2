namespace PerformanceAnalysis.Core.Models;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    DateTime? From = null,
    DateTime? To = null)
{
    public override string ToString() => $"[{Severity}] {Code}: {Message}";
}
