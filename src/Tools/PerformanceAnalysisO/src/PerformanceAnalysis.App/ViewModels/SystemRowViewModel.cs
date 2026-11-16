using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class SystemRowViewModel
{
    public SystemRowViewModel(SystemSample sample)
    {
        Sample = sample;
    }

    public SystemSample Sample { get; }

    public string TimestampText => Sample.Timestamp.ToString("MM-dd HH:mm:ss");

    public string CpuTotalText => Formatting.Number(Sample.CpuTotalPercent);

    public string CpuSimulatorText => Formatting.Number(Sample.CpuSimulatorPercent);

    public string CpuFreeText => Formatting.Number(Sample.CpuFreePercent);

    public string MemoryUsedText => Formatting.Number(Sample.MemoryUsedGb, "0.00");

    public string MemoryAvailableText => Formatting.Number(Sample.MemoryAvailableGb, "0.00");

    public string MemoryPercentText => Formatting.Number(Sample.MemoryUsagePercent, "0");

    public string FrameBufferText => Formatting.Number(Sample.FrameBufferMb, "0");

    public string RecognitionState => Sample.RecognitionState;

    public string RecognitionMsText => Formatting.Number(Sample.RecognitionDurationMs, "0");

    public string Mode => Sample.OperationMode;
}
