# Save system

## Pieces

| Piece | File | Responsibility |
|---|---|---|
| `SaveService` (autoload) | `autoload/save_service.gd` | Envelope, schema version, validation, migrations, slot-id safety, signals |
| `SaveBackend` (interface) | `core/save/save_backend.gd` | Move opaque text to/from storage: `exists`, `read_text` (+`last_error`), `write_text`, `delete`, `list_slots` |
| `JsonFileSaveBackend` | `core/save/json_file_save_backend.gd` | `user://saves/<slot>.json`, atomic writes, `.bak` of previous save |
| `MemorySaveBackend` | `core/save/memory_save_backend.gd` | Tests; `fail_writes` simulates disk errors |
| `SaveResult` | `core/save/save_result.gd` | `status`, `data`, developer `detail`, `player_message_key()` |
| `SaveMigrations` | `core/save/save_migrations.gd` | Step-wise upgrades `vN → vN+1` |
| `GameSession` (autoload) | `autoload/game_session.gd` | Builds/applies the game payload (`to_save_data` / `load_save_data`) |

`SaveService` knows nothing about game rules; `GameSession` knows nothing
about files.

## API

```gdscript
var result: SaveResult = SaveService.save_game(GameSession.to_save_data())   # default slot "campaign_1"
var loaded: SaveResult = SaveService.load_game()
if loaded.is_ok() and GameSession.load_save_data(loaded.data): ...
else: show(loaded.player_message_key())   # translated, player-readable
SaveService.has_save(); SaveService.list_saves(); SaveService.delete_save()
```

Slot ids must match `^[a-z0-9_]{1,32}$` (prevents path traversal).

## File format (schema 1)

```json
{
  "schema_version": 1,
  "game_version": "0.1.0",
  "saved_at_unix": 1790000000,
  "data": {
    "wallet": {"wood": 10, "stone": 0, "food": 0, "gold": 50, "soul_energy": 0},
    "loadout": {"hero_id": "hero_deserter", "battalion_id": "battalion_militia"},
    "active_mission_id": "mission_first_spark",
    "completed_missions": ["mission_first_spark"],
    "soul_energy_spent": 0
  }
}
```

Only plain JSON types and **stable ids** are stored — never translated
text, node paths, or resource paths. Unknown hero/battalion ids make
`GameSession.load_save_data` return `false` without changing the session;
the menu then reports the save as damaged.

## When the game saves

- Autosave after a **won** mission (result screen shows *Saving…* →
  *Progress saved.*, or the write error with **Try Saving Again**; the
  player can always continue).
- Defeat and abandoning do not save, so the last good save stays intact.
- Mid-mission saving is intentionally out of scope (decide later; it needs
  grid/unit/deck state in the payload).

Settings are **not** in save games; they live in `user://settings.cfg`.

## Failure handling

| Status | Cause | Player sees (key) |
|---|---|---|
| `NOT_FOUND` | no file | `SAVE_ERROR_NOT_FOUND` |
| `CORRUPTED` | bad JSON, missing envelope fields, no migration path | `SAVE_ERROR_CORRUPTED` |
| `VERSION_TOO_NEW` | schema newer than the build | `SAVE_ERROR_VERSION_TOO_NEW` |
| `READ_FAILED` / `WRITE_FAILED` | I/O error (permissions, disk full) | `SAVE_ERROR_READ_FAILED` / `SAVE_ERROR_WRITE_FAILED` (previous save is safe) |
| `INVALID_SLOT` | bad slot id | `SAVE_ERROR_INVALID_SLOT` |

Technical details (`SaveResult.detail`) go to `DevLog` only.

## Changing the save format

1. Bump `SaveService.SCHEMA_VERSION`.
2. Add `_migrate_<old>_to_<new>(data: Dictionary) -> Dictionary` to
   `SaveMigrations` and register it in `STEPS`.
3. Add a test that loads a literal old-schema JSON and checks the result.
4. Update the format section above.

Never edit a released migration; never rename a stored id without one.

## Troubleshooting

- Save location: `user://saves/` → Linux `~/.local/share/godot/app_userdata/Utopia/saves/`,
  Windows `%APPDATA%\Godot\app_userdata\Utopia\saves\`.
- A `.bak` next to a slot is the previous successful save; manual recovery =
  rename it to `.json`. (Automatic restore is not implemented.)
- Tests never touch real saves: the runner installs a `MemorySaveBackend`.

## Known limitations

Synchronous writes, single default slot in the UI, no cloud/platform backend,
no automatic `.bak` restore, no encryption or tamper protection.
