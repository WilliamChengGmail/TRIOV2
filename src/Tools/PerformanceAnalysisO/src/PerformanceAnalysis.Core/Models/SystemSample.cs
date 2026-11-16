namespace PerformanceAnalysis.Core.Models;

public sealed record SystemSample(
    int Sequence,
    DateTime Timestamp,
    double CpuTotalPercent,
    double CpuSimulatorPercent,
    double CpuFreePercent,
    double MemoryUsedGb,
    double MemoryAvailableGb,
    double MemoryUsagePercent,
    double FrameBufferMb,
    string RecognitionState,
    double RecognitionDurationMs,
    string OperationMode);
