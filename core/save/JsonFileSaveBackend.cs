using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Stores each slot as &lt;directory&gt;/&lt;slot_id&gt;.json.
///
/// Writes are atomic: the new text goes to &lt;slot&gt;.json.tmp, the previous file
/// is kept as &lt;slot&gt;.json.bak, then the temp file replaces the slot. A crash
/// mid-write therefore never destroys the last good save.
/// </summary>
public class JsonFileSaveBackend : SaveBackend
{
    public const string Extension = ".json";

    public string Directory { get; }

    public JsonFileSaveBackend(string directory = "user://saves")
    {
        Directory = directory;
    }

    public override bool Exists(string slotId) => FileAccess.FileExists(PathOf(slotId));

    public override string ReadText(string slotId)
    {
        using FileAccess file = FileAccess.Open(PathOf(slotId), FileAccess.ModeFlags.Read);
        if (file == null)
        {
            LastError = FileAccess.GetOpenError();
            return "";
        }
        LastError = Error.Ok;
        return file.GetAsText();
    }

    public override Error WriteText(string slotId, string text)
    {
        Error err = DirAccess.MakeDirRecursiveAbsolute(Directory);
        if (err != Error.Ok)
            return err;
        string finalPath = PathOf(slotId);
        string tempPath = finalPath + ".tmp";
        using (FileAccess file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
        {
            if (file == null)
                return FileAccess.GetOpenError();
            file.StoreString(text);
            file.Flush();
            err = file.GetError();
        }
        if (err != Error.Ok)
        {
            DirAccess.RemoveAbsolute(tempPath);
            return err;
        }
        if (FileAccess.FileExists(finalPath))
        {
            DirAccess.CopyAbsolute(finalPath, finalPath + ".bak");
            DirAccess.RemoveAbsolute(finalPath);
        }
        return DirAccess.RenameAbsolute(tempPath, finalPath);
    }

    public override Error Delete(string slotId)
    {
        string path = PathOf(slotId);
        if (!FileAccess.FileExists(path))
            return Error.FileNotFound;
        DirAccess.RemoveAbsolute(path + ".bak");
        return DirAccess.RemoveAbsolute(path);
    }

    public override List<string> ListSlots()
    {
        var slots = new List<string>();
        foreach (string fileName in DirAccess.GetFilesAt(Directory))
            if (fileName.EndsWith(Extension))
                slots.Add(fileName[..^Extension.Length]);
        slots.Sort(System.StringComparer.Ordinal);
        return slots;
    }

    private string PathOf(string slotId) => Directory.PathJoin(slotId + Extension);
}
