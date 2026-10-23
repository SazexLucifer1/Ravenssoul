using Godot;

namespace Utopia;

/// <summary>Maps stable route ids to scene paths so callers never hard-code scene files.</summary>
[GlobalClass]
public partial class RouteTable : Resource
{
    [Export] public Godot.Collections.Dictionary<StringName, string> Routes { get; set; } = new();

    public bool HasRoute(StringName route) => Routes.ContainsKey(route);

    public string PathFor(StringName route) => Routes.TryGetValue(route, out string path) ? path : "";
}
