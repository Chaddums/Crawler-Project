using System.Collections.Generic;
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
            if (_t >= total)
            {
                _showing = null;
                _root.Visible = false;
            }
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
