using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Utopia.Automation;

/// <summary>
/// Validates JSON values against the JSON-Schema subset used in methods.json:
/// type, properties, required, additionalProperties(false), enum, minimum,
/// maximum, minLength, maxLength, pattern, items, minItems, maxItems.
/// Returns a list of problems (empty = valid). Depth- and size-limited.
/// </summary>
public static class AutomationSchemaValidator
{
    public const int MaxDepth = 12;

    public static List<string> Validate(JsonNode value, JsonObject schema, string path = "params", int depth = 0)
    {
        var problems = new List<string>();
        if (depth > MaxDepth)
        {
            problems.Add($"{path}: nested too deeply");
            return problems;
        }
        string expected = JsonNodes.Str(schema["type"]);
        if (expected.Length > 0 && !TypeMatches(value, expected))
        {
            problems.Add($"{path}: expected {expected}");
            return problems;
        }
        if (schema["enum"] is JsonArray options && !options.Any(option => AutomationPredicate.ValuesEqual(option, value)))
            problems.Add($"{path}: must be one of {string.Join(", ", options.Select(o => JsonNodes.Str(o, JsonNodes.Stringify(o))))}");
        switch (expected)
        {
            case "string":
            {
                string s = value.GetValue<string>();
                if (s.Length < JsonNodes.Int(schema["minLength"]))
                    problems.Add($"{path}: too short");
                if (schema["maxLength"] != null && s.Length > JsonNodes.Int(schema["maxLength"]))
                    problems.Add($"{path}: too long");
                if (schema["pattern"] != null && !Regex.IsMatch(s, JsonNodes.Str(schema["pattern"])))
                    problems.Add($"{path}: invalid format");
                break;
            }
            case "integer" or "number":
            {
                double n = JsonNodes.Double(value);
                if (schema["minimum"] != null && n < JsonNodes.Double(schema["minimum"]))
                    problems.Add($"{path}: below minimum {JsonNodes.Stringify(schema["minimum"])}");
                if (schema["maximum"] != null && n > JsonNodes.Double(schema["maximum"]))
                    problems.Add($"{path}: above maximum {JsonNodes.Stringify(schema["maximum"])}");
                break;
            }
            case "array":
            {
                var array = (JsonArray)value;
                if (array.Count < JsonNodes.Int(schema["minItems"]))
                    problems.Add($"{path}: too few items");
                if (schema["maxItems"] != null && array.Count > JsonNodes.Int(schema["maxItems"]))
                    problems.Add($"{path}: too many items");
                if (schema["items"] is JsonObject itemSchema)
                    for (int i = 0; i < array.Count; i++)
                        problems.AddRange(Validate(array[i], itemSchema, $"{path}[{i}]", depth + 1));
                break;
            }
            case "object":
            {
                var obj = (JsonObject)value;
                var props = schema["properties"] as JsonObject ?? new JsonObject();
                foreach (JsonNode key in schema["required"] as JsonArray ?? new JsonArray())
                    if (!obj.ContainsKey(JsonNodes.Str(key)))
                        problems.Add($"{path}.{JsonNodes.Str(key)}: required");
                bool closed = schema["additionalProperties"] is JsonValue additional && JsonNodes.IsBool(additional) && !additional.GetValue<bool>();
                foreach ((string key, JsonNode child) in obj)
                {
                    if (props[key] is JsonObject childSchema)
                        problems.AddRange(Validate(child, childSchema, $"{path}.{key}", depth + 1));
                    else if (closed)
                        problems.Add($"{path}.{key}: unknown field");
                }
                break;
            }
        }
        return problems;
    }

    private static bool TypeMatches(JsonNode value, string expected)
    {
        switch (expected)
        {
            case "object": return value is JsonObject;
            case "array": return value is JsonArray;
            case "string": return JsonNodes.IsString(value);
            case "boolean": return JsonNodes.IsBool(value);
            case "number": return JsonNodes.TryNumber(value, out _);
            case "integer":
                // JSON numbers may arrive as 3.0; accept integral values only.
                return JsonNodes.TryNumber(value, out double n) && Math.Abs(n - Math.Round(n)) < 1e-9 && Math.Abs(n) < 9.0e15;
        }
        return true;
    }
}
