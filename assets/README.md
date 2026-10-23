# assets/

| Folder | Contents |
|---|---|
| `localization/` | `<locale>.po` catalogs (source: `en.po`). See docs/LOCALIZATION.md |
| `ui/theme/` | `utopia_theme.tres` — generated from `core/ui/theme/ui_tokens.tres`; do not hand-edit |
| `fonts/` | Empty. The engine's built-in default font is used until the art pass; CJK/Arabic fonts are required before those locales ship |
| `sprites/` | Empty. Units and the grid are drawn in code as placeholders. Planned source: PixelLab (`sprites/<area>/`) |
| `audio/` | Empty. Planned: `audio/music/` (Suno), `audio/vo/<locale>/<voice_id>.ogg` (ElevenLabs), `.ogg` only |
| `PROVENANCE.md` | One row per generated/third-party file (tool, date, prompt, license, approver) — required before committing it |

Naming: `snake_case`, prefixed by kind where useful (`unit_`, `ui_`, `sfx_`).
Pipeline and quality gates: `docs/AI_WORKFLOW.md#art-and-sound`.
