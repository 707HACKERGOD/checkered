using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Crafting
{
    public partial class InventoryUi : Control
    {
        public static InventoryUi Instance;

        enum Mode { Grid, Menu, Catalog }
        Mode _mode = Mode.Grid;
        int _sel = 0;
        InvItem _menuFor;
        readonly List<string> _opts = new();
        int _optSel;

        GridContainer _grid;
        Label _detail;
        readonly List<Panel> _slotPanels = new();
        readonly List<Label> _slotNames = new();
        readonly List<TextureRect> _slotIcons = new();
        readonly List<Label> _limbLabels = new();
        PanelContainer _menuPanel; VBoxContainer _menuList;
        AudioStreamPlayer _sfx;

        public override void _Ready()
        {
            Instance = this;
            Visible = false;
            MouseFilter = MouseFilterEnum.Stop;
            SetAnchorsPreset(LayoutPreset.FullRect);
            BuildUi();
            _sfx = new AudioStreamPlayer(); AddChild(_sfx);
        }

        void BuildUi()
        {
            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Stop };
            dim.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(dim);

            var root = new VBoxContainer();
            root.SetAnchorsPreset(LayoutPreset.FullRect);
            root.OffsetLeft = 60; root.OffsetRight = -60; root.OffsetTop = 30; root.OffsetBottom = -30;
            root.AddThemeConstantOverride("separation", 12);
            AddChild(root);

            root.AddChild(UiKit.Label("INVENTORY", 26, UiKit.Accent));

            var mid = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            mid.AddThemeConstantOverride("separation", 20); root.AddChild(mid);

            _grid = new GridContainer { Columns = 4 };
            _grid.AddThemeConstantOverride("h_separation", 8);
            _grid.AddThemeConstantOverride("v_separation", 8);
            mid.AddChild(_grid);
            for (int i = 0; i < Inventory.Size + 1; i++) // index 0 = "+ NEW"
            {
                var p = new Panel { CustomMinimumSize = new Vector2(104, 104) };
                var vb = new VBoxContainer();
                vb.SetAnchorsPreset(LayoutPreset.FullRect);
                vb.OffsetLeft = 4; vb.OffsetRight = -4; vb.OffsetTop = 4; vb.OffsetBottom = -4;
                p.AddChild(vb);
                var icon = new TextureRect
                {
                    CustomMinimumSize = new Vector2(72, 72),
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    MouseFilter = MouseFilterEnum.Ignore
                };
                vb.AddChild(icon);
                var name = UiKit.Label(i == 0 ? "+ NEW" : "", 11, UiKit.Dim);
                vb.AddChild(name);
                _grid.AddChild(p);
                _slotPanels.Add(p); _slotIcons.Add(icon); _slotNames.Add(name);
            }

            var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            right.AddChild(UiKit.Label("ITEM", 16, UiKit.Dim));
            _detail = UiKit.Label("", 14, null, true);
            _detail.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            right.AddChild(_detail);
            mid.AddChild(right);

            root.AddChild(UiKit.Label("LIMBS  (1-5 equip selected · Shift+1-5 unequip)", 14, UiKit.Dim));
            var limbs = new HBoxContainer();
            limbs.AddThemeConstantOverride("separation", 8); root.AddChild(limbs);
            for (int i = 0; i < 5; i++)
            {
                var p = new Panel { CustomMinimumSize = new Vector2(120, 48) };
                p.AddThemeStyleboxOverride("panel", UiKit.Flat(UiKit.Panel, UiKit.Border, 1, 0, 6));
                var l = UiKit.Label("", 11, UiKit.Dim, true);
                l.SetAnchorsPreset(LayoutPreset.FullRect);
                l.OffsetLeft = 4; l.OffsetRight = -4;
                p.AddChild(l);
                limbs.AddChild(p);
                _limbLabels.Add(l);
            }

            root.AddChild(UiKit.Label("Arrows: move · Enter: act · Tab/Esc: close", 13, UiKit.Dim));

            _menuPanel = new PanelContainer();
            _menuPanel.SetAnchorsPreset(LayoutPreset.Center);
            _menuPanel.AddThemeStyleboxOverride("panel", UiKit.Flat(UiKit.Panel, UiKit.Accent, 2, 0, 14));
            _menuList = new VBoxContainer();
            _menuPanel.AddChild(_menuList);
            _menuPanel.Visible = false;
            AddChild(_menuPanel);
        }

        public void Open()
        {
            if (Visible) return;
            Visible = true; UiStack.Push(this);
            _mode = Mode.Grid; _menuPanel.Visible = false;
            Play("res://Assets/Audio/inventory_open.ogg");
            Refresh();
        }

        public void Close()
        {
            if (!Visible) return;
            Visible = false; UiStack.Pop(this);
            Play("res://Assets/Audio/inventory_close.ogg");
        }

        void Play(string path)
        {
            var s = UiKit.Load(path);
            if (s == null) return;
            _sfx.Stream = s; _sfx.Play();
        }

        public void Refresh()
        {
            for (int i = 0; i < _slotPanels.Count; i++)
            {
                int invIdx = i - 1;
                var item = invIdx >= 0 ? Inventory.Slots[invIdx] : null;
                bool sel = i == _sel;
                _slotPanels[i].AddThemeStyleboxOverride("panel",
                    UiKit.Flat(sel ? UiKit.Panel : new Color("141419"), sel ? UiKit.Accent : UiKit.Border, sel ? 2 : 1, 0, 4));
                _slotIcons[i].Texture = item?.Thumb;
                _slotNames[i].Text = i == 0 ? "+ NEW" : (item != null ? item.Name : "");
            }
            if (_sel == 0)
                _detail.Text = "PART CATALOG\n\nCreate a part to carry, drop in the world\nand build with. Or spawn parts directly\nwith keys 1-5 outside the inventory.\n\nEnter: open catalog";
            else
            {
                var it = Inventory.Slots[_sel - 1];
                if (it == null) _detail.Text = "";
                else if (it.Kind == ItemKind.Object)
                {
                    var lines = new System.Text.StringBuilder();
                    lines.AppendLine(it.Name.ToUpper());
                    lines.AppendLine($"{it.Data.Parts.Count} parts · {it.Data.MassKg:0.0} kg · {it.Data.Bonds.Count} bonds");
                    lines.AppendLine();
                    foreach (var p in it.Data.Parts)
                        lines.AppendLine($"· {MaterialLibrary.Get(p.MaterialId).DisplayName} {p.Shape.Describe()}");
                    foreach (var c2 in it.Data.Parts)
                        if (c2.ContentsM3 > 0)
                            lines.AppendLine($"  holds {c2.ContentsM3 * 1000:0.#} L");
                    _detail.Text = lines.ToString();
                }
                else _detail.Text = $"{MaterialLibrary.Get(it.MaterialId).DisplayName} · {it.M3 * 1000:0.#} L";
            }
            var lp = CraftingSim.Instance?.Player;
            for (int s = 0; s < 5; s++)
                _limbLabels[s].Text = $"{s + 1} {((LimbRig.Slot)s).ToString().ToUpper()}\n{lp?.Limbs.ItemName((LimbRig.Slot)s)}";
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (!Visible || e is not InputEventKey k || !k.Pressed || k.Echo) return;
            GetViewport().SetInputAsHandled();
            var key = k.PhysicalKeycode;
            if (_mode == Mode.Menu || _mode == Mode.Catalog) { ListKey(key); return; }

            int slot = key switch { Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, Key.Key5 => 4, _ => -1 };
            if (slot >= 0)
            {
                if (k.ShiftPressed) TryUnequip(slot);
                else TryEquip(slot);
                return;
            }
            switch (key)
            {
                case Key.Escape or Key.Tab: Close(); return;
                case Key.Right: _sel = Mathf.Min(_sel + 1, Inventory.Size); Refresh(); return;
                case Key.Left: _sel = Mathf.Max(_sel - 1, 0); Refresh(); return;
                case Key.Down: _sel = Mathf.Min(_sel + 4, Inventory.Size); Refresh(); return;
                case Key.Up: _sel = Mathf.Max(_sel - 4, 0); Refresh(); return;
                case Key.Enter or Key.KpEnter: OnEnter(); return;
            }
        }

        void OnEnter()
        {
            if (_sel == 0) { OpenCatalog(); return; }
            var it = Inventory.Slots[_sel - 1];
            if (it == null) return;
            _menuFor = it;
            _opts.Clear();
            if (it.Kind == ItemKind.Object) _opts.Add("Drop in world");
            else { _opts.Add("Pour out"); _opts.Add("Drop in world"); }
            _opts.Add("Cancel");
            _optSel = 0; _mode = Mode.Menu;
            ShowList();
        }

        void OpenCatalog()
        {
            _mode = Mode.Catalog;
            _opts.Clear();
            _opts.AddRange(PartCatalog.All.Select(c => $"{c.Name}  ·  {MaterialLibrary.Get(c.MaterialId).DisplayName}"));
            _opts.Add("Cancel");
            _optSel = 0;
            ShowList();
        }

        void ShowList()
        {
            foreach (var c in _menuList.GetChildren()) c.QueueFree();
            for (int i = 0; i < _opts.Count; i++)
                _menuList.AddChild(UiKit.Label(_opts[i], 16, i == _optSel ? UiKit.Accent : UiKit.Text));
            _menuPanel.Visible = true;
        }

        void ListKey(Key key)
        {
            switch (key)
            {
                case Key.Escape: _menuPanel.Visible = false; _mode = Mode.Grid; return;
                case Key.Up: _optSel = (_optSel - 1 + _opts.Count) % _opts.Count; ShowList(); return;
                case Key.Down: _optSel = (_optSel + 1) % _opts.Count; ShowList(); return;
                case Key.Enter or Key.KpEnter: ConfirmList(); return;
            }
        }

        void ConfirmList()
        {
            if (_mode == Mode.Catalog)
            {
                int idx = _optSel;
                _menuPanel.Visible = false; _mode = Mode.Grid;
                if (idx < PartCatalog.All.Count) CreateFromCatalog(PartCatalog.All[idx]);
                Refresh();
                return;
            }
            string opt = _opts[_optSel];
            var it = _menuFor;
            _menuPanel.Visible = false; _mode = Mode.Grid;
            if (opt == "Cancel" || it == null) { Refresh(); return; }
            if (opt == "Drop in world") { CraftingSim.DropItem(it); Inventory.Remove(it); Close(); }
            else if (opt == "Pour out") { CraftingSim.PourItem(it); Inventory.Remove(it); Refresh(); }
        }

        void CreateFromCatalog(CatalogEntry e)
        {
            var d = new CraftedObjectData { Name = e.Name };
            d.Parts.Add(new Part { MaterialId = e.MaterialId, Shape = new ShapeDef { Kind = e.Kind, Size = e.DefaultSize } });
            var item = new InvItem { Kind = ItemKind.Object, Data = d, Name = e.Name };
            if (Inventory.Add(item)) ThumbnailGen.Render(item);
            else CraftingHud.Toast?.Invoke("Inventory full.");
        }

        void TryEquip(int slotIdx)
        {
            if (_sel <= 0) return;
            var it = Inventory.Slots[_sel - 1];
            var lp = CraftingSim.Instance?.Player;
            if (it?.Kind != ItemKind.Object || lp == null) return;
            Inventory.Remove(it);
            lp.Limbs.Equip(it, (LimbRig.Slot)slotIdx);
            Refresh();
        }

        void TryUnequip(int slotIdx)
        {
            var lp = CraftingSim.Instance?.Player;
            if (lp == null) return;
            var item = lp.Limbs.Unequip((LimbRig.Slot)slotIdx);
            if (item != null && !Inventory.Add(item))
            {
                lp.Limbs.Equip(item, (LimbRig.Slot)slotIdx);
                CraftingHud.Toast?.Invoke("Inventory full.");
            }
            Refresh();
        }
    }
}