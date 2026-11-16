namespace PerformanceAnalysis.Core.Models;

public sealed record DeviceProfile(
    string DeviceKey,
    string DeviceName,
    string? Alias,
    string? CpuModel,
    int? Cores,
    int? LogicalCpus,
    double? RamGb,
    string? RamSpeed,
    string? OsName,
    string? OsVersion,
    string? OsBuild,
    string? Gpu,
    string Fingerprint,
    bool IsIdentified)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? DeviceName : Alias!;
}
