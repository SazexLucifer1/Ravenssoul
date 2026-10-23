using Godot;

namespace Utopia;

/// <summary>
/// Ordered dialogue lines for one conversation. Branching is out of scope for
/// the first playable (single soul-energy threshold check only).
/// </summary>
[GlobalClass]
public partial class DialogueSequence : Resource
{
    [Export] public StringName Id { get; set; } = "";
    [Export] public Godot.Collections.Array<DialogueLine> Lines { get; set; } = new();
}
