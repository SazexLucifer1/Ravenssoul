using System;
using System.Collections.Generic;
using System.Linq;

namespace Utopia;

/// <summary>Speed-based initiative, recalculated every round.</summary>
public static class TurnOrder
{
    /// <summary>
    /// Returns entries sorted by descending speed. Ties keep their input order
    /// (stable), so authored unit order breaks ties deterministically.
    /// </summary>
    public static List<T> SortBySpeed<T>(IEnumerable<T> entries, Func<T, int> speedOf) =>
        entries.OrderByDescending(speedOf).ToList(); // LINQ ordering is stable

    public static List<UnitStats> SortBySpeed(IEnumerable<UnitStats> entries) => SortBySpeed(entries, s => s.Speed);
}
