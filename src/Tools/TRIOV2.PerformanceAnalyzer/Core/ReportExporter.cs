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
        sb.AppendLine("設備名稱,CPU型號,核心數,記憶體規格,OS名稱,OS版本,GPU,NPU,有效測試次數,系統CPU平均(%),系統CPU_P95(%),系統CPU峰值(%),記憶體平均(%),記憶體峰值(%),記憶體峰值(GB),適配結論,升級診斷建議,燈號,白話結論,最低可用記憶體(GB)");

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
                Csv(d.Verdict.Recommendation),
                Csv(d.Verdict.ShortLabel),
                Csv(d.Verdict.PlainSummary),
                double.IsNaN(d.MinAvailableGbAcrossRuns) ? "" : $"{d.MinAvailableGbAcrossRuns:0.0}"));
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
        sb.AppendLine($"- **驗收基準門檻**：CPU P95 `< {thresholds.CpuP95PassLimit:0}%`（警戒上限 `{thresholds.CpuP95WarnLimit:0}%`）、最低可用記憶體 `≥ {thresholds.AvailableRamPassGb:0.#} GB`（升級線 `≤ {thresholds.AvailableRamUpgradeGb:0.#} GB`）");
        sb.AppendLine();

        var summary = FleetSummary.Build(devices);
        sb.AppendLine("## 結論摘要");
        sb.AppendLine();
        sb.AppendLine($"**{summary.Sentence}**");
        sb.AppendLine();
        sb.AppendLine($"- 建議升級：{summary.UpgradeCount} 台 ｜ 接近上限：{summary.WarnCount} 台 ｜ 通過：{summary.PassCount} 台 ｜ 資料不足：{summary.InsufficientCount} 台");
        sb.AppendLine($"- 燈號說明：{FleetSummary.LevelLegend}");
        sb.AppendLine();
        foreach (var d in devices)
        {
            sb.AppendLine($"- **{d.DisplayName}**：{d.Verdict.ShortLabel} — {d.Verdict.PlainSummary}");
        }

        sb.AppendLine();
        sb.AppendLine("## 多設備規格與效能綜合對照表");
        sb.AppendLine();
        sb.AppendLine("| 設備名稱 | 燈號 | CPU 規格 | 記憶體 | 作業系統 | 測試次數 | CPU Avg / P95 / Max | RAM Peak | 最低可用記憶體 | 適配結論 | 診斷與建議 |");
        sb.AppendLine("| :--- | :---: | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- | :--- |");

        foreach (var d in devices)
        {
            var hw = d.HardwareProfile;
            sb.AppendLine(
                $"| {d.DisplayName} | {d.Verdict.ShortLabel} | {hw.ShortCpuLabel} | {hw.ShortMemoryLabel} | {hw.ShortOsLabel} | " +
                $"{d.IncludedRunCount} | {d.AvgCpuAcrossRuns:0.0}% / {d.MaxCpuP95AcrossRuns:0.0}% / {d.PeakCpuAcrossRuns:0.0}% | " +
                $"{d.RamPeakDisplay} | {d.MinAvailableDisplay} | **{d.Verdict.BadgeText}** | {d.Verdict.Recommendation} |");
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
        sb.AppendLine(".summary{border-left:5px solid #0284c7;} .summary .sentence{font-size:18px;font-weight:700;margin:4px 0 12px;}");
        sb.AppendLine(".counts{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:12px;} .count{border:1px solid #e2e8f0;border-radius:8px;padding:8px 14px;min-width:110px;}");
        sb.AppendLine(".count b{display:block;font-size:24px;} .c-fail b{color:#ef4444;} .c-warn b{color:#d97706;} .c-pass b{color:#10b981;} .c-nodata b{color:#64748b;}");
        sb.AppendLine(".plain{font-size:14px;} .muted{color:#64748b;font-size:12px;} details summary{cursor:pointer;color:#0284c7;font-weight:600;}");
        sb.AppendLine("@media print{details{display:none;} .badge{-webkit-print-color-adjust:exact;print-color-adjust:exact;}}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>TRIO V2 硬體效能數據與機器等級分析報告</h1>");
        sb.AppendLine($"<div class=\"meta\">分析路徑：{WebUtility.HtmlEncode(scannedPath)} ｜ 產出時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss} ｜ 製作者：Office of William<br>");
        sb.AppendLine($"判定門檻：CPU P95 &lt; {thresholds.CpuP95PassLimit:0}% (警戒 {thresholds.CpuP95WarnLimit:0}%)，最低可用記憶體 ≥ {thresholds.AvailableRamPassGb:0.#} GB (升級線 ≤ {thresholds.AvailableRamUpgradeGb:0.#} GB)</div>");

        var summary = FleetSummary.Build(devices);
        sb.AppendLine("<div class=\"card summary\"><div class=\"muted\">結論摘要：這些電腦的硬體夠不夠用？</div>");
        sb.AppendLine($"<div class=\"sentence\">{WebUtility.HtmlEncode(summary.Sentence)}</div>");
        sb.AppendLine("<div class=\"counts\">");
        sb.AppendLine($"<div class=\"count c-fail\"><b>{summary.UpgradeCount}</b>建議升級</div>");
        sb.AppendLine($"<div class=\"count c-warn\"><b>{summary.WarnCount}</b>接近上限</div>");
        sb.AppendLine($"<div class=\"count c-pass\"><b>{summary.PassCount}</b>通過</div>");
        sb.AppendLine($"<div class=\"count c-nodata\"><b>{summary.InsufficientCount}</b>資料不足</div>");
        sb.AppendLine("</div>");
        sb.AppendLine($"<div class=\"muted\">燈號說明：{WebUtility.HtmlEncode(FleetSummary.LevelLegend)}</div>");
        sb.AppendLine("<details style=\"margin-top:10px\"><summary>名詞說明（第一次看這份報告請展開）</summary><ul>");
        sb.AppendLine("<li><b>CPU（處理器）使用率</b>：電腦運算能力用掉多少，越高越忙；長時間接近 100% 就可能卡頓。</li>");
        sb.AppendLine("<li><b>P95</b>：測試期間 95% 的時間，使用率都低於這個數字。比「最高值」更能代表常態下的高負載，本報告以它判定處理器是否足夠。</li>");
        sb.AppendLine("<li><b>最低可用記憶體</b>：測試期間剩下最少的可用記憶體（GB），本報告以它判定記憶體是否足夠：≥ 2 GB 有餘裕、≤ 1 GB 建議升級。以 GB 而非百分比判斷，是因為 TRIO V2 需要的記憶體量固定，不隨機器容量變化。</li>");
        sb.AppendLine("<li><b>RAM Peak（記憶體峰值）</b>：測試期間記憶體用量與使用率的最高值，供參考。</li>");
        sb.AppendLine("<li><b>測試次數</b>：納入判定的測試數。短於 10 秒的試跑紀錄不列入，避免用幾秒鐘的資料下結論。</li>");
        sb.AppendLine("</ul></details></div>");

        sb.AppendLine("<table><thead><tr><th>設備與規格</th><th>判定（白話說明）</th><th>測試次數</th><th>CPU Avg / P95 / Max</th><th>RAM Peak</th><th>最低可用記憶體</th><th>技術依據與建議（SW）</th></tr></thead><tbody>");
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
            sb.AppendLine($"<td class=\"plain\"><span class=\"badge {cls}\">{WebUtility.HtmlEncode(d.Verdict.ShortLabel)}</span><br>{WebUtility.HtmlEncode(d.Verdict.PlainSummary)}</td>");
            sb.AppendLine($"<td>{WebUtility.HtmlEncode(d.RunCountDisplay)}</td>");
            sb.AppendLine($"<td>{d.AvgCpuAcrossRuns:0.0}% / <strong>{d.MaxCpuP95AcrossRuns:0.0}%</strong> / {d.PeakCpuAcrossRuns:0.0}%</td>");
            sb.AppendLine($"<td>{WebUtility.HtmlEncode(d.RamPeakDisplay)}</td>");
            sb.AppendLine($"<td><strong>{WebUtility.HtmlEncode(d.MinAvailableDisplay)}</strong></td>");
            sb.AppendLine($"<td><span class=\"muted\">{WebUtility.HtmlEncode(d.Verdict.BadgeText)}</span><br>{WebUtility.HtmlEncode(d.Verdict.ReasonSummary)}<br><strong>{WebUtility.HtmlEncode(d.Verdict.Recommendation)}</strong></td>");
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
