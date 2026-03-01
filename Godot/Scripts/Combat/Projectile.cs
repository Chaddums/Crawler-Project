using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Visible traveling projectile with emissive sphere + trailing particles.
    /// Moves in a direction, deals damage on first contact, self-destructs at max range or timeout.
    /// </summary>
    public partial class Projectile : Area3D
    {
        private Vector3 _direction;
        private float _speed;
        private float _maxRange;
        private DamageInfo _damage;
        private Team _team;

        private float _distanceTraveled;
        private float _timeout = 3f;
        private float _elapsed;
        private bool _hit;

        private MeshInstance3D _meshVisual;
        private GpuParticles3D _trail;

        /// <summary>
        /// Set up projectile parameters. Call immediately after instantiation.
        /// </summary>
        public void Initialize(Vector3 direction, float speed, float range,
            DamageInfo damage, Team team, DamageType damageType = DamageType.Physical)
        {
            _direction = direction.Normalized();
            _speed = speed;
            _maxRange = range;
            _damage = damage;
            _team = team;

            // Visual: emissive sphere
            _meshVisual = new MeshInstance3D();
            var sphere = new SphereMesh { Radius = 0.15f, Height = 0.3f, RadialSegments = 8, Rings = 4 };
            _meshVisual.Mesh = sphere;

            Color projColor = GetDamageTypeColor(damageType);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = projColor;
            mat.EmissionEnabled = true;
            mat.Emission = projColor;
            mat.EmissionEnergyMultiplier = 3f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _meshVisual.MaterialOverride = mat;
            AddChild(_meshVisual);

            // Trailing particles
            _trail = CreateTrailParticles(projColor);
            AddChild(_trail);

            // Collision setup
            var collisionShape = new CollisionShape3D();
            collisionShape.Shape = new SphereShape3D { Radius = 0.2f };
            AddChild(collisionShape);

            // Set collision layers
            CollisionLayer = _team == Team.Player
                ? 1u << (Constants.LAYER_PLAYER_PROJECTILE - 1)
                : 1u << (Constants.LAYER_ENEMY_PROJECTILE - 1);

            CollisionMask = _team == Team.Player
                ? Constants.MASK_ENEMY
                : Constants.MASK_PLAYER;

            Monitoring = true;
            Monitorable = false;

            BodyEntered += OnBodyEntered;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_hit) return;

            float dt = (float)delta;
            _elapsed += dt;

            // Move forward
            float step = _speed * dt;
            GlobalPosition += _direction * step;
            _distanceTraveled += step;

            // Self-destruct conditions
            if (_distanceTraveled >= _maxRange || _elapsed >= _timeout)
            {
                Destroy();
            }
        }

        private void OnBodyEntered(Node3D body)
        {
            if (_hit) return;

            // Find damageable component
            IDamageable damageable = null;
            if (body is IDamageable d)
                damageable = d;
            else
                damageable = body.GetNodeOrNull<HealthComponent>("HealthComponent");

            if (damageable != null && damageable.IsAlive)
            {
                _hit = true;
                _damage.HitPoint = GlobalPosition;
                _damage.Target = body;
                damageable.TakeDamage(_damage);

                // Impact burst
                var impactPos = GlobalPosition;
                var impact = VfxFactory.CreateImpactBurst(
                    GetDamageTypeColor(_damage.DamageType));
                GetTree().Root.AddChild(impact);
                impact.GlobalPosition = impactPos;

                Destroy();
            }
        }

        private void Destroy()
        {
            _trail.Emitting = false;
            _meshVisual.Visible = false;

            // Wait for trail particles to finish, then free
            GetTree().CreateTimer(0.5f).Timeout += QueueFree;
        }

        private static GpuParticles3D CreateTrailParticles(Color color)
        {
            var particles = new GpuParticles3D();
            particles.Amount = 12;
            particles.Lifetime = 0.3;
            particles.SpeedScale = 1f;
            particles.Explosiveness = 0f;

            var drawMesh = new SphereMesh
            {
                Radius = 0.04f, Height = 0.08f, RadialSegments = 4, Rings = 2
            };
            var drawMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = Colors.White,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
            };
            drawMesh.Material = drawMat;
            particles.DrawPass1 = drawMesh;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0, 1);
            mat.Spread = 30f;
            mat.InitialVelocityMin = 0.2f;
            mat.InitialVelocityMax = 0.5f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.5f;
            mat.ScaleMax = 1.5f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0.8f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;
            mat.Color = color;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            return particles;
        }

        public static Color GetDamageTypeColor(DamageType type) => type switch
        {
            DamageType.Physical => new Color(0.9f, 0.9f, 0.95f),
            DamageType.Fire => new Color(1f, 0.4f, 0.1f),
            DamageType.Ice => new Color(0.3f, 0.7f, 1f),
            DamageType.Lightning => new Color(1f, 1f, 0.3f),
            DamageType.Poison => new Color(0.3f, 0.9f, 0.2f),
            DamageType.Dark => new Color(0.6f, 0.2f, 0.8f),
            DamageType.Holy => new Color(1f, 0.95f, 0.6f),
            _ => Colors.White
        };
    }
}
