using System.Text;
using PerformanceAnalysis.Core.Parsing;

namespace PerformanceAnalysis.Core.Tests;

public sealed class CsvTableReaderTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void ReadsQuotedFieldsAndSkipsCommentLines()
    {
        var path = _workspace.WriteFile(
            "sample.csv",
            "a,b,c\r\n1,\"hello, world\",3\r\n# footer\r\n\r\n2,x,4\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var table = CsvTableReader.Read(path);

        Assert.Equal(new[] { "a", "b", "c" }, table.Headers);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("hello, world", table.Rows[0].Fields[1]);
        Assert.Equal(1, table.CommentLineCount);
        Assert.Equal(0, table.MalformedRowCount);
    }

    [Fact]
    public void PadsShortRowsAndTruncatesLongRows()
    {
        var path = _workspace.WriteFile(
            "sample.csv",
            "a,b,c\n1,2\n1,2,3,4\n");

        var table = CsvTableReader.Read(path);

        Assert.Equal(2, table.MalformedRowCount);
        Assert.Equal(3, table.Rows[0].Fields.Count);
        Assert.Equal(string.Empty, table.Rows[0].Fields[2]);
        Assert.Equal(3, table.Rows[1].Fields.Count);
        Assert.Equal("3", table.Rows[1].Fields[2]);
    }

    [Fact]
    public void FallsBackToBig5()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = "時間,值\r\n2026-01-01 00:00:00,1\r\n";
        var path = Path.Combine(_workspace.Root, "big5.csv");
        File.WriteAllBytes(path, Encoding.GetEncoding(950).GetBytes(text));

        var table = CsvTableReader.Read(path);

        Assert.Equal("時間", table.Headers[0]);
        Assert.Equal("2026-01-01 00:00:00", table.Rows[0].Fields[0]);
    }

    public void Dispose() => _workspace.Dispose();
}
