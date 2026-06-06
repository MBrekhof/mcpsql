using SqlServerMcpServer.Services;

namespace SqlServerMcpServer.Tests;

/// <summary>
/// Characterization tests for <see cref="QueryValidator"/>, the read-only security boundary.
/// These lock in the current block-list behavior so changes that loosen it are caught.
/// </summary>
public class QueryValidatorTests
{
    [Theory]
    [InlineData("SELECT * FROM Users")]
    [InlineData("   SELECT 1")]
    [InlineData("select id, name from dbo.Customers")]
    [InlineData("WITH cte AS (SELECT 1 AS x) SELECT * FROM cte")]
    [InlineData("SELECT CreatedDate, UpdatedAt FROM Orders")] // 'Created'/'Updated' must not trip CREATE/UPDATE
    [InlineData("SELECT * FROM Users WHERE Name = 'DELETE me'")] // keyword inside a string literal is ignored
    [InlineData("SELECT 1;")] // a single trailing semicolon is allowed
    public void ValidateQuery_AllowsReadOnlyQueries(string query)
    {
        var (isValid, error) = QueryValidator.ValidateQuery(query);

        Assert.True(isValid, $"Expected valid, got error: {error}");
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateQuery_RejectsEmptyQuery(string? query)
    {
        var (isValid, error) = QueryValidator.ValidateQuery(query!);

        Assert.False(isValid);
        Assert.Equal("Query cannot be empty", error);
    }

    [Theory]
    [InlineData("INSERT INTO Users VALUES (1)", "INSERT")]
    [InlineData("UPDATE Users SET Name = 'x'", "UPDATE")]
    [InlineData("DELETE FROM Users", "DELETE")]
    [InlineData("DROP TABLE Users", "DROP")]
    [InlineData("CREATE TABLE T (Id int)", "CREATE")]
    [InlineData("ALTER TABLE Users ADD Col int", "ALTER")]
    [InlineData("TRUNCATE TABLE Users", "TRUNCATE")]
    [InlineData("EXEC sp_who", "EXEC")]
    [InlineData("MERGE INTO Target USING Source ON 1=1", "MERGE")]
    [InlineData("GRANT SELECT ON Users TO bob", "GRANT")]
    public void ValidateQuery_RejectsBlockedKeywords(string query, string keyword)
    {
        var (isValid, error) = QueryValidator.ValidateQuery(query);

        Assert.False(isValid);
        Assert.Equal($"Query contains blocked keyword: {keyword}", error);
    }

    [Theory]
    [InlineData("SELECT * INTO NewTable FROM Users")] // SELECT ... INTO
    [InlineData("SELECT * FROM OPENROWSET('x','y','z')")]
    [InlineData("SELECT * FROM OPENDATASOURCE('x','y')")]
    [InlineData("SELECT * FROM Users -- sneaky comment")]
    [InlineData("SELECT * FROM Users /* block comment */")]
    public void ValidateQuery_RejectsDangerousPatterns(string query)
    {
        var (isValid, error) = QueryValidator.ValidateQuery(query);

        Assert.False(isValid);
        Assert.Equal("Query contains potentially dangerous pattern", error);
    }

    [Theory]
    [InlineData("EXPLAIN SELECT 1")]
    [InlineData("PRINT 'hi'")]
    public void ValidateQuery_RequiresSelectOrWithStart(string query)
    {
        var (isValid, error) = QueryValidator.ValidateQuery(query);

        Assert.False(isValid);
        Assert.Equal("Query must start with SELECT or WITH (Common Table Expression)", error);
    }

    [Theory]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1; SELECT 2;")]
    public void ValidateQuery_RejectsMultipleStatements(string query)
    {
        var (isValid, error) = QueryValidator.ValidateQuery(query);

        Assert.False(isValid);
        Assert.Equal("Multiple SQL statements are not allowed", error);
    }

    [Theory]
    [InlineData("Users", "Users")]
    [InlineData("dbo.Users", "dbo.Users")]
    [InlineData("Users; DROP TABLE Orders", "UsersDROPTABLEOrders")] // injection chars stripped
    [InlineData("User_Name", "User_Name")]
    public void SanitizeIdentifier_StripsUnsafeCharacters(string input, string expected)
    {
        Assert.Equal(expected, QueryValidator.SanitizeIdentifier(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!@#$%")]
    public void SanitizeIdentifier_ThrowsOnNoValidCharacters(string input)
    {
        Assert.Throws<ArgumentException>(() => QueryValidator.SanitizeIdentifier(input));
    }

    [Fact]
    public void BuildTableIdentifier_BracketsSchemaAndTable()
    {
        Assert.Equal("[dbo].[Users]", QueryValidator.BuildTableIdentifier("dbo", "Users"));
    }

    [Fact]
    public void BuildTableIdentifier_SanitizesBeforeBracketing()
    {
        Assert.Equal("[dbo].[UsersDROP]", QueryValidator.BuildTableIdentifier("dbo", "Users; DROP"));
    }
}
