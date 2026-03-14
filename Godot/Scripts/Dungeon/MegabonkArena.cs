using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Builds a massive 96x96 arena far below the dungeon, runs escalating waves of enemies,
    /// tracks kills with a billboard counter, awards tiered loot on completion, then
    /// teleports the player back to the original Megabonk room.
    /// </summary>
    public partial class MegabonkArena : Node3D
    {
        private SectorData _sectorData;
        private Node3D _player;
        private RoomController _sourceRoom;
        private Vector3 _returnPosition;
        private Node3D _arenaNode;

        private int _killCount;
        private int _killTarget;
        private int _currentWave;
        private int _totalWaves;
        private Label3D _killLabel;
        private readonly List<EnemyController> _enemies = new();

        private RandomNumberGenerator _rng;
        private PackedScene _enemyScene;
        private SpawnEntryType? _lastEntryType;

        private static readonly Vector3 ArenaOrigin = new(0, -500, 0);
        private const float ArenaSize = 96f;
        private const float ArenaHalf = 43f; // spawn radius for MonsterCloset

        /// <summary>
        /// Launch a Megabonk arena encounter for the given player and source room.
        /// </summary>
        public static void Launch(Node3D player, RoomController sourceRoom, SectorData sectorData)
        {
            var arena = new MegabonkArena();
            arena._player = player;
            arena._sourceRoom = sourceRoom;
            arena._sectorData = sectorData;
            arena._returnPosition = player.GlobalPosition;

            // Add to scene tree root so it persists independent of the dungeon
            player.GetTree().Root.AddChild(arena);
            arena.Begin();
        }

        private void Begin()
        {
            _rng = new RandomNumberGenerator();
            _rng.Randomize();
            _enemyScene = GD.Load<PackedScene>(Constants.SCENE_ENEMY);

            // Determine wave/kill config based on sector
            int sector = _sectorData?.SectorNumber ?? 3;
            if (sector <= 3)
            {
                _totalWaves = 5;
                _killTarget = _rng.RandiRange(25, 30);
            }
            else if (sector == 4)
            {
                _totalWaves = 6;
                _killTarget = _rng.RandiRange(35, 40);
            }
            else
            {
                _totalWaves = 7;
                _killTarget = _rng.RandiRange(45, 55);
            }

            // Build the arena geometry
            BuildArena();

            // Subscribe to kills
            GameEvents.OnEnemyKilled += OnEnemyKilled;

            // Dramatic intro — screen flash + text, then teleport
            ShowIntroText();

            var tree = GetTree();
            if (tree != null)
            {
                tree.CreateTimer(1.0f).Timeout += () =>
                {
                    TeleportPlayerToArena();
                    // Start first wave after a short settle delay
                    tree.CreateTimer(1.0f).Timeout += SpawnNextWave;
                };
            }
        }

        private void BuildArena()
        {
            var size = new Vector2(ArenaSize, ArenaSize);
            _arenaNode = RoomBuilder.BuildRoom(
                ArenaOrigin, size, RoomType.Megabonk,
                doorNorth: false, doorSouth: false, doorEast: false, doorWest: false,
                sectorData: _sectorData, shape: RoomShape.Rectangle);
            GetTree().Root.AddChild(_arenaNode);
        }

        private void ShowIntroText()
        {
            // Big dramatic label at current player position
            var label = new Label3D();
            label.Text = "MEGABONK!";
            label.FontSize = 96;
            label.PixelSize = 0.01f;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(1f, 0.2f, 0.1f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 12;
            label.NoDepthTest = true;
            label.Scale = Vector3.One * 0.01f;

            GetTree().Root.AddChild(label);
            label.GlobalPosition = _player.GlobalPosition + Vector3.Up * 3f;

            var tween = label.CreateTween();
            tween.TweenProperty(label, "scale", Vector3.One * 2f, 0.3f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(label, "scale", Vector3.One * 1.5f, 0.15f);
            tween.TweenInterval(0.8f);
            tween.TweenProperty(label, "modulate:a", 0f, 0.4f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(label)) label.QueueFree();
            }));

            // Screen flash via CelebrationVfxManager
            CelebrationVfxManager.Play(GetTree().Root, _player.GlobalPosition, CelebrationTier.Exciting);

            // AXIS commentary
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                commentary.QueueLine("AXIS",
                    "MEGABONK INITIATED. This is where dreams come to die. Yours specifically.",
                    CommentaryPriority.Announcement, CommentaryCategory.RoomReaction);
            }
        }

        private void TeleportPlayerToArena()
        {
            _player.GlobalPosition = ArenaOrigin + Vector3.Up * 1f;
            GD.Print($"[MegabonkArena] Player teleported to arena at {ArenaOrigin}");
        }

        private void SpawnNextWave()
        {
            if (_currentWave >= _totalWaves) return;
            _currentWave++;

            // Calculate enemies per wave — escalating: wave 1 gets ~20% of total, each subsequent wave adds more
            int basePerWave = Mathf.Max(2, _killTarget / _totalWaves);
            int waveEnemies;

            if (_currentWave == 1)
            {
                // First wave: small group (~20% of total)
                waveEnemies = Mathf.Max(3, Mathf.RoundToInt(_killTarget * 0.2f));
            }
            else if (_currentWave == _totalWaves)
            {
                // Final wave: spawn whatever's left to reach the kill target
                int spawnedSoFar = CountTotalSpawned();
                waveEnemies = Mathf.Max(3, _killTarget - spawnedSoFar);
            }
            else
            {
                // Middle waves: escalating (+2-3 per wave over base)
                waveEnemies = basePerWave + (_currentWave - 1) * 2;
            }

            // Pick entry type — different each wave, final wave = Surround
            SpawnEntryType entryType;
            if (_currentWave == _totalWaves)
            {
                entryType = SpawnEntryType.Surround;
            }
            else
            {
                entryType = MonsterCloset.PickEntryType(_lastEntryType);
            }
            _lastEntryType = entryType;

            var positions = MonsterCloset.GetSpawnPositions(entryType, waveEnemies, ArenaHalf);

            // Build mixed enemy pool: sector pool + adjacent sector pools for variety
            var mixedPool = BuildMixedEnemyPool();

            for (int i = 0; i < waveEnemies; i++)
            {
                if (mixedPool.Count == 0) break;
                var enemyId = mixedPool[_rng.RandiRange(0, mixedPool.Count - 1)];
                var pos = positions[i % positions.Count];
                var enemy = SpawnEnemy(enemyId, pos);
                if (enemy != null)
                    MonsterCloset.PlayEntryAnimation(enemy, entryType, pos);
            }

            // Show wave text
            ShowWaveText($"Wave {_currentWave}!");
            UpdateKillLabel();

            GD.Print($"[MegabonkArena] Wave {_currentWave}/{_totalWaves}: {waveEnemies} enemies via {entryType}");
        }

        private int CountTotalSpawned()
        {
            return _enemies.Count;
        }

        private List<string> BuildMixedEnemyPool()
        {
            var pool = new List<string>();

            // Add current sector's pool
            if (_sectorData?.EnemyPool != null)
                pool.AddRange(_sectorData.EnemyPool);

            // Pull from adjacent sectors for variety
            int sector = _sectorData?.SectorNumber ?? 3;
            if (sector > 1)
            {
                var prevSector = SectorDataRegistry.GetSector(sector - 1);
                if (prevSector?.EnemyPool != null)
                {
                    // Add a subset (every other enemy) to avoid overwhelming the pool
                    for (int i = 0; i < prevSector.EnemyPool.Count; i += 2)
                        pool.Add(prevSector.EnemyPool[i]);
                }
            }
            if (sector < 5)
            {
                var nextSector = SectorDataRegistry.GetSector(sector + 1);
                if (nextSector?.EnemyPool != null)
                {
                    for (int i = 0; i < nextSector.EnemyPool.Count; i += 2)
                        pool.Add(nextSector.EnemyPool[i]);
                }
            }

            return pool;
        }

        private EnemyController SpawnEnemy(string enemyId, Vector3 localPos)
        {
            if (_enemyScene == null) return null;
            var data = EnemyRegistry.GetEnemy(enemyId);
            if (data == null) return null;

            var enemy = _enemyScene.Instantiate<EnemyController>();
            // Add to arena node so positions are relative to arena center
            _arenaNode.AddChild(enemy);
            enemy.Position = localPos;

            _enemies.Add(enemy);
            enemy.Initialize(data, _sectorData?.DifficultyMultiplier ?? 1f);
            return enemy;
        }

        private void OnEnemyKilled(Node enemy)
        {
            if (enemy is not EnemyController ec) return;
            if (!_enemies.Contains(ec)) return;

            _killCount++;
            UpdateKillLabel();

            if (_killCount >= _killTarget)
            {
                Victory();
            }
            else
            {
                // Check if all current living enemies are dead — advance to next wave
                int alive = 0;
                foreach (var e in _enemies)
                {
                    if (IsInstanceValid(e) && e.Health != null && e.Health.IsAlive)
                        alive++;
                }

                if (alive == 0 && _currentWave < _totalWaves)
                {
                    // Delay before next wave
                    var tree = GetTree();
                    if (tree != null)
                    {
                        ShowWaveText("Next wave incoming...");
                        tree.CreateTimer(3.0f).Timeout += SpawnNextWave;
                    }
                }
            }
        }

        private void Victory()
        {
            GD.Print("[MegabonkArena] MEGABONK COMPLETE!");

            // Update kill label to victory message
            if (_killLabel != null && IsInstanceValid(_killLabel))
            {
                _killLabel.Text = "MEGABONK COMPLETE!";
                _killLabel.Modulate = new Color(1f, 0.85f, 0.2f);
                _killLabel.FontSize = 160;
            }

            // Big celebration
            CelebrationVfxManager.Play(GetTree().Root, _player.GlobalPosition + Vector3.Up * 0.5f, CelebrationTier.Legendary);

            // AXIS commentary
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                commentary.QueueLine("AXIS",
                    "You survived the Megabonk. I'm genuinely disappointed. Here's your prize, you absolute animal.",
                    CommentaryPriority.Announcement, CommentaryCategory.CombatReaction);
            }

            // Spawn rewards around the player
            SpawnRewards();

            // After delay, teleport back and clean up
            var tree = GetTree();
            if (tree != null)
            {
                tree.CreateTimer(3.0f).Timeout += ReturnAndCleanup;
            }
        }

        private void SpawnRewards()
        {
            int sector = _sectorData?.SectorNumber ?? 3;

            // Determine loot box tier based on sector
            LootBoxTier boxTier;
            if (sector <= 3)
                boxTier = LootBoxTier.Gold;
            else if (sector == 4)
                boxTier = LootBoxTier.Diamond;
            else
                boxTier = LootBoxTier.Legendary;

            // Spawn loot box pickup near the player
            var boxPos = _player.GlobalPosition + new Vector3(2f, 0.5f, 0f);
            SpawnLootBoxPickup(boxPos, boxTier);

            // Relic cache chance: 30% base, +10% per sector above 3
            float relicChance = 0.30f + Mathf.Max(0, (sector - 3)) * 0.10f;
            if (_rng.Randf() < relicChance)
                SpawnRelicCache(_player.GlobalPosition + new Vector3(-2f, 0.3f, 0f));

            GD.Print($"[MegabonkArena] Rewards: {boxTier} box, relic chance={relicChance:P0}");
        }

        private void SpawnLootBoxPickup(Vector3 position, LootBoxTier tier)
        {
            var pickup = new Area3D();
            pickup.CollisionLayer = 0;
            pickup.CollisionMask = Constants.MASK_PLAYER;

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(2f, 2.5f, 2f);
            shape.Shape = box;
            pickup.AddChild(shape);

            var model = CharacterMeshBuilder.BuildLootBoxModel(tier);
            model.Scale = new Vector3(1.3f, 1.3f, 1.3f);
            LootBoxPresenter.Attach(model, tier);
            pickup.AddChild(model);

            string labelText = tier switch
            {
                LootBoxTier.Gold => "GOLD BOX",
                LootBoxTier.Diamond => "DIAMOND BOX",
                _ => "LEGENDARY BOX"
            };

            var label = new Label3D();
            label.Text = labelText;
            label.FontSize = 32;
            label.Position = new Vector3(0, 1.5f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(1f, 0.85f, 0.3f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 5;
            pickup.AddChild(label);

            var pillar = VfxFactory.CreateLightPillar(ItemRarity.Legendary);
            pickup.AddChild(pillar);

            var light = new OmniLight3D();
            light.LightColor = new Color(1f, 0.85f, 0.3f);
            light.LightEnergy = 3f;
            light.OmniRange = 6f;
            light.Position = new Vector3(0, 0.5f, 0);
            pickup.AddChild(light);

            GetTree().Root.AddChild(pickup);
            pickup.GlobalPosition = position;

            var capturedTier = tier;
            pickup.BodyEntered += (body) =>
            {
                if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;
                StartLootBoxChain(capturedTier, 0);
                pickup.QueueFree();
            };
        }

        private void StartLootBoxChain(LootBoxTier tier, int playerIdx)
        {
            if (playerIdx >= PlayerManager.PlayerCount) return;

            var lootBoxData = LootBoxFactory.CreateLootBox(tier);
            if (lootBoxData?.BaseData is not LootBoxData lbd)
            {
                StartLootBoxChain(tier, playerIdx + 1);
                return;
            }

            var player = PlayerManager.Players[playerIdx];
            var ceremony = new LootBoxCeremonyUI();
            GetTree().Root.AddChild(ceremony);
            ceremony.StartCeremony(lbd, player);

            int nextIdx = playerIdx + 1;
            ceremony.CeremonyCollected += () =>
            {
                if (nextIdx < PlayerManager.PlayerCount)
                    GetTree().CreateTimer(0.5f).Timeout += () => StartLootBoxChain(tier, nextIdx);
            };
        }

        private void SpawnRelicCache(Vector3 position)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var relic = RelicRegistry.PickRandom(gm.FoundRelicsThisRun);
            if (relic == null) return;

            var cache = new Area3D();
            cache.CollisionLayer = 0;
            cache.CollisionMask = Constants.MASK_PLAYER;

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(2f, 2.5f, 2f);
            shape.Shape = box;
            cache.AddChild(shape);

            var model = CharacterMeshBuilder.BuildLootBoxModel(LootBoxTier.Diamond);
            model.Scale = new Vector3(1.3f, 1.3f, 1.3f);
            LootBoxPresenter.Attach(model, LootBoxTier.Legendary);
            cache.AddChild(model);

            var label = new Label3D();
            label.Text = "RELIC CACHE";
            label.FontSize = 32;
            label.Position = new Vector3(0, 1.5f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = relic.GlowColor;
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 5;
            cache.AddChild(label);

            var pillar = VfxFactory.CreateLightPillar(ItemRarity.Absurd);
            cache.AddChild(pillar);

            var light = new OmniLight3D();
            light.LightColor = relic.GlowColor;
            light.LightEnergy = 2.5f;
            light.OmniRange = 5f;
            light.Position = new Vector3(0, 0.5f, 0);
            cache.AddChild(light);

            GetTree().Root.AddChild(cache);
            cache.GlobalPosition = position;

            var capturedRelic = relic;
            cache.BodyEntered += (body) =>
            {
                if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;
                gm.FoundRelicsThisRun.Add(capturedRelic.Id);
                var ceremony = new RelicCacheUI();
                GetTree().Root.AddChild(ceremony);
                ceremony.StartCeremony(capturedRelic);
                cache.QueueFree();
            };
        }

        private void ReturnAndCleanup()
        {
            // Teleport player back
            _player.GlobalPosition = _returnPosition;
            GD.Print($"[MegabonkArena] Player returned to {_returnPosition}");

            // Mark source room as cleared and fire event
            if (_sourceRoom != null && IsInstanceValid(_sourceRoom))
            {
                _sourceRoom.MarkClearedExternally();
                GameEvents.OnRoomCleared?.Invoke(_sourceRoom);
            }

            Cleanup();
        }

        private void Cleanup()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;

            if (_arenaNode != null && IsInstanceValid(_arenaNode))
                _arenaNode.QueueFree();

            if (_killLabel != null && IsInstanceValid(_killLabel))
                _killLabel.QueueFree();

            QueueFree();
            GD.Print("[MegabonkArena] Arena cleaned up");
        }

        private void UpdateKillLabel()
        {
            if (_killLabel == null || !IsInstanceValid(_killLabel))
            {
                _killLabel = new Label3D();
                _killLabel.FontSize = 128;
                _killLabel.PixelSize = 0.01f;
                _killLabel.Position = ArenaOrigin + new Vector3(0, 8f, 0);
                _killLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                _killLabel.OutlineModulate = new Color(0, 0, 0);
                _killLabel.OutlineSize = 12;
                _killLabel.NoDepthTest = true;
                GetTree().Root.AddChild(_killLabel);
            }

            _killLabel.Text = $"Kills: {_killCount} / {_killTarget}";

            // Color shifts green→yellow→red as progress increases
            float t = (float)_killCount / Mathf.Max(1, _killTarget);
            if (t < 0.5f)
                _killLabel.Modulate = new Color(0.3f, 1f, 0.3f).Lerp(new Color(1f, 1f, 0.3f), t * 2f);
            else
                _killLabel.Modulate = new Color(1f, 1f, 0.3f).Lerp(new Color(1f, 0.3f, 0.2f), (t - 0.5f) * 2f);
        }

        private void ShowWaveText(string text)
        {
            var label = new Label3D();
            label.Text = text;
            label.FontSize = 72;
            label.PixelSize = 0.01f;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(1f, 0.4f, 0.2f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 8;
            label.NoDepthTest = true;
            label.Scale = Vector3.One * 0.01f;

            GetTree().Root.AddChild(label);
            label.GlobalPosition = ArenaOrigin + new Vector3(0, 5f, 0);

            var tween = label.CreateTween();
            tween.TweenProperty(label, "scale", Vector3.One * 1.5f, 0.3f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(label, "scale", Vector3.One, 0.1f);
            tween.TweenInterval(1.5f);
            tween.TweenProperty(label, "modulate:a", 0f, 0.5f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(label)) label.QueueFree();
            }));
        }

        public override void _Process(double delta)
        {
            // Safety: if all tracked enemies are dead but kill target not reached,
            // and we have more waves, advance to next wave
            if (_killCount >= _killTarget) return;

            int alive = 0;
            foreach (var e in _enemies)
            {
                if (IsInstanceValid(e) && e.Health != null && e.Health.IsAlive)
                    alive++;
            }

            if (alive == 0 && _enemies.Count > 0)
            {
                if (_currentWave < _totalWaves)
                {
                    // Force advance to next wave — enemies may have died without firing kill event
                    _killCount = _enemies.Count - alive; // Fix count to match reality
                    UpdateKillLabel();
                    SpawnNextWave();
                }
                else if (_killCount < _killTarget)
                {
                    // All waves spawned, all dead — force victory
                    GD.Print("[MegabonkArena] Safety clear: all enemies dead, forcing victory");
                    _killCount = _killTarget;
                    UpdateKillLabel();
                    Victory();
                }
            }
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
        }
    }
}
