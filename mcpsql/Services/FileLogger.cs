using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace SqlServerMcpServer.Services;

/// <summary>
/// Simple file logger to avoid interfering with stdio communication
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logPath;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string logPath)
    {
        _logPath = logPath;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _logPath));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}

public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _logPath;
    private static readonly object _lock = new object();

    public FileLogger(string categoryName, string logPath)
    {
        _categoryName = categoryName;
        _logPath = logPath;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] {_categoryName}: {formatter(state, exception)}";

        if (exception != null)
        {
            message += Environment.NewLine + exception.ToString();
        }

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logPath, message + Environment.NewLine);
            }
            catch
            {
                // Silently fail if we can't write to log file
            }
        }
    }
}