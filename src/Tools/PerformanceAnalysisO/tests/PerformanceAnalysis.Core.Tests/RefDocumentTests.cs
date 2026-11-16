using PerformanceAnalysis.Core.Parsing;

namespace PerformanceAnalysis.Core.Tests;

public sealed class RefDocumentTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void ParsesSummaryHardwareAndDevice()
    {
        var path = _workspace.WriteFile("ref.txt", """
            ========================================
            【TRIOV2 區間效能量測統計報告】
            量測時間：2026-11-12 15:18:17
            樣本數量：共 625 筆樣本 (紀錄時長 10:24)
            ----------------------------------------
            全系統處理器 CPU 使用率：
              - 最高 (Max) : 98.0 %
              - 最低 (Min) : 5.9 %
              - 平均 (Avg) : 29.4 %
            全系統實體記憶體 RAM 使用量：
              - 最高 (Max) : 11.10 GB (69 %)
              - 最低 (Min) : 10.10 GB (63 %)
              - 平均 (Avg) : 10.46 GB (65 %)
            ----------------------------------------
            ► CPU 處理器資訊：
              - 型號         : Intel(R) Core(TM) Ultra 7 255H
              - 核心數目     : 4
              - 邏輯處理器   : 4
            ► 記憶體 (RAM) 資訊：
              - 速度         : 4800 MT/s
              - 已安裝容量   : 16.00 GB (OS 可用 16.00 GB)
            ► GPU 資訊：
              - [GPU 0] VMware SVGA 3D | 專屬記憶體: 4 MB
            ► 作業系統資訊：
              - 作業系統名稱 : Microsoft Windows 10 專業版
              - 版本         : 22H2
              - OS 組建      : 19045.6466
              - 裝置名稱     : DESKTOP-S136SQE
            ========================================
            """);

        var document = RefDocument.Parse(path);
        var summary = document.GetSummary();
        var hardware = document.GetHardware();

        Assert.Equal(625, document.SampleCount);
        Assert.Equal("10:24", document.DurationText);
        Assert.Equal(new DateTime(2026, 11, 12, 15, 18, 17), document.MeasuredAt);
        Assert.Equal("DESKTOP-S136SQE", document.DeviceName);

        Assert.Equal(5.9, summary.CpuMin);
        Assert.Equal(98.0, summary.CpuMax);
        Assert.Equal(29.4, summary.CpuAvg);
        Assert.Equal(10.10, summary.RamMinGb);
        Assert.Equal(11.10, summary.RamMaxGb);
        Assert.Equal(69, summary.RamMaxPercent);

        Assert.Equal("Intel(R) Core(TM) Ultra 7 255H", hardware.Cpu);
        Assert.Equal(4, hardware.Cores);
        Assert.Equal(4, hardware.LogicalCpus);
        Assert.Equal(16.00, hardware.RamGB);
        Assert.Equal("4800 MT/s", hardware.RamSpeed);
        Assert.Equal("Microsoft Windows 10 專業版", hardware.OsName);
        Assert.Equal("22H2", hardware.OsVersion);
        Assert.Equal("19045.6466", hardware.OsBuild);
        Assert.Contains("VMware SVGA 3D", hardware.Gpu);
    }

    public void Dispose() => _workspace.Dispose();
}
