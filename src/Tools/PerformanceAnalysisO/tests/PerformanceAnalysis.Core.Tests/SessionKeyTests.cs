using PerformanceAnalysis.Core.Metadata;

namespace PerformanceAnalysis.Core.Tests;

public sealed class SessionKeyTests
{
    [Fact]
    public void ComputeIsStableAndPathIndependent()
    {
        var first = new DateTime(2026, 11, 12, 15, 7, 52);
        var last = new DateTime(2026, 11, 12, 15, 18, 16);

        var key1 = SessionKey.Compute(first, last, 625, 123456);
        var key2 = SessionKey.Compute(first, last, 625, 123456);

        Assert.Equal(key1, key2);
        Assert.Matches("^[a-f0-9]{16}$", key1);
    }

    [Fact]
    public void ComputeChangesWhenContentChanges()
    {
        var first = new DateTime(2026, 11, 12, 15, 7, 52);
        var last = new DateTime(2026, 11, 12, 15, 18, 16);

        Assert.NotEqual(
            SessionKey.Compute(first, last, 625, 123456),
            SessionKey.Compute(first, last, 626, 123456));
    }
}
