using System.IO;
using System.Text.Json;
using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 將判定門檻保存在執行檔旁的 analyzer-settings.json，
/// 讓重新開啟工具或同一份執行檔的使用者看到一致的判定結果。
/// </summary>
public static class AnalyzerSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "analyzer-settings.json");

    public static EvaluationThresholds Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<EvaluationThresholds>(File.ReadAllText(SettingsPath));
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // 設定檔損毀時回到預設值，不影響分析
        }

        return new EvaluationThresholds();
    }

    public static bool TrySave(EvaluationThresholds thresholds)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(thresholds, JsonOptions));
            return true;
        }
        catch
        {
            // 執行檔所在位置唯讀（例如光碟、受保護資料夾）時略過保存
            return false;
        }
    }
}
