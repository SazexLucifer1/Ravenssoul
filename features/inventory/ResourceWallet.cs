using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia;

/// <summary>
/// Campaign resources (wood, stone, food, gold, soul energy).
/// Keys are stable ids; display names come from translation keys.
/// </summary>
public partial class ResourceWallet : RefCounted
{
    [Signal] public delegate void ChangedEventHandler(StringName resourceId, int amount);

    public static readonly StringName Wood = "wood";
    public static readonly StringName Stone = "stone";
    public static readonly StringName Food = "food";
    public static readonly StringName Gold = "gold";
    public static readonly StringName SoulEnergy = "soul_energy";
    public static readonly IReadOnlyList<StringName> All = new[] { Wood, Stone, Food, Gold, SoulEnergy };

    private readonly Dictionary<StringName, int> _amounts = new();

    public static bool IsKnown(StringName resourceId) => All.Contains(resourceId);

    public int Amount(StringName resourceId) => _amounts.TryGetValue(resourceId, out int value) ? value : 0;

    public void Add(StringName resourceId, int value)
    {
        Debug.Assert(IsKnown(resourceId), $"Unknown resource id: {resourceId}");
        if (value <= 0)
            return;
        _amounts[resourceId] = Amount(resourceId) + value;
        EmitSignalChanged(resourceId, _amounts[resourceId]);
    }

    /// <summary>Returns false (and changes nothing) if the wallet cannot afford it.</summary>
    public bool Spend(StringName resourceId, int value)
    {
        if (value < 0 || Amount(resourceId) < value)
            return false;
        _amounts[resourceId] = Amount(resourceId) - value;
        EmitSignalChanged(resourceId, _amounts[resourceId]);
        return true;
    }

    public JsonObject ToSaveData()
    {
        var output = new JsonObject();
        foreach (StringName id in All)
            output[id] = Amount(id);
        return output;
    }

    public void LoadSaveData(JsonObject data)
    {
        _amounts.Clear();
        foreach (StringName id in All)
            _amounts[id] = Mathf.Max(0, JsonNodes.Int(data[id]));
    }
}
