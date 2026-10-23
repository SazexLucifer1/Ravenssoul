using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Utopia.Automation;

/// <summary>
/// Engine-side contract behind the protocol. The protocol (methods.json) is
/// engine-independent; an adapter maps it onto one engine's scene/entity
/// model. Every method returns plain JSON data (ids, numbers, strings, arrays,
/// objects). Errors are returned as {"error": AutomationProtocol.Error(...)};
/// successes as {"result": ...}.
/// To port the protocol to another engine, implement these methods there.
/// </summary>
public abstract class AutomationAdapter
{
    /// <summary>{"engine", "engine_version", "adapter", "semantic_actions": {name: supported}, "input_devices": [...], "screenshots": bool}</summary>
    public virtual JsonObject Describe() => new();

    public virtual JsonObject SceneInfo() => new();

    public virtual List<JsonObject> Query(JsonObject filters) => new();

    /// <summary>Returns the entity description or null when the id is unknown.</summary>
    public virtual JsonObject Entity(string id, bool deep) => null;

    public virtual JsonObject State() => new();

    public virtual List<JsonObject> AvailableActions() => new();

    public virtual JsonObject Perform(string action, string target, JsonObject args) =>
        AutomationProtocol.Fail(AutomationProtocol.Unsupported, "not implemented");

    public virtual Task<JsonObject> Screenshot(int maxWidth) =>
        Task.FromResult(AutomationProtocol.Fail(AutomationProtocol.Unsupported, "not implemented"));

    public virtual JsonObject Perf() => new();

    public virtual bool IsSettled() => true;

    public virtual Task<JsonObject> Reset(string route) =>
        Task.FromResult(AutomationProtocol.Fail(AutomationProtocol.Unsupported, "not implemented"));

    public virtual JsonObject CaptureCheckpoint() => new();

    public virtual Task<JsonObject> RestoreCheckpoint(JsonObject snapshot) =>
        Task.FromResult(AutomationProtocol.Fail(AutomationProtocol.Unsupported, "not implemented"));

    public virtual Task<JsonObject> ApplyFixture(JsonObject fixture, bool writeSave) =>
        Task.FromResult(AutomationProtocol.Fail(AutomationProtocol.Unsupported, "not implemented"));

    public virtual JsonObject Dev(string method, JsonObject parameters) =>
        AutomationProtocol.Fail(AutomationProtocol.Unsupported, "not implemented");

    /// <summary>Start emitting semantic events into <paramref name="log"/>; stop on <see cref="Shutdown"/>.</summary>
    public virtual void ConnectEvents(AutomationEventLog log)
    {
    }

    /// <summary>Removes temporary state created through the adapter (spawned units, ...).</summary>
    public virtual void CleanupSession()
    {
    }

    public virtual void Shutdown()
    {
    }
}
