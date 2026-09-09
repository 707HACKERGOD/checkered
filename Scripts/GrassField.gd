@tool
extends MultiMeshInstance3D

## Scatters grass clumps over a rectangular patch of ground.
##
## The demo scene has three of these fields - one per material colour variant.
## Everything is computed in _ready(), so the scene file stays small (no thousands
## of hardcoded transforms).

@export var grass_mesh: Mesh:
	set(v):
		grass_mesh = v
		_rebuild()
@export var area_size := Vector2(18.0, 12.0):
	set(v):
		area_size = v
		_rebuild()
## Clumps per square metre.
@export_range(0.05, 8.0, 0.05) var density := 1.6:
	set(v):
		density = v
		_rebuild()
@export var scale_min := 0.7:
	set(v):
		scale_min = v
		_rebuild()
@export var scale_max := 1.35:
	set(v):
		scale_max = v
		_rebuild()
## A slight lean, so the clumps do not stand to attention.
@export_range(0.0, 30.0, 0.5) var tilt_degrees := 6.0:
	set(v):
		tilt_degrees = v
		_rebuild()
@export var random_seed := 12345:
	set(v):
		random_seed = v
		_rebuild()


func _ready() -> void:
	_rebuild()


func _rebuild() -> void:
	if grass_mesh == null:
		return
	var rng := RandomNumberGenerator.new()
	rng.seed = random_seed

	var count := int(area_size.x * area_size.y * density)
	if count <= 0:
		return

	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = grass_mesh
	mm.instance_count = count

	var half := area_size * 0.5
	for i in count:
		var pos := Vector3(rng.randf_range(-half.x, half.x), 0.0, rng.randf_range(-half.y, half.y))
		var basis := Basis(Vector3.UP, rng.randf_range(0.0, TAU))
		if tilt_degrees > 0.0:
			var axis := Vector3(rng.randf_range(-1.0, 1.0), 0.0, rng.randf_range(-1.0, 1.0)).normalized()
			basis = Basis(axis, deg_to_rad(rng.randf_range(-tilt_degrees, tilt_degrees))) * basis
		basis = basis.scaled(Vector3.ONE * rng.randf_range(scale_min, scale_max))
		mm.set_instance_transform(i, Transform3D(basis, pos))

	multimesh = mm
