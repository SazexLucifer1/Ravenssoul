# 0009 — Automation security model
Status: Accepted
Date: 2026-10-01

## Context
A local control server is reachable by any local process and, via the
browser, by web pages (CSRF, DNS rebinding). Shipped builds must never expose
a control channel.

## Decision
- Off by default; requires `--automation`; `dev` profile only in debug builds,
  `qa` in debug builds or release builds with the `automation_qa` feature;
  `production` never starts. Release presets exclude `automation/`.
- Loopback bind by default; non-loopback requires `--automation-allow-remote`
  and caps token lifetime at 15 minutes.
- Bearer token on every request even on loopback (≥ 32 chars, random,
  constant-time compare, expiry); passed by environment or an owner-only file.
- Reject `Origin` headers and unexpected `Host` headers; WebSocket must
  authenticate in its first message within 3 s.
- Capability check per method by profile; strict schema validation (unknown
  fields rejected); declarative predicates only; allow-listed dev commands;
  no file paths, code, or console execution anywhere in the protocol.
- Rate limiting, size/time/connection limits; audit log without params/tokens.
- Isolation: automation saves/settings go to `user://automation/` and are
  removed on shutdown; player files are untouched.

## Consequences
Clients must manage a token (the launcher does). No TLS: remote use is for
trusted networks or SSH tunnels only.

## Alternatives considered
Unauthenticated loopback (exploitable from browsers); mutual TLS (heavy for
local CI; revisit for device farms); Unix domain sockets (not portable to Windows).
