namespace PerformanceAnalysis.Core.Analysis;

public sealed class AnalysisOptions
{
    public int ShortSessionSeconds { get; set; } = 10;

    public int SampleGapSeconds { get; set; } = 2;

    public double CpuHighPercent { get; set; } = 90;

    public int CpuHighSustainedSeconds { get; set; } = 10;

    /// <summary>CPU P95 達此值列為「注意」（與 PerformanceAnalyzer 的安全上限一致）。</summary>
    public double CpuP95AttentionPercent { get; set; } = 70;

    /// <summary>CPU P95 超過此值列為「異常」（與 PerformanceAnalyzer 的升級線一致）。</summary>
    public double CpuP95ProblemPercent { get; set; } = 85;

    public double RamHighPercent { get; set; } = 90;

    public double AvailableRamMinimumGb { get; set; } = 1;

    public double MemoryGrowthMbPerMinute { get; set; } = 20;

    public int MemoryGrowthMinimumSeconds { get; set; } = 300;

    public int TaskRestartThreshold { get; set; } = 3;

    public double DuplicateRatioPercent { get; set; } = 1;

    public static AnalysisOptions Default => new();

    public AnalysisOptions Clone()
        => new()
        {
            ShortSessionSeconds = ShortSessionSeconds,
            SampleGapSeconds = SampleGapSeconds,
            CpuHighPercent = CpuHighPercent,
            CpuHighSustainedSeconds = CpuHighSustainedSeconds,
            CpuP95AttentionPercent = CpuP95AttentionPercent,
            CpuP95ProblemPercent = CpuP95ProblemPercent,
            RamHighPercent = RamHighPercent,
            AvailableRamMinimumGb = AvailableRamMinimumGb,
            MemoryGrowthMbPerMinute = MemoryGrowthMbPerMinute,
            MemoryGrowthMinimumSeconds = MemoryGrowthMinimumSeconds,
            TaskRestartThreshold = TaskRestartThreshold,
            DuplicateRatioPercent = DuplicateRatioPercent
        };

    public void Normalize()
    {
        ShortSessionSeconds = Math.Clamp(ShortSessionSeconds, 0, 86400);
        SampleGapSeconds = Math.Clamp(SampleGapSeconds, 0, 3600);
        CpuHighPercent = Math.Clamp(CpuHighPercent, 1, 100);
        CpuHighSustainedSeconds = Math.Clamp(CpuHighSustainedSeconds, 1, 86400);
        CpuP95AttentionPercent = Math.Clamp(CpuP95AttentionPercent, 1, 100);
        CpuP95ProblemPercent = Math.Clamp(CpuP95ProblemPercent, CpuP95AttentionPercent, 100);
        RamHighPercent = Math.Clamp(RamHighPercent, 1, 100);
        AvailableRamMinimumGb = Math.Clamp(AvailableRamMinimumGb, 0, 1024);
        MemoryGrowthMbPerMinute = Math.Clamp(MemoryGrowthMbPerMinute, 0, 1_000_000);
        MemoryGrowthMinimumSeconds = Math.Clamp(MemoryGrowthMinimumSeconds, 0, 86400);
        TaskRestartThreshold = Math.Clamp(TaskRestartThreshold, 0, 10000);
        DuplicateRatioPercent = Math.Clamp(DuplicateRatioPercent, 0, 100);
    }
}
