using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Terrain mutation data — defines what cell changes at which wave milestone.
    /// Loaded from level JSON "terrainMutations" array.
    /// </summary>
    public class TerrainMutation
    {
        public int TriggerWave;           // Wave number that triggers this mutation
        public Vector2I Cell;             // Grid position to mutate
        public VineCellType NewType;      // What the cell becomes
        public HazardType HazardType;     // If NewType is Hazard, which kind
    }

    /// <summary>
    /// Map expansion zone — a rectangular region that starts as Wall and reveals
    /// at a wave milestone. The map literally grows. Each zone can contain
    /// pre-placed terrain features that activate when the zone opens.
    /// </summary>
    public class ExpansionZone
    {
        public int TriggerWave;           // Wave that reveals this zone
        public int X1, Y1, X2, Y2;       // Rectangular bounds
        public string Label;              // Announcement text (e.g., "NORTHERN SECTOR OPENING")
        public List<TerrainMutation> Features = new(); // Terrain placed inside zone on reveal
    }

    /// <summary>
    /// Applies terrain mutations and expansion zones at wave milestones.
    /// Walls collapse, pits open, hazards activate mid-run.
    /// Expansion zones reveal whole new sections of the map.
    /// Makes the map feel alive and forces reactive building.
    /// </summary>
    public partial class TerrainMutationManager : Node
    {
        private VineGrid _grid;
        private readonly List<TerrainMutation> _mutations = new();
        private readonly List<ExpansionZone> _expansionZones = new();
        private readonly HashSet<int> _appliedMutations = new();
        private readonly HashSet<int> _revealedZones = new();

        public override void _Ready()
        {
            ServiceLocator.TryGet<VineGrid>(out _grid);
            GameEvents.OnWaveCompleted += OnWaveCompleted;
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveCompleted -= OnWaveCompleted;
        }

        /// <summary>Load mutations from level data. Called after map build.</summary>
        public void LoadMutations(List<TerrainMutation> mutations)
        {
            _mutations.Clear();
            _appliedMutations.Clear();
            _mutations.AddRange(mutations);
            GD.Print($"[TerrainMutation] Loaded {_mutations.Count} terrain mutations");
        }

        /// <summary>Load expansion zones from level data.</summary>
        public void LoadExpansionZones(List<ExpansionZone> zones)
        {
            _expansionZones.Clear();
            _revealedZones.Clear();
            _expansionZones.AddRange(zones);
            GD.Print($"[TerrainMutation] Loaded {_expansionZones.Count} expansion zones");
        }

        /// <summary>
        /// Fill an expansion zone with walls at map init.
        /// Call this during map build for each zone.
        /// </summary>
        public void SealExpansionZone(ExpansionZone zone)
        {
            if (_grid == null) return;
            for (int x = zone.X1; x <= zone.X2; x++)
                for (int y = zone.Y1; y <= zone.Y2; y++)
                    if (_grid.InBounds(x, y) && _grid.GetCell(x, y) == VineCellType.Empty)
                        _grid.SetWall(x, y);
        }

        private void OnWaveCompleted(int waveNumber)
        {
            if (_grid == null) return;

            // Expansion zones first — reveal whole sections
            for (int z = 0; z < _expansionZones.Count; z++)
            {
                if (_revealedZones.Contains(z)) continue;
                var zone = _expansionZones[z];
                if (waveNumber < zone.TriggerWave) continue;

                _revealedZones.Add(z);
                RevealExpansionZone(zone, waveNumber);
            }

            // Individual mutations
            for (int i = 0; i < _mutations.Count; i++)
            {
                if (_appliedMutations.Contains(i)) continue;
                var m = _mutations[i];
                if (waveNumber < m.TriggerWave) continue;

                _appliedMutations.Add(i);

                // Apply the mutation
                if (m.NewType == VineCellType.Hazard)
                {
                    _grid.SetHazardCell(m.Cell.X, m.Cell.Y, m.HazardType);
                    // Also fire the general mutation event
                    GameEvents.OnTerrainMutated?.Invoke(m.Cell, m.NewType);
                    GameEvents.OnVinePathRecalculated?.Invoke();
                }
                else
                {
                    _grid.MutateCell(m.Cell, m.NewType);
                }

                // VFX: dust/debris at mutation site
                var worldPos = _grid.GridToWorld(m.Cell);
                VfxFactory.SpawnDeathBurst(
                    GetTree(), worldPos + Vector3.Up * 0.5f,
                    new Color(0.6f, 0.5f, 0.4f), 4);

                // Screen shake for dramatic mutations (walls collapsing, pits opening)
                if (m.NewType == VineCellType.Empty || m.NewType == VineCellType.Pit
                    || m.NewType == VineCellType.Hazard)
                {
                    // TDCamera shake if available
                    var cameras = GetTree().GetNodesInGroup("Camera");
                    foreach (var cam in cameras)
                    {
                        if (cam is TDCamera tdCam)
                        {
                            tdCam.Shake(0.15f, 0.3f);
                            break;
                        }
                    }
                }

                GD.Print($"[TerrainMutation] Wave {waveNumber}: ({m.Cell.X},{m.Cell.Y}) → {m.NewType}");
            }
        }

        private void RevealExpansionZone(ExpansionZone zone, int waveNumber)
        {
            int cleared = 0;
            for (int x = zone.X1; x <= zone.X2; x++)
            {
                for (int y = zone.Y1; y <= zone.Y2; y++)
                {
                    if (!_grid.InBounds(x, y)) continue;
                    var cell = _grid.GetCell(x, y);
                    // Only clear walls that we sealed — don't nuke entry/exit/etc
                    if (cell == VineCellType.Wall)
                    {
                        _grid.ClearCell(x, y);
                        cleared++;
                    }
                }
            }

            // Place any features defined inside the zone (hazards, resource nodes, etc.)
            foreach (var feature in zone.Features)
            {
                if (feature.NewType == VineCellType.Hazard)
                    _grid.SetHazardCell(feature.Cell.X, feature.Cell.Y, feature.HazardType);
                else if (feature.NewType == VineCellType.Pit)
                    _grid.SetPit(feature.Cell.X, feature.Cell.Y);
                else if (feature.NewType == VineCellType.ResourceNode)
                    _grid.SetResourceNodeCell(feature.Cell.X, feature.Cell.Y);
                else if (feature.NewType == VineCellType.Elevated)
                    _grid.SetElevated(feature.Cell.X, feature.Cell.Y);
                else if (feature.NewType == VineCellType.DataStream)
                    _grid.SetDataStream(feature.Cell.X, feature.Cell.Y);
                else if (feature.NewType == VineCellType.DestructibleWall)
                {
                    _grid.SetDestructibleWall(feature.Cell.X, feature.Cell.Y);
                    _grid.SetDestructibleWallVisual(feature.Cell.X, feature.Cell.Y);
                }
            }

            // VFX cascade — burst along the zone perimeter
            for (int x = zone.X1; x <= zone.X2; x += 3)
            {
                var topPos = _grid.GridToWorld(new Vector2I(x, zone.Y1));
                var botPos = _grid.GridToWorld(new Vector2I(x, zone.Y2));
                VfxFactory.SpawnDeathBurst(GetTree(), topPos + Vector3.Up * 0.3f, new Color(0.7f, 0.6f, 0.4f), 3);
                VfxFactory.SpawnDeathBurst(GetTree(), botPos + Vector3.Up * 0.3f, new Color(0.7f, 0.6f, 0.4f), 3);
            }

            // Screen shake — proportional to zone size
            float shakeIntensity = Mathf.Clamp(cleared * 0.005f, 0.1f, 0.4f);
            var cameras = GetTree().GetNodesInGroup("Camera");
            foreach (var cam in cameras)
            {
                if (cam is TDCamera tdCam)
                {
                    tdCam.Shake(shakeIntensity, 0.5f);
                    break;
                }
            }

            // Repath
            _grid.RebuildTerrainMesh();
            GameEvents.OnVinePathRecalculated?.Invoke();

            // Announce
            string label = zone.Label ?? "NEW SECTOR REVEALED";
            GameEvents.OnAnnouncement?.Invoke(label);
            GD.Print($"[TerrainMutation] Wave {waveNumber}: EXPANSION — {label} ({cleared} cells revealed)");
        }
    }
}
