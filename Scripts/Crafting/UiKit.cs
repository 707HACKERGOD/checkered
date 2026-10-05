using Godot;
using System;
using System.Collections.Generic;

namespace Crafting
{
    // Decouples crafting systems from any specific scene/UI. The sandbox lab
    // and CraftingRuntime both subscribe to these.
    public static class CraftingHud
    {
        public static Action<string> Toast;
        public static Action<string> SetHint;
        public static Action<bool> StormChanged;
        public static Action ZapFx;
        public static Action ToggleGrid;
    }

    // Modal stack. Wire into YOUR input locking from your game bootstrap:
    //   UiStack.ExternalBlocking = () => PlayerInputOverride.Active;
    //   UiStack.OpenChanged += open => PlayerInputOverride.Active = open;
    public static class UiStack
    {
        static readonly List<Control> _stack = new();
        public static Func<bool> ExternalBlocking;
        public static event Action<bool> OpenChanged;

        public static bool Blocking => _stack.Count > 0 || (ExternalBlocking != null && ExternalBlocking());

        public static void Push(Control c)
        {
            _stack.Add(c);
            Input.MouseMode = Input.MouseModeEnum.Visible;
            if (_stack.Count == 1) OpenChanged?.Invoke(true);
        }

        public static void Pop(Control c)
        {
            _stack.Remove(c);
            if (_stack.Count == 0)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
                OpenChanged?.Invoke(false);
            }
        }
    }

    public static class UiKit
    {
        public static readonly Color Bg = new("0e0e13"), Panel = new("1a1a21"), Border = new("4a3030"),
            Accent = new("ff4444"), Text = new("e0d5c7"), Dim = new("8f897c"), Good = new("7fd67f"), Bad = new("ff6a6a");

        static Font _font;
        public static Font Font => _font ??= (ResourceLoader.Exists("res://Assets/UI/PixeloidSans-lxa3y.ttf")
            ? ResourceLoader.Load<Font>("res://Assets/UI/PixeloidSans-lxa3y.ttf") : ThemeDB.FallbackFont);

        public static Label Label(string text, int size, Color? color = null, bool wrap = false)
        {
            var l = new Label { Text = text };
            l.AddThemeFontOverride("font", Font);
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color ?? Text);
            if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            return l;
        }

        public static StyleBoxFlat Flat(Color bg, Color? border = null, int bw = 0, int corner = 0, int margin = 10)
        {
            var s = new StyleBoxFlat
            {
                BgColor = bg,
                CornerRadiusTopLeft = corner, CornerRadiusTopRight = corner,
                CornerRadiusBottomLeft = corner, CornerRadiusBottomRight = corner
            };
            s.ContentMarginLeft = s.ContentMarginRight = s.ContentMarginTop = s.ContentMarginBottom = margin;
            if (border != null) { s.BorderColor = border.Value; s.SetBorderWidthAll(bw); }
            return s;
        }

        public static AudioStream Load(string path) =>
            ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;

        static Texture2D _grid;
        public static Texture2D GridTexture()
        {
            if (_grid != null) return _grid;
            var img = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            img.Fill(new Color(0.13f, 0.14f, 0.17f));
            var line = new Color(0.24f, 0.25f, 0.30f);
            for (int i = 0; i < 64; i++) { img.SetPixel(i, 0, line); img.SetPixel(0, i, line); }
            _grid = ImageTexture.CreateFromImage(img);
            return _grid;
        }
    }
}