using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A lobbed shell: it climbs to an apex above both ends and falls onto its target on a true
    /// ballistic arc, with a glowing trail, and calls back when it lands (the Spire's gunner
    /// mortar). One node a shell; the trail is particles. The arc's numbers are shared with the
    /// gunner's aim preview so the dotted line is the path the shell takes.
    /// </summary>
    public partial class LobShell : Node3D
    {
        private Vector3 _from, _v0;
        private Vector3 _to;
        private float _g, _flight, _t, _size, _trail;
        private Color _col;
        private Action<Vector3> _land;
        private MeshInstance3D _mesh;

        /// <summary>Highest point of the arc over the higher end, for a shot <paramref name="flat"/> long.</summary>
        public static float ApexOver(float baseApex, float perUnit, float flat) => baseApex + perUnit * flat;

        /// <summary>Launch speed and flight time for an arc from a to b peaking <paramref name="apexOver"/> above the higher end.</summary>
        public static (Vector3 v0, float flight) Solve(Vector3 a, Vector3 b, float apexOver, float gravity)
        {
            float g = Mathf.Max(1f, gravity);
            float apex = Mathf.Max(a.Y, b.Y) + Mathf.Max(0.2f, apexOver);
            float up = Mathf.Sqrt(2f * g * (apex - a.Y));
            float flight = up / g + Mathf.Sqrt(2f * (apex - b.Y) / g);
            var flatV = new Vector3(b.X - a.X, 0f, b.Z - a.Z) / flight;
            return (flatV + Vector3.Up * up, flight);
        }

        /// <summary>Points along the arc (the aim preview).</summary>
        public static void Path(Vector3 a, Vector3 b, float apexOver, float gravity, List<Vector3> into, int points = 24)
        {
            into.Clear();
            var (v0, flight) = Solve(a, b, apexOver, gravity);
            for (int i = 0; i <= points; i++)
            {
                float t = flight * i / points;
                into.Add(a + v0 * t + Vector3.Down * (0.5f * gravity * t * t));
            }
        }

        /// <summary>Fire one; <paramref name="land"/> is called with where it came down. Returns its flight time.</summary>
        public static float Fire(SceneTree tree, Vector3 from, Vector3 to, Color color, float apexOver, float gravity, float size, Action<Vector3> land)
        {
            var s = new LobShell { Name = "LobShell" };
            tree.CurrentScene.AddChild(s);
            var (v0, flight) = Solve(from, to, apexOver, gravity);
            s._from = from; s._to = to; s._v0 = v0; s._g = gravity; s._flight = flight;
            s._col = color; s._size = size; s._land = land;
            s.GlobalPosition = from;
            s._mesh = new MeshInstance3D
            {
                Mesh = VfxCache.Sphere(0.16f * size),
                MaterialOverride = VfxCache.Glow(color.Lerp(Colors.White, 0.5f), color, 2.6f, alpha: false),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            s.AddChild(s._mesh);
            Launched++;
            return flight;
        }

        /// <summary>Shells fired and landed (tests).</summary>
        public static int Launched { get; private set; }
        public static int Landed { get; private set; }
        /// <summary>The highest point the last shell reached over where it left (tests).</summary>
        public static float LastRise { get; private set; }
        private float _rise;

        public override void _Process(double delta)
        {
            _t += (float)delta;
            if (_t >= _flight)
            {
                GlobalPosition = _to;
                Landed++;
                LastRise = _rise;
                try { _land?.Invoke(_to); }
                finally { QueueFree(); }
                return;
            }
            var p = _from + _v0 * _t + Vector3.Down * (0.5f * _g * _t * _t);
            _rise = Mathf.Max(_rise, p.Y - _from.Y);
            GlobalPosition = p;
            var vel = _v0 + Vector3.Down * (_g * _t);
            if (vel.LengthSquared() > 0.001f)
                _mesh.Basis = Basis.LookingAt(vel.Normalized(), Mathf.Abs(vel.Normalized().Y) > 0.95f ? Vector3.Right : Vector3.Up).Scaled(new Vector3(1f, 1f, 1.8f));
            // A glowing trail and a wisp of smoke, so the arc can be followed
            if ((_trail += (float)delta) >= 0.02f)
            {
                _trail = 0f;
                var vp = VfxParticles.Get(GetTree());
                vp?.Emit(VfxParticles.Kind.Glow, p, Vector3.Zero, _col, 0.34f * _size, 0.22f);
                vp?.Emit(VfxParticles.Kind.Smoke, p, Vector3.Up * 0.2f, new Color(0.5f, 0.5f, 0.55f, 0.28f), 0.18f * _size, 0.45f, 1.1f, 0f, 1.2f);
            }
        }
    }
}
