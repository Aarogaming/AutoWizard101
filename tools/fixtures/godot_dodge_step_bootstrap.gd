
# MAELSTROM_DODGE_STEPPED_TEST_V1 -- added only to a private test copy.
func _ready():
	Engine.max_fps = 30
	var value = OS.get_environment("MAELSTROM_DODGE_SEED")
	seed(int(value) if value.is_valid_int() else 118)
	var speed = OS.get_environment("MAELSTROM_STEP_SPEED")
	Engine.time_scale = float(speed) if speed in ["0.25", "0.5", "1"] else 0.25
	$HUD._on_StartButton_pressed()
	var controller = Node.new()
	controller.set_script(load("res://maelstrom_step_controller.gd"))
	controller.process_mode = Node.PROCESS_MODE_ALWAYS
	add_child(controller)
