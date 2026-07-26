# TODO

Open work for mcpsql. Completed items move to `DOCS/DONE.md`.

## P1: High

#### TST-001: Cover SqlServerTools formatting and row-cap logic (ID: 524)

Tests currently stop at `QueryValidator`. Extend `mcpsql.Tests` to the result-formatting path in
`Services/SqlServerTools.cs` — cell truncation at `MaxCellWidth`, the `MaxQueryRows` row cap, and the
`WasTruncated` notice (`SqlServerTools.cs:604`). Live smoke testing works now (see `CLAUDE.md`), so
behaviour can be pinned against the local Docker SQL Server where a unit test is awkward.

## P2: Medium

#### CFG-001: Add .editorconfig (ID: 525)

So `dotnet format` and the SDK analyzers give consistent style/nullability feedback instead of
per-file drift.

#### CI-001: Add GitHub Actions build + test workflow (ID: 526)

`dotnet build` + `dotnet test` on push/PR to `master`. No live DB in CI — `QueryValidator` and
formatting tests are pure unit tests.

## P3: Low

#### FMT-001: Escape hatch for long-text columns (ID: 726)

Raised 2026-07-15 from the LimsBasic side: long-text columns (`SUBROUTINE.SOURCE_CODE`,
`LIMS_LOG.MESSAGE`) came back truncated at ~50 chars, forcing `SUBSTRING` chunking. Suggested shapes:
a `max_col_width` parameter on the query tools, and/or "write result to file" for payloads too big to
inline.

- a) Confirm the premise first — `McpServer:MaxCellWidth` already defaults to **1000**
  (`SqlServerTools.cs:21`, shipped 2026-06-15). A 50-char cap in July means the MCP client was running
  a stale published binary, not that the code truncates at 50. Re-check before building anything.
- b) Out of scope here: routine-specific tools (`get_routine_source` etc.) — those live in
  `LimsBasic.Mcp`.

Related: [SEC-001] touches the same result-shaping path.

#### SEC-001: Column-level allow/deny lists per connection (ID: 527)

Optional per-connection column filtering, so a named connection can hide sensitive columns from
introspection and query results.

#### CFG-002: Per-connection query timeout overrides (ID: 528)

`McpServer:QueryTimeoutSeconds` is global today; allow a per-connection override for slow instances.
