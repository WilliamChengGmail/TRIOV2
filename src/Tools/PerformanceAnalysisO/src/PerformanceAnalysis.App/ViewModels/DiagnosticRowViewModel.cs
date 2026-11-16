using System.Windows.Media;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class DiagnosticRowViewModel
{
    private static readonly Brush ErrorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)));
    private static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xCA, 0x28)));
    private static readonly Brush InfoBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)));

    public DiagnosticRowViewModel(Diagnostic diagnostic)
    {
        Diagnostic = diagnostic;
    }

    public Diagnostic Diagnostic { get; }

    public DiagnosticSeverity Severity => Diagnostic.Severity;

    public int SeverityOrder => Severity switch
    {
        DiagnosticSeverity.Error => 2,
        DiagnosticSeverity.Warning => 1,
        _ => 0
    };

    public string SeverityText => Severity switch
    {
        DiagnosticSeverity.Error => "錯誤",
        DiagnosticSeverity.Warning => "警告",
        _ => "資訊"
    };

    public Brush SeverityBrush => Severity switch
    {
        DiagnosticSeverity.Error => ErrorBrush,
        DiagnosticSeverity.Warning => WarningBrush,
        _ => InfoBrush
    };

    public string Code => Diagnostic.Code;

    public string Message => Diagnostic.Message;

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
