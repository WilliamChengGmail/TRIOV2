namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// System_BenchmarkLog.csv 單秒採樣資料點
/// </summary>
public sealed class SystemSamplePoint
{
    public int Sequence { get; init; }
    public DateTime Timestamp { get; init; }
    public double ElapsedSeconds { get; init; }
    public double SystemCpuPercent { get; init; }
    public double SimulatorCpuPercent { get; init; }
    public double IdleCpuPercent { get; init; }
    public double UsedMemoryGb { get; init; }
    public double AvailableMemoryGb { get; init; }
    public double MemoryPercent { get; init; }
    public double BufferMb { get; init; }
    public string RecognitionStatus { get; init; } = "待機";
    public double RecognitionDurationMs { get; init; }
    public string WorkMode { get; init; } = string.Empty;

    public bool IsRecognizing =>
        !string.IsNullOrWhiteSpace(RecognitionStatus) &&
        !string.Equals(RecognitionStatus, "待機", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// {TaskName}_BenchmarkLog.csv 單秒採樣資料點
/// </summary>
public sealed class TaskSamplePoint
{
    public int Sequence { get; init; }
    public DateTime Timestamp { get; init; }
    public double ElapsedSeconds { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public int Pid { get; init; }
    public string Status { get; init; } = string.Empty;
    public double CpuPercent { get; init; }
    public double MemoryMb { get; init; }
    public double DiskKbps { get; init; }
    public double NetworkKbps { get; init; }

    public bool IsActive =>
        Pid > 0 ||
        string.Equals(Status, "執行中", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "Running", StringComparison.OrdinalIgnoreCase);
}

