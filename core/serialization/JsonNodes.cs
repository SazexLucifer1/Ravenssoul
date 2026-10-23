using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Utopia;

/// <summary>
/// Small, forgiving helpers around System.Text.Json nodes. Saves, fixtures,
/// and the automation protocol all exchange plain JSON; these helpers read
/// values with a default instead of throwing on missing or mistyped data.
/// </summary>
public static class JsonNodes
{
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Parses JSON text. Returns null (and the reason) on failure.</summary>
    public static JsonNode Parse(string text, out string error)
    {
        error = "";
        try
        {
            JsonNode node = JsonNode.Parse(text);
            if (node == null)
                error = "empty document";
            return node;
        }
        catch (JsonException e)
        {
            error = $"JSON error at line {(e.LineNumber ?? 0) + 1}: {e.Message}";
            return null;
        }
    }

    public static JsonNode Parse(string text) => Parse(text, out _);

    public static string Stringify(JsonNode node, bool indented = false) =>
        node == null ? "null" : node.ToJsonString(indented ? Indented : Compact);

    public static bool IsNumber(JsonNode node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number;

    public static bool IsString(JsonNode node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String;

    public static bool IsBool(JsonNode node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False;

    /// <summary>
    /// Reads any JSON number, whether parsed from text or created in code
    /// (JsonValue.Create(5) does not convert to double on its own).
    /// </summary>
    public static bool TryNumber(JsonNode node, out double value)
    {
        value = 0.0;
        if (node is not JsonValue v)
            return false;
        if (v.TryGetValue(out double d)) { value = d; return true; }
        if (v.TryGetValue(out int i)) { value = i; return true; }
        if (v.TryGetValue(out long l)) { value = l; return true; }
        if (v.TryGetValue(out float f)) { value = f; return true; }
        if (v.TryGetValue(out decimal m)) { value = (double)m; return true; }
        if (v.TryGetValue(out JsonElement e) && e.ValueKind == JsonValueKind.Number) { value = e.GetDouble(); return true; }
        return false;
    }

    public static double Double(JsonNode node, double fallback = 0.0) =>
        TryNumber(node, out double value) ? value : fallback;

    /// <summary>Integral JSON numbers (also "50.0") as int; anything else gives the fallback.</summary>
    public static int Int(JsonNode node, int fallback = 0)
    {
        if (!TryNumber(node, out double value))
            return fallback;
        return value is >= int.MinValue and <= int.MaxValue ? (int)Math.Round(value) : fallback;
    }

    public static long Long(JsonNode node, long fallback = 0)
    {
        if (!TryNumber(node, out double value))
            return fallback;
        return Math.Abs(value) < 9.0e15 ? (long)Math.Round(value) : fallback;
    }

    public static string Str(JsonNode node, string fallback = "") =>
        IsString(node) ? node.GetValue<string>() : fallback;

    public static bool Bool(JsonNode node, bool fallback = false) =>
        IsBool(node) ? node.GetValue<bool>() : fallback;

    /// <summary>A detached deep copy (nodes can only have one parent).</summary>
    public static JsonNode Copy(JsonNode node) => node?.DeepClone();

    /// <summary>JSON equality where 1 == 1.0 (JSON numbers have no int/float distinction).</summary>
    public static bool ValuesEqual(JsonNode a, JsonNode b)
    {
        if (TryNumber(a, out double x) && TryNumber(b, out double y))
            return Math.Abs(x - y) <= 1e-5 * Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y)));
        if (a is JsonArray arrayA && b is JsonArray arrayB)
        {
            if (arrayA.Count != arrayB.Count)
                return false;
            for (int i = 0; i < arrayA.Count; i++)
                if (!ValuesEqual(arrayA[i], arrayB[i]))
                    return false;
            return true;
        }
        if (a is JsonObject objA && b is JsonObject objB)
        {
            if (objA.Count != objB.Count)
                return false;
            foreach ((string key, JsonNode value) in objA)
                if (!objB.ContainsKey(key) || !ValuesEqual(value, objB[key]))
                    return false;
            return true;
        }
        if (a == null || b == null)
            return a == null && b == null;
        if (IsString(a) && IsString(b))
            return a.GetValue<string>() == b.GetValue<string>();
        if (IsBool(a) && IsBool(b))
            return a.GetValue<bool>() == b.GetValue<bool>();
        return false;
    }
}
