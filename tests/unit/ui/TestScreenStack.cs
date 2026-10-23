using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Unit.Ui;

public class TestScreenStack : TestCase
{
    private (ScreenStack Stack, Control Root, GameButton RootButton) StackWithRoot()
    {
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var rootButton = new GameButton { Text = "UI_BACK" };
        root.AddChild(rootButton);
        AddNode(root);
        ScreenStack stack = AddNode(new ScreenStack { UnderlyingRoot = root });
        rootButton.GrabFocus();
        return (stack, root, rootButton);
    }

    public void TestPushFocusesScreenAndBlocksUnderlying()
    {
        (ScreenStack stack, Control root, _) = StackWithRoot();
        var screen = new FakeScreen();
        stack.Push(screen);
        AssertTrue(screen.Button.HasFocus());
        AssertEq(root.FocusBehaviorRecursive, Control.FocusBehaviorRecursiveEnum.Disabled);
        AssertEq(root.MouseBehaviorRecursive, Control.MouseBehaviorRecursiveEnum.Disabled);
    }

    public void TestPopRestoresPreviousFocus()
    {
        (ScreenStack stack, Control root, GameButton rootButton) = StackWithRoot();
        stack.Push(new FakeScreen());
        stack.Pop();
        AssertTrue(rootButton.HasFocus());
        AssertTrue(stack.IsEmpty());
        AssertEq(root.FocusBehaviorRecursive, Control.FocusBehaviorRecursiveEnum.Inherited);
    }

    public void TestNestedModalsOnlyTopIsInteractive()
    {
        (ScreenStack stack, _, _) = StackWithRoot();
        var first = new FakeScreen();
        var second = new FakeScreen();
        stack.Push(first);
        stack.Push(second);
        AssertEq(stack.Depth(), 2);
        AssertEq(first.FocusBehaviorRecursive, Control.FocusBehaviorRecursiveEnum.Disabled);
        AssertTrue(second.Button.HasFocus());
        stack.Pop();
        AssertTrue(first.Button.HasFocus(), "focus returns to the screen below");
        AssertEq(first.FocusBehaviorRecursive, Control.FocusBehaviorRecursiveEnum.Inherited);
    }

    public async Task TestUiCancelClosesTopScreenOnly()
    {
        (ScreenStack stack, _, _) = StackWithRoot();
        stack.Push(new FakeScreen());
        stack.Push(new FakeScreen());
        PressAction("ui_cancel");
        await WaitFrames(1);
        AssertEq(stack.Depth(), 1);
    }

    public async Task TestScreenCanRefuseCancel()
    {
        (ScreenStack stack, _, _) = StackWithRoot();
        stack.Push(new FakeScreen { CancelCloses = false });
        PressAction("ui_cancel");
        await WaitFrames(1);
        AssertEq(stack.Depth(), 1);
    }

    public void TestEmptiedSignal()
    {
        (ScreenStack stack, _, _) = StackWithRoot();
        int emptied = 0;
        stack.Emptied += () => emptied++;
        stack.Push(new FakeScreen());
        stack.Pop();
        stack.Pop();
        AssertEq(emptied, 1);
    }
}
