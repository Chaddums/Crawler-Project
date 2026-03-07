using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    [GlobalClass]
    public partial class EnemyData : Resource
    {
        [Export] public string Id { get; set; } = "";
        [Export] public string EnemyName { get; set; } = "";
        [Export] public EnemyTier Tier { get; set; } = EnemyTier.Normal;
        [Export] public float BaseHealth { get; set; } = 30f;
        [Export] public float BaseDamage { get; set; } = 5f;
        [Export] public float MoveSpeed { get; set; } = 3f;
        [Export] public float AttackRange { get; set; } = 1.5f;
        [Export] public float AttackCooldown { get; set; } = 1.5f;
        [Export] public float AggroRange { get; set; } = 8f;
        [Export] public float Armor { get; set; } = 0f;
        [Export] public int XpReward { get; set; } = 20;
        [Export] public EnemyBehavior Behavior { get; set; } = EnemyBehavior.Melee;

        public StatBlock Stats { get; set; } = new();

        public List<string> AbilityIds { get; set; } = new();
        public LootTableData LootTable { get; set; }

        // Signature loot — specific gear this enemy visibly carries
        public EquipmentData SignatureDrop { get; set; }
        public float SignatureDropChance { get; set; }
        public LootBoxTier? LootBoxDrop { get; set; }
        public float LootBoxDropChance { get; set; }

        // Mesh color for visual identity
        public Color MeshColor { get; set; } = new(0.8f, 0.2f, 0.2f);

        public bool IsBoss => Tier == EnemyTier.Boss;

        public EnemyData() { }

        public EnemyData(string id, string name, EnemyTier tier, float hp, float dmg,
            float speed, int xp)
        {
            Id = id;
            EnemyName = name;
            Tier = tier;
            BaseHealth = hp;
            BaseDamage = dmg;
            MoveSpeed = speed;
            XpReward = xp;
        }

        /// <summary>
        /// Build a StatBlock from this data, applying difficulty scaling.
        /// </summary>
        public StatBlock BuildStats(float difficultyMultiplier = 1f)
        {
            var stats = new StatBlock();
            stats.SetBaseStat(StatType.MaxHealth, BaseHealth * difficultyMultiplier);
            stats.SetBaseStat(StatType.Strength, BaseDamage * difficultyMultiplier);
            stats.SetBaseStat(StatType.MoveSpeed, MoveSpeed);
            stats.SetBaseStat(StatType.Armor, Armor * difficultyMultiplier);
            stats.SetBaseStat(StatType.AttackSpeed, 1f / AttackCooldown);
            return stats;
        }
    }
}
