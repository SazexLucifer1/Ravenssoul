using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Godot;

namespace Utopia;

/// <summary>
/// SaveService (Autoload) — the only code that reads or writes save games.
///
/// Why global: saving is triggered from many scenes (mission results, base,
/// menus), and the backend/serialization rules must be identical everywhere.
/// It knows nothing about game rules: callers hand it a JSON object payload of
/// plain, language-independent data. See docs/SAVE_SYSTEM.md.
/// </summary>
public partial class SaveService : Node
{
    [Signal] public delegate void SaveFinishedEventHandler(string slotId, SaveResult result);
    [Signal] public delegate void LoadFinishedEventHandler(string slotId, SaveResult result);

    public const int SchemaVersion = 1;
    public const string DefaultSlot = "campaign_1";
    public const string SlotPattern = "^[a-z0-9_]{1,32}$";

    private static readonly Regex SlotRegex = new(SlotPattern);

    /// <summary>The autoload instance (registered as "SaveService" in project.godot).</summary>
    public static SaveService Instance { get; private set; }

    public SaveBackend Backend { get; set; } = new JsonFileSaveBackend();

    public override void _EnterTree()
    {
        Instance ??= this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public static bool IsValidSlot(string slotId) => slotId != null && SlotRegex.IsMatch(slotId);

    public bool HasSave(string slotId = DefaultSlot) => IsValidSlot(slotId) && Backend.Exists(slotId);

    public List<string> ListSaves() => Backend.ListSlots();

    public SaveResult SaveGame(JsonObject payload, string slotId = DefaultSlot)
    {
        SaveResult result;
        if (!IsValidSlot(slotId))
        {
            result = SaveResult.Failure(SaveResult.Status.InvalidSlot, $"Invalid slot id '{slotId}'");
        }
        else
        {
            var envelope = new JsonObject
            {
                ["schema_version"] = SchemaVersion,
                ["game_version"] = ProjectSettings.GetSetting("application/config/version", "").AsString(),
                ["saved_at_unix"] = (long)Time.GetUnixTimeFromSystem(),
                ["data"] = JsonNodes.Copy(payload),
            };
            Error err = Backend.WriteText(slotId, JsonNodes.Stringify(envelope, indented: true));
            result = err == Error.Ok
                ? SaveResult.Success(payload)
                : SaveResult.Failure(SaveResult.Status.WriteFailed, err.ToString());
        }
        LogFailure("save", slotId, result);
        EmitSignalSaveFinished(slotId, result);
        return result;
    }

    public SaveResult LoadGame(string slotId = DefaultSlot)
    {
        SaveResult result = Load(slotId);
        LogFailure("load", slotId, result);
        EmitSignalLoadFinished(slotId, result);
        return result;
    }

    public SaveResult DeleteSave(string slotId = DefaultSlot)
    {
        if (!IsValidSlot(slotId))
            return SaveResult.Failure(SaveResult.Status.InvalidSlot, $"Invalid slot id '{slotId}'");
        Error err = Backend.Delete(slotId);
        if (err == Error.FileNotFound)
            return SaveResult.Failure(SaveResult.Status.NotFound, $"No save in '{slotId}'");
        if (err != Error.Ok)
            return SaveResult.Failure(SaveResult.Status.WriteFailed, err.ToString());
        return SaveResult.Success();
    }

    private SaveResult Load(string slotId)
    {
        if (!IsValidSlot(slotId))
            return SaveResult.Failure(SaveResult.Status.InvalidSlot, $"Invalid slot id '{slotId}'");
        if (!Backend.Exists(slotId))
            return SaveResult.Failure(SaveResult.Status.NotFound, $"No save in '{slotId}'");
        string text = Backend.ReadText(slotId);
        if (Backend.LastError != Error.Ok)
            return SaveResult.Failure(SaveResult.Status.ReadFailed, Backend.LastError.ToString());
        JsonNode parsed = JsonNodes.Parse(text, out string parseError);
        if (parsed == null)
            return SaveResult.Failure(SaveResult.Status.Corrupted, parseError);
        if (parsed is not JsonObject envelope)
            return SaveResult.Failure(SaveResult.Status.Corrupted, "Save is not a JSON object");
        if (!JsonNodes.IsNumber(envelope["schema_version"]) || envelope["data"] is not JsonObject data)
            return SaveResult.Failure(SaveResult.Status.Corrupted, "Save envelope is missing fields");
        int version = JsonNodes.Int(envelope["schema_version"]);
        if (version > SchemaVersion)
            return SaveResult.Failure(SaveResult.Status.VersionTooNew, $"Save schema {version} is newer than supported {SchemaVersion}");
        data = (JsonObject)data.DeepClone();
        if (version < SchemaVersion)
        {
            JsonObject migrated = SaveMigrations.Migrate(data, version, SchemaVersion);
            if (migrated == null)
                return SaveResult.Failure(SaveResult.Status.Corrupted, $"No migration from schema {version}");
            data = migrated;
        }
        return SaveResult.Success(data);
    }

    private static void LogFailure(string operation, string slotId, SaveResult result)
    {
        if (!result.IsOk() && result.State != SaveResult.Status.NotFound)
            DevLog.Warn("save", $"{operation} '{slotId}' failed: {result.State} ({result.Detail})");
    }
}
