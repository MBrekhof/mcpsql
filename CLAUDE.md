# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Start of session

At the start of each session, read `SESSION_HANDOFF.md` and `TODO.md` to catch up on where work left off and what's queued. When you finish meaningful work, update both (add a new dated entry at the top of `SESSION_HANDOFF.md`; check off / re-prioritize `TODO.md`).

## What this is

A Model Context Protocol (MCP) server for SQL Server. It speaks JSON-RPC 2.0 over **stdio** and exposes read-only schema-introspection and query tools (list/describe tables & views, preview data, execute SELECT, search columns, foreign keys) plus `database://` resources. Supports multiple named connections with runtime switching via the `use_database` tool.

- `Program.cs` — startup, DI, logging setup
- `McpServer.cs` — JSON-RPC handler (`initialize`, `tools/list`, `tools/call`, `resources/*`)
- `Services/` — `DatabaseService` (connectivity + introspection), `SqlServerTools` (tool defs + execution), `QueryValidator` (read-only enforcement), `FileLogger`
- `Protocol/` — JSON-RPC and MCP message types
- `Models/` — domain models

## Build & run

- Build: `dotnet build`
- Run: `dotnet run` (starts the server on stdio — it waits for JSON-RPC input, so it won't "do" anything when run bare)
- .NET 8.0, win-x64, published as self-contained single-file with ReadyToRun.

## Critical gotchas

- **Never write to stdout/stderr.** Any `Console.Write*` corrupts the JSON-RPC stream over stdio and breaks the client. All logging goes through `FileLoggerProvider` to `./logs/mcp-server-YYYYMMDD.log` — console logging is intentionally not registered. Check those log files when debugging.
- **Queries are read-only by design.** `QueryValidator` blocks INSERT/UPDATE/DELETE/EXEC/DDL, comments, and patterns like OPENROWSET; only SELECT/WITH is allowed. Keep this guarantee intact when touching query paths — don't loosen validation without explicit instruction.
- **Active database is per-server state.** `use_database` switches the connection all subsequent tool calls use. Results are prefixed with the active DB name.
- Queries run under `ReadUncommitted` with automatic rollback; results are row-capped (`MaxQueryRows`) and cells truncated for display.

## Configuration

Copy `appsettings.example.json` to `appsettings.json` (gitignored). Shape:
- `ConnectionStrings` — dictionary of named connections (≥1 required).
- `McpServer` — `DefaultDatabase`, `MaxQueryRows`, `DefaultPreviewRows`, `QueryTimeoutSeconds`, `EnableQueryLogging`.

## Code style

- File-scoped namespaces; nullable reference types enabled (init string props to `string.Empty`, collections to `new()`).
- PascalCase members; `[JsonPropertyName]` for camelCase wire names.
- DI via `ServiceCollection`; async/await for all I/O.

## Verifying changes

- Run `dotnet test` — `mcpsql.Tests` covers `QueryValidator`, the read-only security boundary. Add/extend tests when you touch validation or formatting logic.
- Build-clean and green tests are **not** proof the server works end to end. After changes that affect runtime behavior, also run the server against a live SQL Server instance and exercise the affected tool(s) before claiming success.

## Git

Work on feature branches and open a PR to `master`. Do not commit directly to `master`.
