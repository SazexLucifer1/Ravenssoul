# Remote automation and testing interface

A structured protocol that lets an external AI agent, CI job, test runner, or
developer tool inspect and drive the **running game** through semantic state
and actions (a "game DOM"), instead of inferring everything from pixels.
Development and QA builds only — production builds never start it.

| Piece | Location |
|---|---|
| Protocol table (source of truth: methods, capabilities, param schemas) | `automation/protocol/methods.json` |
| Godot server, transports, security | `automation/server/` |
| Engine adapter (Godot) + game provider (Utopia) | `automation/adapter/` |
| Approved fixtures | `automation/fixtures/*.json` |
| Start gate (ships in every build, loads the server by path) | `core/diagnostics/automation_gate.gd` |
| Typed Python client, launcher, CLI, MCP adapter, scenario | `tools/automation/` |
| Tests | `tests/unit/automation/`, `tests/integration/test_automation_server.gd`, `tools/automation/tests/` |
| Decisions | ADR 0008 (architecture), ADR 0009 (security model) |

## Audit summary (what was reused)

Before building, the existing systems were reviewed; the automation layer
reads them instead of duplicating them:

| Existing system | Reused for |
|---|---|
| `SceneRouter` (+ route ids) | scene discovery, transition state, `route.*` events, reset/checkpoint navigation |
| `ScreenStack` / `UiScreen` / `GameButton` / `FocusChain` | screen discovery, modality (a blocked control is not interactable), focus, button states |
| `InputMap` actions + `InputMethod` service | raw input goes through `Input.parse_input_event` — the same path as players |
| `GameSession`, `SaveService` (+ `SaveBackend`) | authoritative state, fixtures, checkpoints, isolated automation saves |
| Content ids (`hero_deserter`, `mission_first_spark`, …) | stable entity ids |
| Translation keys | language-independent assertions (`text_key`) |
| In-repo test runner + `Logger` error capture | protocol/adapter tests; log observation |
| `tests/run_tests.sh` + GitHub Actions | CI integration |

There was no existing debug console, telemetry, or networking to reuse.

## Threat model (short)

| Asset | Threat | Mitigation |
|---|---|---|
| Player machine / files | Remote party drives the game or reads files | Server off unless `--automation` is passed **and** the build allows it; binds `127.0.0.1`; no file-access methods; fixtures are shipped ids, never paths; screenshots return bytes (client writes the file) |
| Localhost from a browser | Malicious web page posts to `127.0.0.1` (CSRF) or uses DNS rebinding | Bearer token required on every RPC; requests with an `Origin` header are rejected; `Host` must be the bound address; WebSocket must authenticate in its first message |
| Token | Leak via process list / logs / repo | Token passed by environment variable (`UTOPIA_AUTOMATION_TOKEN`) or written to an owner-only `user://automation/session.json`; never logged; constant-time comparison; expires (default 1 h; ≤ 15 min for remote binds) |
| Game integrity in shipped builds | Automation used as a cheat/exploit channel | Release exports exclude `automation/` (test-enforced); gate refuses release builds without the `automation_qa` feature; `production` profile never starts; no static references from game code (test-enforced) |
| Game state | Unrestricted mutation / code execution | Every method maps to a capability checked against the profile; dev mutations are allow-listed (`dev.command` enum), validated (teleport respects walls); **no eval/console execution exists** |
| Availability | Flooding, huge payloads, slowloris | Token-bucket rate limit (burst 240, 120/s), 64 KiB body / 8 KiB header limits, 5 s read timeout, 16 connection cap, wait timeouts ≤ 120 s |
| Player data | Leaking private data | No player-identifying data exists; logs redact local paths; state contains ids/numbers only; saves/settings written by automation go to `user://automation/` and are deleted on shutdown |
| Leftover state | Stuck inputs / test data after failures | Held inputs released on `up`, expiry, scene change, session close, idle timeout (120 s), and shutdown; spawned units, fixture saves, checkpoints removed with the session |

Out of scope: a malicious local user with the same OS account (they can
already control the game process).

## Running it

```bash
# Launch with automation (dev/debug builds). Token via env, loopback only:
UTOPIA_AUTOMATION_TOKEN=$(python3 -c "import secrets;print(secrets.token_hex(32))") \
  godot --path . -- --automation --automation-profile=qa --automation-port=47801
# Or let the launcher do it (picks free ports, fresh token, prints connection JSON):
PYTHONPATH=tools/automation python3 -m utopia_automation launch --godot $GODOT --project . --headless

# Talk to it:
export UTOPIA_AUTOMATION_URL=http://127.0.0.1:47801 UTOPIA_AUTOMATION_TOKEN=...
PYTHONPATH=tools/automation python3 -m utopia_automation caps
PYTHONPATH=tools/automation python3 -m utopia_automation query --interactable
PYTHONPATH=tools/automation python3 -m utopia_automation act activate --target main_menu.new_game
PYTHONPATH=tools/automation python3 -m utopia_automation wait '{"source":"scene","path":"route","op":"eq","value":"gameplay"}'
PYTHONPATH=tools/automation python3 -m utopia_automation key Left
PYTHONPATH=tools/automation python3 -m utopia_automation screenshot out.png     # needs a non-headless run
PYTHONPATH=tools/automation python3 -m utopia_automation quit
# Or install the package: pip install -e tools/automation  (then `utopia-automation ...`)
```

Command-line flags (after `--`): `--automation`, `--automation-profile=dev|qa`,
`--automation-port=N` (WebSocket = N+1; 0 = any free port), `--automation-bind=ADDR`
(non-loopback also needs `--automation-allow-remote`), `--automation-token-ttl=SECONDS`.
If no token is supplied, one is generated and written to
`user://automation/session.json` (permissions 0600, deleted on shutdown).
While a server runs, a small localized badge ("Automated test session
active") is shown at the top of the screen.

### Python SDK

```python
from utopia_automation import GameProcess, Cond

with GameProcess(godot, ".", profile="qa", headless=False) as game, game.client() as c:
    c.wait_for_route("main_menu")
    c.activate("main_menu.new_game")
    c.wait_for_route("gameplay")
    c.key("Left"); c.wait_settled()
    c.assert_that(Cond.state("unit.cell").eq([0, 10]))
    c.screenshot("out/left.png")
```

All protocol methods have typed wrappers (`tools/automation/utopia_automation/client.py`);
errors raise typed exceptions (`NotInteractableError`, `TimeoutError_`, …).
Standard library only; Python ≥ 3.10.

### MCP adapter (AI agents)

`python -m utopia_automation.mcp_server` (or `utopia-automation-mcp`) speaks MCP
over stdio and forwards every tool call through the typed client, so it
inherits the game's profile, auth, rate limit, and validation. It attaches to
a running game via `UTOPIA_AUTOMATION_URL` / `UTOPIA_AUTOMATION_TOKEN` and
never launches processes. Tools: `game_capabilities`, `game_state`,
`game_query`, `game_entity`, `game_actions`, `game_perform`, `game_key`,
`game_gamepad`, `game_release_inputs`, `game_wait_until`, `game_wait_settled`,
`game_assert`, `game_events`, `game_logs`, `game_reset`, `game_load_fixture`,
`game_screenshot`.

## Protocol

- **Name/version**: `utopia-automation` `1.0`. Additive changes bump the
  minor version; breaking changes bump the major (clients check it).
- **Wire format**: JSON-RPC 2.0. Single requests only (no batches).
- **HTTP** (default, simplest for CI/curl): `POST /v1/rpc` with
  `Authorization: Bearer <token>` and `Content-Type: application/json`;
  `GET /v1/health` (unauthenticated liveness: `{ok, protocol, version}` only).
  One request per connection. Status codes: 200 (result *or* JSON-RPC error),
  400 malformed, 401 token, 403 host/origin, 404, 405, 408 read timeout,
  413 body, 415 content type, 429 rate limit, 431 headers, 503 connection cap.
- **WebSocket** (port + 1): first message
  `{"jsonrpc":"2.0","id":1,"method":"session.authenticate","params":{"token":"…"}}`
  within 3 s, then JSON-RPC requests plus pushed notifications
  `{"jsonrpc":"2.0","method":"event","params":<event>}` for types registered
  with `events.subscribe` (`"*"` = all).
- **Why these transports**: both are loopback-friendly, debuggable with
  standard tools, and available in every engine and language. Another
  transport (e.g. a USB/ADB bridge or a platform devkit channel) is only
  justified for devices that cannot open loopback sockets; it must carry the
  same JSON-RPC messages and the same token.

### Errors

`{"code", "message", "data": {"kind", …}}` — kinds: `parse_error` (-32700),
`invalid_request` (-32600), `method_not_found` (-32601), `invalid_params`
(-32602, `data.problems`), `internal` (-32603), `unauthorized` (-32001),
`forbidden` (-32002, `data.capability`), `rate_limited` (-32003), `not_found`
(-32004), `unsupported` (-32005), `not_interactable` (-32006), `timeout`
(-32007, `data.actual`), `session_expired` (-32008), `busy` (-32009, e.g.
during a scene transition), `payload_too_large` (-32010).

### Sessions

The bearer token identifies the client. The first authenticated request opens
a session; `session.hello` returns its id. A session ends on `session.close`,
after 120 s without requests, or at shutdown — and every ending runs the same
cleanup (held inputs, spawned units, fixture saves, checkpoints, recording).
Reconnecting with the same token opens a fresh session; missed events remain
readable with `observe.events {since_seq}` (1000-event buffer).

### Methods

Generated from `automation/protocol/methods.json` by
`python3 tools/automation/scripts/gen_method_table.py` (a test fails if this
table misses a method).

<!-- methods:start -->
| Method | Capability | Params (**required**) | Result | Purpose |
|---|---|---|---|---|
| `session.hello` | `session` | — | {protocol, session_id, profile, capabilities[], expires_at_unix} | Start/resume the session; returns protocol version, profile, granted capabilities. |
| `session.close` | `session` | — | {closed} | End the session: releases held input, stops recording, removes fixtures and checkpoints. |
| `session.reset` | `reset` | route: main_menu|gameplay | {route} | Release input, clear campaign state and automation saves, unpause, go to a route (default main_menu). |
| `protocol.capabilities` | `session` | — | object | Protocol version, engine/adapter, build, profile, capabilities, methods, actions, input devices, events, limits. |
| `protocol.schema` | `session` | — | object | This method table (params schemas and capabilities). |
| `scene.current` | `inspect` | — | object | Current route, scene id, transition and pause state, open screens. |
| `tree.query` | `inspect` | role: string<br>type: string<br>tag: string<br>id_prefix: string<br>visible_only: boolean<br>interactable_only: boolean<br>limit: integer | {entities[]} | List entities (scene, screens, UI controls, units, grid, mission, resources) matching filters. |
| `entity.get` | `inspect` | **id**: string<br>deep: boolean | entity | Describe one entity. deep=true needs inspect.deep (dev). |
| `state.get` | `inspect` | — | object | Authoritative game-state snapshot (ids and numbers only). |
| `actions.available` | `inspect` | — | {actions[]} | Semantic actions that can be performed right now, with targets. |
| `action.perform` | `act.semantic` | **action**: string (enum)<br>target: string<br>args: object | {performed, ...} | Perform a semantic game action (validated against game rules). |
| `input.key` | `act.input` | **key**: string<br>mode: tap|down|up<br>duration_ms: integer | {held[]} | Inject a keyboard key through the real input path. down without up is a held input. |
| `input.action` | `act.input` | **action**: string<br>mode: tap|down|up<br>duration_ms: integer | {held[]} | Inject an InputMap action press/release. |
| `input.mouse` | `act.input` | **mode**: move|click|down|up<br>**x**: number<br>**y**: number<br>button: left|right|middle | {held[]} | Mouse at logical (1280x720-space) coordinates. |
| `input.gamepad` | `act.input` | button: string (enum)<br>axis: left_x|left_y|right_x|right_y<br>value: number<br>mode: tap|down|up<br>duration_ms: integer | {held[]} | Gamepad button (button+mode) or axis (axis+value). |
| `input.touch` | `act.input` | index: integer<br>**x**: number<br>**y**: number<br>mode: tap|down|up|drag | {held[]} | Touch/pointer event at logical coordinates. |
| `input.release_all` | `act.input` | — | {released} | Release every held key, button, action, axis and touch. |
| `wait.frames` | `wait` | **count**: integer | {frames} | Wait an exact number of process frames. |
| `wait.until` | `wait` | **condition**: object<br>timeout_ms: integer | {met, frames, elapsed_ms, actual} | Wait until a predicate holds (checked once per frame). |
| `wait.event` | `wait` | **type**: string<br>match: object<br>after_seq: integer<br>timeout_ms: integer | {met, event} | Wait for an event of a type (optionally matching fields) after a sequence number. |
| `wait.settled` | `wait` | timeout_ms: integer | {met, frames} | Wait until no scene transition, unit movement, or held input is in progress. |
| `assert.check` | `assert` | **condition**: object<br>message: string | {passed, actual, message} | Evaluate a predicate now; failures are returned (not raised) and logged in the session. |
| `checkpoint.create` | `checkpoint` | **name**: string | {name} | Snapshot campaign state + route in memory. |
| `checkpoint.restore` | `checkpoint` | **name**: string | {name, route} | Restore a checkpoint (campaign state + route). |
| `checkpoint.list` | `checkpoint` | — | {names[]} | List checkpoint names. |
| `fixture.list` | `fixture` | — | {fixtures[]} | Approved fixture ids. |
| `fixture.load` | `fixture` | **fixture**: string<br>write_save: boolean | {fixture, route} | Apply an approved fixture (campaign state, optional save slot in the isolated automation save folder). |
| `recording.start` | `recording` | — | {recording} | Start recording requests and events. |
| `recording.stop` | `recording` | — | {entries[]} | Stop and return the recording. |
| `observe.screenshot` | `observe.screenshot` | max_width: integer | {width, height, png_base64} | PNG of the current frame (base64). Unavailable in headless runs. |
| `observe.logs` | `observe.logs` | since_seq: integer<br>min_level: info|warning|error<br>limit: integer | {lines[], last_seq} | Recent engine/game log lines (paths redacted). |
| `observe.events` | `observe.events` | since_seq: integer<br>types: array<br>limit: integer | {events[], last_seq} | Recent events after a sequence number. |
| `events.subscribe` | `observe.events` | types: array | {subscribed[]} | WebSocket only: push matching events as notifications. |
| `observe.perf` | `observe.perf` | — | object | FPS, frame times, node/object counts, static memory. |
| `app.quit` | `app.quit` | exit_code: integer | {quitting} | Clean shutdown (cleanup runs first). |
| `dev.teleport` | `dev.teleport` | **target**: string<br>**cell**: array | {cell} | Move a unit to any walkable free cell, bypassing movement rules. |
| `dev.spawn` | `dev.spawn` | **hero_id**: string<br>**battalion_id**: string<br>**cell**: array | {id} | Spawn an extra unit from roster ids on a free cell (gameplay only). |
| `dev.command` | `dev.command` | **command**: heal_all|grant_resources|win_mission|lose_mission<br>args: object | object | Allow-listed debug commands. There is no arbitrary code execution. |
<!-- methods:end -->

## Capability matrix

| Capability | dev | qa | production |
|---|:-:|:-:|:-:|
| `session` (hello, close, capabilities, schema) | ✓ | ✓ | server disabled |
| `inspect` (scene, tree, entity, state, actions) | ✓ | ✓ | — |
| `inspect.deep` (theme/focus internals, unit stats) | ✓ | — | — |
| `observe.screenshot` (only when a renderer exists) | ✓ | ✓ | — |
| `observe.logs`, `observe.events`, `observe.perf` | ✓ | ✓ | — |
| `act.semantic`, `act.input` | ✓ | ✓ | — |
| `wait`, `assert` | ✓ | ✓ | — |
| `checkpoint`, `reset`, `fixture`, `recording` | ✓ | ✓ | — |
| `app.quit` | ✓ | ✓ | — |
| `dev.teleport`, `dev.spawn`, `dev.command` (allow-listed) | ✓ | — | — |
| Arbitrary console / code execution | never | never | never |

Where profiles may run:

| Build | dev | qa | production |
|---|---|---|---|
| Editor / debug export (`OS.is_debug_build()`) | ✓ | ✓ | refused |
| Release export from preset **"Linux QA (automation)"** (`automation_qa` feature) | refused | ✓ | refused |
| Release export from the Windows/Linux presets | `automation/` not included → refused | refused | refused |

`protocol.capabilities` reports exactly what the current build grants
(e.g. no `observe.screenshot` in headless runs), so one client adapts to any
build or engine.

## Entities ("game DOM")

Every entity has a stable id, `type`, `name`, `role`, `tags`, `visible`,
`enabled`, `interactable`, `position` (logical 1280×720 coordinates, usable
with `input.mouse`), `components`, `actions`, `properties`, and for text
`text_key` (language-independent) + `text` (current localized string).
**Assert on `text_key` and ids, never on `text`.**

| Id pattern | Role | Source |
|---|---|---|
| `scene.<route>` | scene | `SceneRouter` route id |
| `screen.<name>` | screen | `automation_id` metadata on `UiScreen` roots |
| `main_menu.*`, `pause.*`, `settings.*`, `confirm.*`, `result.*`, `hud.*`, `boot.*` | button/switch/combobox/label/progressbar | `automation_id` metadata in `.tscn` |
| `unit.<hero_id>`, `unit.spawned.<n>` | unit | roster content id / spawn order |
| `unit.<hero_id>/health` · `/mover` · `/animator` | component | component nodes |
| `grid.<map_id>` | grid | `BattleMapData.id` |
| `mission.<mission_id>` | quest | `MissionData.id` |
| `resource.<id>` | item | `ResourceWallet` ids |
| fixture ids (`fixture.list`) | fixture | `automation/fixtures/*.json` |

Duplicate ids within one scene get `#2`, `#3` suffixes in tree order (tests
require shipped screens to have none). `interactable` means a player could
use it right now: visible, enabled, and not behind a modal screen.

`state.get` returns: `route`, `scene_id`, `transitioning`, `paused`,
`screens` (bottom→top), `focused`, `locale`, `input_method`, `held_inputs`,
`save.has_save`, `campaign` (wallet, completed missions, soul energy,
loadout), `mission` (`id`, `finished`, `victory`, `save_state`) and `unit`
(`id`, `cell`, `health`, `max_health`, `defeated`, `moving`, `can_act`).

### Semantic actions

| Action | Supported | Behaviour |
|---|---|---|
| `activate` | ✓ | Press a button/switch by id (refused if not interactable) |
| `select` | ✓ | Combobox item by `args.index` or `args.value` (e.g. locale `de`) |
| `toggle` | ✓ | Switch; optional `args.on` |
| `confirm` / `cancel` | ✓ | Real `ui_accept` / `ui_cancel` input |
| `move` | ✓ | Player unit by `args.direction` or `args.path` (≤ 64 steps), through game rules; stops at the first blocked step (`stopped: "blocked"`) |
| `pause` / `resume` | ✓ | Real `pause` input action, only when allowed |
| `interact`, `equip`, `use`, `choose_dialogue` | — | Defined by the protocol; this game has no such systems yet → `unsupported` |

### Raw input

`input.key` (key names as Godot prints them: `Left`, `Escape`, `Enter`, `Shift`),
`input.action` (InputMap action), `input.mouse` (`move|click|down|up`, logical
coordinates), `input.gamepad` (`button`+`mode` or `axis`+`value`, device 0),
`input.touch`. Modes: `tap` (press now, release on a later frame — at least
one frame held), `down` (held until `up`, `duration_ms`, or cleanup), `up`.
All go through `Input.parse_input_event`, so InputMap, GUI focus,
`_unhandled_input`, and input-method detection behave exactly as for players.

### Predicates (waits and assertions)

Declarative only — no code is evaluated:

```json
{"source": "state", "path": "unit.cell", "op": "eq", "value": [0, 10]}
{"source": "entity", "id": "pause.resume", "path": "focused", "op": "eq", "value": true}
{"source": "scene", "path": "screens", "op": "contains", "value": "screen.pause"}
{"all": [ ... ]}   {"any": [ ... ]}   {"not": { ... }}
```

Ops: `eq ne gt gte lt lte contains in exists not_exists`. Paths are dotted
(`campaign.wallet.gold`, `unit.cell.0`). Limits: depth 6, 64 nodes.
`wait.until` checks once per process frame and returns `frames`/`elapsed_ms`;
on timeout it returns `timeout` with the last `actual` value. `wait.settled`
requires two consecutive frames with no scene transition, no unit movement,
and no held input. `wait.frames` waits an exact frame count. `assert.check`
never raises; it reports `passed`/`actual` and records failures in the session.

### Events

`route.changing`, `route.changed`, `route.failed`, `screen.opened`,
`screen.closed`, `unit.moved`, `unit.blocked`, `unit.damaged`,
`unit.defeated`, `mission.objective_reached`, `mission.finished`,
`save.finished`, `load.finished`, `input.method_changed`, `input.released`,
`locale.changed`, `campaign.started`, `campaign.loaded`,
`automation.session_opened`, `automation.session_closed`. Each event:
`{seq, type, frame, t_ms, data}` with ids/numbers only. They are derived
from existing game signals (see `SIGNALS_AND_EVENTS.md`).

### Checkpoints, fixtures, reset, recordings

- `checkpoint.create/restore`: campaign state + route, in memory, per session
  (max 16). Mid-mission positions are not captured (same rule as saves).
- `fixture.load`: applies an approved JSON fixture from `automation/fixtures/`
  (`campaign_fresh`, `campaign_after_first_mission`, `campaign_corrupted_soul`);
  `write_save` also writes it to the **isolated** automation save folder.
- `session.reset`: releases input, removes temporary state and automation
  saves, clears campaign and checkpoints, unpauses, goes to `main_menu` (or a
  fresh `gameplay`).
- `recording.start/stop`: timestamped requests + events; the client's
  `replay()` re-sends recorded requests.

## Implemented scenario

`tools/automation/utopia_automation/scenario_first_mission.py`
(`python -m utopia_automation scenario first_mission …`):

1. Launch the game (fresh token, free ports, QA profile), check protocol version and capabilities.
2. Title screen: discover controls by id, assert `text_key` and `primary` tag.
3. Raw keyboard focus navigation (Down/Up) verified through `scene.focused`.
4. Semantic `activate` New Campaign; wait for the `route.changed` event.
5. Raw keyboard Left → unit cell `[0,10]`; raw gamepad D-pad right → `[1,10]` and input method `gamepad`.
6. Held key tracked and released (`release_all`), `held_inputs` empty.
7. Gamepad Start pauses (pause screen focused on Resume), B resumes.
8. Semantic `move` along an 18-step path to the objective; wait for `mission.finished{victory}` and `save_state == "saved"`.
9. Assert authoritative state: 50 gold, mission completed, save exists, result title key, Continue interactable, no damage taken.
10. Screenshot (required in CI under Xvfb), restore the `mission_start` checkpoint, save the recording and events, assert no error-level logs.
11. `app.quit`; the process must exit with code 0 and no script errors or leaks.

Outputs: `game.log`, `first_mission_victory.png`, `first_mission_recording.json`,
`first_mission_events.json` in `--out`.

## CI and local commands

```bash
GODOT=/path/to/godot tests/run_tests.sh      # includes everything below except the Xvfb screenshot run
godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=automation   # Godot-side tests
(cd tools/automation && python3 -m unittest discover -s tests -t .)                    # client/MCP/parity tests
PYTHONPATH=tools/automation python3 -m utopia_automation scenario first_mission --godot $GODOT --project . --headless
PYTHONPATH=tools/automation xvfb-run -a python3 -m utopia_automation scenario first_mission \
  --godot $GODOT --project . --out automation-output --require-screenshot
```

GitHub Actions: job `test` runs `tests/run_tests.sh` (Godot tests, Python
tests, headless scenario, production-gating check, smoke runs); job
`automation-e2e` runs the scenario under Xvfb with `--require-screenshot` and
uploads `automation-output/`. Ordinary builds and the game's own tests do not
need the automation server: it is only started by these jobs.

## Security decisions (summary)

See the threat model above and ADR 0009. In short: off by default; dev/QA
builds only; loopback bind; token on every request (even locally) with
expiry; no browser origins; Host check; per-method capability check;
JSON-schema validation of every payload (unknown fields rejected); rate
limit; size and time limits; allow-listed dev commands; no file paths or code
in the protocol; redacted logs; audit log in `user://automation/audit.log`
(method names, outcomes, denials, and assertion messages — never params or
tokens; rotated at 1 MiB);
automation never writes the player's real saves or settings.

## Lifecycle rules

| Event | Cleanup |
|---|---|
| `input.* up` / `duration_ms` / tap | that hold released |
| Scene change (`route.changing`) | all held input released |
| `session.close`, idle timeout (120 s) | held input, spawned units, fixture saves, checkpoints, recording |
| Client disconnect (HTTP) | nothing held per connection; session idles out |
| WebSocket close | subscriptions dropped with the peer |
| `app.quit`, window close, node exit | session cleanup, sockets closed, logger removed, signal hooks disconnected, session file and isolated saves/settings deleted, original save backend + settings path restored |
| Test failure (client side) | `with AutomationClient(...)` / `GameProcess` context managers close the session and stop the game; server idle timeout as a backstop |

## Extending

### New entity

- **UI control or screen**: add `metadata/automation_id = "<area>.<name>"` to
  the node in its `.tscn` (screens: `screen.<name>`). Buttons and screens
  without ids fail `test_every_button_in_shipped_screens_has_an_automation_id`
  — add new screens to its list.
- **Game object**: in `UtopiaAutomationProvider.collect()` add
  `index[id] = {"kind": ..., "provider": self, "node": ...}` with an id derived
  from content ids, and describe it in `describe()`. Expose only safe,
  test-relevant properties (no paths, no private data).

### New semantic action

1. Add the name to the `action` enum in `methods.json` and to
   `GodotAutomationAdapter.SEMANTIC_ACTIONS` (`true` when supported).
2. Implement it in a provider's `perform()` by calling the **existing game
   rule** (like `move` calls `PlayerUnitController.try_step`), returning
   `performed:false` + reason for rule rejections and `not_interactable` when
   the game wouldn't let a player do it.
3. Advertise it in `add_actions()`; add a test and a Python wrapper.

### New protocol method

Add it to `methods.json` (capability + strict params schema), implement
`_h_<method_with_underscores>` in `AutomationServer`, add a typed wrapper in
`client.py`, regenerate this document's method table
(`python3 tools/automation/scripts/gen_method_table.py`), and grant the capability
in `AutomationProfiles`. Tests: `test_every_method_has_a_handler` and the
Python parity tests enforce the wiring.

### New scene

Register the route as usual; give its controls/screens `automation_id`s. If
it has game objects, extend the provider (`is_gameplay`-style detection on
public fields, `hook_scene()` to emit events from its signals).

### New engine adapter

The protocol is engine-independent. Another engine implements the same
JSON-RPC methods (`methods.json`), error codes, entity shape, predicate
semantics, and capability reporting; `AutomationAdapter` lists the
operations to map. The Python client, CLI, MCP adapter, and scenario work
unchanged against any conforming server (`protocol.capabilities.engine`
identifies it).

## Troubleshooting

- *Nothing listens*: check the game log for `Automation server not started:`
  (release build, wrong profile, port in use, short token).
- *401*: token mismatch/expired — the launcher passes it by env; tokens
  expire (`--automation-token-ttl`).
- *403*: you sent an `Origin` header (browsers) or a Host other than
  `127.0.0.1:<port>`/`localhost:<port>`.
- *`not_interactable`*: a modal screen is open or the control is disabled —
  inspect `scene.current.screens`.
- *`busy`*: a scene transition is running — `wait.settled` first.
- *Screenshots `unsupported`*: the game runs `--headless`; use Xvfb.
- Audit trail: `user://automation/audit.log`.

## Known limitations

- One client session per server (one token); no multi-client arbitration.
- HTTP is one request per connection (no keep-alive); fine for local CI.
- Raw pointer/touch input targets the root viewport; no multi-window support.
- No pixel-diffing of screenshots; no video recording (recordings are command/event logs).
- `checkpoint`s capture campaign state + route only, not mid-mission positions.
- `interact`, `equip`, `use`, `choose_dialogue` are declared but unsupported until those systems exist.
- The release-export exclusion is verified on the preset configuration, not by building exports (no export templates in CI).
- Remote device testing (`--automation-bind` + `--automation-allow-remote`) has
  no TLS; use it only on trusted networks or through an SSH tunnel.
