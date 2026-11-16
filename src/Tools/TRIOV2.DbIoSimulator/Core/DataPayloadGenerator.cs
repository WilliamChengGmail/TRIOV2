using TRIOV2.DbIoSimulator.Core.Schema;

namespace TRIOV2.DbIoSimulator.Core;

/// <summary>
/// 模擬 TRIO V2 底層硬體通訊與生物萃取程序之假資料產生器 (Thread-Safe、低 GC 壓力)
/// 製作者: Office of William
/// </summary>
public sealed class DataPayloadGenerator
{
    private static readonly (string Phase, string StepName)[] CycleSteps =
    [
        ("Detecting", "DeckDetection_PreScan"),
        ("Extracting", "TipPickup_96Channel"),
        ("Extracting", "SampleAspiration"),
        ("Extracting", "LysisBufferDispense"),
        ("Extracting", "ThermalIncubation_56C"),
        ("Extracting", "MagneticBeadBinding"),
        ("Extracting", "WashBuffer_Stage1"),
        ("Extracting", "WashBuffer_Stage2"),
        ("Extracting", "EthanolEvaporation"),
        ("Extracting", "ElutionBufferDispense"),
        ("Detecting", "DeckDetection_PostCheck"),
        ("Completed", "CycleCompleted_HomeAllAxes")
    ];

    private static readonly (int Address, string Name)[] ModbusDevices =
    [
        (1, "Camera_DeckTop"),
        (2, "Motor_AxisX"),
        (3, "Motor_AxisY"),
        (4, "Motor_AxisZ"),
        (5, "Pipette_ArmA"),
        (6, "Heater_Block1"),
        (7, "Magnet_Rack")
    ];

    private static readonly int[] ModbusFuncCodes = [3, 6, 16];

    private static readonly (string Severity, string Source, string Code, string Description)[] AlarmTemplates =
    [
        ("Info", "System", "SYS_001", "系統完成盤面歸零校正"),
        ("Info", "Camera", "CAM_010", "工業相機完成 24 槽位對焦曝光"),
        ("Warning", "Camera", "CAM_W02", "工業相機補光燈亮度略低，已自動調整增益"),
        ("Warning", "Motor", "MOT_W05", "Y 軸移動接近額定電流上限，請檢查導軌潤滑"),
        ("Warning", "Pipette", "PIP_W03", "檢體槽位 B3 吸液壓力波形輕微偏移"),
        ("Error", "Motor", "MOT_ERR_013", "X 軸步進馬達編碼器脈衝延遲重試成功"),
        ("Error", "Pipette", "PIP_ERR_007", "移液臂吸頭取用感測器回傳異常，執行二次重取"),
        ("Critical", "System", "THERM_C01", "加熱模組區塊溫度短暫超限 (+2.3°C)，啟動風扇降溫")
    ];

    private static readonly (string Type, string Unit, double MinVal, double MaxVal)[] SensorTemplates =
    [
        ("Temperature", "°C", 34.5, 58.0),
        ("Pressure", "kPa", 98.0, 105.5),
        ("Humidity", "%RH", 35.0, 55.0),
        ("FlowRate", "mL/min", 0.8, 4.5)
    ];

    // 預先配置 1024 字元的重複區塊以快速組成大型 JSON payload，避免高頻測試時大量配置 StringBuilder 造成 C# GC 壓力
    private static readonly string PaddingBlock = new('A', 512);
    private readonly object _payloadCacheLock = new();
    private int _cachedPayloadSizeKb = -1;
    private string _cachedPayloadPadding = string.Empty;

    public int TotalSteps => CycleSteps.Length;

    /// <summary>
    /// 產生儀器狀態紀錄
    /// </summary>
    public InstrumentState GenerateInstrumentState(string cycleId, int stepIndex, bool isIdleMode)
    {
        string timestamp = DateTime.UtcNow.ToString("O");
        if (isIdleMode)
        {
            return new InstrumentState
            {
                CycleId = cycleId,
                Phase = "Idle",
                StepIndex = 0,
                StepName = "Standby_Heartbeat",
                ProgressPct = 0.0,
                IsRunning = 0,
                Timestamp = timestamp
            };
        }

        int normalizedStep = Math.Abs(stepIndex) % CycleSteps.Length;
        var (phase, stepName) = CycleSteps[normalizedStep];
        double progress = Math.Round((normalizedStep + 1) * 100.0 / CycleSteps.Length, 1);

        return new InstrumentState
        {
            CycleId = cycleId,
            Phase = phase,
            StepIndex = normalizedStep,
            StepName = stepName,
            ProgressPct = progress,
            IsRunning = phase == "Completed" ? 0 : 1,
            Timestamp = timestamp
        };
    }

    /// <summary>
    /// 產生盤面偵測日誌 (含指定 KB 大小之 JSON Payload)
    /// </summary>
    public DeckDetectionLog GenerateDeckDetectionLog(string cycleId, int slotIndex, int payloadSizeKb)
    {
        var rng = Random.Shared;
        int normalizedSlot = Math.Abs(slotIndex) % 24;
        char rowChar = (char)('A' + (normalizedSlot / 6));
        int colNum = (normalizedSlot % 6) + 1;
        string slotLabel = $"{rowChar}{colNum}";

        int samplePresent = rng.NextDouble() > 0.12 ? 1 : 0;
        int reagentPresent = rng.NextDouble() > 0.05 ? 1 : 0;
        double confidence = Math.Round(0.88 + rng.NextDouble() * 0.119, 4);

        string json = BuildDeckAnalysisJson(slotLabel, samplePresent == 1, reagentPresent == 1, confidence, payloadSizeKb);

        return new DeckDetectionLog
        {
            CycleId = cycleId,
            SlotIndex = normalizedSlot,
            SlotLabel = slotLabel,
            SamplePresent = samplePresent,
            ReagentPresent = reagentPresent,
            ConfidenceScore = confidence,
            AnalysisResultJson = json,
            CameraId = "CAM_BASLER_01",
            Timestamp = DateTime.UtcNow.ToString("O")
        };
    }

    /// <summary>
    /// 產生 Modbus 通訊指令與回應紀錄
    /// </summary>
    public ModbusCommandLog GenerateModbusCommandLog()
    {
        var rng = Random.Shared;
        var (address, name) = ModbusDevices[rng.Next(ModbusDevices.Length)];
        int funcCode = ModbusFuncCodes[rng.Next(ModbusFuncCodes.Length)];
        int regAddr = rng.Next(0x0010, 0x0400);
        bool success = rng.NextDouble() > 0.005; // 約 0.5% 通訊重試模擬
        double rtt = success
            ? Math.Round(0.8 + rng.NextDouble() * 4.5, 2)
            : Math.Round(120.0 + rng.NextDouble() * 80.0, 2);

        string cmdHex = $"{address:X2} {funcCode:X2} {regAddr >> 8:X2} {regAddr & 0xFF:X2} 00 04 {rng.Next(0x1000, 0xFFFF):X4}";
        string respHex = success
            ? $"{address:X2} {funcCode:X2} 08 {rng.Next(0x1000, 0xFFFF):X4} {rng.Next(0x1000, 0xFFFF):X4} {rng.Next(0x1000, 0xFFFF):X4}"
            : $"{address:X2} {funcCode | 0x80:X2} 02 TIMEOUT";

        return new ModbusCommandLog
        {
            DeviceAddress = address,
            DeviceName = name,
            FunctionCode = funcCode,
            RegisterAddress = regAddr,
            CommandHex = cmdHex,
            ResponseHex = respHex,
            RttMs = rtt,
            IsSuccess = success ? 1 : 0,
            Timestamp = DateTime.UtcNow.ToString("O")
        };
    }

    /// <summary>
    /// 產生警報/錯誤日誌紀錄
    /// </summary>
    public AlarmLog GenerateAlarmLog(string? cycleId)
    {
        var rng = Random.Shared;
        var tpl = AlarmTemplates[rng.Next(AlarmTemplates.Length)];

        return new AlarmLog
        {
            CycleId = cycleId,
            Severity = tpl.Severity,
            AlarmCode = tpl.Code,
            Source = tpl.Source,
            Description = tpl.Description,
            IsAcknowledged = tpl.Severity == "Info" ? 1 : 0,
            Timestamp = DateTime.UtcNow.ToString("O")
        };
    }

    /// <summary>
    /// 產生 N 筆感測器讀值紀錄
    /// </summary>
    public List<SensorReading> GenerateSensorReadings(int sensorCount)
    {
        var rng = Random.Shared;
        int count = Math.Clamp(sensorCount, 1, 64);
        string timestamp = DateTime.UtcNow.ToString("O");
        var list = new List<SensorReading>(count);

        for (int i = 0; i < count; i++)
        {
            var tpl = SensorTemplates[i % SensorTemplates.Length];
            double rawVal = tpl.MinVal + rng.NextDouble() * (tpl.MaxVal - tpl.MinVal);
            bool inRange = rng.NextDouble() > 0.02;
            if (!inRange)
            {
                rawVal += 8.5; // 模擬偶發超限數值
            }

            list.Add(new SensorReading
            {
                SensorId = $"{tpl.Type.ToUpperInvariant()[..4]}_{i + 1:D2}",
                SensorType = tpl.Type,
                Value = Math.Round(rawVal, 2),
                Unit = tpl.Unit,
                IsInRange = inRange ? 1 : 0,
                Timestamp = timestamp
            });
        }

        return list;
    }

    private string BuildDeckAnalysisJson(string slotLabel, bool hasSample, bool hasReagent, double confidence, int targetSizeKb)
    {
        string header = $"{{\"slot\":\"{slotLabel}\",\"sample\":{(hasSample ? "true" : "false")},\"reagent\":{(hasReagent ? "true" : "false")},\"confidence\":{confidence:F4},\"roi\":[120,240,64,64],\"blobData\":\"";
        int targetBytes = Math.Max(1, targetSizeKb) * 1024;
        int desiredPadLen = Math.Max(0, targetBytes - header.Length - 2);

        string pad = GetOrBuildPadding(desiredPadLen);
        return string.Concat(header, pad, "\"}");
    }

    private string GetOrBuildPadding(int length)
    {
        if (length <= 0) return string.Empty;

        lock (_payloadCacheLock)
        {
            if (_cachedPayloadSizeKb == length)
            {
                return _cachedPayloadPadding;
            }

            int repeatCount = length / PaddingBlock.Length;
            int remainder = length % PaddingBlock.Length;
            var chars = new char[length];
            for (int i = 0; i < repeatCount; i++)
            {
                PaddingBlock.CopyTo(0, chars, i * PaddingBlock.Length, PaddingBlock.Length);
            }
            if (remainder > 0)
            {
                PaddingBlock.CopyTo(0, chars, repeatCount * PaddingBlock.Length, remainder);
            }

            _cachedPayloadPadding = new string(chars);
            _cachedPayloadSizeKb = length;
            return _cachedPayloadPadding;
        }
    }
}

