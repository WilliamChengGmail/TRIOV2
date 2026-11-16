using System.Globalization;
using System.IO;
using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 高速串流解析 System_BenchmarkLog.csv 與各任務 {TaskName}_BenchmarkLog.csv
/// 製作者: Office of William
/// </summary>
public static class BenchmarkCsvParser
{
    private static readonly string[] Palette =
    [
        "#F59E0B", // Amber
        "#A855F7", // Purple
        "#EC4899", // Pink
        "#14B8A6", // Teal
        "#F97316", // Orange
        "#6366F1", // Indigo
        "#84CC16", // Lime
        "#06B6D4"  // Cyan
    ];

    public static List<SystemSamplePoint> ParseSystemCsv(string filePath)
    {
        var list = new List<SystemSamplePoint>();
        if (!File.Exists(filePath)) return list;

        DateTime? firstTimestamp = null;

        using var reader = new StreamReader(filePath);
        string? header = reader.ReadLine();
        if (header == null) return list;

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] cols = line.Split(',');
            if (cols.Length < 8) continue;

            _ = int.TryParse(cols[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seq);
            DateTime ts = ParseTimestamp(cols[1].Trim());
            firstTimestamp ??= ts;

            double elapsed = firstTimestamp.Value != DateTime.MinValue && ts != DateTime.MinValue
                ? Math.Max(0, (ts - firstTimestamp.Value).TotalSeconds)
                : Math.Max(0, seq - 1);

            list.Add(new SystemSamplePoint
            {
                Sequence = seq,
                Timestamp = ts,
                ElapsedSeconds = elapsed,
                SystemCpuPercent = ParseDouble(cols, 2),
                SimulatorCpuPercent = ParseDouble(cols, 3),
                IdleCpuPercent = ParseDouble(cols, 4),
                UsedMemoryGb = ParseDouble(cols, 5),
                AvailableMemoryGb = ParseDouble(cols, 6),
                MemoryPercent = ParseDouble(cols, 7),
                BufferMb = ParseDouble(cols, 8),
                RecognitionStatus = cols.Length > 9 ? cols[9].Trim() : "待機",
                RecognitionDurationMs = ParseDouble(cols, 10),
                WorkMode = cols.Length > 11 ? cols[11].Trim() : string.Empty
            });
        }

        return list;
    }

    public static TaskBenchmarkSummary ParseTaskCsv(string filePath, string taskNameHint, int colorIndex)
    {
        var samples = new List<TaskSamplePoint>();
        DateTime? firstTimestamp = null;
        string resolvedTaskName = taskNameHint;

        if (File.Exists(filePath))
        {
            using var reader = new StreamReader(filePath);
            string? header = reader.ReadLine();
            if (header != null)
            {
                while (reader.ReadLine() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] cols = line.Split(',');
                    if (cols.Length < 7) continue;

                    _ = int.TryParse(cols[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seq);
                    DateTime ts = ParseTimestamp(cols[1].Trim());
                    firstTimestamp ??= ts;

                    string procName = cols[2].Trim();
                    if (!string.IsNullOrWhiteSpace(procName)) resolvedTaskName = procName;

                    _ = int.TryParse(cols[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid);
                    string status = cols[4].Trim();

                    double elapsed = firstTimestamp.Value != DateTime.MinValue && ts != DateTime.MinValue
                        ? Math.Max(0, (ts - firstTimestamp.Value).TotalSeconds)
                        : Math.Max(0, seq - 1);

                    samples.Add(new TaskSamplePoint
                    {
                        Sequence = seq,
                        Timestamp = ts,
                        ElapsedSeconds = elapsed,
                        ProcessName = procName,
                        Pid = pid,
                        Status = status,
                        CpuPercent = ParseDouble(cols, 5),
                        MemoryMb = ParseDouble(cols, 6),
                        DiskKbps = ParseDouble(cols, 7),
                        NetworkKbps = ParseDouble(cols, 8)
                    });
                }
            }
        }

        // 全時段與 Active-Only 雙軌計算
        var allCpus = samples.Select(s => s.CpuPercent).ToArray();
        var allRams = samples.Select(s => s.MemoryMb).ToArray();
        var allDisks = samples.Select(s => s.DiskKbps).ToArray();
        var allNets = samples.Select(s => s.NetworkKbps).ToArray();

        var activeList = samples.Where(s => s.IsActive).ToList();
        var activeCpus = activeList.Select(s => s.CpuPercent).ToArray();
        var activeRams = activeList.Select(s => s.MemoryMb).ToArray();
        var activeDisks = activeList.Select(s => s.DiskKbps).ToArray();
        var activeNets = activeList.Select(s => s.NetworkKbps).ToArray();

        var allCpuStats = StatisticsHelper.ComputeDistribution(allCpus);
        var allRamStats = StatisticsHelper.ComputeDistribution(allRams);
        var activeCpuStats = StatisticsHelper.ComputeDistribution(activeCpus);
        var activeRamStats = StatisticsHelper.ComputeDistribution(activeRams);
        var activeDiskStats = StatisticsHelper.ComputeDistribution(activeDisks);

        string colorHex = Palette[colorIndex % Palette.Length];

        // 偵測程式執行的所有起訖時段（精準支援執行後關閉、又再執行的多次重啟情境）
        var executionRuns = new List<TaskExecutionRun>();
        var curRunPts = new List<TaskSamplePoint>();
        int runIdx = 1;
        int curPid = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            var pt = samples[i];
            if (pt.IsActive)
            {
                if (curRunPts.Count == 0)
                {
                    curRunPts.Add(pt);
                    curPid = pt.Pid;
                }
                else if (pt.Pid > 0 && curPid > 0 && pt.Pid != curPid)
                {
                    executionRuns.Add(BuildExecutionRun(resolvedTaskName, runIdx++, curPid, curRunPts, colorHex));
                    curRunPts.Clear();
                    curRunPts.Add(pt);
                    curPid = pt.Pid;
                }
                else
                {
                    curRunPts.Add(pt);
                    if (pt.Pid > 0 && curPid == 0) curPid = pt.Pid;
                }
            }
            else
            {
                if (curRunPts.Count > 0)
                {
                    executionRuns.Add(BuildExecutionRun(resolvedTaskName, runIdx++, curPid, curRunPts, colorHex));
                    curRunPts.Clear();
                    curPid = 0;
                }
            }
        }

        if (curRunPts.Count > 0)
        {
            executionRuns.Add(BuildExecutionRun(resolvedTaskName, runIdx++, curPid, curRunPts, colorHex));
            curRunPts.Clear();
        }

        return new TaskBenchmarkSummary
        {
            TaskName = resolvedTaskName,
            TotalSamples = samples.Count,
            ActiveSamples = activeList.Count,
            AllCpuAvg = allCpuStats.Avg,
            AllCpuMin = allCpuStats.Min,
            AllCpuMax = allCpuStats.Max,
            AllCpuP95 = allCpuStats.P95,
            AllRamAvgMb = allRamStats.Avg,
            AllRamMaxMb = allRamStats.Max,
            AllDiskPeakKbps = allDisks.Length > 0 ? allDisks.Max() : 0,
            AllNetPeakKbps = allNets.Length > 0 ? allNets.Max() : 0,
            ActiveCpuAvg = activeCpuStats.Avg,
            ActiveCpuP95 = activeCpuStats.P95,
            ActiveCpuMax = activeCpuStats.Max,
            ActiveRamAvgMb = activeRamStats.Avg,
            ActiveRamMaxMb = activeRamStats.Max,
            ActiveDiskAvgKbps = activeDiskStats.Avg,
            ActiveDiskPeakKbps = activeDisks.Length > 0 ? activeDisks.Max() : 0,
            ActiveNetPeakKbps = activeNets.Length > 0 ? activeNets.Max() : 0,
            ColorHex = colorHex,
            Samples = samples,
            ExecutionRuns = executionRuns
        };
    }

    private static TaskExecutionRun BuildExecutionRun(
        string taskName, int runIndex, int pid, List<TaskSamplePoint> pts, string colorHex)
    {
        var first = pts[0];
        var last = pts[^1];

        var cpus = pts.Select(p => p.CpuPercent).ToArray();
        var rams = pts.Select(p => p.MemoryMb).ToArray();
        var disks = pts.Select(p => p.DiskKbps).ToArray();
        var nets = pts.Select(p => p.NetworkKbps).ToArray();

        var cpuStats = StatisticsHelper.ComputeDistribution(cpus);
        var ramStats = StatisticsHelper.ComputeDistribution(rams);
        var diskStats = StatisticsHelper.ComputeDistribution(disks);

        int sampleCount = pts.Count;
        TimeSpan duration;
        if (first.Timestamp != DateTime.MinValue && last.Timestamp != DateTime.MinValue && last.Timestamp >= first.Timestamp)
        {
            duration = (last.Timestamp - first.Timestamp) + TimeSpan.FromSeconds(1);
        }
        else
        {
            duration = TimeSpan.FromSeconds(sampleCount);
        }

        return new TaskExecutionRun
        {
            TaskName = taskName,
            RunIndex = runIndex,
            Pid = pid > 0 ? pid : first.Pid,
            StartTime = first.Timestamp,
            EndTime = last.Timestamp,
            StartElapsedSeconds = first.ElapsedSeconds,
            EndElapsedSeconds = last.ElapsedSeconds,
            Duration = duration,
            SampleCount = sampleCount,
            CpuAvg = cpuStats.Avg,
            CpuMax = cpuStats.Max,
            CpuP95 = cpuStats.P95,
            RamAvgMb = ramStats.Avg,
            RamMaxMb = ramStats.Max,
            DiskPeakKbps = disks.Length > 0 ? disks.Max() : 0,
            NetPeakKbps = nets.Length > 0 ? nets.Max() : 0,
            ColorHex = colorHex
        };
    }

    private static DateTime ParseTimestamp(string text)
    {
        if (DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var exact))
        {
            return exact;
        }
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }
        return DateTime.MinValue;
    }

    private static double ParseDouble(string[] cols, int index)
    {
        if (index >= cols.Length) return 0;
        return double.TryParse(cols[index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
            ? v
            : 0;
    }
}

