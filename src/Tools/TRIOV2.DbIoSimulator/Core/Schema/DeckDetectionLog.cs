namespace TRIOV2.DbIoSimulator.Core.Schema;

/// <summary>
/// DeckDetectionLog — 工業相機盤面偵測日誌資料表模型
/// 製作者: Office of William
/// </summary>
public sealed class DeckDetectionLog
{
    public long Id { get; set; }
    public string CycleId { get; set; } = string.Empty;
    public int SlotIndex { get; set; }
    public string SlotLabel { get; set; } = string.Empty;
    public int SamplePresent { get; set; }
    public int ReagentPresent { get; set; }
    public double ConfidenceScore { get; set; }
    public string AnalysisResultJson { get; set; } = string.Empty;
    public string CameraId { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

