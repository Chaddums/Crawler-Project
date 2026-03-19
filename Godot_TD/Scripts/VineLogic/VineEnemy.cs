using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Enemy for Vine Logic TD. Follows paths that respond to network state.
    /// Different factions interact with the logic network differently.
    /// Enemies have ranged attacks targeting towers first, then player.
    /// </summary>
    public partial class VineEnemy : Node3D
    {
        public string EnemyName { get; private set; }
        public VineEnemyFaction Faction { get; private set; }
        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public float BaseSpeed { get; private set; }
        public float HealthPercent => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;
        public bool IsAlive => CurrentHealth > 0;
        public int ScrapValue { get; private set; }
        public bool IsBoss { get; private set; }

        // Multipliers for BuffDebuffComponent integration
        public float SpeedMultiplier { get; set; } = 1f;
        public float DamageMultiplier { get; set; } = 1f;
        public float ArmorBonus { get; set; }

        // BuffDebuffComponent (attached on spawn)
        private BuffDebuffComponent _buffDebuff;

        private List<Vector2I> _path;
        private int _pathIndex;
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private Vector2I _spawnEntry;

        // Slow debuff (legacy — kept for simple ApplySlow calls; BuffDebuffComponent handles complex effects)
        private float _slowAmount;
        private float _slowTimer;

        // Frame stagger — each enemy gets a random slot so expensive AI work
        // is distributed evenly across frames when enemy counts are high.
        private int _frameSlot;
        private static readonly RandomNumberGenerator _staggerRng = new();

        // March mode — cheap direct movement while far from the grid.
        // When enemies enter the combat zone, switches to full pathfinding.
        private bool _marchMode;
        private const float COMBAT_ZONE_MARGIN = 4f; // Units beyond grid bounds before switching

        // Re-pathing
        private float _repathTimer;
        private const float REPATH_INTERVAL = 1f;

        // Stuck detection
        private float _stuckTimer = -1f;
        private bool _stuckNoLifeCost;
        private Vector3 _lastPosition;
        private float _stuckCheckTimer;

        private MeshInstance3D _mesh;
        private Node3D _modelRoot;       // 3D model (null if procedural fallback)
        private MeshInstance3D _healthBar;
        private Color _baseColor;
        private CharacterAnimator _animator;

        // Smooth facing to prevent rotation jitter
        private float _smoothYaw;

        // Hit flash
        private float _flashTimer;
        private Color _originalColor;

        // Death animation
        private bool _dyingAnimPlaying;
        private float _deathAnimTimer;

        // Idle emission breathing
        private float _breathTimer;

        // Boss aura reference for planet-aware pulsing
        private MeshInstance3D _bossAura;

        // Ranged attack
        private float _attackRange;
        private float _attackDamage;
        private float _attackInterval;
        private float _attackTimer;
        private float _attackAnimTimer;  // Brief attack pose before resuming walk

        public void Initialize(string name, VineEnemyFaction faction, float health, float speed,
            int scrapValue, Color color, Vector2I spawnEntry, bool isBoss = false,
            float attackRange = 0, float attackDamage = 0, float attackInterval = 0)
        {
            EnemyName = name;
            Faction = faction;
            MaxHealth = health;
            CurrentHealth = health;
            BaseSpeed = speed;
            ScrapValue = scrapValue;
            IsBoss = isBoss;
            _baseColor = color;
            _spawnEntry = spawnEntry;

            // Attack stats: use provided values, or fall back to faction defaults
            if (attackRange > 0)
            {
                _attackRange = attackRange;
                _attackDamage = attackDamage;
                _attackInterval = attackInterval;
            }
            else
            {
                GetFactionAttackDefaults(faction, out _attackRange, out _attackDamage, out _attackInterval);
            }

            _grid = ServiceLocator.Get<VineGrid>();
            _pathfinder = ServiceLocator.Get<VinePathfinder>();

            // Frame stagger slot — random offset so AI ticks are distributed across frames
            _frameSlot = _staggerRng.RandiRange(0, 9999);

            // Attach BuffDebuffComponent
            _buffDebuff = new BuffDebuffComponent();
            _buffDebuff.Name = "BuffDebuff";
            AddChild(_buffDebuff);

            // Register with EntityRegistry for spatial queries
            if (ServiceLocator.TryGet<EntityRegistry>(out var registry))
                registry.Register(this, EntityRegistry.TYPE_ENEMY);

            AddToGroup(Constants.GROUP_VINE_ENEMY);
            BuildVisual();

            if (isBoss)
            {
                // Screen shake on boss spawn
                if (ServiceLocator.TryGet<TDCamera>(out var cam))
                    cam.Shake(1.5f, 1.0f);
                GameEvents.OnBossSpawned?.Invoke();
            }

            // Initial path
            _path = _pathfinder.GetCachedPath(spawnEntry);
            if (_path == null || _path.Count == 0)
            {
                // No path available — try direct pathfind as fallback
                _path = _pathfinder.FindPath(spawnEntry, _grid.ExitPoint);
            }
            if (_path != null && _path.Count > 0)
            {
                _pathIndex = 0;
                GlobalPosition = _grid.GridToWorld(_path[0]) + new Vector3(0, 0.3f, 0);

                // Initialize facing toward first waypoint so rotation doesn't lerp from 0
                if (_path.Count > 1)
                {
                    var firstDir = _grid.GridToWorld(_path[1]) - _grid.GridToWorld(_path[0]);
                    _smoothYaw = Mathf.Atan2(firstDir.X, firstDir.Z);
                }
            }
            else
            {
                // Completely stuck — self-destruct after brief delay so wave can complete
                GD.PushWarning($"[VineEnemy] {EnemyName} has no path from {spawnEntry}, despawning");
                _stuckTimer = 3f;
                _stuckNoLifeCost = true;
            }

            // March mode: if spawned outside the grid bounds + margin, use cheap direct movement
            // until we enter the combat zone. Saves pathfinding queries for offscreen enemies.
            if (_grid != null)
            {
                float gridMinX = 0f;
                float gridMinZ = 0f;
                float gridMaxX = _grid.Width * Constants.VINE_CELL_SIZE;
                float gridMaxZ = _grid.Height * Constants.VINE_CELL_SIZE;

                var pos = GlobalPosition;
                if (pos.X < gridMinX - COMBAT_ZONE_MARGIN || pos.X > gridMaxX + COMBAT_ZONE_MARGIN ||
                    pos.Z < gridMinZ - COMBAT_ZONE_MARGIN || pos.Z > gridMaxZ + COMBAT_ZONE_MARGIN)
                {
                    _marchMode = true;
                }
            }
        }

        /// <summary>
        /// Faction-based default attack stats.
        /// </summary>
        private static void GetFactionAttackDefaults(VineEnemyFaction faction,
            out float range, out float damage, out float interval)
        {
            switch (faction)
            {
                case VineEnemyFaction.Scavenger:
                    range = 5f; damage = 4f; interval = 1.5f; break;
                case VineEnemyFaction.Brute:
                    range = 3f; damage = 10f; interval = 2.5f; break;
                case VineEnemyFaction.Ghost:
                    range = 6f; damage = 3f; interval = 2.0f; break;
                case VineEnemyFaction.Swarm:
                    range = 4f; damage = 2f; interval = 1.0f; break;
                default:
                    range = Constants.ENEMY_ATTACK_RANGE;
                    damage = Constants.ENEMY_ATTACK_DAMAGE;
                    interval = Constants.ENEMY_FIRE_INTERVAL;
                    break;
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Death animation countdown — wait for anim then QueueFree
            if (_dyingAnimPlaying)
            {
                _deathAnimTimer -= dt;
                if (_deathAnimTimer <= 0) QueueFree();
                return;
            }

            // Stuck self-destruct countdown — despawn without costing lives
            if (_stuckTimer > 0)
            {
                _stuckTimer -= dt;
                if (_stuckTimer <= 0) { Despawn(); return; }
            }

            if (!IsAlive || _path == null || _pathIndex >= _path.Count)
            {
                // No path — try to repath, or tick stuck timer
                if (_stuckTimer < 0) { _stuckTimer = 5f; _stuckNoLifeCost = true; }
                _animator?.SetState(AnimState.Idle);
                return;
            }

            // Idle emission breathing — subtle sine pulse on all mesh emissions
            _breathTimer += dt;
            UpdateBreathingEmission();

            // Stuck detection — if we haven't moved in 4s, despawn without life cost
            _stuckCheckTimer += dt;
            if (_stuckCheckTimer >= 4f)
            {
                if (GlobalPosition.DistanceTo(_lastPosition) < 0.2f)
                {
                    GD.PushWarning($"[VineEnemy] {EnemyName} stuck at {GlobalPosition}, despawning");
                    Despawn();
                    return;
                }
                _lastPosition = GlobalPosition;
                _stuckCheckTimer = 0;
            }

            // Hit flash decay
            if (_flashTimer > 0)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0)
                {
                    if (_modelRoot != null)
                    {
                        FlashNodeRecursive(_modelRoot, false);
                        _animator?.SetState(AnimState.Walk);
                    }
                    else if (_mesh?.MaterialOverride is StandardMaterial3D flashMat)
                    {
                        flashMat.AlbedoColor = _originalColor;
                        flashMat.EmissionEnabled = false;
                    }
                }
            }

            // Attack anim timer — resume walk after brief attack pose
            if (_attackAnimTimer > 0)
            {
                _attackAnimTimer -= dt;
                if (_attackAnimTimer <= 0)
                    _animator?.SetState(AnimState.Walk);
            }

            // Periodic re-pathing — gated by frame stagger so not all enemies repath on the same frame
            if (Faction != VineEnemyFaction.Ghost)
            {
                _repathTimer += dt;
                if (_repathTimer >= REPATH_INTERVAL && ShouldProcessAI())
                {
                    _repathTimer = 0;
                    TryRepath();
                }
            }

            // Movement
            float speed = BaseSpeed * SpeedMultiplier;
            if (_slowTimer > 0)
            {
                // Brutes resist slow
                float slowResist = Faction == VineEnemyFaction.Brute ? 0.5f : 1f;
                speed *= (1f - _slowAmount * slowResist);
                _slowTimer -= dt;
            }

            // DataStream speed boost
            var currentGridPos = _grid.WorldToGrid(GlobalPosition);
            if (_grid.GetCell(currentGridPos) == VineCellType.DataStream)
                speed *= 1.5f;

            // March mode: cheap direct movement toward the first waypoint inside the grid.
            // Once inside the combat zone, switch to full pathfinding.
            if (_marchMode)
            {
                var marchTarget = _path.Count > 0
                    ? _grid.GridToWorld(_path[Mathf.Min(_pathIndex, _path.Count - 1)]) + new Vector3(0, 0.3f, 0)
                    : GlobalPosition;
                var marchDir = marchTarget - GlobalPosition;
                marchDir.Y = 0;
                if (marchDir.LengthSquared() > 0.001f)
                {
                    GlobalPosition += marchDir.Normalized() * speed * dt;
                    float targetYaw = Mathf.Atan2(marchDir.X, marchDir.Z);
                    _smoothYaw = Mathf.LerpAngle(_smoothYaw, targetYaw, dt * 25f);
                    if (_modelRoot != null)
                        _modelRoot.Rotation = new Vector3(0, _smoothYaw, 0);
                    else if (_mesh != null)
                        _mesh.Rotation = new Vector3(0, _smoothYaw, 0);
                }

                // Check if we've entered the combat zone
                float gridMaxX = _grid.Width * Constants.VINE_CELL_SIZE;
                float gridMaxZ = _grid.Height * Constants.VINE_CELL_SIZE;
                var p = GlobalPosition;
                if (p.X >= -COMBAT_ZONE_MARGIN && p.X <= gridMaxX + COMBAT_ZONE_MARGIN &&
                    p.Z >= -COMBAT_ZONE_MARGIN && p.Z <= gridMaxZ + COMBAT_ZONE_MARGIN)
                {
                    _marchMode = false;
                    // Re-path now that we're in the combat zone
                    TryRepath();
                }

                UpdateHealthBar();
                return;
            }

            // Advance through any reached waypoints without pausing movement
            var targetPos = _grid.GridToWorld(_path[_pathIndex]) + new Vector3(0, 0.3f, 0);
            var dir = targetPos - GlobalPosition;
            float dist = new Vector2(dir.X, dir.Z).Length();

            while (dist < 0.15f)
            {
                HandleCellArrival(_path[_pathIndex]);
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    ReachExit();
                    return;
                }
                targetPos = _grid.GridToWorld(_path[_pathIndex]) + new Vector3(0, 0.3f, 0);
                dir = targetPos - GlobalPosition;
                dist = new Vector2(dir.X, dir.Z).Length();
            }

            // Always move — no skipped frames at waypoints
            GlobalPosition += dir.Normalized() * speed * dt;

            // Smooth facing
            if (dir.LengthSquared() > 0.001f)
            {
                float targetYaw = Mathf.Atan2(dir.X, dir.Z);
                _smoothYaw = Mathf.LerpAngle(_smoothYaw, targetYaw, dt * 25f);
                if (_modelRoot != null)
                    _modelRoot.Rotation = new Vector3(0, _smoothYaw, 0);
                else
                    _mesh.Rotation = new Vector3(0, _smoothYaw, 0);
            }

            // Sync animation speed with actual movement speed
            float animSpeed = Mathf.Clamp(speed / 3f, 0.5f, 2f);
            if (_animator != null)
                _animator.SetSpeed(animSpeed);

            // Ranged attack — enemies shoot while walking
            UpdateRangedAttack(dt);

            CheckPlayerContact(dt);
            UpdateHealthBar();
        }

        // ── Ranged Attack System ──

        private void UpdateRangedAttack(float dt)
        {
            _attackTimer -= dt;
            if (_attackTimer > 0) return;

            // Gate expensive targeting by frame stagger
            if (!ShouldProcessAI()) return;

            // Find closest target — towers first, then player
            Node3D target = FindAttackTarget();
            if (target == null) return;

            // Fire!
            _attackTimer = _attackInterval;

            // Play attack animation briefly
            _animator?.SetState(AnimState.Attack);
            _attackAnimTimer = 0.4f;

            // VFX: muzzle flash + projectile
            var muzzlePos = GlobalPosition + new Vector3(0, 0.5f, 0);
            VfxFactory.SpawnMuzzleFlash(GetTree(), muzzlePos, DamageType.Physical);
            VfxFactory.SpawnProjectile(GetTree(), muzzlePos, target.GlobalPosition, _baseColor);

            // Apply damage (scaled by DamageMultiplier from buffs)
            float dmg = _attackDamage * DamageMultiplier;
            if (target is VineNode node)
                node.TakeDamage(dmg);
            else if (target is VinePlayer player)
                player.TakeDamage(dmg);
        }

        private Node3D FindAttackTarget()
        {
            // Priority 1: Effect towers (DamageTower, SlowField, PushPull)
            var nodes = GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE);
            VineNode closestNode = null;
            float closestDist = _attackRange;

            foreach (var n in nodes)
            {
                if (n is not VineNode vn) continue;
                if (vn.IsDestroyed) continue;
                if (vn.Data?.Category != VineNodeCategory.Effect) continue;

                float d = GlobalPosition.DistanceTo(vn.GlobalPosition);
                if (d < closestDist)
                {
                    closestDist = d;
                    closestNode = vn;
                }
            }

            if (closestNode != null) return closestNode;

            // Priority 2: Player
            if (ServiceLocator.TryGet<VinePlayer>(out var player) && player.IsAlive)
            {
                float playerDist = GlobalPosition.DistanceTo(player.GlobalPosition);
                if (playerDist < _attackRange)
                    return player;
            }

            return null;
        }

        private void HandleCellArrival(Vector2I cell)
        {
            var node = _grid.GetNode(cell);
            if (node == null) return;

            switch (Faction)
            {
                case VineEnemyFaction.Brute:
                    // Brutes can break switch states
                    if (node.Data?.Type == VineNodeType.Switch)
                    {
                        // Force switch to toggle (break the logic)
                        node.ReceiveSignal(SignalType.Trigger, 0.5f, cell);
                    }
                    break;

                case VineEnemyFaction.Scavenger:
                    // Scavengers get confused by flickering gates — brief pause
                    if (node.Data?.Type == VineNodeType.Gate && !node.IsOpen)
                        _slowTimer = Mathf.Max(_slowTimer, 0.5f);
                    break;
            }
        }

        private void TryRepath()
        {
            if (_pathfinder == null || _path == null || _pathIndex >= _path.Count) return;

            // Get current grid position
            var currentGrid = _grid.WorldToGrid(GlobalPosition);

            // Ghost faction phases through gates and nodes — only walls stop them
            if (Faction == VineEnemyFaction.Ghost)
            {
                _path = _pathfinder.FindGhostPath(currentGrid, _grid.ExitPoint);
                // Skip cell 0 (our current cell) so we don't backtrack to its center
                _pathIndex = _path.Count > 1 ? 1 : 0;
                return;
            }

            // Get new path from current position
            var newPath = _pathfinder.FindPath(currentGrid, _grid.ExitPoint);
            if (newPath != null && newPath.Count > 0)
            {
                _path = newPath;
                // Skip cell 0 (our current cell) so we don't backtrack to its center
                _pathIndex = newPath.Count > 1 ? 1 : 0;
            }
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            // Apply armor reduction
            float reducedAmount = Mathf.Max(1f, amount - ArmorBonus);
            CurrentHealth -= reducedAmount;
            FlashMesh();

            if (!IsAlive) Die();
        }

        public void ApplySlow(float amount, float duration)
        {
            _slowAmount = Mathf.Max(_slowAmount, amount);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        private void Die()
        {
            // Clean up buff/debuff state and registry
            _buffDebuff?.ClearAll();
            UnregisterFromRegistry();

            // Drop scrap
            GameEvents.OnScrapDropped?.Invoke(GlobalPosition, ScrapValue);
            GameEvents.OnEnemyKilled?.Invoke(this);

            // Death VFX
            VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition, _baseColor, 6);

            // Play death animation if available, otherwise instant death
            if (_animator != null && _animator.IsInitialized)
            {
                _animator.SetState(AnimState.Death);
                _animator.SetSpeed(1.5f);
                _dyingAnimPlaying = true;
                _deathAnimTimer = 0.8f; // Max time to wait for death anim
                // Hide health bar during death
                if (_healthBar != null) _healthBar.Visible = false;
            }
            else
            {
                QueueFree();
            }
        }

        private void ReachExit()
        {
            GameEvents.OnEnemyLeaked?.Invoke(this, GlobalPosition);

            // Deal damage to harvester if it exists, otherwise fallback to core lives
            if (_grid?.Harvester != null && !_grid.Harvester.IsDestroyed)
            {
                float damage = IsBoss ? 50f : 10f + MaxHealth * 0.1f;
                _grid.Harvester.TakeDamage(damage);
            }
            else
            {
                GameManager.Instance?.OnEnemyReachedCore();
            }

            QueueFree();
        }

        // Contact damage to player
        private float _contactDamageCooldown;

        private void CheckPlayerContact(float dt)
        {
            _contactDamageCooldown -= dt;
            if (_contactDamageCooldown > 0) return;

            if (!ServiceLocator.TryGet<VinePlayer>(out var player)) return;
            if (!player.IsAlive) return;

            float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
            if (dist < 1.5f)
            {
                player.TakeDamage(5f + MaxHealth * 0.05f);
                _contactDamageCooldown = 1f;
            }
        }

        /// <summary>
        /// Remove from the map without costing a life. Used for stuck enemies.
        /// </summary>
        private void Despawn()
        {
            UnregisterFromRegistry();
            GameEvents.OnEnemyKilled?.Invoke(this);
            QueueFree();
        }

        private void UnregisterFromRegistry()
        {
            if (ServiceLocator.TryGet<EntityRegistry>(out var registry))
                registry.Unregister(this, EntityRegistry.TYPE_ENEMY);
        }

        /// <summary>
        /// Returns true if this enemy should run expensive AI work this frame.
        /// Two layers of throttling:
        ///   1. Count-based stagger: spreads AI ticks across frames at high enemy counts.
        ///   2. Frame budget: defers work entirely when the frame is already over budget.
        /// Movement still runs every frame — only targeting, pathfinding, and behavior are deferred.
        /// </summary>
        private bool ShouldProcessAI()
        {
            // Check frame budget first (cheapest check)
            if (ServiceLocator.TryGet<FrameBudget>(out var budget) && !budget.HasBudget())
                return false;

            // Count-based stagger — only matters at high enemy counts
            int count = 0;
            if (ServiceLocator.TryGet<EntityRegistry>(out var registry))
                count = registry.GetCount(EntityRegistry.TYPE_ENEMY);

            if (count < 80)
                return true;

            int skip;
            if (count < 150) skip = 2;
            else if (count < 250) skip = 3;
            else skip = 5;

            return ((long)Engine.GetProcessFrames() + _frameSlot) % skip == 0;
        }

        /// <summary>
        /// Get the BuffDebuffComponent for external systems to apply effects.
        /// </summary>
        public BuffDebuffComponent GetBuffDebuff() => _buffDebuff;

        // ── Visuals ──

        private void BuildVisual()
        {
            float scale = IsBoss ? Constants.BOSS_SCALE : 1f;

            // Try to load a real 3D model based on faction
            string modelPath = GetModelPathForFaction(Faction);
            _modelRoot = modelPath != null ? AssetLibrary.InstantiateNormalized(modelPath) : null;

            if (_modelRoot != null)
            {
                // Scale bosses up
                if (IsBoss)
                    _modelRoot.Scale *= Constants.BOSS_SCALE;

                AddChild(_modelRoot);
                AssetLibrary.GroundModel(_modelRoot);

                // Apply planet theme
                PlanetTheme.Current.ApplyEnemyTheme(_modelRoot, Faction);

                // Try splitting monolithic animation into named clips
                CharacterAnimator.SplitMonolithicAnimation(_modelRoot);

                // Initialize animator
                _animator = new CharacterAnimator();
                AddChild(_animator);
                _animator.Initialize(_modelRoot);

                // Start walking immediately — enemies spawn and move
                _animator.SetState(AnimState.Walk);
                // Scale walk speed proportional to movement speed
                _animator.SetSpeed(Mathf.Clamp(BaseSpeed / 3f, 0.5f, 2f));

                // Attach weapon model for Scavenger faction
                AttachWeaponModel();

                // Create a minimal _mesh for rotation/flash (invisible — just a pivot)
                _mesh = new MeshInstance3D();
                _mesh.Visible = false;
                AddChild(_mesh);
            }
            else
            {
                // Procedural fallback
                _mesh = new MeshInstance3D();

                Mesh meshShape = Faction switch {
                    VineEnemyFaction.Ghost => CreateSphereMesh(0.35f * scale),
                    VineEnemyFaction.Swarm => CreateBoxMesh(0.25f * scale),
                    VineEnemyFaction.Brute => CreateBoxMesh(0.5f * scale),
                    _ => CreateBoxMesh(0.35f * scale)
                };
                _mesh.Mesh = meshShape;

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = _baseColor;
                mat.Roughness = 0.7f;
                mat.Metallic = 0.4f;
                mat.EmissionEnabled = true;
                mat.Emission = _baseColor;
                bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
                mat.EmissionEnergyMultiplier = IsBoss ? 2.0f : (isScrapyard ? 0.3f : 0.8f);
                _mesh.MaterialOverride = mat;
                AddChild(_mesh);
            }

            // Boss aura ring — planet-aware color
            if (IsBoss)
            {
                _bossAura = new MeshInstance3D();
                var torus = new TorusMesh();
                torus.InnerRadius = 0.6f;
                torus.OuterRadius = 0.9f;
                _bossAura.Mesh = torus;
                _bossAura.Position = new Vector3(0, 0.1f, 0);
                bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
                var bossColor = isScrapyard ? new Color(0.9f, 0.4f, 0.1f) : TronTheme.BossGlow;
                var auraMat = new StandardMaterial3D();
                auraMat.AlbedoColor = bossColor;
                auraMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                auraMat.EmissionEnabled = true;
                auraMat.Emission = bossColor;
                auraMat.EmissionEnergyMultiplier = 1.5f;
                _bossAura.MaterialOverride = auraMat;
                AddChild(_bossAura);
            }

            // Health bar
            float barWidth = IsBoss ? 1.2f : 0.6f;
            float barY = IsBoss ? 1.2f : 0.6f;
            _healthBar = new MeshInstance3D();
            var barMesh = new BoxMesh();
            barMesh.Size = new Vector3(barWidth, 0.06f, 0.06f);
            _healthBar.Mesh = barMesh;
            _healthBar.Position = new Vector3(0, barY, 0);
            var barMat = new StandardMaterial3D();
            barMat.AlbedoColor = new Color(0.1f, 0.9f, 0.1f);
            barMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBar.MaterialOverride = barMat;
            AddChild(_healthBar);
        }

        /// <summary>
        /// Attach a weapon model to Scavenger enemies. Other factions use
        /// body-based attacks (Brute slam, Ghost spectral, Swarm energy zaps).
        /// </summary>
        private void AttachWeaponModel()
        {
            if (Faction != VineEnemyFaction.Scavenger) return;
            if (_modelRoot == null) return;

            var weapon = AssetLibrary.InstantiateNormalized(AssetLibrary.WEAPON_A);
            if (weapon == null) return;

            weapon.Scale = new Vector3(0.3f, 0.3f, 0.3f);
            weapon.Position = new Vector3(0.2f, 0.4f, 0.3f); // Roughly hand height
            _modelRoot.AddChild(weapon);

            // Tint weapon to match faction
            PlanetTheme.Current.ApplyEnemyTheme(weapon, Faction);
        }

        /// <summary>
        /// Map enemy factions to 3D model asset paths.
        /// </summary>
        private static string GetModelPathForFaction(VineEnemyFaction faction)
        {
            return faction switch {
                VineEnemyFaction.Scavenger => AssetLibrary.ENEMY_SCRAP_RAT,
                VineEnemyFaction.Brute => AssetLibrary.ENEMY_QUAD_SHELL,
                VineEnemyFaction.Ghost => AssetLibrary.ENEMY_TRILOBITE,
                VineEnemyFaction.Swarm => AssetLibrary.ENEMY_SPARK_DRONE,
                _ => null
            };
        }

        private static SphereMesh CreateSphereMesh(float radius)
        {
            var s = new SphereMesh();
            s.Radius = radius;
            s.Height = radius * 2;
            return s;
        }

        private static BoxMesh CreateBoxMesh(float size)
        {
            var b = new BoxMesh();
            b.Size = new Vector3(size, size * 1.5f, size);
            return b;
        }

        private void FlashMesh()
        {
            if (_modelRoot != null)
            {
                // Flash all mesh children in the 3D model to white
                FlashNodeRecursive(_modelRoot, true);
                _flashTimer = 0.08f;
                // Brief hit animation
                _animator?.SetState(AnimState.Hit);
            }
            else if (_mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                if (_flashTimer <= 0) _originalColor = mat.AlbedoColor;
                mat.AlbedoColor = Colors.White;
                mat.EmissionEnabled = true;
                mat.Emission = Colors.White;
                mat.EmissionEnergyMultiplier = 1.5f;
                _flashTimer = 0.08f;
            }
        }

        /// <summary>
        /// Subtle idle emission breathing — sine wave pulse on emission energy.
        /// </summary>
        private void UpdateBreathingEmission()
        {
            if (_modelRoot == null) return;
            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            float baseEmission = isScrapyard ? 0.15f : 0.4f;
            float pulse = baseEmission + Mathf.Sin(_breathTimer * Mathf.Pi) * 0.1f;
            SetEmissionEnergyRecursive(_modelRoot, pulse);
        }

        private static void SetEmissionEnergyRecursive(Node node, float energy)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat && mat.EmissionEnabled)
                mat.EmissionEnergyMultiplier = energy;
            foreach (var child in node.GetChildren())
                SetEmissionEnergyRecursive(child, energy);
        }

        private static void FlashNodeRecursive(Node node, bool flash)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
            {
                bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
                if (flash)
                {
                    mat.EmissionEnabled = true;
                    mat.Emission = Colors.White;
                    mat.EmissionEnergyMultiplier = isScrapyard ? 1.2f : 3f;
                }
                else
                {
                    mat.EmissionEnergyMultiplier = isScrapyard ? 0.15f : 0.4f;
                }
            }
            foreach (var child in node.GetChildren())
                FlashNodeRecursive(child, flash);
        }

        private void UpdateHealthBar()
        {
            if (_healthBar == null) return;
            float pct = Mathf.Clamp(HealthPercent, 0f, 1f);
            float halfBar = IsBoss ? 0.6f : 0.3f;
            _healthBar.Scale = new Vector3(pct, 1, 1);
            _healthBar.Position = new Vector3((pct - 1f) * halfBar, _healthBar.Position.Y, 0);

            if (_healthBar.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = pct > 0.5f
                    ? new Color(0.1f, 0.9f, 0.1f)
                    : pct > 0.25f
                        ? new Color(0.9f, 0.7f, 0.1f)
                        : new Color(0.9f, 0.1f, 0.1f);
        }
    }
}
