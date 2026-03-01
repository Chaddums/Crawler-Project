using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Subscribes to OnDamageDealt and spawns hit particles, screen shake, and crit visuals.
    /// </summary>
    public partial class CombatVfxManager : Node
    {
        public override void _Ready()
        {
            GameEvents.OnDamageDealt += OnDamageDealt;
        }

        private void OnDamageDealt(DamageInfo damage)
        {
            if (damage.FinalDamage <= 0) return;

            // Hit particles
            Color hitColor = GetDamageTypeColor(damage.DamageType);
            var hitParticles = VfxFactory.CreateHitParticles(hitColor);
            GetTree().Root.AddChild(hitParticles);
            hitParticles.GlobalPosition = damage.HitPoint;

            // Screen shake
            float trauma = damage.IsCritical ? 0.4f : 0.15f;
            if (ServiceLocator.TryGet<IsometricCamera>(out var camera))
                camera.Shake(trauma);

            // Crit ring burst
            if (damage.IsCritical)
                SpawnCritRing(damage.HitPoint);
        }

        private void SpawnCritRing(Vector3 position)
        {
            var ring = new MeshInstance3D();
            var mesh = new SphereMesh();
            mesh.Radius = 0.3f;
            mesh.Height = 0.6f;
            mesh.RadialSegments = 12;
            mesh.Rings = 6;
            ring.Mesh = mesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(1f, 0.85f, 0.2f, 0.6f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(1f, 0.85f, 0.2f);
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            ring.MaterialOverride = mat;

            GetTree().Root.AddChild(ring);
            ring.GlobalPosition = position;

            // Expand and fade
            var tween = ring.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(ring, "scale", new Vector3(3, 3, 3), 0.25f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(mat, "albedo_color:a", 0f, 0.25f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(ring.QueueFree));
        }

        private static Color GetDamageTypeColor(DamageType type) => type switch
        {
            DamageType.Fire => new Color(1f, 0.4f, 0.1f),
            DamageType.Ice => new Color(0.3f, 0.7f, 1f),
            DamageType.Lightning => new Color(0.8f, 0.8f, 1f),
            DamageType.Poison => new Color(0.3f, 0.9f, 0.3f),
            DamageType.Dark => new Color(0.5f, 0.2f, 0.7f),
            DamageType.Holy => new Color(1f, 1f, 0.6f),
            _ => new Color(1f, 0.9f, 0.8f) // Physical: warm white
        };

        public override void _ExitTree()
        {
            GameEvents.OnDamageDealt -= OnDamageDealt;
        }
    }
}
