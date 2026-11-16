using System.IO;
using System.Text;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// 額外監控任務 (Task Name) 匯入解析結果
/// 製作者: Office of William
/// </summary>
public sealed record TaskImportResult(
    IReadOnlyList<string> TaskNames,
    int TotalLinesRead,
    int SkippedLines);

/// <summary>
/// 額外監控任務清單 (CSV / TXT) 範例產生器與匯入解析器
/// 製作者: Office of William
/// </summary>
public static class TaskListImporter
{
    public const string DefaultSampleFileName = "TrackTasks_Template.csv";

    private static readonly HashSet<string> KnownHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "TaskName",
        "Task Name",
        "ProcessName",
        "Process Name",
        "任務名稱",
        "處理程序名稱",
        "程序名稱"
    };

    /// <summary>
    /// 產生可供 Excel 與文字編輯器直接開啟的標準範例清單內容
    /// </summary>
    public static string BuildSampleContent()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ===========================================================================");
        sb.AppendLine("# TRIO V2 PC效能資源模擬器 — 額外監控任務 (Task Name) 匯入清單範例檔");
        sb.AppendLine("# 製作者: Office of William");
        sb.AppendLine("# ===========================================================================");
        sb.AppendLine("# 【填入格式規範】");
        sb.AppendLine("# 1. 每行填入一個處理程序名稱 (Task / Process Name)，支援 .csv 或 .txt 純文字");
        sb.AppendLine("# 2. 不區分大小寫；可填完整檔名 (如 TRIOV2.App.exe) 或程序名稱 (TRIOV2.App)，系統會自動正規化");
        sb.AppendLine("# 3. 若使用 CSV 多欄位編輯，系統會讀取每列第一欄內容；空白列與 '#' 或 '//' 開頭列會自動略過");
        sb.AppendLine("# 4. 第一列若為 TaskName / ProcessName / 任務名稱 表頭，系統亦會自動略過");
        sb.AppendLine("# 5. 若該任務於開始紀錄當下尚未啟動，系統將保持待命監視，待其啟動後立即自動記錄效能數據");
        sb.AppendLine("# ===========================================================================");
        sb.AppendLine("TaskName");
        sb.AppendLine("TRIOV2.App");
        sb.AppendLine("TRIOV2.DbIoSimulator");
        sb.AppendLine("TRIOV2.PrivilegedService");
        sb.AppendLine("notepad");
        return sb.ToString();
    }

    /// <summary>
    /// 將範例清單儲存至指定檔案 (採用 UTF-8 with BOM 確保 Excel 開啟繁體中文不亂碼)
    /// </summary>
    public static void SaveSampleFile(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(filePath, BuildSampleContent(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// 從指定的 .csv / .txt 檔案解析要監控的 Task Name 清單 (使用 FileShare.ReadWrite，Excel 開啟中亦可讀取)
    /// </summary>
    public static TaskImportResult ParseFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("找不到指定的匯入清單檔案。", filePath);
        }

        var lines = new List<string>();
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }
        }

        return ParseLines(lines);
    }

    /// <summary>
    /// 從手動輸入的文字解析單筆或多筆 Task Name (支援逗號、分號或換行分隔)
    /// </summary>
    public static IReadOnlyList<string> ParseManualInput(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return Array.Empty<string>();
        }

        string[] tokens = rawInput.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (string token in tokens)
        {
            string normalized = NormalizeTaskName(token);
            if (!string.IsNullOrEmpty(normalized) && !KnownHeaders.Contains(normalized) && unique.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    /// <summary>
    /// 解析檔案內容各行並過濾註解、空行與表頭
    /// </summary>
    public static TaskImportResult ParseLines(IEnumerable<string> lines)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tasks = new List<string>();
        int totalLines = 0;
        int skipped = 0;

        foreach (string rawLine in lines)
        {
            totalLines++;
            string trimmed = rawLine.Trim().TrimStart('\uFEFF');

            if (string.IsNullOrWhiteSpace(trimmed) ||
                trimmed.StartsWith('#') ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                skipped++;
                continue;
            }

            // 若為 CSV 格式取第一欄
            string firstColumn = ExtractFirstCsvColumn(trimmed);
            string normalized = NormalizeTaskName(firstColumn);

            if (string.IsNullOrEmpty(normalized) || KnownHeaders.Contains(normalized))
            {
                skipped++;
                continue;
            }

            if (unique.Add(normalized))
            {
                tasks.Add(normalized);
            }
            else
            {
                skipped++;
            }
        }

        return new TaskImportResult(tasks, totalLines, skipped);
    }

    /// <summary>
    /// 正規化處理程序名稱：去除空白、路徑與 .exe 副檔名以匹配 Process.ProcessName
    /// </summary>
    public static string NormalizeTaskName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

        string cleaned = rawName.Trim().Trim('"', '\'').Trim();
        if (string.IsNullOrEmpty(cleaned)) return string.Empty;

        if (cleaned.Contains('\\') || cleaned.Contains('/'))
        {
            cleaned = Path.GetFileName(cleaned);
        }

        if (cleaned.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && cleaned.Length > 4)
        {
            cleaned = cleaned[..^4];
        }

        return cleaned.Trim();
    }

    private static string ExtractFirstCsvColumn(string line)
    {
        if (line.StartsWith('"'))
        {
            int closingQuote = line.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                return line[1..closingQuote];
            }
        }

        int commaIndex = line.IndexOf(',');
        return commaIndex >= 0 ? line[..commaIndex] : line;
    }
}

