using Godot;

namespace Utopia.Tests.Unit.Ui;

public class TestSafeArea : TestCase
{
    public void TestFullViewportHasNoInsets() =>
        AssertEq(SafeAreaContainer.ComputeInsets(new Vector2(1280, 720), new Rect2(0, 0, 1280, 720)), new SafeAreaContainer.Insets(0, 0, 0, 0));

    public void TestNotchAndRoundedCornerInsets() =>
        AssertEq(SafeAreaContainer.ComputeInsets(new Vector2(1280, 720), new Rect2(44, 0, 1280 - 44 - 30, 700)), new SafeAreaContainer.Insets(44, 0, 30, 20));

    public void TestContainerAppliesInsetsOnTopOfBaseMargin()
    {
        SafeAreaContainer container = AddNode(new SafeAreaContainer { BaseMargin = 10 });
        Vector2 size = container.GetViewportRect().Size;
        container.OverrideSafeRect = new Rect2(40, 0, size.X - 40, size.Y);
        AssertEq(container.GetThemeConstant("margin_left"), 50);
        AssertEq(container.GetThemeConstant("margin_right"), 10);
    }
}
