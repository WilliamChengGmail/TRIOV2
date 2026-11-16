using System.Globalization;
using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Models;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Tests;

public sealed class AnalysisOptionsDiagnosticsTests
{
    private const string SystemHeader =
        "樣本序號,時間戳記,全系統CPU(%),模擬器CPU(%),剩餘空閒CPU(%),已用記憶體(GB),可用記憶體(GB),記憶體使用率(%),模擬影像緩衝(MB),底層辨識狀態,辨識運算時長(ms),運作模式";

    private const string TaskHeader =
        "樣本序號,時間戳記,處理程序名稱,PID,狀態,CPU使用率(%),記憶體使用量(MB),磁碟讀寫速率(KB/s),網路傳輸速率(Kbps),電源用量,電源用量趨勢";

    [Fact]
    public void CpuHighSustainedTriggersDiagnostic()
    {
        using var workspace = new TempWorkspace();
        var rows = Enumerable.Range(0, 12).Select(index =>
            $"{index + 1},{Timestamp(index)},95.0,5.0,5.0,8.00,8.00,50,100,待機,800,單次拍圖辨識");
        WriteSession(workspace, "cpu", SystemHeader + "\n" + string.Join("\n", rows));

        var session = Analyze(workspace, "cpu", new AnalysisOptions
        {
            ShortSessionSeconds = 0,
            DuplicateRatioPercent = 100,
            CpuHighPercent = 90,
            CpuHighSustainedSeconds = 10
        });

        Assert.Contains(session.Diagnostics, diagnostic => diagnostic.Code == "CPU_HIGH_SUSTAINED");
    }

    [Fact]
    public void RamHighTriggersDiagnostic()
    {
        using var workspace = new TempWorkspace();
        var rows = Enumerable.Range(0, 5).Select(index =>
            $"{index + 1},{Timestamp(index)},10.0,2.0,90.0,15.50,0.50,95,100,待機,800,單次拍圖辨識");
        WriteSession(workspace, "ram", SystemHeader + "\n" + string.Join("\n", rows));

        var session = Analyze(workspace, "ram", new AnalysisOptions
        {
            ShortSessionSeconds = 0,
            DuplicateRatioPercent = 100,
            RamHighPercent = 90,
            AvailableRamMinimumGb = 1
        });

        Assert.Contains(session.Diagnostics, diagnostic => diagnostic.Code == "RAM_HIGH");
    }

    [Fact]
    public void MemoryGrowthTriggersDiagnostic()
    {
        using var workspace = new TempWorkspace();
        var rows = Enumerable.Range(0, 330).Select(index =>
            $"{index + 1},{Timestamp(index)},10.0,2.0,90.0,{(8.0 + index * 0.002).ToString("0.000", CultureInfo.InvariantCulture)},8.00,50,100,待機,800,單次拍圖辨識");
        WriteSession(workspace, "growth", SystemHeader + "\n" + string.Join("\n", rows));

        var session = Analyze(workspace, "growth", new AnalysisOptions
        {
            ShortSessionSeconds = 0,
            DuplicateRatioPercent = 100,
            MemoryGrowthMbPerMinute = 20,
            MemoryGrowthMinimumSeconds = 300
        });

        Assert.Contains(session.Diagnostics, diagnostic => diagnostic.Code == "MEMORY_GROWTH");
    }

    [Fact]
    public void TaskRestartsTriggersDiagnostic()
    {
        using var workspace = new TempWorkspace();
        var systemRows = Enumerable.Range(0, 5).Select(index =>
            $"{index + 1},{Timestamp(index)},10.0,2.0,90.0,8.00,8.00,50,100,待機,800,單次拍圖辨識");
        WriteSession(workspace, "restart", SystemHeader + "\n" + string.Join("\n", systemRows));

        var taskRows = new[]
        {
            $"1,{Timestamp(0)},task,100,執行中,10.0,100.0,0.0,0.0,低,低",
            $"2,{Timestamp(1)},task,200,執行中,10.0,100.0,0.0,0.0,低,低",
            $"3,{Timestamp(2)},task,300,執行中,10.0,100.0,0.0,0.0,低,低",
            $"4,{Timestamp(3)},task,0,尚未執行,0.0,0.0,0.0,0.0,無,無",
            $"5,{Timestamp(4)},task,400,執行中,10.0,100.0,0.0,0.0,低,低"
        };
        workspace.WriteFile(
            "restart/task/task_BenchmarkLog.csv",
            TaskHeader + "\n" + string.Join("\n", taskRows));

        var session = Analyze(workspace, "restart", new AnalysisOptions
        {
            ShortSessionSeconds = 0,
            DuplicateRatioPercent = 100,
            TaskRestartThreshold = 1
        });

        Assert.Contains(session.Diagnostics, diagnostic => diagnostic.Code == "TASK_RESTARTS");
    }

    private static Session Analyze(TempWorkspace workspace, string folder, AnalysisOptions options)
    {
        var store = new SessionMetadataStore(Path.Combine(workspace.Root, "metadata"));
        var analyzer = new SessionAnalyzer(store, options: options);
        return analyzer.Analyze(new SessionCandidate(workspace.PathFor(folder), null));
    }

    private static void WriteSession(TempWorkspace workspace, string folder, string content)
        => workspace.WriteFile($"{folder}/System_BenchmarkLog.csv", content);

    private static string Timestamp(int second)
        => new DateTime(2026, 1, 1, 0, 0, 0).AddSeconds(second).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
