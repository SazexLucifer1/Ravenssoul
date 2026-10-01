# Architecture decision records

One file per decision: `NNNN-short-title.md`. Never delete; mark as
`Superseded by NNNN` instead.

| # | Decision | Status |
|---|---|---|
| [0001](0001-composition-and-limited-autoloads.md) | Composition, scene-owned UI stacks, five autoloads | Accepted |
| [0002](0002-gl-compatibility-renderer.md) | GL Compatibility renderer | Accepted |
| [0003](0003-po-catalogs-with-stable-keys.md) | PO catalogs with stable keys; `Loc` for placeholders | Accepted |
| [0004](0004-grid-logic-instead-of-physics.md) | Grid logic instead of physics for gameplay collision | Accepted |
| [0005](0005-in-repo-test-runner.md) | In-repo test runner with error capture | Accepted |
| [0006](0006-json-saves-with-envelope.md) | JSON saves with versioned envelope and swappable backend | Accepted |
| [0007](0007-generated-theme-from-tokens.md) | Theme generated from design tokens | Accepted |
| [0008](0008-remote-automation-architecture.md) | Remote automation: JSON-RPC protocol, engine adapter, opt-in server | Accepted |
| [0009](0009-automation-security-model.md) | Automation security model | Accepted |

## Template

```markdown
# NNNN — Title
Status: Proposed | Accepted | Superseded by NNNN
Date: YYYY-MM-DD

## Context
## Decision
## Consequences
## Alternatives considered
```
