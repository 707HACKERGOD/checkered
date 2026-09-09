extends Camera3D

## Shot direction for the WindSpectrum scene.
##
## Besides driving the camera it also controls the wind strength on the field during the
## last shots, so one continuous move shows the transition from calm to gale.

@export var fps := 60.0
@export var spectrum_path: NodePath

var spectrum: Node

# from_position, from_target, to_position, to_target, seconds, wind_from, wind_to
var shots := [
	# The camera travels SIDEWAYS along the row, perpendicular to the clumps. Moving along
	# the axis of the row flattened the perspective and the far labels became hard to make out.
	# 1. Calm -> Light Breeze
	[Vector3(-6.8, 1.1, 3.0), Vector3(-6.8, 0.65, 0.0),
	 Vector3(-3.4, 1.1, 3.0), Vector3(-3.4, 0.65, 0.0), 6.0, 0.0, 0.0],
	# 2. Fresh Breeze -> Choppy Gusts
	[Vector3(-3.4, 1.1, 3.0), Vector3(-3.4, 0.65, 0.0),
	 Vector3(3.4, 1.1, 3.0), Vector3(3.4, 0.65, 0.0), 8.0, 0.0, 0.0],
	# 3. Gale, close up - shows how hard a single blade bends.
	[Vector3(3.4, 1.1, 3.0), Vector3(3.4, 0.65, 0.0),
	 Vector3(6.8, 0.95, 2.3), Vector3(6.8, 0.6, 0.0), 5.0, 0.0, 0.0],
	# 4. Rise above the field, wind building up from calm.
	[Vector3(4.0, 2.0, -6.0), Vector3(0.0, 0.7, -20.0),
	 Vector3(0.0, 15.0, -12.0), Vector3(0.0, 0.0, -26.0), 6.0, 0.0, 0.35],
	# 5. Overhead view, wind ramping up to a gale. The look-down angle stays under
	# 45 degrees so the horizon does not enter the frame and reveal where the field ends.
	[Vector3(0.0, 15.0, -12.0), Vector3(0.0, 0.0, -26.0),
	 Vector3(0.0, 21.0, -16.0), Vector3(0.0, 0.0, -32.0), 7.0, 0.35, 1.0],
]

var shot := 0
var shot_frame := 0


func _ready() -> void:
	spectrum = get_node_or_null(spectrum_path)
	_apply(0.0)


func _process(_delta: float) -> void:
	shot_frame += 1
	var length := int(shots[shot][4] * fps)
	var t := float(shot_frame) / float(maxi(length, 1))
	if t >= 1.0:
		shot += 1
		shot_frame = 0
		if shot >= shots.size():
			shot = shots.size() - 1
			t = 1.0
		else:
			t = 0.0
	_apply(clampf(t, 0.0, 1.0))


func _apply(t: float) -> void:
	var e := t * t * (3.0 - 2.0 * t)
	var s = shots[shot]
	look_at_from_position((s[0] as Vector3).lerp(s[2], e), (s[1] as Vector3).lerp(s[3], e), Vector3.UP)
	if spectrum != null and spectrum.has_method("set_field_wind"):
		spectrum.set_field_wind(lerpf(float(s[5]), float(s[6]), e))
