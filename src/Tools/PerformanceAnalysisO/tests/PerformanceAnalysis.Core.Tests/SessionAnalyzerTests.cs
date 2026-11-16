using PerformanceAnalysis.Core.Analysis;
using PerformanceAnalysis.Core.Metadata;
using PerformanceAnalysis.Core.Models;
using PerformanceAnalysis.Core.Scanning;

namespace PerformanceAnalysis.Core.Tests;

public sealed class SessionAnalyzerTests : IDisposable
{
    private const string SystemHeader =
        "樣本序號,時間戳記,全系統CPU(%),模擬器CPU(%),剩餘空閒CPU(%),已用記憶體(GB),可用記憶體(GB),記憶體使用率(%),模擬影像緩衝(MB),底層辨識狀態,辨識運算時長(ms),運作模式";

    private const string TaskHeader =
        "樣本序號,時間戳記,處理程序名稱,PID,狀態,CPU使用率(%),記憶體使用量(MB),磁碟讀寫速率(KB/s),網路傳輸速率(Kbps),電源用量,電源用量趨勢";

    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void AnalyzesSessionWithDuplicatesAndWritesDerivedMetadata()
    {
        var folder = _workspace.PathFor("20261112_150751");
        Directory.CreateDirectory(folder);

        _workspace.WriteFile(
            "20261112_150751/System_BenchmarkLog.csv",
            SystemHeader + "\n"
            + "1,2026-11-12 15:07:52,10.0,5.0,90.0,10.10,5.90,63,231,待機,800,單次拍圖辨識\n"
            + "2,2026-11-12 15:07:53,20.0,6.0,80.0,10.20,5.80,64,232,待機,800,單次拍圖辨識\n"
            + "3,2026-11-12 15:07:53,30.0,7.0,70.0,10.30,5.70,64,233,辨識運算中,900,單次拍圖辨識\n"
            + "4,2026-11-12 15:07:54,30.0,8.0,70.0,10.40,5.60,65,234,待機,800,單次拍圖辨識\n"
            + "# ========================================================\n");

        _workspace.WriteFile(
            "20261112_150751/task/task_BenchmarkLog.csv",
            TaskHeader + "\n"
            + "1,2026-11-12 15:07:52,task,100,執行中,8.0,100.0,10.0,20.0,低,低\n"
            + "2,2026-11-12 15:07:53,task,100,執行中,12.0,120.0,20.0,40.0,低,低\n"
            + "3,2026-11-12 15:07:54,task,0,尚未執行,0.0,0.0,0.0,0.0,無,無\n"
            + "# ========================================================\n");

        _workspace.WriteFile(
            "20261112_150751/ref.txt",
            """
            ========================================
            【TRIOV2 區間效能量測統計報告】
            量測時間：2026-11-12 15:07:55
            樣本數量：共 4 筆樣本 (紀錄時長 00:02)
            ----------------------------------------
            全系統處理器 CPU 使用率：
              - 最高 (Max) : 30.0 %
              - 最低 (Min) : 10.0 %
              - 平均 (Avg) : 22.5 %
            全系統實體記憶體 RAM 使用量：
              - 最高 (Max) : 10.40 GB (65 %)
              - 最低 (Min) : 10.10 GB (63 %)
              - 平均 (Avg) : 10.25 GB (64 %)
            ► CPU 處理器資訊：
              - 型號         : Intel(R) Core(TM) Ultra 7 255H
              - 核心數目     : 4
              - 邏輯處理器   : 4
            ► 記憶體 (RAM) 資訊：
              - 已安裝容量   : 16.00 GB (OS 可用 16.00 GB)
            ► 作業系統資訊：
              - 作業系統名稱 : Microsoft Windows 10 專業版
              - 版本         : 22H2
              - OS 組建      : 19045.6466
              - 裝置名稱     : DESKTOP-S136SQE
            ========================================
            """);

        var store = new SessionMetadataStore(Path.Combine(_workspace.Root, "Metadata"));
        var analyzer = new SessionAnalyzer(store, writeDerivedMetadata: true);
        var session = analyzer.Analyze(new SessionCandidate(folder, null));

        Assert.Equal(4, session.SystemSamples.Count);
        Assert.Equal(3, session.Aggregate.UniqueTimestampCount);
        Assert.Equal(3, session.Aggregate.ExpectedSampleCount);
        Assert.Equal(1, session.Aggregate.DataCompleteness);
        Assert.Equal(22.5, session.Aggregate.CpuTotal.Mean, 6);
        Assert.Equal(30, session.Aggregate.CpuTotal.Max, 6);
        Assert.Equal(10.4, session.Aggregate.MemoryUsedGb.Max, 6);
        Assert.Equal(825, session.Aggregate.RecognitionMs.Mean, 6);

        var task = Assert.Single(session.Tasks);
        Assert.Equal(TimeSpan.FromSeconds(2), task.Aggregate.ActiveDuration);
        Assert.Equal(10, task.Aggregate.Cpu.Mean, 6);
        Assert.Equal(110, task.Aggregate.MemoryMb.Mean, 6);
        Assert.Equal(1, task.Aggregate.RunCount);
        Assert.Equal(1, task.Aggregate.StateFlaps);
        Assert.Equal(1, task.Aggregate.ActiveRatio, 6);

        Assert.Equal("DESKTOP-S136SQE", session.Device.DeviceName);
        Assert.True(session.Device.IsIdentified);
        Assert.True(session.HasRefTxt);
        Assert.Contains(session.Diagnostics, diagnostic => diagnostic.Code == "DUPLICATE_TIMESTAMPS");

        var metadata = store.TryLoadByFolderName("20261112_150751");
        Assert.NotNull(metadata);
        Assert.Equal("derived", metadata!.Source);
        Assert.Contains("task", metadata.Tasks);
        Assert.True(File.Exists(Path.Combine(store.SessionsDirectory, $"{metadata.SessionKey}.json")));
    }

    [Fact]
    public void MissingRefFallsBackToUnknownDevice()
    {
        _workspace.WriteFile(
            "20260101_120000/System_BenchmarkLog.csv",
            SystemHeader + "\n"
            + "1,2026-01-01 12:00:00,10.0,5.0,90.0,8.00,8.00,50,100,待機,0,單次拍圖辨識\n");

        var store = new SessionMetadataStore(Path.Combine(_workspace.Root, "Metadata"));
        var analyzer = new SessionAnalyzer(store);
        var session = analyzer.Analyze(new SessionCandidate(_workspace.PathFor("20260101_120000"), null));

        Assert.False(session.HasRefTxt);
        Assert.False(session.Device.IsIdentified);
        Assert.Equal("未識別裝置", session.Device.DeviceName);
        Assert.Contains(session.Diagnostics, diagnostic => diagnostic.Code == "NO_REF");
    }

    [Fact]
    public void DerivedUnknownMetadataDoesNotMarkDeviceAsIdentified()
    {
        _workspace.WriteFile(
            "20260101_120000/System_BenchmarkLog.csv",
            SystemHeader + "\n"
            + "1,2026-01-01 12:00:00,10.0,5.0,90.0,8.00,8.00,50,100,待機,0,單次拍圖辨識\n");

        var store = new SessionMetadataStore(Path.Combine(_workspace.Root, "Metadata"));
        var analyzer = new SessionAnalyzer(store, writeDerivedMetadata: true);
        var folder = _workspace.PathFor("20260101_120000");

        var first = analyzer.Analyze(new SessionCandidate(folder, null));
        var second = analyzer.Analyze(new SessionCandidate(folder, null));

        Assert.NotNull(store.TryLoadByFolderName("20260101_120000", first.StartTime));
        Assert.False(first.Device.IsIdentified);
        Assert.False(second.Device.IsIdentified);
    }

    [Fact]
    public void ManualDeviceOverrideWinsOverRefDeviceName()
    {
        _workspace.WriteFile(
            "20260101_120000/System_BenchmarkLog.csv",
            SystemHeader + "\n"
            + "1,2026-01-01 12:00:00,10.0,5.0,90.0,8.00,8.00,50,100,待機,0,單次拍圖辨識\n");

        _workspace.WriteFile(
            "20260101_120000/ref.txt",
            """
            量測時間：2026-01-01 12:00:01
            樣本數量：共 1 筆樣本 (紀錄時長 00:00)
            ► CPU 處理器資訊：
              - 型號         : Test CPU
              - 核心數目     : 4
            ► 記憶體 (RAM) 資訊：
              - 已安裝容量   : 16.00 GB
            ► 作業系統資訊：
              - 作業系統名稱 : Test OS
              - OS 組建      : 1
              - 裝置名稱     : TEST-DEVICE
            """);

        var store = new SessionMetadataStore(Path.Combine(_workspace.Root, "Metadata"));
        var analyzer = new SessionAnalyzer(store, writeDerivedMetadata: true);
        var folder = _workspace.PathFor("20260101_120000");

        var first = analyzer.Analyze(new SessionCandidate(folder, null));
        Assert.True(first.Device.IsIdentified);
        Assert.Equal("TEST-DEVICE", first.Device.DeviceName);

        var metadata = store.TryLoadByFolderName("20260101_120000", first.StartTime);
        Assert.NotNull(metadata);
        metadata!.DeviceId = "dev-other";
        metadata.DeviceName = "未識別裝置";
        metadata.DeviceOverridden = true;
        store.Save(metadata);

        var second = analyzer.Analyze(new SessionCandidate(folder, null));

        Assert.Equal("dev-other", second.Device.DeviceKey);
        Assert.Equal("未識別裝置", second.Device.DeviceName);
        Assert.False(second.Device.IsIdentified);

        metadata.Alias = "LAB-X";
        store.Save(metadata);

        var third = analyzer.Analyze(new SessionCandidate(folder, null));
        Assert.True(third.Device.IsIdentified);
        Assert.Equal("LAB-X", third.Device.DisplayName);
        Assert.Equal("未識別裝置", third.Device.DeviceName);
    }

    public void Dispose() => _workspace.Dispose();
}
