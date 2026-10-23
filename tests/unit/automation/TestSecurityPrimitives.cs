using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Godot;
using Utopia.Automation;
using Utopia.Tests.Tools;

namespace Utopia.Tests.Unit.Automation;

/// <summary>Auth, origin/host checks, rate limiting, capability profiles, gate.</summary>
public class TestSecurityPrimitives : TestCase
{
    private const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";

    private static Dictionary<string, string> Headers(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    public void TestTokenCheckIsExactAndExpires()
    {
        var auth = new AutomationAuth(Token, 60, "127.0.0.1", 47801);
        AssertEq(auth.CheckToken(Token), 0);
        AssertEq(auth.CheckToken(""), AutomationProtocol.Unauthorized);
        AssertEq(auth.CheckToken(Token[..47] + "X"), AutomationProtocol.Unauthorized);
        AssertEq(auth.CheckToken(Token + "0"), AutomationProtocol.Unauthorized);
        var expired = new AutomationAuth(Token, 0, "127.0.0.1", 47801);
        AssertEq(expired.CheckToken(Token), AutomationProtocol.SessionExpired);
    }

    public void TestGeneratedTokensAreLongAndUnique()
    {
        string a = AutomationAuth.GenerateToken();
        AssertTrue(a.Length >= AutomationProtocol.MinTokenLength);
        AssertNe(a, AutomationAuth.GenerateToken());
    }

    public void TestBrowserOriginAndDnsRebindingAreRejected()
    {
        var auth = new AutomationAuth(Token, 60, "127.0.0.1", 47801);
        AssertEq(auth.CheckHttpHeaders(Headers(("host", "127.0.0.1:47801"))), "");
        AssertEq(auth.CheckHttpHeaders(Headers(("host", "localhost:47801"))), "");
        AssertNe(auth.CheckHttpHeaders(Headers(("host", "evil.example:47801"))), "", "DNS rebinding host");
        AssertNe(auth.CheckHttpHeaders(Headers()), "", "missing host");
        AssertNe(auth.CheckHttpHeaders(Headers(("host", "127.0.0.1:47801"), ("origin", "https://evil.example"))), "", "browser origin");
    }

    public void TestBearerParsing()
    {
        AssertEq(AutomationAuth.BearerFrom(Headers(("authorization", "Bearer abc"))), "abc");
        AssertEq(AutomationAuth.BearerFrom(Headers(("authorization", "Basic abc"))), "");
        AssertEq(AutomationAuth.BearerFrom(Headers()), "");
    }

    public void TestRateLimiter()
    {
        var limiter = new AutomationRateLimiter(3, 0);
        AssertTrue(limiter.Allow());
        AssertTrue(limiter.Allow());
        AssertTrue(limiter.Allow());
        AssertFalse(limiter.Allow(), "burst exhausted, no refill");
    }

    public void TestCapabilityMatrix()
    {
        List<string> qa = AutomationProfiles.Capabilities(AutomationProfiles.Qa);
        List<string> dev = AutomationProfiles.Capabilities(AutomationProfiles.Dev);
        foreach (string cap in new[] { "inspect", "act.input", "observe.screenshot", "reset", "fixture" })
            AssertTrue(qa.Contains(cap), $"qa has {cap}");
        foreach (string cap in new[] { "dev.teleport", "dev.spawn", "dev.command", "inspect.deep" })
        {
            AssertFalse(qa.Contains(cap), $"qa lacks {cap}");
            AssertTrue(dev.Contains(cap), $"dev has {cap}");
        }
        AssertEmpty(AutomationProfiles.Capabilities(AutomationProfiles.Production));
        foreach ((string name, JsonNode spec) in AutomationProtocol.Methods())
            AssertTrue(dev.Contains(JsonNodes.Str(spec["capability"])), $"{name} capability is granted by some profile");
    }

    public void TestGateParsesArguments()
    {
        AssertFalse(JsonNodes.Bool(AutomationGate.ParseArgs(new[] { "--route=gameplay" })["enabled"]));
        JsonObject config = AutomationGate.ParseArgs(new[] { "--automation-profile=dev", "--automation-port=50000", "--automation-bind=0.0.0.0", "--automation-allow-remote" });
        AssertEq(config, JsonNode.Parse("{\"enabled\": true, \"profile\": \"dev\", \"port\": 50000, \"bind\": \"0.0.0.0\", \"allow_remote\": true}"));
    }

    public void TestProductionGating()
    {
        // (profile, debug build, automation_qa feature) -> allowed?
        AssertEq(AutomationGate.Refusal("qa", true, false), "");
        AssertEq(AutomationGate.Refusal("dev", true, false), "");
        AssertEq(AutomationGate.Refusal("qa", false, true), "", "QA export preset");
        AssertNe(AutomationGate.Refusal("qa", false, false), "", "release build without the feature");
        AssertNe(AutomationGate.Refusal("dev", false, true), "", "dev never in release");
        AssertNe(AutomationGate.Refusal("production", true, true), "", "production never starts the server");
        AssertNe(AutomationGate.Refusal("root", true, true), "", "unknown profile");
    }

    public void TestGateDoesNothingWithoutFlags()
    {
        AssertNull(AutomationGate.StartIfRequested(Tree, new string[0]));
        AssertNull(Tree.Root.GetNodeOrNull("Automation"));
    }

    public void TestReleaseExportExcludesAutomation()
    {
        var config = new ConfigFile();
        AssertEq(config.Parse(FileAccess.GetFileAsString("res://export_presets.cfg")), Error.Ok);
        int checkedPresets = 0;
        foreach (string section in config.GetSections())
        {
            if (section.EndsWith(".options") || !section.StartsWith("preset."))
                continue;
            string features = config.GetValue(section, "custom_features", "").AsString();
            if (features.Contains(AutomationGate.QaFeature))
                continue;
            checkedPresets++;
            AssertTrue(config.GetValue(section, "exclude_filter", "").AsString().Contains("automation/*"),
                $"{config.GetValue(section, "name")} must exclude automation/*");
        }
        AssertTrue(checkedPresets >= 2, "Windows and Linux release presets checked");
        // C# compiles every .cs file into one assembly, so the export filter alone
        // cannot keep the server out: the project file must drop it from release builds.
        string project = FileAccess.GetFileAsString("res://Utopia.csproj");
        AssertTrue(Regex.IsMatch(project, @"ExportRelease[\s\S]*<Compile Remove=""automation/\*\*"""),
            "Utopia.csproj removes automation/** from ExportRelease builds");
    }

    public void TestGameCodeNeverDependsOnAutomationClasses()
    {
        // Release builds drop res://automation/; nothing else may reference its classes.
        var names = new List<string>();
        var declaration = new Regex(@"(?m)^\s*public\s+(?:static\s+|abstract\s+|sealed\s+|partial\s+)*(?:class|record|interface)\s+(\w+)");
        foreach (string path in SourceFiles.Collect(new[] { "res://automation" }, new[] { "cs" }))
            foreach (Match m in declaration.Matches(FileAccess.GetFileAsString(path)))
                names.Add(m.Groups[1].Value);
        AssertTrue(names.Count >= 10, "automation classes found");
        foreach (string path in SourceFiles.Collect(new[] { "res://autoload", "res://core", "res://features", "res://scenes" }, new[] { "cs", "tscn" }))
        {
            string[] lines = FileAccess.GetFileAsString(path).Split('\n')
                .Where(line => !line.Contains($"const string {nameof(AutomationGate.ServerTypeName)}"))
                .ToArray();
            string text = string.Join("\n", lines);
            AssertFalse(text.Contains("Utopia.Automation"), $"{path} references the Utopia.Automation namespace");
            foreach (string name in names)
                AssertFalse(Regex.IsMatch(text, $@"\b{name}\b"), $"{path} references {name}");
        }
    }
}
