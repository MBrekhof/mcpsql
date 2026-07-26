# Session Handoff

Latest first. Read this and `TODO.md` at the start of each session to catch up.

## 2026-07-26 — Backlog sweep: tests, three bug fixes, CI

**What was done:** worked the board from P1 down. Five PRs, all merged to `master` (now `0b6def5`).
Test count went 38 → 59; CI now runs on every push and PR.

- **PR #5 — TODO.md format.** Migrated to the id-heading convention (`TST-001`, …) with P-sections,
  each item citing its ContextBoard card id; completed work moved to a new `DOCS/DONE.md`.
- **PR #6 — TST-001, coverage for the result path.** 17 tests over `FormatQueryResult` and the row
  limit rewrite. Needed two seams: `SqlServerTools.FormatQueryResult` is now `internal static` taking
  `maxCellWidth`, and the TOP rewrite moved to `DatabaseService.ApplyRowLimit`, with
  `InternalsVisibleTo`. Fixed two defects it exposed (below).
- **PR #7 — ROW-001.** `max_rows` is now enforced while reading rows, not only via the TOP rewrite.
- **PR #8 — CFG-001 + CI-001.** `.editorconfig` and `.github/workflows/build.yml`.
- **PR #9 — CFG-002.** Per-connection query timeouts.

**Bugs found and fixed** (all reproduced against the live Docker SQL Server, not just unit tests):

1. **`SELECT DISTINCT` was completely broken.** TOP was injected *before* DISTINCT, but T-SQL grammar
   is `SELECT [DISTINCT] [TOP n]` — every distinct query failed with `Incorrect syntax near the
   keyword 'DISTINCT'`. The same regex double-injected on `SELECT DISTINCT TOP 5 …`.
2. **`MaxCellWidth` below 4 crashed the tool call** — `Substring(0, maxCellWidth - 3)` goes negative.
   Now clamped in the formatter.
3. **`max_rows` was ignored for CTE queries** — a `WITH` query asking for 2 rows returned **132**.
   The read loop now caps at `min(maxRows, MaxQueryRows)`. Reordering that loop condition also fixed
   an off-by-one that consumed the row past the cap and so suppressed the "Results truncated" notice.
4. **`QueryTimeoutSeconds` was applied at only 2 of 13 command sites** — every introspection tool ran
   at ADO.NET's 30s default regardless of config. Fixed by baking the timeout into the connection
   string, which is also how CFG-002 works.

**Design note worth keeping:** CFG-002 adds no config schema. SQL Server's own `Command Timeout=N`
connection-string keyword *is* the per-connection override; `QueryTimeoutSeconds` stays the default.
Detection uses `ShouldSerialize("Command Timeout")` — `ContainsKey` is true for every *known* keyword
on a typed `SqlConnectionStringBuilder`, which a test caught, and a value check would stomp a
deliberate `30`.

**State:** working tree clean, Release build 0 warnings, 59/59 tests pass, CI green. The Docker SQL
container was started for live tests and **stopped again**.

**Blocked / next:** `SEC-001` (column allow/deny) is the only open card and is **paused pending a
design decision** — see its body in `TODO.md`. The short version: name-based filtering can't be
enforced on `execute_query` (aliases, expressions, `WHERE`-clause probing all defeat it), so the
question is whether this should be SQL Server `DENY` permissions (a real boundary, no app code) or an
explicitly-labelled convenience filter. Don't build it before that's answered.

Cards 524, 525, 526, 528, 726 and 1093 are in **Review** on the board awaiting Confirm-Done.

## 2026-07-09 — Live-DB verification of MaxCellWidth

**What was done:** verified the `MaxCellWidth` change end to end against a live SQL Server
(local Docker container `xafrolechooser-sqlserver`, `localhost,1433`, catalog `master`), driving
the server over stdio JSON-RPC (`initialize` + `tools/call` → `execute_query`):
- No `MaxCellWidth` key in config → default 1000: `REPLICATE('x',200)` came back untruncated (200 chars).
- `MaxCellWidth: 60` → cell truncated to exactly 60 chars ending in `...`.

The "not verified against a live DB" caveat from 2026-06-15 is closed. A working local
`mcpsql/appsettings.json` (gitignored) now points at the Docker instance for future testing.

**State:** build clean, no code changes this session — docs only.

## 2026-06-15 — Configurable display cell width

**What changed:**
- Replaced the hardcoded 50-char cell truncation in the query/preview table formatter with a configurable `McpServer:MaxCellWidth` setting (default **1000**). `SqlServerTools` now takes `IConfiguration`; key added to `appsettings.example.json`.
- Shipped via PR #1, merged to `master` (fast-forward, `cf45b23`).

**Why:** the 50-char cap was lossy to the MCP client (the LLM never saw full text-field values), so it wrote convoluted SUBSTRING-chunking queries to read long values. Bigger default kills that workaround.

**State:** build clean. Not verified against a live DB (none on hand) — worth a quick check against wlncentral.

## 2026-06-06 — Repo setup: tests, docs, licensing

**What changed:**
- Added `CLAUDE.md` (contributor guidance) and a `/add-mcp-tool` skill under `.claude/skills/`.
- Added xUnit test project `mcpsql.Tests` referencing `mcpsql`, with `QueryValidatorTests.cs` — **38 tests, all passing** (`dotnet test`). These are characterization tests locking in the read-only block-list behavior.
- Added `README.md`, `LICENSE` (MIT, Martin Brekhof), and `TODO.md`.
- Made the GitHub repo public.

**State:**
- Build clean, all tests green.
- No verification against a live SQL Server was performed this session (no DB on hand) — the validator tests are pure unit tests and don't need one.

**Next steps:** see `TODO.md` (broaden test coverage, add `.editorconfig`, consider CI).

**Gotchas to remember:**
- Never write to stdout/stderr in server code — it corrupts the stdio JSON-RPC stream. Log via `FileLogger` to `./logs/`.
- Keep `QueryValidator` strict; don't loosen it without explicit intent (the tests will catch regressions).
- `appsettings.json` is gitignored; only `appsettings.example.json` is committed.
