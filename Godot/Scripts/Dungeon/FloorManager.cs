using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Manages a dungeon floor. Spawns player, HUD, camera, enemies,
    /// and support systems (CombatManager, CommentaryManager, SystemMessageManager).
    /// Uses DungeonGenerator for procedural room layout.
    /// </summary>
    public partial class FloorManager : Node3D
    {
        [Export] private PackedScene _playerScene;
        [Export] private PackedScene _hudScene;
        [Export] private PackedScene _cameraScene;
        private PlayerController _player;

        public override void _Ready()
        {
            // Initialize floor data registry
            FloorDataRegistry.Initialize();

            int floorNum = GameManager.Instance?.CurrentFloor ?? 1;
            var floorData = FloorDataRegistry.GetFloor(floorNum);

            // Generate dungeon
            var generator = new DungeonGenerator(floorData);
            var spawnPos = generator.Generate(this);

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

            // Spawn HUD
            if (_hudScene != null)
            {
                var hud = _hudScene.Instantiate();
                AddChild(hud);
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

            GD.Print($"[FloorManager] Floor {floorNum} ready ({generator.RoomGrid.Count} rooms)");
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

            GD.Print("[FloorManager] Support systems spawned");
        }
    }
}
