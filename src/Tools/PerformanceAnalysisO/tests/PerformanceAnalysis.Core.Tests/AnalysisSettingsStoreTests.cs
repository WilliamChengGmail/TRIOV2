using PerformanceAnalysis.Core.Configuration;

namespace PerformanceAnalysis.Core.Tests;

public sealed class AnalysisSettingsStoreTests
{
    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        using var workspace = new TempWorkspace();
        var store = new AnalysisSettingsStore(workspace.Root);
        var options = new PerformanceAnalysis.Core.Analysis.AnalysisOptions
        {
            CpuHighPercent = 80,
            CpuHighSustainedSeconds = 5,
            RamHighPercent = 85,
            AvailableRamMinimumGb = 2,
            MemoryGrowthMbPerMinute = 10,
            MemoryGrowthMinimumSeconds = 60,
            ShortSessionSeconds = 5,
            SampleGapSeconds = 3,
            DuplicateRatioPercent = 0.5,
            TaskRestartThreshold = 1
        };

        store.Save(options);
        var loaded = store.Load();

        Assert.Equal(80, loaded.CpuHighPercent);
        Assert.Equal(5, loaded.CpuHighSustainedSeconds);
        Assert.Equal(85, loaded.RamHighPercent);
        Assert.Equal(2, loaded.AvailableRamMinimumGb);
        Assert.Equal(10, loaded.MemoryGrowthMbPerMinute);
        Assert.Equal(60, loaded.MemoryGrowthMinimumSeconds);
        Assert.Equal(5, loaded.ShortSessionSeconds);
        Assert.Equal(3, loaded.SampleGapSeconds);
        Assert.Equal(0.5, loaded.DuplicateRatioPercent);
        Assert.Equal(1, loaded.TaskRestartThreshold);
        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public void MissingFileReturnsDefaults()
    {
        using var workspace = new TempWorkspace();
        var store = new AnalysisSettingsStore(workspace.Root);

        var loaded = store.Load();

        Assert.Equal(90, loaded.CpuHighPercent);
        Assert.Equal(10, loaded.CpuHighSustainedSeconds);
        Assert.Equal(300, loaded.MemoryGrowthMinimumSeconds);
    }

    [Fact]
    public void InvalidFileReturnsDefaults()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("settings.json", "{ not json");
        var store = new AnalysisSettingsStore(workspace.Root);

        var loaded = store.Load();

        Assert.Equal(90, loaded.CpuHighPercent);
    }
}
