using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Metadata;

public sealed class SessionMetadataStore
{
    private static readonly Regex ValidKeyRegex = new("^[a-f0-9]{8,64}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private Dictionary<string, List<SessionMetadata>>? _folderIndex;

    public SessionMetadataStore(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(AppContext.BaseDirectory, "Metadata");
        SessionsDirectory = Path.Combine(RootDirectory, "sessions");
    }

    public string RootDirectory { get; }

    public string SessionsDirectory { get; }

    public SessionMetadata? TryLoad(string sessionKey)
    {
        if (!ValidKeyRegex.IsMatch(sessionKey))
        {
            return null;
        }

        var path = Path.Combine(SessionsDirectory, $"{sessionKey}.json");
        return TryRead(path);
    }

    public SessionMetadata? TryLoadByFolderName(string folderName, DateTime? startTime = null)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return null;
        }

        _folderIndex ??= BuildFolderIndex();
        if (!_folderIndex.TryGetValue(folderName, out var matches) || matches.Count == 0)
        {
            return null;
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        if (startTime is not null)
        {
            var closest = matches
                .Where(metadata => metadata.StartTime is not null)
                .Select(metadata => (
                    Metadata: metadata,
                    Difference: Math.Abs((metadata.StartTime!.Value.DateTime - startTime.Value).TotalSeconds)))
                .Where(candidate => candidate.Difference <= 2)
                .OrderBy(candidate => candidate.Difference)
                .FirstOrDefault();

            if (closest.Metadata is not null)
            {
                return closest.Metadata;
            }
        }

        // Ambiguous: same folder name exists for multiple devices/sessions.
        return null;
    }

    public IReadOnlyList<SessionMetadata> LoadAll()
    {
        if (!Directory.Exists(SessionsDirectory))
        {
            return Array.Empty<SessionMetadata>();
        }

        var result = new List<SessionMetadata>();
        foreach (var path in Directory.EnumerateFiles(SessionsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var metadata = TryRead(path);
            if (metadata is not null)
            {
                result.Add(metadata);
            }
        }

        return result;
    }

    public void Save(SessionMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.SessionKey) || !ValidKeyRegex.IsMatch(metadata.SessionKey))
        {
            throw new ArgumentException("SessionKey 必須是 8–64 位十六進位字串。", nameof(metadata));
        }

        Directory.CreateDirectory(SessionsDirectory);
        metadata.UpdatedAt ??= DateTimeOffset.Now;

        var path = Path.Combine(SessionsDirectory, $"{metadata.SessionKey}.json");
        var tempPath = Path.Combine(SessionsDirectory, $".{metadata.SessionKey}.{Guid.NewGuid():N}.tmp");

        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        File.WriteAllText(tempPath, json, new System.Text.UTF8Encoding(false));
        File.Move(tempPath, path, overwrite: true);
        _folderIndex = null;
    }

    private SessionMetadata? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SessionMetadata>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private Dictionary<string, List<SessionMetadata>> BuildFolderIndex()
    {
        var index = new Dictionary<string, List<SessionMetadata>>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadata in LoadAll())
        {
            if (string.IsNullOrWhiteSpace(metadata.SourceFolderName))
            {
                continue;
            }

            if (!index.TryGetValue(metadata.SourceFolderName, out var list))
            {
                list = new List<SessionMetadata>();
                index[metadata.SourceFolderName] = list;
            }

            list.Add(metadata);
        }

        return index;
    }
}
