namespace JunkbotArena
{
    public enum AchievementCategory
    {
        Combat,
        Exploration,
        Survival,
        Class,
        Meta
    }

    public class AchievementData
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string SnarkMessage { get; set; }
        public AchievementCategory Category { get; set; }
        public bool IsHidden { get; set; }
        public LootBoxTier? RewardTier { get; set; }

        public AchievementData(string id, string title, string description, string snark,
            AchievementCategory category, bool hidden = false, LootBoxTier? reward = null)
        {
            Id = id;
            Title = title;
            Description = description;
            SnarkMessage = snark;
            Category = category;
            IsHidden = hidden;
            RewardTier = reward;
        }
    }
}
