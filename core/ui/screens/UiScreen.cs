using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Base for every menu, dialog, and overlay pushed onto a ScreenStack.
///
/// A screen owns its own layout and emits intent signals; it never changes
/// game state directly and never reaches into other screens.
/// </summary>
[GlobalClass]
public partial class UiScreen : Control
{
    /// <summary>Ask the owning ScreenStack to close this screen.</summary>
    [Signal] public delegate void CloseRequestedEventHandler();

    /// <summary>Control that receives focus when the screen opens (primary action).</summary>
    [Export] public Control DefaultFocus { get; set; }
    /// <summary>If true, ui_cancel (Esc / B) closes the screen.</summary>
    [Export] public bool CancelCloses { get; set; } = true;

    /// <summary>Called by ScreenStack right after the screen enters the tree.</summary>
    public virtual void OnOpened(ScreenContext context)
    {
    }

    /// <summary>Called by ScreenStack when the player presses ui_cancel on this screen.</summary>
    public virtual void OnCancel()
    {
        if (CancelCloses)
            EmitSignalCloseRequested();
    }

    public void FocusDefault()
    {
        if (DefaultFocus != null && DefaultFocus.IsVisibleInTree() && DefaultFocus.FocusMode != FocusModeEnum.None)
        {
            DefaultFocus.GrabFocus();
            return;
        }
        Control fallback = FindNextValidFocus();
        if (fallback != null && IsAncestorOf(fallback))
            fallback.GrabFocus();
    }

    public void PlayOpen()
    {
        float duration = UiMotion.TransitionSeconds();
        Modulate = Modulate with { A = duration > 0.0f ? 0.0f : 1.0f };
        if (duration <= 0.0f)
            return;
        CreateTween().TweenProperty(this, "modulate:a", 1.0, duration);
    }
}

/// <summary>
/// Opening parameters handed to UiScreen.OnOpened (title keys, flags, rewards).
/// Values are plain C# objects; read them with Get&lt;T&gt;(key, fallback).
/// </summary>
public class ScreenContext : Dictionary<string, object>
{
    public static readonly ScreenContext Empty = new();

    public T Get<T>(string key, T fallback) => TryGetValue(key, out object value) && value is T typed ? typed : fallback;
}
