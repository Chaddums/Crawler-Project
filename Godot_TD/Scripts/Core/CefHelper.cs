using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Central check for CEF availability. Returns false in headless mode
    /// to prevent CEF from blocking the frame loop during automated testing.
    /// All UI screens should use CefHelper.Available instead of ClassDB.ClassExists("CefTexture").
    /// </summary>
    public static class CefHelper
    {
        private static bool? _available;

        public static bool Available
        {
            get
            {
                // OS.HasFeature("headless") misses the --headless flag in Godot 4.6 —
                // DisplayServer's name is the reliable check (see AutoPlayReport).
                _available ??= !OS.HasFeature("headless")
                    && DisplayServer.GetName() != "headless"
                    && ClassDB.ClassExists("CefTexture");
                return _available.Value;
            }
        }

        /// <summary>
        /// Project setting that turns GPU texture sharing back on for the web views. Off by
        /// default: on Vulkan the plugin shares Godot's graphics queue ("may have sync issues under
        /// load"), and on the dev laptop (RTX 3050, Vulkan) pages came up as a strip of noise and
        /// the game froze after a few screen changes. CPU rendering copies each frame instead.
        /// </summary>
        public const string AcceleratedSetting = "junkyard/ui/cef_accelerated_osr";

        /// <summary>
        /// Call <paramref name="onCrash"/> when the page's render process dies (the plugin's
        /// render_process_terminated signal). A dead page draws nothing, which showed as Godot's
        /// grey clear colour with no way out. The signal's arguments vary between plugin builds,
        /// so this connects by the signal's own argument count.
        /// </summary>
        public static void WatchCrash(GodotObject cefTexture, string who, System.Action onCrash)
        {
            if (cefTexture == null) return;
            const string sig = "render_process_terminated";
            if (!cefTexture.HasSignal(sig)) return;
            int args = 0;
            foreach (var d in cefTexture.GetSignalList())
                if (d["name"].AsString() == sig) { args = d["args"].AsGodotArray().Count; break; }
            void Fire(string detail)
            {
                GD.PrintErr($"[{who}] Web page render process ended ({detail}); recovering");
                onCrash?.Invoke();
            }
            Callable c = args switch
            {
                0 => Callable.From(() => Fire("")),
                1 => Callable.From<Variant>(a => Fire(a.ToString())),
                2 => Callable.From<Variant, Variant>((a, b) => Fire($"{a} {b}")),
                _ => Callable.From<Variant, Variant, Variant>((a, b, x) => Fire($"{a} {b} {x}")),
            };
            cefTexture.Connect(sig, c);
        }

        /// <summary>
        /// Drawn behind every web view, and the clear colour, so a page that never paints shows
        /// this (and can be told apart from a page's own background) instead of Godot's grey.
        /// </summary>
        public static readonly Color Backdrop = new(0.012f, 0.016f, 0.03f);

        /// <summary>
        /// A dark backdrop with a hint, behind a web view: what shows until the page paints.
        /// </summary>
        public static Control AddBackdrop(Control host, string hint)
        {
            var bg = new ColorRect { Name = "CefBackdrop", Color = Backdrop, MouseFilter = Control.MouseFilterEnum.Ignore };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            var l = new Label { Text = hint, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            l.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            l.AddThemeFontSizeOverride("font_size", 18);
            l.AddThemeColorOverride("font_color", new Color(0.45f, 0.55f, 0.7f));
            bg.AddChild(l);
            host.AddChild(bg);
            host.MoveChild(bg, 0);
            return bg;
        }

        /// <summary>
        /// The web view hasn't drawn: nearly the whole window is the backdrop (or the clear
        /// colour). Pages paint their own background over everything, so this only happens when
        /// the view draws nothing at all, which is what showed as the grey screen after a run.
        /// </summary>
        public static bool LooksBlank(Viewport vp)
        {
            var img = vp?.GetTexture()?.GetImage();
            if (img == null || img.GetWidth() < 32 || img.GetHeight() < 32) return false;
            int w = img.GetWidth(), h = img.GetHeight(), n = 0, blank = 0;
            for (int y = 1; y < 16; y++)
                for (int x = 1; x < 24; x++, n++)
                {
                    var c = img.GetPixel(x * w / 24, y * h / 16);
                    if (Near(c, Backdrop) || Near(c, new Color(0.3f, 0.3f, 0.3f))) blank++;
                }
            return blank >= n * 0.92f;

            static bool Near(Color a, Color b) =>
                Mathf.Abs(a.R - b.R) < 0.02f && Mathf.Abs(a.G - b.G) < 0.02f && Mathf.Abs(a.B - b.B) < 0.02f;
        }

        /// <summary>
        /// <paramref name="after"/> seconds from now, call <paramref name="onBlank"/> if the window
        /// is still blank (the screen then makes a new web view, or switches to its built-in UI).
        /// </summary>
        public static void WatchPaint(Control host, string who, System.Action onBlank, float after = 2.5f)
        {
            if (host == null || !host.IsInsideTree()) return;
            var timer = host.GetTree().CreateTimer(after, processAlways: true);
            timer.Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree()) return;
                if (!LooksBlank(host.GetViewport())) return;
                GD.PrintErr($"[{who}] Web page hasn't drawn after {after:0.#} s; recovering");
                onBlank?.Invoke();
            };
        }

        /// <summary>Settings every web view gets before it enters the tree.</summary>
        public static void Configure(GodotObject cefTexture)
        {
            if (cefTexture == null) return;
            bool accelerated = ProjectSettings.HasSetting(AcceleratedSetting)
                && ProjectSettings.GetSetting(AcceleratedSetting).AsBool();
            cefTexture.Set("enable_accelerated_osr", accelerated);
        }
    }
}
