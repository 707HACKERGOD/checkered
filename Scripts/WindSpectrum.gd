extends Node3D

## Wind spectrum scene.
##
## Builds a row of clumps, each with a different set of wind parameters, and labels them.
## The camera then travels along the row and finally rises above a field where the wind
## strength ramps up from dead calm to a gale.
##
## The materials are duplicates of the resource on disk - changing parameters here never
## touches the .tres file in the project.

const MAT := "res://Materials/EA_Grass_Green.tres"
const CLUMP := "res://Meshes/EA_Grass_Clump.mesh"

# name, strength, speed, frequency, gust strength, gust scale, gust speed
const PRESETS := [
	["Calm", 0.03, 0.5, 0.5, 0.05, 0.05, 0.2],
	["Light Breeze", 0.14, 0.7, 0.7, 0.25, 0.06, 0.3],
	["Fresh Breeze", 0.31, 0.98, 0.8, 0.5, 0.05, 0.4],
	["Choppy Gusts", 0.34, 2.9, 1.9, 1.5, 0.16, 1.5],
	["Gale", 0.85, 3.1, 1.0, 1.5, 0.05, 1.1],
]

const SPACING := 3.4

var clumps: Array[MeshInstance3D] = []
var field: MultiMeshInstance3D
var field_mat: ShaderMaterial


func _ready() -> void:
	var base: ShaderMaterial = load(MAT)
	var mesh: Mesh = load(CLUMP)

	# --- row of labelled clumps ---
	var x := -float(PRESETS.size() - 1) * 0.5 * SPACING
	for p in PRESETS:
		var m: ShaderMaterial = base.duplicate()
		m.set_shader_parameter("wind_strength", p[1])
		m.set_shader_parameter("wind_speed", p[2])
		m.set_shader_parameter("wind_frequency", p[3])
		m.set_shader_parameter("gust_strength", p[4])
		m.set_shader_parameter("gust_scale", p[5])
		m.set_shader_parameter("gust_speed", p[6])

		# Three clumps side by side per setting - a single one shows too weakly that
		# neighbours move together but not identically.
		var group := Node3D.new()
		group.position = Vector3(x, 0.0, 0.0)
		add_child(group)
		for i in 3:
			var mi := MeshInstance3D.new()
			mi.mesh = mesh
			mi.material_override = m
			mi.position = Vector3((float(i) - 1.0) * 0.95, 0.0, float(i % 2) * 0.5 - 0.25)
			mi.rotation.y = float(i) * 2.1
			group.add_child(mi)
			clumps.append(mi)

		var tag := Label3D.new()
		tag.text = str(p[0])
		tag.font_size = 96
		tag.outline_size = 28
		tag.pixel_size = 0.0016
		tag.position = Vector3(0.0, 1.75, 0.0)
		tag.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		tag.modulate = Color(1, 1, 1)
		tag.outline_modulate = Color(0, 0, 0, 0.8)
		group.add_child(tag)

		x += SPACING

	# --- field for the overhead shot, with rising wind ---
	field_mat = base.duplicate()
	field = MultiMeshInstance3D.new()
	field.material_override = field_mat
	field.position = Vector3(0.0, 0.0, -26.0)
	add_child(field)

	var rng := RandomNumberGenerator.new()
	rng.seed = 9911
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	# The field has to extend past the frame of the overhead shot - otherwise the corners
	# show where the grass ends and it looks like a square test plane instead of a meadow.
	var half := Vector2(65.0, 55.0)
	var count := int(half.x * half.y * 4.0 * 1.5)
	mm.instance_count = count
	for i in count:
		var pos := Vector3(rng.randf_range(-half.x, half.x), 0.0, rng.randf_range(-half.y, half.y))
		var b := Basis(Vector3.UP, rng.randf_range(0.0, TAU))
		b = b.scaled(Vector3.ONE * rng.randf_range(0.75, 1.4))
		mm.set_instance_transform(i, Transform3D(b, pos))
	field.multimesh = mm


## Called by the camera: 0 = calm, 1 = gale.
func set_field_wind(t: float) -> void:
	if field_mat == null:
		return
	var e := clampf(t, 0.0, 1.0)
	field_mat.set_shader_parameter("wind_strength", lerpf(0.05, 0.85, e))
	field_mat.set_shader_parameter("wind_speed", lerpf(0.6, 3.1, e))
	field_mat.set_shader_parameter("gust_strength", lerpf(0.15, 1.5, e))
	field_mat.set_shader_parameter("gust_speed", lerpf(0.25, 1.1, e))
