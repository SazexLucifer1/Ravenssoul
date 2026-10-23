using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Unit.Core;

/// <summary>
/// SaveService runs against MemorySaveBackend (the runner installs a fresh one
/// per test) except where the JSON file backend itself is under test.
/// </summary>
public class TestSaveService : TestCase
{
    private const string TestDir = "user://test_saves";

    private static SaveService Saves => SaveService.Instance;
    private static MemorySaveBackend Memory => (MemorySaveBackend)SaveService.Instance.Backend;

    public override Task BeforeEach()
    {
        DevLog.Muted = true; // failure paths below log warnings by design
        return Task.CompletedTask;
    }

    public override Task AfterEach()
    {
        // The folder only exists after the file-backend test ran once; listing a
        // missing folder logs an engine error (this made fresh checkouts fail).
        if (!DirAccess.DirExistsAbsolute(TestDir))
            return Task.CompletedTask;
        foreach (string file in DirAccess.GetFilesAt(TestDir))
            DirAccess.RemoveAbsolute(TestDir.PathJoin(file));
        return Task.CompletedTask;
    }

    public void TestRoundTrip()
    {
        var payload = new JsonObject { ["wallet"] = new JsonObject { ["gold"] = 5 }, ["soul_energy_spent"] = 2 };
        AssertTrue(Saves.SaveGame(payload).IsOk());
        AssertTrue(Saves.HasSave());
        SaveResult loaded = Saves.LoadGame();
        AssertTrue(loaded.IsOk());
        AssertEq(JsonNodes.Int(loaded.Data["soul_energy_spent"]), 2);
    }

    public void TestEnvelopeCarriesSchemaVersion()
    {
        Saves.SaveGame(new JsonObject { ["x"] = 1 });
        var envelope = (JsonObject)JsonNode.Parse(Memory.Slots[SaveService.DefaultSlot]);
        AssertEq(JsonNodes.Int(envelope["schema_version"]), SaveService.SchemaVersion);
        AssertTrue(envelope.ContainsKey("saved_at_unix"));
    }

    public void TestMissingSaveIsNotFound() =>
        AssertEq(Saves.LoadGame("campaign_9").State, SaveResult.Status.NotFound);

    public void TestCorruptedSaveIsReportedWithPlayerMessage()
    {
        Memory.Slots["campaign_1"] = "{not json";
        SaveResult result = Saves.LoadGame();
        AssertEq(result.State, SaveResult.Status.Corrupted);
        AssertEq(result.PlayerMessageKey(), "SAVE_ERROR_CORRUPTED");
    }

    public void TestNewerSchemaIsRefused()
    {
        Memory.Slots["campaign_1"] = JsonNodes.Stringify(new JsonObject { ["schema_version"] = SaveService.SchemaVersion + 1, ["data"] = new JsonObject() });
        AssertEq(Saves.LoadGame().State, SaveResult.Status.VersionTooNew);
    }

    public void TestSlotIdsCannotEscapeTheSaveFolder()
    {
        foreach (string bad in new[] { "../evil", "a/b", "", "UPPER", new string('x', 40) })
            AssertEq(Saves.SaveGame(new JsonObject(), bad).State, SaveResult.Status.InvalidSlot, bad);
    }

    public void TestWriteFailureKeepsPreviousSave()
    {
        Saves.SaveGame(new JsonObject { ["version"] = "old" });
        Memory.FailWrites = true;
        SaveResult result = Saves.SaveGame(new JsonObject { ["version"] = "new" });
        AssertEq(result.State, SaveResult.Status.WriteFailed);
        AssertEq(result.PlayerMessageKey(), "SAVE_ERROR_WRITE_FAILED");
        AssertEq(Saves.LoadGame().Data["version"], "old");
    }

    public void TestEveryStatusHasAPlayerMessage()
    {
        foreach (SaveResult.Status status in Enum.GetValues<SaveResult.Status>())
            AssertTrue(SaveResult.PlayerMessageKeys.ContainsKey(status), $"status {status}");
    }

    public void TestJsonFileBackendAtomicWriteAndBackup()
    {
        var backend = new JsonFileSaveBackend(TestDir);
        AssertEq(backend.WriteText("slot_a", "first"), Error.Ok);
        AssertEq(backend.WriteText("slot_a", "second"), Error.Ok);
        AssertEq(backend.ReadText("slot_a"), "second");
        AssertTrue(FileAccess.FileExists(TestDir.PathJoin("slot_a.json.bak")));
        AssertFalse(FileAccess.FileExists(TestDir.PathJoin("slot_a.json.tmp")));
        AssertEq(backend.ListSlots(), new[] { "slot_a" });
        AssertEq(backend.Delete("slot_a"), Error.Ok);
        AssertFalse(backend.Exists("slot_a"));
    }

    public void TestGameSessionRoundTrip()
    {
        GameSession session = GameSession.Instance;
        session.StartNewCampaign();
        session.CompleteMission(session.ActiveMission);
        session.SoulEnergySpent = 4;
        Saves.SaveGame(session.ToSaveData());
        session.StartNewCampaign();
        AssertEq(session.Wallet.Amount(ResourceWallet.Gold), 0);
        AssertTrue(session.LoadSaveData(Saves.LoadGame().Data));
        AssertEq(session.Wallet.Amount(ResourceWallet.Gold), 50);
        AssertEq(session.SoulEnergySpent, 4);
        AssertContains(session.CompletedMissions, "mission_first_spark");
    }

    public void TestSessionRejectsUnknownContentWithoutSideEffects()
    {
        GameSession session = GameSession.Instance;
        session.StartNewCampaign();
        session.Wallet.Add(ResourceWallet.Gold, 7);
        AssertFalse(session.LoadSaveData(new JsonObject { ["loadout"] = new JsonObject { ["hero_id"] = "deleted_hero" } }));
        AssertEq(session.Wallet.Amount(ResourceWallet.Gold), 7);
    }
}
