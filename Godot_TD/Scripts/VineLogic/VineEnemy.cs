using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Enemy for Vine Logic TD. Follows paths that respond to network state.
    /// Different factions interact with the logic network differently.
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

        private List<Vector2I> _path;
        private int _pathIndex;
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private Vector2I _spawnEntry;

        // Slow debuff
        private float _slowAmount;
        private float _slowTimer;

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

        // Hit flash
        private float _flashTimer;
        private Color _originalColor;

        public void Initialize(string name, VineEnemyFaction faction, float health, float speed,
            int scrapValue, Color color, Vector2I spawnEntry, bool isBoss = false)
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

            _grid = ServiceLocator.Get<VineGrid>();
            _pathfinder = ServiceLocator.Get<VinePathfinder>();

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
            }
            else
            {
                // Completely stuck — self-destruct after brief delay so wave can complete
                GD.PushWarning($"[VineEnemy] {EnemyName} has no path from {spawnEntry}, despawning");
                _stuckTimer = 3f;
                _stuckNoLifeCost = true;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

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
                return;
            }

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
                    }
                    else if (_mesh?.MaterialOverride is StandardMaterial3D flashMat)
                    {
                        flashMat.AlbedoColor = _originalColor;
                        flashMat.EmissionEnabled = false;
                    }
                }
            }

            // Periodic re-pathing (enemies respond to gate/switch changes)
            if (Faction != VineEnemyFaction.Ghost)
            {
                _repathTimer += dt;
                if (_repathTimer >= REPATH_INTERVAL)
                {
                    _repathTimer = 0;
                    TryRepath();
                }
            }

            // Movement
            float speed = BaseSpeed;
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

            var targetPos = _grid.GridToWorld(_path[_pathIndex]) + new Vector3(0, 0.3f, 0);
            var dir = targetPos - GlobalPosition;
            dir.Y = 0;
            float dist = dir.Length();

            if (dist < 0.15f)
            {
                // Arrived at waypoint — check for faction-specific interactions
                var currentCell = _path[_pathIndex];
                HandleCellArrival(currentCell);

                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    ReachExit();
                    return;
                }
            }
            else
            {
                GlobalPosition += dir.Normalized() * speed * dt;

                // Face movement direction
                if (dir.LengthSquared() > 0.001f)
                {
                    float yaw = Mathf.Atan2(dir.X, dir.Z);
                    if (_modelRoot != null)
                        _modelRoot.Rotation = new Vector3(0, yaw, 0);
                    else
                        _mesh.Rotation = new Vector3(0, yaw, 0);
                }
            }

            UpdateHealthBar();
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
                _pathIndex = 0;
                return;
            }

            // Get new path from current position
            var newPath = _pathfinder.FindPath(currentGrid, _grid.ExitPoint);
            if (newPath != null && newPath.Count > 0)
            {
                _path = newPath;
                _pathIndex = 0;
            }
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            CurrentHealth -= amount;
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
            // Drop scrap
            GameEvents.OnScrapDropped?.Invoke(GlobalPosition, ScrapValue);
            GameEvents.OnEnemyKilled?.Invoke(this);

            // Death VFX
            VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition, _baseColor, 6);

            QueueFree();
        }

        private void ReachExit()
        {
            GameEvents.OnEnemyLeaked?.Invoke(this, GlobalPosition);
            GameManager.Instance?.OnEnemyReachedCore();
            QueueFree();
        }

        /// <summary>
        /// Remove from the map without costing a life. Used for stuck enemies.
        /// </summary>
        private void Despawn()
        {
            GameEvents.OnEnemyKilled?.Invoke(this);
            QueueFree();
        }

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
                mat.EmissionEnergyMultiplier = IsBoss ? 2.0f : 0.8f;
                _mesh.MaterialOverride = mat;
                AddChild(_mesh);
            }

            // Boss aura ring
            if (IsBoss)
            {
                var aura = new MeshInstance3D();
                var torus = new TorusMesh();
                torus.InnerRadius = 0.6f;
                torus.OuterRadius = 0.9f;
                aura.Mesh = torus;
                aura.Position = new Vector3(0, 0.1f, 0);
                var auraMat = new StandardMaterial3D();
                auraMat.AlbedoColor = TronTheme.BossGlow;
                auraMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                auraMat.EmissionEnabled = true;
                auraMat.Emission = TronTheme.BossGlow;
                auraMat.EmissionEnergyMultiplier = 1.5f;
                aura.MaterialOverride = auraMat;
                AddChild(aura);
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

        private static void FlashNodeRecursive(Node node, bool flash)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
            {
                if (flash)
                {
                    mat.EmissionEnabled = true;
                    mat.Emission = Colors.White;
                    mat.EmissionEnergyMultiplier = 3f;
                }
                else
                {
                    mat.EmissionEnergyMultiplier = 0.4f;
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
