using System.Text;

namespace PerformanceAnalysis.Core.Parsing;

public static class CsvTableReader
{
    /// <summary>
    /// Reads a recorder CSV file.
    /// Rules:
    /// - UTF-8 (BOM) is preferred, with UTF-16 and Big5 fallbacks.
    /// - Blank lines and lines whose first non-space character is '#' are skipped.
    /// - Fields are comma-separated; double quotes are supported.
    /// - Rows with a different field count are padded/truncated and counted as malformed.
    /// </summary>
    public static CsvTable Read(string path)
    {
        var text = TextFileReader.ReadAllText(path);
        using var reader = new StringReader(text);

        var headers = new List<string>();
        var rows = new List<CsvRow>();
        var commentLines = 0;
        var malformedRows = 0;
        var lineNumber = 0;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.TrimStart().StartsWith('#'))
            {
                commentLines++;
                continue;
            }

            var fields = ParseLine(line);
            if (headers.Count == 0)
            {
                headers = fields;
                continue;
            }

            if (fields.Count != headers.Count)
            {
                malformedRows++;
                if (fields.Count < headers.Count)
                {
                    while (fields.Count < headers.Count)
                    {
                        fields.Add(string.Empty);
                    }
                }
                else
                {
                    fields.RemoveRange(headers.Count, fields.Count - headers.Count);
                }
            }

            rows.Add(new CsvRow(lineNumber, fields));
        }

        return new CsvTable
        {
            Headers = headers,
            Rows = rows,
            CommentLineCount = commentLines,
            MalformedRowCount = malformedRows
        };
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else
            {
                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        fields.Add(field.ToString());
                        field.Clear();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }
        }

        fields.Add(field.ToString());
        return fields;
    }
}
