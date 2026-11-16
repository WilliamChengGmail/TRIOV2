using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.Core.Reporting;

public static class HtmlReportBuilder
{
    private const string TemplateResource = "PerformanceAnalysis.Core.Reporting.Assets.template.html";
    private const string CssResource = "PerformanceAnalysis.Core.Reporting.Assets.report.css";
    private const string ScriptResource = "PerformanceAnalysis.Core.Reporting.Assets.report.js";
    private const string EChartsResource = "PerformanceAnalysis.Core.Reporting.Assets.echarts.min.js";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Build(IReadOnlyList<Session> sessions, string sourcePath)
        => Build(ReportBuilder.Build(sessions, sourcePath));

    public static string Build(ReportModel model)
    {
        var template = ReadResource(TemplateResource);
        var css = ReadResource(CssResource);
        var script = ReadResource(ScriptResource);
        var echarts = ReadResource(EChartsResource);

        script = script.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
        echarts = echarts.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(model, JsonOptions)
            .Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase)
            .Replace("<", "\\u003C", StringComparison.Ordinal);

        return template
            .Replace("{{TITLE}}", WebUtility.HtmlEncode(model.Title), StringComparison.Ordinal)
            .Replace("{{GENERATED_AT}}", WebUtility.HtmlEncode(
                model.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss")), StringComparison.Ordinal)
            .Replace("{{CSS}}", css, StringComparison.Ordinal)
            .Replace("{{ECHARTS}}", echarts, StringComparison.Ordinal)
            .Replace("{{APP_JS}}", script, StringComparison.Ordinal)
            .Replace("{{DATA}}", json, StringComparison.Ordinal);
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(HtmlReportBuilder).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"找不到內嵌資源：{name}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
