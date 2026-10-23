using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Injects input through Input.ParseInputEvent (the real input path used by
/// players: InputMap, GUI focus, _UnhandledInput, InputMethod tracking).
/// Every "down" is tracked as a hold and is released on "up", on expiry, or by
/// ReleaseAll() (session close, scene change, shutdown).
/// </summary>
public class AutomationInputDriver
{
    public event Action<string> HoldReleased;

    public static readonly IReadOnlyDictionary<string, JoyButton> GamepadButtons = new Dictionary<string, JoyButton>
    {
        ["a"] = JoyButton.A, ["b"] = JoyButton.B, ["x"] = JoyButton.X, ["y"] = JoyButton.Y,
        ["start"] = JoyButton.Start, ["back"] = JoyButton.Back,
        ["dpad_up"] = JoyButton.DpadUp, ["dpad_down"] = JoyButton.DpadDown,
        ["dpad_left"] = JoyButton.DpadLeft, ["dpad_right"] = JoyButton.DpadRight,
        ["left_shoulder"] = JoyButton.LeftShoulder, ["right_shoulder"] = JoyButton.RightShoulder,
    };
    public static readonly IReadOnlyDictionary<string, JoyAxis> GamepadAxes = new Dictionary<string, JoyAxis>
    {
        ["left_x"] = JoyAxis.LeftX, ["left_y"] = JoyAxis.LeftY,
        ["right_x"] = JoyAxis.RightX, ["right_y"] = JoyAxis.RightY,
    };
    public static readonly IReadOnlyDictionary<string, MouseButton> MouseButtons = new Dictionary<string, MouseButton>
    {
        ["left"] = MouseButton.Left, ["right"] = MouseButton.Right, ["middle"] = MouseButton.Middle,
    };

    /// <summary>ReleaseAtMsec -1 = until released.</summary>
    private sealed record Hold(InputEvent Release, long ReleaseAtMsec, ulong Frame);

    private readonly Dictionary<string, Hold> _holds = new();
    private readonly Viewport _viewport;

    public AutomationInputDriver(Viewport viewport)
    {
        _viewport = viewport;
    }

    public List<string> Held() => _holds.Keys.ToList();

    public bool HasHolds() => _holds.Count > 0;

    /// <summary>Returns "" on success or a validation message.</summary>
    public string Key(string name, string mode, int durationMs)
    {
        Key keycode = OS.FindKeycodeFromString(name);
        if (keycode == Godot.Key.None)
            return $"unknown key '{name}'";
        var down = new InputEventKey { Keycode = keycode, PhysicalKeycode = keycode, Pressed = true };
        var up = new InputEventKey { Keycode = keycode, PhysicalKeycode = keycode, Pressed = false };
        Apply($"key:{OS.GetKeycodeString(keycode)}", down, up, mode, durationMs);
        return "";
    }

    public string Action(string name, string mode, int durationMs)
    {
        if (!InputMap.HasAction(name))
            return $"unknown input action '{name}'";
        var down = new InputEventAction { Action = name, Pressed = true };
        var up = new InputEventAction { Action = name, Pressed = false };
        Apply($"action:{name}", down, up, mode, durationMs);
        return "";
    }

    public string GamepadButton(string name, string mode, int durationMs)
    {
        if (!GamepadButtons.TryGetValue(name, out JoyButton button))
            return $"unknown gamepad button '{name}'";
        var down = new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = true };
        var up = new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = false };
        Apply($"pad:{name}", down, up, mode, durationMs);
        return "";
    }

    /// <summary>Axis values persist until set back to 0 (tracked as a hold while non-zero).</summary>
    public string GamepadAxis(string name, float value)
    {
        if (!GamepadAxes.TryGetValue(name, out JoyAxis axis))
            return $"unknown gamepad axis '{name}'";
        Send(new InputEventJoypadMotion { Device = 0, Axis = axis, AxisValue = value });
        string holdId = $"axis:{name}";
        if (Mathf.IsZeroApprox(value))
            _holds.Remove(holdId);
        else
            _holds[holdId] = new Hold(new InputEventJoypadMotion { Device = 0, Axis = axis, AxisValue = 0.0f }, -1, Engine.GetProcessFrames());
        return "";
    }

    public string Mouse(string mode, Vector2 logical, string buttonName)
    {
        Vector2 position = ToWindow(logical);
        if (mode == "move")
        {
            // Relative large enough to register as deliberate movement.
            Send(new InputEventMouseMotion { Position = position, GlobalPosition = position, Relative = new Vector2(8, 0) });
            return "";
        }
        MouseButton button = MouseButtons.TryGetValue(buttonName, out MouseButton found) ? found : MouseButton.Left;
        var down = new InputEventMouseButton { ButtonIndex = button, Position = position, GlobalPosition = position, Pressed = true };
        var up = new InputEventMouseButton { ButtonIndex = button, Position = position, GlobalPosition = position, Pressed = false };
        // Hover first so GUI controls see the pointer like a real cursor.
        Mouse("move", logical, buttonName);
        Apply($"mouse:{buttonName}", down, up, mode == "click" ? "tap" : mode, 0);
        return "";
    }

    public string Touch(int index, string mode, Vector2 logical)
    {
        Vector2 position = ToWindow(logical);
        if (mode == "drag")
        {
            Send(new InputEventScreenDrag { Index = index, Position = position });
            return "";
        }
        var down = new InputEventScreenTouch { Index = index, Position = position, Pressed = true };
        var up = new InputEventScreenTouch { Index = index, Position = position, Pressed = false };
        Apply($"touch:{index}", down, up, mode, 0);
        return "";
    }

    /// <summary>Releases holds whose time is up. Called every frame by the server.</summary>
    public void Tick()
    {
        long now = (long)Time.GetTicksMsec();
        ulong frame = Engine.GetProcessFrames();
        foreach ((string holdId, Hold hold) in _holds.ToList())
            if (hold.ReleaseAtMsec >= 0 && now >= hold.ReleaseAtMsec && frame > hold.Frame)
                Release(holdId);
    }

    public int ReleaseAll()
    {
        int count = _holds.Count;
        foreach (string holdId in _holds.Keys.ToList())
            Release(holdId);
        return count;
    }

    private void Apply(string holdId, InputEvent down, InputEvent up, string mode, int durationMs)
    {
        long now = (long)Time.GetTicksMsec();
        switch (mode)
        {
            case "up":
                if (_holds.ContainsKey(holdId))
                    Release(holdId);
                else
                    Send(up);
                break;
            case "down":
                Send(down);
                _holds[holdId] = new Hold(up, durationMs > 0 ? now + durationMs : -1, Engine.GetProcessFrames());
                break;
            default: // tap: press now, release on a later frame (at least one frame held)
                Send(down);
                _holds[holdId] = new Hold(up, now + durationMs, Engine.GetProcessFrames());
                break;
        }
    }

    private void Release(string holdId)
    {
        Hold hold = _holds[holdId];
        _holds.Remove(holdId);
        Send(hold.Release);
        HoldReleased?.Invoke(holdId);
    }

    private static void Send(InputEvent inputEvent)
    {
        Input.ParseInputEvent(inputEvent);
        Input.FlushBufferedEvents();
    }

    /// <summary>Logical canvas coordinates (1280x720 design space) -> window coordinates.</summary>
    private Vector2 ToWindow(Vector2 logical) => _viewport == null ? logical : _viewport.GetFinalTransform() * logical;
}
