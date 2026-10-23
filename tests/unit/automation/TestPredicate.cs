using System.Linq;
using System.Text.Json.Nodes;
using Utopia.Automation;

namespace Utopia.Tests.Unit.Automation;

public class TestPredicate : TestCase
{
    private static readonly JsonNode State = JsonNode.Parse(
        "{\"unit\": {\"cell\": [0, 10], \"health\": 16}, \"screens\": [\"screen.pause\"], \"route\": \"gameplay\", \"paused\": true}");

    private static bool Eval(JsonObject condition) => AutomationPredicate.Evaluate(condition, (_, _) => State).Passed;

    private static JsonObject C(string path, string op, JsonNode value = null)
    {
        var c = new JsonObject { ["source"] = "state", ["path"] = path, ["op"] = op };
        if (value != null)
            c["value"] = value;
        return c;
    }

    public void TestComparisonOps()
    {
        AssertTrue(Eval(C("unit.cell", "eq", new JsonArray(0.0, 10.0))), "JSON floats equal ints");
        AssertTrue(Eval(C("unit.health", "gt", 10)));
        AssertFalse(Eval(C("unit.health", "lt", 10)));
        AssertTrue(Eval(C("unit.health", "lte", 16)));
        AssertTrue(Eval(C("screens", "contains", "screen.pause")));
        AssertTrue(Eval(C("route", "in", new JsonArray("main_menu", "gameplay"))));
        AssertTrue(Eval(C("route", "ne", "main_menu")));
        AssertTrue(Eval(C("unit.cell.1", "eq", 10)), "array index path");
        AssertTrue(Eval(C("missing.path", "not_exists")));
        AssertFalse(Eval(C("missing.path", "eq", 0)));
    }

    public void TestCombinators()
    {
        AssertTrue(Eval(new JsonObject { ["all"] = new JsonArray(C("paused", "eq", true), C("route", "eq", "gameplay")) }));
        AssertFalse(Eval(new JsonObject { ["all"] = new JsonArray(C("paused", "eq", true), C("route", "eq", "main_menu")) }));
        AssertTrue(Eval(new JsonObject { ["any"] = new JsonArray(C("route", "eq", "x"), C("route", "eq", "gameplay")) }));
        AssertTrue(Eval(new JsonObject { ["not"] = C("route", "eq", "x") }));
    }

    public void TestActualValueIsReported()
    {
        AutomationPredicate.Outcome outcome = AutomationPredicate.Evaluate(C("unit.cell", "eq", new JsonArray(1, 10)), (_, _) => State);
        AssertFalse(outcome.Passed);
        AssertEq(outcome.Actual, new[] { 0, 10 });
    }

    public void TestValidationRejectsMalformedConditions()
    {
        AssertEmpty(AutomationPredicate.Validate(C("a", "eq", 1)));
        AssertTrue(AutomationPredicate.Validate("string").Any());
        AssertTrue(AutomationPredicate.Validate(JsonNode.Parse("{\"source\": \"state\", \"path\": \"a\", \"op\": \"regex\", \"value\": 1}")).Any(), "unknown op");
        AssertTrue(AutomationPredicate.Validate(JsonNode.Parse("{\"source\": \"disk\", \"path\": \"a\", \"op\": \"eq\", \"value\": 1}")).Any(), "unknown source");
        AssertTrue(AutomationPredicate.Validate(JsonNode.Parse("{\"source\": \"entity\", \"path\": \"a\", \"op\": \"eq\", \"value\": 1}")).Any(), "entity needs id");
        AssertTrue(AutomationPredicate.Validate(JsonNode.Parse("{\"source\": \"state\", \"path\": \"a\", \"op\": \"eq\"}")).Any(), "missing value");
        AssertTrue(AutomationPredicate.Validate(JsonNode.Parse("{\"all\": []}")).Any(), "empty all");
        AssertTrue(AutomationPredicate.Validate(JsonNode.Parse("{\"source\": \"state\", \"path\": \"a\", \"op\": \"eq\", \"value\": 1, \"code\": \"x\"}")).Any(), "extra field");
    }

    public void TestSizeAndDepthLimits()
    {
        JsonObject deep = C("a", "exists");
        for (int i = 0; i < 10; i++)
            deep = new JsonObject { ["not"] = deep };
        AssertTrue(AutomationPredicate.Validate(deep).Any(), "too deep");
        var wide = new JsonArray();
        for (int i = 0; i < 100; i++)
            wide.Add(C("a", "exists"));
        AssertTrue(AutomationPredicate.Validate(new JsonObject { ["all"] = wide }).Any(), "too many nodes");
    }
}
