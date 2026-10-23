using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Turns an input action into a short, player-readable glyph label for the
/// active input method (e.g. "Esc" or "B"). Text glyphs for now; swap to icon
/// textures once the art pass delivers them (see docs/UI_ARCHITECTURE.md).
/// </summary>
public static class InputGlyphs
{
    public static readonly IReadOnlyDictionary<JoyButton, string> JoyButtonLabels = new Dictionary<JoyButton, string>
    {
        [JoyButton.A] = "A",
        [JoyButton.B] = "B",
        [JoyButton.X] = "X",
        [JoyButton.Y] = "Y",
        [JoyButton.Start] = "Start",
        [JoyButton.Back] = "Back",
        [JoyButton.DpadUp] = "D-Pad ↑",
        [JoyButton.DpadDown] = "D-Pad ↓",
        [JoyButton.DpadLeft] = "D-Pad ←",
        [JoyButton.DpadRight] = "D-Pad →",
        [JoyButton.LeftShoulder] = "LB",
        [JoyButton.RightShoulder] = "RB",
    };

    public static string GlyphFor(StringName action, InputMethodService.Method method)
    {
        if (!InputMap.HasAction(action))
            return "";
        foreach (InputEvent inputEvent in InputMap.ActionGetEvents(action))
        {
            if (method == InputMethodService.Method.Gamepad && inputEvent is InputEventJoypadButton joy)
                return JoyButtonLabels.TryGetValue(joy.ButtonIndex, out string label) ? label : $"Pad {(int)joy.ButtonIndex}";
            if (method == InputMethodService.Method.KeyboardMouse && inputEvent is InputEventKey key)
            {
                Key keycode = key.Keycode;
                if (keycode == Key.None)
                    keycode = DisplayServer.KeyboardGetKeycodeFromPhysical(key.PhysicalKeycode);
                return OS.GetKeycodeString(keycode);
            }
        }
        return "";
    }
}
