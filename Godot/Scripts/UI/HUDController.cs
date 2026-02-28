using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Controls the in-game HUD. Finds the player and binds the health bar.
    /// Attached to the HUD CanvasLayer node.
    /// </summary>
    public partial class HUDController : CanvasLayer
    {
        [Export] private HealthBarUI _healthBar;

        private bool _bound;

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
    }
}
