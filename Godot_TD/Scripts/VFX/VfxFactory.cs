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
        /// Scrap fragments fly outward when an enemy dies.
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
            var sphere = new SphereMesh();
            sphere.Radius = 0.4f;
            sphere.Height = 0.8f;
            flash.Mesh = sphere;
            flash.GlobalPosition = position;

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = new Color(1f, 0.8f, 0.3f, 0.9f);
            mat.Emission = new Color(1f, 0.6f, 0.2f);
            mat.EmissionEnabled = true;
            mat.EmissionEnergyMultiplier = 3f;
            flash.MaterialOverride = mat;

            var flashNode = new AutoFadeNode(flash, 0.25f, 1.5f);
            root.AddChild(flashNode);
        }

        /// <summary>
        /// Brief white flash on an enemy when hit.
        /// </summary>
        public static void SpawnHitFlash(SceneTree tree, Vector3 position, DamageType damageType)
        {
            var flash = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.15f;
            sphere.Height = 0.3f;
            flash.Mesh = sphere;
            flash.GlobalPosition = position + new Vector3(0, 0.3f, 0);

            var color = damageType switch
            {
                DamageType.Fire => new Color(1f, 0.5f, 0.1f, 0.9f),
                DamageType.Ice => new Color(0.3f, 0.8f, 1f, 0.9f),
                DamageType.Lightning => new Color(0.7f, 0.7f, 1f, 0.9f),
                DamageType.Poison => new Color(0.3f, 0.9f, 0.2f, 0.9f),
                _ => new Color(1f, 1f, 1f, 0.9f)
            };

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = color;
            mat.Emission = new Color(color.R, color.G, color.B);
            mat.EmissionEnabled = true;
            mat.EmissionEnergyMultiplier = 2f;
            flash.MaterialOverride = mat;

            var node = new AutoFadeNode(flash, 0.15f, 2f);
            tree.CurrentScene.AddChild(node);
        }

        /// <summary>
        /// Brief barrel flash when a tower fires.
        /// </summary>
        public static void SpawnMuzzleFlash(SceneTree tree, Vector3 position, DamageType damageType)
        {
            var flash = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.12f;
            sphere.Height = 0.24f;
            flash.Mesh = sphere;
            flash.GlobalPosition = position;

            var color = damageType switch
            {
                DamageType.Fire => new Color(1f, 0.6f, 0.1f),
                DamageType.Ice => new Color(0.5f, 0.8f, 1f),
                DamageType.Lightning => new Color(0.8f, 0.8f, 1f),
                _ => new Color(1f, 0.9f, 0.5f)
            };

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 1f);
            mat.Emission = color;
            mat.EmissionEnabled = true;
            mat.EmissionEnergyMultiplier = 4f;
            flash.MaterialOverride = mat;

            var node = new AutoFadeNode(flash, 0.1f, 3f);
            tree.CurrentScene.AddChild(node);
        }

        /// <summary>
        /// Expanding ring for AoE/splash damage.
        /// </summary>
        public static void SpawnSplashRing(SceneTree tree, Vector3 position, float radius, DamageType damageType)
        {
            var ring = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = radius * Constants.CELL_SIZE * 0.9f;
            torus.OuterRadius = radius * Constants.CELL_SIZE;
            torus.Rings = 16;
            torus.RingSegments = 24;
            ring.Mesh = torus;
            ring.GlobalPosition = position + new Vector3(0, 0.1f, 0);

            var color = damageType switch
            {
                DamageType.Fire => new Color(1f, 0.4f, 0.05f, 0.7f),
                DamageType.Ice => new Color(0.3f, 0.7f, 1f, 0.7f),
                DamageType.Lightning => new Color(0.5f, 0.5f, 1f, 0.7f),
                _ => new Color(1f, 0.8f, 0.3f, 0.7f)
            };

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = color;
            mat.Emission = new Color(color.R, color.G, color.B);
            mat.EmissionEnabled = true;
            mat.EmissionEnergyMultiplier = 2f;
            ring.MaterialOverride = mat;

            var node = new AutoFadeNode(ring, 0.4f, 1.2f);
            tree.CurrentScene.AddChild(node);
        }

        /// <summary>
        /// Pop effect when scrap is collected.
        /// </summary>
        public static void SpawnScrapCollectPop(SceneTree tree, Vector3 position)
        {
            var flash = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.2f;
            sphere.Height = 0.4f;
            flash.Mesh = sphere;
            flash.GlobalPosition = position;

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = new Color(1f, 0.85f, 0.2f, 0.8f);
            mat.Emission = new Color(0.8f, 0.6f, 0.1f);
            mat.EmissionEnabled = true;
            mat.EmissionEnergyMultiplier = 2f;
            flash.MaterialOverride = mat;

            var node = new AutoFadeNode(flash, 0.3f, 1.8f);
            tree.CurrentScene.AddChild(node);
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

            // Fade
            if (_mesh.MaterialOverride is StandardMaterial3D mat)
            {
                var c = mat.AlbedoColor;
                mat.AlbedoColor = new Color(c.R, c.G, c.B, Mathf.Max(0, 1f - t));
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
            var box = new BoxMesh();
            float size = _rng.RandfRange(0.05f, 0.15f);
            box.Size = new Vector3(size, size, size * _rng.RandfRange(0.5f, 2f));
            _mesh.Mesh = box;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = tint.Lightened(_rng.RandfRange(-0.1f, 0.2f));
            mat.Roughness = 0.9f;
            mat.Metallic = 0.5f;
            _mesh.MaterialOverride = mat;
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
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _lifetime -= dt;

            _velocity.Y -= 12f * dt; // Gravity
            GlobalPosition += _velocity * dt;

            // Spin
            _mesh.RotateX(5f * dt);
            _mesh.RotateZ(3f * dt);

            // Fade out
            float t = _lifetime / _maxLifetime;
            if (_mesh.MaterialOverride is StandardMaterial3D mat)
            {
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                var c = mat.AlbedoColor;
                mat.AlbedoColor = new Color(c.R, c.G, c.B, t);
            }

            // Kill when below ground or expired
            if (_lifetime <= 0 || GlobalPosition.Y < -1f)
                QueueFree();
        }
    }
}
