extends Camera3D

## Orbit camera for looking at the grass.
##
##   LMB + mouse move  - orbit
##   wheel             - zoom
##   RMB + mouse move  - pan the target
##   space             - pause/resume animation (to inspect a single frame)
##   1 / 2 / 3 / 4     - camera presets: top down / ground level / from the side /
##                       the winter field behind the meadow

@export var target := Vector3(0.0, 0.6, 0.0)
@export var distance := 9.0
@export var yaw := 35.0
@export var pitch := -12.0

var _dragging := false
var _panning := false
var _paused := false


func _ready() -> void:
	_apply()


func _apply() -> void:
	var y := deg_to_rad(yaw)
	var p := deg_to_rad(pitch)
	var offset := Vector3(
		cos(p) * sin(y),
		sin(p),
		cos(p) * cos(y)) * distance
	# The camera sits on an orbit around the target and always faces it.
	look_at_from_position(target - offset, target, Vector3.UP)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		match event.button_index:
			MOUSE_BUTTON_LEFT:
				_dragging = event.pressed
			MOUSE_BUTTON_RIGHT:
				_panning = event.pressed
			MOUSE_BUTTON_WHEEL_UP:
				distance = maxf(1.0, distance - 0.6)
				_apply()
			MOUSE_BUTTON_WHEEL_DOWN:
				distance = minf(60.0, distance + 0.6)
				_apply()
	elif event is InputEventMouseMotion:
		if _dragging:
			yaw -= event.relative.x * 0.3
			pitch = clampf(pitch - event.relative.y * 0.3, -85.0, 85.0)
			_apply()
		elif _panning:
			var right := global_transform.basis.x
			var fwd := global_transform.basis.y
			target -= (right * event.relative.x + fwd * -event.relative.y) * distance * 0.002
			_apply()
	elif event is InputEventKey and event.pressed and not event.echo:
		match event.keycode:
			KEY_SPACE:
				_paused = not _paused
				# Freezing time freezes TIME in the shader - handy for judging the
				# shape of the wave or taking a screenshot.
				Engine.time_scale = 0.0 if _paused else 1.0
			KEY_1:
				distance = 14.0
				pitch = -35.0
				target = Vector3(0.0, 0.6, 0.0)
				_apply()
			KEY_2:
				distance = 4.0
				pitch = -4.0
				target = Vector3(0.0, 0.5, 0.0)
				_apply()
			KEY_3:
				distance = 9.0
				pitch = -12.0
				yaw = 90.0
				target = Vector3(0.0, 0.6, 0.0)
				_apply()
			KEY_4:
				distance = 11.0
				pitch = -13.0
				yaw = 35.0
				target = Vector3(0.0, 0.6, -20.0)
				_apply()
