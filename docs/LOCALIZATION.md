# Localization and internationalization

## Launch locales

| Locale | Catalog | Status |
|---|---|---|
| `en` English | `assets/localization/en.po` | **Source** and fallback locale |
| `de` German | `assets/localization/de.po` | Complete |

Supported list: `LocaleRegistry.SUPPORTED` (`core/localization/locale_registry.gd`).
First launch picks the OS language if supported (`de_AT` → `de`), else `en`.
The player's choice is saved in `user://settings.cfg` and applied at startup
(`Settings` autoload). Language changes apply immediately, no restart.

## Rules

1. **Every player-facing string is a translation key** (`UPPER_SNAKE_CASE`,
   prefixed by area: `MAIN_MENU_`, `HUD_`, `SAVE_ERROR_`, `CARD_<ID>_NAME`).
   In scenes, put the key in `text`; Godot's auto-translation shows the
   localized text. Data Resources store keys in `*_key` fields.
2. **Named placeholders** `{name}`, filled with `Loc.format(key, args)` or
   `Loc.format_plural(key, count, args)`. Never `tr(key).format(...)` (a test
   enforces this): Godot's pseudolocalizer only protects `%s`-style
   placeholders and `tr_n` skips pseudolocalization, so `Loc` fills
   placeholders first and pseudolocalizes the finished string.
3. **Plurals**: PO plural entries `msgid "KEY"` / `msgid_plural "KEY_PLURAL"`
   with the locale's `Plural-Forms` header. Godot has no ICU MessageFormat;
   gettext plural forms + `{placeholders}` are the engine equivalent.
   Select/gender variants: use separate keys (`..._MALE`, `..._FEMALE`) chosen
   in code by a stable enum, not string concatenation.
4. **Numbers**: format with `LocaleFormat.integer/decimal/percent` before
   passing them in (`1,234` en / `1.234` de; native digits via TextServer).
   Dates/times/currency: none are shown yet; add them to `LocaleFormat`
   (a table per locale, like grouping) when first needed. The game has no
   real-world currency.
5. **Never concatenate translated fragments** to build sentences; use one key
   with placeholders. Lists use `LIST_SEPARATOR`.
6. **Invariant ids everywhere else**: saves, analytics, achievements, game
   rules, signals, and logs use ids (`hero_deserter`, `gold`), never
   translated text or keys of display strings.
7. Endonyms in the language picker come from each catalog's
   `LANGUAGE_ENDONYM` entry, so the picker always reads "Deutsch", not
   "German".
8. Text that is assembled in code and must not be re-translated sets
   `auto_translate_mode = Disabled` on its Label (e.g. HUD hint, rewards).

### Missing-key behavior

At runtime, a key missing in the current locale falls back to `en`
(`internationalization/locale/fallback`). A key missing everywhere shows the
raw key — tests make that impossible to ship (`test_all_referenced_keys_exist`).

## Catalog format and translator workflow

PO (gettext) files, UTF-8, one per locale. Each entry:

```po
#. Unit stepped on a hazard. {amount} is a number.
#. English: Hazard! You lost {amount} health point.
msgid "HUD_HINT_HAZARD"
msgid_plural "HUD_HINT_HAZARD_PLURAL"
msgstr[0] "Gefahr! Du hast {amount} Lebenspunkt verloren."
msgstr[1] "Gefahr! Du hast {amount} Lebenspunkte verloren."
```

- `#.` first line: translator note (context, length limits, placeholder meaning).
- `#. English:` (non-source catalogs): the current English text. If English
  changes, `test_translations_are_not_stale` fails until the translation is
  reviewed and this line updated.
- Keep `{placeholders}` exactly; the placeholder test checks parity.
- Edit with any PO editor (Poedit, Lokalize) or a text editor.

## Commands

```bash
# Validate everything (keys exist, no unused keys, placeholder parity, plurals,
# stale translations, hard-coded scene text, fallback, pseudo, fonts, layout):
godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=localization
godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=ui_layout

# Extract keys referenced in code/scenes/resources but missing from en.po
# (prints ready-to-paste PO stubs; exit code 1 if any are missing):
godot --headless --path . --script res://tests/tools/extract_keys.gd

# Locale screenshots (en, de, pseudo, rtl) — needs a renderer:
xvfb-run -a godot --path . --rendering-driver opengl3 --audio-driver Dummy \
  res://tests/tools/screenshot_capture.tscn -- --out=$PWD/screenshots
```

## Adding a key

1. Use the key in a scene (`text = "MY_KEY"`) or code (`Loc.format(&"MY_KEY")`).
2. Run the extraction command; paste the stub into `en.po`, write the English
   and a translator note.
3. Add the entry to every other catalog with the `#. English:` line and a
   translation (or a reviewed copy of the English, marked in the note).
4. Run the localization tests.

## Adding a language

1. Copy `en.po` to `<locale>.po`; set the `Language:` and correct
   `Plural-Forms` headers; translate; add `#. English:` lines; set
   `LANGUAGE_ENDONYM`.
2. Add the path to `internationalization/locale/translations` in
   `project.godot` and the code to `LocaleRegistry.SUPPORTED`.
3. Add number separators to `LocaleFormat.GROUPING`/`DECIMAL`.
4. RTL language (ar, he, fa, ur): already listed in `LocaleRegistry.RTL_LOCALES`;
   the UI mirrors automatically (root window follows the application locale's
   direction; Controls inherit). Review the RTL screenshots.
5. Non-Latin script: add a font with coverage under `assets/fonts/` as a
   fallback of the theme font **before** shipping
   (`test_default_font_covers_launch_locales` will fail otherwise).
6. Run tests and screenshots; get a native-speaker review.

## Pseudolocalization and RTL testing

Project settings (`internationalization/pseudolocalization/*`): expansion
ratio 0.3, prefix `[`, suffix `]`, accents on. Toggle at runtime with
`TranslationServer.pseudolocalization_enabled`. Tests use it to verify long
strings fit (`test_long_pseudolocalized_text_still_fits`). The screenshot
tool's `rtl` variant enables fake BiDi plus RTL layout. Note: fake BiDi also
reverses digits — that's a pseudo artifact, not a bug.

## Fonts, text layout, input

- Font: the engine's built-in default font. A test verifies it has a glyph
  for every character in the `en` and `de` catalogs. It does **not** cover
  CJK, Arabic, Hebrew, Thai, or Devanagari.
  Plan: Noto Sans family as fallbacks in the theme font.
- Godot's TextServer (Advanced) handles BiDi, shaping, and line breaking
  (incl. CJK/Thai via ICU data) — enable `autowrap_mode` on any label that can
  grow; tests enforce that text fits at 1280×720 with +30% expansion and 150%
  text size.
- Text input/IME: no text fields exist yet. When added (save names), use
  `LineEdit`, which supports IME composition on Windows/Linux.

## Audio and assets per locale

No voice or localized textures yet. Plan: `DialogueLine.voice_id` resolves to
`assets/audio/vo/<locale>/<voice_id>.ogg` with fallback to `en`; subtitles
are always shown (accessibility). Localized textures use Godot's resource
remaps (`internationalization/locale/translation_remaps`).

## Ownership

- Engineering owns keys, `Loc`/`LocaleFormat`, and validation tests.
- Writers own `en.po` text and translator notes.
- Translators own `<locale>.po` translations and `#. English:` reviews.

## Platform limitations

Windows/Linux desktop only; OS locale read via `OS.get_locale()`. No
platform store localization metadata yet.
