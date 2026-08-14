extends MeshInstance3D

@export var sun_light: DirectionalLight3D

func _process(_delta):
	if sun_light:
		# This automatically calculates the exact direction your sun is facing 
		# and sends it to the shader!
		var light_dir = -sun_light.global_basis.z
		get_active_material(0).set_shader_parameter("sun_direction", light_dir)
