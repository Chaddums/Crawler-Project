using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Defines a companion's stats, appearance, and behavior.
    /// </summary>
    public class CompanionData
    {
        public string Id { get; set; } = "";
        public string CompanionName { get; set; } = "";
        public string Description { get; set; } = "";
        public float BaseHealth { get; set; } = 50f;
        public float BaseDamage { get; set; } = 5f;
        public float MoveSpeed { get; set; } = 7f;
        public float AttackRange { get; set; } = 1.5f;
        public float AttackCooldown { get; set; } = 1.2f;
        public float FollowDistance { get; set; } = 3f;
        public float AggroRange { get; set; } = 8f;
        public float Armor { get; set; } = 0f;
        public Color MeshColor { get; set; } = new(0.8f, 0.6f, 0.2f);
        public Vector3 MeshScale { get; set; } = Vector3.One;

        public CompanionData() { }

        public StatBlock BuildStats()
        {
            var stats = new StatBlock();
            stats.SetBaseStat(StatType.MaxHealth, BaseHealth);
            stats.SetBaseStat(StatType.Strength, BaseDamage);
            stats.SetBaseStat(StatType.MoveSpeed, MoveSpeed);
            stats.SetBaseStat(StatType.Armor, Armor);
            stats.SetBaseStat(StatType.AttackSpeed, 1f / AttackCooldown);
            return stats;
        }
    }
}
