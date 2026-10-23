using Godot;

namespace Utopia.Tests.Unit.Ui;

/// <summary>Minimal UiScreen with one focusable button (ScreenStack tests).</summary>
public partial class FakeScreen : UiScreen
{
    public GameButton Button { get; } = new() { Text = "UI_OK" };

    public FakeScreen()
    {
        AddChild(Button);
        DefaultFocus = Button;
    }
}
