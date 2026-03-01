using System.Collections.Generic;
using Godot;

namespace JunkbotArena
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

            BuildCorruptedSentry();
            BuildScrapHydra();
            BuildAxisAvatar();

            GD.Print($"[BossRegistry] Initialized {_bossConfigs.Count} boss types");
        }

        public static BossConfig GetConfig(string bossId)
        {
            return _bossConfigs.TryGetValue(bossId, out var cfg) ? cfg : null;
        }

        private static void BuildCorruptedSentry()
        {
            var e = new EnemyData("corrupted_sentry", StringLoader.Get("bosses.corrupted_sentry"), EnemyTier.Boss, 200, 8, 3.5f, 100)
            {
                AttackRange = 2f,
                AttackCooldown = 1.8f,
                AggroRange = 12f,
                Armor = 3,
                MeshColor = new Color(0.45f, 0.55f, 0.25f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_sentry", MinDrops = 2, MaxDrops = 4 };
            PopulateCorruptedSentryLoot(e.LootTable);
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["corrupted_sentry"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.GroundSlam, BossAbilityType.SummonAdds }
            };
        }

        private static void BuildScrapHydra()
        {
            var e = new EnemyData("scrap_hydra", StringLoader.Get("bosses.scrap_hydra"), EnemyTier.Boss, 400, 15, 2.5f, 200)
            {
                AttackRange = 2f,
                AttackCooldown = 2.2f,
                AggroRange = 10f,
                Armor = 8,
                MeshColor = new Color(0.8f, 0.7f, 0.2f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_hydra", MinDrops = 3, MaxDrops = 5 };
            PopulateScrapHydraLoot(e.LootTable);
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["scrap_hydra"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.ChargeAttack, BossAbilityType.GroundSlam }
            };
        }

        private static void BuildAxisAvatar()
        {
            var e = new EnemyData("axis_avatar", StringLoader.Get("bosses.axis_avatar"), EnemyTier.Boss, 600, 20, 4f, 350)
            {
                AttackRange = 2.5f,
                AttackCooldown = 1.5f,
                AggroRange = 14f,
                Armor = 5,
                MeshColor = new Color(0.5f, 0.3f, 0.6f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_axis", MinDrops = 3, MaxDrops = 6 };
            PopulateAxisAvatarLoot(e.LootTable);
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["axis_avatar"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.ProjectileBarrage, BossAbilityType.ChargeAttack, BossAbilityType.SummonAdds }
            };
        }

        // --- Boss loot table population ---

        private static void PopulateCorruptedSentryLoot(LootTableData table)
        {
            // Medium/large potions + rare gear
            table.AddEntry(ConsumableRegistry.Get("potion_health_medium"), weight: 80);
            table.AddEntry(ConsumableRegistry.Get("potion_health_large"), weight: 40);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_medium"), weight: 60);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_large"), weight: 30);
            table.AddEntry(ConsumableRegistry.Get("elixir_fortitude"), weight: 20);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 30);
        }

        private static void PopulateScrapHydraLoot(LootTableData table)
        {
            // Large potions + rare/epic gear
            table.AddEntry(ConsumableRegistry.Get("potion_health_large"), weight: 80);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_large"), weight: 60);
            table.AddEntry(ConsumableRegistry.Get("elixir_fortitude"), weight: 25);
            table.AddEntry(ConsumableRegistry.Get("overclock_injector"), weight: 25);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 40);
        }

        private static void PopulateAxisAvatarLoot(LootTableData table)
        {
            // Large potions + epic gear + guaranteed loot box
            table.AddEntry(ConsumableRegistry.Get("potion_health_large"), weight: 70);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_large"), weight: 50);
            table.AddEntry(ConsumableRegistry.Get("elixir_fortitude"), weight: 30);
            table.AddEntry(ConsumableRegistry.Get("overclock_injector"), weight: 30);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 50);
            // Guaranteed loot box drop
            var lootBoxData = ItemRegistry.GetItem("lootbox_gold");
            if (lootBoxData != null)
                table.AddEntry(lootBoxData, weight: 200);
        }
    }
}
