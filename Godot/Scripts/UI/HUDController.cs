using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Controls the in-game HUD. Finds the player and binds the health bar.
    /// Spawns inventory, passive tree, pause menu, character sheet, and minimap overlays.
    /// Attached to the HUD CanvasLayer node.
    /// </summary>
    public partial class HUDController : CanvasLayer
    {
        [Export] private HealthBarUI _healthBar;

        private bool _bound;
        private InventoryUI _inventoryUI;
        private PassiveTreeUI _passiveTreeUI;
        private PauseMenuUI _pauseMenuUI;
        private CharacterSheetUI _characterSheetUI;
        private MinimapUI _minimap;
        private Label _floorAreaLabel;

        public override void _Ready()
        {
            // Spawn inventory overlay
            _inventoryUI = new InventoryUI();
            _inventoryUI.Name = "InventoryUI";
            GetTree().Root.CallDeferred("add_child", _inventoryUI);

            // Spawn passive tree overlay
            _passiveTreeUI = new PassiveTreeUI();
            _passiveTreeUI.Name = "PassiveTreeUI";
            GetTree().Root.CallDeferred("add_child", _passiveTreeUI);

            // Spawn pause menu overlay
            _pauseMenuUI = new PauseMenuUI();
            _pauseMenuUI.Name = "PauseMenuUI";
            GetTree().Root.CallDeferred("add_child", _pauseMenuUI);

            // Spawn character sheet overlay
            _characterSheetUI = new CharacterSheetUI();
            _characterSheetUI.Name = "CharacterSheetUI";
            GetTree().Root.CallDeferred("add_child", _characterSheetUI);

            // Minimap (top-right, below floor/area label)
            _minimap = new MinimapUI();
            _minimap.Name = "Minimap";
            _minimap.Position = new Vector2(1700, 60);
            _minimap.Size = new Vector2(200, 200);
            AddChild(_minimap);

            // Floor/Area label (top-right)
            _floorAreaLabel = new Label();
            _floorAreaLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _floorAreaLabel.GrowHorizontal = Control.GrowDirection.Begin;
            _floorAreaLabel.Position = new Vector2(1600, 20);
            _floorAreaLabel.Size = new Vector2(300, 30);
            _floorAreaLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _floorAreaLabel.AddThemeFontSizeOverride("font_size", 18);
            _floorAreaLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.65f, 0.5f));
            AddChild(_floorAreaLabel);
            UpdateFloorAreaLabel();
        }

        public void SetMinimapData(IReadOnlyDictionary<Vector2I, RoomType> roomGrid)
        {
            _minimap?.SetRoomGrid(roomGrid);
        }

        public override void _Process(double delta)
        {
            if (_bound) return;

            // Wait for player to register
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                if (_healthBar != null && player.Health != null)
                {
                    _healthBar.Bind(player.Health);
                    _bound = true;
                    GD.Print("[HUDController] Health bar bound to player");
                }
            }
        }

        private void UpdateFloorAreaLabel()
        {
            int floor = GameManager.Instance?.CurrentFloor ?? 1;
            int area = GameManager.Instance?.CurrentArea ?? 1;
            _floorAreaLabel.Text = $"Floor {floor} - Area {area}";
        }
    }
}
