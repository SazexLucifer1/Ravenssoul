# 0005 — In-repo test runner with error capture
Status: Accepted
Date: 2026-10-01

## Context
Tests must run headless in CI with only the Godot binary and must not pass
when a script error silently aborts a test function.

## Decision
A small runner (`tests/framework/`) run as the main scene (autoloads
present), `TestCase` assertions, and a Godot 4.5+ `Logger` that fails any test
during which engine/script errors were logged.

## Consequences
No addon to vendor or update. Fewer features than GUT/gdUnit4 (no mocking,
no parameterized tests); revisit if tests need them.

## Alternatives considered
GUT, gdUnit4 (capable, but add a dependency and version coupling).
