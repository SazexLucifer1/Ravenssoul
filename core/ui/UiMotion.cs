namespace Utopia;

/// <summary>
/// Shared motion rules for UI. All UI animation goes through these helpers so
/// the reduced-motion preference is honored everywhere.
/// </summary>
public static class UiMotion
{
    private static UiTokens Tokens => UiTokens.Default;

    public static bool Reduced() => SettingsService.Instance != null && SettingsService.Instance.ReducedMotion;

    /// <summary>Seconds for a standard screen transition (0 when motion is reduced).</summary>
    public static float TransitionSeconds() => Reduced() ? 0.0f : Tokens.TransitionSeconds;

    public static float PressSeconds() => Reduced() ? 0.0f : Tokens.PressFeedbackSeconds;
}
