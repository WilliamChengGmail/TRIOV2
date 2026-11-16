using System.Text;

namespace PerformanceAnalysis.Core.Parsing;

public static class LogColumnMap
{
    public static string Normalize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(header.Length);
        foreach (var raw in header.Trim())
        {
            if (char.IsWhiteSpace(raw) || raw == '\uFEFF')
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

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    public static int FindIndex(IReadOnlyList<string> headers, params string[] aliases)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var normalizedHeader = Normalize(headers[i]);
            foreach (var alias in aliases)
            {
                if (string.Equals(normalizedHeader, Normalize(alias), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    public static string? Get(IReadOnlyList<string> fields, int index)
        => index >= 0 && index < fields.Count ? fields[index] : null;
}
