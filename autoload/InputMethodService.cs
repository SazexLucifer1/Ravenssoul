using Godot;

namespace Utopia;

/// <summary>
/// InputMethod (Autoload) — tracks whether the player last used keyboard/mouse
/// or a gamepad, so prompts show matching glyphs and focus indicators appear
/// for controller navigation.
///
/// Why global: input device is a property of the whole session, every prompt
/// needs it, and it must observe input before any screen consumes it.
/// </summary>
[GlobalClass]
public partial class InputMethodService : Node
{
    public enum Method { KeyboardMouse, Gamepad }

    [Signal] public delegate void MethodChangedEventHandler(Method method);

    public const float AxisThreshold = 0.5f;
    public const float MouseMoveThreshold = 4.0f;

    /// <summary>The autoload instance (registered as "InputMethod" in project.godot).</summary>
    public static InputMethodService Instance { get; private set; }

    public Method Current { get; private set; } = Method.KeyboardMouse;

    public override void _EnterTree()
    {
        Instance ??= this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Input(InputEvent inputEvent) => HandleEvent(inputEvent);

    /// <summary>Separate from _Input so tests can feed synthetic events.</summary>
    public void HandleEvent(InputEvent inputEvent)
    {
        Method detected = Current;
        switch (inputEvent)
        {
            case InputEventJoypadButton:
                detected = Method.Gamepad;
                break;
            case InputEventJoypadMotion motion:
                if (Mathf.Abs(motion.AxisValue) >= AxisThreshold)
                    detected = Method.Gamepad;
                break;
            case InputEventKey or InputEventMouseButton:
                detected = Method.KeyboardMouse;
                break;
            case InputEventMouseMotion mouse:
                if (mouse.Relative.Length() >= MouseMoveThreshold)
                    detected = Method.KeyboardMouse;
                break;
        }
        if (detected != Current)
        {
            Current = detected;
            EmitSignalMethodChanged(Current);
        }
    }
}
