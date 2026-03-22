@tool
extends EditorScript

func _run():
	# 1. Get the selected node
	var selection = EditorInterface.get_selection().get_selected_nodes()
	if selection.is_empty():
		print("ERROR: Select the MeshInstance3D first!")
		return

	var node = selection[0]
	if not node is MeshInstance3D:
		print("ERROR: Selected node is not a MeshInstance3D.")
		return

	# 2. Get mesh and scale
	var original_mesh = node.mesh
	var bake_scale = node.scale
	
	print("Baking scale ", bake_scale, " into new mesh...")

	var new_mesh = ArrayMesh.new()

	# 3. Loop through surfaces
	for i in range(original_mesh.get_surface_count()):
		var st = SurfaceTool.new()
		
		# We use PRIMITIVE_TRIANGLES for standard meshes
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		
		# THIS IS THE KEY FIX:
		# [...](asc_slot://start-slot-1)append_from() takes the source mesh, the surface index, AND a transform.
		# This automatically multiplies all vertices by the scale!
		var xform = Transform3D().scaled(bake_scale)
		st.append_from(original_mesh, i, xform)
		
		# Commit directly adds a new surface to 'new_mesh'
		st.commit(new_mesh)
		
		# Restore material
		var mat = original_mesh.surface_get_material(i)
		if mat:
			new_mesh.surface_set_material(i, mat)

	# 4. Save
	var path = "res://pine1_arraymesh.tres"
	var error = ResourceSaver.save(new_mesh, path)

	if error == OK:
		print("SUCCESS! Saved to: " + path)
	else:
		print("Error saving: " + str(error))
