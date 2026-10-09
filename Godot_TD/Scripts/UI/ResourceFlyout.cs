using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What an enemy drops, made visible: a pop of chips where it fell, a "+N" over the spot, and
    /// the chips arcing up the screen into the Resources counter, which pulses as they land.
    /// Drops used to add to the number with nothing on screen. Chips are pooled (at most
    /// <see cref="MaxChips"/> in the air; past that a drop shows its "+N" and pulses the counter).
    /// </summary>
    public partial class ResourceFlyout : Control
    {
        public const int MaxChips = 48;
        private const float Rise = 0.22f, Fly = 0.6f;

        private sealed class Chip
        {
            public Label Node;
            public Vector2 From, Peak;
            public float T;
            public bool Text; // the "+N" label: floats up and fades, doesn't fly
        }

        private readonly List<Chip> _live = new();
        private readonly Stack<Label> _pool = new();
        private Control _target;
        private float _pulse;
        private Vector2 _targetBaseScale = Vector2.One;
        private static readonly Color Gold = new(1f, 0.82f, 0.3f);

        /// <summary>Drops shown so far (tests).</summary>
        public int Shown { get; private set; }
        /// <summary>Chips that reached the counter (tests).</summary>
        public int Landed { get; private set; }
        public int InFlight => _live.Count;

        public void Init(Control target)
        {
            _target = target;
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);
        }

        /// <summary>Show a drop of <paramref name="amount"/> at a world position.</summary>
        public void Drop(Vector3 world, int amount)
        {
            if (amount <= 0 || !IsInsideTree()) return;
            var cam = GetViewport()?.GetCamera3D();
            if (cam == null || cam.IsPositionBehind(world)) return;
            var screen = cam.UnprojectPosition(world + Vector3.Up * 1.2f);
            // Into this layer's coordinates (the HUD's canvas can be scaled)
            screen = GetGlobalTransformWithCanvas().AffineInverse() * screen;
            var rect = GetViewportRect();
            if (!rect.Grow(40f).HasPoint(screen)) return;
            Shown++;

            Spawn(screen, screen + new Vector2(0, -36f), $"+{amount}", true);
            int chips = Mathf.Clamp(1 + amount / 4, 1, 4);
            var rng = new RandomNumberGenerator();
            for (int i = 0; i < chips && _live.Count < MaxChips; i++)
                Spawn(screen, screen + new Vector2(rng.RandfRange(-26f, 26f), rng.RandfRange(-46f, -22f)), "◆", false);
            if (_live.Count >= MaxChips) _pulse = 1f;
        }

        private void Spawn(Vector2 from, Vector2 peak, string text, bool isText)
        {
            var l = _pool.Count > 0 ? _pool.Pop() : MakeLabel();
            l.Text = text;
            l.Visible = true;
            l.Position = from;
            l.Modulate = Colors.White;
            l.Scale = Vector2.One;
            l.AddThemeFontSizeOverride("font_size", isText ? 18 : 16);
            _live.Add(new Chip { Node = l, From = from, Peak = peak, T = 0f, Text = isText });
        }

        private Label MakeLabel()
        {
            var l = new Label { MouseFilter = MouseFilterEnum.Ignore };
            l.AddThemeColorOverride("font_color", Gold);
            l.AddThemeColorOverride("font_outline_color", new Color(0.15f, 0.08f, 0f, 0.9f));
            l.AddThemeConstantOverride("outline_size", 5);
            AddChild(l);
            return l;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (_live.Count > 0)
            {
                Vector2 to = _target != null && IsInstanceValid(_target)
                    ? _target.GlobalPosition + new Vector2(_target.Size.X * 0.25f, _target.Size.Y * 0.5f)
                    : new Vector2(60, 20);
                for (int i = _live.Count - 1; i >= 0; i--)
                {
                    var c = _live[i];
                    c.T += dt;
                    if (c.Text)
                    {
                        float k = c.T / 0.9f;
                        c.Node.Position = c.From.Lerp(c.Peak, Mathf.Min(1f, k * 1.6f));
                        c.Node.Modulate = new Color(1, 1, 1, Mathf.Clamp(1.4f - k * 1.4f, 0f, 1f));
                        if (k >= 1f) Retire(i);
                        continue;
                    }
                    if (c.T < Rise)
                    {
                        float k = c.T / Rise;
                        c.Node.Position = c.From.Lerp(c.Peak, 1f - (1f - k) * (1f - k));
                    }
                    else
                    {
                        float k = Mathf.Min(1f, (c.T - Rise) / Fly);
                        float e = k * k * (3f - 2f * k);
                        // A curve: out sideways first, then up into the counter
                        var mid = new Vector2(Mathf.Lerp(c.Peak.X, to.X, 0.2f), Mathf.Lerp(c.Peak.Y, to.Y, 0.75f));
                        var a = c.Peak.Lerp(mid, e);
                        var b = mid.Lerp(to, e);
                        c.Node.Position = a.Lerp(b, e);
                        float s = Mathf.Lerp(1f, 0.6f, e);
                        c.Node.Scale = new Vector2(s, s);
                        if (k >= 1f)
                        {
                            Landed++;
                            _pulse = 1f;
                            Retire(i);
                        }
                    }
                }
            }
            if (_target != null && IsInstanceValid(_target))
            {
                if (_pulse > 0f)
                {
                    _pulse = Mathf.Max(0f, _pulse - dt * 4f);
                    float s = 1f + 0.14f * _pulse;
                    _target.PivotOffset = new Vector2(0, _target.Size.Y * 0.5f);
                    _target.Scale = _targetBaseScale * s;
                    _target.Modulate = Colors.White.Lerp(new Color(1.4f, 1.25f, 0.7f), _pulse);
                }
                else if (_target.Scale != _targetBaseScale)
                {
                    _target.Scale = _targetBaseScale;
                    _target.Modulate = Colors.White;
                }
            }
        }

        private void Retire(int i)
        {
            var c = _live[i];
            c.Node.Visible = false;
            _pool.Push(c.Node);
            _live.RemoveAt(i);
        }
    }
}
