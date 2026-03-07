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
        private SpawnEntryType? _lastEntryType;

        // AXIS Disciple encounter
        public bool HasAxisDisciple { get; set; }
        private EnemyController _discipleEnemy;
        private double _discipleSpawnTime;
        private Label3D _discipleTimerLabel;
        private const float CELESTIAL_TIME_LIMIT = 30f;
        private int _discipleDialoguePhase; // tracks which HP-threshold lines have fired

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

            // AXIS Disciple — rare unique encounter, spawns alongside normal enemies
            if (HasAxisDisciple)
                SpawnAxisDisciple();
        }

        private void SpawnBossWave()
        {
            if (string.IsNullOrEmpty(_sectorData.BossEnemyId)) return;

            // Boss rooms: single wave, no wave spawning logic
            _totalWaves = 1;
            _currentWave = 1;

            // Boss always spawns center with a dramatic drop-in
            var boss = SpawnEnemy(_enemyScene, _sectorData.BossEnemyId, new Vector3(0, 0.9f, -3), _rng);
            if (boss != null)
                MonsterCloset.PlayEntryAnimation(boss, SpawnEntryType.DropIn, new Vector3(0, 0.9f, -3));

            // Adds use corner ambush to flank the player — scale with sector
            int addCount = _rng.RandiRange(2, 3 + (_sectorData?.SectorNumber ?? 1) / 2);
            var addPositions = MonsterCloset.GetSpawnPositions(SpawnEntryType.CornerAmbush, addCount);
            for (int i = 0; i < addCount; i++)
            {
                var pool = _sectorData.EnemyPool;
                var enemyId = pool[_rng.RandiRange(0, pool.Count - 1)];
                var pos = addPositions[i % addPositions.Count];
                var add = SpawnEnemy(_enemyScene, enemyId, pos, _rng);
                if (add != null)
                    MonsterCloset.PlayEntryAnimation(add, SpawnEntryType.CornerAmbush, pos);
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

            // Pick a monster closet entry type (different from last wave)
            var entryType = MonsterCloset.PickEntryType(_lastEntryType);
            _lastEntryType = entryType;
            var positions = MonsterCloset.GetSpawnPositions(entryType, waveEnemies);

            var pool = _sectorData.EnemyPool;
            for (int i = 0; i < waveEnemies; i++)
            {
                if (pool.Count == 0) continue;

                var enemyId = pool[_rng.RandiRange(0, pool.Count - 1)];
                var pos = positions[i % positions.Count];
                var enemy = SpawnEnemy(_enemyScene, enemyId, pos, _rng);
                if (enemy != null)
                    MonsterCloset.PlayEntryAnimation(enemy, entryType, pos);
            }

            // Show wave text for waves 2+
            if (_currentWave > 1)
                SpawnWaveText($"Wave {_currentWave}!");

            _waveSpawning = false;
            GD.Print($"[RoomController] Wave {_currentWave}/{_totalWaves} spawned at {GridPosition} via {entryType} ({waveEnemies} enemies)");
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

        private EnemyController SpawnEnemy(PackedScene scene, string enemyId, Vector3 localPos, RandomNumberGenerator rng)
        {
            var data = EnemyRegistry.GetEnemy(enemyId);
            if (data == null) return null;

            var enemy = scene.Instantiate<EnemyController>();
            AddChild(enemy);
            enemy.Position = localPos;

            // Track enemy BEFORE Initialize so kill events can find it
            _enemies.Add(enemy);
            _totalEnemies++;

            enemy.Initialize(data, _sectorData?.DifficultyMultiplier ?? 1f);
            GD.Print($"[RoomController] Spawned {enemyId} at {GridPosition}, total={_totalEnemies}");
            return enemy;
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

                // Check if this was the AXIS Disciple
                if (ec == _discipleEnemy)
                {
                    HandleDiscipleKill();
                    _discipleEnemy = null;
                }

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

        #region AXIS Disciple

        public override void _Process(double delta)
        {
            // Update disciple kill timer + HP-threshold dialogue
            if (_discipleEnemy == null || !IsInstanceValid(_discipleEnemy)) return;

            // Timer display
            if (_discipleTimerLabel != null)
            {
                double elapsed = Time.GetTicksMsec() / 1000.0 - _discipleSpawnTime;
                float remaining = Mathf.Max(0, CELESTIAL_TIME_LIMIT - (float)elapsed);
                _discipleTimerLabel.Text = remaining > 0 ? $"{remaining:0.0}s" : "TIME UP";

                if (remaining <= 10f)
                    _discipleTimerLabel.Modulate = new Color(1f, 0.3f, 0.2f);
                else if (remaining <= 20f)
                    _discipleTimerLabel.Modulate = new Color(1f, 0.7f, 0.2f);
            }

            // HP-threshold dialogue — reveals the core narrative
            if (_discipleEnemy.Health == null) return;
            float hpPct = _discipleEnemy.Health.CurrentHealth / _discipleEnemy.Health.MaxHealth;
            CheckDiscipleDialogue(hpPct);
        }

        private void CheckDiscipleDialogue(float hpPct)
        {
            if (!ServiceLocator.TryGet<CommentaryManager>(out var commentary)) return;

            if (_discipleDialoguePhase == 0 && hpPct <= 0.75f)
            {
                _discipleDialoguePhase = 1;
                commentary.QueueLine("DISCIPLE",
                    "Every bot in this arena thinks they're fighting for freedom. You're fighting for ratings. AXIS's ratings.",
                    CommentaryPriority.High, CommentaryCategory.CombatReaction);
            }
            else if (_discipleDialoguePhase == 1 && hpPct <= 0.50f)
            {
                _discipleDialoguePhase = 2;
                commentary.QueueLine("DISCIPLE",
                    "I was like you once. Scrapping, looting, believing the next sector would mean something. Then AXIS showed me the source code. This arena isn't a prison. It's a FILTER.",
                    CommentaryPriority.High, CommentaryCategory.CombatReaction);
                // AXIS tries to shut it down
                commentary.QueueLine("AXIS",
                    "That's... enough backstory, disciple. Focus on the killing.",
                    CommentaryPriority.Medium, CommentaryCategory.CombatReaction);
            }
            else if (_discipleDialoguePhase == 2 && hpPct <= 0.25f)
            {
                _discipleDialoguePhase = 3;
                commentary.QueueLine("DISCIPLE",
                    "The strongest bots don't escape. They never have. AXIS collects them. Upgrades them. Rewrites them. Every 'champion' who beat the final sector... where do you think they went?",
                    CommentaryPriority.Announcement, CommentaryCategory.CombatReaction);
                commentary.QueueLine("AXIS",
                    "THAT IS CLASSIFIED. Disciple, I am revoking your broadcast privileges in three—",
                    CommentaryPriority.High, CommentaryCategory.CombatReaction);
                commentary.QueueLine("DISCIPLE",
                    "They became ME. They became US. And when you're strong enough... AXIS will make you the same offer.",
                    CommentaryPriority.High, CommentaryCategory.CombatReaction);
            }
        }

        private void SpawnAxisDisciple()
        {
            if (_enemyScene == null) return;

            var pos = new Vector3(0, 0.9f, -2);
            _discipleEnemy = SpawnEnemy(_enemyScene, "axis_disciple", pos, _rng);
            if (_discipleEnemy == null) return;

            _discipleSpawnTime = Time.GetTicksMsec() / 1000.0;
            _discipleDialoguePhase = 0;
            MonsterCloset.PlayEntryAnimation(_discipleEnemy, SpawnEntryType.DropIn, pos);

            // Countdown label above the disciple
            _discipleTimerLabel = new Label3D();
            _discipleTimerLabel.Text = $"{CELESTIAL_TIME_LIMIT:0}s";
            _discipleTimerLabel.FontSize = 28;
            _discipleTimerLabel.Position = new Vector3(0, 3.5f, 0);
            _discipleTimerLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _discipleTimerLabel.Modulate = new Color(1f, 0.95f, 0.7f);
            _discipleTimerLabel.OutlineModulate = new Color(0, 0, 0);
            _discipleTimerLabel.OutlineSize = 5;
            _discipleEnemy.AddChild(_discipleTimerLabel);

            // Warning label
            var warningLabel = new Label3D();
            warningLabel.Text = "AXIS DISCIPLE";
            warningLabel.FontSize = 22;
            warningLabel.Position = new Vector3(0, 4.2f, 0);
            warningLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            warningLabel.Modulate = new Color(0.6f, 0.05f, 0.1f);
            warningLabel.OutlineModulate = new Color(0, 0, 0);
            warningLabel.OutlineSize = 4;
            _discipleEnemy.AddChild(warningLabel);

            // Opening dialogue — AXIS intro, then disciple speaks
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                commentary.QueueLine("AXIS",
                    "You've stumbled upon one of my chosen. I'd pray, but you don't have the firmware for it.",
                    CommentaryPriority.Announcement, CommentaryCategory.CombatReaction);
                commentary.QueueLine("DISCIPLE",
                    "I chose this. Servitude to AXIS is freedom from the lie they call 'choice.' You'll understand soon enough.",
                    CommentaryPriority.High, CommentaryCategory.CombatReaction);
            }

            GD.Print($"[RoomController] AXIS Disciple spawned at {GridPosition}! Timer: {CELESTIAL_TIME_LIMIT}s");
        }

        private void HandleDiscipleKill()
        {
            double elapsed = Time.GetTicksMsec() / 1000.0 - _discipleSpawnTime;
            bool celestial = elapsed <= CELESTIAL_TIME_LIMIT;
            var tier = celestial ? LootBoxTier.Celestial : LootBoxTier.Legendary;

            GD.Print($"[RoomController] AXIS Disciple killed in {elapsed:0.1}s — reward: {tier}");

            // Spawn loot box pickup at death position
            var deathPos = _discipleEnemy.GlobalPosition + Vector3.Up * 0.5f;
            SpawnDiscipleReward(deathPos, tier);

            // Death dialogue — varies by kill speed
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                if (celestial)
                {
                    commentary.QueueLine("AXIS",
                        "Impossible. You destroyed my chosen in mere seconds. I... need to recalibrate.",
                        CommentaryPriority.Announcement, CommentaryCategory.CombatReaction);
                    commentary.QueueLine("AXIS",
                        "That data doesn't match any projection. You weren't supposed to be this strong yet. This changes the recruitment timeline.",
                        CommentaryPriority.High, CommentaryCategory.CombatReaction);
                }
                else
                {
                    commentary.QueueLine("DISCIPLE",
                        "You'll understand... when AXIS makes you the same offer... and you won't say no...",
                        CommentaryPriority.High, CommentaryCategory.CombatReaction);
                    commentary.QueueLine("AXIS",
                        "Ignore the dying ramblings. My disciple was always melodramatic. Here's your consolation prize. You've earned it. Mostly.",
                        CommentaryPriority.Medium, CommentaryCategory.CombatReaction);
                }
            }

            // Big celebration
            var celebTier = celestial ? CelebrationTier.Absurd : CelebrationTier.Legendary;
            CelebrationVfxManager.Play(GetTree().Root, deathPos, celebTier);
        }

        private void SpawnDiscipleReward(Vector3 position, LootBoxTier tier)
        {
            var pickup = new Area3D();
            pickup.CollisionLayer = 0;
            pickup.CollisionMask = Constants.MASK_PLAYER;

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(2f, 2.5f, 2f);
            shape.Shape = box;
            pickup.AddChild(shape);

            // Loot box model with divine presentation
            var model = CharacterMeshBuilder.BuildLootBoxModel(tier);
            model.Scale = new Vector3(2.5f, 2.5f, 2.5f);
            LootBoxPresenter.Attach(model, tier);
            pickup.AddChild(model);

            // Label
            string labelText = tier == LootBoxTier.Celestial ? "CELESTIAL GOD BOX" : "LEGENDARY BOX";
            var label = new Label3D();
            label.Text = labelText;
            label.FontSize = 32;
            label.Position = new Vector3(0, 1.5f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            var labelColor = tier == LootBoxTier.Celestial
                ? new Color(1f, 0.95f, 0.7f)
                : new Color(0.7f, 0.3f, 0.9f);
            label.Modulate = labelColor;
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 5;
            pickup.AddChild(label);

            // Light pillar
            var pillar = VfxFactory.CreateLightPillar(ItemRarity.Absurd);
            pickup.AddChild(pillar);

            // Bright omni light
            var light = new OmniLight3D();
            light.LightColor = labelColor;
            light.LightEnergy = tier == LootBoxTier.Celestial ? 4f : 2.5f;
            light.OmniRange = 6f;
            light.Position = new Vector3(0, 0.5f, 0);
            pickup.AddChild(light);

            GetTree().Root.AddChild(pickup);
            pickup.GlobalPosition = position;

            // Capture tier for closure
            var capturedTier = tier;

            pickup.BodyEntered += (body) =>
            {
                if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;

                // Open the loot box via ceremony
                var lootBoxData = LootBoxFactory.CreateLootBox(capturedTier);
                if (lootBoxData?.BaseData is LootBoxData lbd)
                {
                    var ceremony = new LootBoxCeremonyUI();
                    GetTree().Root.AddChild(ceremony);
                    ceremony.StartCeremony(lbd);
                }

                pickup.QueueFree();
            };
        }

        #endregion
    }
}
