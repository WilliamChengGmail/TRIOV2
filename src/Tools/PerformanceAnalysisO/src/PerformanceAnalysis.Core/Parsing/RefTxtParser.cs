using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Parsing;

public sealed class RefDocument
{
    private static readonly Regex SampleCountRegex = new(@"共\s*(\d+)\s*筆樣本", RegexOptions.Compiled);
    private static readonly Regex DurationRegex = new(@"紀錄時長\s*([0-9]{1,2}:[0-9]{2}(?::[0-9]{2})?)", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"[-+]?\d+(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly Regex PercentRegex = new(@"\((\d+(?:\.\d+)?)\s*%\)", RegexOptions.Compiled);

    private readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _gpuLines = new();
    private readonly List<string> _rawLines = new();
    private readonly List<Diagnostic> _diagnostics = new();

    private RefDocument(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public string? MeasuredAtText { get; private set; }

    public DateTime? MeasuredAt { get; private set; }

    public int? SampleCount { get; private set; }

    public string? DurationText { get; private set; }

    public IReadOnlyList<string> GpuLines => _gpuLines;

    public IReadOnlyList<string> RawLines => _rawLines;

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public static RefDocument Parse(string path)
    {
        var doc = new RefDocument(path);
        string text;

        try
        {
            text = TextFileReader.ReadAllText(path);
        }
        catch (Exception ex)
        {
            doc._diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                "REF_READ_FAILED",
                $"{Path.GetFileName(path)}: 無法讀取檔案 ({ex.Message})"));
            return doc;
        }

        string? currentSection = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            doc._rawLines.Add(line);
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith('►') || trimmed.EndsWith('：') || trimmed.EndsWith(':'))
            {
                var section = trimmed.TrimStart('►').Trim().TrimEnd('：', ':').Trim();
                if (section.Length > 0)
                {
                    currentSection = section;
                    doc.EnsureSection(section);
                }

                continue;
            }

            if (trimmed.StartsWith("- [") || trimmed.StartsWith("[GPU"))
            {
                doc._gpuLines.Add(trimmed.TrimStart('-').Trim());
                continue;
            }

            var colonIndex = IndexOfColon(trimmed);
            if (colonIndex > 0)
            {
                var key = trimmed[..colonIndex].Trim().TrimStart('-').Trim();
                var value = trimmed[(colonIndex + 1)..].Trim();

                if (key.Length > 0)
                {
                    if (currentSection is not null)
                    {
                        doc.SetSectionValue(currentSection, key, value);
                    }
                    else
                    {
                        doc._headers[key] = value;
                    }
                }

                continue;
            }
        }

        var sampleMatch = SampleCountRegex.Match(text);
        if (sampleMatch.Success && int.TryParse(sampleMatch.Groups[1].Value, out var sampleCount))
        {
            doc.SampleCount = sampleCount;
        }

        var durationMatch = DurationRegex.Match(text);
        if (durationMatch.Success)
        {
            doc.DurationText = durationMatch.Groups[1].Value;
        }

        if (doc._headers.TryGetValue("量測時間", out var measuredAtText))
        {
            doc.MeasuredAtText = measuredAtText;
            if (DateTime.TryParse(measuredAtText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var measuredAt))
            {
                doc.MeasuredAt = measuredAt;
            }
        }

        if (doc._sections.Count == 0)
        {
            doc._diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "REF_NO_SECTIONS",
                $"{Path.GetFileName(path)}: 找不到可辨識的區段"));
        }

        return doc;
    }

    public string? DeviceName => GetValue("作業系統資訊", "裝置名稱");

    public RefSummary GetSummary()
    {
        return new RefSummary(
            ExtractNumber(GetValue("CPU 使用率", "最低")),
            ExtractNumber(GetValue("CPU 使用率", "最高")),
            ExtractNumber(GetValue("CPU 使用率", "平均")),
            ExtractNumber(GetValue("RAM 使用量", "最低")),
            ExtractNumber(GetValue("RAM 使用量", "最高")),
            ExtractNumber(GetValue("RAM 使用量", "平均")),
            ExtractPercent(GetValue("RAM 使用量", "最低")),
            ExtractPercent(GetValue("RAM 使用量", "最高")),
            ExtractPercent(GetValue("RAM 使用量", "平均")));
    }

    public HardwareInfo GetHardware()
    {
        return new HardwareInfo
        {
            Cpu = GetValue("CPU 處理器資訊", "型號"),
            Cores = (int?)ExtractNumber(GetValue("CPU 處理器資訊", "核心數目")),
            LogicalCpus = (int?)ExtractNumber(GetValue("CPU 處理器資訊", "邏輯處理器")),
            RamGB = ExtractNumber(GetValue("RAM 資訊", "已安裝容量")),
            RamSpeed = GetValue("RAM 資訊", "速度"),
            OsName = GetValue("作業系統資訊", "作業系統名稱"),
            OsVersion = GetValue("作業系統資訊", "版本"),
            OsBuild = GetValue("作業系統資訊", "OS 組建"),
            Gpu = _gpuLines.Count > 0 ? string.Join("; ", _gpuLines) : null
        };
    }

    public string? GetValue(string sectionKeyword, params string[] keyKeywords)
    {
        var normalizedSectionKeyword = Normalize(sectionKeyword);

        foreach (var (sectionName, values) in _sections)
        {
            if (!Normalize(sectionName).Contains(normalizedSectionKeyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var (key, value) in values)
            {
                var normalizedKey = Normalize(key);
                foreach (var keyKeyword in keyKeywords)
                {
                    if (normalizedKey.Contains(Normalize(keyKeyword), StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private static int IndexOfColon(string text)
    {
        var fullWidth = text.IndexOf('：');
        var halfWidth = text.IndexOf(':');
        if (fullWidth < 0)
        {
            return halfWidth;
        }

        if (halfWidth < 0)
        {
            return fullWidth;
        }

        return Math.Min(fullWidth, halfWidth);
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var raw in text)
        {
            if (char.IsWhiteSpace(raw))
            {
                continue;
            }

            var c = raw switch
            {
                '（' => '(',
                '）' => ')',
                '％' => '%',
                _ => raw
            };

            if (c is '(' or ')' or ':' or '：' or '-' or '／' or '/')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    private static double? ExtractNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = NumberRegex.Match(text);
        return match.Success
               && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? ExtractPercent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = PercentRegex.Match(text);
        return match.Success
               && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private void EnsureSection(string section)
    {
        if (!_sections.ContainsKey(section))
        {
            _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SetSectionValue(string section, string key, string value)
    {
        EnsureSection(section);
        _sections[section][key] = value;
    }
}

public sealed record RefSummary(
    double? CpuMin,
    double? CpuMax,
    double? CpuAvg,
    double? RamMinGb,
    double? RamMaxGb,
    double? RamAvgGb,
    double? RamMinPercent,
    double? RamMaxPercent,
    double? RamAvgPercent);
