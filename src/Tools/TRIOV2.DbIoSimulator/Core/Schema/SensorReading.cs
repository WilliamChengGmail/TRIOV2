namespace TRIOV2.DbIoSimulator.Core.Schema;

/// <summary>
/// SensorReading — 設備感測器即時讀值資料表模型
/// 製作者: Office of William
/// </summary>
public sealed class SensorReading
{
    public long Id { get; set; }
    public string SensorId { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int IsInRange { get; set; }
    public string Timestamp { get; set; } = string.Empty;
}

