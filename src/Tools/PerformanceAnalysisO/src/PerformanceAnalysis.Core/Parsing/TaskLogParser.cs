using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Parsing;

public static class TaskLogParser
{
    private static readonly string[] TimestampAliases = { "時間戳記", "時間" };
    private static readonly string[] SequenceAliases = { "樣本序號", "序號" };
    private static readonly string[] ProcessNameAliases = { "處理程序名稱", "進程名稱", "處理程序" };
    private static readonly string[] PidAliases = { "PID", "進程ID", "處理程序ID" };
    private static readonly string[] StateAliases = { "狀態" };
    private static readonly string[] CpuAliases = { "CPU使用率(%)", "CPU使用率" };
    private static readonly string[] MemoryAliases = { "記憶體使用量(MB)", "記憶體使用量" };
    private static readonly string[] DiskAliases = { "磁碟讀寫速率(KB/s)", "磁碟讀寫速率" };
    private static readonly string[] NetworkAliases = { "網路傳輸速率(Kbps)", "網路傳輸速率" };
    private static readonly string[] PowerAliases = { "電源用量" };
    private static readonly string[] PowerTrendAliases = { "電源用量趨勢" };

    public static LogParseResult<TaskSample> Parse(string path, string taskName)
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
            return LogParseResult<TaskSample>.Empty(new Diagnostic(
                DiagnosticSeverity.Error,
                "CSV_READ_FAILED",
                $"{fileName}: 無法讀取檔案 ({ex.Message})"));
        }

        if (table.Rows.Count == 0)
        {
            return LogParseResult<TaskSample>.Empty(new Diagnostic(
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
            return LogParseResult<TaskSample>.Empty(new Diagnostic(
                DiagnosticSeverity.Error,
                "MISSING_TIMESTAMP",
                $"{fileName}: 找不到時間戳記欄位"));
        }

        var sequenceIndex = LogColumnMap.FindIndex(table.Headers, SequenceAliases);
        var processNameIndex = LogColumnMap.FindIndex(table.Headers, ProcessNameAliases);
        var pidIndex = LogColumnMap.FindIndex(table.Headers, PidAliases);
        var stateIndex = LogColumnMap.FindIndex(table.Headers, StateAliases);
        var cpuIndex = LogColumnMap.FindIndex(table.Headers, CpuAliases);
        var memoryIndex = LogColumnMap.FindIndex(table.Headers, MemoryAliases);
        var diskIndex = LogColumnMap.FindIndex(table.Headers, DiskAliases);
        var networkIndex = LogColumnMap.FindIndex(table.Headers, NetworkAliases);
        var powerIndex = LogColumnMap.FindIndex(table.Headers, PowerAliases);
        var powerTrendIndex = LogColumnMap.FindIndex(table.Headers, PowerTrendAliases);

        var items = new List<TaskSample>(table.Rows.Count);
        var invalidRows = 0;
        var unknownStateRows = 0;

        foreach (var row in table.Rows)
        {
            var timestampText = LogColumnMap.Get(row.Fields, timestampIndex);
            if (!SystemLogParser.TryParseTimestamp(timestampText, out var timestamp))
            {
                invalidRows++;
                continue;
            }

            var rawState = LogColumnMap.Get(row.Fields, stateIndex)?.Trim() ?? string.Empty;
            var state = MapState(rawState);
            if (state == TaskRunState.Unknown && !string.IsNullOrWhiteSpace(rawState))
            {
                unknownStateRows++;
            }

            items.Add(new TaskSample(
                SystemLogParser.ParseInt(LogColumnMap.Get(row.Fields, sequenceIndex), 0),
                timestamp,
                string.IsNullOrWhiteSpace(LogColumnMap.Get(row.Fields, processNameIndex))
                    ? taskName
                    : LogColumnMap.Get(row.Fields, processNameIndex)!.Trim(),
                SystemLogParser.ParseInt(LogColumnMap.Get(row.Fields, pidIndex), 0),
                state,
                rawState,
                SystemLogParser.ParseDouble(LogColumnMap.Get(row.Fields, cpuIndex)),
                SystemLogParser.ParseDouble(LogColumnMap.Get(row.Fields, memoryIndex)),
                SystemLogParser.ParseDouble(LogColumnMap.Get(row.Fields, diskIndex)),
                SystemLogParser.ParseDouble(LogColumnMap.Get(row.Fields, networkIndex)),
                LogColumnMap.Get(row.Fields, powerIndex)?.Trim() ?? string.Empty,
                LogColumnMap.Get(row.Fields, powerTrendIndex)?.Trim() ?? string.Empty));
        }

        if (invalidRows > 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "INVALID_ROWS",
                $"{fileName}: 略過 {invalidRows} 行無效時間戳記"));
        }

        if (unknownStateRows > 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "UNKNOWN_STATE",
                $"{fileName}: {unknownStateRows} 行狀態值無法識別"));
        }

        items.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        return new LogParseResult<TaskSample>(items, diagnostics, invalidRows);
    }

    public static TaskRunState MapState(string? rawState)
    {
        return rawState?.Trim() switch
        {
            "執行中" => TaskRunState.Running,
            "尚未執行" => TaskRunState.NotRunning,
            "已結束" => TaskRunState.NotRunning,
            "已停止" => TaskRunState.NotRunning,
            "未執行" => TaskRunState.NotRunning,
            _ => TaskRunState.Unknown
        };
    }
}
