# TODO

Open work for mcpsql. Completed items move to `DOCS/DONE.md`.

## P3: Low

#### SEC-001: Column-level allow/deny lists per connection (ID: 527)

Optional per-connection column filtering, so a named connection can hide sensitive columns from
introspection and query results.

**Paused 2026-07-26 pending a design decision — do not implement before it's answered.** The
deciding question is *who are the columns being hidden from?*

Name-based filtering is easy on the paths mcpsql builds itself (`describe_table`, `search_columns`,
`preview_data`) and unenforceable on `execute_query`, where arbitrary SELECT walks straight past it:

- `SELECT SSN AS x FROM Customers` — alias defeats name matching.
- `SELECT SUBSTRING(SSN,1,4) FROM Customers` — computed column, no name to match.
- `SELECT Id FROM Customers WHERE SSN LIKE '1%'` — extracts values without selecting the column.

Options, in order of preference:

- a) **Use SQL Server's own permissions** and close this card. `DENY SELECT ON dbo.Customers(SSN) TO
  mcp_reader`, then point the connection at that login. No app code, no config, and it's a real
  boundary — the server rejects the query before mcpsql sees it. README gets a short section.
- b) **App-level convenience filter.** Per-connection `DeniedColumns`, hidden from introspection and
  preview, matching result columns stripped. Must be documented as *not* a security boundary, or it
  gives false assurance.
- c) **Parse with ScriptDom** and reject queries referencing denied columns. Real enforcement, new
  dependency, heavy — and still leaks through a view that aliases the column.

