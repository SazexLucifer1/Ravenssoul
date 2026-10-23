using System.Text.Json.Nodes;

namespace Utopia;

/// <summary>
/// The only contract game code knows about the remote automation server.
/// The server class itself lives in res://automation/ and is compiled out of
/// release builds; AutomationGate finds it by type name at runtime.
/// </summary>
public interface IAutomationServer
{
    /// <summary>Keys are documented on the server's Configure method (docs/AUTOMATION.md).</summary>
    void Configure(JsonObject config);

    /// <summary>Returns "" on success, else a developer-facing reason.</summary>
    string Start();
}
