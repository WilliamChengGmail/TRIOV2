using System.Windows.Media;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class TaskRowViewModel
{
    public TaskRowViewModel(TaskLog task, int colorIndex)
    {
        Task = task;
        ColorHex = Palette.HexAt(colorIndex);
        Brush = Palette.BrushAt(colorIndex);
    }

    public TaskLog Task { get; }

    public string Name => Task.Name;

    public string ColorHex { get; }

    public Brush Brush { get; }

    public bool HasRuns => Task.Runs.Count > 0;

    public string StatusText => HasRuns ? "已執行" : "未執行";

    public string ActiveText => Formatting.Duration(Task.Aggregate.ActiveDuration);

    public string ActiveRatioText => $"{Task.Aggregate.ActiveRatio:P0}";

    public string CpuText
        => Task.Aggregate.Cpu.HasData
            ? $"{Formatting.Number(Task.Aggregate.Cpu.Mean)} / {Formatting.Number(Task.Aggregate.Cpu.Max)}"
            : "—";

    public string MemoryText
        => Task.Aggregate.MemoryMb.HasData
            ? $"{Formatting.Number(Task.Aggregate.MemoryMb.Mean)} / {Formatting.Number(Task.Aggregate.MemoryMb.Max)}"
            : "—";

    public string DiskText => Formatting.Rate(Task.Aggregate.DiskKbPerSecond.Max, "KB/s");

    public string NetworkText => Formatting.Rate(Task.Aggregate.NetworkKbps.Max, "Kbps");

    public string RunsText => $"{Task.Aggregate.RunCount} / {Task.Aggregate.RestartCount}";

    public string CpuSecondsText => Formatting.Number(Task.Aggregate.CpuSeconds);
}
