# TODO

Open work for mcpsql. Completed items move to `DOCS/DONE.md`.

## P2: Medium

#### ROW-001: max_rows is ignored for CTE (WITH) queries (ID: 1093)

`DatabaseService.ApplyRowLimit` only injects TOP into a query starting with SELECT, but
`QueryValidator` also allows WITH — so a CTE query gets no TOP and the caller's `max_rows` is silently
dropped. Confirmed live 2026-07-26: a CTE with `max_rows: 2` returned **132 rows**. Bounded by the
reader loop's `MaxQueryRows` cap (default 1000), so it over-fetches rather than running away.

Locked in by `RowLimitTests.ApplyRowLimit_DoesNotLimitCteQueries`, which currently asserts the broken
behaviour — flip that test when fixing.

- a) Preferred fix: cap the reader loop in `ExecuteQueryInternalAsync` at the caller's `maxRows`
  instead of only `_maxQueryRows`. Fixes every query shape without parsing SQL.
- b) Alternative: locate the final SELECT with ScriptDom. Correct, much heavier; regex can't do it
  (nested CTEs, UNION, subqueries).

#### CFG-001: Add .editorconfig (ID: 525)

So `dotnet format` and the SDK analyzers give consistent style/nullability feedback instead of
per-file drift.

#### CI-001: Add GitHub Actions build + test workflow (ID: 526)

`dotnet build` + `dotnet test` on push/PR to `master`. No live DB in CI — `QueryValidator` and
formatting tests are pure unit tests.

## P3: Low

#### SEC-001: Column-level allow/deny lists per connection (ID: 527)

Optional per-connection column filtering, so a named connection can hide sensitive columns from
introspection and query results.

#### CFG-002: Per-connection query timeout overrides (ID: 528)

`McpServer:QueryTimeoutSeconds` is global today; allow a per-connection override for slow instances.
