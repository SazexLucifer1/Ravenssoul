class_name TestCase
extends RefCounted
## Base class for automated tests. Any method named test_* is a test.
## Tests may `await`. Nodes added with add_node() are freed after each test.
## Engine/script errors logged during a test fail it unless
## expect_engine_errors() was called.

var tree: SceneTree
var failures: PackedStringArray = []
var allowed_engine_errors: int = 0
var _owned_nodes: Array[Node] = []


func before_each() -> void:
	pass


func after_each() -> void:
	pass


# --- assertions -------------------------------------------------------------

func fail(message: String) -> void:
	failures.append(message)


func assert_true(condition: bool, message: String = "expected true") -> void:
	if not condition:
		fail(message)


func assert_false(condition: bool, message: String = "expected false") -> void:
	if condition:
		fail(message)


func assert_eq(actual: Variant, expected: Variant, message: String = "") -> void:
	if typeof(actual) != typeof(expected) or actual != expected:
		fail("%sexpected <%s> but got <%s>" % [_prefix(message), var_to_str(expected), var_to_str(actual)])


func assert_ne(actual: Variant, unexpected: Variant, message: String = "") -> void:
	if typeof(actual) == typeof(unexpected) and actual == unexpected:
		fail("%sdid not expect <%s>" % [_prefix(message), var_to_str(actual)])


func assert_null(value: Variant, message: String = "expected null") -> void:
	if value != null:
		fail(message)


func assert_not_null(value: Variant, message: String = "expected a value") -> void:
	if value == null:
		fail(message)


func assert_empty(collection: Variant, message: String = "") -> void:
	if not collection.is_empty():
		fail("%sexpected empty, got %s" % [_prefix(message), str(collection)])


func assert_contains(collection: Variant, item: Variant, message: String = "") -> void:
	if not collection.has(item):
		fail("%s%s does not contain %s" % [_prefix(message), str(collection), str(item)])


# --- helpers ----------------------------------------------------------------

## Declares that the current test deliberately triggers engine errors.
func expect_engine_errors(count: int) -> void:
	allowed_engine_errors += count


func add_node(node: Node, parent: Node = null) -> Node:
	(parent if parent != null else tree.root).add_child(node)
	_owned_nodes.append(node)
	return node


func instantiate(path: String, parent: Node = null) -> Node:
	var packed := load(path) as PackedScene
	return add_node(packed.instantiate(), parent)


func wait_frames(count: int = 1) -> void:
	for i: int in count:
		await tree.process_frame


func wait_seconds(seconds: float) -> void:
	await tree.create_timer(seconds, true, false, true).timeout


## Polls [param condition] every frame. Returns false on timeout.
func wait_until(condition: Callable, timeout_seconds: float = 5.0) -> bool:
	var deadline: int = Time.get_ticks_msec() + int(timeout_seconds * 1000.0)
	while not condition.call():
		if Time.get_ticks_msec() > deadline:
			return false
		await tree.process_frame
	return true


## Sends an action press (and release) through the input pipeline.
func press_action(action: StringName) -> void:
	var press := InputEventAction.new()
	press.action = action
	press.pressed = true
	Input.parse_input_event(press)
	var release := InputEventAction.new()
	release.action = action
	release.pressed = false
	Input.parse_input_event(release)
	Input.flush_buffered_events()


func free_owned_nodes() -> void:
	for node: Node in _owned_nodes:
		if is_instance_valid(node):
			if node.get_parent() != null:
				node.get_parent().remove_child(node)
			node.queue_free()
	_owned_nodes.clear()


func _prefix(message: String) -> String:
	return "" if message.is_empty() else message + ": "
