using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Unit.Ui;

public class TestGameButton : TestCase
{
    private GameButton MakeButton() => AddNode(new GameButton { Text = "UI_OK", CustomMinimumSize = new Vector2(200, 52) });

    private static void Press(GameButton button) => button.EmitSignal(BaseButton.SignalName.Pressed);

    public void TestIdleFocusAndDisabledStates()
    {
        GameButton button = MakeButton();
        AssertEq(button.GetState(), GameButton.State.Idle);
        button.GrabFocus();
        AssertEq(button.GetState(), GameButton.State.Focus);
        button.Disabled = true;
        AssertEq(button.GetState(), GameButton.State.Disabled);
    }

    public void TestSelectedStateForToggleButtons()
    {
        GameButton button = MakeButton();
        button.ToggleMode = true;
        button.ButtonPressed = true;
        AssertEq(button.GetState(), GameButton.State.Selected);
    }

    public void TestRepeatedPressesActivateOnceWithinGuard()
    {
        GameButton button = MakeButton();
        int count = 0;
        button.Activated += () => count++;
        for (int i = 0; i < 5; i++)
            Press(button);
        AssertEq(count, 1);
        button.RepeatGuardSeconds = 0.0f;
        Press(button);
        AssertEq(count, 2);
    }

    public void TestBusyBlocksActivationAndRelabels()
    {
        GameButton button = MakeButton();
        button.RepeatGuardSeconds = 0.0f;
        int count = 0;
        button.Activated += () => count++;
        button.SetBusy(true);
        AssertEq(button.GetState(), GameButton.State.Busy);
        AssertEq(button.Text, "UI_BUSY");
        Press(button);
        AssertEq(count, 0);
        button.SetBusy(false);
        AssertEq(button.Text, "UI_OK");
        AssertFalse(button.Disabled);
    }

    public async Task TestCooldownDisablesThenRecovers()
    {
        GameButton button = MakeButton();
        button.StartCooldown(0.1f);
        AssertEq(button.GetState(), GameButton.State.Cooldown);
        AssertTrue(button.Disabled);
        AssertTrue(await WaitUntil(() => button.GetState() != GameButton.State.Cooldown, 2.0));
        AssertFalse(button.Disabled);
    }

    public async Task TestErrorStateIsTemporary()
    {
        GameButton button = MakeButton();
        button.ShowError();
        AssertEq(button.GetState(), GameButton.State.Error);
        AssertTrue(await WaitUntil(() => button.GetState() != GameButton.State.Error, 2.0));
        AssertEq(button.Modulate, Colors.White);
    }
}
