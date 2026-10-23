using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia.Automation;

/// <summary>One discoverable entity: what it is, the node behind it, and who describes it.</summary>
public sealed record IndexEntry(string Kind, Node Node = null, AutomationProvider Provider = null)
{
    /// <summary>Provider-specific detail (resource id, component name, ...).</summary>
    public string Detail { get; init; } = "";
    public bool IsPlayer { get; init; }
}

/// <summary>
/// Game-specific extension of the Godot adapter. A provider contributes
/// semantic entities, state, actions, and events for game systems the generic
/// UI layer cannot understand (units, grids, missions, inventories).
/// Register providers in the GodotAutomationAdapter constructor.
/// </summary>
public abstract class AutomationProvider
{
    public AutomationEventLog Events { get; set; }
    public AutomationInputDriver Input { get; set; }

    /// <summary>Add entries to <paramref name="index"/> (id -> entry with Provider = this).</summary>
    public virtual void Collect(Dictionary<string, IndexEntry> index, Node scene)
    {
    }

    /// <summary>Describe an entry this provider added (see docs/AUTOMATION.md#entities).</summary>
    public virtual JsonObject Describe(string id, IndexEntry entry, bool deep) => new();

    public virtual void AddState(JsonObject state, Node scene)
    {
    }

    public virtual void AddActions(List<JsonObject> actions, Node scene)
    {
    }

    /// <summary>Return null if this provider does not handle the action.</summary>
    public virtual JsonObject Perform(string action, string target, JsonObject args, Node scene) => null;

    /// <summary>Called after every scene change so the provider can connect scene signals.</summary>
    public virtual void HookScene(Node scene)
    {
    }

    public virtual bool IsSettled(Node scene) => true;

    public virtual JsonObject Dev(string method, JsonObject parameters, Node scene) => null;

    public virtual void CleanupSession(Node scene)
    {
    }
}
