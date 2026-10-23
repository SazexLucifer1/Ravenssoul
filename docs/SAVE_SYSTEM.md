# Save system

## Pieces

| Piece | File | Responsibility |
|---|---|---|
| `SaveService` (autoload) | `autoload/SaveService.cs` | Envelope, schema version, validation, migrations, slot-id safety, signals |
| `SaveBackend` (interface) | `core/save/SaveBackend.cs` | Move opaque text to/from storage: `Exists`, `ReadText` (+`LastError`), `WriteText`, `Delete`, `ListSlots` |
| `JsonFileSaveBackend` | `core/save/JsonFileSaveBackend.cs` | `user://saves/<slot>.json`, atomic writes, `.bak` of previous save |
| `MemorySaveBackend` | `core/save/MemorySaveBackend.cs` | Tests; `FailWrites` simulates disk errors |
| `SaveResult` | `core/save/SaveResult.cs` | `State`, `Data` (a `JsonObject`), developer `Detail`, `PlayerMessageKey()` |
| `SaveMigrations` | `core/save/SaveMigrations.cs` | Step-wise upgrades `vN → vN+1` |
| `GameSession` (autoload) | `autoload/GameSession.cs` | Builds/applies the game payload (`ToSaveData` / `LoadSaveData`) |

`SaveService` knows nothing about game rules; `GameSession` knows nothing
about files.

## API

```csharp
SaveService saves = SaveService.Instance;
SaveResult result = saves.SaveGame(GameSession.Instance.ToSaveData());  // default slot "campaign_1"
SaveResult loaded = saves.LoadGame();
if (loaded.IsOk() && GameSession.Instance.LoadSaveData(loaded.Data)) { /* ... */ }
else ShowMessage(loaded.PlayerMessageKey());  // translated, player-readable
saves.HasSave(); saves.ListSaves(); saves.DeleteSave();
```

Payloads are `System.Text.Json.Nodes.JsonObject`s; read them with the
forgiving helpers in `core/serialization/JsonNodes.cs` (`JsonNodes.Int(node,
fallback)`, `Str`, `Bool`), which accept `50` and `50.0` alike and never
throw on missing or mistyped fields.

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
`GameSession.LoadSaveData` return `false` without changing the session;
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

| Status (`SaveResult.Status`) | Cause | Player sees (key) |
|---|---|---|
| `NotFound` | no file | `SAVE_ERROR_NOT_FOUND` |
| `Corrupted` | bad JSON, missing envelope fields, no migration path | `SAVE_ERROR_CORRUPTED` |
| `VersionTooNew` | schema newer than the build | `SAVE_ERROR_VERSION_TOO_NEW` |
| `ReadFailed` / `WriteFailed` | I/O error (permissions, disk full) | `SAVE_ERROR_READ_FAILED` / `SAVE_ERROR_WRITE_FAILED` (previous save is safe) |
| `InvalidSlot` | bad slot id | `SAVE_ERROR_INVALID_SLOT` |

Technical details (`SaveResult.Detail`) go to `DevLog` only.

## Changing the save format

1. Bump `SaveService.SchemaVersion`.
2. Add `static JsonObject Migrate<Old>To<New>(JsonObject data)` to
   `SaveMigrations` and register it in `Steps`.
3. Add a test that loads a literal old-schema JSON and checks the result.
4. Update the format section above.

Never edit a released migration; never rename a stored id without one.

## Automation isolation

While the remote automation server runs, `SaveService.Instance.Backend` points at
`user://automation/saves/` and `SettingsService.Instance.SettingsPath` at
`user://automation/settings.cfg`; both are deleted and the originals restored
on shutdown, so automated runs never touch a player's saves or preferences.
Fixtures (`automation/fixtures/*.json`) use the same payload format as
`data` above.

## Troubleshooting

- Save location: `user://saves/` → Linux `~/.local/share/godot/app_userdata/Utopia/saves/`,
  Windows `%APPDATA%\Godot\app_userdata\Utopia\saves\`.
- A `.bak` next to a slot is the previous successful save; manual recovery =
  rename it to `.json`. (Automatic restore is not implemented.)
- Tests never touch real saves: the runner installs a `MemorySaveBackend`.

## Known limitations

Synchronous writes, single default slot in the UI, no cloud/platform backend,
no automatic `.bak` restore, no encryption or tamper protection.
