using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Pooled particles for every transient effect: one MultiMesh per kind (sparks, glows, smoke,
    /// debris, ground rings, lightning segments), simulated here and pushed to the renderer as one
    /// buffer a frame. An effect is a handful of <see cref="Emit"/> calls into fixed arrays, so a
    /// shot, hit or explosion creates no nodes, meshes or materials (the old effects added a node
    /// per flash and per projectile trail dot, every 0.03 s).
    /// One per battle scene, created on first use (<see cref="Get"/>).
    /// </summary>
    public partial class VfxParticles : Node3D
    {
        public enum Kind { Spark, Glow, Smoke, Debris, Ring, Bolt }
        /// <summary>Effects that go off after a delay (a tracer landing).</summary>
        public enum Delayed { SmallImpact }

        private struct Pending { public float Time; public Vector3 Pos; public Color Col; public Delayed What; }
        private readonly Pending[] _pending = new Pending[256];
        private int _pendingCount;

        private sealed class Channel
        {
            public Kind Kind;
            public MultiMesh Mesh;
            public MultiMeshInstance3D Instance;
            public int Count;
            public Vector3[] Pos, Vel, Axis, Spin;
            public Color[] Col;
            public float[] Age, Life, Size, Grow, Gravity, Drag, Stretch;
            public float[] Buffer;
        }

        private readonly Channel[] _channels = new Channel[6];
        private static readonly int[] Capacity = { 1536, 384, 512, 384, 96, 768 };
        private const int Stride = 20; // transform 12 + colour 4 + custom 4

        private static VfxParticles _current;

        /// <summary>Live particles of a kind (for tests and budgets).</summary>
        public int CountOf(Kind kind) => _channels[(int)kind]?.Count ?? 0;
        public int TotalCount { get { int n = 0; foreach (var c in _channels) n += c?.Count ?? 0; return n; } }
        /// <summary>Lowest live particle of a kind (for tests); +infinity when none.</summary>
        public float LowestOf(Kind kind)
        {
            var c = _channels[(int)kind];
            float y = float.PositiveInfinity;
            if (c != null) for (int i = 0; i < c.Count; i++) y = Mathf.Min(y, c.Pos[i].Y);
            return y;
        }

        /// <summary>Particles dropped because a pool was full (should stay 0 in normal play).</summary>
        public int Dropped { get; private set; }

        /// <summary>The scene's particle system, made on first use.</summary>
        public static VfxParticles Get(SceneTree tree)
        {
            if (_current != null && IsInstanceValid(_current) && _current.IsInsideTree()
                && _current.GetTree() == tree && _current.GetParent() == tree.CurrentScene)
                return _current;
            var scene = tree?.CurrentScene;
            if (scene == null) return null;
            _current = new VfxParticles { Name = "VfxParticles" };
            scene.AddChild(_current);
            return _current;
        }

        public override void _Ready()
        {
            for (int k = 0; k < _channels.Length; k++)
                _channels[k] = MakeChannel((Kind)k, Capacity[k]);
        }

        public override void _ExitTree()
        {
            if (_current == this) _current = null;
        }

        private Channel MakeChannel(Kind kind, int cap)
        {
            var c = new Channel
            {
                Kind = kind,
                Pos = new Vector3[cap], Vel = new Vector3[cap], Axis = new Vector3[cap], Spin = new Vector3[cap],
                Col = new Color[cap],
                Age = new float[cap], Life = new float[cap], Size = new float[cap], Grow = new float[cap],
                Gravity = new float[cap], Drag = new float[cap], Stretch = new float[cap],
                Buffer = new float[cap * Stride],
            };
            c.Mesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                UseCustomData = true,
                InstanceCount = cap,
                VisibleInstanceCount = 0,
                Mesh = VfxLook.MeshFor(kind),
            };
            c.Instance = new MultiMeshInstance3D
            {
                Name = kind + "Particles",
                Multimesh = c.Mesh,
                MaterialOverride = VfxLook.MaterialFor(kind),
                CastShadow = kind == Kind.Debris ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                // Particles fly all over the field: never cull the whole set by its stale bounds
                CustomAabb = new Aabb(new Vector3(-500, -100, -500), new Vector3(1000, 400, 1000)),
            };
            AddChild(c.Instance);
            return c;
        }

        /// <summary>
        /// Add one particle. <paramref name="size"/> is its width; it grows by <paramref name="grow"/>
        /// times over its life. Sparks and bolts are streaks along their velocity (or
        /// <paramref name="axis"/>), <paramref name="stretch"/> widths long.
        /// </summary>
        public void Emit(Kind kind, Vector3 pos, Vector3 vel, Color col, float size, float life,
            float grow = 0f, float gravity = 0f, float drag = 0f, Vector3 axis = default, float stretch = 1f)
        {
            var c = _channels[(int)kind];
            if (c == null || life <= 0f) return;
            if (c.Count >= c.Pos.Length) { Dropped++; return; }
            int i = c.Count++;
            c.Pos[i] = pos; c.Vel[i] = vel; c.Col[i] = col;
            c.Age[i] = 0f; c.Life[i] = life; c.Size[i] = size; c.Grow[i] = grow;
            c.Gravity[i] = gravity; c.Drag[i] = drag; c.Stretch[i] = stretch;
            c.Axis[i] = axis;
            c.Spin[i] = kind == Kind.Debris
                ? new Vector3(GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f) * 9f
                : Vector3.Zero;
        }

        /// <summary>Set off <paramref name="what"/> at <paramref name="pos"/> after <paramref name="delay"/> seconds.</summary>
        public void Schedule(float delay, Vector3 pos, Color col, Delayed what)
        {
            if (_pendingCount >= _pending.Length) { Dropped++; return; }
            _pending[_pendingCount++] = new Pending { Time = delay, Pos = pos, Col = col, What = what };
        }

        public override void _Process(double delta)
        {
            long __pt = FrameProfiler.Start();
            Tick((float)delta);
            FrameProfiler.Stop("particles", __pt);
        }

        /// <summary>
        /// Run the effects forward by hand in small steps (sheet renders, where a software-rendered
        /// frame takes longer than most effects live).
        /// </summary>
        public void Advance(float seconds, float step = 1f / 120f)
        {
            for (float t = 0f; t < seconds - 0.0001f; t += step) Tick(Mathf.Min(step, seconds - t));
        }

        /// <summary>Drop every live particle and pending effect.</summary>
        public void Clear()
        {
            _pendingCount = 0;
            foreach (var c in _channels)
            {
                if (c == null) continue;
                c.Count = 0;
                c.Mesh.VisibleInstanceCount = 0;
            }
        }

        private void Tick(float dt)
        {
            for (int i = 0; i < _pendingCount; i++)
            {
                _pending[i].Time -= dt;
                if (_pending[i].Time > 0f) continue;
                var e = _pending[i];
                _pending[i] = _pending[--_pendingCount];
                i--;
                if (e.What == Delayed.SmallImpact) SmallImpact(e.Pos, e.Col);
            }
            foreach (var c in _channels)
                if (c != null && (c.Count > 0 || c.Mesh.VisibleInstanceCount > 0))
                    Step(c, dt);
        }

        private void Step(Channel c, float dt)
        {
            for (int i = 0; i < c.Count; i++)
            {
                c.Age[i] += dt;
                if (c.Age[i] >= c.Life[i])
                {
                    // Swap the last one in and look at this slot again
                    int last = --c.Count;
                    Copy(c, last, i);
                    i--;
                    continue;
                }
                var v = c.Vel[i];
                v.Y -= c.Gravity[i] * dt;
                if (c.Drag[i] > 0f) v /= 1f + c.Drag[i] * dt;
                c.Vel[i] = v;
                c.Pos[i] += v * dt;
                // Debris bounces once off the ground it fell to, then settles
                if (c.Kind == Kind.Debris && c.Pos[i].Y < c.Axis[i].Y && v.Y < 0f)
                {
                    c.Pos[i].Y = c.Axis[i].Y;
                    c.Vel[i] = new Vector3(v.X * 0.5f, -v.Y * 0.3f, v.Z * 0.5f);
                    c.Spin[i] *= 0.5f;
                }
            }
            Write(c);
        }

        private static void Copy(Channel c, int from, int to)
        {
            if (from == to) return;
            c.Pos[to] = c.Pos[from]; c.Vel[to] = c.Vel[from]; c.Axis[to] = c.Axis[from]; c.Spin[to] = c.Spin[from];
            c.Col[to] = c.Col[from]; c.Age[to] = c.Age[from]; c.Life[to] = c.Life[from]; c.Size[to] = c.Size[from];
            c.Grow[to] = c.Grow[from]; c.Gravity[to] = c.Gravity[from]; c.Drag[to] = c.Drag[from]; c.Stretch[to] = c.Stretch[from];
        }

        private static void Write(Channel c)
        {
            var b = c.Buffer;
            for (int i = 0; i < c.Count; i++)
            {
                float t = c.Age[i] / c.Life[i];
                float size = c.Size[i] * (1f + c.Grow[i] * t);
                float alpha = c.Kind switch
                {
                    Kind.Glow => 1f - t * t,
                    Kind.Smoke => Mathf.Min(1f, t / 0.12f) * (1f - t),
                    Kind.Debris => t < 0.75f ? 1f : (1f - t) / 0.25f,
                    Kind.Ring => 1f - t,
                    Kind.Bolt => t < 0.5f ? 1f : (1f - t) * 2f,
                    _ => 1f - t,
                };
                var col = c.Col[i];
                int o = i * Stride;
                // Transform rows (Godot's MultiMesh buffer is row-major 3x4)
                Basis basis;
                if (c.Kind == Kind.Debris)
                {
                    var r = c.Spin[i] * c.Age[i];
                    basis = Basis.FromEuler(r).Scaled(Vector3.One * size);
                }
                else if (c.Kind == Kind.Ring) basis = Basis.Identity.Scaled(new Vector3(size, 1f, size));
                else basis = Basis.Identity.Scaled(Vector3.One * size);
                var p = c.Pos[i];
                b[o + 0] = basis.X.X; b[o + 1] = basis.Y.X; b[o + 2] = basis.Z.X; b[o + 3] = p.X;
                b[o + 4] = basis.X.Y; b[o + 5] = basis.Y.Y; b[o + 6] = basis.Z.Y; b[o + 7] = p.Y;
                b[o + 8] = basis.X.Z; b[o + 9] = basis.Y.Z; b[o + 10] = basis.Z.Z; b[o + 11] = p.Z;
                b[o + 12] = col.R; b[o + 13] = col.G; b[o + 14] = col.B; b[o + 15] = col.A * Mathf.Clamp(alpha, 0f, 1f);
                // Custom: streak axis and length (0 = round billboard)
                Vector3 axis = Vector3.Zero;
                float stretch = 0f;
                if (c.Kind == Kind.Spark)
                {
                    float speed = c.Vel[i].Length();
                    if (speed > 0.01f) { axis = c.Vel[i] / speed; stretch = Mathf.Clamp(speed * 0.05f, 1f, 6f) * c.Stretch[i]; }
                }
                else if (c.Kind == Kind.Bolt) { axis = c.Axis[i]; stretch = c.Stretch[i]; }
                // Debris: x = how hot it glows, cooling over the first half of its life
                else if (c.Kind == Kind.Debris) axis = new Vector3(c.Stretch[i] * Mathf.Max(0f, 1f - t * 2f), 0f, 0f);
                b[o + 16] = axis.X; b[o + 17] = axis.Y; b[o + 18] = axis.Z; b[o + 19] = stretch;
            }
            c.Mesh.VisibleInstanceCount = c.Count;
            if (c.Count > 0)
                RenderingServer.MultimeshSetBuffer(c.Mesh.GetRid(), b);
        }

        private void SmallImpact(Vector3 at, Color col)
        {
            var hot = col.Lerp(Colors.White, 0.4f);
            Emit(Kind.Glow, at, Vector3.Zero, hot, 0.32f, 0.07f, 0.4f);
            for (int i = 0; i < 3; i++)
                Emit(Kind.Spark, at, new Vector3(GD.Randf() - 0.5f, GD.Randf() * 0.8f, GD.Randf() - 0.5f) * 8f, hot, 0.03f, 0.15f, 0f, 9f, 2f);
            Emit(Kind.Smoke, at, Vector3.Up * 0.5f, new Color(0.18f, 0.17f, 0.17f, 0.4f), 0.28f, 0.45f, 1.3f, 0f, 1.5f);
        }

        /// <summary>
        /// Debris settles on the ground at <paramref name="groundY"/> (kept in the unused axis
        /// slot). <paramref name="heat"/> is how hot it glows when thrown (0 for tar, mud).
        /// </summary>
        public void EmitDebris(Vector3 pos, Vector3 vel, Color col, float size, float life, float groundY, float heat = 1f)
            => Emit(Kind.Debris, pos, vel, col, size, life, 0f, 14f, 0.4f, new Vector3(0, groundY, 0), heat);
    }
}
