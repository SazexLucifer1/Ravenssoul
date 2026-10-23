using System.Collections.Generic;
using Godot;

namespace Utopia.Tests;

/// <summary>
/// Counts engine and script errors so the runner can fail the test that
/// caused them (Godot keeps running after a script error or an exception in
/// a callback, which would otherwise let a broken test look green).
/// </summary>
public partial class ErrorCaptureLogger : Logger
{
    private readonly object _lock = new();
    private readonly List<string> _errors = new();

    public override void _LogError(string function, string file, int line, string code, string rationale,
        bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
    {
        if (errorType == (int)ErrorType.Warning)
            return;
        lock (_lock)
            _errors.Add($"{code} ({file}:{line} {function}) {rationale}");
    }

    public override void _LogMessage(string message, bool error)
    {
    }

    public List<string> Take()
    {
        lock (_lock)
        {
            var copy = new List<string>(_errors);
            _errors.Clear();
            return copy;
        }
    }
}
