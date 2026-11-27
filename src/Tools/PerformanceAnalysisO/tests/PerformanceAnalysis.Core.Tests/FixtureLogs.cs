namespace PerformanceAnalysis.Core.Tests;

/// <summary>
/// 測試用的固定 Logs（從實機紀錄複製，隨測試專案輸出）。
/// 不再讀取 D:\TRIOV2\publish_tools\Logs，避免更新實際資料後測試失敗。
/// <list type="bullet">
/// <item>20261127_091207：Celeron J4125，16 分鐘；記錄器每約 20 秒漏記 2–3 秒（不應誤判為重啟）、可用記憶體降到 1 GB。</item>
/// <item>20261127_093607：同一台，7 分鐘；CPU 平均 53%、記憶體持續成長，TRIO 有真正的 PID 變更。</item>
/// </list>
/// </summary>
internal static class FixtureLogs
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleSessions");

    public const string GapSession = "20261127_091207";

    public const string LoadSession = "20261127_093607";
}
