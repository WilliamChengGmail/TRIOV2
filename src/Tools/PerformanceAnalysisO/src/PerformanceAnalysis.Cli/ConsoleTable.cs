using System.Text;

namespace PerformanceAnalysis.Cli;

internal static class ConsoleTable
{
    public static void Write(
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows,
        IReadOnlyList<bool>? rightAligned = null,
        IReadOnlyList<int>? maxWidths = null)
    {
        var widths = new int[headers.Count];
        for (var i = 0; i < headers.Count; i++)
        {
            widths[i] = DisplayWidth(headers[i]);
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < widths.Length && i < row.Length; i++)
            {
                widths[i] = Math.Max(widths[i], DisplayWidth(row[i]));
            }
        }

        if (maxWidths is not null)
        {
            for (var i = 0; i < widths.Length && i < maxWidths.Count; i++)
            {
                if (maxWidths[i] > 0)
                {
                    widths[i] = Math.Min(widths[i], maxWidths[i]);
                }
            }
        }

        WriteRow(headers, widths, rightAligned, isHeader: true);
        Console.WriteLine(string.Join("  ", widths.Select(width => new string('-', width))));

        foreach (var row in rows)
        {
            WriteRow(row, widths, rightAligned, isHeader: false);
        }
    }

    private static void WriteRow(
        IReadOnlyList<string> values,
        IReadOnlyList<int> widths,
        IReadOnlyList<bool>? rightAligned,
        bool isHeader)
    {
        var cells = new List<string>(widths.Count);
        for (var i = 0; i < widths.Count; i++)
        {
            var value = i < values.Count ? values[i] ?? string.Empty : string.Empty;
            value = Fit(value, widths[i]);
            var padding = widths[i] - DisplayWidth(value);
            var alignRight = !isHeader && rightAligned is not null && i < rightAligned.Count && rightAligned[i];
            cells.Add(alignRight
                ? new string(' ', padding) + value
                : value + new string(' ', padding));
        }

        Console.WriteLine(string.Join("  ", cells));
    }

    private static string Fit(string text, int maxWidth)
    {
        if (maxWidth <= 0 || DisplayWidth(text) <= maxWidth)
        {
            return text;
        }

        var builder = new StringBuilder();
        var width = 0;
        foreach (var c in text)
        {
            var charWidth = IsWide(c) ? 2 : 1;
            if (width + charWidth > maxWidth - 1)
            {
                break;
            }

            builder.Append(c);
            width += charWidth;
        }

        return builder.Append('…').ToString();
    }

    private static int DisplayWidth(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var width = 0;
        foreach (var c in text)
        {
            width += IsWide(c) ? 2 : 1;
        }

        return width;
    }

    private static bool IsWide(char c)
    {
        return c >= 0x1100
               && (c <= 0x115F
                   || c is '\u2329' or '\u232A'
                   || (c >= 0x2E80 && c <= 0xA4CF && c != '\u303F')
                   || (c >= 0xAC00 && c <= 0xD7A3)
                   || (c >= 0xF900 && c <= 0xFAFF)
                   || (c >= 0xFE10 && c <= 0xFE19)
                   || (c >= 0xFE30 && c <= 0xFE6F)
                   || (c >= 0xFF00 && c <= 0xFF60)
                   || (c >= 0xFFE0 && c <= 0xFFE6)
                   || (c >= 0x20000 && c <= 0x3FFFD));
    }
}
