using Microsoft.Extensions.Logging;
using SqlServerMcpServer.Protocol;
using System.Text.Json;

namespace SqlServerMcpServer.Services;

/// <summary>
/// Defines and executes all SQL Server MCP tools
/// </summary>
public class SqlServerTools
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<SqlServerTools> _logger;

    public SqlServerTools(DatabaseService databaseService, ILogger<SqlServerTools> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available tool definitions
    /// </summary>
    public List<McpTool> GetToolDefinitions()
    {
        return new List<McpTool>
        {
            new McpTool
            {
                Name = "get_database_info",
                Description = "Get general information about the SQL Server database including server version, schemas, and object counts",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>()
                }
            },
            new McpTool
            {
                Name = "list_tables",
                Description = "List all tables in the database with optional filtering by schema and name pattern",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["schema"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Filter by schema name (optional)"
                        },
                        ["pattern"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Filter by table name pattern using SQL LIKE syntax, e.g., 'Customer%' (optional)"
                        }
                    }
                }
            },
            new McpTool
            {
                Name = "list_views",
                Description = "List all views in the database with optional filtering by schema and name pattern",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["schema"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Filter by schema name (optional)"
                        },
                        ["pattern"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Filter by view name pattern using SQL LIKE syntax (optional)"
                        }
                    }
                }
            },
            new McpTool
            {
                Name = "get_table_count",
                Description = "Get the exact count of records in a specific table",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["schema"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Schema name (default: 'dbo')",
                            Default = "dbo"
                        },
                        ["table"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Table name (required)"
                        }
                    },
                    Required = new List<string> { "table" }
                }
            },
            new McpTool
            {
                Name = "describe_table",
                Description = "Get detailed structure of a table or view including columns, data types, constraints, indexes, and foreign keys",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["schema"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Schema name (default: 'dbo')",
                            Default = "dbo"
                        },
                        ["object_name"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Table or view name (required)"
                        }
                    },
                    Required = new List<string> { "object_name" }
                }
            },
            new McpTool
            {
                Name = "preview_data",
                Description = "Preview sample rows from a table or view with optional ordering",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["schema"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Schema name (default: 'dbo')",
                            Default = "dbo"
                        },
                        ["object_name"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Table or view name (required)"
                        },
                        ["top_n"] = new PropertySchema
                        {
                            Type = "integer",
                            Description = "Number of rows to return (default: 10, max: 100)",
                            Default = 10,
                            Minimum = 1,
                            Maximum = 100
                        },
                        ["order_by"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Column(s) to order by, e.g., 'CustomerID DESC' or 'LastName, FirstName' (optional)"
                        }
                    },
                    Required = new List<string> { "object_name" }
                }
            },
            new McpTool
            {
                Name = "execute_query",
                Description = "Execute a custom SELECT query (READ-ONLY). The query will be validated to ensure it only performs read operations. Supports SELECT and WITH (CTE) statements only.",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["query"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "SQL SELECT query to execute (required)"
                        },
                        ["max_rows"] = new PropertySchema
                        {
                            Type = "integer",
                            Description = "Maximum number of rows to return (default: 100, max: 1000)",
                            Default = 100,
                            Minimum = 1,
                            Maximum = 1000
                        }
                    },
                    Required = new List<string> { "query" }
                }
            },
            new McpTool
            {
                Name = "search_columns",
                Description = "Search for tables and views that contain columns matching a specific pattern. Useful for finding where certain data is stored.",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["column_pattern"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Column name pattern using SQL LIKE syntax, e.g., '%Email%' or 'Customer%' (required)"
                        }
                    },
                    Required = new List<string> { "column_pattern" }
                }
            },
            new McpTool
            {
                Name = "get_foreign_keys",
                Description = "Get all foreign key relationships for a specific table, showing both parent and child relationships",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["schema"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Schema name (default: 'dbo')",
                            Default = "dbo"
                        },
                        ["table"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "Table name (required)"
                        }
                    },
                    Required = new List<string> { "table" }
                }
            }
        };
    }

    /// <summary>
    /// Executes a tool by name with provided arguments
    /// </summary>
    public async Task<ToolCallResult> ExecuteToolAsync(string toolName, Dictionary<string, object>? arguments)
    {
        try
        {
            _logger.LogInformation("Executing tool: {ToolName} with arguments: {Arguments}",
                toolName, JsonSerializer.Serialize(arguments));

            var result = toolName switch
            {
                "get_database_info" => await GetDatabaseInfoAsync(),
                "list_tables" => await ListTablesAsync(arguments),
                "list_views" => await ListViewsAsync(arguments),
                "get_table_count" => await GetTableCountAsync(arguments),
                "describe_table" => await DescribeTableAsync(arguments),
                "preview_data" => await PreviewDataAsync(arguments),
                "execute_query" => await ExecuteQueryAsync(arguments),
                "search_columns" => await SearchColumnsAsync(arguments),
                "get_foreign_keys" => await GetForeignKeysAsync(arguments),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool: {ToolName}", toolName);
            return new ToolCallResult
            {
                Content = new List<ContentItem>
                {
                    new ContentItem
                    {
                        Type = "text",
                        Text = $"Error: {ex.Message}"
                    }
                },
                IsError = true
            };
        }
    }

    private async Task<ToolCallResult> GetDatabaseInfoAsync()
    {
        var info = await _databaseService.GetDatabaseInfoAsync();

        var text = $@"Database Information:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Database Name: {info.DatabaseName}
Server Version: {info.ServerVersion}
Compatibility Level: {info.CompatibilityLevel}

Tables: {info.TableCount}
Views: {info.ViewCount}

Schemas ({info.Schemas.Count}):
{string.Join(", ", info.Schemas)}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = text }
            }
        };
    }

    private async Task<ToolCallResult> ListTablesAsync(Dictionary<string, object>? arguments)
    {
        var schema = GetArgument<string>(arguments, "schema");
        var pattern = GetArgument<string>(arguments, "pattern");

        var tables = await _databaseService.ListTablesAsync(schema, pattern);

        if (tables.Count == 0)
        {
            return new ToolCallResult
            {
                Content = new List<ContentItem>
                {
                    new ContentItem { Type = "text", Text = "No tables found matching the criteria." }
                }
            };
        }

        var text = $"Found {tables.Count} table(s):\n\n";
        text += string.Join("\n", tables.Select(t =>
            $"• {t.Schema}.{t.Name}" + (t.RowCount.HasValue ? $" ({t.RowCount:N0} rows)" : "")));

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = text }
            }
        };
    }

    private async Task<ToolCallResult> ListViewsAsync(Dictionary<string, object>? arguments)
    {
        var schema = GetArgument<string>(arguments, "schema");
        var pattern = GetArgument<string>(arguments, "pattern");

        var views = await _databaseService.ListViewsAsync(schema, pattern);

        if (views.Count == 0)
        {
            return new ToolCallResult
            {
                Content = new List<ContentItem>
                {
                    new ContentItem { Type = "text", Text = "No views found matching the criteria." }
                }
            };
        }

        var text = $"Found {views.Count} view(s):\n\n";
        text += string.Join("\n", views.Select(v => $"• {v.Schema}.{v.Name}"));

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = text }
            }
        };
    }

    private async Task<ToolCallResult> GetTableCountAsync(Dictionary<string, object>? arguments)
    {
        var schema = GetArgument<string>(arguments, "schema") ?? "dbo";
        var table = GetRequiredArgument<string>(arguments, "table");

        var count = await _databaseService.GetTableCountAsync(schema, table);

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem
                {
                    Type = "text",
                    Text = $"Table {schema}.{table} contains {count:N0} record(s)."
                }
            }
        };
    }

    private async Task<ToolCallResult> DescribeTableAsync(Dictionary<string, object>? arguments)
    {
        var schema = GetArgument<string>(arguments, "schema") ?? "dbo";
        var objectName = GetRequiredArgument<string>(arguments, "object_name");

        var structure = await _databaseService.DescribeTableAsync(schema, objectName);

        var text = $@"{structure.ObjectType}: {structure.Schema}.{structure.TableName}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

COLUMNS ({structure.Columns.Count}):
";

        foreach (var col in structure.Columns)
        {
            var flags = new List<string>();
            if (col.IsPrimaryKey) flags.Add("PK");
            if (col.IsForeignKey) flags.Add("FK");
            if (col.IsIdentity) flags.Add("IDENTITY");
            if (!col.IsNullable) flags.Add("NOT NULL");

            var flagsStr = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";
            var typeStr = col.DataType;
            if (col.MaxLength.HasValue && col.MaxLength > 0) typeStr += $"({col.MaxLength})";
            else if (col.Precision.HasValue) typeStr += $"({col.Precision},{col.Scale})";

            text += $"\n  {col.OrdinalPosition}. {col.ColumnName} - {typeStr}{flagsStr}";
            if (col.DefaultValue != null) text += $" = {col.DefaultValue}";
        }

        if (structure.Indexes.Count > 0)
        {
            text += $"\n\nINDEXES ({structure.Indexes.Count}):\n";
            foreach (var idx in structure.Indexes)
            {
                var type = idx.IsPrimaryKey ? "PRIMARY KEY" : idx.IsUnique ? "UNIQUE" : "INDEX";
                text += $"\n  • {idx.IndexName} ({type}): {string.Join(", ", idx.Columns)}";
            }
        }

        if (structure.ForeignKeys.Count > 0)
        {
            text += $"\n\nFOREIGN KEYS ({structure.ForeignKeys.Count}):\n";
            foreach (var fk in structure.ForeignKeys)
            {
                text += $"\n  • {fk.ConstraintName}:";
                text += $"\n    {fk.ForeignKeyColumn} → {fk.ReferencedSchema}.{fk.ReferencedTable}.{fk.ReferencedColumn}";
                text += $"\n    ON UPDATE {fk.UpdateRule}, ON DELETE {fk.DeleteRule}";
            }
        }

        if (!string.IsNullOrEmpty(structure.ViewDefinition))
        {
            text += $"\n\nVIEW DEFINITION:\n{structure.ViewDefinition}";
        }

        text += "\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = text }
            }
        };
    }

    private async Task<ToolCallResult> PreviewDataAsync(Dictionary<string, object>? arguments)
    {
        var schema = GetArgument<string>(arguments, "schema") ?? "dbo";
        var objectName = GetRequiredArgument<string>(arguments, "object_name");
        var topN = GetArgument<int?>(arguments, "top_n") ?? 10;
        var orderBy = GetArgument<string>(arguments, "order_by");

        var result = await _databaseService.PreviewDataAsync(schema, objectName, topN, orderBy);

        var text = $"Preview of {schema}.{objectName} (showing {result.RowCount} row(s), executed in {result.ExecutionTimeMs}ms):\n\n";
        text += FormatQueryResult(result);

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = text }
            }
        };
    }

    private async Task<ToolCallResult> ExecuteQueryAsync(Dictionary<string, object>? arguments)
    {
        var query = GetRequiredArgument<string>(arguments, "query");
        var maxRows = GetArgument<int?>(arguments, "max_rows") ?? 100;

        var result = await _databaseService.ExecuteQueryAsync(query, maxRows);

        var text = $"Query executed successfully ({result.RowCount} row(s) returned, {result.ExecutionTimeMs}ms)";
        if (result.WasTruncated)
        {
            text += $" - Results truncated to {maxRows} rows";
        }
        text += ":\n\n";
        text += FormatQueryResult(result);

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = text }
            }
        };
    }

    private async Task<ToolCallResult> SearchColumnsAsync(Dictionary<string, object>? arguments)
    {
        var pattern = GetRequiredArgument<string>(arguments, "column_pattern");

        var results = await _databaseService.SearchColumnsAsync(pattern);

        if (results.Count == 0)
        {
            return new ToolCallResult
            {
                Content = new List<ContentItem>
                {
                    new ContentItem { Type = "text", Text = $"No columns found matching pattern: {pattern}" }
                }
            };
        }

        var text = $"Found {results.Count} column(s) matching '{pattern}':\n\n";

        var grouped = results.GroupBy(r => (r.Schema, r.Table));
        foreach (var group in grouped)
        {
            text += $"• {group.Key.Schema}.{group.Key.Table}:\n";
            foreach (var col in group)
            {
                text += $"    - {col.Column} ({col.DataType})\n";
            }
        }

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = text }
            }
        };
    }

    private async Task<ToolCallResult> GetForeignKeysAsync(Dictionary<string, object>? arguments)
    {
        var schema = GetArgument<string>(arguments, "schema") ?? "dbo";
        var table = GetRequiredArgument<string>(arguments, "table");

        var structure = await _databaseService.DescribeTableAsync(schema, table);

        if (structure.ForeignKeys.Count == 0)
        {
            return new ToolCallResult
            {
                Content = new List<ContentItem>
                {
                    new ContentItem { Type = "text", Text = $"No foreign keys found for table {schema}.{table}" }
                }
            };
        }

        var text = $"Foreign Keys for {schema}.{table} ({structure.ForeignKeys.Count}):\n\n";
        foreach (var fk in structure.ForeignKeys)
        {
            text += $"• {fk.ConstraintName}:\n";
            text += $"    {fk.ForeignKeyColumn} → {fk.ReferencedSchema}.{fk.ReferencedTable}.{fk.ReferencedColumn}\n";
            text += $"    ON UPDATE {fk.UpdateRule}, ON DELETE {fk.DeleteRule}\n\n";
        }

        return new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = text }
            }
        };
    }

    private string FormatQueryResult(Models.QueryResult result)
    {
        if (result.Rows.Count == 0)
        {
            return "(No rows returned)";
        }

        var text = "";
        var colWidths = new Dictionary<string, int>();

        // Calculate column widths
        foreach (var col in result.ColumnNames)
        {
            colWidths[col] = col.Length;
        }

        foreach (var row in result.Rows)
        {
            foreach (var col in result.ColumnNames)
            {
                var value = row.ContainsKey(col) ? row[col]?.ToString() ?? "NULL" : "NULL";
                colWidths[col] = Math.Max(colWidths[col], Math.Min(value.Length, 50));
            }
        }

        // Header
        text += string.Join(" | ", result.ColumnNames.Select(c => c.PadRight(colWidths[c]))) + "\n";
        text += string.Join("-+-", result.ColumnNames.Select(c => new string('-', colWidths[c]))) + "\n";

        // Rows
        foreach (var row in result.Rows)
        {
            var values = result.ColumnNames.Select(col =>
            {
                var value = row.ContainsKey(col) ? row[col]?.ToString() ?? "NULL" : "NULL";
                if (value.Length > 50) value = value.Substring(0, 47) + "...";
                return value.PadRight(colWidths[col]);
            });
            text += string.Join(" | ", values) + "\n";
        }

        return text;
    }

    private T? GetArgument<T>(Dictionary<string, object>? arguments, string key)
    {
        if (arguments == null || !arguments.ContainsKey(key))
        {
            return default;
        }

        var value = arguments[key];
        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    private T GetRequiredArgument<T>(Dictionary<string, object>? arguments, string key)
    {
        var value = GetArgument<T>(arguments, key);
        if (value == null)
        {
            throw new ArgumentException($"Required argument '{key}' is missing");
        }
        return value;
    }
}