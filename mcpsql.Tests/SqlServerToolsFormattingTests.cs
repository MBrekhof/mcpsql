using SqlServerMcpServer.Models;
using SqlServerMcpServer.Services;

namespace SqlServerMcpServer.Tests;

/// <summary>
/// Characterization tests for the result-formatting path every tool response flows through
/// (<see cref="SqlServerTools.FormatQueryResult"/>). These lock in cell truncation, NULL rendering
/// and column alignment so a change to display logic can't silently reshape tool output.
/// </summary>
public class SqlServerToolsFormattingTests
{
    private const int DefaultMaxCellWidth = 1000;

    private static QueryResult Result(List<string> columns, params Dictionary<string, object?>[] rows)
        => new()
        {
            ColumnNames = columns,
            Rows = rows.ToList(),
            RowCount = rows.Length
        };

    [Fact]
    public void FormatQueryResult_WithNoRows_ReturnsMarkerInsteadOfEmptyTable()
    {
        var result = Result(new List<string> { "Id" });

        var text = SqlServerTools.FormatQueryResult(result, DefaultMaxCellWidth);

        Assert.Equal("(No rows returned)", text);
    }

    [Fact]
    public void FormatQueryResult_RendersHeaderSeparatorAndOneLinePerRow()
    {
        var result = Result(
            new List<string> { "Id", "Name" },
            new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Ann" },
            new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "Bob" });

        var lines = SqlServerTools.FormatQueryResult(result, DefaultMaxCellWidth)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(4, lines.Length); // header + separator + 2 rows
        Assert.Equal("Id | Name", lines[0]);
        Assert.Equal("---+-----", lines[1]);
        Assert.Equal("1  | Ann ", lines[2]); // every cell is padded, including the last on the line
        Assert.Equal("2  | Bob ", lines[3]);
    }

    [Fact]
    public void FormatQueryResult_RendersNullValueAsNullLiteral()
    {
        var result = Result(
            new List<string> { "Name" },
            new Dictionary<string, object?> { ["Name"] = null });

        var text = SqlServerTools.FormatQueryResult(result, DefaultMaxCellWidth);

        Assert.Contains("NULL", text);
    }

    [Fact]
    public void FormatQueryResult_RendersMissingColumnAsNullLiteral()
    {
        // A row dictionary that simply lacks the column must not throw.
        var result = Result(
            new List<string> { "Id", "Absent" },
            new Dictionary<string, object?> { ["Id"] = 1 });

        var text = SqlServerTools.FormatQueryResult(result, DefaultMaxCellWidth);

        Assert.Contains("NULL", text);
    }

    [Fact]
    public void FormatQueryResult_TruncatesOverlongCellToExactlyMaxCellWidth()
    {
        var result = Result(
            new List<string> { "Text" },
            new Dictionary<string, object?> { ["Text"] = new string('x', 200) });

        var cell = SqlServerTools.FormatQueryResult(result, 60)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[2];

        Assert.Equal(60, cell.Length);
        Assert.EndsWith("...", cell);
    }

    [Fact]
    public void FormatQueryResult_LeavesCellAtExactlyMaxCellWidthIntact()
    {
        var value = new string('x', 60);
        var result = Result(
            new List<string> { "Text" },
            new Dictionary<string, object?> { ["Text"] = value });

        var cell = SqlServerTools.FormatQueryResult(result, 60)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[2];

        Assert.Equal(value, cell);
        Assert.DoesNotContain("...", cell);
    }

    [Fact]
    public void FormatQueryResult_PadsCellsSoColumnsStayAligned()
    {
        var result = Result(
            new List<string> { "Name", "City" },
            new Dictionary<string, object?> { ["Name"] = "Ann", ["City"] = "Amsterdam" },
            new Dictionary<string, object?> { ["Name"] = "Bartholomew", ["City"] = "Ede" });

        var lines = SqlServerTools.FormatQueryResult(result, DefaultMaxCellWidth)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.All(lines, line => Assert.Equal(lines[0].Length, line.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void FormatQueryResult_WithMaxCellWidthTooSmallForEllipsis_DoesNotThrow(int maxCellWidth)
    {
        // McpServer:MaxCellWidth is operator-supplied and unvalidated; a value below 4 leaves no room
        // for the "..." suffix and must not blow up the whole tool call.
        var result = Result(
            new List<string> { "Text" },
            new Dictionary<string, object?> { ["Text"] = "a long value" });

        var text = SqlServerTools.FormatQueryResult(result, maxCellWidth);

        Assert.False(string.IsNullOrWhiteSpace(text));
    }
}
