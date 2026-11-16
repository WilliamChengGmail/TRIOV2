using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Scanning;

public sealed record SessionCandidate(string Path, string? DeviceFolderHint);

public sealed record ScanResult(
    string RootPath,
    IReadOnlyList<SessionCandidate> Candidates,
    IReadOnlyList<Diagnostic> Diagnostics);

public static class SessionScanner
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
        "node_modules",
        "metadata"
    };

    public static ScanResult Scan(string path, int maxDepth = 2)
    {
        var diagnostics = new List<Diagnostic>();
        var fullPath = Path.GetFullPath(path);

        if (File.Exists(fullPath))
        {
            fullPath = Path.GetDirectoryName(fullPath) ?? fullPath;
        }

        if (!Directory.Exists(fullPath))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                "PATH_NOT_FOUND",
                $"找不到路徑：{fullPath}"));
            return new ScanResult(fullPath, Array.Empty<SessionCandidate>(), diagnostics);
        }

        if (IsSessionFolder(fullPath))
        {
            return new ScanResult(
                fullPath,
                new[] { new SessionCandidate(fullPath, null) },
                diagnostics);
        }

        var candidates = new List<SessionCandidate>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Directory, int Depth, string? DeviceHint)>();
        queue.Enqueue((fullPath, 0, null));

        while (queue.Count > 0)
        {
            var (directory, depth, deviceHint) = queue.Dequeue();
            if (!visited.Add(directory) || depth >= maxDepth)
            {
                continue;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "DIRECTORY_READ_FAILED",
                    $"無法讀取資料夾：{directory} ({ex.Message})"));
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (IgnoredDirectoryNames.Contains(name))
                {
                    continue;
                }

                if (IsSessionFolder(child))
                {
                    candidates.Add(new SessionCandidate(child, deviceHint));
                    continue;
                }

                // A grouping folder directly below the selected root is treated as a device hint.
                var nextHint = depth == 0 ? name : deviceHint;
                queue.Enqueue((child, depth + 1, nextHint));
            }
        }

        if (candidates.Count == 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "NO_SESSIONS_FOUND",
                $"在 {fullPath} 下找不到任何包含 System_BenchmarkLog.csv 或 *_BenchmarkLog.csv 的場次資料夾"));
        }

        candidates.Sort((a, b) => string.Compare(b.Path, a.Path, StringComparison.OrdinalIgnoreCase));

        return new ScanResult(fullPath, candidates, diagnostics);
    }

    public static bool IsSessionFolder(string directory)
    {
        try
        {
            if (File.Exists(Path.Combine(directory, "System_BenchmarkLog.csv")))
            {
                return true;
            }

            return Directory
                .EnumerateFiles(directory, "*_BenchmarkLog.csv", SearchOption.TopDirectoryOnly)
                .Any();
        }
        catch
        {
            return false;
        }
    }
}
