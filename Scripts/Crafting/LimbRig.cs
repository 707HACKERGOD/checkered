using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Crafting
{
    // Per-limb item storage: visual attach, mass encumbrance, lightning-rod check.
    public partial class LimbRig : Node3D
    {
        public enum Slot { Head, Torso, Back, HandL, HandR }

        class SlotData { public Node3D Marker; public InvItem Item; public Node3D Visual; }
        readonly Dictionary<Slot, SlotData> _slots = new();

        readonly Dictionary<Slot, Vector3> _offsets = new()
        {
            { Slot.Head,  new Vector3(0, 1.95f, 0) },
            { Slot.Torso, new Vector3(0, 1.25f, 0.32f) },
            { Slot.Back,  new Vector3(0, 1.25f, -0.32f) },
            { Slot.HandL, new Vector3(-0.5f, 1.05f, 0.1f) },
            { Slot.HandR, new Vector3(0.5f, 1.05f, 0.1f) },
        };

        public void ConfigureOffsets(Vector3 head, Vector3 torso, Vector3 back, Vector3 handL, Vector3 handR)
        {
            _offsets[Slot.Head] = head; _offsets[Slot.Torso] = torso; _offsets[Slot.Back] = back;
            _offsets[Slot.HandL] = handL; _offsets[Slot.HandR] = handR;
            foreach (var kv in _slots)
                if (kv.Value.Marker != null) kv.Value.Marker.Position = _offsets[kv.Key];
        }

        public override void _Ready()
        {
            void Add(Slot s)
            {
                var m = new Node3D { Position = _offsets[s] };
                GetParent().AddChild(m);
                _slots[s] = new SlotData { Marker = m };
            }
            Add(Slot.Head); Add(Slot.Torso); Add(Slot.Back); Add(Slot.HandL); Add(Slot.HandR);
        }

        public bool Equip(InvItem item, Slot s)
        {
            if (item?.Kind != ItemKind.Object) return false;
            var old = Unequip(s);
            if (old != null) Inventory.Add(old);
            var d = _slots[s];
            d.Item = item;
            d.Visual = BuildScaled(item);
            d.Marker.AddChild(d.Visual);
            return true;
        }

        public InvItem Unequip(Slot s)
        {
            var d = _slots[s];
            var item = d.Item;
            if (d.Visual != null) { d.Visual.QueueFree(); d.Visual = null; }
            d.Item = null;
            return item;
        }

        public void RefreshVisual(Slot s)
        {
            var d = _slots[s];
            if (d.Item == null) return;
            if (d.Visual != null) d.Visual.QueueFree();
            d.Visual = BuildScaled(d.Item);
            d.Marker.AddChild(d.Visual);
        }

        static Node3D BuildScaled(InvItem item)
        {
            var v = VisualBuilder.BuildObjectVisual(item.Data);
            var aabb = VisualBuilder.ComputeAabb(item.Data);
            float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
            if (maxDim > 0.8f) v.Scale = Vector3.One * (0.8f / maxDim);
            return v;
        }

        public string ItemName(Slot s) => _slots[s].Item?.Name ?? "— empty —";

        public float TotalMassKg() => _slots.Values.Where(d => d.Item != null).Sum(d => d.Item.Data.MassKg);

        public float HighestConductiveY()
        {
            float best = float.MinValue;
            foreach (var d in _slots.Values)
            {
                if (d.Item?.Data == null) continue;
                foreach (var p in d.Item.Data.Parts)
                {
                    if (MaterialLibrary.Get(p.MaterialId).Electrical < 0.5f) continue;
                    var wx = d.Marker.GlobalTransform * p.Local;
                    best = Mathf.Max(best, wx.Origin.Y + p.Shape.Size.Y * 0.5f);
                }
            }
            return best;
        }

        public (Slot Slot, InvItem Item, Part Part) FindCarriedContainer()
        {
            foreach (var s in new[] { Slot.HandR, Slot.HandL, Slot.Torso, Slot.Back, Slot.Head })
            {
                var d = _slots[s];
                if (d.Item?.Data == null) continue;
                foreach (var p in d.Item.Data.Parts)
                    if (p.IsContainer && p.Shape.CapacityM3 - p.ContentsM3 > 0.0002f)
                        return (s, d.Item, p);
            }
            return (Slot.HandR, null, null);
        }
    }
}