using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Manages a dungeon floor. Spawns player, HUD, camera, enemies,
    /// and support systems (CombatManager, CommentaryManager, SystemMessageManager).
    /// </summary>
    public partial class FloorManager : Node3D
    {
        [Export] private PackedScene _playerScene;
        [Export] private PackedScene _hudScene;
        [Export] private PackedScene _cameraScene;
        private PlayerController _player;

        public override void _Ready()
        {
            // Spawn player
            if (_playerScene != null)
            {
                _player = _playerScene.Instantiate<PlayerController>();
                AddChild(_player);

                var spawnPoint = GetNodeOrNull<Marker3D>("SpawnPoint");
                if (spawnPoint != null)
                    _player.GlobalPosition = spawnPoint.GlobalPosition;
                else
                    _player.GlobalPosition = new Vector3(0, 0.9f, 0);

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

            // Spawn test enemies
            SpawnFloor1Enemies();

            GameManager.Instance?.ChangeState(GameState.InFloor);
            GameEvents.OnFloorEntered?.Invoke(1);
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

        private void SpawnFloor1Enemies()
        {
            var enemyScene = GD.Load<PackedScene>(Constants.SCENE_ENEMY);
            if (enemyScene == null)
            {
                GD.PrintErr("[FloorManager] Could not load enemy scene");
                return;
            }

            // 3 training dummies
            SpawnEnemy(enemyScene, "training_dummy", new Vector3(3, 0.9f, -3));
            SpawnEnemy(enemyScene, "training_dummy", new Vector3(-3, 0.9f, -3));
            SpawnEnemy(enemyScene, "training_dummy", new Vector3(0, 0.9f, -5));

            // 2 crawler rats (far from spawn so they don't aggro immediately)
            SpawnEnemy(enemyScene, "crawler_rat", new Vector3(7, 0.9f, 7));
            SpawnEnemy(enemyScene, "crawler_rat", new Vector3(-7, 0.9f, 7));

            GD.Print("[FloorManager] Floor 1 enemies spawned");
        }

        private void SpawnEnemy(PackedScene scene, string enemyId, Vector3 position)
        {
            var data = EnemyRegistry.GetEnemy(enemyId);
            if (data == null)
            {
                GD.PrintErr($"[FloorManager] Unknown enemy: {enemyId}");
                return;
            }

            var enemy = scene.Instantiate<EnemyController>();
            AddChild(enemy);
            enemy.GlobalPosition = position;
            enemy.Initialize(data);
        }
    }
}
