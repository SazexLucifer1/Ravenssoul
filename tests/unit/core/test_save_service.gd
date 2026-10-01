extends TestCase
## SaveService runs against MemorySaveBackend (the runner installs a fresh one
## per test) except where the JSON file backend itself is under test.

const TEST_DIR: String = "user://test_saves"


func before_each() -> void:
	DevLog.muted = true # failure paths below log warnings by design


func after_each() -> void:
	for file: String in DirAccess.get_files_at(TEST_DIR):
		DirAccess.remove_absolute(TEST_DIR.path_join(file))


func test_round_trip() -> void:
	var payload: Dictionary = {"wallet": {"gold": 5}, "soul_energy_spent": 2}
	assert_true(SaveService.save_game(payload).is_ok())
	assert_true(SaveService.has_save())
	var loaded: SaveResult = SaveService.load_game()
	assert_true(loaded.is_ok())
	assert_eq(int(loaded.data["soul_energy_spent"]), 2)


func test_envelope_carries_schema_version() -> void:
	SaveService.save_game({"x": 1})
	var backend := SaveService.backend as MemorySaveBackend
	var envelope: Dictionary = JSON.parse_string(backend.slots[SaveService.DEFAULT_SLOT])
	assert_eq(int(envelope["schema_version"]), SaveService.SCHEMA_VERSION)
	assert_true(envelope.has("saved_at_unix"))


func test_missing_save_is_not_found() -> void:
	var result: SaveResult = SaveService.load_game("campaign_9")
	assert_eq(result.status, SaveResult.Status.NOT_FOUND)


func test_corrupted_save_is_reported_with_player_message() -> void:
	(SaveService.backend as MemorySaveBackend).slots["campaign_1"] = "{not json"
	var result: SaveResult = SaveService.load_game()
	assert_eq(result.status, SaveResult.Status.CORRUPTED)
	assert_eq(result.player_message_key(), "SAVE_ERROR_CORRUPTED")


func test_newer_schema_is_refused() -> void:
	var envelope: Dictionary = {"schema_version": SaveService.SCHEMA_VERSION + 1, "data": {}}
	(SaveService.backend as MemorySaveBackend).slots["campaign_1"] = JSON.stringify(envelope)
	assert_eq(SaveService.load_game().status, SaveResult.Status.VERSION_TOO_NEW)


func test_slot_ids_cannot_escape_the_save_folder() -> void:
	for bad: String in ["../evil", "a/b", "", "UPPER", "x".repeat(40)]:
		assert_eq(SaveService.save_game({}, bad).status, SaveResult.Status.INVALID_SLOT, bad)


func test_write_failure_keeps_previous_save() -> void:
	SaveService.save_game({"version": "old"})
	(SaveService.backend as MemorySaveBackend).fail_writes = true
	var result: SaveResult = SaveService.save_game({"version": "new"})
	assert_eq(result.status, SaveResult.Status.WRITE_FAILED)
	assert_eq(result.player_message_key(), "SAVE_ERROR_WRITE_FAILED")
	assert_eq(SaveService.load_game().data["version"], "old")


func test_every_status_has_a_player_message() -> void:
	for status: int in SaveResult.Status.values():
		assert_true(SaveResult.PLAYER_MESSAGE_KEYS.has(status), "status %d" % status)


func test_json_file_backend_atomic_write_and_backup() -> void:
	var backend := JsonFileSaveBackend.new(TEST_DIR)
	assert_eq(backend.write_text("slot_a", "first"), OK)
	assert_eq(backend.write_text("slot_a", "second"), OK)
	assert_eq(backend.read_text("slot_a"), "second")
	assert_true(FileAccess.file_exists(TEST_DIR.path_join("slot_a.json.bak")))
	assert_false(FileAccess.file_exists(TEST_DIR.path_join("slot_a.json.tmp")))
	assert_eq(backend.list_slots(), PackedStringArray(["slot_a"]))
	assert_eq(backend.delete("slot_a"), OK)
	assert_false(backend.exists("slot_a"))


func test_game_session_round_trip() -> void:
	GameSession.start_new_campaign()
	GameSession.complete_mission(GameSession.active_mission)
	GameSession.soul_energy_spent = 4
	SaveService.save_game(GameSession.to_save_data())
	GameSession.start_new_campaign()
	assert_eq(GameSession.wallet.amount(ResourceWallet.GOLD), 0)
	assert_true(GameSession.load_save_data(SaveService.load_game().data))
	assert_eq(GameSession.wallet.amount(ResourceWallet.GOLD), 50)
	assert_eq(GameSession.soul_energy_spent, 4)
	assert_contains(GameSession.completed_missions, "mission_first_spark")


func test_session_rejects_unknown_content_without_side_effects() -> void:
	GameSession.start_new_campaign()
	GameSession.wallet.add(ResourceWallet.GOLD, 7)
	DevLog.muted = true
	assert_false(GameSession.load_save_data({"loadout": {"hero_id": "deleted_hero"}}))
	assert_eq(GameSession.wallet.amount(ResourceWallet.GOLD), 7)
