using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Spawns enemies according to wave definitions.
    /// Tracks active enemies and transitions between waves.
    /// </summary>
    public partial class WaveManager : Node
    {
        private List<WaveData> _waves;
        private int _currentWaveIndex = -1;
        private bool _waveActive;
        private int _enemiesAlive;
        private int _totalWaves;

        // Spawn state per group
        private class GroupState
        {
            public SpawnGroup Group;
            public int Spawned;
            public float Timer;
            public float DelayTimer;
        }

        private readonly List<GroupState> _activeGroups = new();
        private MapGrid _grid;
        private Pathfinder _pathfinder;
        private RandomNumberGenerator _rng = new();

        public int CurrentWave => _currentWaveIndex + 1;
        public int TotalWaves => _totalWaves;
        public bool IsWaveActive => _waveActive;
        public int EnemiesAlive => _enemiesAlive;

        public override void _Ready()
        {
            _waves = WaveRegistry.BuildDefaultWaves();
            _totalWaves = _waves.Count;
            _grid = ServiceLocator.Get<MapGrid>();
            _pathfinder = ServiceLocator.Get<Pathfinder>();

            GameEvents.OnEnemyKilled += OnEnemyDied;
            GameEvents.OnEnemyLeaked += OnEnemyLeaked;

            ServiceLocator.Register(this);
        }

        public void StartNextWave()
        {
            _currentWaveIndex++;
            if (_currentWaveIndex >= _waves.Count)
            {
                GameEvents.OnAllWavesCleared?.Invoke(_currentWaveIndex);
                GameManager.Instance?.SetPhase(GamePhase.Victory);
                return;
            }

            var wave = _waves[_currentWaveIndex];
            _waveActive = true;
            _enemiesAlive = 0;
            _activeGroups.Clear();

            foreach (var group in wave.Groups)
            {
                _activeGroups.Add(new GroupState {
                    Group = group,
                    Spawned = 0,
                    Timer = 0,
                    DelayTimer = group.StartDelay
                });
            }

            GameManager.Instance?.SetPhase(GamePhase.Wave);
            GameManager.Instance.CurrentWave = CurrentWave;
            GameEvents.OnWaveStarted?.Invoke(CurrentWave);
            GD.Print($"[WaveManager] Wave {CurrentWave}: {wave.Name}");
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_waveActive) return;

            bool allDoneSpawning = true;

            foreach (var gs in _activeGroups)
            {
                if (gs.Spawned >= gs.Group.Count) continue;
                allDoneSpawning = false;

                // Wait for start delay
                if (gs.DelayTimer > 0)
                {
                    gs.DelayTimer -= (float)delta;
                    continue;
                }

                gs.Timer -= (float)delta;
                if (gs.Timer <= 0)
                {
                    SpawnEnemy(gs.Group);
                    gs.Spawned++;
                    gs.Timer = gs.Group.SpawnInterval;
                }
            }

            // Wave complete when all spawned and all dead
            if (allDoneSpawning && _enemiesAlive <= 0)
                CompleteWave();
        }

        private void SpawnEnemy(SpawnGroup group)
        {
            var data = EnemyRegistry.Get(group.EnemyType);
            if (data == null)
            {
                GD.PushError($"[WaveManager] Unknown enemy type: {group.EnemyType}");
                return;
            }

            if (_grid.SpawnPoints.Count == 0)
            {
                GD.PushError("[WaveManager] No spawn points on map!");
                return;
            }

            // Pick spawn point
            Vector2I spawn;
            if (group.SpawnPointIndex >= 0 && group.SpawnPointIndex < _grid.SpawnPoints.Count)
                spawn = _grid.SpawnPoints[group.SpawnPointIndex];
            else
                spawn = _grid.SpawnPoints[_rng.RandiRange(0, _grid.SpawnPoints.Count - 1)];

            // Get path
            var path = data.IsFlying
                ? GetFlyingPath(spawn)
                : _pathfinder.GetCachedPath(spawn);

            if (path == null)
            {
                GD.PushWarning($"[WaveManager] No path for {data.Name} from {spawn} to core {_grid.CorePosition}");
                return;
            }

            var enemy = new EnemyController();
            GetTree().CurrentScene.AddChild(enemy);
            enemy.Initialize(data, new List<Vector2I>(path));
            enemy.Tier = group.Tier;
            _enemiesAlive++;
            GD.Print($"[WaveManager] Spawned {data.Name} at {spawn}, path length={path.Count}");
        }

        /// <summary>
        /// Flying enemies go straight to the core.
        /// </summary>
        private List<Vector2I> GetFlyingPath(Vector2I spawn)
        {
            return new List<Vector2I> { spawn, _grid.CorePosition };
        }

        private void OnEnemyDied(Node enemy)
        {
            _enemiesAlive--;
        }

        private void OnEnemyLeaked(Node enemy, Vector3 pos)
        {
            _enemiesAlive--;
        }

        private void CompleteWave()
        {
            _waveActive = false;
            var wave = _waves[_currentWaveIndex];

            // Award bonus scrap
            if (ServiceLocator.TryGet<ScrapManager>(out var scrapMgr))
                scrapMgr.AddScrap(wave.BonusScrap);

            GameEvents.OnWaveCompleted?.Invoke(CurrentWave);
            GD.Print($"[WaveManager] Wave {CurrentWave} complete! Bonus: {wave.BonusScrap} scrap");

            if (_currentWaveIndex >= _waves.Count - 1)
            {
                GameEvents.OnAllWavesCleared?.Invoke(CurrentWave);
                GameManager.Instance?.SetPhase(GamePhase.Victory);
            }
            else
            {
                GameManager.Instance?.SetPhase(GamePhase.Build);
            }
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyDied;
            GameEvents.OnEnemyLeaked -= OnEnemyLeaked;
            ServiceLocator.Unregister<WaveManager>();
        }
    }
}
