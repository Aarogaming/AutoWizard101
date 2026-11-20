# Test clock only: it receives frame counts and returns ticks, never game state.
extends Node

var channel := ""
var next_id := 0
var remaining := 0
var tick := 0
var finishing := false
var audit_file: FileAccess

func _ready():
	channel = OS.get_environment("MAELSTROM_STEP_CHANNEL")
	if channel.is_empty():
		get_tree().quit(2)
		return
	if OS.get_environment("MAELSTROM_DODGE_EVAL") == "1":
		audit_file = FileAccess.open(channel.path_join("evaluation-audit.jsonl"), FileAccess.WRITE)
		if audit_file == null:
			get_tree().quit(9)
			return
	get_tree().paused = true

func _process(_delta):
	if remaining > 0:
		remaining -= 1
		tick += 1
		if remaining == 0:
			get_tree().paused = true
			_finish_step()
		return
	if finishing:
		return
	var path = channel.path_join("step-request-%04d.json" % next_id)
	if not FileAccess.file_exists(path):
		# --fixed-fps advances as fast as possible even while paused. Yield
		# between requests so an idle private test does not burn a CPU core.
		OS.delay_msec(20)
		return
	var request = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not request is Dictionary or int(request.get("id", -1)) != next_id:
		get_tree().quit(3)
		return
	var count = int(request.get("frames", 0))
	if count < 1 or count > 240:
		get_tree().quit(4)
		return
	remaining = count
	get_tree().paused = false

func _finish_step():
	finishing = true
	await RenderingServer.frame_post_draw
	if OS.get_environment("MAELSTROM_DODGE_EVAL") == "1":
		# Evaluation only. The visual policy never receives this data.
		var main = get_parent()
		var evaluation_path = channel.path_join("evaluation.json")
		var evaluation = FileAccess.open(evaluation_path + ".tmp", FileAccess.WRITE)
		if evaluation == null:
			get_tree().quit(7)
			return
		evaluation.store_string(JSON.stringify({"score": main.score, "alive": main.get_node("Player").visible, "ticks": tick}))
		evaluation.close()
		if DirAccess.rename_absolute(evaluation_path + ".tmp", evaluation_path) != OK:
			get_tree().quit(8)
			return
		# Audit only: never read by the visual policy or the goal supervisor.
		var bodies = []
		for mob in get_tree().get_nodes_in_group("mobs"):
			bodies.append({"x": mob.position.x, "y": mob.position.y, "vx": mob.linear_velocity.x, "vy": mob.linear_velocity.y})
		var player = main.get_node("Player")
		audit_file.store_line(JSON.stringify({"ticks": tick, "score": main.score, "alive": player.visible, "player": {"x": player.position.x, "y": player.position.y}, "mobs": bodies}))
		audit_file.flush()
	var final_path = channel.path_join("step-ack-%04d.json" % next_id)
	var temp_path = final_path + ".tmp"
	var file = FileAccess.open(temp_path, FileAccess.WRITE)
	if file == null:
		get_tree().quit(5)
		return
	file.store_string(JSON.stringify({"id": next_id, "ticks": tick}))
	file.close()
	if DirAccess.rename_absolute(temp_path, final_path) != OK:
		get_tree().quit(6)
		return
	next_id += 1
	finishing = false
