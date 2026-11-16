using PerformanceAnalysis.Core.Caching;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Tests;

public sealed class SessionCacheTests
{
    [Fact]
    public void FingerprintChangesWhenSourceFileChanges()
    {
        using var workspace = new TempWorkspace();
        var file = workspace.WriteFile("session/System_BenchmarkLog.csv", "a,b\n1,2\n");
        var candidate = new SessionCandidate(workspace.PathFor("session"), null);

        var first = SessionCache.ComputeFingerprint(candidate);

        File.WriteAllText(file, "a,b\n1,2\n3,4\n");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(1));

        var second = SessionCache.ComputeFingerprint(candidate);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void FingerprintIsStableWhenNothingChanges()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("session/System_BenchmarkLog.csv", "a,b\n1,2\n");
        workspace.WriteFile("session/ref.txt", "test");
        var candidate = new SessionCandidate(workspace.PathFor("session"), null);

        Assert.Equal(
            SessionCache.ComputeFingerprint(candidate),
            SessionCache.ComputeFingerprint(candidate));
    }
}
