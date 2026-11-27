using System.Windows.Media;
using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

/// <summary>單場次「結論燈號」的顯示資料，供總覽列表與單場次頁共用。</summary>
public sealed class HealthViewModel
{
    private static readonly Brush GoodBrush = Freeze(Color.FromRgb(0x66, 0xBB, 0x6A));
    private static readonly Brush AttentionBrush = Freeze(Color.FromRgb(0xFF, 0xCA, 0x28));
    private static readonly Brush ProblemBrush = Freeze(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly Brush InsufficientBrush = Freeze(Color.FromRgb(0x7B, 0x8F, 0xA8));

    public HealthViewModel(Session session)
    {
        Health = HealthAssessor.Assess(session);
        Findings = Health.Findings.Select(finding => new FindingViewModel(finding)).ToList();
    }

    public SessionHealth Health { get; }

    public HealthLevel Level => Health.Level;

    public int SortOrder => Level switch
    {
        HealthLevel.Problem => 0,
        HealthLevel.Attention => 1,
        HealthLevel.Good => 2,
        _ => 3
    };

    public string LevelText => Health.LevelText;

    public string Headline => Health.Headline;

    public string ConfidenceText => $"資料可信度：{Health.ConfidenceText}";

    public Brush Brush => BrushFor(Level);

    /// <summary>燈號底色上的文字顏色（黃底用深色字）。</summary>
    public Brush ForegroundBrush => Level == HealthLevel.Attention ? Brushes.Black : Brushes.White;

    public IReadOnlyList<FindingViewModel> Findings { get; }

    public bool HasFindings => Findings.Count > 0;

    public static Brush BrushFor(HealthLevel level) => level switch
    {
        HealthLevel.Good => GoodBrush,
        HealthLevel.Attention => AttentionBrush,
        HealthLevel.Problem => ProblemBrush,
        _ => InsufficientBrush
    };

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

public sealed class FindingViewModel
{
    public FindingViewModel(HealthFinding finding)
    {
        Finding = finding;
    }

    public HealthFinding Finding { get; }

    public string CategoryText => HealthAssessor.CategoryText(Finding.Category);

    public string Title => Finding.Title;

    public string Explanation => Finding.Explanation;

    public string SuggestionText => $"建議：{Finding.Suggestion}";

    public string DetailText => $"{Finding.Code} ・ {Finding.Detail}";

    public Brush Brush => Finding.Impact switch
    {
        FindingImpact.Problem => HealthViewModel.BrushFor(HealthLevel.Problem),
        FindingImpact.Attention => HealthViewModel.BrushFor(HealthLevel.Attention),
        _ => HealthViewModel.BrushFor(HealthLevel.Insufficient)
    };
}
