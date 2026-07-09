# Session Handoff

Latest first. Read this and `TODO.md` at the start of each session to catch up.

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
