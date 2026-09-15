@tool
extends Node

## Assign your sky material (the ShaderMaterial on your Sky3D's Sky).
@export var sky_material: ShaderMaterial

## Assign your four SeasonPalette resources.
@export var spring_palette: Resource
@export var summer_palette: Resource
@export var autumn_palette: Resource
@export var winter_palette: Resource

## Which season to preview. Changing this in the editor re-applies instantly.
@export_enum("Spring", "Summer", "Autumn", "Winter") var preview_season: int = 0

## Where along the day cycle to preview (matches TimeManager.GetSeasonalSunProgress()).
## 0.25 = sunrise, 0.50 = noon, 0.75 = sunset, 0.0 = midnight.
@export_range(0.0, 1.0, 0.001) var preview_sun_progress: float = 0.5

## Blend amount — mirrors the shader's gradient_blend.
@export_range(0.0, 1.0, 0.01) var preview_gradient_blend: float = 0.35

## Enable this in-editor to preview. In-game it's a no-op so WeatherManager owns the sky.
@export var enabled: bool = true:
	set(v):
		enabled = v
		_apply()


func _process(_delta: float) -> void:
	# Only drives the material in editor. At runtime, WeatherManager is in charge.
	if not Engine.is_editor_hint():
		return
	if not enabled or sky_material == null:
		return
	_apply()


func _apply() -> void:
	if sky_material == null:
		return

	var pal: Resource = _pick_palette()
	if pal == null:
		return

	var top_col: Color = pal.get("TopColor").sample(preview_sun_progress)
	var hor_col: Color = pal.get("HorizonColor").sample(preview_sun_progress)

	sky_material.set_shader_parameter("sky_top_color", top_col)
	sky_material.set_shader_parameter("sky_horizon_color", hor_col)
	sky_material.set_shader_parameter("gradient_blend", preview_gradient_blend)


func _pick_palette() -> Resource:
	match preview_season:
		0: return spring_palette
		1: return summer_palette
		2: return autumn_palette
		3: return winter_palette
	return null
