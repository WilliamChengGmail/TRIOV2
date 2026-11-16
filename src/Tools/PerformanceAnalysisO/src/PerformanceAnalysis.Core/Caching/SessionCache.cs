using System.Security.Cryptography;
using System.Text;
using PerformanceAnalysis.Core.Models;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Caching;

/// <summary>
/// In-memory cache of analyzed sessions. The key includes source file sizes and timestamps,
/// so editing a CSV invalidates the entry. Metadata changes are handled by clearing the cache
/// from the caller when aliases/baselines/settings change.
/// </summary>
public sealed class SessionCache
{
    private sealed record Entry(string Fingerprint, Session Session);

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryGet(SessionCandidate candidate, out Session? session)
    {
        var fingerprint = ComputeFingerprint(candidate);
        lock (_gate)
        {
            if (_entries.TryGetValue(candidate.Path, out var entry)
                && string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                session = entry.Session;
                return true;
            }

            _entries.Remove(candidate.Path);
            session = null;
            return false;
        }
    }

    public void Set(SessionCandidate candidate, Session session)
    {
        var fingerprint = ComputeFingerprint(candidate);
        lock (_gate)
        {
            _entries[candidate.Path] = new Entry(fingerprint, session);
        }
    }

    public int Clear()
    {
        lock (_gate)
        {
            var count = _entries.Count;
            _entries.Clear();
            return count;
        }
    }

    public static string ComputeFingerprint(SessionCandidate candidate)
    {
        var builder = new StringBuilder(candidate.Path);
        try
        {
            var files = Directory
                .EnumerateFiles(candidate.Path, "*", SearchOption.AllDirectories)
                .Where(file =>
                {
                    var name = Path.GetFileName(file);
                    return name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(name, "ref.txt", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                builder.Append('|')
                    .Append(Path.GetRelativePath(candidate.Path, file))
                    .Append('|')
                    .Append(info.Length)
                    .Append('|')
                    .Append(info.LastWriteTimeUtc.Ticks);
            }
        }
        catch
        {
            builder.Append("|unreadable");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }
}
