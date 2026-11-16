namespace PerformanceAnalysis.Core.Analysis;

public sealed class AnalysisOptions
{
    public int ShortSessionSeconds { get; set; } = 10;

    public int SampleGapSeconds { get; set; } = 2;

    public double CpuHighPercent { get; set; } = 90;

    public int CpuHighSustainedSeconds { get; set; } = 10;

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
        RamHighPercent = Math.Clamp(RamHighPercent, 1, 100);
        AvailableRamMinimumGb = Math.Clamp(AvailableRamMinimumGb, 0, 1024);
        MemoryGrowthMbPerMinute = Math.Clamp(MemoryGrowthMbPerMinute, 0, 1_000_000);
        MemoryGrowthMinimumSeconds = Math.Clamp(MemoryGrowthMinimumSeconds, 0, 86400);
        TaskRestartThreshold = Math.Clamp(TaskRestartThreshold, 0, 10000);
        DuplicateRatioPercent = Math.Clamp(DuplicateRatioPercent, 0, 100);
    }
}
