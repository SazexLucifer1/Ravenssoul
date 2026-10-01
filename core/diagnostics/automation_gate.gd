class_name AutomationGate
extends RefCounted
## Decides whether the remote automation server may start, and starts it.
##
## This file ships in every build; the server itself (res://automation/) is
## loaded by path only when allowed, so release exports can exclude that
## folder entirely and never depend on it.
##
## Command line (after `--`):
##   --automation                     enable (profile qa)
##   --automation-profile=dev|qa      dev needs a debug build
##   --automation-port=47801          HTTP port (WebSocket = port + 1; 0 = any free port)
##   --automation-bind=127.0.0.1      non-loopback needs --automation-allow-remote
##   --automation-allow-remote        allow a non-loopback bind (token TTL capped at 15 min)
##   --automation-token-ttl=3600      seconds
## Token: env UTOPIA_AUTOMATION_TOKEN (>= 32 chars), else generated and written
## to user://automation/session.json (owner-only permissions).

const SERVER_SCRIPT: String = "res://automation/server/automation_server.gd"
const QA_FEATURE: String = "automation_qa"
const TOKEN_ENV: String = "UTOPIA_AUTOMATION_TOKEN"


static func parse_args(args: PackedStringArray) -> Dictionary:
	var config: Dictionary = {}
	for arg: String in args:
		if arg == "--automation":
			config["enabled"] = true
		elif arg == "--automation-allow-remote":
			config["allow_remote"] = true
		elif arg.begins_with("--automation-"):
			config["enabled"] = true
			var parts: PackedStringArray = arg.trim_prefix("--automation-").split("=", true, 1)
			if parts.size() == 2:
				match parts[0]:
					"profile": config["profile"] = parts[1]
					"port": config["port"] = int(parts[1]) if parts[1].is_valid_int() else -1
					"bind": config["bind"] = parts[1]
					"token-ttl": config["token_ttl_s"] = int(parts[1]) if parts[1].is_valid_int() else -1
	return config


## Returns "" when allowed, otherwise the reason the server must not start.
static func refusal(profile: String, is_debug_build: bool, has_qa_feature: bool) -> String:
	match profile:
		"dev":
			return "" if is_debug_build else "the dev profile is only available in debug builds"
		"qa":
			return "" if is_debug_build or has_qa_feature else "this build does not include automation (release build without the automation_qa feature)"
		"production":
			return "the production profile never starts the automation server"
	return "unknown automation profile '%s'" % profile


## Starts the server if requested and allowed. Returns it, or null.
static func start_if_requested(tree: SceneTree, args: PackedStringArray = OS.get_cmdline_user_args()) -> Node:
	var config: Dictionary = parse_args(args)
	if not config.get("enabled", false):
		return null
	config["profile"] = config.get("profile", "qa")
	var reason: String = refusal(config["profile"], OS.is_debug_build(), OS.has_feature(QA_FEATURE))
	if reason.is_empty() and (int(config.get("port", 47801)) < 0 or int(config.get("port", 47801)) > 65534 or int(config.get("token_ttl_s", 3600)) <= 0):
		reason = "invalid --automation-port or --automation-token-ttl"
	if reason.is_empty() and not ResourceLoader.exists(SERVER_SCRIPT):
		reason = "automation is not included in this build"
	if not reason.is_empty():
		DevLog.warn("automation", "Automation server not started: %s" % reason)
		return null
	config["token"] = OS.get_environment(TOKEN_ENV)
	var server: Node = (load(SERVER_SCRIPT) as GDScript).new()
	server.name = "Automation"
	server.call("configure", config)
	tree.root.add_child(server)
	var error: String = server.call("start")
	if not error.is_empty():
		DevLog.error("automation", "Automation server failed to start: %s" % error)
		server.queue_free()
		return null
	return server
