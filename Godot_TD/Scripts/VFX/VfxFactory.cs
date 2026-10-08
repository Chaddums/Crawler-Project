using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Creates visual effects: death bursts, hit flashes, muzzle flashes,
    /// splash rings, scrap collect pops. All self-cleaning (QueueFree after lifetime).
    /// </summary>
    public static class VfxFactory
    {
        private static VfxParticles P(SceneTree tree) => tree != null ? VfxParticles.Get(tree) : null;
        private static readonly RandomNumberGenerator _rng = new();
        private static float R(float a, float b) => _rng.RandfRange(a, b);
        private static Vector3 RandomDir() => new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f)).Normalized();

        /// <summary>A random direction within <paramref name="cone"/> radians of <paramref name="dir"/>.</summary>
        private static Vector3 InCone(Vector3 dir, float cone)
        {
            var d = dir.LengthSquared() > 0.0001f ? dir.Normalized() : Vector3.Up;
            var side = d.Cross(Mathf.Abs(d.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
            var up = side.Cross(d);
            float a = R(0f, Mathf.Tau), r = Mathf.Tan(cone) * Mathf.Sqrt(R(0f, 1f));
            return (d + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * r).Normalized();
        }

        private static float Ground(Vector3 at)
            => ServiceLocator.TryGet<VineGrid>(out var g) ? g.GetWorldHeight(at.X, at.Z) : at.Y - 0.5f;

        private static Color TypeColor(DamageType t) => t switch
        {
            DamageType.Fire => new Color(1f, 0.5f, 0.12f),
            DamageType.Ice => new Color(0.45f, 0.8f, 1f),
            DamageType.Lightning => new Color(0.65f, 0.75f, 1f),
            DamageType.Poison => new Color(0.4f, 0.95f, 0.3f),
            _ => new Color(1f, 0.78f, 0.4f),
        };

        /// <summary>An enemy (or tower) breaks apart: hot metal chunks, sparks, a puff of smoke.</summary>
        public static void SpawnDeathBurst(SceneTree tree, Vector3 position, Color tint, int fragmentCount = 6)
        {
            var p = P(tree);
            if (p == null) return;
            float g = Ground(position);
            var metal = tint.Lerp(new Color(0.35f, 0.33f, 0.32f), 0.55f);
            for (int i = 0; i < fragmentCount * 2; i++)
            {
                var v = new Vector3(R(-3.5f, 3.5f), R(2.5f, 6f), R(-3.5f, 3.5f));
                p.EmitDebris(position + Vector3.Up * 0.4f, v, metal.Lightened(R(-0.1f, 0.25f)), R(0.07f, 0.17f), R(0.9f, 1.4f), g);
            }
            for (int i = 0; i < 10; i++)
                p.Emit(VfxParticles.Kind.Spark, position + Vector3.Up * 0.5f, RandomDir() * R(4f, 9f) + Vector3.Up * 2f,
                    new Color(1f, 0.6f, 0.25f), R(0.05f, 0.09f), R(0.25f, 0.5f), 0f, 9f, 1.5f);
            p.Emit(VfxParticles.Kind.Glow, position + Vector3.Up * 0.5f, Vector3.Zero, new Color(1f, 0.6f, 0.25f), 1.6f, 0.18f, 0.6f);
            for (int i = 0; i < 4; i++)
                p.Emit(VfxParticles.Kind.Smoke, position + new Vector3(R(-0.3f, 0.3f), R(0.2f, 0.6f), R(-0.3f, 0.3f)),
                    new Vector3(R(-0.4f, 0.4f), R(0.6f, 1.2f), R(-0.4f, 0.4f)), new Color(0.16f, 0.15f, 0.15f, 0.7f), R(0.7f, 1.1f), R(0.9f, 1.4f), 1.6f, 0f, 1.2f);
        }

        /// <summary>A burst of energy (abilities, level-ups, BIT arriving): light and sparks, no wreckage.</summary>
        public static void SpawnEnergyBurst(SceneTree tree, Vector3 position, Color color, int count = 8)
        {
            var p = P(tree);
            if (p == null) return;
            var hot = color.Lerp(Colors.White, 0.4f);
            p.Emit(VfxParticles.Kind.Glow, position, Vector3.Zero, hot, 1.4f, 0.2f, 0.7f);
            for (int i = 0; i < count * 2; i++)
                p.Emit(VfxParticles.Kind.Spark, position, RandomDir() * R(3f, 7f) + Vector3.Up * 1.5f, hot, R(0.04f, 0.07f), R(0.3f, 0.55f), 0f, 5f, 1.5f);
            for (int i = 0; i < count; i++)
                p.Emit(VfxParticles.Kind.Glow, position + RandomDir() * 0.4f, new Vector3(R(-0.6f, 0.6f), R(1.2f, 2.5f), R(-0.6f, 0.6f)),
                    color, R(0.1f, 0.2f), R(0.6f, 1f), 0f, 0f, 0.8f);
            p.Emit(VfxParticles.Kind.Ring, new Vector3(position.X, Ground(position) + 0.08f, position.Z), Vector3.Zero, color, 0.8f, 0.45f, 2.5f);
        }

        /// <summary>A boss goes down: a much bigger burst with a shockwave along the ground.</summary>
        public static void SpawnBossDeathBurst(SceneTree tree, Vector3 position, Color tint)
        {
            var p = P(tree);
            if (p == null) return;
            SpawnDeathBurst(tree, position, tint, 12);
            SpawnExplosion(tree, position + Vector3.Up * 0.6f, 4f, new Color(1f, 0.6f, 0.2f));
            p.Emit(VfxParticles.Kind.Glow, position + Vector3.Up * 1f, Vector3.Zero, new Color(1f, 0.8f, 0.45f), 5f, 0.4f, 0.8f);
            for (int i = 0; i < 12; i++)
                p.Emit(VfxParticles.Kind.Smoke, position + new Vector3(R(-1f, 1f), R(0.3f, 1.5f), R(-1f, 1f)),
                    new Vector3(R(-1f, 1f), R(1f, 2.2f), R(-1f, 1f)), new Color(0.13f, 0.12f, 0.12f, 0.75f), R(1.4f, 2.2f), R(1.6f, 2.4f), 1.8f, 0f, 1f);
        }

        /// <summary>Wave cleared: motes of light rise from the Spire.</summary>
        public static void SpawnWaveCompleteBurst(SceneTree tree, Vector3 position)
        {
            var p = P(tree);
            if (p == null) return;
            for (int i = 0; i < 40; i++)
            {
                var at = position + new Vector3(R(-2.5f, 2.5f), R(0f, 1.5f), R(-2.5f, 2.5f));
                p.Emit(VfxParticles.Kind.Glow, at, new Vector3(R(-0.3f, 0.3f), R(2f, 4.5f), R(-0.3f, 0.3f)),
                    new Color(0.85f, 0.92f, 1f), R(0.12f, 0.26f), R(1.2f, 2f), 0f, -0.5f, 0.6f);
            }
            p.Emit(VfxParticles.Kind.Ring, position + Vector3.Up * 0.1f, Vector3.Zero, new Color(0.8f, 0.9f, 1f), 2f, 0.8f, 3.5f);
        }

        /// <summary>Something was hit: sparks off the surface and a quick flash.</summary>
        public static void SpawnHitFlash(SceneTree tree, Vector3 position, DamageType damageType)
            => SpawnImpact(tree, position + new Vector3(0, 0.3f, 0), TypeColor(damageType));

        /// <summary>Sparks, a flash and a wisp of smoke where a shot lands.</summary>
        public static void SpawnImpact(SceneTree tree, Vector3 position, Color color, float scale = 1f)
        {
            var p = P(tree);
            if (p == null) return;
            var hot = color.Lerp(Colors.White, 0.35f);
            p.Emit(VfxParticles.Kind.Glow, position, Vector3.Zero, hot, 0.7f * scale, 0.1f, 0.5f);
            int n = Mathf.RoundToInt(6 * scale);
            for (int i = 0; i < n; i++)
                p.Emit(VfxParticles.Kind.Spark, position, RandomDir() * R(3f, 7f) * scale + Vector3.Up * 1.5f,
                    hot, R(0.035f, 0.06f) * scale, R(0.15f, 0.32f), 0f, 10f, 2f);
            p.Emit(VfxParticles.Kind.Smoke, position, new Vector3(R(-0.2f, 0.2f), 0.6f, R(-0.2f, 0.2f)),
                new Color(0.2f, 0.19f, 0.18f, 0.45f), 0.35f * scale, 0.6f, 1.4f, 0f, 1.5f);
        }

        /// <summary>A gun fires: a flash at the barrel, sparks and a puff out along it.</summary>
        public static void SpawnMuzzleFlash(SceneTree tree, Vector3 position, Vector3 direction, Color color, float scale = 1f)
        {
            var p = P(tree);
            if (p == null) return;
            var dir = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector3.Zero;
            var hot = color.Lerp(Colors.White, 0.5f);
            p.Emit(VfxParticles.Kind.Glow, position, Vector3.Zero, hot, 0.7f * scale, 0.06f, 0.4f);
            p.Emit(VfxParticles.Kind.Glow, position + dir * 0.22f * scale, Vector3.Zero, color, 0.5f * scale, 0.08f, 0.6f);
            for (int i = 0; i < 5; i++)
            {
                var v = dir == Vector3.Zero ? RandomDir() : InCone(dir, 0.35f);
                p.Emit(VfxParticles.Kind.Spark, position, v * R(5f, 10f) * scale, hot, 0.04f * scale, R(0.06f, 0.14f), 0f, 0f, 4f);
            }
            if (dir != Vector3.Zero)
                p.Emit(VfxParticles.Kind.Smoke, position + dir * 0.2f, dir * 0.8f + Vector3.Up * 0.4f,
                    new Color(0.3f, 0.29f, 0.28f, 0.35f), 0.28f * scale, 0.5f, 1.8f, 0f, 2f);
        }

        /// <summary>Barrel flash with no known direction (BIT's guns, old callers).</summary>
        public static void SpawnMuzzleFlash(SceneTree tree, Vector3 position, DamageType damageType)
            => SpawnMuzzleFlash(tree, position, Vector3.Zero, TypeColor(damageType), 0.8f);

        /// <summary>An explosion: fireball, sparks, smoke, debris and a ring along the ground.</summary>
        public static void SpawnExplosion(SceneTree tree, Vector3 position, float radius, Color color)
        {
            var p = P(tree);
            if (p == null) return;
            float g = Ground(position);
            var ground = new Vector3(position.X, g + 0.06f, position.Z);
            var hot = color.Lerp(new Color(1f, 0.9f, 0.6f), 0.4f);
            p.Emit(VfxParticles.Kind.Glow, position + Vector3.Up * 0.4f, Vector3.Zero, hot, radius * 0.9f, 0.16f, 0.8f);
            p.Emit(VfxParticles.Kind.Glow, position + Vector3.Up * 0.6f, Vector3.Zero, color, radius * 0.6f, 0.32f, 1.2f);
            p.Emit(VfxParticles.Kind.Ring, ground, Vector3.Zero, hot, radius * 0.6f, 0.35f, 2.6f);
            for (int i = 0; i < 18; i++)
                p.Emit(VfxParticles.Kind.Spark, position + Vector3.Up * 0.4f, RandomDir() * R(5f, 11f) + Vector3.Up * 3f,
                    hot, R(0.05f, 0.09f), R(0.3f, 0.6f), 0f, 12f, 1f);
            for (int i = 0; i < 8; i++)
            {
                var off = new Vector3(R(-1f, 1f), R(0.1f, 0.8f), R(-1f, 1f)) * radius * 0.4f;
                p.Emit(VfxParticles.Kind.Smoke, position + off, off.Normalized() * R(0.4f, 1.2f) + Vector3.Up * 0.8f,
                    new Color(0.14f, 0.13f, 0.12f, 0.7f), R(0.6f, 1f) * radius * 0.5f, R(1f, 1.6f), 1.5f, 0f, 1.6f);
            }
            for (int i = 0; i < 6; i++)
                p.EmitDebris(position + Vector3.Up * 0.3f, new Vector3(R(-4f, 4f), R(3f, 7f), R(-4f, 4f)),
                    new Color(0.3f, 0.28f, 0.26f), R(0.06f, 0.13f), R(0.8f, 1.2f), g);
        }

        /// <summary>Splash damage: an explosion sized to the blast.</summary>
        public static void SpawnSplashRing(SceneTree tree, Vector3 position, float radius, DamageType damageType)
            => SpawnExplosion(tree, position, Mathf.Max(radius, 0.6f), TypeColor(damageType));

        /// <summary>Resources picked up: a little twinkle.</summary>
        public static void SpawnScrapCollectPop(SceneTree tree, Vector3 position)
        {
            var p = P(tree);
            if (p == null) return;
            for (int i = 0; i < 6; i++)
                p.Emit(VfxParticles.Kind.Glow, position, RandomDir() * R(0.8f, 1.8f) + Vector3.Up, new Color(1f, 0.85f, 0.25f), R(0.1f, 0.18f), R(0.3f, 0.5f), 0f, 3f);
        }

        /// <summary>A shot that flies from origin to target, with a trail, landing in an impact.</summary>
        public static void SpawnProjectile(SceneTree tree, Vector3 from, Vector3 to,
            Color color, float speed = 18f, float size = 1f, ProjectileImpact impact = ProjectileImpact.Sparks)
        {
            var proj = new VineProjectile();
            tree.CurrentScene.AddChild(proj);
            proj.Initialize(from, to, color, speed, size, impact);
        }

        /// <summary>
        /// A tracer: a streak that flies to the target and bursts there, all particles (for
        /// rapid fire that would otherwise make a node per round).
        /// </summary>
        public static void SpawnTracer(SceneTree tree, Vector3 from, Vector3 to, Color color, float speed = 40f)
        {
            var p = P(tree);
            if (p == null) return;
            var d = to - from;
            float dist = d.Length();
            if (dist < 0.05f) return;
            float life = dist / speed;
            var hot = color.Lerp(Colors.White, 0.35f);
            p.Emit(VfxParticles.Kind.Spark, from, d / life, hot, 0.09f, life, 0f, 0f, 0f, default, 4.5f);
            p.Emit(VfxParticles.Kind.Glow, from, d / life, color, 0.22f, life);
            p.Schedule(life, to, color, VfxParticles.Delayed.SmallImpact);
        }

        /// <summary>Lightning between two points: a jagged bolt that flickers and a flash at each end.</summary>
        public static void SpawnArc(SceneTree tree, Vector3 from, Vector3 to, Color color)
        {
            var p = P(tree);
            if (p == null) return;
            var hot = color.Lerp(Colors.White, 0.45f);
            for (int pass = 0; pass < 2; pass++)
            {
                var d = to - from;
                float len = d.Length();
                if (len < 0.05f) return;
                int segs = Mathf.Clamp(Mathf.RoundToInt(len / 0.7f), 3, 14);
                var side = d.Cross(Vector3.Up);
                side = side.LengthSquared() < 0.0001f ? Vector3.Right : side.Normalized();
                var up = side.Cross(d / len);
                var prev = from;
                for (int i = 1; i <= segs; i++)
                {
                    float t = (float)i / segs;
                    float jitter = i == segs ? 0f : len * 0.07f * (pass == 0 ? 1f : 0.6f);
                    var next = from + d * t + side * R(-jitter, jitter) + up * R(-jitter, jitter);
                    var seg = next - prev;
                    float sl = seg.Length();
                    float width = pass == 0 ? 0.09f : 0.05f;
                    if (sl > 0.001f)
                        p.Emit(VfxParticles.Kind.Bolt, (prev + next) * 0.5f, Vector3.Zero, pass == 0 ? color : hot,
                            width, pass == 0 ? 0.16f : 0.1f, 0f, 0f, 0f, seg / sl, sl / width);
                    prev = next;
                }
            }
            p.Emit(VfxParticles.Kind.Glow, from, Vector3.Zero, hot, 0.45f, 0.1f, 0.4f);
            p.Emit(VfxParticles.Kind.Glow, to, Vector3.Zero, hot, 0.6f, 0.14f, 0.5f);
            for (int i = 0; i < 4; i++)
                p.Emit(VfxParticles.Kind.Spark, to, RandomDir() * R(3f, 6f), hot, 0.035f, R(0.1f, 0.2f), 0f, 6f, 2f);
        }

        /// <summary>A gob of tar lands: a dark splash and droplets.</summary>
        public static void SpawnTarSplat(SceneTree tree, Vector3 position)
        {
            var p = P(tree);
            if (p == null) return;
            // Pitch with a violet sheen, so the splash reads on dark ground too
            var tar = new Color(0.17f, 0.12f, 0.24f, 0.95f);
            for (int i = 0; i < 6; i++)
                p.Emit(VfxParticles.Kind.Smoke, position + new Vector3(R(-0.3f, 0.3f), 0.15f, R(-0.3f, 0.3f)),
                    new Vector3(R(-1.4f, 1.4f), R(0.3f, 1.3f), R(-1.4f, 1.4f)), tar, R(0.4f, 0.65f), R(0.4f, 0.6f), 0.9f, 3f, 3f);
            float g = Ground(position);
            for (int i = 0; i < 10; i++)
                p.EmitDebris(position + Vector3.Up * 0.2f, new Vector3(R(-2.8f, 2.8f), R(1.5f, 3.8f), R(-2.8f, 2.8f)),
                    new Color(0.13f, 0.1f, 0.18f), R(0.06f, 0.12f), R(0.6f, 0.9f), g, 0f);
        }

        /// <summary>A ring sweeping out along the ground (slow fields, shoves, level-ups).</summary>
        public static void SpawnAreaPulse(SceneTree tree, Vector3 position, float radius,
            Color color, float lifetime = 0.6f, float strength = 0.8f)
        {
            var p = P(tree);
            if (p == null) return;
            var at = new Vector3(position.X, Ground(position) + 0.08f, position.Z);
            p.Emit(VfxParticles.Kind.Ring, at, Vector3.Zero, new Color(color.R, color.G, color.B, strength), radius * 2f * 0.55f, lifetime, 0.82f);
        }

        /// <summary>AXIS sets off a corruption: a big ring and a flash.</summary>
        public static void SpawnCorruptionPulse(SceneTree tree, Vector3 position, Color color)
        {
            var p = P(tree);
            if (p == null) return;
            p.Emit(VfxParticles.Kind.Ring, position + Vector3.Up * 0.1f, Vector3.Zero, color, 2f, 1f, 8f);
            p.Emit(VfxParticles.Kind.Glow, position + Vector3.Up * 0.6f, Vector3.Zero, color, 2.2f, 0.4f, 1f);
            for (int i = 0; i < 16; i++)
                p.Emit(VfxParticles.Kind.Spark, position + Vector3.Up * 0.5f, RandomDir() * R(4f, 8f), color, 0.06f, R(0.4f, 0.7f), 0f, 3f, 1f);
        }

        /// <summary>A node acts on a signal: a quick flash.</summary>
        public static void SpawnSignalBurst(SceneTree tree, Vector3 position, Color color)
        {
            var p = P(tree);
            if (p == null) return;
            p.Emit(VfxParticles.Kind.Glow, position + Vector3.Up * 0.5f, Vector3.Zero, color, 0.7f, 0.2f, 1.5f);
        }

        /// <summary>A pneumatic shove: a blast of air and dust thrown out along <paramref name="direction"/>.</summary>
        public static void SpawnShove(SceneTree tree, Vector3 position, Vector3 direction)
        {
            var p = P(tree);
            if (p == null) return;
            direction.Y = 0;
            var dir = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector3.Forward;
            for (int i = 0; i < 7; i++)
            {
                var v = InCone(dir, 0.6f);
                v.Y = Mathf.Abs(v.Y) * 0.4f;
                p.Emit(VfxParticles.Kind.Smoke, position + dir * 0.4f, v * R(3f, 6f), new Color(0.45f, 0.4f, 0.33f, 0.45f),
                    R(0.35f, 0.6f), R(0.5f, 0.8f), 2.2f, 0f, 3f);
            }
            for (int i = 0; i < 5; i++)
                p.Emit(VfxParticles.Kind.Spark, position + dir * 0.4f, InCone(dir, 0.3f) * R(8f, 14f), new Color(0.8f, 0.9f, 1f, 0.7f),
                    0.03f, R(0.1f, 0.18f), 0f, 0f, 3f);
        }

        /// <summary>Buff motes rising off a tower an Overclock Relay is boosting.</summary>
        public static void SpawnBuffMotes(SceneTree tree, Vector3 position, Color color)
        {
            var p = P(tree);
            if (p == null) return;
            for (int i = 0; i < 2; i++)
                p.Emit(VfxParticles.Kind.Glow, position + new Vector3(R(-0.5f, 0.5f), R(0.2f, 0.8f), R(-0.5f, 0.5f)),
                    new Vector3(0, R(0.8f, 1.4f), 0), color, R(0.1f, 0.16f), R(0.6f, 0.9f));
        }
    }

    /// <summary>What a projectile does when it lands.</summary>
    public enum ProjectileImpact { Sparks, Tar, None }

    /// <summary>
    /// Glowing projectile that flies from A to B, spawns hit flash on arrival.
    /// </summary>
    public partial class VineProjectile : Node3D
    {
        private Vector3 _target;
        private Vector3 _direction;
        private float _speed;
        private Color _color;
        private MeshInstance3D _mesh;
        private float _trailTimer;
        private ProjectileImpact _impact;

        public float Size { get; private set; } = 1f;
        /// <summary>Where the shot left from (it moves on from the first frame).</summary>
        public Vector3 Origin { get; private set; }

        public void Initialize(Vector3 from, Vector3 to, Color color, float speed, float size = 1f,
            ProjectileImpact impact = ProjectileImpact.Sparks)
        {
            Size = size;
            Origin = from;
            GlobalPosition = from;
            _target = to;
            _direction = (to - from).Normalized();
            _speed = speed;
            _color = color;
            _impact = impact;

            _mesh = new MeshInstance3D();
            _mesh.Mesh = VfxCache.Sphere(0.1f * size);
            _mesh.MaterialOverride = VfxCache.Glow(color.Lerp(Colors.White, 0.4f), color, 2f, alpha: false);
            // Stretched along its flight so it reads as a round in motion, not a ball
            if (_direction.LengthSquared() > 0.0001f)
            {
                var basis = Basis.LookingAt(_direction, Mathf.Abs(_direction.Y) > 0.95f ? Vector3.Right : Vector3.Up);
                _mesh.Basis = basis.Scaled(new Vector3(1f, 1f, 2.2f));
            }
            AddChild(_mesh);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            GlobalPosition += _direction * _speed * dt;

            // Trail: particles, not a node per dot
            _trailTimer += dt;
            if (_trailTimer >= 0.016f)
            {
                _trailTimer = 0;
                var p = VfxParticles.Get(GetTree());
                p?.Emit(VfxParticles.Kind.Glow, GlobalPosition, Vector3.Zero, _color, 0.16f * Size, 0.16f);
                p?.Emit(VfxParticles.Kind.Glow, GlobalPosition, Vector3.Zero, _color.Lerp(Colors.White, 0.5f), 0.3f * Size, 0.03f);
            }

            // Arrived?
            if (GlobalPosition.DistanceTo(_target) < 0.3f)
            {
                switch (_impact)
                {
                    case ProjectileImpact.Sparks: VfxFactory.SpawnImpact(GetTree(), _target, _color, Mathf.Clamp(Size, 0.6f, 1.6f)); break;
                    case ProjectileImpact.Tar: VfxFactory.SpawnTarSplat(GetTree(), _target); break;
                }
                QueueFree();
            }

            // Safety: kill if too far (missed)
            if (GlobalPosition.DistanceTo(_target) > 30f)
                QueueFree();
        }
    }

    /// <summary>
    /// Auto-fading, auto-scaling node that cleans itself up.
    /// Drives a MeshInstance3D from full to transparent over its lifetime.
    /// </summary>
    public partial class AutoFadeNode : Node3D
    {
        private MeshInstance3D _mesh;
        private float _lifetime;
        private float _maxLifetime;
        private float _expandRate;

        public AutoFadeNode(MeshInstance3D mesh, float lifetime, float expandRate = 1f)
        {
            _mesh = mesh;
            _lifetime = lifetime;
            _maxLifetime = lifetime;
            _expandRate = expandRate;
        }

        public override void _Ready()
        {
            AddChild(_mesh);
        }

        public override void _Process(double delta)
        {
            _lifetime -= (float)delta;
            float t = 1f - (_lifetime / _maxLifetime); // 0 -> 1

            // Expand
            float scale = 1f + t * _expandRate;
            _mesh.Scale = new Vector3(scale, scale, scale);

            // Fade per instance. Shared (VfxCache) materials must not be written to; materials a
            // caller built itself (e.g. IntroCinematic) keep the original alpha-overwrite fade.
            if (_mesh.MaterialOverride is StandardMaterial3D mat && !VfxCache.IsShared(mat))
            {
                var c = mat.AlbedoColor;
                mat.AlbedoColor = new Color(c.R, c.G, c.B, Mathf.Max(0, 1f - t));
            }
            else
            {
                _mesh.Transparency = Mathf.Clamp(t, 0f, 1f);
            }

            if (_lifetime <= 0)
                QueueFree();
        }
    }

    /// <summary>
    /// A scrap fragment that flies outward with gravity and fades.
    /// </summary>
    public partial class DeathFragment : Node3D
    {
        private MeshInstance3D _mesh;
        private Vector3 _velocity;
        private float _lifetime = 0.6f;
        private float _maxLifetime = 0.6f;
        private static readonly RandomNumberGenerator _rng = new();

        public void Initialize(Color tint)
        {
            _mesh = new MeshInstance3D();
            // Shared unit cube scaled per fragment (was a new BoxMesh + material per fragment)
            _mesh.Mesh = VfxCache.UnitBox;
            float size = _rng.RandfRange(0.05f, 0.15f);
            _fragmentScale = new Vector3(size, size, size * _rng.RandfRange(0.5f, 2f));
            _mesh.MaterialOverride = VfxCache.Fragment(tint.Lightened(_rng.RandfRange(-0.1f, 0.2f)));
            AddChild(_mesh);

            // Random outward velocity
            _velocity = new Vector3(
                _rng.RandfRange(-3f, 3f),
                _rng.RandfRange(2f, 5f),
                _rng.RandfRange(-3f, 3f)
            );

            // Random rotation
            _mesh.Rotation = new Vector3(
                _rng.RandfRange(0, Mathf.Tau),
                _rng.RandfRange(0, Mathf.Tau),
                _rng.RandfRange(0, Mathf.Tau)
            );
            _mesh.Scale = _fragmentScale;
        }

        private Vector3 _fragmentScale = Vector3.One;

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _lifetime -= dt;

            _velocity.Y -= 12f * dt; // Gravity
            GlobalPosition += _velocity * dt;

            // Spin
            _mesh.RotateX(5f * dt);
            _mesh.RotateZ(3f * dt);

            // Fade out (per instance — material is shared)
            float t = _lifetime / _maxLifetime;
            _mesh.Transparency = Mathf.Clamp(1f - t, 0f, 1f);

            // Kill when below ground or expired
            if (_lifetime <= 0 || GlobalPosition.Y < -1f)
                QueueFree();
        }
    }

    /// <summary>
    /// Shared meshes and materials for transient VFX.
    ///
    /// Every hit flash, muzzle flash, projectile trail dot and death fragment used to allocate its
    /// own Mesh + StandardMaterial3D. The node was freed after a fraction of a second, but the C#
    /// wrapper kept the native resource alive until the garbage collector finalized it — and with a
    /// small managed heap that rarely happens. A 45-turret autoplay run grew from 15k to 116k engine
    /// objects and ~4 GB of native memory by wave 14. Effects now share resources and fade per
    /// instance through GeometryInstance3D.Transparency.
    /// </summary>
    public static class VfxCache
    {
        private const int MaxMaterials = 512;
        private static readonly System.Collections.Generic.Dictionary<int, SphereMesh> _spheres = new();
        private static readonly System.Collections.Generic.Dictionary<(int, int, int, int), TorusMesh> _tori = new();
        private static readonly System.Collections.Generic.Dictionary<(int, int, int, int, int, bool), StandardMaterial3D> _glow = new();
        private static readonly System.Collections.Generic.Dictionary<int, StandardMaterial3D> _fragments = new();
        private static BoxMesh _unitBox;

        private static int Q(float v, float step = 0.01f) => Mathf.RoundToInt(v / step);
        private static int QColor(Color c) =>
            (Mathf.RoundToInt(Mathf.Clamp(c.R, 0f, 4f) * 63f) << 24) ^ (Mathf.RoundToInt(Mathf.Clamp(c.G, 0f, 4f) * 63f) << 16)
            ^ (Mathf.RoundToInt(Mathf.Clamp(c.B, 0f, 4f) * 63f) << 8) ^ Mathf.RoundToInt(Mathf.Clamp(c.A, 0f, 1f) * 63f);

        /// <summary>Sphere with height = 2 x radius (the only shape the effects use).</summary>
        public static SphereMesh Sphere(float radius)
        {
            int key = Q(radius, 0.005f);
            if (!_spheres.TryGetValue(key, out var mesh))
            {
                mesh = new SphereMesh { Radius = radius, Height = radius * 2f };
                _spheres[key] = mesh;
            }
            return mesh;
        }

        public static TorusMesh Torus(float inner, float outer, int rings, int segments)
        {
            var key = (Q(inner), Q(outer), rings, segments);
            if (!_tori.TryGetValue(key, out var mesh))
            {
                mesh = new TorusMesh { InnerRadius = inner, OuterRadius = outer, Rings = rings, RingSegments = segments };
                _tori[key] = mesh;
            }
            return mesh;
        }

        public static BoxMesh UnitBox => _unitBox ??= new BoxMesh { Size = Vector3.One };

        private static readonly System.Collections.Generic.HashSet<ulong> _sharedIds = new();

        /// <summary>True for materials owned by this cache (never mutate those per instance).</summary>
        public static bool IsShared(Material mat) => mat != null && _sharedIds.Contains(mat.GetInstanceId());

        private static T Track<T>(T mat) where T : Material { _sharedIds.Add(mat.GetInstanceId()); return mat; }

        /// <summary>
        /// Alpha-blended glow for effects faded by AutoFadeNode. The old fade overwrote the
        /// material's alpha with (1 - t) from the first frame, so authored alpha never showed —
        /// start opaque to keep that look; the fade itself is per-instance Transparency.
        /// </summary>
        public static StandardMaterial3D FadeGlow(Color albedo, Color emission, float energy)
            => Glow(new Color(albedo.R, albedo.G, albedo.B, 1f), emission, energy, alpha: true);

        /// <summary>Unshaded emissive material, optionally alpha-blended.</summary>
        public static StandardMaterial3D Glow(Color albedo, Color emission, float energy, bool alpha)
        {
            var key = (QColor(albedo), QColor(emission), Q(energy, 0.05f), 0, 0, alpha);
            if (_glow.TryGetValue(key, out var mat)) return mat;
            if (_glow.Count >= MaxMaterials) _glow.Clear(); // live users keep their reference

            mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = albedo,
                EmissionEnabled = true,
                Emission = emission,
                EmissionEnergyMultiplier = energy,
            };
            if (alpha) mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _glow[key] = mat;
            return Track(mat);
        }

        /// <summary>Lit, alpha-capable material for death fragments.</summary>
        public static StandardMaterial3D Fragment(Color albedo)
        {
            int key = QColor(albedo);
            if (_fragments.TryGetValue(key, out var mat)) return mat;
            if (_fragments.Count >= MaxMaterials) _fragments.Clear();
            mat = new StandardMaterial3D
            {
                AlbedoColor = albedo,
                Roughness = 0.9f,
                Metallic = 0.5f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            _fragments[key] = mat;
            return Track(mat);
        }

        /// <summary>Drop all cached resources (shutdown / tests).</summary>
        public static void Clear()
        {
            _spheres.Clear(); _tori.Clear(); _glow.Clear(); _fragments.Clear(); _sharedIds.Clear();
            _unitBox = null;
        }
    }
}
