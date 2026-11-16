namespace TRIOV2.DbIoSimulator.Core;

/// <summary>
/// 模擬情境模式列舉
/// 製作者: Office of William
/// </summary>
public enum SimulationMode
{
    Idle,
    Normal,
    Stress,
    Custom
}

/// <summary>
/// 各情境對應的讀寫頻率、Payload 大小與並行設定參數
/// 製作者: Office of William
/// </summary>
public sealed class ScenarioConfig
{
    public SimulationMode Mode { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>Modbus 日誌寫入間隔 (ms)，0 表示停用</summary>
    public int ModbusIntervalMs { get; set; }

    /// <summary>單次 Modbus 迴圈寫入的批次筆數 (Stress 模式用 Transaction 批次塞入高吞吐量)</summary>
    public int ModbusBatchSize { get; set; } = 1;

    /// <summary>儀器狀態表寫入間隔 (ms)，0 表示停用</summary>
    public int InstrumentStateIntervalMs { get; set; }

    /// <summary>工業相機盤面偵測寫入間隔 (ms)，0 表示停用</summary>
    public int DeckDetectionIntervalMs { get; set; }

    /// <summary>DeckDetectionLog 每筆 JSON Payload 大小 (KB)</summary>
    public int DeckPayloadSizeKb { get; set; }

    /// <summary>感測器數據寫入間隔 (ms)，0 表示停用</summary>
    public int SensorIntervalMs { get; set; }

    /// <summary>模擬感測器數量</summary>
    public int SensorCount { get; set; }

    /// <summary>每秒觸發 AlarmLog 的機率 (0 ~ 100%)</summary>
    public double AlarmProbabilityPctPerSec { get; set; }

    /// <summary>模擬上層/UI 定期輪詢 SQLite 的查詢間隔 (ms)，0 表示停用</summary>
    public int ReadQueryIntervalMs { get; set; }

    /// <summary>Modbus 寫入並行執行緒數量 (1 ~ 4)</summary>
    public int ConcurrentWriterThreads { get; set; } = 1;

    /// <summary>DB 檔案大小上限保護 (MB)，0 表示不限制</summary>
    public int MaxDbSizeMb { get; set; } = 1024;

    /// <summary>
    /// 依據模擬模式建立對應預設參數組 (Mapping / Factory 模式)
    /// </summary>
    public static ScenarioConfig FromMode(SimulationMode mode) => mode switch
    {
        SimulationMode.Idle => new ScenarioConfig
        {
            Mode = SimulationMode.Idle,
            DisplayName = "待機模式 (Idle)",
            Description = "僅保留儀器狀態心跳與低頻環境感測器寫入 (~5 ops/min)",
            ModbusIntervalMs = 0,
            ModbusBatchSize = 1,
            InstrumentStateIntervalMs = 5000,
            DeckDetectionIntervalMs = 0,
            DeckPayloadSizeKb = 10,
            SensorIntervalMs = 10000,
            SensorCount = 2,
            AlarmProbabilityPctPerSec = 0.0,
            ReadQueryIntervalMs = 2000,
            ConcurrentWriterThreads = 1,
            MaxDbSizeMb = 1024
        },
        SimulationMode.Normal => new ScenarioConfig
        {
            Mode = SimulationMode.Normal,
            DisplayName = "正常運行 (Normal)",
            Description = "模擬完整萃取循環：100ms Modbus、500ms 狀態、5s 盤面影像摘要 (~15 op/s)",
            ModbusIntervalMs = 100,
            ModbusBatchSize = 1,
            InstrumentStateIntervalMs = 500,
            DeckDetectionIntervalMs = 5000,
            DeckPayloadSizeKb = 20,
            SensorIntervalMs = 1000,
            SensorCount = 4,
            AlarmProbabilityPctPerSec = 1.0,
            ReadQueryIntervalMs = 333,
            ConcurrentWriterThreads = 1,
            MaxDbSizeMb = 1024
        },
        SimulationMode.Stress => new ScenarioConfig
        {
            Mode = SimulationMode.Stress,
            DisplayName = "高負載壓力 (Stress)",
            Description = "4 執行緒並行寫入、10ms Modbus、2MB 影像分析 JSON 與高頻查詢 (100+ op/s)",
            ModbusIntervalMs = 10,
            ModbusBatchSize = 3,
            InstrumentStateIntervalMs = 100,
            DeckDetectionIntervalMs = 2000,
            DeckPayloadSizeKb = 2048,
            SensorIntervalMs = 500,
            SensorCount = 16,
            AlarmProbabilityPctPerSec = 10.0,
            ReadQueryIntervalMs = 50,
            ConcurrentWriterThreads = 4,
            MaxDbSizeMb = 1024
        },
        _ => new ScenarioConfig
        {
            Mode = SimulationMode.Custom,
            DisplayName = "自訂模式 (Custom)",
            Description = "由下方參數滑桿自訂各項資料庫讀寫負載強度",
            ModbusIntervalMs = 100,
            ModbusBatchSize = 1,
            InstrumentStateIntervalMs = 500,
            DeckDetectionIntervalMs = 5000,
            DeckPayloadSizeKb = 20,
            SensorIntervalMs = 1000,
            SensorCount = 4,
            AlarmProbabilityPctPerSec = 1.0,
            ReadQueryIntervalMs = 333,
            ConcurrentWriterThreads = 1,
            MaxDbSizeMb = 1024
        }
    };

    public ScenarioConfig Clone() => new()
    {
        Mode = Mode,
        DisplayName = DisplayName,
        Description = Description,
        ModbusIntervalMs = ModbusIntervalMs,
        ModbusBatchSize = ModbusBatchSize,
        InstrumentStateIntervalMs = InstrumentStateIntervalMs,
        DeckDetectionIntervalMs = DeckDetectionIntervalMs,
        DeckPayloadSizeKb = DeckPayloadSizeKb,
        SensorIntervalMs = SensorIntervalMs,
        SensorCount = SensorCount,
        AlarmProbabilityPctPerSec = AlarmProbabilityPctPerSec,
        ReadQueryIntervalMs = ReadQueryIntervalMs,
        ConcurrentWriterThreads = ConcurrentWriterThreads,
        MaxDbSizeMb = MaxDbSizeMb
    };
}

