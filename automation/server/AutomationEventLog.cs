using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Ring buffer of semantic events with monotonically increasing sequence
/// numbers. Payloads contain ids and numbers only (no localized text).
/// </summary>
public class AutomationEventLog
{
    /// <summary>Raised with each new event (a private copy per listener call).</summary>
    public event Action<JsonObject> EventAdded;

    public int Capacity { get; }
    public long LastSeq { get; private set; }

    private readonly LinkedList<JsonObject> _events = new();

    public AutomationEventLog(int capacity = AutomationProtocol.EventBuffer)
    {
        Capacity = capacity;
    }

    public JsonObject Emit(string type, JsonObject data = null)
    {
        LastSeq++;
        var entry = new JsonObject
        {
            ["seq"] = LastSeq,
            ["type"] = type,
            ["frame"] = (long)Engine.GetProcessFrames(),
            ["t_ms"] = (long)Time.GetTicksMsec(),
            ["data"] = data ?? new JsonObject(),
        };
        _events.AddLast(entry);
        if (_events.Count > Capacity)
            _events.RemoveFirst();
        EventAdded?.Invoke((JsonObject)entry.DeepClone());
        return entry;
    }

    /// <summary>Copies of the events after <paramref name="seq"/>, optionally filtered by type.</summary>
    public List<JsonObject> Since(long seq, IReadOnlyCollection<string> types = null, int limit = 200)
    {
        var output = new List<JsonObject>();
        foreach (JsonObject entry in _events)
        {
            if (JsonNodes.Long(entry["seq"]) <= seq)
                continue;
            if (types != null && types.Count > 0 && !types.Contains(JsonNodes.Str(entry["type"])))
                continue;
            output.Add((JsonObject)entry.DeepClone());
            if (output.Count >= limit)
                break;
        }
        return output;
    }

    public static bool Matches(JsonObject entry, string type, JsonObject match)
    {
        if (JsonNodes.Str(entry["type"]) != type)
            return false;
        foreach ((string key, JsonNode expected) in match ?? new JsonObject())
        {
            (bool exists, JsonNode actual) = AutomationPredicate.Lookup(entry["data"], key);
            if (!exists || !AutomationPredicate.ValuesEqual(actual, expected))
                return false;
        }
        return true;
    }

    public static JsonArray ToArray(IEnumerable<JsonObject> entries) => new(entries.Select(e => (JsonNode)e).ToArray());
}
