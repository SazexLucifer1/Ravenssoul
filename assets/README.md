# assets/

| Folder | Contents |
|---|---|
| `localization/` | `<locale>.po` catalogs (source: `en.po`). See docs/LOCALIZATION.md |
| `ui/theme/` | `utopia_theme.tres` — generated from `core/ui/theme/ui_tokens.tres`; do not hand-edit |
| `fonts/` | Empty. The engine's built-in default font is used until the art pass; CJK/Arabic fonts are required before those locales ship |
| `sprites/`, `audio/` | Empty. Units and the grid are drawn in code as placeholders |

Naming: `snake_case`, prefixed by kind where useful (`unit_`, `ui_`, `sfx_`).
