# Adding a feature

Example: adding **scouting** (intel tiers per enemy type).

1. **Read first**: the blueprint section for the feature, this folder's
   `ARCHITECTURE.md`, and the feature's README if it exists.
2. **Create the folder** `features/<feature>/` with only what you need:
   ```
   features/scouting/
     data/        Resource classes ([GlobalClass], [Export] properties, Validate())
     content/     authored .tres instances
     ui/          feature-specific Controls/screens
     <Thing>.cs   logic (plain C# class) or component (Node); file name = class name
     <thing>.tscn scenes if the feature has world/UI presence
     README.md    what it owns, status, decisions
   ```
3. **Model data as Resources** with stable `id`s and `*_key` text fields
   (see `RESOURCES.md`). Pure rules go in plain C# classes so they are
   unit-testable without a scene.
4. **Expose signals** for facts other code may care about
   (`[Signal] delegate void IntelTierChangedEventHandler(StringName enemyId, int tier)`); see `SIGNALS_AND_EVENTS.md`.
5. **Persist** through `GameSession.ToSaveData()` / `LoadSaveData()`
   using ids only. If you change the payload shape, follow the migration
   steps in `SAVE_SYSTEM.md`.
6. **Player text**: add keys to `assets/localization/en.po` **and** every
   launch locale (`de.po`) with a `#.` translator note. Use `Loc.Format()` for
   placeholders. Run the key extraction command.
7. **UI**: new screens extend `UiScreen`, use `GameButton` (connect
   `Activated`), set `DefaultFocus`, link focus with `FocusChain`, and are
   pushed on the owning scene's `ScreenStack`. One primary action.
8. **Wire it** in the composition root (a routed scene), not by having
   features find each other.
9. **Tests**: unit tests in `tests/unit/<feature>/Test*.cs`; integration
   tests in `tests/integration/` if it touches scenes or flows. Add the new
   screens to `Screens` in `tests/integration/TestUiLayout.cs`.
10. **Docs**: update the feature README, `ARCHITECTURE.md` (blueprint
    coverage table), `SIGNALS_AND_EVENTS.md`, `RESOURCES.md`, and add an ADR
    in `docs/decisions/` for any non-obvious choice.
11. **Validate**: `tests/run_tests.sh` must pass (guards, C# build, tests,
    key extraction, smoke runs), and capture screenshots if UI changed.

### New routed scene?

Add the id to `core/routing/Routes.cs`, map it in
`core/routing/route_table.tres`, and navigate with
`SceneRouter.Instance.Goto(Routes.X)`. The routes test checks every entry loads.

### New autoload?

Almost never. Only for state with genuinely global lifetime. Write an ADR,
add a row to the autoload table in `ARCHITECTURE.md`, reset its state in
`tests/framework/TestRunner.cs::ResetGlobalState()`, and give it a static
`Instance` property like the existing services.
