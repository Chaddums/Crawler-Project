using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Visible traveling projectile with emissive sphere + trailing particles.
    /// Moves in a direction, deals damage on first contact, self-destructs at max range or timeout.
    /// Supports Ricochet Rounds perk: bounces to nearby enemy at 60% damage.
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
        private Vector3 _prevPosition;
        private bool _isBounce; // true if this is a ricochet bounce (no further bouncing)
        private Node3D _ignoreTarget; // skip this target (already hit by parent)

        // AoE explosion support (used by Launcher weapon type)
        private float _aoeRadius;
        private float _aoeSplashMult = 0.5f;

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

            // Collision setup — larger radius to prevent tunneling
            var collisionShape = new CollisionShape3D();
            collisionShape.Shape = new SphereShape3D { Radius = 0.5f };
            AddChild(collisionShape);

            // Set collision layers
            CollisionLayer = _team == Team.Player
                ? 1u << (Constants.LAYER_PLAYER_PROJECTILE - 1)
                : 1u << (Constants.LAYER_ENEMY_PROJECTILE - 1);

            // Include wall layer (DEFAULT=1) so projectiles collide with walls
            uint wallMask = 1u << (Constants.LAYER_DEFAULT - 1);
            CollisionMask = (_team == Team.Player
                ? Constants.MASK_ENEMY
                : Constants.MASK_PLAYER) | wallMask;

            Monitoring = true;
            Monitorable = false;

            BodyEntered += OnBodyEntered;

            _prevPosition = GlobalPosition;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_hit) return;

            float dt = (float)delta;
            _elapsed += dt;

            _prevPosition = GlobalPosition;

            // Move forward
            float step = _speed * dt;
            GlobalPosition += _direction * step;
            _distanceTraveled += step;

            // Sweep raycast from previous to current position to catch tunneling
            var spaceState = GetWorld3D()?.DirectSpaceState;
            if (spaceState != null)
            {
                var query = PhysicsRayQueryParameters3D.Create(_prevPosition, GlobalPosition, CollisionMask);
                var result = spaceState.IntersectRay(query);
                if (result.Count > 0)
                {
                    var body = result["collider"].As<Node3D>();
                    if (body != null)
                        OnBodyEntered(body);
                }
            }

            // Self-destruct conditions
            if (_distanceTraveled >= _maxRange || _elapsed >= _timeout)
            {
                Destroy();
            }
        }

        private void OnBodyEntered(Node3D body)
        {
            if (_hit) return;
            if (_ignoreTarget != null && body == _ignoreTarget) return;

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

                if (!_isBounce && _team == Team.Player)
                {
                    // Ricochet Rounds: bounce to nearby enemy at 60% damage
                    TryRicochet(body, impactPos);
                    // Chain Lightning: arc to nearby enemy at 50% damage
                    TryChainLightning(body, impactPos);
                }

                // AoE explosion (Launcher weapon type)
                ExplodeAoE(impactPos, body);

                Destroy();
            }
            else if (body is StaticBody3D)
            {
                // Hit a wall or obstacle — destroy projectile
                _hit = true;
                var impact = VfxFactory.CreateImpactBurst(
                    GetDamageTypeColor(_damage.DamageType));
                GetTree().Root.AddChild(impact);
                impact.GlobalPosition = GlobalPosition;

                // AoE explosion even on wall hit (Launcher weapon type)
                ExplodeAoE(GlobalPosition, null);

                Destroy();
            }
        }

        /// <summary>
        /// Mark this projectile as a ricochet bounce that skips a specific target.
        /// </summary>
        public void SetBounce(Node3D ignoreTarget)
        {
            _isBounce = true;
            _ignoreTarget = ignoreTarget;
        }

        /// <summary>
        /// Enable AoE explosion on impact. Enemies within radius take splash damage.
        /// </summary>
        public void SetAoE(float radius, float splashDamageMult = 0.5f)
        {
            _aoeRadius = radius;
            _aoeSplashMult = splashDamageMult;
        }

        private void ExplodeAoE(Vector3 hitPos, Node3D directHit)
        {
            if (_aoeRadius <= 0f) return;

            var spaceState = GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = _aoeRadius };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, hitPos),
                CollisionMask = _team == Team.Player ? Constants.MASK_ENEMY : Constants.MASK_PLAYER
            };
            var results = spaceState.IntersectShape(queryParams);

            foreach (var result in results)
            {
                var collider = result["collider"].As<Node3D>();
                if (collider == null || collider == directHit) continue;

                IDamageable splash = null;
                if (collider is IDamageable sd) splash = sd;
                else splash = collider.GetNodeOrNull<HealthComponent>("HealthComponent");

                if (splash == null || !splash.IsAlive) continue;

                var splashDmg = _damage;
                splashDmg.FinalDamage *= _aoeSplashMult;
                splashDmg.RawDamage *= _aoeSplashMult;
                splashDmg.Target = collider;
                splashDmg.HitPoint = collider.GlobalPosition;
                splash.TakeDamage(splashDmg);
            }

            // AoE ring VFX
            var aoeRing = VfxFactory.CreateAoEIndicator(
                GetDamageTypeColor(_damage.DamageType), _aoeRadius);
            GetTree().Root.AddChild(aoeRing);
            aoeRing.GlobalPosition = hitPos;
        }

        private void TryRicochet(Node3D hitTarget, Vector3 hitPos)
        {
            // Check if player has Ricochet Rounds perk
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;
            if (player.PerkProcessor == null || !player.PerkProcessor.HasRicochetRounds()) return;

            // Find nearest enemy within 8m that isn't the one we just hit
            var spaceState = GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 8f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, hitPos),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            float bestDist = float.MaxValue;
            Node3D bestTarget = null;
            foreach (var result in results)
            {
                var collider = result["collider"].As<Node3D>();
                if (collider == null || collider == hitTarget) continue;
                float dist = hitPos.DistanceTo(collider.GlobalPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = collider;
                }
            }

            if (bestTarget == null) return;

            // Spawn bounce projectile at 60% damage
            var bounceDir = (bestTarget.GlobalPosition - hitPos).Normalized();
            var bounceDamage = _damage;
            bounceDamage.FinalDamage *= 0.6f;
            bounceDamage.RawDamage *= 0.6f;

            var proj = new Projectile();
            GetTree().Root.AddChild(proj);
            proj.GlobalPosition = hitPos;
            proj.Initialize(bounceDir, _speed, 10f, bounceDamage, _team, _damage.DamageType);
            proj.SetBounce(hitTarget);
        }

        /// <summary>
        /// Chain Lightning perk: arc from hit target to 1 nearby enemy at 50% damage.
        /// Called when a player projectile ability hits.
        /// </summary>
        private void TryChainLightning(Node3D hitTarget, Vector3 hitPos)
        {
            if (_isBounce) return; // don't chain from a bounce
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;
            if (player.PerkProcessor == null || !player.PerkProcessor.HasChainLightning()) return;

            var spaceState = GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 8f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, hitPos),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            float bestDist = float.MaxValue;
            Node3D bestTarget = null;
            foreach (var result in results)
            {
                var collider = result["collider"].As<Node3D>();
                if (collider == null || collider == hitTarget) continue;
                float dist = hitPos.DistanceTo(collider.GlobalPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = collider;
                }
            }

            if (bestTarget == null) return;

            IDamageable chainDamageable = null;
            if (bestTarget is IDamageable cd) chainDamageable = cd;
            else chainDamageable = bestTarget.GetNodeOrNull<HealthComponent>("HealthComponent");

            if (chainDamageable == null || !chainDamageable.IsAlive) return;

            float chainDmg = _damage.FinalDamage * 0.5f;
            var chainInfo = new DamageInfo
            {
                RawDamage = chainDmg,
                FinalDamage = chainDmg,
                DamageType = DamageType.Lightning,
                Attacker = _damage.Attacker,
                Target = bestTarget,
                HitPoint = bestTarget.GlobalPosition
            };
            chainDamageable.TakeDamage(chainInfo);

            // Lightning arc VFX (visible bolt between targets)
            var arc = VfxFactory.CreateLightningArc(
                hitPos + Vector3.Up * 0.8f,
                bestTarget.GlobalPosition + Vector3.Up * 0.8f);
            GetTree().Root.AddChild(arc);
        }

        private void Destroy()
        {
            _trail.Emitting = false;
            _meshVisual.Visible = false;

            // Wait for trail particles to finish, then free
            GetTree().CreateTimer(0.5f).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(this) && IsInsideTree())
                    QueueFree();
            };
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
