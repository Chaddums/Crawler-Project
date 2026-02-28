using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Manages a dungeon floor. Spawns player, companion, HUD, camera, enemies,
    /// and support systems (CombatManager, CommentaryManager, SystemMessageManager, AudioManager).
    /// Uses DungeonGenerator for procedural room layout.
    /// </summary>
    public partial class FloorManager : Node3D
    {
        [Export] private PackedScene _playerScene;
        [Export] private PackedScene _hudScene;
        [Export] private PackedScene _cameraScene;
        private PlayerController _player;
        private DungeonGenerator _generator;

        public DungeonGenerator Generator => _generator;

        public override void _Ready()
        {
            // Initialize floor data registry
            FloorDataRegistry.Initialize();

            int floorNum = GameManager.Instance?.CurrentFloor ?? 1;
            int areaNum = GameManager.Instance?.CurrentArea ?? 1;
            var floorData = FloorDataRegistry.GetFloor(floorNum);

            // Scale difficulty up slightly per area within a floor
            if (areaNum > 1)
                floorData.DifficultyMultiplier *= 1f + (areaNum - 1) * 0.15f;

            // Generate dungeon
            _generator = new DungeonGenerator(floorData);
            var spawnPos = _generator.Generate(this);

            // Spawn player
            if (_playerScene != null)
            {
                _player = _playerScene.Instantiate<PlayerController>();
                AddChild(_player);
                _player.GlobalPosition = spawnPos;

                GD.Print("[FloorManager] Player spawned");

                // Apply selected class
                var selectedClass = GameManager.Instance?.SelectedClass ?? CrawlerClassName.BoringOlFighter;
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

                GD.Print("[FloorManager] HUD spawned");
            }

            // Spawn camera
            if (_cameraScene != null)
            {
                var camera = _cameraScene.Instantiate<IsometricCamera>();
                AddChild(camera);
                camera.Initialize(_player);
                GD.Print("[FloorManager] Camera spawned");
            }

            // Spawn support systems
            SpawnSupportSystems();

            GameManager.Instance?.ChangeState(GameState.InFloor);
            GameEvents.OnFloorEntered?.Invoke(floorNum);

            // Apply saved state if loading
            if (GameManager.Instance?.IsLoadingGame == true)
            {
                GameManager.Instance.IsLoadingGame = false;
                SaveManager.ApplyLoadedState(_player);
            }

            // Auto-save on floor entry
            if (_player != null)
                SaveManager.SaveGame(_player, floorNum);

            GD.Print($"[FloorManager] Floor {floorNum}, Area {areaNum} ready ({_generator.RoomGrid.Count} rooms)");
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
                GD.PrintErr("[FloorManager] Companion scene not found");
                return;
            }

            var companion = scene.Instantiate<CompanionController>();
            AddChild(companion);
            companion.GlobalPosition = playerSpawnPos + new Vector3(2, 0, 2);
            companion.Initialize(companionData);

            GameEvents.OnCompanionSummoned?.Invoke(companion);
            GD.Print($"[FloorManager] Companion '{companionData.CompanionName}' spawned");
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

            GD.Print("[FloorManager] Support systems spawned");
        }
    }
}
