using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlServerMcpServer;
using SqlServerMcpServer.Services;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Setup dependency injection
var services = new ServiceCollection();

// Add logging - CRITICAL: Don't log to console as it interferes with stdio!
services.AddLogging(builder =>
{
    // Log to a file instead of console
    var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(logPath);
    var logFile = Path.Combine(logPath, $"mcp-server-{DateTime.Now:yyyyMMdd}.log");

    // Create a simple file logger
    builder.AddProvider(new FileLoggerProvider(logFile));
    builder.SetMinimumLevel(LogLevel.Information);

#if DEBUG
    builder.SetMinimumLevel(LogLevel.Debug);
#endif
});

// Add configuration
services.AddSingleton<IConfiguration>(configuration);

// Add services
services.AddSingleton<DatabaseService>();
services.AddSingleton<SqlServerTools>();
services.AddSingleton<McpServer>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Get logger for startup
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

try
{
    // Test database connection (log to file only, no console output)
    var dbService = serviceProvider.GetRequiredService<DatabaseService>();
    var canConnect = await dbService.TestConnectionAsync();

    if (!canConnect)
    {
        logger.LogCritical("Failed to connect to database. Please check your connection string in appsettings.json");
        Environment.Exit(1);
    }

    logger.LogInformation("Database connection successful");

    // Start MCP server
    var server = serviceProvider.GetRequiredService<McpServer>();
    await server.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Fatal error starting server");
    Environment.Exit(1);
}
finally
{
    if (serviceProvider is IDisposable disposable)
    {
        disposable.Dispose();
    }
}