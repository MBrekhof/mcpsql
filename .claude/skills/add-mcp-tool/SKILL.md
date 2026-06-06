---
name: add-mcp-tool
description: Add a new tool to this SQL Server MCP server following the repo's wiring. Use when asked to add, expose, or implement a new MCP tool / database capability in mcpsql.
---

# Adding an MCP tool

Follow this codebase's existing pattern exactly — mirror a similar existing tool (e.g. `describe_table`, `preview_data`) rather than inventing structure.

## Steps

1. **Define the tool** in `mcpsql/Services/SqlServerTools.cs`:
   - Add an `McpTool` entry to the tool list returned for `tools/list`, with `name`, `description`, and a JSON-schema `inputSchema` (match the casing/shape of existing entries).
   - Add the execution branch that handles the call, validates/extracts arguments, and returns a `ToolCallResult`. Keep result formatting consistent with neighbors (column truncation, active-DB prefix).

2. **Route it** in `mcpsql/McpServer.cs` if dispatch isn't already generic — confirm `tools/call` reaches the new branch.

3. **Database access** goes through `DatabaseService` (use the active connection; don't open ad-hoc connections). Sanitize identifiers with brackets `[schema].[object]` as existing code does.

4. **Enforce read-only.** If the tool runs user-supplied SQL, pass it through `QueryValidator` — never bypass it. SELECT/WITH only.

5. **Logging:** use the injected `ILogger` / `FileLogger` only. **Never** `Console.Write*` — it corrupts the stdio JSON-RPC stream.

6. **Verify.** `dotnet build`, then run the server against a live SQL Server and actually invoke the new tool. Build-clean is not proof it works. Check `./logs/` on failure.

## Guardrails

- Don't loosen `QueryValidator` to make a tool work.
- Don't add console/stdout output anywhere.
- Keep nullable-clean: init string props to `string.Empty`, collections to `new()`.
