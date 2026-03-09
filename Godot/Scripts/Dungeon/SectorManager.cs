using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Manages an arena sector. Spawns player, companion, HUD, camera, enemies,
    /// and support systems (CombatManager, CommentaryManager, SystemMessageManager, AudioManager).
    /// Uses DungeonGenerator for procedural room layout.
    /// </summary>
    public partial class SectorManager : Node3D
    {
        [Export] private PackedScene _playerScene;
        [Export] private PackedScene _hudScene;
        [Export] private PackedScene _cameraScene;
        [Export] private bool _skipIntro;
        private PlayerController _player;
        private PlayerController _player2;
        private DungeonGenerator _generator;
        private DungeonAssemblyIntro _assemblyIntro;
        private bool _introFinished;

        public DungeonGenerator Generator => _generator;

        public override void _Ready()
        {
            int sectorNum = GameManager.Instance?.CurrentSector ?? 1;
            int areaNum = GameManager.Instance?.CurrentArea ?? 1;
            GD.Print($"[SectorManager] _Ready starting for Sector {sectorNum}, Area {areaNum}");

            try
            {
                ReadyInternal(sectorNum, areaNum);
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SectorManager] FATAL: _Ready crashed for Sector {sectorNum}: {ex}");
            }
        }

        private void ReadyInternal(int sectorNum, int areaNum)
        {
            // Initialize sector data registry
            SectorDataRegistry.Initialize();

            var baseSectorData = SectorDataRegistry.GetSector(sectorNum);

            // Clone sector data to avoid mutating the shared registry object
            var sectorData = new SectorData(baseSectorData.SectorNumber, baseSectorData.DifficultyMultiplier,
                new System.Collections.Generic.List<string>(baseSectorData.EnemyPool), baseSectorData.BossEnemyId)
            {
                MinRooms = baseSectorData.MinRooms,
                MaxRooms = baseSectorData.MaxRooms,
                MinEnemiesPerRoom = baseSectorData.MinEnemiesPerRoom,
                MaxEnemiesPerRoom = baseSectorData.MaxEnemiesPerRoom,
                TimeLimit = baseSectorData.TimeLimit,
                WaveChance = baseSectorData.WaveChance,
                MaxWaves = baseSectorData.MaxWaves,
                AllowedHazards = baseSectorData.AllowedHazards != null
                    ? new System.Collections.Generic.List<HazardType>(baseSectorData.AllowedHazards)
                    : new System.Collections.Generic.List<HazardType>(),
                TotalRooms = baseSectorData.TotalRooms,
                CombatRoomCount = baseSectorData.CombatRoomCount,
                TreasureRooms = baseSectorData.TreasureRooms,
                EventRooms = baseSectorData.EventRooms,
                ShopRooms = baseSectorData.ShopRooms,
                PuzzleRooms = baseSectorData.PuzzleRooms,
                SafeRoomChance = baseSectorData.SafeRoomChance,
                MegabonkChance = baseSectorData.MegabonkChance,
                MaxMegabonkRooms = baseSectorData.MaxMegabonkRooms,
                RareLootChance = baseSectorData.RareLootChance,
                FloorTint = baseSectorData.FloorTint,
                WallTint = baseSectorData.WallTint,
                AccentColor = baseSectorData.AccentColor,
                TorchTint = baseSectorData.TorchTint,
                ThemeName = baseSectorData.ThemeName,
            };

            // Scale difficulty up slightly per area within a sector
            if (areaNum > 1)
                sectorData.DifficultyMultiplier *= 1f + (areaNum - 1) * 0.15f;

            // Apply meta-progression threat scaling
            MetaSaveManager.ApplyThreatToSector(sectorData);

            // Build backdrop (sky, distant structures, fog, particles)
            var backdrop = new DungeonBackdrop();
            backdrop.Name = "DungeonBackdrop";
            AddChild(backdrop);
            backdrop.Initialize(sectorData);

            // Generate dungeon
            RoomBuilder.ClearFloorCache();
            _generator = new DungeonGenerator(sectorData);
            var spawnPos = _generator.Generate(this);

            // Spawn support systems early (audio needed for intro)
            SpawnSupportSystems();

            // Spawn player in the entrance room — visible during intro
            if (_playerScene != null)
            {
                _player = _playerScene.Instantiate<PlayerController>();
                AddChild(_player);
                _player.GlobalPosition = spawnPos;

                var selectedClass = GameManager.Instance?.SelectedClass ?? BotFrameType.TinCan;
                _player.ClassController.SelectClass(selectedClass);

                // Apply persistent meta-perks to player stats
                MetaSaveManager.ApplyPerksToPlayer(_player.Stats);

                GD.Print("[SectorManager] Player 1 spawned");

                // Spawn P2 only when the player explicitly chose co-op from the menu
                if (GameManager.Instance?.CoOpEnabled == true && _playerScene != null)
                {
                    _player2 = _playerScene.Instantiate<PlayerController>();
                    _player2.SetPlayerIndex(1);
                    AddChild(_player2);
                    _player2.GlobalPosition = spawnPos + new Vector3(2, 0, 0);

                    var p2Class = GameManager.Instance?.SelectedClassP2 ?? BotFrameType.TinCan;
                    _player2.ClassController.SelectClass(p2Class);
                    MetaSaveManager.ApplyPerksToPlayer(_player2.Stats);

                    GD.Print("[SectorManager] Player 2 spawned (gamepad co-op)");
                }
            }

            // Spawn companion next to player
            SpawnCompanion(spawnPos);

            // Create fog of war (rooms start hidden, entrance is discovered)
            var fogManager = new FogOfWarManager();
            fogManager.Name = "FogOfWarManager";
            AddChild(fogManager);

            if (_skipIntro)
            {
                fogManager.Initialize(_generator);
                SetupPostIntro(sectorNum, areaNum, spawnPos);
            }
            else
            {
                // Disable player input during the intro (keep physics alive for gravity)
                if (_player != null)
                {
                    var inputHandler = _player.GetNodeOrNull<PlayerInputHandler>("PlayerInputHandler");
                    inputHandler?.DisableInput();
                }

                // Play the dungeon assembly intro — player watches from the entrance
                _assemblyIntro = new DungeonAssemblyIntro();
                _assemblyIntro.Name = "DungeonAssemblyIntro";
                AddChild(_assemblyIntro);
                _assemblyIntro.Initialize(_generator, fogManager);

                int sn = sectorNum;
                int an = areaNum;
                Vector3 sp = spawnPos;
                _assemblyIntro.IntroFinished += () => OnIntroFinished(sn, an, sp);
                _assemblyIntro.Play();

                // Safety timer — if the intro tween chain breaks for any reason,
                // force-finish after 30 seconds so the player isn't stuck
                GetTree().CreateTimer(30.0).Timeout += () =>
                {
                    if (!_introFinished)
                    {
                        GD.PrintErr("[SectorManager] Intro safety timer fired — forcing finish");
                        OnIntroFinished(sn, an, sp);
                    }
                };

                GD.Print($"[SectorManager] Sector {sectorNum}, Area {areaNum} — playing assembly intro ({_generator.RoomGrid.Count} rooms)");
            }
        }

        private void OnIntroFinished(int sectorNum, int areaNum, Vector3 spawnPos)
        {
            if (_introFinished) return; // Guard against double-call (safety timer + normal finish)
            _introFinished = true;

            // Re-enable player input
            if (_player != null)
            {
                var inputHandler = _player.GetNodeOrNull<PlayerInputHandler>("PlayerInputHandler");
                inputHandler?.EnableInput();
            }

            SetupPostIntro(sectorNum, areaNum, spawnPos);

            if (_assemblyIntro != null && IsInstanceValid(_assemblyIntro))
            {
                _assemblyIntro.QueueFree();
                _assemblyIntro = null;
            }

            GD.Print("[SectorManager] Intro finished — player input re-enabled");
        }

        private void SetupPostIntro(int sectorNum, int areaNum, Vector3 spawnPos)
        {
            // Spawn HUD (with minimap data)
            if (_hudScene != null)
            {
                var hud = _hudScene.Instantiate();
                AddChild(hud);
                CallDeferred(nameof(SetupMinimap), hud);
                GD.Print("[SectorManager] HUD spawned");
            }

            // Spawn gameplay camera — takes over from intro camera
            if (_cameraScene != null)
            {
                var camera = _cameraScene.Instantiate<IsometricCamera>();
                AddChild(camera);

                if (_player2 != null)
                    camera.Initialize(_player, _player2);
                else
                    camera.Initialize(_player);

                GD.Print("[SectorManager] Camera spawned");
            }

            GameManager.Instance?.ChangeState(GameState.InSector);
            GameEvents.OnSectorEntered?.Invoke(sectorNum);

            // Restore player state from previous area/sector
            if (GameManager.Instance?.IsLoadingGame == true)
            {
                GameManager.Instance.IsLoadingGame = false;
                SaveManager.ApplyLoadedState(_player);
            }
            else if (SaveManager.SaveFileExists() && _player != null)
            {
                SaveManager.ApplyTransitionState(_player);
            }

            // Auto-save on sector entry
            if (_player != null)
                SaveManager.SaveGame(_player, sectorNum);

            // Start ambient music (shifts tone with ascension rank)
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlayAscensionAmbience(sectorNum, MetaSaveManager.Data.AscensionRank);

            GD.Print($"[SectorManager] Sector {sectorNum}, Area {areaNum} ready ({_generator.RoomGrid.Count} rooms)");
        }

        private void SpawnCompanion(Vector3 playerSpawnPos)
        {
            string companionId = GameManager.Instance?.ActiveCompanionId;
            if (string.IsNullOrEmpty(companionId)) return;

            var companionData = CompanionRegistry.Get(companionId);
            if (companionData == null) return;

            var scene = GD.Load<PackedScene>(Constants.SCENE_COMPANION);
            if (scene == null)
            {
                GD.PrintErr("[SectorManager] Companion scene not found");
                return;
            }

            var companion = scene.Instantiate<CompanionController>();
            AddChild(companion);
            companion.GlobalPosition = playerSpawnPos + new Vector3(2, 0, 2);
            companion.Initialize(companionData);

            GameEvents.OnCompanionSummoned?.Invoke(companion);
            GD.Print($"[SectorManager] Companion '{companionData.CompanionName}' spawned");
        }

        private void SetupMinimap(Node hud)
        {
            // Find minimap in HUD children (it's added by HUDController._Ready)
            // Use a short delay so HUDController has time to create it
            GetTree().CreateTimer(0.1).Timeout += () =>
            {
                if (hud is HUDController hudCtrl)
                    hudCtrl.SetMinimapData(_generator?.RoomGrid);
            };
        }

        private void SpawnSupportSystems()
        {
            var combatManager = new CombatManager();
            combatManager.Name = "CombatManager";
            AddChild(combatManager);

            var commentaryManager = new CommentaryManager();
            commentaryManager.Name = "CommentaryManager";
            AddChild(commentaryManager);

            var systemMessages = new SystemMessageManager();
            systemMessages.Name = "SystemMessageManager";
            AddChild(systemMessages);

            var audioManager = new AudioManager();
            audioManager.Name = "AudioManager";
            AddChild(audioManager);

            var damageNumbers = new DamageNumberUI();
            damageNumbers.Name = "DamageNumbers";
            AddChild(damageNumbers);

            var combatVfx = new CombatVfxManager();
            combatVfx.Name = "CombatVfxManager";
            AddChild(combatVfx);

            var roomClear = new RoomClearManager();
            roomClear.Name = "RoomClearManager";
            AddChild(roomClear);

            var achievementManager = new AchievementManager();
            achievementManager.Name = "AchievementManager";
            AddChild(achievementManager);

            var liftTimer = new LiftTimer();
            liftTimer.Name = "LiftTimer";
            AddChild(liftTimer);

            var achievementUI = new AchievementNotificationUI();
            achievementUI.Name = "AchievementNotificationUI";
            AddChild(achievementUI);

            var timerUI = new LiftTimerUI();
            timerUI.Name = "LiftTimerUI";
            AddChild(timerUI);

            var axisTroll = new AXISTrollManager();
            axisTroll.Name = "AXISTrollManager";
            AddChild(axisTroll);

            GD.Print("[SectorManager] Support systems spawned");
        }
    }
}
