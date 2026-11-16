using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The big moments, in the middle of the screen: a site secured, a region taken, an Ascendant
    /// down, a relic found, a first-time milestone. A kicker line, a title, a line of detail and a
    /// burst ring in the moment's colour; it pops in, holds, fades, and the next one waits its
    /// turn. These used to be a line in the announcement band or a small card under the build bar.
    /// </summary>
    public partial class Celebration : CanvasLayer
    {
        public record Moment(string Kicker, string Title, string Detail, Color Color, float Hold);

        private static Celebration _current;
        private readonly Queue<Moment> _queue = new();
        private Control _root;
        private Label _kicker, _title, _detail;
        private Panel _ring, _bar;
        private float _t = -1f;
        private Moment _showing;

        /// <summary>What's on screen now (tests), or null.</summary>
        public static Moment Showing => _current != null && IsInstanceValid(_current) ? _current._showing : null;
        /// <summary>Everything shown this scene, oldest first (tests).</summary>
        public static readonly List<Moment> History = new();
        /// <summary>Moments wait (nothing shows or counts down) while this is set: a loot crate opening.</summary>
        public static bool Held;

        /// <summary>Show a moment over the current scene (queued behind one already showing).</summary>
        public static void Show(string kicker, string title, string detail, Color color, float hold = 3.2f)
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.CurrentScene == null) return;
            if (_current == null || !IsInstanceValid(_current) || !_current.IsInsideTree() || _current.GetParent() != tree.CurrentScene)
            {
                _current = new Celebration();
                tree.CurrentScene.AddChild(_current);
            }
            var m = new Moment(kicker ?? "", title ?? "", detail ?? "", color, hold);
            foreach (var q in _current._queue) if (q.Title == m.Title && q.Kicker == m.Kicker) return;
            if (_current._showing != null && _current._showing.Title == m.Title && _current._showing.Kicker == m.Kicker) return;
            if (_current._queue.Count >= 5) return;
            _current._queue.Enqueue(m);
            History.Add(m);
            if (History.Count > 50) History.RemoveAt(0);
        }

        /// <summary>
        /// Show a moment now, ahead of everything waiting: what's on screen comes down and the
        /// rest follow it. For the one thing that just happened that matters most: the last boss
        /// fight won said "+1 PERK POINT" (wave 30's milestone, which came first) and went to
        /// the debrief before "Campaign complete" had its turn.
        /// </summary>
        public static void ShowFirst(string kicker, string title, string detail, Color color, float hold = 3.2f)
        {
            Show(kicker, title, detail, color, hold);
            if (_current == null || !IsInstanceValid(_current)) return;
            var mine = _current._queue.LastOrDefault(m => m.Kicker == (kicker ?? "") && m.Title == (title ?? ""));
            if (mine == null) return;
            var rest = _current._queue.Where(m => m != mine).ToList();
            _current._queue.Clear();
            _current._queue.Enqueue(mine);
            foreach (var m in rest) _current._queue.Enqueue(m);
            // What's showing steps aside for it (and comes back after, if it had most of its time left)
            if (_current._showing != null && _current._showing != mine)
            {
                var cut = _current._showing;
                if (_current._t < 0.35f + cut.Hold * 0.5f) _current._queue.Enqueue(cut);
                _current._showing = null;
                _current._t = -1f;
                if (_current._root != null) _current._root.Visible = false;
            }
        }

        /// <summary>Take a moment down, showing or waiting (what it said is no longer true).</summary>
        public static void Dismiss(string kicker)
        {
            if (_current == null || !IsInstanceValid(_current)) return;
            var keep = _current._queue.Where(m => m.Kicker != kicker).ToList();
            _current._queue.Clear();
            foreach (var m in keep) _current._queue.Enqueue(m);
            if (_current._showing != null && _current._showing.Kicker == kicker)
            {
                _current._showing = null;
                _current._t = -1f;
                if (_current._root != null) _current._root.Visible = false;
            }
        }

        public override void _Ready()
        {
            Layer = 28; // over the HUD and the Spire menu, under the pause menu (30) and the perk pick (35)
            ProcessMode = ProcessModeEnum.Always;
            Name = "Celebration";

            _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            box.AnchorLeft = 0.1f; box.AnchorRight = 0.9f;
            box.AnchorTop = 0.2f; box.AnchorBottom = 0.42f;
            box.AddThemeConstantOverride("separation", 4);
            _root.AddChild(box);

            _ring = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.AddChild(_ring);
            _root.MoveChild(_ring, 0);

            // A soft dark plate behind the words: over the lit floor inside the dome the white
            // title with its amber glow and the pale detail line were lost (PC renderer, 2026-11)
            _shade = new Panel { Name = "Shade", MouseFilter = Control.MouseFilterEnum.Ignore };
            var shade = new StyleBoxFlat { BgColor = ShadeColor, ShadowColor = new Color(ShadeColor, 0.45f), ShadowSize = 20 };
            shade.SetCornerRadiusAll(12);
            _shade.AddThemeStyleboxOverride("panel", shade);
            _root.AddChild(_shade);
            _root.MoveChild(_shade, 0);

            _kicker = MakeLabel(20, 6);
            _title = MakeLabel(52, 10);
            _detail = MakeLabel(19, 5);
            _detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _bar = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 4), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            box.AddChild(_kicker);
            box.AddChild(_title);
            box.AddChild(_bar);
            box.AddChild(_detail);
            _root.Visible = false;
        }

        private static Label MakeLabel(int size, int outline)
        {
            var l = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeConstantOverride("outline_size", outline);
            l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
            return l;
        }

        public override void _ExitTree()
        {
            if (_current == this) _current = null;
        }

        public override void _Process(double delta)
        {
            if (Held) return;
            float dt = (float)delta;
            if (_showing == null)
            {
                if (_queue.Count == 0) return;
                Begin(_queue.Dequeue());
            }
            _t += dt;
            float total = 0.35f + _showing.Hold + 0.6f;
            // Pop in with a little overshoot, hold, fade
            float a = _t < 0.25f ? _t / 0.25f : _t > total - 0.6f ? Mathf.Clamp((total - _t) / 0.6f, 0f, 1f) : 1f;
            float pop = _t < 0.35f ? 0.75f + 0.3f * Mathf.Sin(Mathf.Clamp(_t / 0.35f, 0f, 1f) * Mathf.Pi * 0.75f) / 0.92f : 1f;
            _root.Modulate = new Color(1, 1, 1, a);
            _title.PivotOffset = _title.Size / 2f;
            _title.Scale = new Vector2(pop, pop);
            // The bar draws out under the title; the ring bursts out behind it
            float grow = Mathf.Clamp(_t / 0.5f, 0f, 1f);
            _bar.CustomMinimumSize = new Vector2(Mathf.Lerp(40f, 520f, 1f - Mathf.Pow(1f - grow, 3f)), 4);
            var vp = GetViewport().GetVisibleRect().Size;
            float r = Mathf.Lerp(30f, vp.X * 0.42f, 1f - Mathf.Pow(1f - Mathf.Clamp(_t / 0.9f, 0f, 1f), 2f));
            var centre = new Vector2(vp.X * 0.5f, vp.Y * 0.31f);
            _ring.Position = centre - new Vector2(r, r * 0.42f);
            _ring.Size = new Vector2(r * 2f, r * 0.84f);
            _ring.Modulate = new Color(1, 1, 1, Mathf.Clamp(1f - _t / 0.9f, 0f, 1f) * 0.9f);
            PlaceShade(vp);
            if (_t >= total)
            {
                _showing = null;
                _root.Visible = false;
            }
        }

        internal static readonly Color ShadeColor = new(0.01f, 0.015f, 0.03f, 0.66f);
        private Panel _shade;

        /// <summary>The plate behind the words, and the words (tests).</summary>
        internal static Rect2 ShadeRect => _current?._shade?.GetGlobalRect() ?? default;
        internal static Rect2 WordsRect => _current == null ? default : _current.Words(_current.GetViewport().GetVisibleRect().Size);

        /// <summary>The words as drawn: from the kicker (or title) to the detail (or bar), as wide as the widest line.</summary>
        private Rect2 Words(Vector2 vp)
        {
            Control top = _kicker.Visible ? _kicker : _title;
            Control bottom = _detail.Visible ? _detail : _bar;
            float detail = _detail.Visible ? _detail.GetThemeFont("font").GetStringSize(_detail.Text, HorizontalAlignment.Left, -1, 19).X : 0f;
            float w = Mathf.Min(Mathf.Max(Mathf.Max(_title.GetMinimumSize().X, detail), _kicker.Visible ? _kicker.GetMinimumSize().X : 0f), vp.X * 0.8f);
            float y0 = top.GlobalPosition.Y, y1 = bottom.GlobalPosition.Y + bottom.Size.Y;
            return new Rect2(vp.X * 0.5f - w / 2f, y0, w, y1 - y0);
        }

        private void PlaceShade(Vector2 vp)
        {
            var words = Words(vp).Grow(14f);
            float w = Mathf.Max(words.Size.X + 70f, 560f);
            _shade.Position = new Vector2(vp.X * 0.5f - w / 2f, words.Position.Y);
            _shade.Size = new Vector2(w, words.Size.Y);
        }

        private void Begin(Moment m)
        {
            _showing = m;
            _t = 0f;
            _root.Visible = true;
            _kicker.Text = m.Kicker.ToUpperInvariant();
            _kicker.AddThemeColorOverride("font_color", m.Color.Lightened(0.15f));
            _kicker.Visible = m.Kicker.Length > 0;
            _title.Text = m.Title;
            _title.AddThemeColorOverride("font_color", Colors.White);
            _title.AddThemeColorOverride("font_shadow_color", new Color(m.Color, 0.8f));
            _title.AddThemeConstantOverride("shadow_outline_size", 14);
            _detail.Text = m.Detail;
            _detail.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.95f));
            _detail.Visible = m.Detail.Length > 0;
            var bar = new StyleBoxFlat { BgColor = m.Color };
            bar.SetCornerRadiusAll(2);
            bar.ShadowColor = new Color(m.Color, 0.6f);
            bar.ShadowSize = 8;
            _bar.AddThemeStyleboxOverride("panel", bar);
            var ring = new StyleBoxFlat { BgColor = new Color(m.Color, 0.06f), BorderColor = new Color(m.Color, 0.85f), DrawCenter = true };
            ring.SetBorderWidthAll(3);
            ring.SetCornerRadiusAll(2000);
            ring.AntiAliasing = true;
            _ring.AddThemeStyleboxOverride("panel", ring);
            Sfx();
        }

        private static void Sfx()
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("victory");
        }
    }
}
