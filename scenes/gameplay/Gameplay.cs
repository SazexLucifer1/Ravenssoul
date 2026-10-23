using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Utopia;

/// <summary>
/// Minimal battle scene: composition root for one mission.
///
/// Wires grid, unit, controller, HUD, and menus together through signals.
/// Rules live in features/; this script only coordinates the mission flow.
/// </summary>
[GlobalClass]
public partial class Gameplay : Node
{
    [Signal] public delegate void MissionFinishedEventHandler(bool victory);

    [Export] public PackedScene CharacterScene { get; set; }
    [Export] public PackedScene PauseMenuScene { get; set; }
    [Export] public PackedScene SettingsScreenScene { get; set; }
    [Export] public PackedScene ResultScreenScene { get; set; }
    [Export] public PackedScene ConfirmDialogScene { get; set; }

    [Export] public BattleGrid Grid { get; set; }
    [Export] public Node2D UnitsRoot { get; set; }
    [Export] public Camera2D Camera { get; set; }
    [Export] public PlayerUnitController Controller { get; set; }
    [Export] public Hud Hud { get; set; }
    [Export] public ScreenStack ScreenStack { get; set; }

    public Character PlayerUnit { get; private set; }
    public MissionData Mission { get; private set; }
    public bool Finished { get; private set; }

    private MissionResultScreen _resultScreen;

    public override void _Ready()
    {
        GameSession session = GameSession.Instance;
        if (!session.HasCampaign)
        {
            // Launched directly (editor "Run current scene" or --route=gameplay).
            session.StartNewCampaign();
        }
        Mission = session.ActiveMission;
        Grid.MapData = Mission.Map;
        Camera.Position = Grid.Position + Grid.WorldBounds().GetCenter();

        PlayerUnit = CharacterScene.Instantiate<Character>();
        UnitsRoot.AddChild(PlayerUnit);
        UnitLoadout loadout = session.ActiveLoadout;
        PlayerUnit.Configure(loadout.CombinedStats(), loadout.Hero.NameKey);
        Grid.Place(PlayerUnit, Mission.Map.PlayerSpawn);
        PlayerUnit.PlaceAt(Mission.Map.PlayerSpawn, Grid.CellToLocal(Mission.Map.PlayerSpawn));

        Controller.Unit = PlayerUnit;
        Controller.StepBlocked += _ => Hud.ShowHint(Loc.Format("HUD_HINT_BLOCKED"));
        Controller.HazardEntered += (_, dealt) => Hud.ShowHint(Loc.FormatPlural("HUD_HINT_HAZARD", dealt,
            new Dictionary<string, string> { ["amount"] = LocaleFormat.Integer(dealt) }));
        Controller.ObjectiveReached += _ => Finish(true);
        PlayerUnit.Defeated += () => Finish(false);
        Hud.Bind(PlayerUnit, Mission);
        Controller.Stepped += _ => RefreshMoveHighlights();
        RefreshMoveHighlights();
        ScreenStack.Emptied += OnStackEmptied;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!inputEvent.IsActionPressed("pause") || Finished)
            return;
        if (ScreenStack.IsEmpty())
        {
            GetViewport().SetInputAsHandled();
            OpenPauseMenu();
        }
        else if (ScreenStack.Top() is PauseMenu)
        {
            // Start/Esc toggles the pause menu closed again.
            GetViewport().SetInputAsHandled();
            ScreenStack.Pop();
        }
    }

    public PauseMenu OpenPauseMenu()
    {
        GetTree().Paused = true;
        Controller.Enabled = false;
        Grid.SetMoveHighlights(new List<Vector2I>());
        var menu = PauseMenuScene.Instantiate<PauseMenu>();
        menu.ResumeRequested += () => ScreenStack.Pop();
        menu.SettingsRequested += () => ScreenStack.Push(SettingsScreenScene.Instantiate<UiScreen>());
        menu.AbandonRequested += ConfirmAbandon;
        ScreenStack.Push(menu);
        return menu;
    }

    /// <summary>Ends the mission (victory or defeat) and shows the result screen.</summary>
    public void Finish(bool victory)
    {
        if (Finished)
            return;
        Finished = true;
        Controller.Enabled = false;
        Grid.SetMoveHighlights(new List<Vector2I>());
        Dictionary<StringName, int> rewards = victory ? GameSession.Instance.CompleteMission(Mission) : new Dictionary<StringName, int>();
        var screen = ResultScreenScene.Instantiate<MissionResultScreen>();
        screen.ContinueRequested += () => _ = SceneRouter.Instance.Goto(Routes.MainMenu);
        screen.RetryRequested += () => _ = SceneRouter.Instance.Goto(Routes.Gameplay);
        screen.SaveRetryRequested += () => _ = Autosave();
        _resultScreen = screen;
        ScreenStack.Push(screen, new ScreenContext { ["victory"] = victory, ["rewards"] = rewards });
        EmitSignalMissionFinished(victory);
        if (victory)
            _ = Autosave();
    }

    private void ConfirmAbandon()
    {
        var dialog = ConfirmDialogScene.Instantiate<ConfirmDialog>();
        dialog.Confirmed += () => _ = SceneRouter.Instance.Goto(Routes.MainMenu);
        ScreenStack.Push(dialog, new ScreenContext
        {
            ["title_key"] = "CONFIRM_ABANDON_TITLE",
            ["body_key"] = "CONFIRM_ABANDON_BODY",
            ["confirm_key"] = "CONFIRM_ABANDON_ACCEPT",
            ["cancel_key"] = "CONFIRM_ABANDON_DECLINE",
            ["destructive"] = true,
        });
    }

    private void OnStackEmptied()
    {
        GetTree().Paused = false;
        Controller.Enabled = !Finished;
        RefreshMoveHighlights();
    }

    /// <summary>Blue diamonds on the cells the unit can step to (readability, rubric cat. 10).</summary>
    private void RefreshMoveHighlights()
    {
        var cells = new List<Vector2I>();
        if (Controller.Enabled && !PlayerUnit.IsDefeated())
            foreach (Vector2I direction in PlayerUnitController.Directions.Values)
                if (Grid.CanEnter(PlayerUnit.Cell + direction))
                    cells.Add(PlayerUnit.Cell + direction);
        Grid.SetMoveHighlights(cells);
    }

    private async Task Autosave()
    {
        _resultScreen.SetSaveState(MissionResultScreen.SaveState.Saving);
        // One frame so the "Saving…" state is visible before the (blocking) write.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SaveResult result = SaveService.Instance.SaveGame(GameSession.Instance.ToSaveData());
        if (result.IsOk())
            _resultScreen.SetSaveState(MissionResultScreen.SaveState.Saved);
        else
            _resultScreen.SetSaveState(MissionResultScreen.SaveState.Failed, result.PlayerMessageKey());
    }
}
