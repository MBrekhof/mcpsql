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
    // Test the active database connection (log to file only, no console output)
    var dbService = serviceProvider.GetRequiredService<DatabaseService>();

    var configured = dbService.GetDatabases();
    logger.LogInformation("Configured databases: {Names}",
        string.Join(", ", configured.Select(d => d.Display)));

    var active = dbService.CurrentDatabase;
    var canConnect = await dbService.TestConnectionAsync();

    if (!canConnect)
    {
        logger.LogCritical("Failed to connect to active database '{Name}'. Please check its connection string in appsettings.json", active.Name);
        Environment.Exit(1);
    }

    logger.LogInformation("Database connection successful (active: {Name})", active.Name);

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