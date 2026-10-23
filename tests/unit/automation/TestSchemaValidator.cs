using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Utopia.Automation;

namespace Utopia.Tests.Unit.Automation;

public class TestSchemaValidator : TestCase
{
    private static readonly JsonObject Schema = (JsonObject)JsonNode.Parse("""
        {
          "type": "object", "additionalProperties": false, "required": ["name"],
          "properties": {
            "name": {"type": "string", "minLength": 1, "maxLength": 8, "pattern": "^[a-z_]+$"},
            "count": {"type": "integer", "minimum": 1, "maximum": 5},
            "mode": {"type": "string", "enum": ["tap", "down"]},
            "cell": {"type": "array", "items": {"type": "integer"}, "minItems": 2, "maxItems": 2},
            "flag": {"type": "boolean"}
          }
        }
        """);

    private static List<string> Problems(string json) => AutomationSchemaValidator.Validate(JsonNode.Parse(json), Schema);

    public void TestValidPayload() =>
        AssertEmpty(Problems("{\"name\": \"ok_name\", \"count\": 3.0, \"mode\": \"tap\", \"cell\": [1, 2], \"flag\": true}"));

    public void TestRejectsWrongTypesAndMissingFields()
    {
        AssertTrue(Problems("[]").Any(), "array instead of object");
        AssertTrue(Problems("{}").Any(), "missing required");
        AssertTrue(Problems("{\"name\": 5}").Any(), "number instead of string");
        AssertTrue(Problems("{\"name\": \"a\", \"count\": 1.5}").Any(), "non-integral integer");
        AssertTrue(Problems("{\"name\": \"a\", \"flag\": \"yes\"}").Any(), "string instead of boolean");
    }

    public void TestRejectsUnknownFieldsAndOutOfRangeValues()
    {
        AssertTrue(Problems("{\"name\": \"a\", \"evil\": true}").Any(), "unknown field");
        AssertTrue(Problems("{\"name\": \"a\", \"count\": 9}").Any(), "above maximum");
        AssertTrue(Problems("{\"name\": \"a\", \"mode\": \"hold\"}").Any(), "not in enum");
        AssertTrue(Problems("{\"name\": \"../x\"}").Any(), "pattern");
        AssertTrue(Problems("{\"name\": \"abcdefghijk\"}").Any(), "too long");
        AssertTrue(Problems("{\"name\": \"a\", \"cell\": [1]}").Any(), "too few items");
        AssertTrue(Problems("{\"name\": \"a\", \"cell\": [1, \"x\"]}").Any(), "item type");
    }

    public void TestEveryMethodSchemaIsWellFormed()
    {
        string[] known = { "object", "string", "integer", "number", "boolean", "array" };
        JsonObject methods = AutomationProtocol.Methods();
        AssertTrue(methods.Count >= 30, "method table loaded");
        foreach ((string name, JsonNode spec) in methods)
        {
            var parameters = (JsonObject)spec["params"];
            AssertEq(JsonNodes.Str(parameters["type"]), "object", name);
            AssertEq(parameters["additionalProperties"], false, $"{name} must reject unknown params");
            foreach ((string prop, JsonNode propSchema) in parameters["properties"] as JsonObject ?? new JsonObject())
                AssertTrue(known.Contains(JsonNodes.Str(propSchema["type"])), $"{name}.{prop} has a known type");
        }
        AssertEmpty(AutomationSchemaValidator.Validate(new JsonObject(), new JsonObject { ["type"] = "object" }));
    }
}
