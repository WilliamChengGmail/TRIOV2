using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 解析 TRIOV2 ref.txt 之完整結構化資料與系統硬體／作業系統規格快照
/// 製作者: Office of William
/// </summary>
public sealed class RefParsedResult
{
    public DateTime? MeasuredAt { get; init; }
    public int? SampleCount { get; init; }
    public TimeSpan? Duration { get; init; }

    public double? CpuMax { get; init; }
    public double? CpuMin { get; init; }
    public double? CpuAvg { get; init; }
    public double? RamMaxGb { get; init; }
    public double? RamMaxPct { get; init; }
    public double? RamMinGb { get; init; }
    public double? RamMinPct { get; init; }
    public double? RamAvgGb { get; init; }
    public double? RamAvgPct { get; init; }

    public DeviceProfile HardwareProfile { get; init; } = new();
    public string RawText { get; init; } = string.Empty;
}

public static class RefSummaryParser
{
    private static readonly Regex SampleDurationRegex = new(@"共\s*(\d+)\s*筆樣本.*?(\d+):(\d+)", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"[-+]?\d+(?:\.\d+)?", RegexOptions.Compiled);

    public static RefParsedResult ParseFile(string refFilePath)
    {
        string raw = File.ReadAllText(refFilePath);
        return ParseText(raw);
    }

    public static RefParsedResult ParseText(string rawText)
    {
        DateTime? measuredAt = null;
        int? sampleCount = null;
        TimeSpan? duration = null;

        double? cpuMax = null, cpuMin = null, cpuAvg = null;
        double? ramMaxGb = null, ramMaxPct = null, ramMinGb = null, ramMinPct = null, ramAvgGb = null, ramAvgPct = null;

        string cpuModel = "未知 CPU", cpuBaseSpeed = "--", cpuSockets = "--", cpuCores = "--", cpuLogical = "--", virt = "--";
        string l1 = "--", l2 = "--", l3 = "--";
        string memSpec = "--", memSpeed = "--", memSlots = "--", memFormFactor = "--", memReserved = "--", memInstalled = "--";
        double memInstalledGb = 0;
        var npus = new List<string>();
        var gpus = new List<string>();
        string totalProcText = "--";
        string osName = "未知 OS", osVersion = "--", osBuild = "--", osArch = "--", machineName = "未知設備", uptime = "--";

        string mainSection = string.Empty;
        string hwSubSection = string.Empty;

        string[] lines = rawText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("====") || line.StartsWith("----"))
            {
                continue;
            }

            if (line.StartsWith("【") && line.EndsWith("】"))
            {
                mainSection = line;
                hwSubSection = string.Empty;
                continue;
            }

            if (line.StartsWith("量測時間："))
            {
                string val = line["量測時間：".Length..].Trim();
                if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                {
                    measuredAt = dt;
                }
                continue;
            }

            if (line.StartsWith("樣本數量："))
            {
                var m = SampleDurationRegex.Match(line);
                if (m.Success)
                {
                    if (int.TryParse(m.Groups[1].Value, out int cnt)) sampleCount = cnt;
                    if (int.TryParse(m.Groups[2].Value, out int min) && int.TryParse(m.Groups[3].Value, out int sec))
                    {
                        duration = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
                    }
                }
                continue;
            }

            if (line.StartsWith("全系統處理器 CPU"))
            {
                hwSubSection = "SYS_CPU";
                continue;
            }
            if (line.StartsWith("全系統實體記憶體 RAM"))
            {
                hwSubSection = "SYS_RAM";
                continue;
            }

            if (hwSubSection == "SYS_CPU" && line.StartsWith("-"))
            {
                double? num = ExtractFirstDouble(line);
                if (line.Contains("最高")) cpuMax = num;
                else if (line.Contains("最低")) cpuMin = num;
                else if (line.Contains("平均")) cpuAvg = num;
                continue;
            }

            if (hwSubSection == "SYS_RAM" && line.StartsWith("-"))
            {
                var nums = ExtractAllDoubles(line);
                if (nums.Count >= 1)
                {
                    if (line.Contains("最高")) { ramMaxGb = nums[0]; if (nums.Count >= 2) ramMaxPct = nums[1]; }
                    else if (line.Contains("最低")) { ramMinGb = nums[0]; if (nums.Count >= 2) ramMinPct = nums[1]; }
                    else if (line.Contains("平均")) { ramAvgGb = nums[0]; if (nums.Count >= 2) ramAvgPct = nums[1]; }
                }
                continue;
            }

            if (mainSection.Contains("系統硬體規格"))
            {
                if (line.StartsWith("► "))
                {
                    hwSubSection = line;
                    continue;
                }

                if (!line.StartsWith("-")) continue;

                string itemText = line[1..].Trim();

                if (hwSubSection.Contains("NPU"))
                {
                    npus.Add(itemText);
                    continue;
                }

                if (hwSubSection.Contains("GPU"))
                {
                    gpus.Add(itemText);
                    continue;
                }

                int colonIdx = itemText.IndexOf(':');
                if (colonIdx <= 0) continue;

                string key = itemText[..colonIdx].Trim();
                string value = itemText[(colonIdx + 1)..].Trim();

                if (hwSubSection.Contains("CPU"))
                {
                    switch (key)
                    {
                        case "型號": cpuModel = value; break;
                        case "基本速度": cpuBaseSpeed = value; break;
                        case "插槽": cpuSockets = value; break;
                        case "核心數目": cpuCores = value; break;
                        case "邏輯處理器": cpuLogical = value; break;
                        case "模擬": virt = value; break;
                        case "L1 快取": l1 = value; break;
                        case "L2 快取": l2 = value; break;
                        case "L3 快取": l3 = value; break;
                    }
                }
                else if (hwSubSection.Contains("記憶體"))
                {
                    switch (key)
                    {
                        case "記憶體規格": memSpec = value; break;
                        case "速度": memSpeed = value; break;
                        case "已使用插槽": memSlots = value; break;
                        case "尺寸": memFormFactor = value; break;
                        case "硬體保留": memReserved = value; break;
                        case "已安裝容量":
                            memInstalled = value;
                            memInstalledGb = ExtractFirstDouble(value) ?? 0;
                            break;
                    }
                }
                else if (hwSubSection.Contains("系統程序總數"))
                {
                    if (key == "當前所有處理程序總數") totalProcText = value;
                }
                else if (hwSubSection.Contains("作業系統"))
                {
                    switch (key)
                    {
                        case "作業系統名稱": osName = value; break;
                        case "版本": osVersion = value; break;
                        case "OS 組建": osBuild = value; break;
                        case "系統類型": osArch = value; break;
                        case "裝置名稱": machineName = value; break;
                        case "已開機時間": uptime = value; break;
                    }
                }
            }
        }

        var profile = new DeviceProfile
        {
            HasRefTxt = true,
            MachineName = machineName,
            CpuModel = cpuModel,
            CpuBaseSpeed = cpuBaseSpeed,
            CpuSockets = cpuSockets,
            CpuCores = cpuCores,
            CpuLogicalProcessors = cpuLogical,
            Virtualization = virt,
            L1Cache = l1,
            L2Cache = l2,
            L3Cache = l3,
            MemorySpecType = memSpec,
            MemorySpeed = memSpeed,
            MemorySlotsUsed = memSlots,
            MemoryFormFactor = memFormFactor,
            MemoryHardwareReserved = memReserved,
            MemoryInstalledText = memInstalled,
            MemoryInstalledGb = memInstalledGb,
            NpuSummary = npus.Count > 0 ? string.Join("；", npus) : "未偵測到獨立 NPU 裝置",
            GpuSummary = gpus.Count > 0 ? string.Join(" | ", gpus.Select(StripGpuPrefix)) : "未回報 GPU",
            TotalProcessesText = totalProcText,
            OsName = osName,
            OsVersion = osVersion,
            OsBuild = osBuild,
            OsArchitecture = osArch,
            UptimeText = uptime
        };

        return new RefParsedResult
        {
            MeasuredAt = measuredAt,
            SampleCount = sampleCount,
            Duration = duration,
            CpuMax = cpuMax,
            CpuMin = cpuMin,
            CpuAvg = cpuAvg,
            RamMaxGb = ramMaxGb,
            RamMaxPct = ramMaxPct,
            RamMinGb = ramMinGb,
            RamMinPct = ramMinPct,
            RamAvgGb = ramAvgGb,
            RamAvgPct = ramAvgPct,
            HardwareProfile = profile,
            RawText = rawText
        };
    }

    private static string StripGpuPrefix(string gpuLine)
    {
        int closeBracket = gpuLine.IndexOf(']');
        if (gpuLine.StartsWith("[") && closeBracket > 0 && closeBracket + 1 < gpuLine.Length)
        {
            return gpuLine[(closeBracket + 1)..].Trim();
        }
        return gpuLine;
    }

    private static double? ExtractFirstDouble(string text)
    {
        var m = NumberRegex.Match(text);
        if (m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            return v;
        }
        return null;
    }

    private static List<double> ExtractAllDoubles(string text)
    {
        var list = new List<double>();
        foreach (Match m in NumberRegex.Matches(text))
        {
            if (double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                list.Add(v);
            }
        }
        return list;
    }
}

