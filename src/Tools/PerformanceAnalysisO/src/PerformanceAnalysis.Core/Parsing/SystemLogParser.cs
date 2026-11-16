using System.Globalization;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Parsing;

public static class SystemLogParser
{
    private static readonly string[] TimestampAliases = { "時間戳記", "時間" };
    private static readonly string[] SequenceAliases = { "樣本序號", "序號" };
    private static readonly string[] CpuTotalAliases = { "全系統CPU(%)", "全系統CPU", "系統CPU(%)" };
    private static readonly string[] CpuSimulatorAliases = { "模擬器CPU(%)", "模擬器CPU" };
    private static readonly string[] CpuFreeAliases = { "剩餘空閒CPU(%)", "剩餘空閒CPU", "空閒CPU(%)" };
    private static readonly string[] MemoryUsedAliases = { "已用記憶體(GB)", "已用記憶體" };
    private static readonly string[] MemoryAvailableAliases = { "可用記憶體(GB)", "可用記憶體" };
    private static readonly string[] MemoryPercentAliases = { "記憶體使用率(%)", "記憶體使用率" };
    private static readonly string[] FrameBufferAliases = { "模擬影像緩衝(MB)", "模擬影像緩衝" };
    private static readonly string[] RecognitionStateAliases = { "底層辨識狀態" };
    private static readonly string[] RecognitionMsAliases = { "辨識運算時長(ms)", "辨識運算時長" };
    private static readonly string[] OperationModeAliases = { "運作模式" };

    public static LogParseResult<SystemSample> Parse(string path)
    {
        var diagnostics = new List<Diagnostic>();
        var fileName = Path.GetFileName(path);
        CsvTable table;

        try
        {
            table = CsvTableReader.Read(path);
        }
        catch (Exception ex)
        {
            return LogParseResult<SystemSample>.Empty(new Diagnostic(
                DiagnosticSeverity.Error,
                "CSV_READ_FAILED",
                $"{fileName}: 無法讀取檔案 ({ex.Message})"));
        }

        if (table.Rows.Count == 0)
        {
            return LogParseResult<SystemSample>.Empty(new Diagnostic(
                DiagnosticSeverity.Error,
                "CSV_EMPTY",
                $"{fileName}: 沒有可解析的資料列"));
        }

        if (table.CommentLineCount > 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Info,
                "CSV_COMMENTS",
                $"{fileName}: 已忽略 {table.CommentLineCount} 行註解/摘要"));
        }

        if (table.MalformedRowCount > 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "CSV_MALFORMED",
                $"{fileName}: {table.MalformedRowCount} 行欄位數與表頭不一致，已補齊/截斷"));
        }

        var timestampIndex = LogColumnMap.FindIndex(table.Headers, TimestampAliases);
        if (timestampIndex < 0)
        {
            return LogParseResult<SystemSample>.Empty(new Diagnostic(
                DiagnosticSeverity.Error,
                "MISSING_TIMESTAMP",
                $"{fileName}: 找不到時間戳記欄位"));
        }

        var sequenceIndex = LogColumnMap.FindIndex(table.Headers, SequenceAliases);
        var cpuTotalIndex = LogColumnMap.FindIndex(table.Headers, CpuTotalAliases);
        var cpuSimulatorIndex = LogColumnMap.FindIndex(table.Headers, CpuSimulatorAliases);
        var cpuFreeIndex = LogColumnMap.FindIndex(table.Headers, CpuFreeAliases);
        var memoryUsedIndex = LogColumnMap.FindIndex(table.Headers, MemoryUsedAliases);
        var memoryAvailableIndex = LogColumnMap.FindIndex(table.Headers, MemoryAvailableAliases);
        var memoryPercentIndex = LogColumnMap.FindIndex(table.Headers, MemoryPercentAliases);
        var frameBufferIndex = LogColumnMap.FindIndex(table.Headers, FrameBufferAliases);
        var recognitionStateIndex = LogColumnMap.FindIndex(table.Headers, RecognitionStateAliases);
        var recognitionMsIndex = LogColumnMap.FindIndex(table.Headers, RecognitionMsAliases);
        var operationModeIndex = LogColumnMap.FindIndex(table.Headers, OperationModeAliases);

        AddMissingColumnWarnings(diagnostics, fileName, new (string Name, int Index)[]
        {
            ("全系統CPU(%)", cpuTotalIndex),
            ("模擬器CPU(%)", cpuSimulatorIndex),
            ("已用記憶體(GB)", memoryUsedIndex),
            ("可用記憶體(GB)", memoryAvailableIndex),
            ("記憶體使用率(%)", memoryPercentIndex)
        });

        var items = new List<SystemSample>(table.Rows.Count);
        var invalidRows = 0;

        foreach (var row in table.Rows)
        {
            var timestampText = LogColumnMap.Get(row.Fields, timestampIndex);
            if (!TryParseTimestamp(timestampText, out var timestamp))
            {
                invalidRows++;
                continue;
            }

            items.Add(new SystemSample(
                ParseInt(LogColumnMap.Get(row.Fields, sequenceIndex), 0),
                timestamp,
                ParseDouble(LogColumnMap.Get(row.Fields, cpuTotalIndex)),
                ParseDouble(LogColumnMap.Get(row.Fields, cpuSimulatorIndex)),
                ParseDouble(LogColumnMap.Get(row.Fields, cpuFreeIndex)),
                ParseDouble(LogColumnMap.Get(row.Fields, memoryUsedIndex)),
                ParseDouble(LogColumnMap.Get(row.Fields, memoryAvailableIndex)),
                ParseDouble(LogColumnMap.Get(row.Fields, memoryPercentIndex)),
                ParseDouble(LogColumnMap.Get(row.Fields, frameBufferIndex)),
                LogColumnMap.Get(row.Fields, recognitionStateIndex)?.Trim() ?? string.Empty,
                ParseDouble(LogColumnMap.Get(row.Fields, recognitionMsIndex)),
                LogColumnMap.Get(row.Fields, operationModeIndex)?.Trim() ?? string.Empty));
        }

        if (invalidRows > 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "INVALID_ROWS",
                $"{fileName}: 略過 {invalidRows} 行無效時間戳記"));
        }

        items.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        return new LogParseResult<SystemSample>(items, diagnostics, invalidRows);
    }

    internal static bool TryParseTimestamp(string? text, out DateTime timestamp)
    {
        timestamp = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return DateTime.TryParseExact(
                   text.Trim(),
                   "yyyy-MM-dd HH:mm:ss",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out timestamp)
               || DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    internal static double ParseDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return double.TryParse(
            text.Trim(),
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
    }

    internal static int ParseInt(string? text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static void AddMissingColumnWarnings(
        ICollection<Diagnostic> diagnostics,
        string fileName,
        IEnumerable<(string Name, int Index)> columns)
    {
        foreach (var (name, index) in columns)
        {
            if (index < 0)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "MISSING_COLUMN",
                    $"{fileName}: 找不到欄位「{name}」，將以 0 處理"));
            }
        }
    }
}
