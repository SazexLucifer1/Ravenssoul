# networking/

Reserved. The game is single-player and has no networking code. If online
features (cloud saves, telemetry) are added, they live here behind an
interface, exchange only stable language-independent ids, and get an ADR in
`docs/decisions/`. `SaveBackend` is the extension point for cloud saves.
