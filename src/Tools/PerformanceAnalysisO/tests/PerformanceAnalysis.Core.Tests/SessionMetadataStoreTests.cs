using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Tests;

public sealed class SessionMetadataStoreTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void FolderFallbackUsesStartTimeWhenNamesCollide()
    {
        var store = new SessionMetadataStore(Path.Combine(_workspace.Root, "Metadata"));
        var firstStart = new DateTime(2026, 1, 1, 12, 0, 0);
        var secondStart = new DateTime(2026, 1, 2, 12, 0, 0);

        store.Save(new SessionMetadata
        {
            SessionKey = "aaaaaaaaaaaaaaaa",
            SourceFolderName = "20260101_120000",
            DeviceName = "DEVICE-A",
            StartTime = new DateTimeOffset(firstStart, TimeSpan.FromHours(8))
        });
        store.Save(new SessionMetadata
        {
            SessionKey = "bbbbbbbbbbbbbbbb",
            SourceFolderName = "20260101_120000",
            DeviceName = "DEVICE-B",
            StartTime = new DateTimeOffset(secondStart, TimeSpan.FromHours(8))
        });

        Assert.Equal("DEVICE-A", store.TryLoadByFolderName("20260101_120000", firstStart)!.DeviceName);
        Assert.Equal("DEVICE-B", store.TryLoadByFolderName("20260101_120000", secondStart)!.DeviceName);
        Assert.Null(store.TryLoadByFolderName("20260101_120000"));
    }

    public void Dispose() => _workspace.Dispose();
}
