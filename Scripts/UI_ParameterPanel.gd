extends Control

## Runtime parameter panel for the EA_GrassWind demo.
##
## The layout is saved data: every group, slider and label lives in
## UI_ParameterPanel.tscn and is visible in the editor without running the game.
## This script only wires those existing controls to the shader parameters of
## the selected grass field and keeps the numbers next to the sliders current.
##
## Nothing here writes to disk. The panel calls set_shader_parameter() on the
## already loaded ShaderMaterial, which only changes the copy living in memory;
## ResourceSaver is never used, so the .tres materials stay exactly as shipped
## and every restart of the demo comes back to the authored look.
##
##   H - hide / show the panel (and the help overlay), for clean screenshots
##
## The three field paths and the help overlay path are exported, so a scene that
## arranges its grass differently can repoint them in the inspector.

## Grass fields offered by the drop-down, in the same order as its items.
@export var surface_paths: Array[NodePath] = [
	^"../../Field_Green",
	^"../../Field_Dry",
	^"../../Field_Dead",
	^"../../Field_Winter",
]

## Help text drawn by the demo scene. Hidden together with the panel.
@export var help_overlay_path: NodePath = ^"../Help"

## Key that hides and shows the panel.
@export var toggle_key: Key = KEY_H

@onready var _panel: PanelContainer = $Panel
@onready var _params: VBoxContainer = $Panel/Layout/Scroll/Params
@onready var _surface_selector: OptionButton = $Panel/Layout/SurfaceRow/SurfaceSelector

var _material: ShaderMaterial = null
var _slider_rows: Array[Node] = []


func _ready() -> void:
	_collect_rows()
	_surface_selector.item_selected.connect(_on_surface_selected)
	_select_surface(_surface_selector.selected)


# Every row is named after the shader uniform it drives, so the panel needs no
# parallel table of names that could drift away from the scene.
func _collect_rows() -> void:
	for row in _params.get_children():
		var slider := row.get_node_or_null(^"Slider") as HSlider
		if slider != null:
			slider.value_changed.connect(_on_slider_changed.bind(row))
			_slider_rows.append(row)


func _on_surface_selected(index: int) -> void:
	_select_surface(index)


func _select_surface(index: int) -> void:
	_material = null
	if index >= 0 and index < surface_paths.size():
		var mesh := get_node_or_null(surface_paths[index]) as GeometryInstance3D
		if mesh != null:
			_material = mesh.material_override as ShaderMaterial
	_refresh_from_material()


func _on_slider_changed(value: float, row: Node) -> void:
	var slider := row.get_node(^"Slider") as HSlider
	_update_value_label(row, value, slider.step)
	if _material == null:
		return
	_material.set_shader_parameter(row.name, value)


# On start and after every switch the controls show what the picked material
# really holds, not the defaults written into the scene.
func _refresh_from_material() -> void:
	for row in _slider_rows:
		var slider := row.get_node(^"Slider") as HSlider
		var value := float(_shader_value(row.name, slider.value))
		slider.set_value_no_signal(clampf(value, slider.min_value, slider.max_value))
		_update_value_label(row, slider.value, slider.step)


func _shader_value(uniform: StringName, fallback: Variant) -> Variant:
	if _material == null:
		return fallback
	var value: Variant = _material.get_shader_parameter(uniform)
	if value == null and _material.shader != null:
		value = RenderingServer.shader_get_parameter_default(_material.shader.get_rid(), uniform)
	if value == null:
		return fallback
	return value


func _update_value_label(row: Node, value: float, step: float) -> void:
	var label := row.get_node_or_null(^"Header/Value") as Label
	if label == null:
		return
	if step >= 1.0:
		label.text = "%d" % int(round(value))
	elif step < 0.01:
		label.text = "%.3f" % value
	else:
		label.text = "%.2f" % value


# This node stays visible and only switches its child panel off, so the key
# still reaches the script once the panel is gone.
func _unhandled_key_input(event: InputEvent) -> void:
	var key := event as InputEventKey
	if key == null or not key.pressed or key.echo:
		return
	if key.keycode != toggle_key:
		return
	var show_ui := not _panel.visible
	_panel.visible = show_ui
	var help := get_node_or_null(help_overlay_path) as CanvasItem
	if help != null:
		help.visible = show_ui
	get_viewport().set_input_as_handled()
