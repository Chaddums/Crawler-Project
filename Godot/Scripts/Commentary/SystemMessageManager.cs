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
                StringLoader.Get("systemMessages.levelUp", ("{level}", level)));
        }

        private void OnSectorEntered(int sector)
        {
            FireSystemMessage("Sector",
                StringLoader.Get("systemMessages.sectorEnter", ("{sector}", sector)));
        }

        private void OnItemPickedUp(Godot.Resource item)
        {
            if (GD.Randi() % 5 == 0)
            {
                FireSystemMessage("Loot",
                    StringLoader.Get("systemMessages.lootWarning"));
            }
        }

        private void OnPlayerDeath(Node player)
        {
            FireSystemMessage("Death",
                StringLoader.Get("systemMessages.death"));
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
