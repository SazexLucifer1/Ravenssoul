# Adding a feature

Example: adding **scouting** (intel tiers per enemy type).

1. **Read first**: the blueprint section for the feature, this folder's
   `ARCHITECTURE.md`, and the feature's README if it exists.
2. **Create the folder** `features/<feature>/` with only what you need:
   ```
   features/scouting/
     data/        Resource scripts (class_name, @export fields, validate())
     content/     authored .tres instances
     ui/          feature-specific Controls/screens
     <thing>.gd   logic (RefCounted) or component (Node)
     <thing>.tscn scenes if the feature has world/UI presence
     README.md    what it owns, status, decisions
   ```
3. **Model data as Resources** with stable `id`s and `*_key` text fields
   (see `RESOURCES.md`). Pure rules go in `RefCounted` classes so they are
   unit-testable without a scene.
4. **Expose signals** for facts other code may care about
   (`intel_tier_changed(enemy_id, tier)`); see `SIGNALS_AND_EVENTS.md`.
5. **Persist** through `GameSession.to_save_data()` / `load_save_data()`
   using ids only. If you change the payload shape, follow the migration
   steps in `SAVE_SYSTEM.md`.
6. **Player text**: add keys to `assets/localization/en.po` **and** every
   launch locale (`de.po`) with a `#.` translator note. Use `Loc.format()` for
   placeholders. Run the key extraction command.
7. **UI**: new screens extend `UiScreen`, use `GameButton` (connect
   `activated`), set `default_focus`, link focus with `FocusChain`, and are
   pushed on the owning scene's `ScreenStack`. One primary action.
8. **Wire it** in the composition root (a routed scene), not by having
   features find each other.
9. **Tests**: unit tests in `tests/unit/<feature>/test_*.gd`; integration
   tests in `tests/integration/` if it touches scenes or flows. Add the new
   screens to `SCREENS` in `tests/integration/test_ui_layout.gd`.
10. **Docs**: update the feature README, `ARCHITECTURE.md` (blueprint
    coverage table), `SIGNALS_AND_EVENTS.md`, `RESOURCES.md`, and add an ADR
    in `docs/decisions/` for any non-obvious choice.
11. **Validate**: `tests/run_tests.sh` must pass (tests, key extraction,
    smoke runs), and capture screenshots if UI changed.

### New routed scene?

Add the id to `core/routing/routes.gd`, map it in
`core/routing/route_table.tres`, and navigate with
`SceneRouter.goto(Routes.X)`. The routes test checks every entry loads.

### New autoload?

Almost never. Only for state with genuinely global lifetime. Write an ADR,
add a row to the autoload table in `ARCHITECTURE.md`, reset its state in
`tests/framework/test_runner.gd::_reset_global_state()`.
