using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// AXIS-flavor system messages: "ATTENTION SCRAPPERS:", "Warning:", etc.
    /// Handles commentary flavor text. Achievement tracking is now in AchievementManager.
    /// </summary>
    public partial class SystemMessageManager : Node
    {
        public override void _Ready()
        {
            ServiceLocator.Register(this);

            GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;
            GameEvents.OnSectorEntered += OnSectorEntered;
            GameEvents.OnItemPickedUp += OnItemPickedUp;
            GameEvents.OnPlayerDeath += OnPlayerDeath;
        }

        private void OnPlayerLevelUp(int level)
        {
            FireSystemMessage("Announcement",
                $"ATTENTION SCRAPPERS: Power surge detected. Unit upgraded to level {level}. Adjusting difficulty.");
        }

        private void OnSectorEntered(int sector)
        {
            FireSystemMessage("Sector",
                $"Now entering Sector {sector}. AXIS has recalibrated hostiles. Good luck, scrapper.");
        }

        private void OnItemPickedUp(Godot.Resource item)
        {
            if (GD.Randi() % 5 == 0)
            {
                FireSystemMessage("Loot",
                    "WARNING: Excessive scrap hoarding detected. AXIS may redistribute your inventory.");
            }
        }

        private void OnPlayerDeath(Node player)
        {
            FireSystemMessage("Death",
                "UNIT OFFLINE. Viewer ratings spiked by 340%. Your scrap has been redistributed. Thank you for participating.");
        }

        private void FireSystemMessage(string category, string message)
        {
            GameEvents.OnSystemMessage?.Invoke(category, message);
            GD.Print($"[System:{category}] {message}");
        }

        public override void _ExitTree()
        {
            GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
            GameEvents.OnSectorEntered -= OnSectorEntered;
            GameEvents.OnItemPickedUp -= OnItemPickedUp;
            GameEvents.OnPlayerDeath -= OnPlayerDeath;
            ServiceLocator.Unregister<SystemMessageManager>();
        }
    }
}
