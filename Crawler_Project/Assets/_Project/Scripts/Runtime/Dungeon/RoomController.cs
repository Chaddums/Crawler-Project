using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class RoomController : MonoBehaviour
    {
        [Header("Room Configuration")]
        [SerializeField] private RoomType _roomType = RoomType.Combat;
        [SerializeField] private Transform[] _enemySpawnPoints;
        [SerializeField] private Transform[] _lootSpawnPoints;
        [SerializeField] private DoorController[] _doors;
        [SerializeField] private Collider _roomBounds;

        [Header("Spawning")]
        [Tooltip("Optional pre-placed EnemySpawner. If absent, enemies are instantiated directly.")]
        [SerializeField] private EnemySpawner _enemySpawner;
        [SerializeField] private GameObject _enemyPrefab;

        public RoomType RoomType => _roomType;
        public bool IsCleared { get; private set; }
        public Collider RoomBounds => _roomBounds;

        private FloorData _floorData;
        private readonly List<EnemyController> _spawnedEnemies = new();
        private int _enemiesKilled;
        private bool _playerInside;
        private FormationCoordinator _formation;

        /// <summary>
        /// One-time setup called by FloorManager after generation.
        /// </summary>
        public void Initialize(FloorData floorData)
        {
            _floorData = floorData;

            // Non-combat rooms start already cleared
            if (_roomType != RoomType.Combat && _roomType != RoomType.Boss)
            {
                IsCleared = true;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_playerInside) return;

            if (other.CompareTag(Constants.TAG_PLAYER))
            {
                _playerInside = true;
                OnPlayerEnter();
            }
        }

        private void OnPlayerEnter()
        {
            GameEvents.OnRoomEntered?.Invoke(gameObject);

            if (IsCleared) return;

            if (_roomType == RoomType.Combat || _roomType == RoomType.Boss)
            {
                LockDoors();
                SpawnEnemies();
                SetupFormation();
            }
        }

        private void LockDoors()
        {
            if (_doors == null) return;

            foreach (var door in _doors)
            {
                if (door != null)
                    door.Lock();
            }
        }

        private void UnlockDoors()
        {
            if (_doors == null) return;

            foreach (var door in _doors)
            {
                if (door != null)
                    door.Unlock();
            }
        }

        private void SpawnEnemies()
        {
            if (_floorData == null || _floorData.EnemyPool == null || _floorData.EnemyPool.Count == 0)
            {
                Debug.LogWarning($"[RoomController] No enemy pool available for room {gameObject.name}.");
                MarkCleared();
                return;
            }

            // If a dedicated EnemySpawner is attached, configure and delegate to it
            if (_enemySpawner != null)
            {
                SpawnViaSpawner();
                return;
            }

            // Direct spawn fallback
            SpawnEnemiesDirect();
        }

        private void SpawnViaSpawner()
        {
            _enemySpawner.SetEnemyPool(_floorData.EnemyPool.ToArray());

            int minEnemies = 1;
            int maxEnemies = 3;
            ResolveEnemyCounts(ref minEnemies, ref maxEnemies);

            int enemyCount = Random.Range(minEnemies, maxEnemies + 1);

            _enemySpawner.Configure(enemyCount, 1, _enemySpawnPoints);
            _enemySpawner.OnAllWavesCompleted += MarkCleared;

            var enemies = _enemySpawner.SpawnWave();
            _spawnedEnemies.AddRange(enemies);

            if (_spawnedEnemies.Count == 0)
            {
                Debug.LogWarning("[RoomController] EnemySpawner produced no enemies. Marking room cleared.");
                MarkCleared();
            }
        }

        private void SpawnEnemiesDirect()
        {
            if (_enemySpawnPoints == null || _enemySpawnPoints.Length == 0)
            {
                Debug.LogWarning($"[RoomController] No spawn points in room {gameObject.name}.");
                MarkCleared();
                return;
            }

            if (_enemyPrefab == null)
            {
                Debug.LogWarning($"[RoomController] No enemy prefab assigned to room {gameObject.name}.");
                MarkCleared();
                return;
            }

            int minEnemies = 1;
            int maxEnemies = Mathf.Min(3, _enemySpawnPoints.Length);
            ResolveEnemyCounts(ref minEnemies, ref maxEnemies);

            int enemyCount = Random.Range(minEnemies, maxEnemies + 1);

            for (int i = 0; i < enemyCount; i++)
            {
                Transform spawnPoint = _enemySpawnPoints[i % _enemySpawnPoints.Length];
                EnemyData data = _floorData.EnemyPool[Random.Range(0, _floorData.EnemyPool.Count)];

                GameObject instance = Instantiate(_enemyPrefab, spawnPoint.position, spawnPoint.rotation, transform);
                var controller = instance.GetComponent<EnemyController>();
                if (controller != null)
                {
                    float difficulty = _floorData != null ? _floorData.DifficultyMultiplier : 1f;
                    controller.Initialize(data, difficulty);
                    _spawnedEnemies.Add(controller);
                }
                else
                {
                    Debug.LogWarning("[RoomController] Enemy prefab is missing EnemyController component.");
                    Destroy(instance);
                }
            }

            if (_spawnedEnemies.Count == 0)
            {
                Debug.LogWarning("[RoomController] No enemies were spawned. Marking room cleared.");
                MarkCleared();
            }
        }

        private void SetupFormation()
        {
            if (_spawnedEnemies.Count < 2) return;

            _formation = gameObject.AddComponent<FormationCoordinator>();
            foreach (var enemy in _spawnedEnemies)
            {
                var ai = enemy.AI;
                if (ai == null) continue;

                float attackRange = enemy.RuntimeStats.GetStat(StatType.Dexterity) > enemy.RuntimeStats.GetStat(StatType.Strength)
                    ? Constants.DEFAULT_ATTACK_RANGE * 2.5f
                    : Constants.DEFAULT_ATTACK_RANGE;

                _formation.RegisterEnemy(ai, attackRange);
            }
        }

        /// <summary>
        /// Resolves min/max enemy counts from the matching RoomTemplate in FloorData.
        /// </summary>
        private void ResolveEnemyCounts(ref int minEnemies, ref int maxEnemies)
        {
            if (_floorData.RoomTemplates == null) return;

            foreach (var template in _floorData.RoomTemplates)
            {
                if (template.Type == _roomType)
                {
                    minEnemies = template.MinEnemies;
                    maxEnemies = _enemySpawnPoints != null && _enemySpawnPoints.Length > 0
                        ? Mathf.Min(template.MaxEnemies, _enemySpawnPoints.Length)
                        : template.MaxEnemies;
                    break;
                }
            }
        }

        private void HandleEnemyKilled(GameObject enemyObject)
        {
            if (IsCleared) return;

            var controller = enemyObject.GetComponent<EnemyController>();
            if (controller == null || !_spawnedEnemies.Contains(controller)) return;

            _enemiesKilled++;

            if (_enemiesKilled >= _spawnedEnemies.Count)
            {
                MarkCleared();
            }
        }

        private void MarkCleared()
        {
            if (IsCleared) return;

            IsCleared = true;
            UnlockDoors();
            SpawnLoot();
            GameEvents.OnRoomCleared?.Invoke(gameObject);
            Debug.Log($"[RoomController] Room {gameObject.name} cleared.");
        }

        private void SpawnLoot()
        {
            if (_lootSpawnPoints == null || _lootSpawnPoints.Length == 0) return;
            if (_floorData == null) return;
            if (_floorData.FloorLootTables == null || _floorData.FloorLootTables.Count == 0) return;

            // Pick a random loot table from the floor pool and a spawn point
            Transform spawnPoint = _lootSpawnPoints[Random.Range(0, _lootSpawnPoints.Length)];
            LootTableData table = _floorData.FloorLootTables[Random.Range(0, _floorData.FloorLootTables.Count)];

            Debug.Log($"[RoomController] Spawning loot from table '{table.name}' at {spawnPoint.position}.");

            // Delegate to LootDropper if one exists on this room, otherwise log for manual wiring
            var dropper = GetComponentInChildren<LootDropper>();
            if (dropper != null)
            {
                dropper.DropLoot(table, spawnPoint.position);
            }
            else
            {
                Debug.Log("[RoomController] No LootDropper component found. Loot spawning deferred.");
            }
        }
    }
}
