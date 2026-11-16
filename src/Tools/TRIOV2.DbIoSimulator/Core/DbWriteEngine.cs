using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using TRIOV2.DbIoSimulator.Core.Schema;

namespace TRIOV2.DbIoSimulator.Core;

/// <summary>
/// 實際執行 SQLite 資料表寫入 (單筆/批次交易) 與查詢讀取之底層 I/O 引擎
/// 製作者: Office of William
/// </summary>
public sealed class DbWriteEngine
{
    private readonly WriteRateMonitor _monitor;
    private int _readCycleCursor;

    public DbWriteEngine(WriteRateMonitor monitor)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
    }

    public async Task WriteInstrumentStateAsync(string dbPath, InstrumentState state, CancellationToken ct)
    {
        try
        {
            await using var conn = await DbInitializer.OpenConfiguredConnectionAsync(dbPath, ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO InstrumentState (CycleId, Phase, StepIndex, StepName, ProgressPct, IsRunning, Timestamp)
                VALUES ($cycleId, $phase, $stepIndex, $stepName, $progressPct, $isRunning, $timestamp);
                """;
            cmd.Parameters.AddWithValue("$cycleId", state.CycleId);
            cmd.Parameters.AddWithValue("$phase", state.Phase);
            cmd.Parameters.AddWithValue("$stepIndex", state.StepIndex);
            cmd.Parameters.AddWithValue("$stepName", state.StepName);
            cmd.Parameters.AddWithValue("$progressPct", state.ProgressPct);
            cmd.Parameters.AddWithValue("$isRunning", state.IsRunning);
            cmd.Parameters.AddWithValue("$timestamp", state.Timestamp);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            long approxBytes = 96 + Encoding.UTF8.GetByteCount(state.StepName) + Encoding.UTF8.GetByteCount(state.Phase);
            _monitor.RecordWrite(
                SimulatedTableType.InstrumentState,
                rowCount: 1,
                approxBytes,
                $"Phase={state.Phase} Step={state.StepName} ({state.ProgressPct:F0}%)");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _monitor.RecordError(SimulatedTableType.InstrumentState, "INSERT", ex.Message);
        }
    }

    public async Task WriteDeckDetectionAsync(string dbPath, DeckDetectionLog log, CancellationToken ct)
    {
        try
        {
            await using var conn = await DbInitializer.OpenConfiguredConnectionAsync(dbPath, ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO DeckDetectionLog
                    (CycleId, SlotIndex, SlotLabel, SamplePresent, ReagentPresent, ConfidenceScore, AnalysisResultJson, CameraId, Timestamp)
                VALUES
                    ($cycleId, $slotIndex, $slotLabel, $samplePresent, $reagentPresent, $confidence, $json, $cameraId, $timestamp);
                """;
            cmd.Parameters.AddWithValue("$cycleId", log.CycleId);
            cmd.Parameters.AddWithValue("$slotIndex", log.SlotIndex);
            cmd.Parameters.AddWithValue("$slotLabel", log.SlotLabel);
            cmd.Parameters.AddWithValue("$samplePresent", log.SamplePresent);
            cmd.Parameters.AddWithValue("$reagentPresent", log.ReagentPresent);
            cmd.Parameters.AddWithValue("$confidence", log.ConfidenceScore);
            cmd.Parameters.AddWithValue("$json", log.AnalysisResultJson);
            cmd.Parameters.AddWithValue("$cameraId", log.CameraId);
            cmd.Parameters.AddWithValue("$timestamp", log.Timestamp);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            long approxBytes = 128 + Encoding.UTF8.GetByteCount(log.AnalysisResultJson);
            _monitor.RecordWrite(
                SimulatedTableType.DeckDetectionLog,
                rowCount: 1,
                approxBytes,
                $"Slot={log.SlotLabel} Sample={log.SamplePresent} Conf={log.ConfidenceScore:F3} ({approxBytes / 1024.0:F1} KB)");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _monitor.RecordError(SimulatedTableType.DeckDetectionLog, "INSERT", ex.Message);
        }
    }

    public async Task WriteModbusCommandsBatchAsync(string dbPath, IReadOnlyList<ModbusCommandLog> items, CancellationToken ct)
    {
        if (items.Count == 0) return;

        try
        {
            await using var conn = await DbInitializer.OpenConfiguredConnectionAsync(dbPath, ct).ConfigureAwait(false);
            await using var tx = await BeginImmediateTransactionAsync(conn, ct).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO ModbusCommandLog
                    (DeviceAddress, DeviceName, FunctionCode, RegisterAddress, CommandHex, ResponseHex, RttMs, IsSuccess, Timestamp)
                VALUES
                    ($addr, $name, $fc, $reg, $cmdHex, $respHex, $rtt, $ok, $ts);
                """;

            var pAddr = cmd.Parameters.Add("$addr", SqliteType.Integer);
            var pName = cmd.Parameters.Add("$name", SqliteType.Text);
            var pFc = cmd.Parameters.Add("$fc", SqliteType.Integer);
            var pReg = cmd.Parameters.Add("$reg", SqliteType.Integer);
            var pCmd = cmd.Parameters.Add("$cmdHex", SqliteType.Text);
            var pResp = cmd.Parameters.Add("$respHex", SqliteType.Text);
            var pRtt = cmd.Parameters.Add("$rtt", SqliteType.Real);
            var pOk = cmd.Parameters.Add("$ok", SqliteType.Integer);
            var pTs = cmd.Parameters.Add("$ts", SqliteType.Text);

            long totalBytes = 0;
            foreach (var item in items)
            {
                pAddr.Value = item.DeviceAddress;
                pName.Value = item.DeviceName;
                pFc.Value = item.FunctionCode;
                pReg.Value = item.RegisterAddress;
                pCmd.Value = item.CommandHex;
                pResp.Value = item.ResponseHex;
                pRtt.Value = item.RttMs;
                pOk.Value = item.IsSuccess;
                pTs.Value = item.Timestamp;

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                totalBytes += 140 + item.CommandHex.Length + item.ResponseHex.Length;
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);

            var first = items[0];
            _monitor.RecordWrite(
                SimulatedTableType.ModbusCommandLog,
                rowCount: items.Count,
                totalBytes,
                $"Dev={first.DeviceName} FC={first.FunctionCode} RTT={first.RttMs:F1}ms");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _monitor.RecordError(SimulatedTableType.ModbusCommandLog, "INSERT", ex.Message);
        }
    }

    public async Task WriteAlarmLogAsync(string dbPath, AlarmLog alarm, CancellationToken ct)
    {
        try
        {
            await using var conn = await DbInitializer.OpenConfiguredConnectionAsync(dbPath, ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO AlarmLog (CycleId, Severity, AlarmCode, Source, Description, IsAcknowledged, Timestamp)
                VALUES ($cycleId, $severity, $code, $source, $desc, $ack, $ts);
                """;
            cmd.Parameters.AddWithValue("$cycleId", (object?)alarm.CycleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$severity", alarm.Severity);
            cmd.Parameters.AddWithValue("$code", alarm.AlarmCode);
            cmd.Parameters.AddWithValue("$source", alarm.Source);
            cmd.Parameters.AddWithValue("$desc", alarm.Description);
            cmd.Parameters.AddWithValue("$ack", alarm.IsAcknowledged);
            cmd.Parameters.AddWithValue("$ts", alarm.Timestamp);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            long approxBytes = 128 + Encoding.UTF8.GetByteCount(alarm.Description);
            _monitor.RecordWrite(
                SimulatedTableType.AlarmLog,
                rowCount: 1,
                approxBytes,
                $"[{alarm.Severity}] {alarm.AlarmCode}: {alarm.Description}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _monitor.RecordError(SimulatedTableType.AlarmLog, "INSERT", ex.Message);
        }
    }

    public async Task WriteSensorReadingsBatchAsync(string dbPath, IReadOnlyList<SensorReading> readings, CancellationToken ct)
    {
        if (readings.Count == 0) return;

        try
        {
            await using var conn = await DbInitializer.OpenConfiguredConnectionAsync(dbPath, ct).ConfigureAwait(false);
            await using var tx = await BeginImmediateTransactionAsync(conn, ct).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO SensorReading (SensorId, SensorType, Value, Unit, IsInRange, Timestamp)
                VALUES ($id, $type, $val, $unit, $inRange, $ts);
                """;

            var pId = cmd.Parameters.Add("$id", SqliteType.Text);
            var pType = cmd.Parameters.Add("$type", SqliteType.Text);
            var pVal = cmd.Parameters.Add("$val", SqliteType.Real);
            var pUnit = cmd.Parameters.Add("$unit", SqliteType.Text);
            var pInRange = cmd.Parameters.Add("$inRange", SqliteType.Integer);
            var pTs = cmd.Parameters.Add("$ts", SqliteType.Text);

            long totalBytes = 0;
            foreach (var r in readings)
            {
                pId.Value = r.SensorId;
                pType.Value = r.SensorType;
                pVal.Value = r.Value;
                pUnit.Value = r.Unit;
                pInRange.Value = r.IsInRange;
                pTs.Value = r.Timestamp;

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                totalBytes += 96 + r.SensorId.Length + r.SensorType.Length;
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);

            var first = readings[0];
            _monitor.RecordWrite(
                SimulatedTableType.SensorReading,
                rowCount: readings.Count,
                totalBytes,
                $"{first.SensorId}={first.Value:F2}{first.Unit} (Batch={readings.Count})");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _monitor.RecordError(SimulatedTableType.SensorReading, "INSERT", ex.Message);
        }
    }

    /// <summary>
    /// 模擬上層 UI / 系統監控服務定期對 SQLite 進行查詢讀取 (與寫入形成真實 WAL 併發壓力)
    /// </summary>
    public async Task ExecuteReadSampleAsync(string dbPath, CancellationToken ct)
    {
        try
        {
            int queryIndex = Interlocked.Increment(ref _readCycleCursor) % 3;
            await using var conn = await DbInitializer.OpenConfiguredConnectionAsync(dbPath, ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();

            switch (queryIndex)
            {
                case 0:
                    cmd.CommandText = "SELECT Phase, StepName, ProgressPct FROM InstrumentState ORDER BY Id DESC LIMIT 10;";
                    await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                    {
                        int rows = 0;
                        while (await reader.ReadAsync(ct).ConfigureAwait(false)) rows++;
                        _monitor.RecordRead("InstrumentState", $"SELECT 最新 {rows} 筆狀態");
                    }
                    break;

                case 1:
                    cmd.CommandText = "SELECT DeviceName, AVG(RttMs), COUNT(*) FROM ModbusCommandLog WHERE Id > (SELECT COALESCE(MAX(Id),0) - 200 FROM ModbusCommandLog) GROUP BY DeviceName;";
                    await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                    {
                        int groups = 0;
                        while (await reader.ReadAsync(ct).ConfigureAwait(false)) groups++;
                        _monitor.RecordRead("ModbusCommandLog", $"SELECT 近200筆通訊RTT統計 ({groups} 裝置)");
                    }
                    break;

                default:
                    cmd.CommandText = "SELECT Severity, COUNT(*) FROM AlarmLog WHERE IsAcknowledged = 0 GROUP BY Severity;";
                    await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                    {
                        int severities = 0;
                        while (await reader.ReadAsync(ct).ConfigureAwait(false)) severities++;
                        _monitor.RecordRead("AlarmLog", $"SELECT 未確認警報統計 ({severities} 等級)");
                    }
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _monitor.RecordError(null, "SELECT", ex.Message);
        }
    }

    /// <summary>
    /// 取得 SQLite 資料庫總大小 (主檔 + WAL + SHM 位元組)
    /// </summary>
    public static long GetDatabaseTotalBytes(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath)) return 0;

        long total = 0;
        string[] paths = [dbPath, $"{dbPath}-wal", $"{dbPath}-shm"];
        foreach (string p in paths)
        {
            try
            {
                if (File.Exists(p))
                {
                    total += new FileInfo(p).Length;
                }
            }
            catch
            {
                // 檔案可能正被刪除或重建
            }
        }
        return total;
    }

    /// <summary>
    /// 取得目標資料庫所在磁碟的可用剩餘空間 (Bytes)
    /// </summary>
    public static long GetAvailableDiskFreeBytes(string dbPath)
    {
        try
        {
            string? fullPath = Path.GetFullPath(dbPath);
            string? root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrWhiteSpace(root))
            {
                return new DriveInfo(root).AvailableFreeSpace;
            }
        }
        catch
        {
            // 無效路徑或網路路徑則回傳 -1 表示未知
        }
        return -1;
    }

    private static async Task<SqliteTransaction> BeginImmediateTransactionAsync(SqliteConnection conn, CancellationToken ct)
    {
        // SQLite 在 WAL 併發下明確啟動 IMMEDIATE 鎖，能讓 busy_timeout 正確發揮等待效果
        return (SqliteTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct).ConfigureAwait(false);
    }
}

