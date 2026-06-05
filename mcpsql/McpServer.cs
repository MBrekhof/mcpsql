using Microsoft.Extensions.Logging;
using SqlServerMcpServer.Protocol;
using SqlServerMcpServer.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlServerMcpServer;

/// <summary>
/// Main MCP server that handles JSON-RPC communication via stdio
/// </summary>
public class McpServer
{
    private const string DatabaseInfoResourceUri = "database://info";
    private const string DatabaseObjectResourceTemplate = "database://schema/{schema}/{object_name}";

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
                    if (response != null)
                    {
                        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);

                        _logger.LogDebug("Sending: {Response}", responseJson);
                        await writer.WriteLineAsync(responseJson);
                    }
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

    private async Task<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest request)
    {
        try
        {
            return request.Method switch
            {
                "initialize" => await HandleInitializeAsync(request),
                "initialized" => await HandleInitializedNotificationAsync(request),
                "notifications/initialized" => await HandleInitializedNotificationAsync(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolsCallAsync(request),
                "resources/list" => HandleResourcesList(request),
                "resources/templates/list" => HandleResourceTemplatesList(request),
                "resources/read" => await HandleResourcesReadAsync(request),
                "ping" => HandlePing(request),
                _ => request.Id == null
                    ? null
                    : CreateErrorResponse(request.Id, JsonRpcErrorCodes.MethodNotFound, $"Method not found: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling method: {Method}", request.Method);
            return request.Id == null
                ? null
                : CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
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
                    Tools = new ToolsCapability { ListChanged = false },
                    Resources = new ResourcesCapability
                    {
                        Subscribe = false,
                        ListChanged = false
                    }
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

    private Task<JsonRpcResponse?> HandleInitializedNotificationAsync(JsonRpcRequest request)
    {
        _logger.LogInformation("Client initialized notification received");

        if (request.Id == null)
        {
            return Task.FromResult<JsonRpcResponse?>(null);
        }

        return Task.FromResult<JsonRpcResponse?>(new JsonRpcResponse
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

    private JsonRpcResponse HandleResourcesList(JsonRpcRequest request)
    {
        if (!_isInitialized)
        {
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidRequest, "Server not initialized");
        }

        var result = new ResourcesListResult
        {
            Resources = new List<McpResource>
            {
                new McpResource
                {
                    Uri = DatabaseInfoResourceUri,
                    Name = "Database Info",
                    Description = "Basic metadata about the configured SQL Server database connection",
                    MimeType = "text/plain"
                }
            }
        };

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    private JsonRpcResponse HandleResourceTemplatesList(JsonRpcRequest request)
    {
        if (!_isInitialized)
        {
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidRequest, "Server not initialized");
        }

        var result = new ResourceTemplatesListResult
        {
            ResourceTemplates = new List<McpResourceTemplate>
            {
                new McpResourceTemplate
                {
                    UriTemplate = DatabaseObjectResourceTemplate,
                    Name = "Database Object Schema",
                    Description = "Detailed schema information for a table or view",
                    MimeType = "text/plain"
                }
            }
        };

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    private async Task<JsonRpcResponse> HandleResourcesReadAsync(JsonRpcRequest request)
    {
        if (!_isInitialized)
        {
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidRequest, "Server not initialized");
        }

        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            var readParams = JsonSerializer.Deserialize<ReadResourceParams>(paramsJson, _jsonOptions);

            if (readParams == null || string.IsNullOrWhiteSpace(readParams.Uri))
            {
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Resource URI is required");
            }

            var result = await ReadResourceAsync(readParams.Uri);

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (ArgumentException ex)
        {
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading resource");
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, $"Resource read failed: {ex.Message}");
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

    private async Task<ResourcesReadResult> ReadResourceAsync(string uri)
    {
        if (string.Equals(uri, DatabaseInfoResourceUri, StringComparison.OrdinalIgnoreCase))
        {
            var toolResult = await _tools.ExecuteToolAsync("get_database_info", null);
            return CreateTextResourceResult(uri, ExtractToolText(toolResult));
        }

        if (TryParseSchemaResourceUri(uri, out var schema, out var objectName))
        {
            var toolResult = await _tools.ExecuteToolAsync("describe_table", new Dictionary<string, object>
            {
                ["schema"] = schema,
                ["object_name"] = objectName
            });

            return CreateTextResourceResult(uri, ExtractToolText(toolResult));
        }

        throw new ArgumentException($"Resource not found: {uri}");
    }

    private static ResourcesReadResult CreateTextResourceResult(string uri, string text)
    {
        return new ResourcesReadResult
        {
            Contents = new List<ResourceContents>
            {
                new ResourceContents
                {
                    Uri = uri,
                    MimeType = "text/plain",
                    Text = text
                }
            }
        };
    }

    private static string ExtractToolText(ToolCallResult toolResult)
    {
        var builder = new StringBuilder();

        foreach (var item in toolResult.Content)
        {
            if (!string.Equals(item.Type, "text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(item.Text);
        }

        var text = builder.ToString();
        if (toolResult.IsError == true)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? "Resource generation failed" : text);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("No text content was returned for the requested resource");
        }

        return text;
    }

    private static bool TryParseSchemaResourceUri(string uri, out string schema, out string objectName)
    {
        const string prefix = "database://schema/";

        schema = string.Empty;
        objectName = string.Empty;

        if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri[prefix.Length..].Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        schema = Uri.UnescapeDataString(segments[0]);
        objectName = Uri.UnescapeDataString(segments[1]);
        return !string.IsNullOrWhiteSpace(schema) && !string.IsNullOrWhiteSpace(objectName);
    }
}
