using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// DCC-flavor system messages: "New achievement!", "ATTENTION CRAWLERS:", "Warning:" etc.
    /// Listens for game events and fires OnSystemMessage.
    /// </summary>
    public partial class SystemMessageManager : Node
    {
        private int _killCount;
        private int _levelUpsThisFloor;

        public override void _Ready()
        {
            ServiceLocator.Register(this);

            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;
            GameEvents.OnFloorEntered += OnFloorEntered;
            GameEvents.OnItemPickedUp += OnItemPickedUp;
            GameEvents.OnPlayerDeath += OnPlayerDeath;
        }

        private void OnEnemyKilled(Node enemy)
        {
            _killCount++;

            // Achievement milestones
            if (_killCount == 1)
                FireAchievement("First Blood", "You killed your first enemy. The dungeon notices.");
            else if (_killCount == 10)
                FireAchievement("Getting Warmed Up", "10 kills. The dungeon is mildly impressed.");
            else if (_killCount == 50)
                FireAchievement("Dungeon Menace", "50 kills! The other crawlers are watching.");
        }

        private void OnPlayerLevelUp(int level)
        {
            _levelUpsThisFloor++;

            FireSystemMessage("Announcement",
                $"ATTENTION CRAWLERS: A level-up has been detected on this floor. Crawler 'Carl' is now level {level}.");

            if (level == 5)
                FireAchievement("Settling In", "Level 5. You might actually survive this floor.");
            else if (level == 10)
                FireAchievement("Double Digits", "Level 10. Don't let it go to your head.");
        }

        private void OnFloorEntered(int floor)
        {
            _killCount = 0;
            _levelUpsThisFloor = 0;

            FireSystemMessage("Floor",
                $"Now entering Floor {floor}. Difficulty has been adjusted. Good luck, crawler.");
        }

        private void OnItemPickedUp(Godot.Resource item)
        {
            // Occasional flavor messages about loot
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

        private void FireAchievement(string id, string description)
        {
            GameEvents.OnAchievementUnlocked?.Invoke(id);
            FireSystemMessage("Achievement", $"New achievement: \"{id}\" — {description}");
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
            GameEvents.OnFloorEntered -= OnFloorEntered;
            GameEvents.OnItemPickedUp -= OnItemPickedUp;
            GameEvents.OnPlayerDeath -= OnPlayerDeath;
            ServiceLocator.Unregister<SystemMessageManager>();
        }
    }
}
