using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Tests;

public sealed class SessionScannerTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void FindsFlatAndNestedSessions()
    {
        WriteSession("20260101_120000");
        WriteSession("LAB-PC-01/20260102_120000");
        _workspace.WriteFile("empty/readme.txt", "not a session");

        var result = SessionScanner.Scan(_workspace.Root);

        Assert.Equal(2, result.Candidates.Count);
        var nested = Assert.Single(result.Candidates, candidate => candidate.Path.Contains("LAB-PC-01"));
        Assert.Equal("LAB-PC-01", nested.DeviceFolderHint);
        var flat = Assert.Single(result.Candidates, candidate => candidate.Path.EndsWith("20260101_120000"));
        Assert.Null(flat.DeviceFolderHint);
    }

    [Fact]
    public void SingleSessionPathReturnsOneCandidate()
    {
        var folder = WriteSession("20260101_120000");

        var result = SessionScanner.Scan(folder);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(folder, candidate.Path);
        Assert.Null(candidate.DeviceFolderHint);
    }

    [Fact]
    public void MissingPathReturnsError()
    {
        var result = SessionScanner.Scan(Path.Combine(_workspace.Root, "does-not-exist"));

        Assert.Empty(result.Candidates);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PATH_NOT_FOUND");
    }

    private string WriteSession(string relativePath)
    {
        var folder = _workspace.PathFor(relativePath);
        Directory.CreateDirectory(folder);
        _workspace.WriteFile(
            $"{relativePath}/System_BenchmarkLog.csv",
            "樣本序號,時間戳記\n1,2026-01-01 12:00:00\n");
        return folder;
    }

    public void Dispose() => _workspace.Dispose();
}
