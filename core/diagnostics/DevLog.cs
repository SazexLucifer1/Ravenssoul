using Godot;

namespace Utopia;

/// <summary>
/// Development-only diagnostics. Nothing logged here is ever shown to players.
///
/// Player-facing surfaces must use translation keys (see docs/LOCALIZATION.md).
/// Use <see cref="Error"/> for programmer errors and unrecoverable states; it is
/// routed through GD.PushError so the test runner can detect it.
/// </summary>
public static class DevLog
{
    /// <summary>Set to true by tests that deliberately exercise failure paths.</summary>
    public static bool Muted { get; set; }

    public static void Info(string category, string message)
    {
        if (Muted || !OS.IsDebugBuild())
            return;
        GD.Print($"[{category}] {message}");
    }

    public static void Warn(string category, string message)
    {
        if (Muted)
            return;
        GD.PushWarning($"[{category}] {message}");
    }

    public static void Error(string category, string message)
    {
        if (Muted)
            return;
        GD.PushError($"[{category}] {message}");
    }
}
