# 0008 — Remote automation: JSON-RPC protocol, engine adapter, opt-in server
Status: Accepted
Date: 2026-10-01

## Context
External AI agents, CI, and tools need to inspect and drive the running game
semantically (ids, state, actions) rather than through pixels, across builds
and potentially other engines, without affecting player builds.

## Decision
- Engine-independent protocol `utopia-automation/1.0`: JSON-RPC 2.0, one
  method table (`automation/protocol/methods.json`) holding capability and
  strict param schema per method; `protocol.capabilities` for adaptation.
- Transports: localhost HTTP (`POST /v1/rpc`) and WebSocket (port + 1, event push).
- Godot side: `AutomationServer` (dispatch, sessions, lifecycle) →
  `GodotAutomationAdapter` (generic scenes/UI/input/screenshots) →
  `AutomationProvider`s (game semantics: `UtopiaAutomationProvider`).
  Everything reads existing systems (router, screen stacks, GameSession,
  SaveService, InputMap); no parallel game state or input system.
- Raw input via `Input.parse_input_event` (the player path); semantic actions
  call existing game rules.
- Not an autoload: `AutomationGate` (in `core/`, ships everywhere) loads the
  server by path from bootstrap only when requested and allowed, and adds it
  under the root. Release exports exclude `automation/`.
- Stable ids: `automation_id` node metadata for UI; content ids for game objects.
- External client: Python, standard library only (typed client, launcher,
  CLI, MCP adapter, scenario) in `tools/automation/`.

## Consequences
Game code stays free of automation dependencies (test-enforced). New screens
must carry ids; new systems add provider code. Protocol changes touch the
method table, a handler, the client, and the docs table (all test-checked).

## Alternatives considered
Pixel/OCR-based testing (brittle); Godot's remote debugger protocol (editor-
coupled, exposes arbitrary object access); GUT-only in-process tests (cannot
serve external agents); an always-on autoload (would ship in production).
