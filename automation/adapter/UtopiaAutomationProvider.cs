using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Game semantics for Utopia: units (+ components), the battle grid, the
/// active mission, campaign resources, unit movement, pause/resume, and the
/// allow-listed dev commands. Ids derive from content ids, so they are stable
/// across runs and languages:
///   unit.&lt;hero_id&gt;, unit.&lt;hero_id&gt;/health, unit.spawned.&lt;n&gt;, grid.&lt;map_id&gt;,
///   mission.&lt;mission_id&gt;, resource.&lt;resource_id&gt;
/// </summary>
public class UtopiaAutomationProvider : AutomationProvider
{
    public static readonly IReadOnlyDictionary<string, Vector2I> Directions = new Dictionary<string, Vector2I>
    {
        ["up"] = Vector2I.Up, ["down"] = Vector2I.Down, ["left"] = Vector2I.Left, ["right"] = Vector2I.Right,
    };
    public const int MaxPath = 64;
    public const int MaxGrant = 10000;

    /// <summary>null = running, true/false = finished.</summary>
    private bool? _missionResult;
    private readonly List<Character> _spawned = new();

    private static GameSession Session => GameSession.Instance;

    // --- gameplay scene contract (public members of scenes/gameplay/Gameplay.cs) --------

    public static Gameplay AsGameplay(Node scene) => scene as Gameplay;

    public static string PlayerUnitId()
    {
        UnitLoadout loadout = Session.ActiveLoadout;
        return loadout?.Hero != null ? $"unit.{loadout.Hero.Id}" : "unit.player";
    }

    // --- entities ----------------------------------------------------------------------

    public override void Collect(Dictionary<string, IndexEntry> index, Node scene)
    {
        foreach (StringName id in ResourceWallet.All)
            index[$"resource.{id}"] = new IndexEntry("resource", null, this) { Detail = id };
        if (Session.ActiveMission != null && Session.HasCampaign)
            index[$"mission.{Session.ActiveMission.Id}"] = new IndexEntry("mission", null, this);
        Gameplay gameplay = AsGameplay(scene);
        if (gameplay == null)
            return;
        if (gameplay.Grid?.MapData != null)
            index[$"grid.{gameplay.Grid.MapData.Id}"] = new IndexEntry("grid", gameplay.Grid, this);
        if (gameplay.PlayerUnit != null && GodotObject.IsInstanceValid(gameplay.PlayerUnit))
            AddUnit(index, PlayerUnitId(), gameplay.PlayerUnit, true);
        for (int i = 0; i < _spawned.Count; i++)
            if (GodotObject.IsInstanceValid(_spawned[i]))
                AddUnit(index, $"unit.spawned.{i + 1}", _spawned[i], false);
    }

    private void AddUnit(Dictionary<string, IndexEntry> index, string id, Character unit, bool isPlayer)
    {
        index[id] = new IndexEntry("unit", unit, this) { IsPlayer = isPlayer };
        index[$"{id}/health"] = new IndexEntry("component", unit.Health, this) { Detail = "health" };
        index[$"{id}/mover"] = new IndexEntry("component", unit.Mover, this) { Detail = "mover" };
        index[$"{id}/animator"] = new IndexEntry("component", unit.Animator, this) { Detail = "animator" };
    }

    public override JsonObject Describe(string id, IndexEntry entry, bool deep)
    {
        var desc = new JsonObject
        {
            ["tags"] = new JsonArray(), ["visible"] = true, ["enabled"] = true, ["interactable"] = false,
            ["components"] = new JsonArray(), ["actions"] = new JsonArray(), ["properties"] = new JsonObject(),
        };
        switch (entry.Kind)
        {
            case "resource":
                desc["type"] = "Resource";
                desc["name"] = entry.Detail;
                desc["role"] = "item";
                desc["text_key"] = "RESOURCE_" + entry.Detail.ToUpperInvariant();
                desc["properties"] = new JsonObject { ["amount"] = Session.Wallet.Amount(entry.Detail) };
                break;
            case "mission":
            {
                MissionData mission = Session.ActiveMission;
                var rewards = new JsonObject();
                foreach ((StringName resource, int amount) in mission.Rewards)
                    rewards[resource] = amount;
                desc["type"] = "Mission";
                desc["name"] = (string)mission.Id;
                desc["role"] = "quest";
                desc["text_key"] = mission.TitleKey;
                desc["properties"] = new JsonObject
                {
                    ["objective"] = GodotAutomationAdapter.Snake(mission.Objective.ToString()),
                    ["objective_key"] = mission.ObjectiveKey,
                    ["completed"] = Session.CompletedMissions.Contains(mission.Id),
                    ["rewards"] = rewards,
                };
                break;
            }
            case "grid":
            {
                BattleMapData map = ((BattleGrid)entry.Node).MapData;
                desc["type"] = "BattleGrid";
                desc["name"] = (string)map.Id;
                desc["role"] = "grid";
                desc["properties"] = new JsonObject
                {
                    ["size"] = new JsonArray(map.Size.X, map.Size.Y),
                    ["objective_cell"] = CellJson(map.ObjectiveCell),
                    ["spawn_cell"] = CellJson(map.PlayerSpawn),
                    ["blocked_cells"] = new JsonArray(map.BlockedCells.Select(c => (JsonNode)CellJson(c)).ToArray()),
                    ["hazard_cells"] = new JsonArray(map.HazardCells.Select(c => (JsonNode)CellJson(c)).ToArray()),
                    ["hazard_damage"] = map.HazardDamage,
                };
                break;
            }
            case "unit":
            {
                var unit = (Character)entry.Node;
                Vector2 screenPos = unit.GetGlobalTransformWithCanvas().Origin;
                PlayerUnitController controller = entry.IsPlayer ? AsGameplay(unit.GetTree().CurrentScene)?.Controller : null;
                bool canAct = entry.IsPlayer && controller != null && controller.Enabled && !unit.IsDefeated();
                desc["type"] = "Character";
                desc["name"] = unit.DisplayNameKey;
                desc["role"] = "unit";
                desc["tags"] = new JsonArray(entry.IsPlayer ? "player" : "spawned");
                desc["visible"] = unit.IsVisibleInTree();
                desc["enabled"] = !unit.IsDefeated();
                desc["interactable"] = canAct;
                desc["position"] = new JsonObject { ["x"] = Mathf.RoundToInt(screenPos.X), ["y"] = Mathf.RoundToInt(screenPos.Y) };
                desc["cell"] = CellJson(unit.Cell);
                desc["components"] = new JsonArray("health", "mover", "animator");
                desc["actions"] = canAct ? new JsonArray("move") : new JsonArray();
                desc["text_key"] = unit.DisplayNameKey;
                var props = new JsonObject
                {
                    ["health"] = unit.Health.CurrentHealth,
                    ["max_health"] = unit.Health.MaxHealth,
                    ["defeated"] = unit.IsDefeated(),
                    ["moving"] = unit.Mover.IsMoving(),
                    ["facing"] = unit.Visual.Facing,
                    ["animation"] = (string)unit.Animator.CurrentAnimation(),
                };
                if (deep && unit.Stats != null)
                {
                    var stats = new JsonObject();
                    foreach ((string field, int value) in unit.Stats.ToDict())
                        stats[field] = value;
                    props["stats"] = stats;
                }
                desc["properties"] = props;
                break;
            }
            case "component":
            {
                Node node = entry.Node;
                desc["properties"] = node switch
                {
                    HealthComponent health => new JsonObject { ["current"] = health.CurrentHealth, ["max"] = health.MaxHealth, ["dead"] = health.IsDead() },
                    GridMover mover => new JsonObject { ["moving"] = mover.IsMoving(), ["step_seconds"] = mover.StepSeconds },
                    CharacterAnimator animator => new JsonObject { ["animation"] = (string)animator.CurrentAnimation() },
                    _ => new JsonObject(),
                };
                desc["type"] = node.GetType().Name;
                desc["name"] = entry.Detail;
                desc["role"] = "component";
                break;
            }
        }
        return desc;
    }

    // --- state & actions ---------------------------------------------------------------

    public override void AddState(JsonObject state, Node scene)
    {
        state["campaign"] = new JsonObject
        {
            ["active"] = Session.HasCampaign,
            ["wallet"] = Session.Wallet.ToSaveData(),
            ["completed_missions"] = GodotAutomationAdapter.ToArray(Session.CompletedMissions),
            ["soul_energy_spent"] = Session.SoulEnergySpent,
            ["loadout"] = Session.ActiveLoadout != null ? Session.ActiveLoadout.ToSaveData() : new JsonObject(),
        };
        state["mission"] = null;
        state["unit"] = null;
        Gameplay gameplay = AsGameplay(scene);
        if (gameplay == null)
            return;
        MissionResultScreen resultScreen = ResultScreen(gameplay);
        state["mission"] = new JsonObject
        {
            ["id"] = gameplay.Mission != null ? (string)gameplay.Mission.Id : "",
            ["finished"] = _missionResult != null,
            ["victory"] = _missionResult,
            ["save_state"] = resultScreen != null ? GodotAutomationAdapter.Snake(resultScreen.CurrentSaveState.ToString()) : null,
        };
        Character unit = gameplay.PlayerUnit;
        if (unit != null)
        {
            state["unit"] = new JsonObject
            {
                ["id"] = PlayerUnitId(),
                ["cell"] = CellJson(unit.Cell),
                ["health"] = unit.Health.CurrentHealth,
                ["max_health"] = unit.Health.MaxHealth,
                ["defeated"] = unit.IsDefeated(),
                ["moving"] = unit.Mover.IsMoving(),
                ["can_act"] = gameplay.Controller.Enabled,
            };
        }
    }

    public override void AddActions(List<JsonObject> actions, Node scene)
    {
        Gameplay gameplay = AsGameplay(scene);
        if (gameplay == null)
            return;
        Character unit = gameplay.PlayerUnit;
        if (unit != null && gameplay.Controller.Enabled && !unit.IsDefeated())
        {
            foreach ((string direction, Vector2I offset) in Directions)
                if (gameplay.Grid.CanEnter(unit.Cell + offset))
                    actions.Add(new JsonObject { ["action"] = "move", ["target"] = PlayerUnitId(), ["args"] = new JsonObject { ["direction"] = direction } });
        }
        if (gameplay.ScreenStack.IsEmpty() && _missionResult == null)
            actions.Add(new JsonObject { ["action"] = "pause", ["target"] = "scene.gameplay" });
        else if (gameplay.ScreenStack.Top() is PauseMenu)
            actions.Add(new JsonObject { ["action"] = "resume", ["target"] = "scene.gameplay" });
    }

    public override JsonObject Perform(string action, string target, JsonObject args, Node scene)
    {
        switch (action)
        {
            case "move":
                return Move(target, args, scene);
            case "pause" or "resume":
            {
                Gameplay gameplay = AsGameplay(scene);
                if (gameplay == null)
                    return AutomationProtocol.Fail(AutomationProtocol.Unsupported, $"{action} is only available during a mission");
                ScreenStack stack = gameplay.ScreenStack;
                bool allowed = (action == "pause" && stack.IsEmpty() && _missionResult == null)
                    || (action == "resume" && stack.Top() is PauseMenu);
                if (!allowed)
                    return AutomationProtocol.Fail(AutomationProtocol.NotInteractable, $"cannot {action} now");
                // The real "pause" input action, so the gameplay input path is exercised.
                Input.Action("pause", "tap", 0);
                return AutomationProtocol.Ok(new JsonObject { ["performed"] = true, ["via"] = "pause" });
            }
        }
        return null;
    }

    private JsonObject Move(string target, JsonObject args, Node scene)
    {
        Gameplay gameplay = AsGameplay(scene);
        if (gameplay == null)
            return AutomationProtocol.Fail(AutomationProtocol.Unsupported, "move is only available during a mission");
        string unitId = PlayerUnitId();
        if (target.Length > 0 && target != unitId)
            return AutomationProtocol.Fail(AutomationProtocol.NotFound, $"'{target}' is not a controllable unit");
        var steps = new List<string>();
        bool valid = true;
        if (JsonNodes.IsString(args["direction"]))
        {
            steps.Add(args["direction"].GetValue<string>());
        }
        else if (args["path"] is JsonArray path)
        {
            foreach (JsonNode step in path)
            {
                valid &= JsonNodes.IsString(step);
                steps.Add(JsonNodes.Str(step));
            }
        }
        if (!valid || steps.Count == 0 || steps.Count > MaxPath || steps.Any(d => !Directions.ContainsKey(d)))
            return AutomationProtocol.Fail(AutomationProtocol.InvalidParams,
                $"args.direction (up/down/left/right) or args.path (list of directions, max {MaxPath}) required");
        PlayerUnitController controller = gameplay.Controller;
        Character unit = gameplay.PlayerUnit;
        if (!controller.Enabled || unit.IsDefeated())
            return AutomationProtocol.Fail(AutomationProtocol.NotInteractable, "the unit cannot act now (paused, defeated, or mission over)");
        int taken = 0;
        foreach (string direction in steps)
        {
            if (!controller.Enabled || !controller.TryStep(Directions[direction]))
            {
                return AutomationProtocol.Ok(new JsonObject
                {
                    ["performed"] = taken > 0, ["steps"] = taken, ["cell"] = CellJson(unit.Cell),
                    ["stopped"] = controller.Enabled ? "blocked" : "unit_cannot_act",
                });
            }
            taken++;
        }
        return AutomationProtocol.Ok(new JsonObject { ["performed"] = true, ["steps"] = taken, ["cell"] = CellJson(unit.Cell) });
    }

    // --- events --------------------------------------------------------------------------

    public override void HookScene(Node scene)
    {
        _missionResult = null;
        _spawned.Clear();
        Gameplay gameplay = AsGameplay(scene);
        if (gameplay == null || Events == null)
            return;
        AutomationEventLog log = Events;
        Character unit = gameplay.PlayerUnit;
        string unitId = PlayerUnitId();
        gameplay.MissionFinished += victory =>
        {
            _missionResult = victory;
            log.Emit("mission.finished", new JsonObject { ["mission"] = (string)gameplay.Mission.Id, ["victory"] = victory });
        };
        unit.CellChanged += cell => log.Emit("unit.moved", new JsonObject { ["unit"] = unitId, ["cell"] = CellJson(cell) });
        unit.Health.Damaged += (amount, source) => log.Emit("unit.damaged", new JsonObject
        {
            ["unit"] = unitId, ["amount"] = amount, ["source"] = (string)source, ["health"] = unit.Health.CurrentHealth,
        });
        unit.Defeated += () => log.Emit("unit.defeated", new JsonObject { ["unit"] = unitId });
        gameplay.Controller.StepBlocked += cell => log.Emit("unit.blocked", new JsonObject { ["unit"] = unitId, ["cell"] = CellJson(cell) });
        gameplay.Controller.ObjectiveReached += cell => log.Emit("mission.objective_reached", new JsonObject { ["unit"] = unitId, ["cell"] = CellJson(cell) });
    }

    public override bool IsSettled(Node scene)
    {
        Character unit = AsGameplay(scene)?.PlayerUnit;
        if (unit != null && unit.Mover.IsMoving())
            return false;
        return !_spawned.Any(spawned => GodotObject.IsInstanceValid(spawned) && spawned.Mover.IsMoving());
    }

    // --- dev profile ---------------------------------------------------------------------

    public override JsonObject Dev(string method, JsonObject parameters, Node scene) => method switch
    {
        "dev.teleport" => Teleport(JsonNodes.Str(parameters["target"]), parameters["cell"] as JsonArray, scene),
        "dev.spawn" => Spawn(parameters, scene),
        "dev.command" => Command(JsonNodes.Str(parameters["command"]), parameters["args"] as JsonObject, scene),
        _ => null,
    };

    private JsonObject Teleport(string target, JsonArray cellValue, Node scene)
    {
        Gameplay gameplay = AsGameplay(scene);
        if (gameplay == null)
            return AutomationProtocol.Fail(AutomationProtocol.Unsupported, "teleport is only available during a mission");
        var index = new Dictionary<string, IndexEntry>();
        Collect(index, scene);
        if (!index.TryGetValue(target, out IndexEntry entry) || entry.Kind != "unit")
            return AutomationProtocol.Fail(AutomationProtocol.NotFound, $"no unit '{target}'");
        var unit = (Character)entry.Node;
        var cell = new Vector2I(JsonNodes.Int(cellValue[0]), JsonNodes.Int(cellValue[1]));
        if (!gameplay.Grid.Relocate(unit.Cell, cell))
            return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, $"cell {cell} is blocked, occupied, or outside the map");
        unit.PlaceAt(cell, gameplay.Grid.CellToLocal(cell));
        return AutomationProtocol.Ok(new JsonObject { ["cell"] = CellJson(cell) });
    }

    private JsonObject Spawn(JsonObject parameters, Node scene)
    {
        Gameplay gameplay = AsGameplay(scene);
        if (gameplay == null)
            return AutomationProtocol.Fail(AutomationProtocol.Unsupported, "spawn is only available during a mission");
        HeroData hero = GameSession.Roster.FindHero(JsonNodes.Str(parameters["hero_id"]));
        BattalionData battalion = GameSession.Roster.FindBattalion(JsonNodes.Str(parameters["battalion_id"]));
        if (hero == null || battalion == null)
            return AutomationProtocol.Fail(AutomationProtocol.NotFound, "unknown hero_id or battalion_id");
        var cellValue = (JsonArray)parameters["cell"];
        var cell = new Vector2I(JsonNodes.Int(cellValue[0]), JsonNodes.Int(cellValue[1]));
        if (!gameplay.Grid.CanEnter(cell))
            return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, $"cell {cell} is blocked, occupied, or outside the map");
        var unit = gameplay.CharacterScene.Instantiate<Character>();
        gameplay.UnitsRoot.AddChild(unit);
        unit.Configure(new UnitLoadout(hero, battalion).CombinedStats(), hero.NameKey);
        gameplay.Grid.Place(unit, cell);
        unit.PlaceAt(cell, gameplay.Grid.CellToLocal(cell));
        _spawned.Add(unit);
        return AutomationProtocol.Ok(new JsonObject { ["id"] = $"unit.spawned.{_spawned.Count}", ["cell"] = CellJson(cell) });
    }

    private JsonObject Command(string command, JsonObject args, Node scene)
    {
        if (command == "grant_resources")
        {
            StringName resource = JsonNodes.Str(args?["resource"]);
            int amount = JsonNodes.Int(args?["amount"]);
            if (!ResourceWallet.IsKnown(resource) || amount < 1 || amount > MaxGrant)
                return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, $"args.resource must be a resource id and args.amount 1..{MaxGrant}");
            Session.Wallet.Add(resource, amount);
            return AutomationProtocol.Ok(new JsonObject { ["resource"] = (string)resource, ["amount"] = Session.Wallet.Amount(resource) });
        }
        Gameplay gameplay = AsGameplay(scene);
        if (gameplay == null)
            return AutomationProtocol.Fail(AutomationProtocol.Unsupported, $"{command} is only available during a mission");
        Character unit = gameplay.PlayerUnit;
        switch (command)
        {
            case "heal_all":
                foreach (Character u in _spawned.Where(GodotObject.IsInstanceValid).Prepend(unit))
                    if (!u.IsDefeated())
                        u.Health.SetCurrentHealth(u.Health.MaxHealth);
                return AutomationProtocol.Ok(new JsonObject { ["health"] = unit.Health.CurrentHealth });
            case "win_mission":
                gameplay.Finish(true);
                return AutomationProtocol.Ok(new JsonObject { ["finished"] = true });
            case "lose_mission":
                unit.Health.ApplyDamage(unit.Health.CurrentHealth, "dev_command");
                return AutomationProtocol.Ok(new JsonObject { ["finished"] = true });
        }
        return AutomationProtocol.Fail(AutomationProtocol.Unsupported, command);
    }

    public override void CleanupSession(Node scene)
    {
        BattleGrid grid = AsGameplay(scene)?.Grid;
        foreach (Character unit in _spawned)
        {
            if (!GodotObject.IsInstanceValid(unit))
                continue;
            grid?.Remove(unit.Cell);
            unit.QueueFree();
        }
        _spawned.Clear();
    }

    // --- helpers -------------------------------------------------------------------------

    private static MissionResultScreen ResultScreen(Gameplay gameplay) => gameplay.ScreenStack?.Top() as MissionResultScreen;

    public static JsonArray CellJson(Vector2I cell) => new(cell.X, cell.Y);
}
