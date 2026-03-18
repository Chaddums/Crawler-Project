using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Spawns enemies for Vine Logic TD waves.
    /// Reads current floor from GameManager and uses floor-based wave registry.
    /// </summary>
    public partial class VineWaveManager : Node
    {
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private int _currentFloor;
        private int _currentWaveInFloor;
        private bool _waveActive;
        private int _enemiesAlive;

        // Active spawn groups
        private readonly List<ActiveSpawnGroup> _activeGroups = new();
        private readonly RandomNumberGenerator _rng = new();

        public int CurrentWave => _currentWaveInFloor;
        public bool WaveActive => _waveActive;
        public int TotalWavesThisFloor => VineWaveRegistry.GetFloorWaveCount(_currentFloor);

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<VineGrid>();
            _pathfinder = ServiceLocator.Get<VinePathfinder>();
            _currentFloor = GameManager.Instance?.CurrentFloor ?? 1;
            _currentWaveInFloor = 0;

            GameEvents.OnEnemyKilled += OnEnemyDied;
            GameEvents.OnEnemyLeaked += OnEnemyLeaked;

            ServiceLocator.Register(this);
        }

        public void StartWave()
        {
            _currentWaveInFloor++;
            var data = VineWaveRegistry.GetFloorWave(_currentFloor, _currentWaveInFloor);
            if (data == null)
            {
                // No more waves on this floor — should not happen if floor logic is correct
                GameManager.Instance?.SetPhase(GamePhase.Victory);
                return;
            }

            _waveActive = true;
            _activeGroups.Clear();

            foreach (var group in data.Groups)
            {
                _activeGroups.Add(new ActiveSpawnGroup {
                    Data = group,
                    Remaining = group.Count,
                    Timer = group.StartDelay
                });
            }

            if (GameManager.Instance != null)
                GameManager.Instance.CurrentWave = _currentWaveInFloor;
            GameManager.Instance?.SetPhase(GamePhase.Wave);
            GameEvents.OnWaveStarted?.Invoke(_currentWaveInFloor);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_waveActive) return;

            float dt = (float)delta;
            bool anyGroupsLeft = false;

            for (int i = 0; i < _activeGroups.Count; i++)
            {
                var group = _activeGroups[i];
                if (group.Remaining <= 0) continue;

                anyGroupsLeft = true;
                group.Timer -= dt;

                if (group.Timer <= 0)
                {
                    SpawnEnemy(group.Data);
                    group.Remaining--;
                    float jitter = group.Data.SpawnJitter > 0
                        ? _rng.RandfRange(-group.Data.SpawnJitter, group.Data.SpawnJitter)
                        : 0f;
                    group.Timer = group.Data.SpawnInterval + jitter;
                }

                _activeGroups[i] = group;
            }

            // Wave complete when all spawned and all dead
            if (!anyGroupsLeft && _enemiesAlive <= 0)
            {
                _waveActive = false;
                var data = VineWaveRegistry.GetFloorWave(_currentFloor, _currentWaveInFloor);

                // Award bonus gold
                if (data != null)
                    GameEvents.OnScrapCollected?.Invoke(data.BonusGold);

                GameEvents.OnWaveCompleted?.Invoke(_currentWaveInFloor);

                // Check if this was the last wave of the floor
                int totalWaves = TotalWavesThisFloor;
                if (_currentWaveInFloor >= totalWaves)
                {
                    // Floor complete
                    if (_currentFloor < Constants.VINE_FLOOR_COUNT)
                    {
                        // More floors to go — show perk select
                        GameManager.Instance?.SetPhase(GamePhase.FloorComplete);
                        GameEvents.OnFloorCompleted?.Invoke(_currentFloor);

                        // Delay then transition to meta perk / perk screen
                        GetTree().CreateTimer(2.0f).Timeout += () =>
                            GameManager.Instance?.ShowMetaPerkOrPerkSelect();
                    }
                    else
                    {
                        // Final floor complete — victory!
                        GameManager.Instance?.SetPhase(GamePhase.Victory);
                        GameEvents.OnAllWavesCleared?.Invoke(_currentWaveInFloor);
                    }
                }
                else
                {
                    // More waves on this floor
                    GameManager.Instance?.SetPhase(GamePhase.WaveComplete);
                    GetTree().CreateTimer(1.5f).Timeout += () =>
                        GameManager.Instance?.SetPhase(GamePhase.Build);
                }
            }
        }

        private void SpawnEnemy(VineSpawnGroup group)
        {
            // Pick entry region
            int entryIdx = group.EntryIndex;
            var regions = _grid.EntryRegions;

            if (entryIdx < 0 || entryIdx >= regions.Count)
                entryIdx = _rng.RandiRange(0, regions.Count - 1);

            var region = regions[entryIdx];

            // Pick random cell within the region for chaotic spawning
            var spawnCell = region.GetRandomSpawnCell(_rng);

            // Try to find path from this specific cell
            var path = _pathfinder.FindPathFromPosition(spawnCell);
            if (path == null || path.Count == 0)
            {
                // Fallback to cached path from region center
                path = _pathfinder.GetCachedPath(region.Center);
                spawnCell = region.Center;
            }
            if (path == null || path.Count == 0) return;

            var enemy = new VineEnemy();
            GetTree().Root.AddChild(enemy);
            enemy.Initialize(group.EnemyName, group.Faction, group.Health, group.Speed,
                group.ScrapValue, group.Color, spawnCell, group.IsBoss);

            _enemiesAlive++;
        }

        private void OnEnemyDied(Node enemy)
        {
            if (enemy is VineEnemy)
                _enemiesAlive = Mathf.Max(0, _enemiesAlive - 1);
        }

        private void OnEnemyLeaked(Node enemy, Vector3 pos)
        {
            if (enemy is VineEnemy)
                _enemiesAlive = Mathf.Max(0, _enemiesAlive - 1);
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyDied;
            GameEvents.OnEnemyLeaked -= OnEnemyLeaked;
            ServiceLocator.Unregister<VineWaveManager>();
        }

        private struct ActiveSpawnGroup
        {
            public VineSpawnGroup Data;
            public int Remaining;
            public float Timer;
        }
    }
}
