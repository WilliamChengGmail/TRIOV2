namespace TRIOV2.PerformanceAnalyzer.Models;

/// <summary>
/// 單台測試設備之硬體與作業系統規格快照（解析自 ref.txt 或由 CSV 推估）
/// 製作者: Office of William
/// </summary>
public sealed class DeviceProfile
{
    public bool HasRefTxt { get; init; }
    public string MachineName { get; init; } = "未知設備";
    public string CpuModel { get; init; } = "未知 CPU";
    public string CpuBaseSpeed { get; init; } = "--";
    public string CpuSockets { get; init; } = "--";
    public string CpuCores { get; init; } = "--";
    public string CpuLogicalProcessors { get; init; } = "--";
    public string Virtualization { get; init; } = "--";
    public string L1Cache { get; init; } = "--";
    public string L2Cache { get; init; } = "--";
    public string L3Cache { get; init; } = "--";

    public string MemorySpecType { get; init; } = "--";
    public string MemorySpeed { get; init; } = "--";
    public string MemorySlotsUsed { get; init; } = "--";
    public string MemoryFormFactor { get; init; } = "--";
    public string MemoryHardwareReserved { get; init; } = "--";
    public string MemoryInstalledText { get; init; } = "--";
    public double MemoryInstalledGb { get; init; }

    public string NpuSummary { get; init; } = "未知";
    public string GpuSummary { get; init; } = "未知";
    public string TotalProcessesText { get; init; } = "--";

    public string OsName { get; init; } = "未知 OS";
    public string OsVersion { get; init; } = "--";
    public string OsBuild { get; init; } = "--";
    public string OsArchitecture { get; init; } = "--";
    public string UptimeText { get; init; } = "--";

    public string ShortCpuLabel => string.IsNullOrWhiteSpace(CpuCores) || CpuCores == "--"
        ? CpuModel
        : $"{CpuModel} ({CpuCores}C/{CpuLogicalProcessors}T)";

    public string ShortMemoryLabel => MemoryInstalledGb > 0
        ? $"{MemoryInstalledGb:0.#} GB {MemorySpecType}".Trim()
        : MemoryInstalledText;

    public string ShortOsLabel => OsVersion != "--" && !string.IsNullOrWhiteSpace(OsVersion)
        ? $"{OsName} ({OsVersion})"
        : OsName;

    public string ComputeDeviceKey()
    {
        if (HasRefTxt && !string.Equals(MachineName, "未知設備", StringComparison.OrdinalIgnoreCase))
        {
            return $"REF|{MachineName}|{CpuModel}|{MemoryInstalledGb:0.#}|{OsName}";
        }

        if (HasRefTxt)
        {
            return $"HW|{CpuModel}|{MemoryInstalledGb:0.#}|{OsName}";
        }

        return $"NOREF|{MemoryInstalledGb:0.#}GB";
    }
}

