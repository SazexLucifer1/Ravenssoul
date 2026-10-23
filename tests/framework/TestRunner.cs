using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests;

/// <summary>
/// Discovers and runs every TestCase subclass under tests/unit and tests/integration.
///
///   godot --headless --path . res://tests/framework/test_runner.tscn
///   godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=save
///
/// Runs as the main scene so all autoloads exist exactly as in the game.
/// Exits with code 0 when every test passes, 1 otherwise. A test's label is
/// its folder plus class name (e.g. unit/core/TestSaveService), derived from
/// its namespace (Utopia.Tests.Unit.Core); --filter matches that label,
/// case-insensitively.
/// </summary>
public partial class TestRunner : Node
{
    private readonly ErrorCaptureLogger _logger = new();

    public override async void _Ready()
    {
        OS.AddLogger(_logger);
        // Tests own scene changes: give SceneRouter a disposable current scene so
        // routing never frees this runner.
        var placeholder = new Node { Name = "TestPlaceholderScene" };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, placeholder);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().CurrentScene = placeholder;
        int exitCode = await RunAll(Filter());
        OS.RemoveLogger(_logger);
        GetTree().Quit(exitCode);
    }

    private static string Filter()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--filter="))
                return arg["--filter=".Length..];
        return "";
    }

    public static string LabelOf(Type type)
    {
        string ns = type.Namespace ?? "";
        string folder = ns.StartsWith("Utopia.Tests.") ? ns["Utopia.Tests.".Length..].ToLowerInvariant().Replace('.', '/') : "";
        return folder.Length > 0 ? $"{folder}/{type.Name}" : type.Name;
    }

    public static List<Type> DiscoverTestTypes() =>
        typeof(TestRunner).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(TestCase)) && !t.IsAbstract)
            .Where(t => LabelOf(t).StartsWith("unit/") || LabelOf(t).StartsWith("integration/"))
            .OrderBy(LabelOf, StringComparer.Ordinal)
            .ToList();

    private async Task<int> RunAll(string filter)
    {
        int passed = 0;
        var failed = new List<string>();
        _logger.Take();
        foreach (Type type in DiscoverTestTypes())
        {
            string typeLabel = LabelOf(type);
            if (filter.Length > 0 && !typeLabel.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;
            IEnumerable<MethodInfo> tests = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.StartsWith("Test") && m.GetParameters().Length == 0)
                .OrderBy(m => m.MetadataToken);
            foreach (MethodInfo method in tests)
            {
                var test = (TestCase)Activator.CreateInstance(type);
                test.Tree = GetTree();
                ResetGlobalState();
                await Run(test, () => test.BeforeEach());
                await Run(test, () => method.Invoke(test, null) as Task);
                await Run(test, () => test.AfterEach());
                test.FreeOwnedNodes();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                List<string> errors = _logger.Take();
                if (errors.Count > test.AllowedEngineErrors)
                    foreach (string error in errors)
                        test.Fail("engine error: " + error);
                string label = $"{type.Name}::{method.Name}";
                if (test.Failures.Count == 0)
                {
                    passed++;
                    GD.Print("  PASS  ", label);
                }
                else
                {
                    failed.Add(label);
                    GD.Print("  FAIL  ", label);
                    foreach (string failure in test.Failures)
                        GD.Print("        - ", failure);
                }
            }
        }
        GD.Print($"\n{passed} passed, {failed.Count} failed");
        foreach (string label in failed)
            GD.Print("  failed: ", label);
        return failed.Count == 0 && passed > 0 ? 0 : 1;
    }

    /// <summary>Runs one step; exceptions become test failures instead of stopping the run.</summary>
    private static async Task Run(TestCase test, Func<Task> step)
    {
        try
        {
            Task task = step();
            if (task != null)
                await task;
        }
        catch (Exception e)
        {
            Exception inner = e is TargetInvocationException { InnerException: not null } wrapped ? wrapped.InnerException : e;
            test.Fail($"exception: {inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}");
        }
    }

    /// <summary>Keeps tests independent from each other and from the player's real files.</summary>
    private void ResetGlobalState()
    {
        DevLog.Muted = false;
        GetTree().Paused = false;
        SaveService.Instance.Backend = new MemorySaveBackend();
        SettingsService settings = SettingsService.Instance;
        settings.SettingsPath = "user://test_settings.cfg";
        settings.ReducedMotion = true;
        settings.TextScale = 1.0f;
        settings.Locale = "en";
        TranslationServer.SetLocale("en");
        TranslationServer.PseudolocalizationEnabled = false;
        GameSession.Instance.HasCampaign = false;
    }
}
