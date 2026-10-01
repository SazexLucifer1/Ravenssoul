# 0003 — PO catalogs with stable keys; `Loc` for placeholders
Status: Accepted
Date: 2026-10-01

## Context
Text must be localizable from day one, support plurals, translator notes,
RTL, and pseudolocalization.

## Decision
- gettext PO catalogs per locale in `assets/localization/`, loaded directly by
  Godot (no generated `.translation` files).
- `msgid` is a stable key (`MAIN_MENU_NEW_GAME`), not English text, so text
  edits don't break references; English lives in `en.po`.
- Named `{placeholders}`, filled by `Loc.format()` which pseudolocalizes the
  final string (the engine pseudolocalizer only protects `%s`).
- Non-source catalogs carry `#. English:` reference lines; a test detects
  stale translations.

## Consequences
Translators need the `#. English:` line for context (provided). Key-based
msgids mean Godot's built-in POT generation isn't used; `extract_keys.gd`
replaces it.

## Alternatives considered
CSV translations (no plurals/notes, generated files); English-as-msgid PO
(typo fixes invalidate all translations); ICU MessageFormat library (no
maintained GDScript implementation; gettext plurals suffice).
