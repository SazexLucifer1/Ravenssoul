using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Utopia;

/// <summary>In-memory backend for tests. <see cref="FailWrites"/> simulates a full disk.</summary>
public class MemorySaveBackend : SaveBackend
{
    public Dictionary<string, string> Slots { get; } = new();
    public bool FailWrites { get; set; }

    public override bool Exists(string slotId) => Slots.ContainsKey(slotId);

    public override string ReadText(string slotId)
    {
        if (!Slots.TryGetValue(slotId, out string text))
        {
            LastError = Error.FileNotFound;
            return "";
        }
        LastError = Error.Ok;
        return text;
    }

    public override Error WriteText(string slotId, string text)
    {
        if (FailWrites)
            return Error.FileCantWrite;
        Slots[slotId] = text;
        return Error.Ok;
    }

    public override Error Delete(string slotId) => Slots.Remove(slotId) ? Error.Ok : Error.FileNotFound;

    public override List<string> ListSlots() => Slots.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToList();
}
