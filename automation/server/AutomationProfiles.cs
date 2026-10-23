using System.Collections.Generic;
using System.Linq;

namespace Utopia.Automation;

/// <summary>
/// Capability profiles. A method runs only if its capability (methods.json)
/// is granted by the active profile. See the matrix in docs/AUTOMATION.md.
/// </summary>
public static class AutomationProfiles
{
    public const string Dev = "dev";
    public const string Qa = "qa";
    public const string Production = "production";

    public static readonly IReadOnlyList<string> QaCapabilities = new[]
    {
        "session", "inspect", "observe.screenshot", "observe.logs", "observe.events",
        "observe.perf", "act.semantic", "act.input", "wait", "assert", "checkpoint",
        "reset", "fixture", "recording", "app.quit",
    };
    public static readonly IReadOnlyList<string> DevOnly = new[] { "inspect.deep", "dev.teleport", "dev.spawn", "dev.command" };
    /// <summary>Production never starts the server (see AutomationGate); listed for the matrix.</summary>
    public static readonly IReadOnlyList<string> ProductionCapabilities = new string[0];

    public static List<string> Capabilities(string profile) => profile switch
    {
        Dev => QaCapabilities.Concat(DevOnly).ToList(),
        Qa => QaCapabilities.ToList(),
        _ => ProductionCapabilities.ToList(),
    };

    public static bool IsKnown(string profile) => profile is Dev or Qa or Production;
}
