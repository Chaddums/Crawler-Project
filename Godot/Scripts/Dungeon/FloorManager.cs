using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Manages a dungeon floor. Spawns the player at the spawn point
    /// and instantiates the HUD.
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

            GameManager.Instance?.ChangeState(GameState.InFloor);
            GameEvents.OnFloorEntered?.Invoke(1);
        }
    }
}
