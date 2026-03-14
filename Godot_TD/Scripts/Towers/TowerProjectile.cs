using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Simple homing projectile fired by towers.
    /// </summary>
    public partial class TowerProjectile : Node3D
    {
        private Node3D _source;
        private Node3D _target;
        private float _damage;
        private DamageType _damageType;
        private float _splashRadius;
        private float _speed = 20f;
        private float _lifetime = 3f;
        private MeshInstance3D _mesh;

        // Trail
        private float _trailTimer;
        private const float TRAIL_INTERVAL = 0.03f;

        public void Initialize(Node3D source, Node3D target, float damage, DamageType damageType, float splash)
        {
            _source = source;
            _target = target;
            _damage = damage;
            _damageType = damageType;
            _splashRadius = splash;

            // Visual
            _mesh = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.1f;
            sphere.Height = 0.2f;
            _mesh.Mesh = sphere;

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.AlbedoColor = damageType switch
            {
                DamageType.Fire => new Color(1f, 0.5f, 0.1f),
                DamageType.Ice => new Color(0.3f, 0.7f, 1f),
                DamageType.Lightning => new Color(0.6f, 0.6f, 1f),
                _ => new Color(1f, 0.9f, 0.6f)
            };
            mat.Emission = mat.AlbedoColor;
            mat.EmissionEnabled = true;
            _mesh.MaterialOverride = mat;
            AddChild(_mesh);

            AddToGroup(Constants.GROUP_PROJECTILE);
        }

        public override void _PhysicsProcess(double delta)
        {
            _lifetime -= (float)delta;
            if (_lifetime <= 0 || !IsInstanceValid(_target))
            {
                QueueFree();
                return;
            }

            var dir = (_target.GlobalPosition - GlobalPosition).Normalized();
            GlobalPosition += dir * _speed * (float)delta;

            // Trail particles
            _trailTimer -= (float)delta;
            if (_trailTimer <= 0)
            {
                _trailTimer = TRAIL_INTERVAL;
                SpawnTrailDot();
            }

            if (GlobalPosition.DistanceTo(_target.GlobalPosition) < 0.3f)
            {
                Hit();
            }
        }

        private void Hit()
        {
            if (_splashRadius > 0)
            {
                // VFX: splash ring
                VfxFactory.SpawnSplashRing(GetTree(), GlobalPosition, _splashRadius, _damageType);

                // Screen shake for AoE
                if (ServiceLocator.TryGet<TDCamera>(out var cam))
                    cam.Shake(0.15f, 0.15f);

                // AoE damage
                foreach (var node in GetTree().GetNodesInGroup(Constants.GROUP_ENEMY))
                {
                    if (node is not Node3D enemy) continue;
                    if (enemy.GlobalPosition.DistanceTo(GlobalPosition) <= _splashRadius * Constants.CELL_SIZE)
                        DealDamage(enemy);
                }
            }
            else if (IsInstanceValid(_target))
            {
                DealDamage(_target);
            }
            QueueFree();
        }

        private void SpawnTrailDot()
        {
            var dot = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.04f;
            sphere.Height = 0.08f;
            dot.Mesh = sphere;
            dot.GlobalPosition = GlobalPosition;

            var mat = (StandardMaterial3D)_mesh.MaterialOverride;
            var trailMat = new StandardMaterial3D();
            trailMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            trailMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            trailMat.AlbedoColor = new Color(mat.AlbedoColor.R, mat.AlbedoColor.G, mat.AlbedoColor.B, 0.6f);
            trailMat.Emission = mat.Emission;
            trailMat.EmissionEnabled = true;
            trailMat.EmissionEnergyMultiplier = 1.5f;
            dot.MaterialOverride = trailMat;

            var node = new AutoFadeNode(dot, 0.2f, 0.5f);
            GetTree().CurrentScene.AddChild(node);
        }

        private void DealDamage(Node3D target)
        {
            if (target is EnemyController enemy)
            {
                var info = new DamageInfo
                {
                    RawDamage = _damage,
                    FinalDamage = _damage,
                    DamageType = _damageType,
                    Attacker = _source,
                    Target = target,
                    HitPoint = GlobalPosition
                };
                enemy.TakeDamage(info);
            }
        }
    }
}
