using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PerformanceAnalysis.Core.Analysis;

namespace PerformanceAnalysis.Core.Configuration;

public sealed class AnalysisSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AnalysisSettingsStore(string rootDirectory)
    {
        FilePath = Path.Combine(rootDirectory, "settings.json");
    }

    public string FilePath { get; }

    public AnalysisOptions Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return AnalysisOptions.Default;
            }

            var json = File.ReadAllText(FilePath);
            var options = JsonSerializer.Deserialize<AnalysisOptions>(json, JsonOptions)
                          ?? AnalysisOptions.Default;
            options.Normalize();
            return options;
        }
        catch
        {
            return AnalysisOptions.Default;
        }
    }

    public void Save(AnalysisOptions options)
    {
        options.Normalize();
        var directory = Path.GetDirectoryName(FilePath) ?? ".";
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".settings.{Guid.NewGuid():N}.tmp");
        var json = JsonSerializer.Serialize(options, JsonOptions);
        File.WriteAllText(tempPath, json, new UTF8Encoding(false));
        File.Move(tempPath, FilePath, overwrite: true);
    }
}
