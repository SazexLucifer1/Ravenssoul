using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Settings (Autoload) — player preferences that apply to every screen.
///
/// Why global: language, reduced motion, and text scale must be applied before
/// the first screen is shown and must survive scene changes. Persisted in
/// user://settings.cfg (separate from save games so preferences survive a
/// deleted save).
/// </summary>
public partial class SettingsService : Node
{
    [Signal] public delegate void LocaleChangedEventHandler(string locale);
    [Signal] public delegate void ReducedMotionChangedEventHandler(bool enabled);
    [Signal] public delegate void TextScaleChangedEventHandler(float scale);

    public const string DefaultSettingsPath = "user://settings.cfg";
    public static readonly IReadOnlyList<float> TextScales = new[] { 1.0f, 1.25f, 1.5f };

    /// <summary>The autoload instance (registered as "Settings" in project.godot).</summary>
    public static SettingsService Instance { get; private set; }

    public string Locale { get; set; } = LocaleRegistry.SourceLocale;
    public bool ReducedMotion { get; set; }
    public float TextScale { get; set; } = 1.0f;

    /// <summary>Tests point this at a throwaway file.</summary>
    public string SettingsPath { get; set; } = DefaultSettingsPath;

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
        LoadSettings();
    }

    public void LoadSettings()
    {
        var config = new ConfigFile();
        Error err = config.Load(SettingsPath);
        if (err != Error.Ok && err != Error.FileNotFound)
            DevLog.Warn("settings", $"Could not read settings ({err}); using defaults");
        string defaultLocale = LocaleRegistry.BestMatch(OS.GetLocale());
        Locale = config.GetValue("general", "locale", defaultLocale).AsString();
        if (!LocaleRegistry.IsSupported(Locale))
            Locale = LocaleRegistry.SourceLocale;
        ReducedMotion = config.GetValue("accessibility", "reduced_motion", false).AsBool();
        TextScale = SanitizeScale(config.GetValue("accessibility", "text_scale", 1.0f).AsSingle());
        ApplyAll();
    }

    public Error SaveSettings()
    {
        var config = new ConfigFile();
        config.SetValue("general", "locale", Locale);
        config.SetValue("accessibility", "reduced_motion", ReducedMotion);
        config.SetValue("accessibility", "text_scale", TextScale);
        Error err = config.Save(SettingsPath);
        if (err != Error.Ok)
            DevLog.Error("settings", $"Could not write settings: {err}");
        return err;
    }

    public void SetLocale(string value)
    {
        if (!LocaleRegistry.IsSupported(value) || value == Locale)
            return;
        Locale = value;
        ApplyLocale();
        EmitSignalLocaleChanged(Locale);
        SaveSettings();
    }

    public void SetReducedMotion(bool value)
    {
        if (value == ReducedMotion)
            return;
        ReducedMotion = value;
        EmitSignalReducedMotionChanged(value);
        SaveSettings();
    }

    public void SetTextScale(float value)
    {
        float sanitized = SanitizeScale(value);
        if (Mathf.IsEqualApprox(sanitized, TextScale))
            return;
        TextScale = sanitized;
        ThemeScaler.Apply(ThemeDB.GetProjectTheme(), TextScale);
        EmitSignalTextScaleChanged(TextScale);
        SaveSettings();
    }

    private void ApplyAll()
    {
        ApplyLocale();
        ThemeScaler.Apply(ThemeDB.GetProjectTheme(), TextScale);
    }

    private void ApplyLocale()
    {
        TranslationServer.SetLocale(Locale);
        // Controls default to "inherited", so the whole UI mirrors automatically
        // when the application locale is right-to-left.
        GetTree().Root.SetLayoutDirection(Window.LayoutDirection.ApplicationLocale);
    }

    private static float SanitizeScale(float value)
    {
        float best = TextScales[0];
        foreach (float candidate in TextScales)
            if (Mathf.Abs(candidate - value) < Mathf.Abs(best - value))
                best = candidate;
        return best;
    }
}
