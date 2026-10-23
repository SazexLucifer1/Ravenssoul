using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests;

/// <summary>
/// Base class for automated tests. Any public method named Test* is a test;
/// it may return Task and await. Nodes added with AddNode() are freed after
/// each test. Engine/script errors logged during a test fail it unless
/// ExpectEngineErrors() was called.
/// </summary>
public abstract class TestCase
{
    public SceneTree Tree { get; set; }
    public List<string> Failures { get; } = new();
    public int AllowedEngineErrors { get; private set; }

    private readonly List<Node> _ownedNodes = new();

    public virtual Task BeforeEach() => Task.CompletedTask;

    public virtual Task AfterEach() => Task.CompletedTask;

    // --- assertions -------------------------------------------------------------

    public void Fail(string message) => Failures.Add(message);

    public void AssertTrue(bool condition, string message = "expected true")
    {
        if (!condition)
            Fail(message);
    }

    public void AssertFalse(bool condition, string message = "expected false")
    {
        if (condition)
            Fail(message);
    }

    /// <summary>
    /// Deep equality: sequences element by element, JSON by value (1 == 1.0),
    /// numbers by value across numeric types; a JSON node also matches a plain
    /// value or array with the same JSON form.
    /// </summary>
    public void AssertEq(object actual, object expected, string message = "")
    {
        if (!Same(actual, expected))
            Fail($"{Prefix(message)}expected <{Describe(expected)}> but got <{Describe(actual)}>");
    }

    public void AssertNe(object actual, object unexpected, string message = "")
    {
        if (Same(actual, unexpected))
            Fail($"{Prefix(message)}did not expect <{Describe(actual)}>");
    }

    public void AssertNull(object value, string message = "expected null")
    {
        if (value != null)
            Fail(message);
    }

    public void AssertNotNull(object value, string message = "expected a value")
    {
        if (value == null)
            Fail(message);
    }

    public void AssertEmpty(IEnumerable collection, string message = "")
    {
        if (collection.Cast<object>().Any())
            Fail($"{Prefix(message)}expected empty, got {Describe(collection)}");
    }

    public void AssertContains(object collection, object item, string message = "")
    {
        bool found = collection switch
        {
            string text => text.Contains(item?.ToString() ?? ""),
            IEnumerable items => items.Cast<object>().Any(element => Same(element, item)),
            _ => false,
        };
        if (!found)
            Fail($"{Prefix(message)}{Describe(collection)} does not contain {Describe(item)}");
    }

    // --- helpers ----------------------------------------------------------------

    /// <summary>Declares that the current test deliberately triggers engine errors.</summary>
    public void ExpectEngineErrors(int count) => AllowedEngineErrors += count;

    public T AddNode<T>(T node, Node parent = null) where T : Node
    {
        (parent ?? Tree.Root).AddChild(node);
        _ownedNodes.Add(node);
        return node;
    }

    public T Instantiate<T>(string path, Node parent = null) where T : Node =>
        AddNode(GD.Load<PackedScene>(path).Instantiate<T>(), parent);

    public async Task WaitFrames(int count = 1)
    {
        for (int i = 0; i < count; i++)
            await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    public async Task WaitSeconds(double seconds)
    {
        await Tree.ToSignal(Tree.CreateTimer(seconds, true, false, true), SceneTreeTimer.SignalName.Timeout);
    }

    /// <summary>Polls <paramref name="condition"/> every frame. Returns false on timeout.</summary>
    public async Task<bool> WaitUntil(Func<bool> condition, double timeoutSeconds = 5.0)
    {
        ulong deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000.0);
        while (!condition())
        {
            if (Time.GetTicksMsec() > deadline)
                return false;
            await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        }
        return true;
    }

    /// <summary>Sends an action press (and release) through the input pipeline.</summary>
    public static void PressAction(StringName action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        Input.FlushBufferedEvents();
    }

    public void FreeOwnedNodes()
    {
        foreach (Node node in _ownedNodes)
        {
            if (!GodotObject.IsInstanceValid(node))
                continue;
            node.GetParent()?.RemoveChild(node);
            node.QueueFree();
        }
        _ownedNodes.Clear();
    }

    // --- equality ---------------------------------------------------------------

    public static bool Same(object a, object b)
    {
        if (a == null || b == null)
            return a == null && b == null;
        if (a is JsonNode || b is JsonNode)
            return JsonNodes.ValuesEqual(ToJson(a), ToJson(b));
        if (IsNumber(a) && IsNumber(b))
            return Convert.ToDouble(a) == Convert.ToDouble(b);
        if (a is string || b is string)
            return a.ToString() == b.ToString() && (a is string || a is StringName) && (b is string || b is StringName);
        if (a is StringName || b is StringName)
            return a.ToString() == b.ToString() && (a is StringName or string) && (b is StringName or string);
        if (a is IDictionary dictA && b is IDictionary dictB)
        {
            if (dictA.Count != dictB.Count)
                return false;
            foreach (DictionaryEntry entry in dictA)
                if (!dictB.Contains(entry.Key) || !Same(entry.Value, dictB[entry.Key]))
                    return false;
            return true;
        }
        if (a is IEnumerable seqA && b is IEnumerable seqB)
        {
            object[] left = seqA.Cast<object>().ToArray();
            object[] right = seqB.Cast<object>().ToArray();
            return left.Length == right.Length && left.Zip(right).All(pair => Same(pair.First, pair.Second));
        }
        return a.Equals(b);
    }

    private static bool IsNumber(object value) =>
        value is int or long or short or byte or uint or ulong or float or double or decimal;

    private static JsonNode ToJson(object value) => value switch
    {
        null => null,
        JsonNode node => node,
        StringName name => JsonValue.Create(name.ToString()),
        _ => JsonSerializer.SerializeToNode(value),
    };

    public static string Describe(object value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        JsonNode node => node.ToJsonString(),
        IDictionary dict => "{" + string.Join(", ", dict.Cast<DictionaryEntry>().Select(e => $"{Describe(e.Key)}: {Describe(e.Value)}")) + "}",
        IEnumerable items => "[" + string.Join(", ", items.Cast<object>().Select(Describe)) + "]",
        _ => value.ToString(),
    };

    private static string Prefix(string message) => string.IsNullOrEmpty(message) ? "" : message + ": ";
}
