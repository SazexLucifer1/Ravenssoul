extends TestCase
## Auth, origin/host checks, rate limiting, capability profiles, gate.

const TOKEN: String = "0123456789abcdef0123456789abcdef0123456789abcdef"


func test_token_check_is_exact_and_expires() -> void:
	var auth := AutomationAuth.new(TOKEN, 60, "127.0.0.1", 47801)
	assert_eq(auth.check_token(TOKEN), 0)
	assert_eq(auth.check_token(""), AutomationProtocol.UNAUTHORIZED)
	assert_eq(auth.check_token(TOKEN.left(47) + "X"), AutomationProtocol.UNAUTHORIZED)
	assert_eq(auth.check_token(TOKEN + "0"), AutomationProtocol.UNAUTHORIZED)
	var expired := AutomationAuth.new(TOKEN, 0, "127.0.0.1", 47801)
	assert_eq(expired.check_token(TOKEN), AutomationProtocol.SESSION_EXPIRED)


func test_generated_tokens_are_long_and_unique() -> void:
	var a: String = AutomationAuth.generate_token()
	assert_true(a.length() >= AutomationProtocol.MIN_TOKEN_LENGTH)
	assert_ne(a, AutomationAuth.generate_token())


func test_browser_origin_and_dns_rebinding_are_rejected() -> void:
	var auth := AutomationAuth.new(TOKEN, 60, "127.0.0.1", 47801)
	assert_eq(auth.check_http_headers({"host": "127.0.0.1:47801"}), "")
	assert_eq(auth.check_http_headers({"host": "localhost:47801"}), "")
	assert_ne(auth.check_http_headers({"host": "evil.example:47801"}), "", "DNS rebinding host")
	assert_ne(auth.check_http_headers({}), "", "missing host")
	assert_ne(auth.check_http_headers({"host": "127.0.0.1:47801", "origin": "https://evil.example"}), "", "browser origin")


func test_bearer_parsing() -> void:
	assert_eq(AutomationAuth.bearer_from({"authorization": "Bearer abc"}), "abc")
	assert_eq(AutomationAuth.bearer_from({"authorization": "Basic abc"}), "")
	assert_eq(AutomationAuth.bearer_from({}), "")


func test_rate_limiter() -> void:
	var limiter := AutomationRateLimiter.new(3, 0)
	assert_true(limiter.allow())
	assert_true(limiter.allow())
	assert_true(limiter.allow())
	assert_false(limiter.allow(), "burst exhausted, no refill")


func test_capability_matrix() -> void:
	var qa: PackedStringArray = AutomationProfiles.capabilities(AutomationProfiles.QA)
	var dev: PackedStringArray = AutomationProfiles.capabilities(AutomationProfiles.DEV)
	for cap: String in ["inspect", "act.input", "observe.screenshot", "reset", "fixture"]:
		assert_true(qa.has(cap), "qa has %s" % cap)
	for cap: String in ["dev.teleport", "dev.spawn", "dev.command", "inspect.deep"]:
		assert_false(qa.has(cap), "qa lacks %s" % cap)
		assert_true(dev.has(cap), "dev has %s" % cap)
	assert_empty(AutomationProfiles.capabilities(AutomationProfiles.PRODUCTION))
	var methods: Dictionary = AutomationProtocol.methods()
	for name: String in methods:
		assert_true(dev.has(methods[name]["capability"]), "%s capability is granted by some profile" % name)


func test_gate_parses_arguments() -> void:
	assert_false(AutomationGate.parse_args(PackedStringArray(["--route=gameplay"])).get("enabled", false))
	var config: Dictionary = AutomationGate.parse_args(PackedStringArray(["--automation-profile=dev", "--automation-port=50000", "--automation-bind=0.0.0.0", "--automation-allow-remote"]))
	assert_eq(config, {"enabled": true, "profile": "dev", "port": 50000, "bind": "0.0.0.0", "allow_remote": true})


func test_production_gating() -> void:
	# (profile, debug build, automation_qa feature) -> allowed?
	assert_eq(AutomationGate.refusal("qa", true, false), "")
	assert_eq(AutomationGate.refusal("dev", true, false), "")
	assert_eq(AutomationGate.refusal("qa", false, true), "", "QA export preset")
	assert_ne(AutomationGate.refusal("qa", false, false), "", "release build without the feature")
	assert_ne(AutomationGate.refusal("dev", false, true), "", "dev never in release")
	assert_ne(AutomationGate.refusal("production", true, true), "", "production never starts the server")
	assert_ne(AutomationGate.refusal("root", true, true), "", "unknown profile")


func test_gate_does_nothing_without_flags() -> void:
	assert_null(AutomationGate.start_if_requested(tree, PackedStringArray([])))
	assert_null(tree.root.get_node_or_null(^"Automation"))


func test_release_export_excludes_automation() -> void:
	var presets: String = FileAccess.get_file_as_string("res://export_presets.cfg")
	var config := ConfigFile.new()
	assert_eq(config.parse(presets), OK)
	var checked: int = 0
	for section: String in config.get_sections():
		if section.ends_with(".options") or not section.begins_with("preset."):
			continue
		var features: String = config.get_value(section, "custom_features", "")
		if features.contains(AutomationGate.QA_FEATURE):
			continue
		checked += 1
		assert_true(str(config.get_value(section, "exclude_filter", "")).contains("automation/*"),
			"%s must exclude automation/*" % config.get_value(section, "name"))
	assert_true(checked >= 2, "Windows and Linux release presets checked")


func test_game_code_never_depends_on_automation_classes() -> void:
	# Release exports drop res://automation/; nothing else may reference its classes.
	var names: PackedStringArray = []
	var regex := RegEx.create_from_string("(?m)^class_name (\\w+)")
	for dir: String in ["res://automation/protocol", "res://automation/server", "res://automation/adapter"]:
		for file: String in DirAccess.get_files_at(dir):
			if file.ends_with(".gd"):
				var m: RegExMatch = regex.search(FileAccess.get_file_as_string(dir.path_join(file)))
				if m != null:
					names.append(m.get_string(1))
	assert_true(names.size() >= 10, "automation classes found")
	var files: PackedStringArray = []
	for dir: String in ["res://autoload", "res://core", "res://features", "res://scenes"]:
		_collect(dir, files)
	for path: String in files:
		var text: String = FileAccess.get_file_as_string(path)
		for name: String in names:
			assert_false(RegEx.create_from_string("\\b%s\\b" % name).search(text) != null, "%s references %s" % [path, name])


func _collect(dir: String, out: PackedStringArray) -> void:
	for sub: String in DirAccess.get_directories_at(dir):
		_collect(dir.path_join(sub), out)
	for file: String in DirAccess.get_files_at(dir):
		if file.get_extension() in ["gd", "tscn"]:
			out.append(dir.path_join(file))
