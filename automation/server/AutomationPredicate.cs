using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Utopia.Automation;

/// <summary>
/// Declarative conditions for waits and assertions. No code is evaluated.
///
///   {"source": "state", "path": "unit.cell", "op": "eq", "value": [10, 1]}
///   {"source": "entity", "id": "main_menu.new_game", "path": "enabled", "op": "eq", "value": true}
///   {"all": [...]}, {"any": [...]}, {"not": {...}}
/// Ops: eq ne gt gte lt lte contains in exists not_exists.
/// </summary>
public static class AutomationPredicate
{
    public static readonly IReadOnlyList<string> Ops = new[] { "eq", "ne", "gt", "gte", "lt", "lte", "contains", "in", "exists", "not_exists" };
    public const int MaxNodes = 64;
    public const int MaxDepth = 6;

    private static readonly string[] Fields = { "source", "id", "path", "op", "value" };
    private static readonly string[] Sources = { "state", "entity", "scene" };

    public readonly record struct Outcome(bool Passed, JsonNode Actual);

    /// <summary>Returns problems with the predicate's shape (empty = valid).</summary>
    public static List<string> Validate(JsonNode condition)
    {
        int counter = 0;
        return Validate(condition, 0, ref counter);
    }

    private static List<string> Validate(JsonNode condition, int depth, ref int counter)
    {
        var problems = new List<string>();
        counter++;
        if (depth > MaxDepth || counter > MaxNodes)
        {
            problems.Add("condition too large");
            return problems;
        }
        if (condition is not JsonObject c)
        {
            problems.Add("condition must be an object");
            return problems;
        }
        foreach (string combinator in new[] { "all", "any" })
        {
            if (!c.ContainsKey(combinator))
                continue;
            if (c[combinator] is not JsonArray children || children.Count == 0 || c.Count != 1)
            {
                problems.Add($"{combinator} must be the only key and a non-empty list");
                return problems;
            }
            foreach (JsonNode child in children)
                problems.AddRange(Validate(child, depth + 1, ref counter));
            return problems;
        }
        if (c.ContainsKey("not"))
        {
            if (c.Count != 1)
            {
                problems.Add("not must be the only key");
                return problems;
            }
            return Validate(c["not"], depth + 1, ref counter);
        }
        foreach ((string key, _) in c)
            if (!Fields.Contains(key))
                problems.Add($"unknown condition field '{key}'");
        string source = JsonNodes.Str(c["source"]);
        if (!Sources.Contains(source))
            problems.Add("source must be state, entity or scene");
        if (source == "entity" && !JsonNodes.IsString(c["id"]))
            problems.Add("entity conditions need an id");
        if (!JsonNodes.IsString(c["path"]) || JsonNodes.Str(c["path"]).Length > 128)
            problems.Add("path must be a string");
        string op = JsonNodes.Str(c["op"]);
        if (!Ops.Contains(op))
            problems.Add($"op must be one of {string.Join(", ", Ops)}");
        if (op is not ("exists" or "not_exists") && !c.ContainsKey("value"))
            problems.Add($"value required for op {op}");
        return problems;
    }

    /// <summary><paramref name="resolve"/> maps (source, id) to the root object (or null).</summary>
    public static Outcome Evaluate(JsonObject condition, Func<string, string, JsonNode> resolve)
    {
        if (condition["all"] is JsonArray all)
        {
            foreach (JsonNode child in all)
            {
                Outcome r = Evaluate((JsonObject)child, resolve);
                if (!r.Passed)
                    return r;
            }
            return new Outcome(true, null);
        }
        if (condition["any"] is JsonArray any)
        {
            var last = new Outcome(false, null);
            foreach (JsonNode child in any)
            {
                last = Evaluate((JsonObject)child, resolve);
                if (last.Passed)
                    return last;
            }
            return last;
        }
        if (condition.ContainsKey("not"))
        {
            Outcome inner = Evaluate((JsonObject)condition["not"], resolve);
            return new Outcome(!inner.Passed, inner.Actual);
        }
        JsonNode root = resolve(JsonNodes.Str(condition["source"]), JsonNodes.Str(condition["id"]));
        (bool exists, JsonNode actual) = Lookup(root, JsonNodes.Str(condition["path"]));
        JsonNode expected = condition["value"];
        bool passed = false;
        switch (JsonNodes.Str(condition["op"]))
        {
            case "exists": passed = exists; break;
            case "not_exists": passed = !exists; break;
            case "eq": passed = exists && ValuesEqual(actual, expected); break;
            case "ne": passed = !exists || !ValuesEqual(actual, expected); break;
            case "gt": passed = exists && Compare(actual, expected, (a, b) => a > b); break;
            case "gte": passed = exists && Compare(actual, expected, (a, b) => a >= b); break;
            case "lt": passed = exists && Compare(actual, expected, (a, b) => a < b); break;
            case "lte": passed = exists && Compare(actual, expected, (a, b) => a <= b); break;
            case "contains":
                if (JsonNodes.IsString(actual))
                    passed = actual.GetValue<string>().Contains(JsonNodes.Str(expected, JsonNodes.Stringify(expected)));
                else if (actual is JsonArray array)
                    passed = array.Any(v => ValuesEqual(v, expected));
                else if (actual is JsonObject obj)
                    passed = JsonNodes.IsString(expected) && obj.ContainsKey(expected.GetValue<string>());
                break;
            case "in":
                passed = exists && expected is JsonArray choices && choices.Any(v => ValuesEqual(actual, v));
                break;
        }
        return new Outcome(passed, JsonNodes.Copy(actual));
    }

    /// <summary>Dotted path lookup: "unit.cell.0", "screens.0.id". Returns (exists, value).</summary>
    public static (bool Exists, JsonNode Value) Lookup(JsonNode root, string path)
    {
        if (path.Length == 0)
            return (root != null, root);
        JsonNode current = root;
        foreach (string part in path.Split('.'))
        {
            if (current is JsonObject obj && obj.ContainsKey(part))
                current = obj[part];
            else if (current is JsonArray array && int.TryParse(part, out int index) && index >= 0 && index < array.Count)
                current = array[index];
            else
                return (false, null);
        }
        return (true, current);
    }

    private static bool Compare(JsonNode a, JsonNode b, Func<double, double, bool> op) =>
        JsonNodes.TryNumber(a, out double x) && JsonNodes.TryNumber(b, out double y) && op(x, y);

    /// <summary>JSON equality where 1 == 1.0 (see JsonNodes.ValuesEqual).</summary>
    public static bool ValuesEqual(JsonNode a, JsonNode b) => JsonNodes.ValuesEqual(a, b);
}
