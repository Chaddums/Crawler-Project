using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Creates visual effects: death bursts, hit flashes, muzzle flashes,
    /// splash rings, scrap collect pops. All self-cleaning (QueueFree after lifetime).
    /// </summary>
    public static class VfxFactory
    {
        /// <summary>
        /// Resource fragments fly outward when an enemy dies.
        /// </summary>
        public static void SpawnDeathBurst(SceneTree tree, Vector3 position, Color tint, int fragmentCount = 6)
        {
            var root = tree.CurrentScene;

            for (int i = 0; i < fragmentCount; i++)
            {
                var frag = new DeathFragment();
                root.AddChild(frag);
                frag.GlobalPosition = position;
                frag.Initialize(tint);
            }

            // Central flash
            var flash = new MeshInstance3D();
            flash.Mesh = VfxCache.Sphere(0.4f);

            flash.MaterialOverride = VfxCache.FadeGlow(new Color(1f, 0.8f, 0.3f, 0.9f), new Color(1f, 0.6f, 0.2f), 3f);

            var flashNode = new AutoFadeNode(flash, 0.25f, 1.5f);
            root.AddChild(flashNode);
            flashNode.GlobalPosition = position;
        }

        /// <summary>
        /// Massive death explosion for bosses — more fragments, bigger flash, shockwave ring.
        /// </summary>
        public static void SpawnBossDeathBurst(SceneTree tree, Vector3 position, Color tint)
        {
            var root = tree.CurrentScene;

            // 16 fragments (vs 6 for normal enemies)
            for (int i = 0; i < 16; i++)
            {
                var frag = new DeathFragment();
                root.AddChild(frag);
                frag.GlobalPosition = position;
                frag.Initialize(tint);
            }

            // Large central flash
            var flash = new MeshInstance3D();
            flash.Mesh = VfxCache.Sphere(1.2f);

            flash.MaterialOverride = VfxCache.FadeGlow(new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.7f, 0.2f), 6f);

            var flashNode = new AutoFadeNode(flash, 0.5f, 3f);
            root.AddChild(flashNode);
            flashNode.GlobalPosition = position;

            // Expanding shockwave ring
            var ring = new MeshInstance3D();
            ring.Mesh = VfxCache.Torus(0.8f, 1.2f, 16, 32);

            ring.MaterialOverride = VfxCache.FadeGlow(new Color(1f, 0.8f, 0.3f, 0.8f), new Color(1f, 0.6f, 0.1f), 4f);

            var ringNode = new AutoFadeNode(ring, 0.8f, 6f);
            root.AddChild(ringNode);
            ringNode.GlobalPosition = position + new Vector3(0, 0.2f, 0);
        }

        /// <summary>
        /// Wave completion celebration — upward burst of light particles.
        /// </summary>
        public static void SpawnWaveCompleteBurst(SceneTree tree, Vector3 position)
        {
            var root = tree.CurrentScene;
            var rng = new RandomNumberGenerator();

            for (int i = 0; i < 12; i++)
            {
                var particle = new MeshInstance3D();
                particle.Mesh = VfxCache.Sphere(0.08f);
                var offset = new Vector3(
                    rng.RandfRange(-2f, 2f), 0, rng.RandfRange(-2f, 2f));

                particle.MaterialOverride = VfxCache.FadeGlow(new Color(0.9f, 0.93f, 1f, 0.9f), new Color(0.9f, 0.93f, 1f), 2f);

                var node = new AutoFadeNode(particle, 1.2f, 0.5f);
                root.AddChild(node);
                node.GlobalPosition = position + offset;
            }
        }

        /// <summary>
        /// Brief white flash on an enemy when hit.
        /// </summary>
        public static void SpawnHitFlash(SceneTree tree, Vector3 position, DamageType damageType)
        {
            var flash = new MeshInstance3D();
            flash.Mesh = VfxCache.Sphere(0.15f);

            var color = damageType switch
            {
                DamageType.Fire => new Color(1f, 0.5f, 0.1f, 0.9f),
                DamageType.Ice => new Color(0.3f, 0.8f, 1f, 0.9f),
                DamageType.Lightning => new Color(0.7f, 0.7f, 1f, 0.9f),
                DamageType.Poison => new Color(0.3f, 0.9f, 0.2f, 0.9f),
                _ => new Color(1f, 1f, 1f, 0.9f)
            };

            flash.MaterialOverride = VfxCache.FadeGlow(color, new Color(color.R, color.G, color.B), 2f);

            var node = new AutoFadeNode(flash, 0.15f, 2f);
            tree.CurrentScene.AddChild(node);
            node.GlobalPosition = position + new Vector3(0, 0.3f, 0);
        }

        /// <summary>
        /// Brief barrel flash when a tower fires.
        /// </summary>
        public static void SpawnMuzzleFlash(SceneTree tree, Vector3 position, DamageType damageType)
        {
            var flash = new MeshInstance3D();
            flash.Mesh = VfxCache.Sphere(0.12f);

            var color = damageType switch
            {
                DamageType.Fire => new Color(1f, 0.6f, 0.1f),
                DamageType.Ice => new Color(0.5f, 0.8f, 1f),
                DamageType.Lightning => new Color(0.8f, 0.8f, 1f),
                _ => new Color(1f, 0.9f, 0.5f)
            };

            flash.MaterialOverride = VfxCache.FadeGlow(new Color(color.R, color.G, color.B, 1f), color, 4f);

            var node = new AutoFadeNode(flash, 0.1f, 3f);
            tree.CurrentScene.AddChild(node);
            node.GlobalPosition = position;
        }

        /// <summary>
        /// Expanding ring for AoE/splash damage.
        /// </summary>
        public static void SpawnSplashRing(SceneTree tree, Vector3 position, float radius, DamageType damageType)
        {
            var ring = new MeshInstance3D();
            ring.Mesh = VfxCache.Torus(radius * Constants.CELL_SIZE * 0.9f, radius * Constants.CELL_SIZE, 16, 24);

            var color = damageType switch
            {
                DamageType.Fire => new Color(1f, 0.4f, 0.05f, 0.7f),
                DamageType.Ice => new Color(0.3f, 0.7f, 1f, 0.7f),
                DamageType.Lightning => new Color(0.5f, 0.5f, 1f, 0.7f),
                _ => new Color(1f, 0.8f, 0.3f, 0.7f)
            };

            ring.MaterialOverride = VfxCache.FadeGlow(color, new Color(color.R, color.G, color.B), 2f);

            var node = new AutoFadeNode(ring, 0.4f, 1.2f);
            tree.CurrentScene.AddChild(node);
            node.GlobalPosition = position + new Vector3(0, 0.1f, 0);
        }

        /// <summary>
        /// Pop effect when scrap is collected.
        /// </summary>
        public static void SpawnScrapCollectPop(SceneTree tree, Vector3 position)
        {
            var flash = new MeshInstance3D();
            flash.Mesh = VfxCache.Sphere(0.2f);

            flash.MaterialOverride = VfxCache.FadeGlow(new Color(1f, 0.85f, 0.2f, 0.8f), new Color(0.8f, 0.6f, 0.1f), 2f);

            var node = new AutoFadeNode(flash, 0.3f, 1.8f);
            tree.CurrentScene.AddChild(node);
            node.GlobalPosition = position;
        }

        /// <summary>
        /// Glowing projectile that flies from origin to target, then spawns a hit flash.
        /// </summary>
        public static void SpawnProjectile(SceneTree tree, Vector3 from, Vector3 to,
            Color color, float speed = 18f, float size = 1f)
        {
            var proj = new VineProjectile();
            tree.CurrentScene.AddChild(proj);
            proj.Initialize(from, to, color, speed, size);
        }

        /// <summary>
        /// Expanding ring pulse for active area effects (slow field, sensor range).
        /// </summary>
        public static void SpawnAreaPulse(SceneTree tree, Vector3 position, float radius,
            Color color, float lifetime = 0.6f)
        {
            var ring = new MeshInstance3D();
            ring.Mesh = VfxCache.Torus(radius * 0.85f, radius, 16, 24);

            ring.MaterialOverride = VfxCache.FadeGlow(new Color(color.R, color.G, color.B, 0.5f), color, 1.5f);

            var node = new AutoFadeNode(ring, lifetime, 0.3f);
            tree.CurrentScene.AddChild(node);
            node.GlobalPosition = position + new Vector3(0, 0.15f, 0);
        }

        /// <summary>
        /// Corruption event expanding ring — large dramatic pulse when AXIS activates a corruption.
        /// </summary>
        public static void SpawnCorruptionPulse(SceneTree tree, Vector3 position, Color color)
        {
            var ring = new MeshInstance3D();
            ring.Mesh = VfxCache.Torus(0.5f, 1.0f, 16, 32);

            ring.MaterialOverride = VfxCache.FadeGlow(new Color(color.R, color.G, color.B, 0.7f), color, 4f);

            var node = new AutoFadeNode(ring, 1.0f, 8f);
            tree.CurrentScene.AddChild(node);
            node.GlobalPosition = position + new Vector3(0, 0.3f, 0);
        }

        /// <summary>
        /// Signal activation burst — plays when a node receives and acts on a signal.
        /// </summary>
        public static void SpawnSignalBurst(SceneTree tree, Vector3 position, Color color)
        {
            var flash = new MeshInstance3D();
            flash.Mesh = VfxCache.Sphere(0.25f);

            flash.MaterialOverride = VfxCache.FadeGlow(new Color(color.R, color.G, color.B, 0.8f), color, 3f);

            var node = new AutoFadeNode(flash, 0.2f, 2.5f);
            tree.CurrentScene.AddChild(node);
            node.GlobalPosition = position + new Vector3(0, 0.5f, 0);
        }
    }

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

        public float Size { get; private set; } = 1f;
        /// <summary>Where the shot left from (it moves on from the first frame).</summary>
        public Vector3 Origin { get; private set; }

        public void Initialize(Vector3 from, Vector3 to, Color color, float speed, float size = 1f)
        {
            Size = size;
            Origin = from;
            GlobalPosition = from;
            _target = to;
            _direction = (to - from).Normalized();
            _speed = speed;
            _color = color;

            _mesh = new MeshInstance3D();
            _mesh.Mesh = VfxCache.Sphere(0.1f * size);

            _mesh.MaterialOverride = VfxCache.Glow(color, color, 1.5f, alpha: false);
            AddChild(_mesh);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            GlobalPosition += _direction * _speed * dt;

            // Trail dots
            _trailTimer += dt;
            if (_trailTimer >= 0.03f)
            {
                _trailTimer = 0;
                SpawnTrailDot();
            }

            // Arrived?
            if (GlobalPosition.DistanceTo(_target) < 0.3f)
            {
                VfxFactory.SpawnHitFlash(GetTree(), _target, DamageType.Physical);
                QueueFree();
            }

            // Safety: kill if too far (missed)
            if (GlobalPosition.DistanceTo(_target) > 30f)
                QueueFree();
        }

        private void SpawnTrailDot()
        {
            var dot = new MeshInstance3D();
            dot.Mesh = VfxCache.Sphere(0.04f * Size);

            dot.MaterialOverride = VfxCache.FadeGlow(new Color(_color.R, _color.G, _color.B, 0.6f), _color, 2f);

            var pos = GlobalPosition;
            var fade = new AutoFadeNode(dot, 0.2f, 0.5f);
            GetTree().CurrentScene.AddChild(fade);
            fade.GlobalPosition = pos;
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
