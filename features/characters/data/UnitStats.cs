using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Additive stat block shared by heroes, battalions, and enemies.
///
/// A battlefield unit is hero + battalion, so stats combine by addition
/// (see <see cref="Combine"/>). Keep every field an int so combination and saving
/// stay trivial and deterministic.
/// </summary>
[GlobalClass]
public partial class UnitStats : Resource
{
    [Export(PropertyHint.Range, "0,999")] public int MaxHealth { get; set; }
    [Export(PropertyHint.Range, "0,999")] public int PhysicalDamage { get; set; }
    [Export(PropertyHint.Range, "0,999")] public int MagicalDamage { get; set; }
    [Export(PropertyHint.Range, "0,20")] public int Movement { get; set; }
    [Export(PropertyHint.Range, "0,20")] public int CardDraw { get; set; }
    [Export(PropertyHint.Range, "0,20")] public int MaxHandSize { get; set; }
    [Export(PropertyHint.Range, "0,20")] public int MaxAp { get; set; }
    [Export(PropertyHint.Range, "-100,100")] public int Morale { get; set; }
    [Export(PropertyHint.Range, "0,999")] public int Speed { get; set; }

    public static UnitStats Combine(UnitStats a, UnitStats b) => new()
    {
        MaxHealth = a.MaxHealth + b.MaxHealth,
        PhysicalDamage = a.PhysicalDamage + b.PhysicalDamage,
        MagicalDamage = a.MagicalDamage + b.MagicalDamage,
        Movement = a.Movement + b.Movement,
        CardDraw = a.CardDraw + b.CardDraw,
        MaxHandSize = a.MaxHandSize + b.MaxHandSize,
        MaxAp = a.MaxAp + b.MaxAp,
        Morale = a.Morale + b.Morale,
        Speed = a.Speed + b.Speed,
    };

    /// <summary>Stable snake_case field ids (automation, analytics); never shown to players.</summary>
    public Dictionary<string, int> ToDict() => new()
    {
        ["max_health"] = MaxHealth,
        ["physical_damage"] = PhysicalDamage,
        ["magical_damage"] = MagicalDamage,
        ["movement"] = Movement,
        ["card_draw"] = CardDraw,
        ["max_hand_size"] = MaxHandSize,
        ["max_ap"] = MaxAp,
        ["morale"] = Morale,
        ["speed"] = Speed,
    };
}
