using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Spawns enemies for Vine Logic TD waves.
    /// </summary>
    public partial class VineWaveManager : Node
    {
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private int _currentWave;
        private bool _waveActive;
        private int _enemiesAlive;

        // Active spawn groups
        private readonly List<ActiveSpawnGroup> _activeGroups = new();

        public int CurrentWave => _currentWave;
        public bool WaveActive => _waveActive;

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<VineGrid>();
            _pathfinder = ServiceLocator.Get<VinePathfinder>();

            GameEvents.OnEnemyKilled += OnEnemyDied;
            GameEvents.OnEnemyLeaked += OnEnemyLeaked;

            ServiceLocator.Register(this);
        }

        public void StartWave()
        {
            _currentWave++;
            var data = VineWaveRegistry.Get(_currentWave);
            if (data == null)
            {
                GameManager.Instance?.SetPhase(GamePhase.Victory);
                GameEvents.OnAllWavesCleared?.Invoke(_currentWave);
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
                GameManager.Instance.CurrentWave = _currentWave;
            GameManager.Instance?.SetPhase(GamePhase.Wave);
            GameEvents.OnWaveStarted?.Invoke(_currentWave);
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
                    group.Timer = group.Data.SpawnInterval;
                }

                _activeGroups[i] = group;
            }

            // Wave complete when all spawned and all dead
            if (!anyGroupsLeft && _enemiesAlive <= 0)
            {
                _waveActive = false;
                var data = VineWaveRegistry.Get(_currentWave);

                // Award bonus gold
                if (data != null)
                    GameEvents.OnScrapCollected?.Invoke(data.BonusGold);

                GameManager.Instance?.SetPhase(GamePhase.WaveComplete);
                GameEvents.OnWaveCompleted?.Invoke(_currentWave);

                // Auto-transition to build phase after short delay
                GetTree().CreateTimer(1.5f).Timeout += () =>
                    GameManager.Instance?.SetPhase(GamePhase.Build);
            }
        }

        private void SpawnEnemy(VineSpawnGroup group)
        {
            // Pick entry point
            int entryIdx = group.EntryIndex;
            if (entryIdx < 0 || entryIdx >= _grid.EntryPoints.Count)
                entryIdx = GD.RandRange(0, _grid.EntryPoints.Count - 1);

            var entry = _grid.EntryPoints[entryIdx];
            var path = _pathfinder.GetCachedPath(entry);
            if (path == null || path.Count == 0) return;

            var enemy = new VineEnemy();
            GetTree().Root.AddChild(enemy);
            enemy.Initialize(group.EnemyName, group.Faction, group.Health, group.Speed,
                group.ScrapValue, group.Color, entry);

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
