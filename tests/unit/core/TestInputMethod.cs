using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Utopia.Tests.Unit.Core;

public class TestInputMethod : TestCase
{
    public void TestSwitchesToGamepadAndBack()
    {
        InputMethodService service = AddNode(new InputMethodService());
        var changes = new List<InputMethodService.Method>();
        service.MethodChanged += method => changes.Add(method);
        service.HandleEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true });
        AssertEq(service.Current, InputMethodService.Method.Gamepad);
        service.HandleEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
        AssertEq(service.Current, InputMethodService.Method.KeyboardMouse);
        AssertEq(changes.Count, 2);
    }

    public void TestStickNoiseAndTinyMouseJitterDoNotSwitch()
    {
        InputMethodService service = AddNode(new InputMethodService());
        service.HandleEvent(new InputEventJoypadMotion { AxisValue = 0.1f });
        AssertEq(service.Current, InputMethodService.Method.KeyboardMouse);
        service.HandleEvent(new InputEventJoypadButton { Pressed = true });
        service.HandleEvent(new InputEventMouseMotion { Relative = new Vector2(1, 0) });
        AssertEq(service.Current, InputMethodService.Method.Gamepad);
    }

    public void TestGlyphsFollowInputMethod()
    {
        AssertEq(InputGlyphs.GlyphFor("ui_cancel", InputMethodService.Method.KeyboardMouse), "Escape");
        AssertEq(InputGlyphs.GlyphFor("ui_cancel", InputMethodService.Method.Gamepad), "B");
        AssertEq(InputGlyphs.GlyphFor("pause", InputMethodService.Method.Gamepad), "Start");
        AssertEq(InputGlyphs.GlyphFor("no_such_action", InputMethodService.Method.Gamepad), "");
    }

    public void TestMenuActionsAreReachableFromEveryInputMethod()
    {
        // Godot's defaults leave ui_accept/ui_cancel without gamepad bindings; the
        // project overrides them so controller players never hit a dead end.
        foreach (string action in new[] { "ui_accept", "ui_cancel", "pause", "ui_up", "ui_down", "ui_left", "ui_right" })
        {
            AssertNe(InputGlyphs.GlyphFor(action, InputMethodService.Method.KeyboardMouse), "", $"{action} keyboard");
            bool hasPad = InputMap.ActionGetEvents(action).Any(e => e is InputEventJoypadButton or InputEventJoypadMotion);
            AssertTrue(hasPad, $"{action} gamepad");
        }
    }
}
