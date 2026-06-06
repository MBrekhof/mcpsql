# mcpsql

A read-only [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server for **Microsoft SQL Server**. It lets MCP clients (Claude Desktop, Claude Code, etc.) explore database schemas and run safe `SELECT` queries against one or more SQL Server instances over JSON-RPC 2.0 (stdio).

## Features

- **Schema introspection** — list/describe tables and views, columns, indexes, foreign keys, row counts, and view definitions.
- **Read-only queries** — execute custom `SELECT`/`WITH` queries through a strict validator that blocks any write or DDL operation.
- **Multi-database** — configure several named connections and switch the active one at runtime with `use_database`.
- **Safe by design** — queries run under `READ UNCOMMITTED` with automatic rollback, are row-capped, and identifiers are bracket-sanitized.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A reachable Microsoft SQL Server instance

## Setup

1. Copy the config template and fill in your connections:

   ```sh
   cp mcpsql/appsettings.example.json mcpsql/appsettings.json
   ```

   `appsettings.json` is gitignored — it never gets committed.

2. Edit `appsettings.json`:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=MyDb;Integrated Security=true;TrustServerCertificate=true",
       "PrdConnection": "Server=prod;Database=MyDb;User Id=reader;Password=...;TrustServerCertificate=true"
     },
     "McpServer": {
       "DefaultDatabase": "DefaultConnection",
       "MaxQueryRows": 1000,
       "DefaultPreviewRows": 10,
       "QueryTimeoutSeconds": 30,
       "EnableQueryLogging": true
     }
   }
   ```

3. Build:

   ```sh
   dotnet build
   ```

## Running

The server speaks JSON-RPC over **stdio**, so it's normally launched by an MCP client rather than run by hand. To register it (e.g. in Claude Desktop's `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "mcpsql": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/Projects/mcpsql/mcpsql"]
    }
  }
}
```

Or publish a self-contained executable and point `command` at it:

```sh
dotnet publish mcpsql -c Release
```

## Tools

| Tool | Description |
| --- | --- |
| `list_databases` | List configured connections and the active one |
| `use_database` | Switch the active database |
| `get_database_info` | Server version, schemas, table/view counts |
| `list_tables` | List tables (optional schema/name filters) |
| `list_views` | List views (optional schema/name filters) |
| `get_table_count` | Row count for a table |
| `describe_table` | Columns, indexes, FKs, and view definition |
| `preview_data` | Sample rows from a table or view |
| `execute_query` | Run a validated `SELECT`/`WITH` query |
| `search_columns` | Find columns by name pattern |
| `get_foreign_keys` | Foreign-key relationships for a table |

It also exposes MCP resources: `database://info` and `database://schema/{schema}/{object_name}`.

## Development

- Build: `dotnet build`
- Test: `dotnet test`

Tests cover `QueryValidator`, the read-only security boundary. **Important:** the server must never write to stdout/stderr — all logging goes to `./logs/` via the file logger, because any console output corrupts the JSON-RPC stream. See [CLAUDE.md](CLAUDE.md) for the full set of contributor gotchas.

## License

[MIT](LICENSE)
