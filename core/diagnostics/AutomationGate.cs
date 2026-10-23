using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia;

/// <summary>
/// Decides whether the remote automation server may start, and starts it.
///
/// This file ships in every build; the server itself (res://automation/) is
/// compiled only into debug builds and QA exports (see Utopia.csproj), and is
/// found by type name at runtime, so release builds never depend on it.
///
/// Command line (after <c>--</c>):
///   --automation                     enable (profile qa)
///   --automation-profile=dev|qa      dev needs a debug build
///   --automation-port=47801          HTTP port (WebSocket = port + 1; 0 = any free port)
///   --automation-bind=127.0.0.1      non-loopback needs --automation-allow-remote
///   --automation-allow-remote        allow a non-loopback bind (token TTL capped at 15 min)
///   --automation-token-ttl=3600      seconds
/// Token: env UTOPIA_AUTOMATION_TOKEN (>= 32 chars), else generated and written
/// to user://automation/session.json (owner-only permissions).
/// </summary>
public static class AutomationGate
{
    /// <summary>The one place game code may name the server type (as text only).</summary>
    public const string ServerTypeName = "Utopia.Automation.AutomationServer";
    public const string QaFeature = "automation_qa";
    public const string TokenEnv = "UTOPIA_AUTOMATION_TOKEN";

    public static JsonObject ParseArgs(IEnumerable<string> args)
    {
        var config = new JsonObject();
        foreach (string arg in args)
        {
            if (arg == "--automation")
            {
                config["enabled"] = true;
            }
            else if (arg == "--automation-allow-remote")
            {
                config["allow_remote"] = true;
            }
            else if (arg.StartsWith("--automation-"))
            {
                config["enabled"] = true;
                string[] parts = arg["--automation-".Length..].Split('=', 2);
                if (parts.Length != 2)
                    continue;
                switch (parts[0])
                {
                    case "profile": config["profile"] = parts[1]; break;
                    case "port": config["port"] = int.TryParse(parts[1], out int port) ? port : -1; break;
                    case "bind": config["bind"] = parts[1]; break;
                    case "token-ttl": config["token_ttl_s"] = int.TryParse(parts[1], out int ttl) ? ttl : -1; break;
                }
            }
        }
        return config;
    }

    /// <summary>Returns "" when allowed, otherwise the reason the server must not start.</summary>
    public static string Refusal(string profile, bool isDebugBuild, bool hasQaFeature)
    {
        return profile switch
        {
            "dev" => isDebugBuild ? "" : "the dev profile is only available in debug builds",
            "qa" => isDebugBuild || hasQaFeature ? "" : "this build does not include automation (release build without the automation_qa feature)",
            "production" => "the production profile never starts the automation server",
            _ => $"unknown automation profile '{profile}'",
        };
    }

    /// <summary>Starts the server if requested and allowed. Returns it, or null.</summary>
    public static Node StartIfRequested(SceneTree tree, IEnumerable<string> args = null)
    {
        JsonObject config = ParseArgs(args ?? OS.GetCmdlineUserArgs());
        if (!JsonNodes.Bool(config["enabled"]))
            return null;
        string profile = JsonNodes.Str(config["profile"], "qa");
        config["profile"] = profile;
        string reason = Refusal(profile, OS.IsDebugBuild(), OS.HasFeature(QaFeature));
        int port = JsonNodes.Int(config["port"], 47801);
        if (reason.Length == 0 && (port < 0 || port > 65534 || JsonNodes.Int(config["token_ttl_s"], 3600) <= 0))
            reason = "invalid --automation-port or --automation-token-ttl";
        Type serverType = Type.GetType(ServerTypeName);
        if (reason.Length == 0 && serverType == null)
            reason = "automation is not included in this build";
        if (reason.Length > 0)
        {
            DevLog.Warn("automation", $"Automation server not started: {reason}");
            return null;
        }
        config["token"] = OS.GetEnvironment(TokenEnv);
        var server = (Node)Activator.CreateInstance(serverType);
        server.Name = "Automation";
        ((IAutomationServer)server).Configure(config);
        tree.Root.AddChild(server);
        string error = ((IAutomationServer)server).Start();
        if (error.Length > 0)
        {
            DevLog.Error("automation", $"Automation server failed to start: {error}");
            server.QueueFree();
            return null;
        }
        return server;
    }
}
