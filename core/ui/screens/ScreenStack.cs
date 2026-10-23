using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Narrow, scene-owned navigation service for modal screens (menus, dialogs).
///
/// Each top-level scene owns its own ScreenStack. Only the top screen is
/// interactive; everything beneath it (including <see cref="UnderlyingRoot"/>) has
/// focus and mouse input disabled. Closing a screen restores the focus that was
/// active before it opened, so controller players never lose their place.
/// </summary>
[GlobalClass]
public partial class ScreenStack : Control
{
    [Signal] public delegate void ScreenOpenedEventHandler(UiScreen screen);
    [Signal] public delegate void ScreenClosedEventHandler(UiScreen screen);
    [Signal] public delegate void EmptiedEventHandler();

    /// <summary>
    /// Content behind the stack (e.g. the main menu) that must be blocked while a
    /// screen is open.
    /// </summary>
    [Export] public Control UnderlyingRoot { get; set; }

    private readonly record struct Entry(UiScreen Screen, Control PreviousFocus);

    private readonly List<Entry> _entries = new();
    private ColorRect _scrim;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        // Created here (not at member init) so an instance that never enters the
        // tree doesn't leak an orphan node.
        _scrim = new ColorRect { Color = UiTokens.Default.Scrim, MouseFilter = MouseFilterEnum.Stop, Visible = false };
        _scrim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_scrim, false, InternalMode.Front);
    }

    public bool IsEmpty() => _entries.Count == 0;

    public int Depth() => _entries.Count;

    public UiScreen Top() => _entries.Count == 0 ? null : _entries[^1].Screen;

    public UiScreen Push(UiScreen screen, ScreenContext context = null)
    {
        Viewport viewport = GetViewport();
        Control previousFocus = viewport?.GuiGetFocusOwner();
        Control below = !IsEmpty() ? Top() : UnderlyingRoot;
        if (below != null)
            SetInteractive(below, false);
        _entries.Add(new Entry(screen, previousFocus));
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(screen);
        screen.CloseRequested += () => OnCloseRequested(screen);
        _scrim.Visible = true;
        screen.OnOpened(context ?? ScreenContext.Empty);
        screen.FocusDefault();
        screen.PlayOpen();
        EmitSignalScreenOpened(screen);
        return screen;
    }

    /// <summary>Closes the top screen. Returns the closed screen (already queued for free).</summary>
    public UiScreen Pop()
    {
        if (_entries.Count == 0)
            return null;
        Entry entry = _entries[^1];
        _entries.RemoveAt(_entries.Count - 1);
        UiScreen screen = entry.Screen;
        SetInteractive(screen, false);
        RemoveChild(screen);
        screen.QueueFree();

        Control revealed = !IsEmpty() ? Top() : UnderlyingRoot;
        if (revealed != null)
            SetInteractive(revealed, true);
        _scrim.Visible = !IsEmpty();

        Control previous = entry.PreviousFocus;
        if (IsInstanceValid(previous) && previous.IsVisibleInTree())
            previous.GrabFocus();
        else
            Top()?.FocusDefault();
        EmitSignalScreenClosed(screen);
        if (IsEmpty())
            EmitSignalEmptied();
        return screen;
    }

    public void Clear()
    {
        while (!IsEmpty())
            Pop();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (IsEmpty())
            return;
        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Top().OnCancel();
        }
    }

    private void OnCloseRequested(UiScreen screen)
    {
        if (Top() == screen)
            Pop();
    }

    private static void SetInteractive(Control control, bool enabled)
    {
        control.FocusBehaviorRecursive = enabled ? FocusBehaviorRecursiveEnum.Inherited : FocusBehaviorRecursiveEnum.Disabled;
        control.MouseBehaviorRecursive = enabled ? MouseBehaviorRecursiveEnum.Inherited : MouseBehaviorRecursiveEnum.Disabled;
    }
}
