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
        private PlayerController _player;
        private DungeonGenerator _generator;

        public DungeonGenerator Generator => _generator;

        public override void _Ready()
        {
            // Initialize sector data registry
            SectorDataRegistry.Initialize();

            int sectorNum = GameManager.Instance?.CurrentSector ?? 1;
            int areaNum = GameManager.Instance?.CurrentArea ?? 1;
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
                    : new System.Collections.Generic.List<HazardType>()
            };

            // Scale difficulty up slightly per area within a sector
            if (areaNum > 1)
                sectorData.DifficultyMultiplier *= 1f + (areaNum - 1) * 0.15f;

            // Generate dungeon
            _generator = new DungeonGenerator(sectorData);
            var spawnPos = _generator.Generate(this);

            // Fog of war — must initialize before player spawns so rooms start hidden
            var fogManager = new FogOfWarManager();
            fogManager.Name = "FogOfWarManager";
            AddChild(fogManager);
            fogManager.Initialize(_generator);

            // Spawn player
            if (_playerScene != null)
            {
                _player = _playerScene.Instantiate<PlayerController>();
                AddChild(_player);
                _player.GlobalPosition = spawnPos;

                GD.Print("[SectorManager] Player spawned");

                // Apply selected class
                var selectedClass = GameManager.Instance?.SelectedClass ?? BotFrameType.TinCan;
                _player.ClassController.SelectClass(selectedClass);
            }

            // Spawn companion
            SpawnCompanion(spawnPos);

            // Spawn HUD (with minimap data)
            if (_hudScene != null)
            {
                var hud = _hudScene.Instantiate();
                AddChild(hud);

                // Pass room grid to minimap via deferred call (HUD needs to _Ready first)
                CallDeferred(nameof(SetupMinimap), hud);

                GD.Print("[SectorManager] HUD spawned");
            }

            // Spawn camera
            if (_cameraScene != null)
            {
                var camera = _cameraScene.Instantiate<IsometricCamera>();
                AddChild(camera);
                camera.Initialize(_player);
                GD.Print("[SectorManager] Camera spawned");
            }

            // Spawn support systems
            SpawnSupportSystems();

            GameManager.Instance?.ChangeState(GameState.InSector);
            GameEvents.OnSectorEntered?.Invoke(sectorNum);

            // Restore player state from previous area/sector
            if (GameManager.Instance?.IsLoadingGame == true)
            {
                // Full restore from save file (Continue Game)
                GameManager.Instance.IsLoadingGame = false;
                SaveManager.ApplyLoadedState(_player);
            }
            else if (SaveManager.SaveFileExists() && _player != null)
            {
                // Normal transition — restore inventory, equipment, level, etc.
                SaveManager.ApplyTransitionState(_player);
            }

            // Auto-save on sector entry
            if (_player != null)
                SaveManager.SaveGame(_player, sectorNum);

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
