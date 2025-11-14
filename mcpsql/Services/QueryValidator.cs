using System.Text.RegularExpressions;

namespace SqlServerMcpServer.Services;

/// <summary>
/// Validates SQL queries to ensure they are read-only and safe
/// </summary>
public static class QueryValidator
{
    // Blocked keywords that indicate write operations
    private static readonly string[] BlockedKeywords = new[]
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
        "TRUNCATE", "EXEC", "EXECUTE", "MERGE", "BULK",
        "sp_executesql", "xp_", "sp_", "GRANT", "DENY", "REVOKE"
    };

    // Regex patterns for dangerous operations
    private static readonly Regex[] DangerousPatterns = new[]
    {
        new Regex(@"\bINTO\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bFROM\s+OPENROWSET\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bFROM\s+OPENDATASOURCE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"--", RegexOptions.Compiled), // SQL comments (potential injection)
        new Regex(@"/\*", RegexOptions.Compiled), // Block comments
    };

    /// <summary>
    /// Validates if a query is safe to execute (read-only)
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return (false, "Query cannot be empty");
        }

        // Remove string literals to avoid false positives
        var queryWithoutStrings = RemoveStringLiterals(query);

        // Check for blocked keywords
        foreach (var keyword in BlockedKeywords)
        {
            if (Regex.IsMatch(queryWithoutStrings, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
            {
                return (false, $"Query contains blocked keyword: {keyword}");
            }
        }

        // Check for dangerous patterns
        foreach (var pattern in DangerousPatterns)
        {
            if (pattern.IsMatch(queryWithoutStrings))
            {
                return (false, "Query contains potentially dangerous pattern");
            }
        }

        // Must start with SELECT, WITH (CTE), or whitespace followed by SELECT/WITH
        var trimmedQuery = query.TrimStart();
        if (!trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmedQuery.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Query must start with SELECT or WITH (Common Table Expression)");
        }

        // Check for semicolons (multiple statements)
        var semicolonCount = queryWithoutStrings.Count(c => c == ';');
        if (semicolonCount > 1 || (semicolonCount == 1 && !queryWithoutStrings.TrimEnd().EndsWith(";")))
        {
            return (false, "Multiple SQL statements are not allowed");
        }

        return (true, null);
    }

    /// <summary>
    /// Removes string literals from query to avoid false positive matches
    /// </summary>
    private static string RemoveStringLiterals(string query)
    {
        // Remove single-quoted strings
        var result = Regex.Replace(query, @"'([^']|'')*'", "''");

        // Remove double-quoted identifiers (SQL Server can use these)
        result = Regex.Replace(result, @"""[^""]*""", "\"\"");

        return result;
    }

    /// <summary>
    /// Sanitizes table/schema names to prevent SQL injection
    /// </summary>
    public static string SanitizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be empty", nameof(identifier));
        }

        // Remove any characters that aren't alphanumeric, underscore, or period
        var sanitized = Regex.Replace(identifier, @"[^\w\.]", "");

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException("Identifier contains no valid characters", nameof(identifier));
        }

        return sanitized;
    }

    /// <summary>
    /// Builds a safe table identifier with schema
    /// </summary>
    public static string BuildTableIdentifier(string schema, string tableName)
    {
        var safeSchema = SanitizeIdentifier(schema);
        var safeTable = SanitizeIdentifier(tableName);
        return $"[{safeSchema}].[{safeTable}]";
    }
}