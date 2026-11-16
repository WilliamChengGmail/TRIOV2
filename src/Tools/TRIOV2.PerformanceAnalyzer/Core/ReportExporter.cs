using System.IO;
using System.Net;
using System.Text;
using TRIOV2.PerformanceAnalyzer.Models;

namespace TRIOV2.PerformanceAnalyzer.Core;

/// <summary>
/// 匯出多設備硬體比較與效能選型結論報告（HTML / CSV / Markdown）
/// 製作者: Office of William
/// </summary>
public static class ReportExporter
{
    public static void ExportCsv(string filePath, IEnumerable<DeviceAggregateSummary> devices)
    {
        var sb = new StringBuilder();
        sb.AppendLine("設備名稱,CPU型號,核心數,記憶體規格,OS名稱,OS版本,GPU,NPU,有效測試次數,系統CPU平均(%),系統CPU_P95(%),系統CPU峰值(%),記憶體平均(%),記憶體峰值(%),記憶體峰值(GB),適配結論,升級診斷建議");

        foreach (var d in devices)
        {
            var hw = d.HardwareProfile;
            sb.AppendLine(string.Join(",",
                Csv(d.DisplayName),
                Csv(hw.CpuModel),
                Csv($"{hw.CpuCores}C/{hw.CpuLogicalProcessors}T"),
                Csv(hw.ShortMemoryLabel),
                Csv(hw.OsName),
                Csv($"{hw.OsVersion} ({hw.OsBuild})"),
                Csv(hw.GpuSummary),
                Csv(hw.NpuSummary),
                d.IncludedRunCount.ToString(),
                $"{d.AvgCpuAcrossRuns:0.0}",
                $"{d.MaxCpuP95AcrossRuns:0.0}",
                $"{d.PeakCpuAcrossRuns:0.0}",
                $"{d.AvgRamPercentAcrossRuns:0.0}",
                $"{d.PeakRamPercentAcrossRuns:0.0}",
                $"{d.PeakRamGbAcrossRuns:0.00}",
                Csv(d.Verdict.BadgeText),
                Csv(d.Verdict.Recommendation)));
        }

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static string BuildMarkdownReport(
        string scannedPath,
        IReadOnlyList<DeviceAggregateSummary> devices,
        EvaluationThresholds thresholds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# TRIO V2 硬體效能數據與規格選型分析報告");
        sb.AppendLine();
        sb.AppendLine($"- **分析路徑**：`{scannedPath}`");
        sb.AppendLine($"- **產生時間**：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **分析工具**：TRIOV2.PerformanceAnalyzer (製作者：Office of William)");
        sb.AppendLine($"- **驗收基準門檻**：CPU P95 `< {thresholds.CpuP95PassLimit:0}%`（警戒上限 `{thresholds.CpuP95WarnLimit:0}%`）、RAM Peak `< {thresholds.RamPeakPassLimit:0}%`（警戒上限 `{thresholds.RamPeakWarnLimit:0}%`）");
        sb.AppendLine();
        sb.AppendLine("## 多設備規格與效能綜合對照表");
        sb.AppendLine();
        sb.AppendLine("| 設備名稱 | CPU 規格 | 記憶體 | 作業系統 | 測試次數 | CPU Avg / P95 / Max | RAM Peak | 適配結論 | 診斷與建議 |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- | :--- |");

        foreach (var d in devices)
        {
            var hw = d.HardwareProfile;
            sb.AppendLine(
                $"| {d.DisplayName} | {hw.ShortCpuLabel} | {hw.ShortMemoryLabel} | {hw.ShortOsLabel} | " +
                $"{d.IncludedRunCount} | {d.AvgCpuAcrossRuns:0.0}% / {d.MaxCpuP95AcrossRuns:0.0}% / {d.PeakCpuAcrossRuns:0.0}% | " +
                $"{d.RamPeakDisplay} | **{d.Verdict.BadgeText}** | {d.Verdict.Recommendation} |");
        }

        sb.AppendLine();
        sb.AppendLine("## 各設備主要耗資源程序排行 (Active-Only 實際運行統計)");
        foreach (var d in devices)
        {
            sb.AppendLine();
            sb.AppendLine($"### {d.DisplayName} ({d.HardwareProfile.CpuModel})");
            if (d.TopTasks.Count == 0)
            {
                sb.AppendLine("- 無獨立任務紀錄");
                continue;
            }
            foreach (var t in d.TopTasks.Take(8))
            {
                sb.AppendLine($"- **{t.TaskName}**：運行樣本 `{t.ActiveSamples}/{t.TotalSamples}` ({t.ActiveRatioPercent:0.#}%) | 執行時段：`{t.ExecutionTimeRangeText}` | CPU Avg `{t.ActiveCpuAvg:0.0}%` / Peak `{t.ActiveCpuMax:0.0}%` | RAM Peak `{t.ActiveRamMaxMb:0.0} MB` | Disk Peak `{t.ActiveDiskPeakKbps:0.0} KB/s`");
                if (t.ExecutionRuns.Count > 1)
                {
                    foreach (var r in t.ExecutionRuns)
                    {
                        sb.AppendLine($"  - {r.RunLabel}：起 `{r.StartTimeText}` ~ 訖 `{r.EndTimeText}` (時長 `{r.DurationText}`, PID `{r.Pid}`) | CPU `{r.CpuDisplay}` | RAM Peak `{r.RamDisplay}`");
                    }
                }
            }
        }

        return sb.ToString();
    }

    public static void ExportHtml(
        string filePath,
        string scannedPath,
        IReadOnlyList<DeviceAggregateSummary> devices,
        EvaluationThresholds thresholds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"zh-Hant\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<title>TRIO V2 硬體效能數據分析報告</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI','Microsoft JhengHei',sans-serif;background:#f8fafc;color:#0f172a;margin:28px;line-height:1.5;}");
        sb.AppendLine("h1{margin-bottom:4px;font-size:22px;} .meta{color:#475569;font-size:13px;margin-bottom:20px;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;background:#fff;box-shadow:0 1px 3px rgba(0,0,0,.08);margin-bottom:24px;font-size:13px;}");
        sb.AppendLine("th,td{padding:10px 12px;border:1px solid #e2e8f0;text-align:left;vertical-align:top;}");
        sb.AppendLine("th{background:#f1f5f9;font-weight:600;} .badge{display:inline-block;padding:2px 8px;border-radius:4px;font-weight:600;color:#fff;font-size:12px;}");
        sb.AppendLine(".pass{background:#10b981;} .warn{background:#f59e0b;} .fail{background:#ef4444;} .nodata{background:#64748b;}");
        sb.AppendLine(".card{background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:16px;margin-bottom:16px;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>TRIO V2 硬體效能數據與機器等級分析報告</h1>");
        sb.AppendLine($"<div class=\"meta\">分析路徑：{WebUtility.HtmlEncode(scannedPath)} ｜ 產出時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss} ｜ 製作者：Office of William<br>");
        sb.AppendLine($"判定門檻：CPU P95 &lt; {thresholds.CpuP95PassLimit:0}% (警戒 {thresholds.CpuP95WarnLimit:0}%)，RAM Peak &lt; {thresholds.RamPeakPassLimit:0}% (警戒 {thresholds.RamPeakWarnLimit:0}%)</div>");

        sb.AppendLine("<table><thead><tr><th>設備與規格</th><th>測試次數</th><th>CPU Avg / P95 / Max</th><th>RAM Peak</th><th>適配判定</th><th>診斷與建議</th></tr></thead><tbody>");
        foreach (var d in devices)
        {
            var hw = d.HardwareProfile;
            string cls = d.Verdict.Level switch
            {
                SizingLevel.Pass => "pass",
                SizingLevel.Warning => "warn",
                SizingLevel.UpgradeRequired => "fail",
                _ => "nodata"
            };
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td><strong>{WebUtility.HtmlEncode(d.DisplayName)}</strong><br><small>{WebUtility.HtmlEncode(hw.ShortCpuLabel)}<br>{WebUtility.HtmlEncode(hw.ShortMemoryLabel)} | {WebUtility.HtmlEncode(hw.ShortOsLabel)}<br>GPU: {WebUtility.HtmlEncode(hw.GpuSummary)}</small></td>");
            sb.AppendLine($"<td>{d.IncludedRunCount}</td>");
            sb.AppendLine($"<td>{d.AvgCpuAcrossRuns:0.0}% / <strong>{d.MaxCpuP95AcrossRuns:0.0}%</strong> / {d.PeakCpuAcrossRuns:0.0}%</td>");
            sb.AppendLine($"<td>{WebUtility.HtmlEncode(d.RamPeakDisplay)}</td>");
            sb.AppendLine($"<td><span class=\"badge {cls}\">{WebUtility.HtmlEncode(d.Verdict.BadgeText)}</span></td>");
            sb.AppendLine($"<td>{WebUtility.HtmlEncode(d.Verdict.ReasonSummary)}<br><strong>{WebUtility.HtmlEncode(d.Verdict.Recommendation)}</strong></td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        foreach (var d in devices)
        {
            sb.AppendLine($"<div class=\"card\"><h3>{WebUtility.HtmlEncode(d.DisplayName)} — 測試場次與主要耗資源任務</h3>");
            if (d.TopTasks.Count > 0)
            {
                sb.AppendLine("<table><thead><tr><th>任務名稱</th><th>執行起訖與長度</th><th>運行樣本</th><th>CPU Avg (全程 / 運行中)</th><th>CPU Peak</th><th>RAM Peak</th><th>Disk Peak</th></tr></thead><tbody>");
                foreach (var t in d.TopTasks)
                {
                    sb.AppendLine($"<tr><td><strong>{WebUtility.HtmlEncode(t.TaskName)}</strong></td><td>{WebUtility.HtmlEncode(t.ExecutionTimeRangeText)}</td><td>{WebUtility.HtmlEncode(t.ActiveSamplesDisplay)}</td><td>{WebUtility.HtmlEncode(t.CpuAvgDualDisplay)}</td><td>{t.ActiveCpuMax:0.0}%</td><td>{t.ActiveRamMaxMb:0.0} MB</td><td>{t.ActiveDiskPeakKbps:0.0} KB/s</td></tr>");
                }
                sb.AppendLine("</tbody></table>");
            }
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</body></html>");
        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Csv(string? input)
    {
        string s = input ?? string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
        {
            return $"\"{s.Replace("\"", "\"\"")}\"";
        }
        return s;
    }
}
