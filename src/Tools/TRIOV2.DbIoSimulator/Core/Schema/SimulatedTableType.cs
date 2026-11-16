namespace TRIOV2.DbIoSimulator.Core.Schema;

/// <summary>
/// 模擬寫入的目標 SQLite 資料表列舉
/// 製作者: Office of William
/// </summary>
public enum SimulatedTableType
{
    InstrumentState,
    DeckDetectionLog,
    ModbusCommandLog,
    AlarmLog,
    SensorReading
}

