using SqlServerMcpServer.Services;

namespace SqlServerMcpServer.Tests;

/// <summary>
/// Characterization tests for the row-cap rewrite (<see cref="DatabaseService.ApplyRowLimit"/>),
/// which injects a TOP clause into user queries before execution.
/// </summary>
public class RowLimitTests
{
    [Fact]
    public void ApplyRowLimit_InjectsTopWhenQueryHasNone()
    {
        var sql = DatabaseService.ApplyRowLimit("SELECT Id FROM Users", 25);

        Assert.Equal("SELECT TOP 25 Id FROM Users", sql);
    }

    [Fact]
    public void ApplyRowLimit_InjectsTopForLowercaseSelect()
    {
        var sql = DatabaseService.ApplyRowLimit("select id from users", 25);

        Assert.Equal("SELECT TOP 25 id from users", sql);
    }

    [Fact]
    public void ApplyRowLimit_LeavesCallerSuppliedTopAlone()
    {
        var sql = DatabaseService.ApplyRowLimit("SELECT TOP 5 Id FROM Users", 25);

        Assert.Equal("SELECT TOP 5 Id FROM Users", sql);
    }

    [Fact]
    public void ApplyRowLimit_TrimsLeadingWhitespace()
    {
        var sql = DatabaseService.ApplyRowLimit("   SELECT Id FROM Users", 25);

        Assert.Equal("SELECT TOP 25 Id FROM Users", sql);
    }

    [Fact]
    public void ApplyRowLimit_DoesNotRewriteCteQueries()
    {
        // The TOP rewrite only matches a leading SELECT, and QueryValidator also allows WITH. That
        // is fine as long as nobody treats this rewrite as the only row cap: ExecuteQueryInternalAsync
        // enforces maxRows while reading, which is what actually limits CTE queries (ROW-001).
        const string cte = "WITH c AS (SELECT Id FROM Users) SELECT * FROM c";

        var sql = DatabaseService.ApplyRowLimit(cte, 25);

        Assert.Equal(cte, sql);
        Assert.DoesNotContain("TOP", sql);
    }

    [Fact]
    public void ApplyRowLimit_PlacesTopAfterDistinct()
    {
        // T-SQL grammar is SELECT [DISTINCT] [TOP n]; putting TOP first is a syntax error and made
        // every SELECT DISTINCT fail with "Incorrect syntax near the keyword 'DISTINCT'".
        var sql = DatabaseService.ApplyRowLimit("SELECT DISTINCT City FROM Users", 25);

        Assert.Equal("SELECT DISTINCT TOP 25 City FROM Users", sql);
    }

    [Fact]
    public void ApplyRowLimit_LeavesCallerSuppliedTopAloneAfterDistinct()
    {
        var sql = DatabaseService.ApplyRowLimit("SELECT DISTINCT TOP 5 City FROM Users", 25);

        Assert.Equal("SELECT DISTINCT TOP 5 City FROM Users", sql);
    }
}
