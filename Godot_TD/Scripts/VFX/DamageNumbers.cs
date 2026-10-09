using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Numbers over what gets hit, pooled. Hits on an enemy are added up and shown every
    /// <see cref="Every"/> seconds as one number (a minigun lands dozens a second); hits on BIT
    /// show at once, in red. Off with Settings, Damage numbers. At most <see cref="Pool"/> numbers
    /// are up at once; the oldest is reused.
    /// </summary>
    public partial class DamageNumbers : Node3D
    {
        public const int Pool = 48;
        public const float Every = 0.35f, Life = 0.85f;

        private static DamageNumbers _current;
        private readonly Dictionary<ulong, (VineEnemy enemy, float sum, float t)> _pending = new();
        private readonly List<(Label3D label, float t, Vector3 from)> _live = new();
        private readonly Queue<Label3D> _free = new();

        /// <summary>Numbers shown since the scene began (tests).</summary>
        public static int ShownCount { get; private set; }
        public static int LastBitNumber { get; private set; }

        private static DamageNumbers Get(bool numbers = true)
        {
            if (numbers && !GameSettings.DamageNumbers) return null;
            // Called for every hit: a managed flag, not an engine call, says it's still in the tree
            if (_current != null && _current._inTree && IsInstanceValid(_current)) return _current;
            if (Engine.GetMainLoop() is not SceneTree tree || tree.CurrentScene == null) return null;
            _current = new DamageNumbers { Name = "DamageNumbers" };
            tree.CurrentScene.AddChild(_current);
            ShownCount = 0;
            return _current;
        }

        /// <summary>An enemy took <paramref name="amount"/>; it shows with the next batch for that enemy.</summary>
        public static void Enemy(VineEnemy e, float amount)
        {
            if (amount <= 0f || e == null) return;
            var d = Get();
            if (d == null) return;
            ulong id = e.GetInstanceId();
            d._pending[id] = d._pending.TryGetValue(id, out var p) ? (e, p.sum + amount, p.t) : (e, amount, Every * 0.5f);
        }

        /// <summary>BIT took a hit: a red number at once.</summary>
        public static void Bit(Vector3 at, float amount)
        {
            if (amount <= 0f) return;
            var d = Get();
            if (d == null) return;
            LastBitNumber = Mathf.Max(1, Mathf.RoundToInt(amount));
            d.Show(at + Vector3.Up * 2.2f, $"-{LastBitNumber}", new Color(1f, 0.3f, 0.25f), 40);
        }

        /// <summary>Nodes the pool holds, itself included (tests that count nodes leave these out: the pool is bounded).</summary>
        public static int PooledNodes => _current != null && IsInstanceValid(_current) ? _current.GetChildCount() + 1 : 0;

        /// <summary>Tags shown since the scene began (tests), and the last one's text.</summary>
        public static int TagCount { get; private set; }
        public static string LastTag { get; private set; }

        /// <summary>A small tag that floats up and fades (what boosts a tower). Not tied to the damage numbers setting.</summary>
        public static void Tag(Vector3 at, string text, Color color)
        {
            var d = Get(numbers: false);
            if (d == null || string.IsNullOrEmpty(text)) return;
            TagCount++;
            LastTag = text;
            d.Show(at, text, color, 20);
        }

        private bool _inTree;
        public override void _EnterTree() => _inTree = true;

        public override void _ExitTree()
        {
            _inTree = false;
            if (_current == this) _current = null;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (_pending.Count > 0)
            {
                var done = new List<ulong>();
                foreach (var kv in _pending)
                {
                    var (e, sum, t) = kv.Value;
                    t -= dt;
                    if (t > 0f) { _pending[kv.Key] = (e, sum, t); continue; }
                    done.Add(kv.Key);
                    if (!IsInstanceValid(e)) continue;
                    var at = e.GlobalPosition + Vector3.Up * (e.IsBoss ? 3.2f : 1.9f);
                    bool big = sum >= e.MaxHealth * 0.25f;
                    Show(at, Format(sum), big ? new Color(1f, 0.85f, 0.3f) : new Color(0.95f, 0.95f, 0.95f), big ? 34 : 26);
                }
                foreach (var k in done) _pending.Remove(k);
            }
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var (label, t, from) = _live[i];
                t += dt;
                if (t >= Life || !IsInstanceValid(label))
                {
                    if (IsInstanceValid(label)) { label.Visible = false; _free.Enqueue(label); }
                    _live.RemoveAt(i);
                    continue;
                }
                float k = t / Life;
                label.GlobalPosition = from + Vector3.Up * (0.9f * (1f - (1f - k) * (1f - k)));
                var m = label.Modulate;
                label.Modulate = new Color(m.R, m.G, m.B, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                label.OutlineModulate = new Color(0, 0, 0, label.Modulate.A * 0.9f);
                _live[i] = (label, t, from);
            }
        }

        private static string Format(float v) => v >= 10000f ? $"{v / 1000f:0}k" : v >= 1000f ? $"{v / 1000f:0.0}k" : $"{Mathf.RoundToInt(v)}";

        private void Show(Vector3 at, string text, Color color, int size)
        {
            Label3D l;
            if (_free.Count > 0) l = _free.Dequeue();
            else if (_live.Count < Pool)
            {
                l = new Label3D
                {
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    NoDepthTest = true,
                    FixedSize = true,
                    PixelSize = 0.0022f,
                    OutlineSize = 10,
                    RenderPriority = 10,
                    OutlineRenderPriority = 9,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(l);
            }
            else
            {
                // Reuse the oldest
                l = _live[0].label;
                _live.RemoveAt(0);
            }
            l.Text = text;
            l.FontSize = size;
            l.Modulate = color;
            l.Visible = true;
            var jitter = new Vector3((float)GD.RandRange(-0.25, 0.25), 0f, (float)GD.RandRange(-0.25, 0.25));
            l.GlobalPosition = at + jitter;
            _live.Add((l, 0f, at + jitter));
            ShownCount++;
        }
    }
}
