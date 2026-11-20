
# MAELSTROM_DODGE_NATIVE_TEST_V1 -- added only to an isolated test copy.
# This exposes test-only engine state and actions; it does not use desktop input.
var maelstrom_channel: String = ""
var maelstrom_next_id: int = 0
var maelstrom_pending: Dictionary = {}
var maelstrom_started_ms: int = 0
var maelstrom_action_name: StringName = &""
var maelstrom_deadline_ms: int = 0
var maelstrom_before: Vector2 = Vector2.ZERO

func _ready():
	Engine.max_fps = 30
	seed(118)
	maelstrom_channel = OS.get_environment("MAELSTROM_DODGE_CHANNEL")
	if maelstrom_channel.is_empty():
		get_tree().quit(2)
		return
	maelstrom_started_ms = Time.get_ticks_msec()
	new_game()

func _process(_delta):
	var now = Time.get_ticks_msec()
	if now - maelstrom_started_ms > 20000:
		if maelstrom_action_name != &"":
			Input.action_release(maelstrom_action_name)
		get_tree().quit()
		return
	if not maelstrom_pending.is_empty():
		if now < maelstrom_deadline_ms:
			return
		Input.action_release(maelstrom_action_name)
		maelstrom_action_name = &""
		var response = maelstrom_snapshot()
		response["kind"] = "action-result"
		response["before"] = [maelstrom_before.x, maelstrom_before.y]
		response["action"] = maelstrom_pending["action"]
		maelstrom_write_response(response)
		maelstrom_pending = {}
		return
	var path = maelstrom_channel.path_join("request-%04d.json" % maelstrom_next_id)
	if not FileAccess.file_exists(path):
		return
	var request = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not request is Dictionary or int(request.get("id", -1)) != maelstrom_next_id:
		get_tree().quit(3)
		return
	if request.get("kind") == "observe":
		var response = maelstrom_snapshot()
		response["kind"] = "observation"
		maelstrom_write_response(response)
		return
	var action_map = {"move-left": &"move_left", "move-right": &"move_right", "move-up": &"move_up", "move-down": &"move_down"}
	var action: String = request.get("action", "")
	var duration: int = int(request.get("duration_ms", 0))
	if request.get("kind") != "action" or not action_map.has(action) or duration < 1 or duration > 200 or not $Player.visible:
		get_tree().quit(4)
		return
	maelstrom_before = $Player.position
	maelstrom_pending = request
	maelstrom_action_name = action_map[action]
	maelstrom_deadline_ms = now + duration
	Input.action_press(maelstrom_action_name)

func maelstrom_snapshot() -> Dictionary:
	var threats: Array = []
	for mob in get_tree().get_nodes_in_group(&"mobs"):
		if threats.size() >= 32:
			break
		threats.append([mob.position.x, mob.position.y])
	return {"id": maelstrom_next_id, "game_ms": Time.get_ticks_msec(), "visible": $Player.visible,
		"player": [$Player.position.x, $Player.position.y], "threats": threats, "score": score}

func maelstrom_write_response(response: Dictionary):
	var final_path = maelstrom_channel.path_join("response-%04d.json" % maelstrom_next_id)
	var temp_path = final_path + ".tmp"
	var file = FileAccess.open(temp_path, FileAccess.WRITE)
	if file == null:
		get_tree().quit(5)
		return
	file.store_string(JSON.stringify(response))
	file.close()
	if DirAccess.rename_absolute(temp_path, final_path) != OK:
		get_tree().quit(6)
		return
	maelstrom_next_id += 1
