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
    /// Applies terrain mutations at wave milestones.
    /// Walls collapse, pits open, hazards activate mid-run.
    /// Makes the map feel alive and forces reactive building.
    /// </summary>
    public partial class TerrainMutationManager : Node
    {
        private VineGrid _grid;
        private readonly List<TerrainMutation> _mutations = new();
        private readonly HashSet<int> _appliedIndices = new();

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
            _appliedIndices.Clear();
            _mutations.AddRange(mutations);
            GD.Print($"[TerrainMutation] Loaded {_mutations.Count} terrain mutations");
        }

        private void OnWaveCompleted(int waveNumber)
        {
            if (_grid == null) return;

            for (int i = 0; i < _mutations.Count; i++)
            {
                if (_appliedIndices.Contains(i)) continue;
                var m = _mutations[i];
                if (waveNumber < m.TriggerWave) continue;

                _appliedIndices.Add(i);

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
    }
}
