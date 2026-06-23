@tool
extends EditorScenePostImport

func _post_import(scene):
	# Scene is the root node of your imported 3D file
	if scene is Node3D:
		scene.rotate_y(PI) # Rotates the model 180 degrees
	return scene
