# 0006 — JSON saves with versioned envelope and swappable backend
Status: Accepted
Date: 2026-10-01

## Context
Saves must survive game updates, never be corrupted by crashes, and later
possibly sync to a platform cloud.

## Decision
JSON payload of plain data and stable ids inside an envelope with
`schema_version`; step-wise `SaveMigrations`; `SaveBackend` interface with an
atomic file backend (temp file + `.bak`) and an in-memory test backend.

## Consequences
Human-readable, diffable saves; easy migrations. No tamper protection.
Synchronous writes (fine for small saves).

## Alternatives considered
`ResourceSaver` `.tres` saves (can execute embedded scripts when loading
untrusted files, couples saves to class layout); binary `store_var` (opaque).
