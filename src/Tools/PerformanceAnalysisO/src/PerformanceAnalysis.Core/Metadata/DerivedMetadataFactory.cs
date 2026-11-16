using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Metadata;

public static class DerivedMetadataFactory
{
    public static SessionMetadata Create(
        string sessionKey,
        string folderName,
        DeviceProfile device,
        DateTime start,
        DateTime end,
        string? mode,
        IEnumerable<string> taskNames)
    {
        return new SessionMetadata
        {
            SchemaVersion = 1,
            SessionKey = sessionKey,
            SourceFolderName = folderName,
            Source = "derived",
            DeviceId = device.DeviceKey,
            DeviceName = device.DeviceName,
            Alias = device.Alias,
            StartTime = ToOffset(start),
            EndTime = ToOffset(end),
            Mode = mode,
            Tasks = taskNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Hardware = new HardwareInfo
            {
                Cpu = device.CpuModel,
                Cores = device.Cores,
                LogicalCpus = device.LogicalCpus,
                RamGB = device.RamGb,
                RamSpeed = device.RamSpeed,
                OsName = device.OsName,
                OsVersion = device.OsVersion,
                OsBuild = device.OsBuild,
                Gpu = device.Gpu
            },
            UpdatedAt = DateTimeOffset.Now
        };
    }

    private static DateTimeOffset ToOffset(DateTime value)
        => value.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(value, TimeZoneInfo.Local.GetUtcOffset(value))
            : new DateTimeOffset(value);
}
