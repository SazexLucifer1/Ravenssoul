# networking/

Reserved. The game is single-player and has no networking code. If online
features (cloud saves, telemetry) are added, they live here behind an
interface, exchange only stable language-independent ids, and get an ADR in
`docs/decisions/`. `SaveBackend` is the extension point for cloud saves.

The test-automation transport (local HTTP/WebSocket for CI and agents) is not
gameplay networking and lives in `automation/` (see `docs/AUTOMATION.md`).
