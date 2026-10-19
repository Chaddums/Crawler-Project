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

        /// <summary>
        /// Fire one; <paramref name="land"/> is called with where it came down. Returns its flight
        /// time. <paramref name="rock"/>: a tumbling rock with embers in its cracks, slow enough to
        /// follow (the Spire's trebuchet), instead of a glowing slug.
        /// </summary>
        public static float Fire(SceneTree tree, Vector3 from, Vector3 to, Color color, float apexOver, float gravity, float size, Action<Vector3> land, bool rock = false)
        {
            var s = new LobShell { Name = "LobShell" };
            tree.CurrentScene.AddChild(s);
            var (v0, flight) = Solve(from, to, apexOver, gravity);
            s._from = from; s._to = to; s._v0 = v0; s._g = gravity; s._flight = flight;
            s._col = color; s._size = size; s._land = land; s._rock = rock;
            s.GlobalPosition = from;
            s._mesh = new MeshInstance3D
            {
                Mesh = rock ? RockMesh : VfxCache.Sphere(0.16f * size),
                MaterialOverride = rock ? RockMaterial : VfxCache.Glow(color.Lerp(Colors.White, 0.5f), color, 2.6f, alpha: false),
                CastShadow = rock ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            };
            if (rock)
            {
                s._mesh.Scale = new Vector3(1.1f, 0.85f, 0.95f) * (0.16f * size);
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                s._spinAxis = new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f)).Normalized();
                if (s._spinAxis.LengthSquared() < 0.01f) s._spinAxis = Vector3.Right;
                s._spinRate = rng.RandfRange(3f, 6f);
            }
            s.AddChild(s._mesh);
            Launched++;
            return flight;
        }

        private bool _rock;
        private Vector3 _spinAxis = Vector3.Right;
        private float _spinRate;
        private static Mesh _rockMesh;
        private static StandardMaterial3D _rockMat;

        /// <summary>A lumpy unit rock: a coarse sphere with its points pushed in and out.</summary>
        private static Mesh RockMesh
        {
            get
            {
                if (_rockMesh != null) return _rockMesh;
                var src = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 9, Rings = 5 };
                var arrays = src.GetMeshArrays();
                var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var rng = new RandomNumberGenerator { Seed = 7 };
                // The same direction gets the same push, so the seams stay closed
                var push = new Dictionary<Vector3I, float>();
                for (int i = 0; i < verts.Length; i++)
                {
                    var key = new Vector3I(Mathf.RoundToInt(verts[i].X * 100), Mathf.RoundToInt(verts[i].Y * 100), Mathf.RoundToInt(verts[i].Z * 100));
                    if (!push.TryGetValue(key, out float k)) { k = rng.RandfRange(0.72f, 1.12f); push[key] = k; }
                    verts[i] *= k;
                }
                var st = new SurfaceTool();
                st.Begin(Mesh.PrimitiveType.Triangles);
                var idx = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                // Flat faces: each triangle on its own, so it reads as chipped stone
                st.SetSmoothGroup(uint.MaxValue);
                foreach (int i in idx) st.AddVertex(verts[i]);
                st.GenerateNormals();
                _rockMesh = st.Commit();
                return _rockMesh;
            }
        }

        private static StandardMaterial3D RockMaterial => _rockMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.34f, 0.29f, 0.25f),
            Roughness = 1f,
            // Embers in its cracks, so it can be followed against the night
            EmissionEnabled = true,
            Emission = new Color(1f, 0.45f, 0.15f),
            EmissionEnergyMultiplier = 0.55f,
            RimEnabled = true,
            Rim = 0.6f,
            RimTint = 0.3f,
        };

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
            if (_rock)
            {
                // It tumbles, trailing dust and the odd ember
                _mesh.RotateObjectLocal(_spinAxis, _spinRate * (float)delta);
                if ((_trail += (float)delta) >= 0.04f)
                {
                    _trail = 0f;
                    var vp = VfxParticles.Get(GetTree());
                    vp?.Emit(VfxParticles.Kind.Smoke, p, Vector3.Up * 0.15f, new Color(0.55f, 0.5f, 0.45f, 0.3f), 0.3f * _size, 0.6f, 1.2f, 0f, 1.4f);
                    vp?.Emit(VfxParticles.Kind.Glow, p, Vector3.Zero, new Color(1f, 0.5f, 0.2f), 0.12f * _size, 0.25f);
                }
                return;
            }
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
