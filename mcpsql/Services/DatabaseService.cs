using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqlServerMcpServer.Models;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SqlServerMcpServer.Services;

/// <summary>
/// Service for interacting with SQL Server database
/// </summary>
public class DatabaseService
{
    private readonly string _connectionString;
    private readonly int _queryTimeoutSeconds;
    private readonly int _maxQueryRows;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");

        _queryTimeoutSeconds = configuration.GetValue<int>("McpServer:QueryTimeoutSeconds", 30);
        _maxQueryRows = configuration.GetValue<int>("McpServer:MaxQueryRows", 1000);
        _logger = logger;
    }

    /// <summary>
    /// Tests the database connection
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database");
            return false;
        }
    }

    /// <summary>
    /// Gets database information
    /// </summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var info = new DatabaseInfo
        {
            DatabaseName = connection.Database,
            ServerVersion = connection.ServerVersion
        };

        // Get compatibility level
        var compatCmd = new SqlCommand("SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()", connection);
        var compatLevel = await compatCmd.ExecuteScalarAsync();
        info.CompatibilityLevel = compatLevel?.ToString() ?? "Unknown";

        // Get schemas
        var schemaCmd = new SqlCommand(@"
            SELECT name FROM sys.schemas 
            WHERE name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest', 'db_owner', 'db_accessadmin', 
                               'db_securityadmin', 'db_ddladmin', 'db_backupoperator', 'db_datareader', 
                               'db_datawriter', 'db_denydatareader', 'db_denydatawriter')
            ORDER BY name", connection);

        using (var schemaReader = await schemaCmd.ExecuteReaderAsync())
        {
            while (await schemaReader.ReadAsync())
            {
                info.Schemas.Add(schemaReader.GetString(0));
            }
        }

        // Get table and view counts
        var countCmd = new SqlCommand(@"
            SELECT 
                SUM(CASE WHEN TABLE_TYPE = 'BASE TABLE' THEN 1 ELSE 0 END) as TableCount,
                SUM(CASE WHEN TABLE_TYPE = 'VIEW' THEN 1 ELSE 0 END) as ViewCount
            FROM INFORMATION_SCHEMA.TABLES", connection);

        using (var countReader = await countCmd.ExecuteReaderAsync())
        {
            if (await countReader.ReadAsync())
            {
                info.TableCount = countReader.GetInt32(0);
                info.ViewCount = countReader.GetInt32(1);
            }
        } // Close reader

        return info;
    }

    /// <summary>
    /// Lists all tables in the database
    /// </summary>
    /// <summary>
    /// Lists all tables in the database
    /// </summary>
    public async Task<List<DatabaseObject>> ListTablesAsync(string? schemaFilter = null, string? namePattern = null)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT 
                t.TABLE_SCHEMA,
                t.TABLE_NAME,
                ISNULL(p.rows, 0) as NrOfRows
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.tables st ON t.TABLE_NAME = st.name AND SCHEMA_NAME(st.schema_id) = t.TABLE_SCHEMA
            LEFT JOIN (
                SELECT object_id, SUM(rows) as rows
                FROM sys.partitions
                WHERE index_id IN (0,1)
                GROUP BY object_id
            ) p ON st.object_id = p.object_id
            WHERE t.TABLE_TYPE = 'BASE TABLE'";

        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            sql += " AND t.TABLE_SCHEMA = @Schema";
        }

        if (!string.IsNullOrWhiteSpace(namePattern))
        {
            sql += " AND t.TABLE_NAME LIKE @Pattern";
        }

        sql += " ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

        using var cmd = new SqlCommand(sql, connection);

        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            cmd.Parameters.AddWithValue("@Schema", schemaFilter);
        }

        if (!string.IsNullOrWhiteSpace(namePattern))
        {
            cmd.Parameters.AddWithValue("@Pattern", namePattern);
        }

        var tables = new List<DatabaseObject>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(new DatabaseObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                Type = "TABLE",
                RowCount = reader.IsDBNull(2) ? null : reader.GetInt64(2)
            });
        }

        return tables;
    }
    /// <summary>
    /// Lists all views in the database
    /// </summary>
    public async Task<List<DatabaseObject>> ListViewsAsync(string? schemaFilter = null, string? namePattern = null)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'VIEW'";

        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            sql += " AND TABLE_SCHEMA = @Schema";
        }

        if (!string.IsNullOrWhiteSpace(namePattern))
        {
            sql += " AND TABLE_NAME LIKE @Pattern";
        }

        sql += " ORDER BY TABLE_SCHEMA, TABLE_NAME";

        using var cmd = new SqlCommand(sql, connection);

        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            cmd.Parameters.AddWithValue("@Schema", schemaFilter);
        }

        if (!string.IsNullOrWhiteSpace(namePattern))
        {
            cmd.Parameters.AddWithValue("@Pattern", namePattern);
        }

        var views = new List<DatabaseObject>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            views.Add(new DatabaseObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                Type = "VIEW"
            });
        }

        return views;
    }

    /// <summary>
    /// Gets the count of records in a table
    /// </summary>
    public async Task<long> GetTableCountAsync(string schema, string tableName)
    {
        var tableIdentifier = QueryValidator.BuildTableIdentifier(schema, tableName);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $"SELECT COUNT(*) FROM {tableIdentifier}";
        using var cmd = new SqlCommand(sql, connection);
        cmd.CommandTimeout = _queryTimeoutSeconds;

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Gets detailed structure of a table or view
    /// </summary>
    public async Task<TableStructure> DescribeTableAsync(string schema, string objectName)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var structure = new TableStructure
        {
            Schema = schema,
            TableName = objectName
        };

        // Determine object type
        var typeCmd = new SqlCommand(@"
            SELECT TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Name", connection);
        typeCmd.Parameters.AddWithValue("@Schema", schema);
        typeCmd.Parameters.AddWithValue("@Name", objectName);

        var tableType = await typeCmd.ExecuteScalarAsync() as string;
        if (tableType == null)
        {
            throw new ArgumentException($"Object {schema}.{objectName} not found");
        }

        structure.ObjectType = tableType == "VIEW" ? "VIEW" : "TABLE";

        // Get columns
        structure.Columns = await GetColumnsAsync(connection, schema, objectName);

        // Get indexes (tables only)
        if (structure.ObjectType == "TABLE")
        {
            structure.Indexes = await GetIndexesAsync(connection, schema, objectName);
            structure.ForeignKeys = await GetForeignKeysAsync(connection, schema, objectName);
        }

        // Get view definition (views only)
        if (structure.ObjectType == "VIEW")
        {
            structure.ViewDefinition = await GetViewDefinitionAsync(connection, schema, objectName);
        }

        return structure;
    }

    private async Task<List<ColumnInfo>> GetColumnsAsync(SqlConnection connection, string schema, string tableName)
    {
        var sql = @"
            SELECT 
                c.COLUMN_NAME,
                c.ORDINAL_POSITION,
                c.DATA_TYPE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IsPrimaryKey,
                CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IsForeignKey,
                COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') as IsIdentity
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku 
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                AND c.TABLE_NAME = pk.TABLE_NAME 
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku 
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
            ) fk ON c.TABLE_SCHEMA = fk.TABLE_SCHEMA 
                AND c.TABLE_NAME = fk.TABLE_NAME 
                AND c.COLUMN_NAME = fk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @Schema AND c.TABLE_NAME = @Table
            ORDER BY c.ORDINAL_POSITION";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", tableName);

        var columns = new List<ColumnInfo>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                OrdinalPosition = reader.GetInt32(1),
                DataType = reader.GetString(2),
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Precision = reader.IsDBNull(4) ? null : (int?)reader.GetByte(4),
                Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                IsNullable = reader.GetString(6) == "YES",
                DefaultValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                IsPrimaryKey = reader.GetInt32(8) == 1,
                IsForeignKey = reader.GetInt32(9) == 1,
                IsIdentity = reader.GetInt32(10) == 1
            });
        }

        return columns;
    }

    private async Task<List<IndexInfo>> GetIndexesAsync(SqlConnection connection, string schema, string tableName)
    {
        var sql = @"
            SELECT 
                i.name as IndexName,
                i.type_desc as IndexType,
                i.is_unique as IsUnique,
                i.is_primary_key as IsPrimaryKey,
                STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) as Columns
            FROM sys.indexes i
            JOIN sys.tables t ON i.object_id = t.object_id
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE s.name = @Schema AND t.name = @Table
            GROUP BY i.name, i.type_desc, i.is_unique, i.is_primary_key
            ORDER BY i.is_primary_key DESC, i.name";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", tableName);

        var indexes = new List<IndexInfo>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            indexes.Add(new IndexInfo
            {
                IndexName = reader.GetString(0),
                IndexType = reader.GetString(1),
                IsUnique = reader.GetBoolean(2),
                IsPrimaryKey = reader.GetBoolean(3),
                Columns = reader.GetString(4).Split(", ").ToList()
            });
        }

        return indexes;
    }

    private async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(SqlConnection connection, string schema, string tableName)
    {
        var sql = @"
            SELECT 
                fk.name as ConstraintName,
                c1.name as ForeignKeyColumn,
                s2.name as ReferencedSchema,
                t2.name as ReferencedTable,
                c2.name as ReferencedColumn,
                fk.update_referential_action_desc as UpdateRule,
                fk.delete_referential_action_desc as DeleteRule
            FROM sys.foreign_keys fk
            JOIN sys.tables t1 ON fk.parent_object_id = t1.object_id
            JOIN sys.schemas s1 ON t1.schema_id = s1.schema_id
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            JOIN sys.columns c1 ON fkc.parent_object_id = c1.object_id AND fkc.parent_column_id = c1.column_id
            JOIN sys.tables t2 ON fk.referenced_object_id = t2.object_id
            JOIN sys.schemas s2 ON t2.schema_id = s2.schema_id
            JOIN sys.columns c2 ON fkc.referenced_object_id = c2.object_id AND fkc.referenced_column_id = c2.column_id
            WHERE s1.name = @Schema AND t1.name = @Table
            ORDER BY fk.name";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", tableName);

        var foreignKeys = new List<ForeignKeyInfo>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = reader.GetString(0),
                ForeignKeyColumn = reader.GetString(1),
                ReferencedSchema = reader.GetString(2),
                ReferencedTable = reader.GetString(3),
                ReferencedColumn = reader.GetString(4),
                UpdateRule = reader.GetString(5),
                DeleteRule = reader.GetString(6)
            });
        }

        return foreignKeys;
    }

    private async Task<string?> GetViewDefinitionAsync(SqlConnection connection, string schema, string viewName)
    {
        var sql = @"
            SELECT m.definition
            FROM sys.sql_modules m
            JOIN sys.views v ON m.object_id = v.object_id
            JOIN sys.schemas s ON v.schema_id = s.schema_id
            WHERE s.name = @Schema AND v.name = @View";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@View", viewName);

        return await cmd.ExecuteScalarAsync() as string;
    }

    /// <summary>
    /// Previews data from a table or view
    /// </summary>
    public async Task<QueryResult> PreviewDataAsync(string schema, string objectName, int topN = 10, string? orderBy = null)
    {
        if (topN > _maxQueryRows)
        {
            topN = _maxQueryRows;
        }

        var tableIdentifier = QueryValidator.BuildTableIdentifier(schema, objectName);
        var sql = $"SELECT TOP (@TopN) * FROM {tableIdentifier}";

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            // Sanitize order by columns
            var orderColumns = orderBy.Split(',')
                .Select(c => c.Trim())
                .Select(c => {
                    var parts = c.Split(' ');
                    var column = QueryValidator.SanitizeIdentifier(parts[0]);
                    var direction = parts.Length > 1 && parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                    return $"[{column}] {direction}";
                });
            sql += $" ORDER BY {string.Join(", ", orderColumns)}";
        }

        return await ExecuteQueryInternalAsync(sql, new Dictionary<string, object> { { "@TopN", topN } });
    }

    /// <summary>
    /// Executes a user-provided SELECT query with safety checks
    /// </summary>
    public async Task<QueryResult> ExecuteQueryAsync(string query, int maxRows)
    {
        // Validate query
        var (isValid, errorMessage) = QueryValidator.ValidateQuery(query);
        if (!isValid)
        {
            throw new InvalidOperationException($"Query validation failed: {errorMessage}");
        }

        if (maxRows > _maxQueryRows)
        {
            maxRows = _maxQueryRows;
        }

        // Wrap query with TOP clause if it doesn't have one
        var modifiedQuery = query.Trim();
        if (!Regex.IsMatch(modifiedQuery, @"^\s*SELECT\s+TOP\s+\d+", RegexOptions.IgnoreCase))
        {
            modifiedQuery = Regex.Replace(modifiedQuery, @"^\s*SELECT\s+", $"SELECT TOP {maxRows} ", RegexOptions.IgnoreCase);
        }

        return await ExecuteQueryInternalAsync(modifiedQuery, null);
    }

    private async Task<QueryResult> ExecuteQueryInternalAsync(string sql, Dictionary<string, object>? parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);

        try
        {
            using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.CommandTimeout = _queryTimeoutSeconds;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }
            }

            var stopwatch = Stopwatch.StartNew();
            using var reader = await cmd.ExecuteReaderAsync();

            var result = new QueryResult();

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.ColumnNames.Add(reader.GetName(i));
            }

            // Read rows
            while (await reader.ReadAsync() && result.RowCount < _maxQueryRows)
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Rows.Add(row);
                result.RowCount++;
            }

            result.WasTruncated = reader.HasRows;
            stopwatch.Stop();
            result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;

            // Always rollback (read-only)
            await transaction.RollbackAsync();

            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Searches for columns matching a pattern
    /// </summary>
    public async Task<List<(string Schema, string Table, string Column, string DataType)>> SearchColumnsAsync(string columnPattern)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE COLUMN_NAME LIKE @Pattern
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Pattern", columnPattern);

        var results = new List<(string, string, string, string)>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)
            ));
        }

        return results;
    }
}