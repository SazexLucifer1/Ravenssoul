using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Captures engine/script messages for observe.logs, with local filesystem
/// paths redacted. Callbacks can arrive from any thread, hence the lock.
/// </summary>
public partial class AutomationLogCapture : Logger
{
    private readonly object _lock = new();
    private readonly LinkedList<JsonObject> _lines = new();
    private readonly List<(string Path, string Label)> _redactions = new();
    private long _seq;

    public int ErrorCount { get; private set; }

    public AutomationLogCapture()
    {
        foreach ((string path, string label) in new[]
        {
            (OS.GetUserDataDir(), "<user_data>"),
            (ProjectSettings.GlobalizePath("res://").TrimEnd('/'), "<project>"),
            (OS.GetExecutablePath().GetBaseDir(), "<engine>"),
            (OS.GetEnvironment("HOME"), "<home>"),
        })
        {
            if (path.Length > 1)
                _redactions.Add((path, label));
        }
    }

    public override void _LogMessage(string message, bool error) => Add(error ? "error" : "info", message.Trim());

    public override void _LogError(string function, string file, int line, string code, string rationale,
        bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
    {
        string level = errorType == (int)ErrorType.Warning ? "warning" : "error";
        string text = rationale.Length > 0 ? rationale : code;
        Add(level, $"{text} ({file.GetFile()}:{line} {function})");
    }

    public string Redact(string text)
    {
        foreach ((string path, string label) in _redactions)
            text = text.Replace(path, label);
        return text;
    }

    public JsonObject LinesSince(long seq, string minLevel, int limit)
    {
        var rank = new Dictionary<string, int> { ["info"] = 0, ["warning"] = 1, ["error"] = 2 };
        int minimum = rank.TryGetValue(minLevel, out int r) ? r : 0;
        lock (_lock)
        {
            var output = new JsonArray();
            foreach (JsonObject entry in _lines)
            {
                if (JsonNodes.Long(entry["seq"]) > seq && rank[JsonNodes.Str(entry["level"])] >= minimum)
                {
                    output.Add(entry.DeepClone());
                    if (output.Count >= limit)
                        break;
                }
            }
            return new JsonObject { ["lines"] = output, ["last_seq"] = _seq };
        }
    }

    private void Add(string level, string message)
    {
        if (message.Length == 0)
            return;
        lock (_lock)
        {
            _seq++;
            if (level == "error")
                ErrorCount++;
            string redacted = Redact(message);
            _lines.AddLast(new JsonObject
            {
                ["seq"] = _seq,
                ["level"] = level,
                ["message"] = redacted.Length > 1000 ? redacted[..1000] : redacted,
                ["t_ms"] = (long)Time.GetTicksMsec(),
            });
            if (_lines.Count > AutomationProtocol.LogBuffer)
                _lines.RemoveFirst();
        }
    }
}
