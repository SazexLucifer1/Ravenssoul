using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Utopia;

/// <summary>
/// Upgrades older save payloads to the current schema, one version at a time.
///
/// To change the save format: bump SaveService.SchemaVersion, then add a
/// <c>Migrate&lt;old&gt;To&lt;new&gt;(data)</c> function here and register it in Steps.
/// Never edit an existing step after release.
/// </summary>
public static class SaveMigrations
{
    /// <summary>from_version -> function producing from_version + 1.</summary>
    public static readonly IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> Steps =
        new Dictionary<int, Func<JsonObject, JsonObject>>();

    /// <summary>Returns the migrated payload, or null when a step is missing.</summary>
    public static JsonObject Migrate(JsonObject data, int fromVersion, int toVersion)
    {
        var current = (JsonObject)data.DeepClone();
        for (int version = fromVersion; version < toVersion; version++)
        {
            if (!Steps.TryGetValue(version, out Func<JsonObject, JsonObject> step))
                return null;
            current = step(current);
        }
        return current;
    }
}
