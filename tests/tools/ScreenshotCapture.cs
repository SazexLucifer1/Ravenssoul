using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Tools;

/// <summary>
/// Captures representative screenshots per locale for visual review/CI.
/// Needs a real renderer (not --headless). On Linux CI use Xvfb:
///   xvfb-run -a godot --path . --rendering-driver opengl3 \
///     res://tests/tools/screenshot_capture.tscn -- --out=/abs/path/to/dir
/// Variants: en, de, pseudo (expanded accents), rtl (pseudo + fake bidi + RTL layout).
/// </summary>
public partial class ScreenshotCapture : Node
{
    private static readonly string[] Variants = { "en", "de", "pseudo", "rtl" };

    private string _outDir = "user://screenshots";

    public override async void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--out="))
                _outDir = arg["--out=".Length..];
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        SettingsService.Instance.SettingsPath = "user://screenshot_settings.cfg";
        SaveService.Instance.Backend = new MemorySaveBackend();
        SettingsService.Instance.ReducedMotion = true;
        var placeholder = new Node();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, placeholder);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().CurrentScene = placeholder;
        foreach (string variant in Variants)
            await CaptureVariant(variant);
        await CaptureExtraStates();
        GetTree().Quit();
    }

    /// <summary>English-only states for the visual-state matrix (docs/VISUAL_QUALITY_RUBRIC.md).</summary>
    private async Task CaptureExtraStates()
    {
        TranslationServer.SetLocale("en");
        TranslationServer.PseudolocalizationEnabled = false;
        GetTree().Root.SetLayoutDirection(Window.LayoutDirection.ApplicationLocale);
        GameSession.Instance.StartNewCampaign();
        await SceneRouter.Instance.Goto(Routes.Gameplay);
        var gameplay = (Gameplay)GetTree().CurrentScene;
        await Shot("state", "battle_start");
        gameplay.OpenPauseMenu().RequestAbandon();
        await Shot("state", "confirm_abandon");
        gameplay.ScreenStack.Clear();
        gameplay.PlayerUnit.Health.ApplyDamage(999);
        await Shot("state", "defeat");
        ((MemorySaveBackend)SaveService.Instance.Backend).Slots[SaveService.DefaultSlot] = "{damaged";
        await SceneRouter.Instance.Goto(Routes.MainMenu);
        ((MainMenu)GetTree().CurrentScene).ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
        await Shot("state", "load_failed");
    }

    private async Task CaptureVariant(string variant)
    {
        TranslationServer.SetLocale(variant == "de" ? "de" : "en");
        bool pseudo = variant is "pseudo" or "rtl";
        ProjectSettings.SetSetting("internationalization/pseudolocalization/fake_bidi", variant == "rtl");
        TranslationServer.PseudolocalizationEnabled = pseudo;
        TranslationServer.ReloadPseudolocalization();
        GetTree().Root.SetLayoutDirection(variant == "rtl" ? Window.LayoutDirection.Rtl : Window.LayoutDirection.ApplicationLocale);

        await SceneRouter.Instance.Goto(Routes.MainMenu);
        await Shot(variant, "main_menu");
        ((MainMenu)GetTree().CurrentScene).OpenSettings();
        await Shot(variant, "settings");

        GameSession.Instance.StartNewCampaign();
        await SceneRouter.Instance.Goto(Routes.Gameplay);
        var gameplay = (Gameplay)GetTree().CurrentScene;
        for (int i = 0; i < 4; i++)
            gameplay.Controller.TryStep(Vector2I.Right);
        gameplay.Controller.TryStep(Vector2I.Up);
        gameplay.Controller.TryStep(Vector2I.Up);
        await Shot(variant, "gameplay");
        gameplay.OpenPauseMenu();
        await Shot(variant, "pause");
        gameplay.ScreenStack.Clear();
        gameplay.Finish(true);
        await Shot(variant, "victory");
    }

    private async Task Shot(string variant, string name)
    {
        for (int i = 0; i < 6; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Image image = GetViewport().GetTexture().GetImage();
        string path = _outDir.PathJoin($"{variant}_{name}.png");
        image.SavePng(path);
        GD.Print("saved ", path);
    }
}
