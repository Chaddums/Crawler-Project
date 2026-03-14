using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Hero Bot (Pillar #6) — a deployable avatar the player controls directly.
    /// Can repair towers, collect scrap, body-block enemies, and AoE slam.
    /// Deploy with R, click to move, Q to slam, E to repair nearest tower.
    /// </summary>
    public partial class HeroBotController : CharacterBody3D
    {
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public bool IsDeployed { get; private set; }

        private float _slamCooldown;
        private float _respawnTimer;
        private Vector3 _moveTarget;
        private bool _hasTarget;

        private MeshInstance3D _bodyMesh;
        private MeshInstance3D _healthBar;
        private MapGrid _grid;

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<MapGrid>();
            CurrentHealth = Constants.HERO_MAX_HEALTH;

            CollisionLayer = (uint)(1 << (Constants.LAYER_HERO - 1));
            CollisionMask = Constants.MASK_GROUND;

            BuildVisual();
            AddToGroup(Constants.GROUP_HERO);
            Visible = false;
            SetPhysicsProcess(false);

            ServiceLocator.Register(this);
        }

        private void BuildVisual()
        {
            // Body
            _bodyMesh = new MeshInstance3D();
            var capsule = new CapsuleMesh();
            capsule.Radius = 0.3f;
            capsule.Height = 1.0f;
            _bodyMesh.Mesh = capsule;
            _bodyMesh.Position = new Vector3(0, 0.5f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.2f, 0.7f, 0.9f);
            mat.Metallic = 0.5f;
            mat.Roughness = 0.5f;
            mat.Emission = new Color(0.1f, 0.3f, 0.5f);
            mat.EmissionEnabled = true;
            mat.EmissionEnergyMultiplier = 0.5f;
            _bodyMesh.MaterialOverride = mat;
            AddChild(_bodyMesh);

            // Antenna
            var antenna = new MeshInstance3D();
            var antBox = new BoxMesh();
            antBox.Size = new Vector3(0.05f, 0.4f, 0.05f);
            antenna.Mesh = antBox;
            antenna.Position = new Vector3(0, 1.2f, 0);
            var antMat = new StandardMaterial3D();
            antMat.AlbedoColor = new Color(0.9f, 0.7f, 0.2f);
            antMat.EmissionEnabled = true;
            antMat.Emission = new Color(0.8f, 0.6f, 0.1f);
            antMat.EmissionEnergyMultiplier = 1.5f;
            antenna.MaterialOverride = antMat;
            AddChild(antenna);

            // Collision
            var collision = new CollisionShape3D();
            var shape = new CapsuleShape3D();
            shape.Radius = 0.3f;
            shape.Height = 1.0f;
            collision.Shape = shape;
            collision.Position = new Vector3(0, 0.5f, 0);
            AddChild(collision);

            // Health bar
            _healthBar = new MeshInstance3D();
            var barMesh = new BoxMesh();
            barMesh.Size = new Vector3(0.6f, 0.06f, 0.06f);
            _healthBar.Mesh = barMesh;
            _healthBar.Position = new Vector3(0, 1.5f, 0);
            var barMat = new StandardMaterial3D();
            barMat.AlbedoColor = new Color(0.2f, 0.8f, 1f);
            barMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBar.MaterialOverride = barMat;
            AddChild(_healthBar);
        }

        public void Deploy(Vector3 position)
        {
            if (IsDeployed) return;
            if (!ServiceLocator.TryGet<ScrapManager>(out var scrap)) return;
            if (!scrap.TrySpend(Constants.HERO_DEPLOY_COST)) return;

            IsDeployed = true;
            CurrentHealth = Constants.HERO_MAX_HEALTH;
            GlobalPosition = position + new Vector3(0, 0, 0);
            Visible = true;
            SetPhysicsProcess(true);
            _hasTarget = false;

            GameEvents.OnHeroBotDeployed?.Invoke(this);
            GD.Print("[HeroBot] Deployed!");
        }

        public void Recall()
        {
            if (!IsDeployed) return;
            IsDeployed = false;
            Visible = false;
            SetPhysicsProcess(false);
            _hasTarget = false;

            // Partial refund
            if (ServiceLocator.TryGet<ScrapManager>(out var scrap))
                scrap.AddScrap(Constants.HERO_DEPLOY_COST / 2);

            GameEvents.OnHeroBotRecalled?.Invoke(this);
            GD.Print("[HeroBot] Recalled.");
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsDeployed || !IsAlive) return;

            _slamCooldown -= (float)delta;

            // Movement toward target
            if (_hasTarget)
            {
                var dir = (_moveTarget - GlobalPosition);
                dir.Y = 0;
                if (dir.Length() > 0.3f)
                {
                    Velocity = dir.Normalized() * Constants.HERO_MOVE_SPEED;
                    MoveAndSlide();

                    // Face direction
                    float angle = Mathf.Atan2(dir.X, dir.Z);
                    _bodyMesh.Rotation = new Vector3(0, angle, 0);
                }
                else
                {
                    _hasTarget = false;
                    Velocity = Vector3.Zero;
                }
            }

            // Auto-collect nearby scrap
            if (ServiceLocator.TryGet<ScrapManager>(out var scrapMgr))
                scrapMgr.TryCollectNear(GlobalPosition, Constants.SCRAP_COLLECT_RADIUS * 1.5f);

            UpdateHealthBar();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // R to deploy/recall
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
            {
                if (IsDeployed)
                    Recall();
                else
                    DeployAtMouse();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!IsDeployed) return;

            // Click to move
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Middle)
            {
                var worldPos = MouseToGround(mb.Position);
                if (worldPos.HasValue)
                {
                    _moveTarget = worldPos.Value;
                    _hasTarget = true;
                }
                GetViewport().SetInputAsHandled();
            }

            // Q to slam
            if (@event is InputEventKey slamKey && slamKey.Pressed && slamKey.Keycode == Key.Q)
            {
                TrySlam();
                GetViewport().SetInputAsHandled();
            }

            // E to repair
            if (@event is InputEventKey repairKey && repairKey.Pressed && repairKey.Keycode == Key.E)
            {
                TryRepair();
                GetViewport().SetInputAsHandled();
            }
        }

        private void DeployAtMouse()
        {
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;
            var mousePos = GetViewport().GetMousePosition();
            var worldPos = MouseToGround(mousePos);
            if (worldPos.HasValue)
                Deploy(worldPos.Value);
        }

        private Vector3? MouseToGround(Vector2 screenPos)
        {
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return null;
            var from = camera.ProjectRayOrigin(screenPos);
            var dir = camera.ProjectRayNormal(screenPos);
            if (Mathf.Abs(dir.Y) < 0.001f) return null;
            float t = -from.Y / dir.Y;
            if (t < 0) return null;
            return from + dir * t;
        }

        private void TrySlam()
        {
            if (_slamCooldown > 0) return;
            _slamCooldown = Constants.HERO_SLAM_COOLDOWN;

            // VFX
            VfxFactory.SpawnSplashRing(GetTree(), GlobalPosition, Constants.HERO_SLAM_RADIUS / Constants.CELL_SIZE, DamageType.Physical);
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(0.3f, 0.2f);

            // Damage + knockback
            foreach (var node in GetTree().GetNodesInGroup(Constants.GROUP_ENEMY))
            {
                if (node is not EnemyController enemy) continue;
                float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
                if (dist <= Constants.HERO_SLAM_RADIUS)
                {
                    var info = new DamageInfo
                    {
                        RawDamage = Constants.HERO_SLAM_DAMAGE,
                        FinalDamage = Constants.HERO_SLAM_DAMAGE,
                        DamageType = DamageType.Physical,
                        Attacker = this,
                        Target = enemy,
                        HitPoint = enemy.GlobalPosition
                    };
                    enemy.TakeDamage(info);
                }
            }

            GD.Print("[HeroBot] SLAM!");
        }

        private void TryRepair()
        {
            TowerController nearest = null;
            float nearestDist = Constants.HERO_REPAIR_RANGE;

            foreach (var node in GetTree().GetNodesInGroup(Constants.GROUP_TOWER))
            {
                if (node is not TowerController tower) continue;
                float dist = GlobalPosition.DistanceTo(tower.GlobalPosition);
                if (dist < nearestDist)
                {
                    nearest = tower;
                    nearestDist = dist;
                }
            }

            if (nearest != null)
            {
                // Visual feedback
                VfxFactory.SpawnHitFlash(GetTree(), nearest.GlobalPosition, DamageType.Holy);
                GD.Print($"[HeroBot] Repaired {nearest.Data.Name}");
            }
        }

        public void TakeDamage(float damage)
        {
            CurrentHealth -= damage;
            if (!IsAlive)
            {
                GD.Print("[HeroBot] Destroyed! Respawning...");
                Recall();
            }
        }

        private void UpdateHealthBar()
        {
            float pct = CurrentHealth / Constants.HERO_MAX_HEALTH;
            _healthBar.Scale = new Vector3(pct, 1, 1);
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<HeroBotController>();
        }
    }
}
