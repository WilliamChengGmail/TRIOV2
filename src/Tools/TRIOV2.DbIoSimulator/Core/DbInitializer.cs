using System.IO;
using Microsoft.Data.Sqlite;
using TRIOV2.DbIoSimulator.Core.Schema;

namespace TRIOV2.DbIoSimulator.Core;

/// <summary>
/// 負責建立、初始化與重置 TRIO V2 模擬 SQLite 資料庫 (啟用 WAL 模式支援高並行讀寫)
/// 製作者: Office of William
/// </summary>
public static class DbInitializer
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS InstrumentState (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CycleId TEXT NOT NULL,
            Phase TEXT NOT NULL,
            StepIndex INTEGER NOT NULL,
            StepName TEXT NOT NULL,
            ProgressPct REAL NOT NULL,
            IsRunning INTEGER NOT NULL,
            Timestamp TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_InstrumentState_Timestamp ON InstrumentState(Timestamp);

        CREATE TABLE IF NOT EXISTS DeckDetectionLog (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CycleId TEXT NOT NULL,
            SlotIndex INTEGER NOT NULL,
            SlotLabel TEXT NOT NULL,
            SamplePresent INTEGER NOT NULL,
            ReagentPresent INTEGER NOT NULL,
            ConfidenceScore REAL NOT NULL,
            AnalysisResultJson TEXT NOT NULL,
            CameraId TEXT NOT NULL,
            Timestamp TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_DeckDetectionLog_Timestamp ON DeckDetectionLog(Timestamp);

        CREATE TABLE IF NOT EXISTS ModbusCommandLog (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DeviceAddress INTEGER NOT NULL,
            DeviceName TEXT NOT NULL,
            FunctionCode INTEGER NOT NULL,
            RegisterAddress INTEGER NOT NULL,
            CommandHex TEXT NOT NULL,
            ResponseHex TEXT NOT NULL,
            RttMs REAL NOT NULL,
            IsSuccess INTEGER NOT NULL,
            Timestamp TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_ModbusCommandLog_Timestamp ON ModbusCommandLog(Timestamp);

        CREATE TABLE IF NOT EXISTS AlarmLog (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CycleId TEXT NULL,
            Severity TEXT NOT NULL,
            AlarmCode TEXT NOT NULL,
            Source TEXT NOT NULL,
            Description TEXT NOT NULL,
            IsAcknowledged INTEGER NOT NULL,
            Timestamp TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_AlarmLog_Timestamp ON AlarmLog(Timestamp);

        CREATE TABLE IF NOT EXISTS SensorReading (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SensorId TEXT NOT NULL,
            SensorType TEXT NOT NULL,
            Value REAL NOT NULL,
            Unit TEXT NOT NULL,
            IsInRange INTEGER NOT NULL,
            Timestamp TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_SensorReading_Timestamp ON SensorReading(Timestamp);
        """;

    /// <summary>
    /// 建置統一的 SQLite 連線字串 (含 Shared Cache 與預設逾時設定)
    /// </summary>
    public static string BuildConnectionString(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5
        };
        return builder.ToString();
    }

    /// <summary>
    /// 開啟並套用 SQLite 連線層級效能 PRAGMA (synchronous 與 busy_timeout)
    /// </summary>
    public static async Task<SqliteConnection> OpenConfiguredConnectionAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        var conn = new SqliteConnection(BuildConnectionString(dbPath));
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = """
            PRAGMA synchronous = NORMAL;
            PRAGMA busy_timeout = 5000;
            PRAGMA temp_store = MEMORY;
            """;
        await pragmaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }

    /// <summary>
    /// 初始化目標資料庫檔案、WAL 模式與五張模擬資料表
    /// </summary>
    public static async Task InitializeAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new ArgumentException("資料庫路徑不可為空白", nameof(dbPath));
        }

        string? dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var conn = await OpenConfiguredConnectionAsync(dbPath, cancellationToken).ConfigureAwait(false);
        await using var walCmd = conn.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode = WAL;";
        await walCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SchemaSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 查詢五張資料表目前的資料筆數
    /// </summary>
    public static async Task<Dictionary<SimulatedTableType, long>> GetExistingRowCountsAsync(
        string dbPath,
        CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<SimulatedTableType, long>();
        foreach (SimulatedTableType table in Enum.GetValues<SimulatedTableType>())
        {
            counts[table] = 0;
        }

        if (!File.Exists(dbPath))
        {
            return counts;
        }

        await using var conn = await OpenConfiguredConnectionAsync(dbPath, cancellationToken).ConfigureAwait(false);
        foreach (SimulatedTableType table in Enum.GetValues<SimulatedTableType>())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
            object? scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            counts[table] = scalar is long l ? l : Convert.ToInt64(scalar ?? 0L);
        }

        return counts;
    }

    /// <summary>
    /// 安全清除並重建資料庫檔案 (含 -wal / -shm 附屬檔)
    /// </summary>
    public static async Task ResetDatabaseAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        SqliteConnection.ClearAllPools();

        // 稍微等待 Windows 檔案系統釋放句柄，必要時進行重試
        string[] candidateFiles = [dbPath, $"{dbPath}-wal", $"{dbPath}-shm", $"{dbPath}-journal"];
        foreach (string file in candidateFiles)
        {
            if (File.Exists(file))
            {
                await DeleteFileWithRetryAsync(file, cancellationToken).ConfigureAwait(false);
            }
        }

        await InitializeAsync(dbPath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteFileWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                SqliteConnection.ClearAllPools();
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
