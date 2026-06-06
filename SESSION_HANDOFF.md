# Session Handoff

Latest first. Read this and `TODO.md` at the start of each session to catch up.

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
