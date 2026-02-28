using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Enemy Pool")]
        [SerializeField] private EnemyData[] _enemyPool;
        [SerializeField] private GameObject _enemyPrefab;

        [Header("Spawn Configuration")]
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private int _enemiesPerWave = 5;
        [SerializeField] private int _totalWaves = 3;

        [Header("Timing")]
        [SerializeField] private float _delayBetweenSpawns = 0.3f;
        [SerializeField] private float _delayBetweenWaves = 5f;

        private int _currentWave;
        private int _enemiesAliveThisWave;
        private List<EnemyController> _spawnedEnemies = new();
        private bool _isSpawning;

        public int CurrentWave => _currentWave;
        public int TotalWaves => _totalWaves;
        public int EnemiesAlive => _enemiesAliveThisWave;
        public bool AllWavesComplete => _currentWave >= _totalWaves && _enemiesAliveThisWave <= 0;
        public IReadOnlyList<EnemyController> SpawnedEnemies => _spawnedEnemies;

        public event Action<int> OnWaveStarted;
        public event Action<int> OnWaveCompleted;
        public event Action OnAllWavesCompleted;

        /// <summary>
        /// Override the enemy pool at runtime, e.g. from FloorData.
        /// </summary>
        public void SetEnemyPool(EnemyData[] pool)
        {
            _enemyPool = pool;
        }

        /// <summary>
        /// Configure wave settings at runtime.
        /// </summary>
        public void Configure(int enemiesPerWave, int totalWaves, Transform[] spawnPoints = null)
        {
            _enemiesPerWave = enemiesPerWave;
            _totalWaves = totalWaves;
            if (spawnPoints != null && spawnPoints.Length > 0)
                _spawnPoints = spawnPoints;
        }

        /// <summary>
        /// Begin the spawning sequence from wave 1.
        /// </summary>
        public void StartSpawning()
        {
            _currentWave = 0;
            _spawnedEnemies.Clear();
            SpawnNextWave();
        }

        /// <summary>
        /// Spawn the next wave. Can also be called externally to manually trigger waves.
        /// </summary>
        public void SpawnNextWave()
        {
            if (_currentWave >= _totalWaves) return;
            if (_isSpawning) return;

            _currentWave++;
            StartCoroutine(SpawnWaveRoutine(_currentWave));
        }

        /// <summary>
        /// Spawn a single wave immediately (no coroutine delay) and return the spawned controllers.
        /// </summary>
        public List<EnemyController> SpawnWave()
        {
            if (_enemyPool == null || _enemyPool.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No enemy pool configured.");
                return new List<EnemyController>();
            }

            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No spawn points assigned.");
                return new List<EnemyController>();
            }

            _currentWave++;
            var waveEnemies = new List<EnemyController>();

            OnWaveStarted?.Invoke(_currentWave);

            for (int i = 0; i < _enemiesPerWave; i++)
            {
                var enemy = SpawnSingleEnemy(i);
                if (enemy != null)
                    waveEnemies.Add(enemy);
            }

            _enemiesAliveThisWave = waveEnemies.Count;

            return waveEnemies;
        }

        private System.Collections.IEnumerator SpawnWaveRoutine(int waveNumber)
        {
            _isSpawning = true;

            OnWaveStarted?.Invoke(waveNumber);
            Debug.Log($"[EnemySpawner] Wave {waveNumber}/{_totalWaves} starting.");

            var waveEnemies = new List<EnemyController>();

            for (int i = 0; i < _enemiesPerWave; i++)
            {
                var enemy = SpawnSingleEnemy(i);
                if (enemy != null)
                    waveEnemies.Add(enemy);

                if (_delayBetweenSpawns > 0f)
                    yield return new WaitForSeconds(_delayBetweenSpawns);
            }

            _enemiesAliveThisWave = waveEnemies.Count;
            _isSpawning = false;
        }

        private EnemyController SpawnSingleEnemy(int index)
        {
            if (_enemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] Enemy prefab is not assigned.");
                return null;
            }

            // Pick a random enemy from the pool
            EnemyData data = _enemyPool[UnityEngine.Random.Range(0, _enemyPool.Length)];

            // Pick a spawn point (cycle through available points)
            Transform spawnPoint = _spawnPoints[index % _spawnPoints.Length];
            Vector3 position = spawnPoint.position;

            // Add slight random offset to prevent stacking
            position += new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                0f,
                UnityEngine.Random.Range(-1f, 1f)
            );

            GameObject enemyObj = Instantiate(_enemyPrefab, position, Quaternion.identity, transform);
            var controller = enemyObj.GetComponent<EnemyController>();

            if (controller == null)
            {
                Debug.LogError("[EnemySpawner] Enemy prefab is missing EnemyController component.");
                Destroy(enemyObj);
                return null;
            }

            controller.Initialize(data);
            _spawnedEnemies.Add(controller);

            // Listen for death to track wave completion
            var health = controller.Health;
            if (health != null)
            {
                health.OnDeath += () => OnSpawnedEnemyDied(controller);
            }

            return controller;
        }

        private void OnSpawnedEnemyDied(EnemyController enemy)
        {
            _enemiesAliveThisWave--;

            if (_enemiesAliveThisWave <= 0)
            {
                OnWaveCompleted?.Invoke(_currentWave);
                Debug.Log($"[EnemySpawner] Wave {_currentWave}/{_totalWaves} completed.");

                if (_currentWave >= _totalWaves)
                {
                    OnAllWavesCompleted?.Invoke();
                    Debug.Log("[EnemySpawner] All waves completed.");
                }
                else
                {
                    // Auto-start next wave after delay
                    if (_delayBetweenWaves > 0f)
                        StartCoroutine(DelayedNextWave());
                    else
                        SpawnNextWave();
                }
            }
        }

        private System.Collections.IEnumerator DelayedNextWave()
        {
            yield return new WaitForSeconds(_delayBetweenWaves);
            SpawnNextWave();
        }

        /// <summary>
        /// Destroy all currently spawned enemies.
        /// </summary>
        public void ClearAllEnemies()
        {
            foreach (var enemy in _spawnedEnemies)
            {
                if (enemy != null)
                    Destroy(enemy.gameObject);
            }

            _spawnedEnemies.Clear();
            _enemiesAliveThisWave = 0;
        }
    }
}
