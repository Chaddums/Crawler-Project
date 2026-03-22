using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages shield walls that gate entry regions on map edges.
    /// Tracks game time for TimeMilestone triggers. Any system can call
    /// BreakWall() directly for WorldObject, UIPrompt, or Scripted triggers.
    /// </summary>
    public partial class ShieldWallManager : Node
    {
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private readonly Dictionary<CardinalDirection, ShieldWallState> _walls = new();
        private float _elapsedTime;

        public float ElapsedTime => _elapsedTime;

        public override void _Ready()
        {
            ServiceLocator.Register(this);
        }

        /// <summary>
        /// Set up shield walls from config. Call after grid and pathfinder are ready.
        /// </summary>
        public void Initialize(VineGrid grid, VinePathfinder pathfinder, List<ShieldWallConfig> configs)
        {
            _grid = grid;
            _pathfinder = pathfinder;
            _elapsedTime = 0f;

            if (configs == null || configs.Count == 0) return;

            foreach (var cfg in configs)
            {
                // Deactivate the gated entry region
                if (cfg.EntryRegionIndex >= 0 && cfg.EntryRegionIndex < grid.EntryRegions.Count)
                {
                    grid.DeactivateEntryRegion(cfg.EntryRegionIndex);

                    // Set direction on the region from config
                    grid.EntryRegions[cfg.EntryRegionIndex].Direction = cfg.Direction;
                }

                // Create visual shield wall entity
                var wall = new ShieldWall();
                AddChild(wall);
                wall.Initialize(cfg, grid);

                _walls[cfg.Direction] = new ShieldWallState
                {
                    Config = cfg,
                    Visual = wall,
                    Destroyed = false
                };

                GD.Print($"[ShieldWallManager] Wall created: {cfg.Direction} — HP={cfg.HP}, trigger={cfg.TriggerType} at {cfg.TriggerTime}s, entry region #{cfg.EntryRegionIndex}");
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_walls.Count == 0) return;

            _elapsedTime += (float)delta;

            // Check time-based milestone triggers
            foreach (var kvp in _walls)
            {
                var state = kvp.Value;
                if (state.Destroyed) continue;
                if (state.Config.TriggerType != ShieldWallTriggerType.TimeMilestone) continue;

                if (_elapsedTime >= state.Config.TriggerTime)
                {
                    BreakWall(kvp.Key);
                }
            }
        }

        /// <summary>
        /// Public API — any system can call this to break a wall.
        /// Used by TimeMilestone (auto), WorldObject, UIPrompt, Scripted, or Manual triggers.
        /// </summary>
        public void BreakWall(CardinalDirection direction)
        {
            if (!_walls.TryGetValue(direction, out var state)) return;
            if (state.Destroyed) return;

            state.Destroyed = true;

            GD.Print($"[ShieldWallManager] Wall BREACHED: {direction}");

            // Collapse visual
            state.Visual?.Collapse();

            // Activate the gated entry region
            _grid.ActivateEntryRegion(state.Config.EntryRegionIndex);

            // Build entry visuals for the newly opened region
            if (state.Config.EntryRegionIndex >= 0 && state.Config.EntryRegionIndex < _grid.EntryRegions.Count)
            {
                var region = _grid.EntryRegions[state.Config.EntryRegionIndex];
                BuildNewEntryVisuals(region);
            }

            // Fire events
            GameEvents.OnShieldWallDestroyed?.Invoke(direction);
            GameEvents.OnAnnouncement?.Invoke($"Shield wall breached — {direction} entry open");
        }

        /// <summary>
        /// Damage a wall (for future enemy siege or player interaction).
        /// </summary>
        public void DamageWall(CardinalDirection direction, float amount)
        {
            if (!_walls.TryGetValue(direction, out var state)) return;
            if (state.Destroyed) return;

            state.Visual?.TakeDamage(amount);

            float currentHP = state.Visual?.CurrentHP ?? 0;
            float maxHP = state.Config.HP;
            GameEvents.OnShieldWallDamaged?.Invoke(direction, currentHP, maxHP);

            if (currentHP <= 0)
                BreakWall(direction);
        }

        // ── Queries ──

        public bool IsWallActive(CardinalDirection direction)
        {
            return _walls.TryGetValue(direction, out var state) && !state.Destroyed;
        }

        public bool IsWallDestroyed(CardinalDirection direction)
        {
            return _walls.TryGetValue(direction, out var state) && state.Destroyed;
        }

        public float GetWallHP(CardinalDirection direction)
        {
            if (!_walls.TryGetValue(direction, out var state) || state.Destroyed) return 0f;
            return state.Visual?.CurrentHP ?? 0f;
        }

        public float GetWallMaxHP(CardinalDirection direction)
        {
            if (!_walls.TryGetValue(direction, out var state)) return 0f;
            return state.Config.HP;
        }

        /// <summary>
        /// Time remaining until a TimeMilestone wall breaks. -1 if not time-triggered or already broken.
        /// </summary>
        public float GetTimeRemaining(CardinalDirection direction)
        {
            if (!_walls.TryGetValue(direction, out var state)) return -1f;
            if (state.Destroyed) return 0f;
            if (state.Config.TriggerType != ShieldWallTriggerType.TimeMilestone) return -1f;
            return Mathf.Max(0f, state.Config.TriggerTime - _elapsedTime);
        }

        public IEnumerable<CardinalDirection> GetAllWallDirections() => _walls.Keys;

        /// <summary>
        /// Build entry glow markers for a newly activated region (same style as VineMapLayouts).
        /// </summary>
        private void BuildNewEntryVisuals(VineEntryRegion region)
        {
            var entryColor = PlanetTheme.Current.EntryMarkerColor;
            foreach (var cell in region.Cells)
            {
                var glow = new MeshInstance3D();
                var glowMesh = new CylinderMesh
                {
                    TopRadius = 1.0f,
                    BottomRadius = 1.0f,
                    Height = 0.05f
                };
                glow.Mesh = glowMesh;
                glow.Position = _grid.GridToWorld(cell) + new Vector3(0, 0.03f, 0);

                var glowMat = new StandardMaterial3D();
                glowMat.AlbedoColor = new Color(entryColor.R, entryColor.G, entryColor.B, 0.25f);
                glowMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                glowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                glowMat.EmissionEnabled = true;
                glowMat.Emission = entryColor;
                glowMat.EmissionEnergyMultiplier = 0.4f;
                glow.MaterialOverride = glowMat;
                _grid.AddChild(glow);
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<ShieldWallManager>();
        }

        private class ShieldWallState
        {
            public ShieldWallConfig Config;
            public ShieldWall Visual;
            public bool Destroyed;
        }
    }
}
