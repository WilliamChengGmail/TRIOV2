using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Configuration;
using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Models;
using PerformanceAnalysis.Core.Reporting;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        if (!string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"未知命令：{args[0]}");
            PrintUsage();
            return 2;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("請指定要掃描的路徑。");
            PrintUsage();
            return 2;
        }

        var path = args[1];
        string? jsonPath = null;
        string? htmlPath = null;
        string? metadataDirectory = null;
        var writeMetadata = false;
        var verbose = false;
        var showInfo = false;

        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    if (++i >= args.Length)
                    {
                        Console.Error.WriteLine("--json 需要一個輸出檔案路徑。");
                        return 2;
                    }

                    jsonPath = args[i];
                    break;

                case "--html":
                    if (++i >= args.Length)
                    {
                        Console.Error.WriteLine("--html 需要一個輸出檔案路徑。");
                        return 2;
                    }

                    htmlPath = args[i];
                    break;

                case "--metadata-dir":
                    if (++i >= args.Length)
                    {
                        Console.Error.WriteLine("--metadata-dir 需要一個資料夾路徑。");
                        return 2;
                    }

                    metadataDirectory = args[i];
                    break;

                case "--write-metadata":
                    writeMetadata = true;
                    break;

                case "--verbose":
                    verbose = true;
                    break;

                case "--show-info":
                    showInfo = true;
                    break;

                default:
                    Console.Error.WriteLine($"未知選項：{args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        var stopwatch = Stopwatch.StartNew();
        var scan = SessionScanner.Scan(path);

        if (scan.Candidates.Count == 0)
        {
            Console.Error.WriteLine($"在 {scan.RootPath} 找不到任何場次。");
            PrintDiagnostics(scan.Diagnostics, showInfo);
            return 1;
        }

        var metadataStore = new SessionMetadataStore(metadataDirectory);
        var metadataRoot = metadataDirectory ?? Path.Combine(AppContext.BaseDirectory, "Metadata");
        var analysisOptions = new AnalysisSettingsStore(metadataRoot).Load();
        var analyzer = new SessionAnalyzer(metadataStore, writeMetadata, analysisOptions);
        var sessions = new List<Session>();
        var failures = new List<(string Path, string Message)>();

        foreach (var candidate in scan.Candidates)
        {
            try
            {
                sessions.Add(analyzer.Analyze(candidate));
            }
            catch (Exception ex)
            {
                failures.Add((candidate.Path, ex.Message));
            }
        }

        sessions.Sort((a, b) => b.StartTime.CompareTo(a.StartTime));
        stopwatch.Stop();

        PrintOverview(scan, sessions, failures, stopwatch.Elapsed, verbose, showInfo);

        if (jsonPath is not null)
        {
            try
            {
                var report = new
                {
                    schemaVersion = 1,
                    generatedAt = DateTimeOffset.Now,
                    sourcePath = scan.RootPath,
                    sessionCount = sessions.Count,
                    deviceCount = sessions.Select(session => session.Device.DeviceKey).Distinct().Count(),
                    taskCatalog = sessions
                        .SelectMany(session => session.Tasks.Select(task => task.Name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    sessions,
                    scanDiagnostics = scan.Diagnostics,
                    failures
                };

                var fullPath = Path.GetFullPath(jsonPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                File.WriteAllText(
                    fullPath,
                    JsonSerializer.Serialize(report, JsonOptions),
                    new UTF8Encoding(false));
                Console.WriteLine();
                Console.WriteLine($"JSON 已輸出：{fullPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"JSON 輸出失敗：{ex.Message}");
                return 1;
            }
        }

        if (htmlPath is not null)
        {
            try
            {
                var fullPath = Path.GetFullPath(htmlPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                var html = HtmlReportBuilder.Build(sessions, scan.RootPath);
                File.WriteAllText(fullPath, html, new UTF8Encoding(false));
                Console.WriteLine();
                Console.WriteLine($"HTML 報告已輸出：{fullPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"HTML 輸出失敗：{ex.Message}");
                return 1;
            }
        }

        var hasErrors = failures.Count > 0
                        || sessions.SelectMany(session => session.Diagnostics)
                            .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        return hasErrors ? 1 : 0;
    }

    private static void PrintOverview(
        ScanResult scan,
        IReadOnlyList<Session> sessions,
        IReadOnlyList<(string Path, string Message)> failures,
        TimeSpan elapsed,
        bool verbose,
        bool showInfo)
    {
        Console.WriteLine();
        Console.WriteLine("TRIOV2 效能分析 — 掃描結果");
        Console.WriteLine($"來源：{scan.RootPath}");
        Console.WriteLine(
            $"場次 {sessions.Count}｜裝置 {sessions.Select(session => session.Device.DeviceKey).Distinct().Count()}｜"
            + $"任務 {sessions.SelectMany(session => session.Tasks.Select(task => task.Name)).Distinct(StringComparer.OrdinalIgnoreCase).Count()}｜"
            + $"耗時 {elapsed.TotalSeconds:0.00}s");
        Console.WriteLine();

        var headers = new[]
        {
            "資料夾", "開始時間", "時長", "樣本", "CPU 平均/最高", "RAM 平均/最高 GB", "任務", "警告"
        };

        var rows = sessions.Select(session => new[]
        {
            session.FolderName,
            session.StartTime.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            FormatDuration(session.Duration),
            session.Aggregate.SampleCount.ToString(CultureInfo.InvariantCulture),
            FormatPair(session.Aggregate.CpuTotal.Mean, session.Aggregate.CpuTotal.Max),
            FormatPair(session.Aggregate.MemoryUsedGb.Mean, session.Aggregate.MemoryUsedGb.Max),
            session.Tasks.Count.ToString(CultureInfo.InvariantCulture),
            session.Diagnostics.Count(diagnostic => diagnostic.Severity != DiagnosticSeverity.Info)
                .ToString(CultureInfo.InvariantCulture)
        }).ToArray();

        ConsoleTable.Write(
            headers,
            rows,
            rightAligned: new[] { false, false, true, true, true, true, true, true },
            maxWidths: new[] { 26, 14, 8, 6, 16, 18, 5, 5 });

        Console.WriteLine();
        Console.WriteLine("裝置");
        foreach (var device in sessions
                     .GroupBy(session => session.Device.DeviceKey)
                     .Select(group => group.First().Device)
                     .OrderBy(device => device.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            var hardware = string.Join(
                " / ",
                new[]
                {
                    device.CpuModel,
                    device.Cores is int cores ? $"{cores}C" : null,
                    device.RamGb is double ram ? $"{ram:0.#}GB" : null,
                    device.OsName,
                    device.OsBuild is string build ? $"Build {build}" : null
                }.Where(part => !string.IsNullOrWhiteSpace(part)));

            Console.WriteLine(
                $"  {device.DisplayName}  [{device.DeviceKey}]  {hardware}"
                + (device.IsIdentified ? string.Empty : "  (未識別)"));
        }

        if (verbose)
        {
            Console.WriteLine();
            Console.WriteLine("任務明細");
            foreach (var session in sessions)
            {
                Console.WriteLine($"  {session.FolderName}");
                foreach (var task in session.Tasks)
                {
                    var aggregate = task.Aggregate;
                    Console.WriteLine(
                        $"    {task.Name,-32} 執行 {FormatDuration(aggregate.ActiveDuration),-8} "
                        + $"({aggregate.ActiveRatio * 100,5:0.0}%)  "
                        + $"CPU {aggregate.Cpu.Mean,5:0.0}/{aggregate.Cpu.Max,5:0.0}  "
                        + $"RAM {aggregate.MemoryMb.Mean,7:0.0}/{aggregate.MemoryMb.Max,7:0.0} MB  "
                        + $"區間 {aggregate.RunCount}  重啟 {aggregate.RestartCount}  翻跳 {aggregate.StateFlaps}");
                }
            }
        }

        var diagnostics = sessions
            .SelectMany(session => session.Diagnostics)
            .Where(diagnostic => showInfo || verbose || diagnostic.Severity != DiagnosticSeverity.Info)
            .ToList();

        if (diagnostics.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"診斷（{diagnostics.Count}）");
            foreach (var diagnostic in diagnostics.Take(40))
            {
                Console.WriteLine(
                    $"  {diagnostic.Severity.ToString().ToUpperInvariant(),-7} {diagnostic.Code,-22} {diagnostic.Message}");
            }

            if (diagnostics.Count > 40)
            {
                Console.WriteLine($"  …另有 {diagnostics.Count - 40} 筆，使用 --show-info 或 --json 查看完整內容");
            }
        }

        if (failures.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"解析失敗（{failures.Count}）");
            foreach (var (failedPath, message) in failures)
            {
                Console.WriteLine($"  {failedPath}");
                Console.WriteLine($"    {message}");
            }
        }
    }

    private static void PrintDiagnostics(IReadOnlyList<Diagnostic> diagnostics, bool showInfo)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (!showInfo && diagnostic.Severity == DiagnosticSeverity.Info)
            {
                continue;
            }

            Console.Error.WriteLine($"  [{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}");
        }
    }

    private static string FormatPair(double average, double maximum)
    {
        if (average <= 0 && maximum <= 0)
        {
            return "—";
        }

        return $"{average:0.0} / {maximum:0.0}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "00:00";
        }

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            TRIOV2 效能分析 CLI v0.1

            用法：
              PerformanceAnalysis.Cli scan <路徑> [選項]

            選項：
              --json <檔案>         輸出完整分析 JSON
              --html <檔案>         輸出單一 HTML 報告
              --write-metadata      將推斷的中繼資料寫入 Metadata\sessions
              --metadata-dir <路徑> 覆寫中繼資料根目錄（預設：執行檔旁的 Metadata）
              --verbose             顯示每個任務的統計
              --show-info           顯示 Info 等級診斷
              --help                顯示此說明

            範例：
              PerformanceAnalysis.Cli scan "D:\TRIOV2\publish_tools\Logs"
              PerformanceAnalysis.Cli scan "D:\TRIOV2\publish_tools\Logs\20261112_150751" --verbose
              PerformanceAnalysis.Cli scan "D:\TRIOV2\publish_tools\Logs" --json out\report.json --write-metadata
              PerformanceAnalysis.Cli scan "D:\TRIOV2\publish_tools\Logs" --html out\perf-report.html
            """);
    }
}
