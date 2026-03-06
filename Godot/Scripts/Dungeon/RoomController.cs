using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Manages a single room: enemy spawning with wave support, kill tracking, room-enter trigger.
    /// </summary>
    public partial class RoomController : Node3D
    {
        public RoomType RoomType { get; set; } = RoomType.Combat;
        public Vector2I GridPosition { get; set; }
        public bool IsCleared { get; private set; }
        public bool IsEntered { get; private set; }
        public bool IsDiscovered { get; private set; }

        private FogState _currentFogState = FogState.Hidden;
        private readonly Dictionary<ulong, float> _originalLightEnergies = new();
        private int _totalEnemies;
        private int _killedEnemies;
        private Area3D _enterTrigger;
        private readonly List<EnemyController> _enemies = new();
        private SectorData _sectorData;

        // Wave spawning
        private int _currentWave;
        private int _totalWaves = 1;
        private int _waveKillTarget;
        private int _totalEnemyCount;
        private bool _waveSpawning;
        private PackedScene _enemyScene;
        private RandomNumberGenerator _rng;

        public void Initialize(SectorData sectorData)
        {
            _sectorData = sectorData;
        }

        public void SetFogState(FogState state)
        {
            _currentFogState = state;
            if (state != FogState.Hidden)
                IsDiscovered = true;

            var roomNode = GetParent<Node3D>();
            if (roomNode == null) return;

            switch (state)
            {
                case FogState.Hidden:
                    roomNode.Visible = false;
                    break;
                case FogState.Active:
                    roomNode.Visible = true;
                    SetLightsDimmed(roomNode, false);
                    break;
                case FogState.Explored:
                    roomNode.Visible = true;
                    SetLightsDimmed(roomNode, true);
                    break;
            }
        }

        private void SetLightsDimmed(Node root, bool dimmed)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is Light3D light)
                {
                    ulong id = light.GetInstanceId();
                    if (!_originalLightEnergies.ContainsKey(id))
                        _originalLightEnergies[id] = light.LightEnergy;

                    light.LightEnergy = dimmed
                        ? _originalLightEnergies[id] * 0.4f
                        : _originalLightEnergies[id];
                }

                if (child is RoomController) continue;
                if (child is Node node && node.GetChildCount() > 0)
                    SetLightsDimmed(node, dimmed);
            }
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
            if (RoomType != RoomType.Combat && RoomType != RoomType.Boss && RoomType != RoomType.Megabonk)
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
            if (RoomType != RoomType.Combat && RoomType != RoomType.Boss && RoomType != RoomType.Megabonk) return;
            if (_sectorData == null) return;

            _enemyScene = GD.Load<PackedScene>(Constants.SCENE_ENEMY);
            if (_enemyScene == null) return;

            _rng = new RandomNumberGenerator();
            _rng.Randomize();

            if (RoomType == RoomType.Boss)
            {
                // Boss rooms: always 1 wave, no change
                SpawnBossWave();
                return;
            }

            // Determine wave count
            _totalEnemyCount = _rng.RandiRange(_sectorData.MinEnemiesPerRoom, _sectorData.MaxEnemiesPerRoom);

            if (_sectorData.WaveChance > 0 && _rng.Randf() < _sectorData.WaveChance)
                _totalWaves = _rng.RandiRange(2, _sectorData.MaxWaves);
            else
                _totalWaves = 1;

            _currentWave = 0;
            SpawnNextWave();
        }

        private void SpawnBossWave()
        {
            if (string.IsNullOrEmpty(_sectorData.BossEnemyId)) return;

            // Boss rooms: single wave, no wave spawning logic
            _totalWaves = 1;
            _currentWave = 1;

            SpawnEnemy(_enemyScene, _sectorData.BossEnemyId, new Vector3(0, 0.9f, -3), _rng);

            int addCount = _rng.RandiRange(1, 3);
            var bossRoomSize = RoomBuilder.GetRoomSize(RoomType.Boss);
            float spawnRange = Mathf.Min(bossRoomSize.X, bossRoomSize.Y) * 0.3f;
            for (int i = 0; i < addCount; i++)
            {
                var pool = _sectorData.EnemyPool;
                var enemyId = pool[_rng.RandiRange(0, pool.Count - 1)];
                var offset = new Vector3(_rng.RandfRange(-spawnRange, spawnRange), 0.9f, _rng.RandfRange(-spawnRange, spawnRange));
                SpawnEnemy(_enemyScene, enemyId, offset, _rng);
            }

            // Set wave kill target to total so wave check doesn't misfire
            _waveKillTarget = _totalEnemies;
        }

        private void SpawnNextWave()
        {
            if (_currentWave >= _totalWaves) return;
            _currentWave++;
            _waveSpawning = true;

            // Calculate enemies for this wave
            int waveEnemies;
            if (_totalWaves == 1)
            {
                waveEnemies = _totalEnemyCount;
            }
            else if (_currentWave == 1)
            {
                // First wave: 60% of enemies
                waveEnemies = Mathf.Max(1, Mathf.RoundToInt(_totalEnemyCount * 0.6f));
            }
            else
            {
                // Remaining waves split the rest evenly
                int remaining = _totalEnemyCount - Mathf.RoundToInt(_totalEnemyCount * 0.6f);
                int wavesLeft = _totalWaves - 1;
                waveEnemies = Mathf.Max(1, remaining / wavesLeft);
            }

            _waveKillTarget = _totalEnemies + waveEnemies;

            var roomSize = RoomBuilder.GetRoomSize(RoomType);
            float spawnRadius = Mathf.Min(roomSize.X, roomSize.Y) * 0.35f;

            for (int i = 0; i < waveEnemies; i++)
            {
                var pool = _sectorData.EnemyPool;
                if (pool.Count == 0) continue;

                var enemyId = pool[_rng.RandiRange(0, pool.Count - 1)];
                float angle = _rng.RandfRange(0, Mathf.Tau);
                float dist = _rng.RandfRange(2f, spawnRadius);
                var offset = new Vector3(Mathf.Cos(angle) * dist, 0.9f, Mathf.Sin(angle) * dist);
                SpawnEnemy(_enemyScene, enemyId, offset, _rng);
            }

            // Show wave text for waves 2+
            if (_currentWave > 1)
                SpawnWaveText($"Wave {_currentWave}!");

            _waveSpawning = false;
            GD.Print($"[RoomController] Wave {_currentWave}/{_totalWaves} spawned at {GridPosition} ({waveEnemies} enemies)");
        }

        private void SpawnWaveText(string text)
        {
            var label = new Label3D();
            label.Text = text;
            label.FontSize = 64;
            label.Position = new Vector3(0, 3f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(1f, 0.4f, 0.2f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 6;
            label.PixelSize = 0.01f;
            AddChild(label);

            // Animate: scale in, hold, fade out
            // Use tiny scale instead of zero to avoid Basis invert error
            label.Scale = Vector3.One * 0.01f;
            var tween = CreateTween();
            tween.TweenProperty(label, "scale", Vector3.One * 1.2f, 0.3f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(label, "scale", Vector3.One, 0.1f);
            tween.TweenInterval(1.0f);
            tween.TweenProperty(label, "modulate:a", 0f, 0.5f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(label)) label.QueueFree();
            }));
        }

        private void SpawnEnemy(PackedScene scene, string enemyId, Vector3 localPos, RandomNumberGenerator rng)
        {
            var data = EnemyRegistry.GetEnemy(enemyId);
            if (data == null) return;

            var enemy = scene.Instantiate<EnemyController>();
            AddChild(enemy);
            enemy.Position = localPos;

            // Track enemy BEFORE Initialize so kill events can find it
            _enemies.Add(enemy);
            _totalEnemies++;

            enemy.Initialize(data, _sectorData?.DifficultyMultiplier ?? 1f);
            GD.Print($"[RoomController] Spawned {enemyId} at {GridPosition}, total={_totalEnemies}");
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
            if (!IsEntered) return; // Room not entered yet — can't have our enemies

            // Check if this enemy belongs to our room
            if (enemy is EnemyController ec && _enemies.Contains(ec))
            {
                _killedEnemies++;
                GD.Print($"[RoomController] Kill registered at {GridPosition}: {_killedEnemies}/{_totalEnemies}");

                // Check if current wave is cleared
                if (_killedEnemies >= _waveKillTarget && _currentWave < _totalWaves && !_waveSpawning)
                {
                    // Delay before next wave
                    GD.Print($"[RoomController] Wave {_currentWave} cleared at {GridPosition}, next wave in 2s");
                    var tree = GetTree();
                    if (tree != null)
                    {
                        var timer = tree.CreateTimer(2.0f);
                        timer.Timeout += SpawnNextWave;
                    }
                }
                else if (_killedEnemies >= _totalEnemies && _currentWave >= _totalWaves)
                {
                    IsCleared = true;
                    GameEvents.OnRoomCleared?.Invoke(this);
                    GD.Print($"[RoomController] Room CLEARED at {GridPosition}!");
                }
            }
        }
    }
}
