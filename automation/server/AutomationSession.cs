using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Per-client automation state. Everything here is temporary and is removed
/// by AutomationServer.CloseSession() (explicit close, idle timeout,
/// shutdown).
/// </summary>
public class AutomationSession
{
    public string Id { get; } = System.Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
    public ulong OpenedMsec { get; } = Time.GetTicksMsec();
    public ulong LastSeenMsec { get; private set; }
    /// <summary>Entries while recording; null otherwise.</summary>
    public JsonArray Recording { get; set; }
    public Dictionary<string, JsonObject> Checkpoints { get; } = new();
    public List<string> FixtureSlots { get; } = new();
    public List<JsonObject> AssertionFailures { get; } = new();
    public int RequestCount { get; private set; }

    public AutomationSession()
    {
        LastSeenMsec = OpenedMsec;
    }

    public void Touch()
    {
        LastSeenMsec = Time.GetTicksMsec();
        RequestCount++;
    }

    public long IdleMs() => (long)(Time.GetTicksMsec() - LastSeenMsec);

    public void Record(string kind, JsonNode data)
    {
        Recording?.Add(new JsonObject
        {
            ["t_ms"] = (long)(Time.GetTicksMsec() - OpenedMsec),
            ["kind"] = kind,
            ["data"] = JsonNodes.Copy(data),
        });
    }
}
