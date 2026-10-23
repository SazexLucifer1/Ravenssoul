using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Title screen. One clear primary action: "Continue" when a save exists,
/// otherwise "New Campaign".
/// </summary>
[GlobalClass]
public partial class MainMenu : Control
{
    [Export] public Control LayoutRoot { get; set; }
    [Export] public GameButton NewGameButton { get; set; }
    [Export] public GameButton ContinueButton { get; set; }
    [Export] public GameButton SettingsButton { get; set; }
    [Export] public GameButton QuitButton { get; set; }
    [Export] public Label VersionLabel { get; set; }
    [Export] public ScreenStack ScreenStack { get; set; }
    [Export] public PackedScene SettingsScreenScene { get; set; }
    [Export] public PackedScene ConfirmDialogScene { get; set; }

    public override void _Ready()
    {
        ScreenStack.UnderlyingRoot = LayoutRoot;
        NewGameButton.Activated += OnNewGame;
        ContinueButton.Activated += OnContinue;
        SettingsButton.Activated += () => OpenSettings();
        QuitButton.Activated += OnQuit;
        UpdateVersionLabel();
        Refresh();
    }

    // Never await SceneRouter.Goto() here: on success this scene is freed
    // mid-coroutine. Failures come back through RouteFailed instead.
    // Autoload signals outlive this scene: subscribe while in the tree only (docs/AI_INSTRUCTIONS.md).
    public override void _EnterTree()
    {
        SceneRouter.Instance.RouteFailed += OnRouteFailed;
        SettingsService.Instance.LocaleChanged += OnLocaleChanged;
    }

    public override void _ExitTree()
    {
        SceneRouter.Instance.RouteFailed -= OnRouteFailed;
        SettingsService.Instance.LocaleChanged -= OnLocaleChanged;
    }

    public void Refresh()
    {
        bool hasSave = SaveService.Instance.HasSave();
        ContinueButton.Visible = hasSave;
        ContinueButton.ThemeTypeVariation = "PrimaryButton";
        NewGameButton.ThemeTypeVariation = hasSave ? "Button" : "PrimaryButton";
        var buttons = new List<Control> { ContinueButton, NewGameButton, SettingsButton, QuitButton };
        FocusChain.LinkVertical(FocusChain.Focusable(buttons));
        PrimaryButton().GrabFocus();
    }

    public GameButton PrimaryButton() => ContinueButton.Visible ? ContinueButton : NewGameButton;

    public SettingsScreen OpenSettings() => (SettingsScreen)ScreenStack.Push(SettingsScreenScene.Instantiate<SettingsScreen>());

    private void OnLocaleChanged(string locale) => UpdateVersionLabel();

    private void UpdateVersionLabel()
    {
        VersionLabel.Text = Loc.Format("MAIN_MENU_VERSION", new Dictionary<string, string>
        {
            ["version"] = ProjectSettings.GetSetting("application/config/version").AsString(),
        });
    }

    private void OnNewGame()
    {
        if (!SaveService.Instance.HasSave())
        {
            StartNewCampaign();
            return;
        }
        var dialog = ConfirmDialogScene.Instantiate<ConfirmDialog>();
        dialog.Confirmed += StartNewCampaign;
        ScreenStack.Push(dialog, new ScreenContext
        {
            ["title_key"] = "CONFIRM_NEW_GAME_TITLE",
            ["body_key"] = "CONFIRM_NEW_GAME_BODY",
            ["confirm_key"] = "CONFIRM_NEW_GAME_ACCEPT",
            ["cancel_key"] = "UI_CANCEL",
            ["destructive"] = true,
        });
    }

    private void StartNewCampaign()
    {
        NewGameButton.SetBusy(true);
        GameSession.Instance.StartNewCampaign();
        _ = SceneRouter.Instance.Goto(Routes.Gameplay);
    }

    private void OnContinue()
    {
        ContinueButton.SetBusy(true);
        SaveResult result = SaveService.Instance.LoadGame();
        if (result.IsOk() && GameSession.Instance.LoadSaveData(result.Data))
        {
            _ = SceneRouter.Instance.Goto(Routes.Gameplay);
            return;
        }
        ContinueButton.SetBusy(false);
        ContinueButton.ShowError();
        // A readable save that references missing content is reported as damaged.
        ShowMessage("LOAD_FAILED_TITLE", !result.IsOk() ? result.PlayerMessageKey() : "SAVE_ERROR_CORRUPTED");
    }

    private void OnRouteFailed(StringName route)
    {
        foreach (GameButton button in new[] { NewGameButton, ContinueButton })
        {
            if (button.IsBusy())
            {
                button.SetBusy(false);
                button.ShowError();
            }
        }
        ShowMessage("ROUTE_FAILED_TITLE", "ROUTE_FAILED_BODY");
    }

    private void ShowMessage(string titleKey, string bodyKey)
    {
        var dialog = ConfirmDialogScene.Instantiate<ConfirmDialog>();
        ScreenStack.Push(dialog, new ScreenContext
        {
            ["title_key"] = titleKey,
            ["body_key"] = bodyKey,
            ["confirm_key"] = "UI_OK",
            ["single_action"] = true,
        });
    }

    private void OnQuit() => GetTree().Quit();
}
