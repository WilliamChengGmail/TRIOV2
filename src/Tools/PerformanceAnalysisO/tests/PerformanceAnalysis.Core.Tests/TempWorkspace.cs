using System.Text;

namespace PerformanceAnalysis.Core.Tests;

internal sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "PerformanceAnalysisTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathFor(string relativePath)
        => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public string WriteFile(string relativePath, string content, Encoding? encoding = null)
    {
        var path = PathFor(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
