using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Storage interface used by SaveService. Implementations move opaque text
/// between a slot id and a storage medium. They know nothing about the game.
///
/// Implementations: JsonFileSaveBackend (disk), MemorySaveBackend (tests).
/// A future platform/cloud backend implements the same five methods.
/// </summary>
public abstract class SaveBackend
{
    /// <summary>Result of the last ReadText() call.</summary>
    public Error LastError { get; protected set; } = Error.Ok;

    public abstract bool Exists(string slotId);

    /// <summary>Returns the stored text. Sets <see cref="LastError"/> to Ok or the failure.</summary>
    public abstract string ReadText(string slotId);

    public abstract Error WriteText(string slotId, string text);

    public abstract Error Delete(string slotId);

    public abstract List<string> ListSlots();
}
