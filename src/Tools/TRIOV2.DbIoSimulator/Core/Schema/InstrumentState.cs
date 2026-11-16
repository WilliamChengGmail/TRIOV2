namespace TRIOV2.DbIoSimulator.Core.Schema;

/// <summary>
/// InstrumentState — 儀器狀態資料表模型
/// 製作者: Office of William
/// </summary>
public sealed class InstrumentState
{
    public long Id { get; set; }
    public string CycleId { get; set; } = string.Empty;
    public string Phase { get; set; } = "Idle";
    public int StepIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public double ProgressPct { get; set; }
    public int IsRunning { get; set; }
    public string Timestamp { get; set; } = string.Empty;
}

