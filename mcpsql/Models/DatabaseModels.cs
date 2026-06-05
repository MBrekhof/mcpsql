namespace SqlServerMcpServer.Models;

/// <summary>
/// Represents a database table or view
/// </summary>
public class DatabaseObject
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // TABLE or VIEW
    public long? RowCount { get; set; }
}

/// <summary>
/// Represents a table/view column
/// </summary>
public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public int OrdinalPosition { get; set; }
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsIdentity { get; set; }
}

/// <summary>
/// Represents table structure details
/// </summary>
public class TableStructure
{
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<IndexInfo> Indexes { get; set; } = new();
    public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
    public string? ViewDefinition { get; set; }
}

/// <summary>
/// Represents an index
/// </summary>
public class IndexInfo
{
    public string IndexName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public List<string> Columns { get; set; } = new();
}

/// <summary>
/// Represents a foreign key relationship
/// </summary>
public class ForeignKeyInfo
{
    public string ConstraintName { get; set; } = string.Empty;
    public string ForeignKeyColumn { get; set; } = string.Empty;
    public string ReferencedSchema { get; set; } = string.Empty;
    public string ReferencedTable { get; set; } = string.Empty;
    public string ReferencedColumn { get; set; } = string.Empty;
    public string UpdateRule { get; set; } = string.Empty;
    public string DeleteRule { get; set; } = string.Empty;
}

/// <summary>
/// Represents database information
/// </summary>
public class DatabaseInfo
{
    public string DatabaseName { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = string.Empty;
    public string CompatibilityLevel { get; set; } = string.Empty;
    public List<string> Schemas { get; set; } = new();
    public int TableCount { get; set; }
    public int ViewCount { get; set; }
}

/// <summary>
/// Represents a configured, switchable database connection
/// </summary>
public class DatabaseConnectionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Human-friendly label, e.g. "Labware8 (Labware8 on tst-sql-005)"</summary>
    public string Display => $"{Name} ({Database} on {Server})";
}

/// <summary>
/// Query execution result
/// </summary>
public class QueryResult
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowCount { get; set; }
    public List<string> ColumnNames { get; set; } = new();
    public bool WasTruncated { get; set; }
    public int ExecutionTimeMs { get; set; }
}