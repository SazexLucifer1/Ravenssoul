using Godot;

namespace Utopia;

/// <summary>
/// One line of dialogue. Text is a translation key; voice/subtitle variants
/// are resolved per locale at playback time.
/// </summary>
[GlobalClass]
public partial class DialogueLine : Resource
{
    [Export] public string SpeakerNameKey { get; set; } = "";
    [Export] public string TextKey { get; set; } = "";
    [Export] public Texture2D Portrait { get; set; }
    /// <summary>Optional localized voice clip id (resolved per locale, see docs/LOCALIZATION.md).</summary>
    [Export] public StringName VoiceId { get; set; } = "";
}
