using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// An Ascendant entity on the battlefield. Massively overpowered AI that fights
    /// other Ascendants — not the player. The player is just the stage.
    ///
    /// Ascendants ignore all player units, towers, and the Spire.
    /// They pathfind toward their rival and fight until one falls.
    /// Collateral damage from their clash destroys terrain and damages nearby nodes.
    /// </summary>
    public partial class Ascendant : Node3D
    {
        public string AscendantId { get; private set; }
        public string AscendantName { get; private set; }
        public string Faction { get; private set; }  // "enemy" or "friendly"
        public string Motivation { get; private set; }
        public string CombatStyle { get; private set; }

        public float MaxHP { get; private set; }
        public float CurrentHP { get; private set; }
        public float Damage { get; private set; }
        public float Speed { get; private set; }
        public float AttackRange { get; private set; }
        public float AttackInterval { get; private set; }
        public float ChaosRadius { get; private set; }
        public int ChaosTerrainDamage { get; private set; }

        public bool IsAlive => CurrentHP > 0;
        public bool IsEnemy => Faction == "enemy";

        // Combat state
        private Ascendant _rival;
        private float _attackTimer;
        private float _chaosTimer;
        private const float CHAOS_TICK_INTERVAL = 2f;

        // Visual
        private MeshInstance3D _mesh;
        private MeshInstance3D _healthBar;
        private Color _color;
        private float _modelScale;

        // Grid reference for chaos damage
        private VineGrid _grid;

        public void Initialize(AscendantProfile profile, VineGrid grid)
        {
            AscendantId = profile.Id;
            AscendantName = profile.Name;
            Faction = profile.Faction;
            Motivation = profile.Motivation;
            CombatStyle = profile.CombatStyle;
            MaxHP = profile.HP;
            CurrentHP = MaxHP;
            Damage = profile.Damage;
            Speed = profile.Speed;
            AttackRange = profile.AttackRange;
            AttackInterval = profile.AttackInterval;
            ChaosRadius = profile.ChaosRadius;
            ChaosTerrainDamage = profile.ChaosTerrainDamage;
            _color = new Color(profile.ColorR, profile.ColorG, profile.ColorB);
            _modelScale = profile.ModelScale;
            _grid = grid;

            BuildVisual();
            GD.Print($"[Ascendant] {AscendantName} spawned ({Faction}). HP: {MaxHP}, DMG: {Damage}");
        }

        public void SetRival(Ascendant rival)
        {
            _rival = rival;
            GD.Print($"[Ascendant] {AscendantName} targeting rival: {rival?.AscendantName ?? "none"}");
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsAlive || _rival == null || !_rival.IsAlive) return;

            float dt = (float)delta;

            // Move toward rival
            float distance = GlobalPosition.DistanceTo(_rival.GlobalPosition);
            if (distance > AttackRange)
            {
                var direction = (_rival.GlobalPosition - GlobalPosition).Normalized();
                GlobalPosition += direction * Speed * dt;

                // Face rival
                if (direction.LengthSquared() > 0.001f)
                    LookAt(GlobalPosition + direction, Vector3.Up);
            }
            else
            {
                // Attack rival
                _attackTimer -= dt;
                if (_attackTimer <= 0)
                {
                    _attackTimer = AttackInterval;
                    AttackRival();
                }
            }

            // Periodic chaos damage to nearby terrain
            _chaosTimer -= dt;
            if (_chaosTimer <= 0)
            {
                _chaosTimer = CHAOS_TICK_INTERVAL;
                ApplyChaos();
            }

            // Update health bar
            UpdateHealthBar();
        }

        private void AttackRival()
        {
            if (_rival == null || !_rival.IsAlive) return;

            float dmg = Damage;
            _rival.TakeDamage(dmg);

            // VFX: projectile or melee hit
            if (CombatStyle == "ranged_artillery")
            {
                VfxFactory.SpawnProjectile(GetTree(),
                    GlobalPosition + Vector3.Up * 2f,
                    _rival.GlobalPosition + Vector3.Up * 1f,
                    _color, 0.4f);
            }
            else
            {
                VfxFactory.SpawnAreaPulse(GetTree(), GlobalPosition, AttackRange * 0.5f, _color);
            }

            // Screen shake proportional to damage
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(0.3f + dmg * 0.002f, 0.2f);
        }

        public void TakeDamage(float amount)
        {
            CurrentHP = Mathf.Max(0, CurrentHP - amount);

            if (CurrentHP <= 0)
            {
                GD.Print($"[Ascendant] {AscendantName} has fallen.");
                OnDeath();
            }
        }

        private void OnDeath()
        {
            // Big VFX burst
            VfxFactory.SpawnBossDeathBurst(GetTree(), GlobalPosition, _color);

            // Screen shake
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(1.5f, 0.5f);

            // Final chaos burst — bigger than normal
            ApplyChaos(radiusMult: 2f);

            // Notify manager
            GameEvents.OnAscendantDefeated?.Invoke(this);

            // Remove after a brief delay
            var timer = GetTree().CreateTimer(2f);
            timer.Timeout += QueueFree;
        }

        /// <summary>
        /// Apply chaos damage to the map around this Ascendant.
        /// Destroys walls, damages player nodes caught in the radius.
        /// </summary>
        private void ApplyChaos(float radiusMult = 1f)
        {
            if (_grid == null) return;

            float radius = ChaosRadius * radiusMult;
            Vector2I gridPos = _grid.WorldToGrid(GlobalPosition);

            int cellRadius = Mathf.CeilToInt(radius / 2f);
            int destroyed = 0;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    int x = gridPos.X + dx;
                    int y = gridPos.Y + dy;
                    if (!_grid.InBounds(x, y)) continue;

                    float dist = new Vector2(dx, dy).Length();
                    if (dist > cellRadius) continue;

                    // Random chance to affect each cell — not everything gets destroyed
                    var rng = new RandomNumberGenerator();
                    if (rng.Randf() > 0.15f * radiusMult) continue;

                    var cellType = _grid.GetCell(x, y);

                    // Destroy walls — open new corridors
                    if (cellType == VineCellType.Wall)
                    {
                        _grid.ClearCell(x, y);
                        destroyed++;
                    }

                    // Damage player nodes caught in the crossfire
                    var node = _grid.GetNode(x, y);
                    if (node != null && !node.IsDestroyed)
                    {
                        node.TakeDamage(ChaosTerrainDamage * 10f);
                    }
                }
            }

            if (destroyed > 0)
            {
                GD.Print($"[Ascendant] {AscendantName} chaos: destroyed {destroyed} walls");
                GameEvents.OnTerrainChanged?.Invoke(gridPos);
            }
        }

        private void BuildVisual()
        {
            // Giant procedural mesh — placeholder until real Ascendant models exist
            _mesh = new MeshInstance3D();
            var capsule = new CapsuleMesh();
            capsule.Height = 3f * _modelScale;
            capsule.Radius = 0.8f * _modelScale;
            _mesh.Mesh = capsule;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = _color * 0.3f;
            mat.EmissionEnabled = true;
            mat.Emission = _color;
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _mesh.MaterialOverride = mat;

            AddChild(_mesh);

            // Health bar above
            _healthBar = new MeshInstance3D();
            var barMesh = new QuadMesh();
            barMesh.Size = new Vector2(3f * _modelScale, 0.3f);
            _healthBar.Mesh = barMesh;
            _healthBar.Position = new Vector3(0, 3.5f * _modelScale, 0);

            var barMat = new StandardMaterial3D();
            barMat.AlbedoColor = IsEnemy ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.2f, 0.8f, 0.4f);
            barMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            barMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            _healthBar.MaterialOverride = barMat;

            AddChild(_healthBar);

            // Point light — these things glow
            var light = new OmniLight3D();
            light.LightColor = _color;
            light.LightEnergy = 3f;
            light.OmniRange = ChaosRadius;
            light.ShadowEnabled = false;
            AddChild(light);
        }

        private void UpdateHealthBar()
        {
            if (_healthBar?.Mesh is QuadMesh quad)
            {
                float pct = CurrentHP / MaxHP;
                quad.Size = new Vector2(3f * _modelScale * pct, 0.3f);
            }
        }
    }

    /// <summary>
    /// Parsed Ascendant profile from JSON.
    /// </summary>
    public class AscendantProfile
    {
        public string Id;
        public string Name;
        public string Faction;
        public string Motivation;
        public string CombatStyle;
        public float HP;
        public float Damage;
        public float Speed;
        public float AttackRange;
        public float AttackInterval;
        public float ChaosRadius;
        public int ChaosTerrainDamage;
        public float ModelScale;
        public float ColorR, ColorG, ColorB;
        public int[] PlanetAffinity;
    }
}
