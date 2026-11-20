
# MAELSTROM_DODGE_VISUAL_TEST_V1 -- appended to an isolated test copy only.
func _ready():
	Engine.max_fps = 30
	var value = OS.get_environment("MAELSTROM_DODGE_SEED")
	seed(int(value) if value.is_valid_int() else 118)
	get_tree().create_timer(40.0).timeout.connect(func(): get_tree().quit())
	$HUD._on_StartButton_pressed()
