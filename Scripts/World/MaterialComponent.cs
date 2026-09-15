using Godot;

[GlobalClass]
public partial class MaterialComponent : Resource
{
    [Export] public string Name = "Material";
    [Export] public string StructureId = "amorph_sio2";
    [Export] public float VolumeM3 = 0.01f;
    [Export] public NodePath RegionNode;   // optional Marker3D for click-region picking
    [Export] public float RegionRadius = 0.4f;
}