# TODO

Open work for mcpsql. Completed items move to `DOCS/DONE.md`.

## P3: Low

#### SEC-001: Column-level allow/deny lists per connection (ID: 527)

Optional per-connection column filtering, so a named connection can hide sensitive columns from
introspection and query results.

#### CFG-002: Per-connection query timeout overrides (ID: 528)

`McpServer:QueryTimeoutSeconds` is global today; allow a per-connection override for slow instances.
