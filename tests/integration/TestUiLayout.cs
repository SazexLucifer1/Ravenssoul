using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Integration;

/// <summary>
/// Menus at supported resolutions, RTL, long (pseudolocalized) text, large
/// text, and controller navigation without dead ends. Screens are laid out in
/// a SubViewport of the target size so no real window is required.
/// </summary>
public class TestUiLayout : TestCase
{
    private static readonly Vector2I[] Resolutions =
    {
        new(1280, 720), new(1366, 768), new(1920, 1080),
        new(2560, 1440), new(2560, 1080), new(1280, 1024), new(1024, 768),
    };
    public static readonly string[] Screens =
    {
        "res://scenes/main_menu/main_menu.tscn",
        "res://scenes/menus/pause/pause_menu.tscn",
        "res://scenes/menus/settings/settings_screen.tscn",
        "res://core/ui/screens/confirm_dialog.tscn",
        "res://scenes/menus/mission_result/mission_result_screen.tscn",
    };
    private const float MinTarget = 44.0f;

    private static ScreenContext VictoryWithGold() => new() { ["victory"] = true, ["rewards"] = new Dictionary<StringName, int> { ["gold"] = 50 } };

    public override Task AfterEach()
    {
        TranslationServer.PseudolocalizationEnabled = false;
        ThemeScaler.Apply(ThemeDB.GetProjectTheme(), 1.0f);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Mirrors the project's canvas_items/expand stretch: logical height stays
    /// 720, logical width grows with wider aspect ratios.
    /// </summary>
    private static Vector2I LogicalSize(Vector2I resolution)
    {
        float scale = Math.Min(resolution.X / 1280.0f, resolution.Y / 720.0f);
        return new Vector2I(Mathf.RoundToInt(resolution.X / scale), Mathf.RoundToInt(resolution.Y / scale));
    }

    private async Task<Control> Layout(string scenePath, Vector2I size, ScreenContext context = null)
    {
        SubViewport viewport = AddNode(new SubViewport { Size = size, GuiDisableInput = false });
        var screen = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        viewport.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        (screen as UiScreen)?.OnOpened(context ?? new ScreenContext());
        await WaitFrames(3);
        return screen;
    }

    private static List<Control> Interactive(Node root) =>
        root.FindChildren("*", "BaseButton", true, false).Cast<Control>().Where(c => c.IsVisibleInTree()).ToList();

    private void AssertFits(Control screen, Vector2I size, string label)
    {
        var bounds = new Rect2(Vector2.Zero, size);
        foreach (Control control in Interactive(screen))
        {
            Rect2 rect = control.GetGlobalRect();
            AssertTrue(bounds.Encloses(rect), $"{label}: {control.Name} at {rect} outside {bounds}");
            AssertTrue(rect.Size.Y >= MinTarget, $"{label}: {control.Name} is {rect.Size.Y:F0}px tall");
        }
        foreach (Label textLabel in screen.FindChildren("*", "Label", true, false).Cast<Label>())
            if (textLabel.IsVisibleInTree() && textLabel.Text.Length > 0)
                AssertTrue(bounds.Encloses(textLabel.GetGlobalRect()), $"{label}: label {textLabel.Name} clipped");
    }

    public async Task TestScreensFitEverySupportedResolution()
    {
        foreach (Vector2I resolution in Resolutions)
        {
            Vector2I logical = LogicalSize(resolution);
            foreach (string path in Screens)
            {
                Control screen = await Layout(path, logical, VictoryWithGold());
                AssertFits(screen, logical, $"{path.GetFile()} @ {resolution}");
                FreeOwnedNodes();
            }
        }
    }

    public async Task TestLongPseudolocalizedTextStillFits()
    {
        TranslationServer.PseudolocalizationEnabled = true;
        TranslationServer.ReloadPseudolocalization();
        foreach (string path in Screens)
        {
            Control screen = await Layout(path, new Vector2I(1280, 720), new ScreenContext { ["victory"] = false });
            AssertFits(screen, new Vector2I(1280, 720), "pseudo " + path.GetFile());
            FreeOwnedNodes();
        }
    }

    public async Task TestGermanTextFits()
    {
        TranslationServer.SetLocale("de");
        foreach (string path in Screens)
        {
            Control screen = await Layout(path, new Vector2I(1280, 720), VictoryWithGold());
            AssertFits(screen, new Vector2I(1280, 720), "de " + path.GetFile());
            FreeOwnedNodes();
        }
    }

    public async Task TestLargestTextSizeFits()
    {
        ThemeScaler.Apply(ThemeDB.GetProjectTheme(), SettingsService.TextScales[^1]);
        foreach (string path in Screens)
        {
            Control screen = await Layout(path, new Vector2I(1280, 720), new ScreenContext { ["victory"] = true });
            AssertFits(screen, new Vector2I(1280, 720), "150% text " + path.GetFile());
            FreeOwnedNodes();
        }
    }

    public async Task TestRtlLayoutMirrorsMainMenu()
    {
        var screen = (MainMenu)await Layout("res://scenes/main_menu/main_menu.tscn", new Vector2I(1280, 720));
        Control button = screen.NewGameButton;
        float ltrX = button.GetGlobalRect().Position.X;
        screen.LayoutDirection = Control.LayoutDirectionEnum.Rtl;
        await WaitFrames(3);
        AssertTrue(button.IsLayoutRtl());
        AssertTrue(button.GetGlobalRect().Position.X > ltrX, "menu column moves to the right edge");
        AssertFits(screen, new Vector2I(1280, 720), "rtl main menu");
    }

    public async Task TestHorizontalFocusFollowsVisualOrderInLtrAndRtl()
    {
        foreach (Control.LayoutDirectionEnum direction in new[] { Control.LayoutDirectionEnum.Ltr, Control.LayoutDirectionEnum.Rtl })
        {
            var dialog = (ConfirmDialog)await Layout("res://core/ui/screens/confirm_dialog.tscn", new Vector2I(1280, 720));
            dialog.LayoutDirection = direction;
            dialog.OnOpened(new ScreenContext()); // relinks focus for the current direction
            await WaitFrames(2);
            Control a = dialog.CancelButton;
            Control b = dialog.ConfirmButton;
            Control left = a.GetGlobalRect().Position.X < b.GetGlobalRect().Position.X ? a : b;
            Control right = left == a ? b : a;
            AssertEq(left.GetNode(left.FocusNeighborRight), right, $"direction {direction}: left -> right");
            AssertEq(right.GetNode(right.FocusNeighborLeft), left, $"direction {direction}: right -> left");
            FreeOwnedNodes();
        }
    }

    public async Task TestNoFocusDeadEnds()
    {
        var cases = new (string Path, ScreenContext Context)[]
        {
            ("res://scenes/main_menu/main_menu.tscn", new ScreenContext()),
            ("res://scenes/menus/pause/pause_menu.tscn", new ScreenContext()),
            ("res://scenes/menus/settings/settings_screen.tscn", new ScreenContext()),
            ("res://core/ui/screens/confirm_dialog.tscn", new ScreenContext()),
            ("res://scenes/menus/mission_result/mission_result_screen.tscn", new ScreenContext { ["victory"] = false }),
        };
        foreach ((string path, ScreenContext context) in cases)
        {
            Control screen = await Layout(path, new Vector2I(1280, 720), context);
            List<Control> controls = Interactive(screen);
            AssertTrue(controls.Count > 0, $"{path} has interactive controls");
            foreach (Control control in controls)
            {
                foreach ((string side, NodePath nodePath) in new[] { ("focus_next", control.FocusNext), ("focus_previous", control.FocusPrevious) })
                {
                    var target = nodePath.IsEmpty ? null : control.GetNodeOrNull<Control>(nodePath);
                    AssertTrue(target != null && target.IsVisibleInTree(), $"{path.GetFile()}: {control.Name} has no visible {side}");
                }
            }
            FreeOwnedNodes();
        }
    }

    public async Task TestEveryScreenHasOnePrimaryAction()
    {
        foreach (string path in new[] { "res://scenes/menus/pause/pause_menu.tscn", "res://scenes/menus/settings/settings_screen.tscn" })
        {
            Control screen = await Layout(path, new Vector2I(1280, 720));
            int primaries = Interactive(screen).Count(c => c.ThemeTypeVariation == "PrimaryButton");
            AssertEq(primaries, 1, path.GetFile());
            FreeOwnedNodes();
        }
    }
}
