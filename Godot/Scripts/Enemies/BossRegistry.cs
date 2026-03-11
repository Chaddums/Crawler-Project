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
            BuildRustTitan();
            BuildScrapHydra();
            BuildNullWarden();
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

        private static void BuildRustTitan()
        {
            var e = new EnemyData("rust_titan", StringLoader.Get("bosses.rust_titan"), EnemyTier.Boss, 300, 12, 2.8f, 150)
            {
                AttackRange = 2.5f,
                AttackCooldown = 2.0f,
                AggroRange = 11f,
                Armor = 6,
                MeshColor = new Color(0.6f, 0.35f, 0.15f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_titan", MinDrops = 2, MaxDrops = 4 };
            PopulateRustTitanLoot(e.LootTable);
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["rust_titan"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.ChargeAttack, BossAbilityType.GroundSlam }
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

        private static void BuildNullWarden()
        {
            var e = new EnemyData("null_warden", StringLoader.Get("bosses.null_warden"), EnemyTier.Boss, 500, 18, 3.5f, 280)
            {
                AttackRange = 2.5f,
                AttackCooldown = 1.6f,
                AggroRange = 13f,
                Armor = 7,
                MeshColor = new Color(0.25f, 0.35f, 0.55f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_warden", MinDrops = 3, MaxDrops = 5 };
            PopulateNullWardenLoot(e.LootTable);
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["null_warden"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.ProjectileBarrage, BossAbilityType.SummonAdds, BossAbilityType.GroundSlam }
            };
        }

        private static void BuildAxisAvatar()
        {
            var e = new EnemyData("axis_avatar", StringLoader.Get("bosses.axis_avatar"), EnemyTier.Boss, 800, 25, 0f, 500)
            {
                AttackRange = 20f,     // stationary boss, attacks entire arena
                AttackCooldown = 2.0f,
                AggroRange = 30f,      // always sees the player
                Armor = 8,
                MeshColor = new Color(0.35f, 0.15f, 0.55f)
            };
            e.LootTable = new LootTableData { Id = "loot_boss_axis", MinDrops = 3, MaxDrops = 6 };
            PopulateAxisAvatarLoot(e.LootTable);
            EnemyRegistry.RegisterEnemy(e);

            _bossConfigs["axis_avatar"] = new BossConfig
            {
                Abilities = new() { BossAbilityType.ProjectileBarrage, BossAbilityType.GroundSlam, BossAbilityType.SummonAdds },
                Phase2Threshold = 0.55f,
                Phase3Threshold = 0.25f
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

        private static void PopulateRustTitanLoot(LootTableData table)
        {
            table.AddEntry(ConsumableRegistry.Get("potion_health_medium"), weight: 70);
            table.AddEntry(ConsumableRegistry.Get("potion_health_large"), weight: 50);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_medium"), weight: 50);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_large"), weight: 35);
            table.AddEntry(ConsumableRegistry.Get("elixir_fortitude"), weight: 20);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 35);
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

        private static void PopulateNullWardenLoot(LootTableData table)
        {
            table.AddEntry(ConsumableRegistry.Get("potion_health_large"), weight: 80);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_large"), weight: 60);
            table.AddEntry(ConsumableRegistry.Get("elixir_fortitude"), weight: 30);
            table.AddEntry(ConsumableRegistry.Get("overclock_injector"), weight: 30);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 45);
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
