using Microsoft.Data.SqlClient;
using SqlServerMcpServer.Services;

namespace SqlServerMcpServer.Tests;

/// <summary>
/// Tests for per-connection query timeouts (<see cref="DatabaseService.WithCommandTimeout"/>).
/// A connection string may carry its own "Command Timeout"; otherwise the global
/// McpServer:QueryTimeoutSeconds applies.
/// </summary>
public class ConnectionTimeoutTests
{
    private const string Basic = "Server=localhost;Database=master;Integrated Security=SSPI";

    [Fact]
    public void WithCommandTimeout_AppliesGlobalDefault_WhenConnectionStringSpecifiesNone()
    {
        var result = DatabaseService.WithCommandTimeout(Basic, 45);

        Assert.Equal(45, new SqlConnectionStringBuilder(result).CommandTimeout);
    }

    [Fact]
    public void WithCommandTimeout_KeepsPerConnectionOverride()
    {
        var result = DatabaseService.WithCommandTimeout($"{Basic};Command Timeout=120", 45);

        Assert.Equal(120, new SqlConnectionStringBuilder(result).CommandTimeout);
    }

    [Fact]
    public void WithCommandTimeout_KeepsPerConnectionOverride_EvenWhenItMatchesAdoDefault()
    {
        // 30 is also ADO.NET's default, so a naive "is it still the default?" check would treat this
        // as unset and stomp it with the global value.
        var result = DatabaseService.WithCommandTimeout($"{Basic};Command Timeout=30", 45);

        Assert.Equal(30, new SqlConnectionStringBuilder(result).CommandTimeout);
    }

    [Fact]
    public void WithCommandTimeout_PreservesTheRestOfTheConnectionString()
    {
        var result = new SqlConnectionStringBuilder(DatabaseService.WithCommandTimeout(Basic, 45));

        Assert.Equal("localhost", result.DataSource);
        Assert.Equal("master", result.InitialCatalog);
        Assert.True(result.IntegratedSecurity);
    }
}
