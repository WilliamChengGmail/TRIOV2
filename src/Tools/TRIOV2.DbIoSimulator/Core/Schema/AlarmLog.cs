namespace TRIOV2.DbIoSimulator.Core.Schema;

/// <summary>
/// AlarmLog — 儀器異常與警報日誌資料表模型
/// 製作者: Office of William
/// </summary>
public sealed class AlarmLog
{
    public long Id { get; set; }
    public string? CycleId { get; set; }
    public string Severity { get; set; } = "Info";
    public string AlarmCode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int IsAcknowledged { get; set; }
    public string Timestamp { get; set; } = string.Empty;
}

