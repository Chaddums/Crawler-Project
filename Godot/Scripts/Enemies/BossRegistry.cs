using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    public enum BossAbilityType
    {
        GroundSlam,
        ChargeAttack,
        SummonAdds,
        ProjectileBarrage
    }

    public class BossConfig
    {
        public List<BossAbilityType> Abilities { get; set; } = new();
        public float Phase2Threshold { get; set; } = 0.6f;
        public float Phase3Threshold { get; set; } = 0.3f;
    }

    /// <summary>
    /// Defines the 3 boss enemy types with unique stats, abilities, and phase configs.
    /// </summary>
    public static class BossRegistry
    {
        private static readonly Dictionary<string, BossConfig> _bossConfigs = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<string, BossConfig> Configs => _bossConfigs;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildGoblinOverseer();
            BuildMimicKing();
            BuildAnnouncerChampion();

            GD.Print($"[BossRegistry] Initialized {_bossConfigs.Count} boss types");
        }

        public static BossConfig GetConfig(string bossId)
        {
            return _bossConfigs.TryGetValue(bossId, out var cfg) ? cfg : null;
        }

        private static void BuildGoblinOverseer()
        {
            var e = new EnemyData("goblin_overseer", "Goblin Overseer", EnemyTier.Boss, 200, 8, 3.5f, 100)
            {
                AttackRange = 2f,
                AttackCooldown = 1.8f,
                AggroRange = 12f,
                Armor = 3,
                MeshColor = new Color(0.45f, 0.55f, 0.25f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_goblin", MinDrops = 2, MaxDrops = 4 };
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["goblin_overseer"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.GroundSlam, BossAbilityType.SummonAdds }
            };
        }

        private static void BuildMimicKing()
        {
            var e = new EnemyData("mimic_king", "Dungeon Mimic King", EnemyTier.Boss, 400, 15, 2.5f, 200)
            {
                AttackRange = 2f,
                AttackCooldown = 2.2f,
                AggroRange = 10f,
                Armor = 8,
                MeshColor = new Color(0.8f, 0.7f, 0.2f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_mimic", MinDrops = 3, MaxDrops = 5 };
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["mimic_king"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.ChargeAttack, BossAbilityType.GroundSlam }
            };
        }

        private static void BuildAnnouncerChampion()
        {
            var e = new EnemyData("announcer_champion", "The Announcer's Champion", EnemyTier.Boss, 600, 20, 4f, 350)
            {
                AttackRange = 2.5f,
                AttackCooldown = 1.5f,
                AggroRange = 14f,
                Armor = 5,
                MeshColor = new Color(0.5f, 0.3f, 0.6f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_champion", MinDrops = 3, MaxDrops = 6 };
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["announcer_champion"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.ProjectileBarrage, BossAbilityType.ChargeAttack, BossAbilityType.SummonAdds }
            };
        }
    }
}
