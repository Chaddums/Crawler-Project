using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// DCC-flavor system messages: "ATTENTION CRAWLERS:", "Warning:", etc.
    /// Handles commentary flavor text. Achievement tracking is now in AchievementManager.
    /// </summary>
    public partial class SystemMessageManager : Node
    {
        public override void _Ready()
        {
            ServiceLocator.Register(this);

            GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;
            GameEvents.OnFloorEntered += OnFloorEntered;
            GameEvents.OnItemPickedUp += OnItemPickedUp;
            GameEvents.OnPlayerDeath += OnPlayerDeath;
        }

        private void OnPlayerLevelUp(int level)
        {
            FireSystemMessage("Announcement",
                $"ATTENTION CRAWLERS: A level-up has been detected on this floor. Crawler 'Carl' is now level {level}.");
        }

        private void OnFloorEntered(int floor)
        {
            FireSystemMessage("Floor",
                $"Now entering Floor {floor}. Difficulty has been adjusted. Good luck, crawler.");
        }

        private void OnItemPickedUp(Godot.Resource item)
        {
            if (GD.Randi() % 5 == 0)
            {
                FireSystemMessage("Loot",
                    "WARNING: Excessive loot hoarding may attract unwanted attention from the dungeon.");
            }
        }

        private void OnPlayerDeath(Node player)
        {
            FireSystemMessage("Death",
                "CRAWLER DOWN. The audience viewership has spiked by 340%. Your sacrifice is appreciated.");
        }

        private void FireSystemMessage(string category, string message)
        {
            GameEvents.OnSystemMessage?.Invoke(category, message);
            GD.Print($"[System:{category}] {message}");
        }

        public override void _ExitTree()
        {
            GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
            GameEvents.OnFloorEntered -= OnFloorEntered;
            GameEvents.OnItemPickedUp -= OnItemPickedUp;
            GameEvents.OnPlayerDeath -= OnPlayerDeath;
            ServiceLocator.Unregister<SystemMessageManager>();
        }
    }
}
