using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Shared look for the code-built between-runs and milestone screens, matched to the
    /// Command Center page (ui/meta-hub): deep navy, sky-blue frame, teal for "go",
    /// amber for currency. Keeps those screens from each inventing their own palette.
    /// </summary>
    public static class MetaUiStyle
    {
        public static readonly Color Background = new(0.024f, 0.055f, 0.125f);       // #060e20
        public static readonly Color Panel = new(0.09f, 0.12f, 0.2f);                // #171f33
        public static readonly Color PanelHigh = new(0.133f, 0.165f, 0.24f);         // #222a3d
        public static readonly Color Frame = new(0.49f, 0.83f, 0.99f);               // #7dd3fc
        public static readonly Color Text = new(0.855f, 0.886f, 0.992f);             // #dae2fd
        public static readonly Color TextDim = new(0.6f, 0.65f, 0.72f);
        public static readonly Color TextFaint = new(0.38f, 0.42f, 0.5f);
        public static readonly Color Go = new(0.396f, 0.988f, 0.902f);               // #65fce6
        public static readonly Color Currency = new(0.98f, 0.75f, 0.14f);            // amber-400
        public static readonly Color Danger = new(1f, 0.55f, 0.5f);

        public static StyleBoxFlat Box(Color bg, Color border, int borderWidth = 1, int radius = 4, int margin = 12)
        {
            var sb = new StyleBoxFlat
            {
                BgColor = bg,
                BorderColor = border,
                ContentMarginLeft = margin,
                ContentMarginRight = margin,
                ContentMarginTop = margin,
                ContentMarginBottom = margin,
            };
            sb.SetBorderWidthAll(borderWidth);
            sb.SetCornerRadiusAll(radius);
            return sb;
        }

        public static Label Label(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
        {
            var l = new Label { Text = text, HorizontalAlignment = align };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            return l;
        }

        /// <summary>Outlined button in the hub's "hologram" style.</summary>
        public static Button Button(string text, Color color, Vector2 minSize)
        {
            var b = new Button { Text = text, CustomMinimumSize = minSize, FocusMode = Control.FocusModeEnum.All };
            var normal = Box(new Color(color.R * 0.08f, color.G * 0.08f, color.B * 0.08f, 0.9f),
                new Color(color.R, color.G, color.B, 0.45f), 1, 3, 10);
            var hover = Box(new Color(color.R * 0.16f, color.G * 0.16f, color.B * 0.16f, 0.95f), color, 1, 3, 10);
            var pressed = Box(new Color(color.R * 0.28f, color.G * 0.28f, color.B * 0.28f, 1f), color, 1, 3, 10);
            var disabled = Box(new Color(0.06f, 0.07f, 0.1f, 0.9f), new Color(0.3f, 0.32f, 0.38f, 0.5f), 1, 3, 10);
            b.AddThemeStyleboxOverride("normal", normal);
            b.AddThemeStyleboxOverride("hover", hover);
            b.AddThemeStyleboxOverride("pressed", pressed);
            b.AddThemeStyleboxOverride("focus", hover);
            b.AddThemeStyleboxOverride("disabled", disabled);
            b.AddThemeColorOverride("font_color", color);
            b.AddThemeColorOverride("font_hover_color", Colors.White);
            b.AddThemeColorOverride("font_pressed_color", Colors.White);
            b.AddThemeColorOverride("font_disabled_color", TextFaint);
            b.AddThemeFontSizeOverride("font_size", 16);
            return b;
        }

        /// <summary>Full-screen background with the hub's faint scanlines and corner brackets.</summary>
        public static Control Backdrop(float alpha = 1f)
        {
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            var bg = new ColorRect { Color = new Color(Background.R, Background.G, Background.B, alpha) };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Ignore;
            root.AddChild(bg);

            var deco = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            deco.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            deco.Draw += () =>
            {
                var size = deco.Size;
                var line = new Color(Frame.R, Frame.G, Frame.B, 0.035f);
                for (float y = 0; y < size.Y; y += 4f)
                    deco.DrawLine(new Vector2(0, y), new Vector2(size.X, y), line, 1f);
                var bracket = new Color(Frame.R, Frame.G, Frame.B, 0.22f);
                const float m = 10f, len = 110f, w = 3f;
                foreach (var (corner, dx, dy) in new[] {
                    (new Vector2(m, m), 1f, 1f), (new Vector2(size.X - m, m), -1f, 1f),
                    (new Vector2(m, size.Y - m), 1f, -1f), (new Vector2(size.X - m, size.Y - m), -1f, -1f) })
                {
                    deco.DrawLine(corner, corner + new Vector2(dx * len, 0), bracket, w);
                    deco.DrawLine(corner, corner + new Vector2(0, dy * len), bracket, w);
                }
            };
            deco.Resized += deco.QueueRedraw;
            root.AddChild(deco);
            return root;
        }
    }
}
