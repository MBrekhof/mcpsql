using Microsoft.Extensions.Logging;
using SqlServerMcpServer.Protocol;
using SqlServerMcpServer.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlServerMcpServer;

/// <summary>
/// Main MCP server that handles JSON-RPC communication via stdio
/// </summary>
public class McpServer
{
    private readonly SqlServerTools _tools;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _isInitialized = false;

    public McpServer(SqlServerTools tools, ILogger<McpServer> logger)
    {
        _tools = tools;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Main message processing loop - reads from stdin, writes to stdout
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("MCP Server starting...");

        try
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                _logger.LogDebug("Received: {Line}", line);

                try
                {
                    var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                    if (request == null)
                    {
                        await SendErrorAsync(writer, null, JsonRpcErrorCodes.ParseError, "Failed to parse request");
                        continue;
                    }

                    var response = await HandleRequestAsync(request);
                    var responseJson = JsonSerializer.Serialize(response, _jsonOptions);

                    _logger.LogDebug("Sending: {Response}", responseJson);
                    await writer.WriteLineAsync(responseJson);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON parse error");
                    await SendErrorAsync(writer, null, JsonRpcErrorCodes.ParseError, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing request");
                    await SendErrorAsync(writer, null, JsonRpcErrorCodes.InternalError, "Internal server error");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in message loop");
            throw;
        }

        _logger.LogInformation("MCP Server stopped");
    }

    private async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
    {
        try
        {
            return request.Method switch
            {
                "initialize" => await HandleInitializeAsync(request),
                "initialized" => await HandleInitializedNotification(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolsCallAsync(request),
                "ping" => HandlePing(request),
                _ => CreateErrorResponse(request.Id, JsonRpcErrorCodes.MethodNotFound, $"Method not found: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling method: {Method}", request.Method);
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    private async Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request)
    {
        _logger.LogInformation("Handling initialize request");
        
        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            var initParams = JsonSerializer.Deserialize<InitializeParams>(paramsJson, _jsonOptions);

            _logger.LogInformation("Client: {ClientName} {ClientVersion}, Protocol: {ProtocolVersion}",
                initParams?.ClientInfo?.Name,
                initParams?.ClientInfo?.Version,
                initParams?.ProtocolVersion);

            var result = new InitializeResult
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability { ListChanged = false }
                },
                ServerInfo = new ServerInfo
                {
                    Name = "sql-server-mcp",
                    Version = "1.0.0"
                }
            };

            _isInitialized = true;

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initialization");
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, "Initialization failed");
        }
    }

    private Task<JsonRpcResponse> HandleInitializedNotification(JsonRpcRequest request)
    {
        _logger.LogInformation("Client initialized notification received");

        // Notifications don't require a response, but we'll return an empty one
        return Task.FromResult(new JsonRpcResponse
        {
            Id = request.Id,
            Result = new { }
        });
    }

    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        if (!_isInitialized)
        {
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidRequest, "Server not initialized");
        }

        _logger.LogInformation("Listing tools");

        var tools = _tools.GetToolDefinitions();
        var result = new ToolsListResult { Tools = tools };

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
    {
        if (!_isInitialized)
        {
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidRequest, "Server not initialized");
        }

        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            var callParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, _jsonOptions);

            if (callParams == null || string.IsNullOrWhiteSpace(callParams.Name))
            {
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Tool name is required");
            }

            _logger.LogInformation("Calling tool: {ToolName}", callParams.Name);

            // Convert arguments - handle JsonElement properly
            Dictionary<string, object>? arguments = null;
            if (callParams.Arguments != null)
            {
                arguments = new Dictionary<string, object>();
                foreach (var kvp in callParams.Arguments)
                {
                    arguments[kvp.Key] = kvp.Value;
                }
            }

            var result = await _tools.ExecuteToolAsync(callParams.Name, arguments);

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling tool");
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, $"Tool execution failed: {ex.Message}");
        }
    }

    private JsonRpcResponse HandlePing(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new { status = "ok" }
        };
    }

    private JsonRpcResponse CreateErrorResponse(object? id, int code, string message)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        };
    }

    private async Task SendErrorAsync(StreamWriter writer, object? id, int code, string message)
    {
        var response = CreateErrorResponse(id, code, message);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await writer.WriteLineAsync(json);
    }
}