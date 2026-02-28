using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Manages a single room: enemy spawning, kill tracking, room-enter trigger.
    /// </summary>
    public partial class RoomController : Node3D
    {
        public RoomType RoomType { get; set; } = RoomType.Combat;
        public Vector2I GridPosition { get; set; }
        public bool IsCleared { get; private set; }
        public bool IsEntered { get; private set; }

        private int _totalEnemies;
        private int _killedEnemies;
        private Area3D _enterTrigger;
        private readonly List<EnemyController> _enemies = new();
        private FloorData _floorData;

        public void Initialize(FloorData floorData)
        {
            _floorData = floorData;
        }

        public override void _Ready()
        {
            // Create room-enter trigger area
            _enterTrigger = new Area3D();
            _enterTrigger.CollisionLayer = 0;
            _enterTrigger.CollisionMask = Constants.MASK_PLAYER;
            AddChild(_enterTrigger);

            var shape = new CollisionShape3D();
            var roomSize = RoomBuilder.GetRoomSize(RoomType);
            var box = new BoxShape3D();
            box.Size = new Vector3(roomSize.X * 0.8f, 4f, roomSize.Y * 0.8f);
            shape.Shape = box;
            shape.Position = new Vector3(0, 2, 0);
            _enterTrigger.AddChild(shape);

            _enterTrigger.BodyEntered += OnBodyEntered;

            // Subscribe to enemy death events
            GameEvents.OnEnemyKilled += OnEnemyKilled;

            // Non-combat rooms are always clear
            if (RoomType != RoomType.Combat && RoomType != RoomType.Boss)
                IsCleared = true;
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
        }

        /// <summary>
        /// Spawn enemies in this room from the floor's enemy pool.
        /// </summary>
        public void SpawnEnemies()
        {
            if (RoomType != RoomType.Combat && RoomType != RoomType.Boss) return;
            if (_floorData == null) return;

            var enemyScene = GD.Load<PackedScene>(Constants.SCENE_ENEMY);
            if (enemyScene == null) return;

            var rng = new RandomNumberGenerator();
            rng.Randomize();

            int count;
            if (RoomType == RoomType.Boss)
            {
                count = 1;
                // Spawn boss
                if (!string.IsNullOrEmpty(_floorData.BossEnemyId))
                {
                    SpawnEnemy(enemyScene, _floorData.BossEnemyId, new Vector3(0, 0.9f, -3), rng);
                    // Add some adds
                    int addCount = rng.RandiRange(1, 3);
                    for (int i = 0; i < addCount; i++)
                    {
                        var pool = _floorData.EnemyPool;
                        var enemyId = pool[rng.RandiRange(0, pool.Count - 1)];
                        var offset = new Vector3(rng.RandfRange(-8, 8), 0.9f, rng.RandfRange(-8, 8));
                        SpawnEnemy(enemyScene, enemyId, offset, rng);
                    }
                    return;
                }
            }

            count = rng.RandiRange(_floorData.MinEnemiesPerRoom, _floorData.MaxEnemiesPerRoom);
            var roomSize = RoomBuilder.GetRoomSize(RoomType);
            float spawnRadius = Mathf.Min(roomSize.X, roomSize.Y) * 0.35f;

            for (int i = 0; i < count; i++)
            {
                var pool = _floorData.EnemyPool;
                if (pool.Count == 0) continue;

                var enemyId = pool[rng.RandiRange(0, pool.Count - 1)];
                float angle = rng.RandfRange(0, Mathf.Tau);
                float dist = rng.RandfRange(2f, spawnRadius);
                var offset = new Vector3(Mathf.Cos(angle) * dist, 0.9f, Mathf.Sin(angle) * dist);
                SpawnEnemy(enemyScene, enemyId, offset, rng);
            }
        }

        private void SpawnEnemy(PackedScene scene, string enemyId, Vector3 localPos, RandomNumberGenerator rng)
        {
            var data = EnemyRegistry.GetEnemy(enemyId);
            if (data == null) return;

            var enemy = scene.Instantiate<EnemyController>();
            AddChild(enemy);
            enemy.Position = localPos;
            enemy.Initialize(data, _floorData?.DifficultyMultiplier ?? 1f);
            _enemies.Add(enemy);
            _totalEnemies++;
        }

        private void OnBodyEntered(Node3D body)
        {
            if (IsEntered) return;
            if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;

            IsEntered = true;
            GameEvents.OnRoomEntered?.Invoke(this);
            GD.Print($"[RoomController] Entered {RoomType} room at {GridPosition}");

            // Spawn enemies on first entry
            SpawnEnemies();
        }

        private void OnEnemyKilled(Node enemy)
        {
            if (IsCleared) return;

            // Check if this enemy belongs to our room
            if (enemy is EnemyController ec && _enemies.Contains(ec))
            {
                _killedEnemies++;

                if (_killedEnemies >= _totalEnemies)
                {
                    IsCleared = true;
                    GameEvents.OnRoomCleared?.Invoke(this);
                    GD.Print($"[RoomController] Room cleared at {GridPosition}!");
                }
            }
        }
    }
}
