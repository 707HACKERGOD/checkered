using Godot;

namespace Crafting
{
    public enum ItemKind { Object, Substance }

    public class InvItem
    {
        public ItemKind Kind = ItemKind.Object;
        public string Name = "Object";
        public CraftedObjectData Data;          // Kind == Object
        public string MaterialId;               // Kind == Substance
        public float M3;
        public Texture2D Thumb;
    }

    public static class Inventory
    {
        public const int Size = 12;
        public static readonly InvItem[] Slots = new InvItem[Size];

        public static bool Add(InvItem item)
        {
            for (int i = 0; i < Size; i++) if (Slots[i] == null) { Slots[i] = item; return true; }
            return false;
        }
        public static void Remove(InvItem item)
        {
            for (int i = 0; i < Size; i++) if (ReferenceEquals(Slots[i], item)) Slots[i] = null;
        }
    }

    // Renders a 3D thumbnail of an object into a hidden SubViewport, async.
    public static class ThumbnailGen
    {
        static SubViewport _vp; static Node3D _root; static Camera3D _cam;

        public static async void Render(InvItem item)
        {
            var lab = CraftingSim.Instance;
            if (lab == null || item?.Data == null) return;
            if (_vp == null)
            {
                _vp = new SubViewport { Size = new Vector2I(96, 96), OwnWorld3D = true, TransparentBg = true };
                _vp.Set("update_mode", 0);
                lab.AddChild(_vp);
                _root = new Node3D(); _vp.AddChild(_root);
                _cam = new Camera3D { Fov = 35, Current = true }; _vp.AddChild(_cam);
                var l = new DirectionalLight3D { RotationDegrees = new Vector3(-40, 30, 0) }; _vp.AddChild(l);
            }
            foreach (var c in _root.GetChildren()) { _root.RemoveChild(c); c.QueueFree(); }
            _root.AddChild(VisualBuilder.BuildObjectVisual(item.Data));
            var aabb = VisualBuilder.ComputeAabb(item.Data);
            float rad = Mathf.Max(aabb.Size.Length() * 0.5f, 0.15f);
            _cam.GlobalPosition = aabb.GetCenter() + new Vector3(1, 0.75f, 1).Normalized() * rad * 2.4f;
            _cam.LookAt(aabb.GetCenter(), Vector3.Up);
            _vp.Set("update_mode", 4);
            await lab.ToSignal(lab.GetTree(), SceneTree.SignalName.ProcessFrame);
            await lab.ToSignal(lab.GetTree(), SceneTree.SignalName.ProcessFrame);
            item.Thumb = ImageTexture.CreateFromImage(_vp.GetTexture().GetImage());
            _vp.Set("update_mode", 0);
            InventoryUi.Instance?.Refresh();
        }
    }
}