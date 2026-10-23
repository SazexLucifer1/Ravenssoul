using Godot;

namespace Utopia;

/// <summary>
/// Regenerates assets/ui/theme/utopia_theme.tres from ui_tokens.tres.
/// Usage: godot --headless res://core/ui/theme/build_theme.tscn
/// </summary>
public partial class BuildTheme : Node
{
    public override void _Ready()
    {
        Theme theme = ThemeBuilder.Build(GD.Load<UiTokens>(ThemeBuilder.TokensPath));
        Error err = ResourceSaver.Save(theme, ThemeBuilder.ThemePath);
        GD.Print($"Theme written to {ThemeBuilder.ThemePath}: {Error.Ok == err}");
        GetTree().Quit(err == Error.Ok ? 0 : 1);
    }
}
