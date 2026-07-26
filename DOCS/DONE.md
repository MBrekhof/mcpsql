# Done

Completed work, newest first. Items move here from `TODO.md` with a completion date.

#### TST-001: Cover SqlServerTools formatting and row-cap logic (ID: 524) — 2026-07-26

17 new tests (55 total, all green). Added two seams so the pure logic is reachable from
`mcpsql.Tests` without a database: `SqlServerTools.FormatQueryResult` became `internal static` taking
`maxCellWidth`, and the TOP-injection rewrite was extracted to `DatabaseService.ApplyRowLimit`
(`InternalsVisibleTo` on the main project).

Three real defects surfaced, two fixed here:

- **Fixed — `SELECT DISTINCT` was completely broken.** TOP was injected *before* DISTINCT, but T-SQL
  grammar is `SELECT [DISTINCT] [TOP n]`. Every distinct query failed with `Incorrect syntax near the
  keyword 'DISTINCT'` (reproduced live). The same regex also double-injected on
  `SELECT DISTINCT TOP 5 …`. Both fixed by making DISTINCT part of the match.
- **Fixed — `MaxCellWidth` below 4 crashed the tool call.** `Substring(0, maxCellWidth - 3)` goes
  negative; the value is operator-supplied and unvalidated. Now clamped in the formatter, where every
  caller routes through.
- **Carded — `max_rows` ignored for CTE queries.** See ROW-001 (ID 1093) in `TODO.md`.

Verified live against the local Docker SQL Server: DISTINCT returns rows, `DISTINCT TOP 2` keeps its
own limit, plain queries and `preview_data` unchanged.

#### FMT-001: Escape hatch for long-text columns (ID: 726) — 2026-07-26

Closed as **stale, not implemented** — the premise didn't hold. Raised 2026-07-15 after long-text
columns came back truncated at ~50 chars, but the codebase has exactly one cell-truncation site
(`SqlServerTools.cs:728`) and it uses `McpServer:MaxCellWidth`, default **1000**
(`SqlServerTools.cs:21`, shipped in PR #1 on 2026-06-15). No ~50-char path exists; there is also no
published build of mcpsql on this machine and no MCP registration pointing at it, only `bin/Debug`
binaries. So that session ran a stale Debug exe predating PR #1, or a different tool entirely.

The one idea `MaxCellWidth` does *not* cover is "write result to file" for oversized payloads — a
response-size concern, not a cell-width one. Worth a fresh card if it is ever actually hit.

#### FMT-000: Make display cell width configurable — 2026-06-15

`McpServer:MaxCellWidth`, default 1000, replacing the hardcoded 50-char cap. PR #1. Verified against a
live SQL Server on 2026-07-09.

#### TST-000: Add xUnit test project covering QueryValidator — 2026-06-06

`mcpsql.Tests`, 38 tests characterizing the read-only block-list.

#### DOC-000: Add README, LICENSE (MIT), and CLAUDE.md — 2026-06-06
