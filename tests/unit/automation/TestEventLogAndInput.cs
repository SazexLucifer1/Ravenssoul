using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Utopia.Automation;

namespace Utopia.Tests.Unit.Automation;

public class TestEventLogAndInput : TestCase
{
    public override Task AfterEach()
    {
        Input.ActionRelease("ui_left");
        return Task.CompletedTask;
    }

    public void TestEventSequenceCapacityAndFilters()
    {
        var log = new AutomationEventLog(3);
        for (int i = 0; i < 5; i++)
            log.Emit(i % 2 == 0 ? "a" : "b", new JsonObject { ["i"] = i });
        AssertEq(log.LastSeq, 5);
        AssertEq(log.Since(0).Select(e => JsonNodes.Long(e["seq"])).ToList(), new long[] { 3, 4, 5 }, "ring buffer keeps the newest");
        AssertEq(log.Since(3, new[] { "a" }).Count, 1);
        AssertTrue(AutomationEventLog.Matches(log.Since(4)[0], "a", new JsonObject { ["i"] = 4 }));
        AssertFalse(AutomationEventLog.Matches(log.Since(4)[0], "a", new JsonObject { ["i"] = 5 }));
    }

    public void TestLogCaptureRedactsPaths()
    {
        var capture = new AutomationLogCapture();
        string text = capture.Redact($"failed at {OS.GetUserDataDir()}/saves/x.json");
        AssertFalse(text.Contains(OS.GetUserDataDir()));
        AssertTrue(text.Contains("<user_data>"));
    }

    public void TestKeyHoldAndReleaseAll()
    {
        var driver = new AutomationInputDriver(Tree.Root);
        AssertEq(driver.Key("Left", "down", 0), "");
        AssertTrue(Input.IsKeyPressed(Key.Left), "real Input state reflects the hold");
        AssertTrue(Input.IsActionPressed("ui_left"), "InputMap action sees the key");
        AssertEq(driver.Held(), new[] { "key:Left" });
        AssertEq(driver.ReleaseAll(), 1);
        AssertFalse(Input.IsKeyPressed(Key.Left));
        AssertFalse(driver.HasHolds());
    }

    public async Task TestTapReleasesOnALaterFrame()
    {
        var driver = new AutomationInputDriver(Tree.Root);
        driver.Action("ui_left", "tap", 0);
        AssertTrue(Input.IsActionPressed("ui_left"));
        driver.Tick();
        AssertTrue(Input.IsActionPressed("ui_left"), "held for at least one frame");
        await WaitFrames(1);
        driver.Tick();
        AssertFalse(Input.IsActionPressed("ui_left"));
    }

    public async Task TestTimedHoldExpires()
    {
        var driver = new AutomationInputDriver(Tree.Root);
        driver.GamepadButton("a", "down", 50);
        await WaitSeconds(0.08);
        driver.Tick();
        AssertFalse(driver.HasHolds());
    }

    public void TestAxisHoldReturnsToNeutral()
    {
        var driver = new AutomationInputDriver(Tree.Root);
        driver.GamepadAxis("left_x", -1.0f);
        AssertEq(driver.Held(), new[] { "axis:left_x" });
        AssertTrue(Input.GetJoyAxis(0, JoyAxis.LeftX) < -0.5f);
        driver.ReleaseAll();
        AssertTrue(Mathf.IsZeroApprox(Input.GetJoyAxis(0, JoyAxis.LeftX)));
    }

    public void TestInvalidInputsAreReported()
    {
        var driver = new AutomationInputDriver(Tree.Root);
        AssertNe(driver.Key("NotAKey", "tap", 0), "");
        AssertNe(driver.Action("no_such_action", "tap", 0), "");
        AssertNe(driver.GamepadButton("turbo", "tap", 0), "");
        AssertNe(driver.GamepadAxis("z", 1.0f), "");
        AssertFalse(driver.HasHolds());
    }
}
