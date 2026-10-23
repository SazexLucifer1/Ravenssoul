using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia;

/// <summary>
/// GameSession (Autoload) — campaign state that must survive scene changes.
///
/// Why global: the base, mission select, and battle scenes are separate routed
/// scenes; the active roster, resources, and soul-energy tally live between
/// them. It holds data and serialization only: rules live in features, UI
/// reads it through signals.
/// </summary>
public partial class GameSession : Node
{
    [Signal] public delegate void CampaignStartedEventHandler();
    [Signal] public delegate void CampaignLoadedEventHandler();

    public const string RosterPath = "res://features/characters/content/roster_catalog.tres";
    public const string FirstMissionPath = "res://features/quests/content/mission_first_spark.tres";

    private static RosterCatalog _roster;
    private static MissionData _firstMission;

    public static RosterCatalog Roster => _roster ??= GD.Load<RosterCatalog>(RosterPath);
    public static MissionData FirstMission => _firstMission ??= GD.Load<MissionData>(FirstMissionPath);

    /// <summary>The autoload instance (registered as "GameSession" in project.godot).</summary>
    public static GameSession Instance { get; private set; }

    public ResourceWallet Wallet { get; set; } = new();
    public UnitLoadout ActiveLoadout { get; set; }
    public MissionData ActiveMission { get; set; }
    public List<string> CompletedMissions { get; set; } = new();
    /// <summary>Tracked globally; drives the corruption story threshold (design pillar).</summary>
    public int SoulEnergySpent { get; set; }
    public bool HasCampaign { get; set; }

    public override void _EnterTree()
    {
        Instance ??= this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public void StartNewCampaign()
    {
        Wallet = new ResourceWallet();
        ActiveLoadout = new UnitLoadout(Roster.DefaultHero, Roster.DefaultBattalion);
        ActiveMission = FirstMission;
        CompletedMissions = new List<string>();
        SoulEnergySpent = 0;
        HasCampaign = true;
        EmitSignalCampaignStarted();
    }

    /// <summary>Applies mission rewards and records completion. Returns the rewards granted.</summary>
    public Dictionary<StringName, int> CompleteMission(MissionData mission)
    {
        var rewards = new Dictionary<StringName, int>();
        foreach ((StringName resourceId, int amount) in mission.Rewards)
        {
            Wallet.Add(resourceId, amount);
            rewards[resourceId] = amount;
        }
        if (!CompletedMissions.Contains(mission.Id))
            CompletedMissions.Add(mission.Id);
        return rewards;
    }

    public JsonObject ToSaveData() => new()
    {
        ["wallet"] = Wallet.ToSaveData(),
        ["loadout"] = ActiveLoadout != null ? ActiveLoadout.ToSaveData() : new JsonObject(),
        ["active_mission_id"] = ActiveMission != null ? (string)ActiveMission.Id : "",
        ["completed_missions"] = new JsonArray(CompletedMissions.Select(id => (JsonNode)id).ToArray()),
        ["soul_energy_spent"] = SoulEnergySpent,
    };

    /// <summary>
    /// Returns false (leaving the session untouched) if the data references
    /// content that no longer exists.
    /// </summary>
    public bool LoadSaveData(JsonObject data)
    {
        UnitLoadout loadout = UnitLoadout.FromSaveData(data["loadout"] as JsonObject ?? new JsonObject(), Roster);
        if (!loadout.IsComplete())
        {
            DevLog.Warn("session", $"Save references unknown hero or battalion: {JsonNodes.Stringify(data["loadout"])}");
            return false;
        }
        Wallet = new ResourceWallet();
        Wallet.LoadSaveData(data["wallet"] as JsonObject ?? new JsonObject());
        ActiveLoadout = loadout;
        // Only one mission exists in the first playable; unknown ids fall back to it.
        ActiveMission = FirstMission;
        CompletedMissions = (data["completed_missions"] as JsonArray ?? new JsonArray())
            .Select(n => JsonNodes.Str(n)).Where(id => id.Length > 0).ToList();
        SoulEnergySpent = Mathf.Max(0, JsonNodes.Int(data["soul_energy_spent"]));
        HasCampaign = true;
        EmitSignalCampaignLoaded();
        return true;
    }
}
