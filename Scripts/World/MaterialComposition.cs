using Godot;

[GlobalClass]
public partial class MaterialComposition : Node
{
    [Export] public MaterialComponent[] Components = new MaterialComponent[0];

    public string OwnerName => GetParent()?.Name ?? "Object";

    public MaterialComponent GetComponentAtPoint(Vector3 worldPoint)
    {
        MaterialComponent best = null;
        float bestD = float.MaxValue;
        foreach (var c in Components)
        {
            if (c?.RegionNode == null || c.RegionNode.IsEmpty) continue;
            var marker = GetNodeOrNull<Node3D>(c.RegionNode);
            if (marker == null) continue;
            float d = marker.GlobalPosition.DistanceTo(worldPoint);
            if (d < c.RegionRadius && d < bestD) { best = c; bestD = d; }
        }
        if (best != null) return best;
        return Components.Length > 0 ? Components[0] : null;
    }

    // finds a composition on the hit node or its ancestors; creates a sensible default so
    // EVERY interactable is scannable from day one
    public static MaterialComposition FindOrCreate(Node collider)
    {
        // search by TYPE — script-created nodes are auto-named "@MaterialComposition@N",
        // so a name-based lookup never finds them
        Node n = collider;
        while (n != null)
        {
            foreach (var c in n.GetChildren())
                if (c is MaterialComposition mc)
                    return mc;
            n = n.GetParent();
        }

        var data = (collider as InteractableItem)?.Data;
        var comp = new MaterialComposition
        {
            Name = "MaterialComposition",
            Components = new[]
            {
                new MaterialComponent
                {
                    Name = data?.Name ?? collider.Name,
                    StructureId = StructureLibrary.InferStructureId(data),
                },
            },
        };
        collider.AddChild(comp);
        return comp;
    }
}