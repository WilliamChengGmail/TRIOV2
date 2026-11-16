namespace TRIOV2.DbIoSimulator.Core.Schema;

/// <summary>
/// ModbusCommandLog — Modbus 通訊控制指令與回應日誌資料表模型
/// 製作者: Office of William
/// </summary>
public sealed class ModbusCommandLog
{
    public long Id { get; set; }
    public int DeviceAddress { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public int FunctionCode { get; set; }
    public int RegisterAddress { get; set; }
    public string CommandHex { get; set; } = string.Empty;
    public string ResponseHex { get; set; } = string.Empty;
    public double RttMs { get; set; }
    public int IsSuccess { get; set; }
    public string Timestamp { get; set; } = string.Empty;
}

