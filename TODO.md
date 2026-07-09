# TODO

Working backlog for mcpsql. Newest priorities at the top.

## Now

- [ ] Nothing in progress.

## Next

- [ ] Expand test coverage beyond `QueryValidator` (e.g. result formatting / row-cap logic in `SqlServerTools`).
- [ ] Add an `.editorconfig` so `dotnet format` and the SDK analyzers give consistent style/nullability feedback.
- [ ] Consider a CI workflow (GitHub Actions) running `dotnet build` + `dotnet test` on push/PR.

## Ideas / later

- [ ] Optional column-level allow/deny lists per connection.
- [ ] Configurable per-connection query timeout overrides.

## Done

- [x] Make display cell width configurable (`McpServer:MaxCellWidth`, default 1000) — PR #1.
- [x] Add xUnit test project (`mcpsql.Tests`) covering `QueryValidator` — 38 tests.
- [x] Add README, LICENSE (MIT), and contributor guidance (CLAUDE.md).
