using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Static registry of ~25 AXIS-flavored achievements across categories.
    /// </summary>
    public static class AchievementRegistry
    {
        private static readonly Dictionary<string, AchievementData> _achievements = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<string, AchievementData> All => _achievements;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // -- Combat --

            Register(new AchievementData(
                "first_blood", StringLoader.Get("achievements.first_blood.title"), StringLoader.Get("achievements.first_blood.description"),
                StringLoader.Get("achievements.first_blood.commentary"),
                AchievementCategory.Combat, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "getting_warmed_up", StringLoader.Get("achievements.getting_warmed_up.title"), StringLoader.Get("achievements.getting_warmed_up.description"),
                StringLoader.Get("achievements.getting_warmed_up.commentary"),
                AchievementCategory.Combat, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "dungeon_menace", StringLoader.Get("achievements.dungeon_menace.title"), StringLoader.Get("achievements.dungeon_menace.description"),
                StringLoader.Get("achievements.dungeon_menace.commentary"),
                AchievementCategory.Combat, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "boss_slayer", StringLoader.Get("achievements.boss_slayer.title"), StringLoader.Get("achievements.boss_slayer.description"),
                StringLoader.Get("achievements.boss_slayer.commentary"),
                AchievementCategory.Combat, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "combo_master", StringLoader.Get("achievements.combo_master.title"), StringLoader.Get("achievements.combo_master.description"),
                StringLoader.Get("achievements.combo_master.commentary"),
                AchievementCategory.Combat, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "overkill", StringLoader.Get("achievements.overkill.title"), StringLoader.Get("achievements.overkill.description"),
                StringLoader.Get("achievements.overkill.commentary"),
                AchievementCategory.Combat, reward: LootBoxTier.Diamond));

            Register(new AchievementData(
                "pacifist_floor", StringLoader.Get("achievements.pacifist_floor.title"), StringLoader.Get("achievements.pacifist_floor.description"),
                StringLoader.Get("achievements.pacifist_floor.commentary"),
                AchievementCategory.Combat, hidden: true, reward: LootBoxTier.Gold));

            // -- Exploration --

            Register(new AchievementData(
                "floor_2", StringLoader.Get("achievements.floor_2.title"), StringLoader.Get("achievements.floor_2.description"),
                StringLoader.Get("achievements.floor_2.commentary"),
                AchievementCategory.Exploration, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "floor_5", StringLoader.Get("achievements.floor_5.title"), StringLoader.Get("achievements.floor_5.description"),
                StringLoader.Get("achievements.floor_5.commentary"),
                AchievementCategory.Exploration, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "treasure_hunter", StringLoader.Get("achievements.treasure_hunter.title"), StringLoader.Get("achievements.treasure_hunter.description"),
                StringLoader.Get("achievements.treasure_hunter.commentary"),
                AchievementCategory.Exploration, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "completionist", StringLoader.Get("achievements.completionist.title"), StringLoader.Get("achievements.completionist.description"),
                StringLoader.Get("achievements.completionist.commentary"),
                AchievementCategory.Exploration, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "stairwell_rush", StringLoader.Get("achievements.stairwell_rush.title"), StringLoader.Get("achievements.stairwell_rush.description"),
                StringLoader.Get("achievements.stairwell_rush.commentary"),
                AchievementCategory.Exploration, hidden: true, reward: LootBoxTier.Gold));

            // -- Survival --

            Register(new AchievementData(
                "close_call", StringLoader.Get("achievements.close_call.title"), StringLoader.Get("achievements.close_call.description"),
                StringLoader.Get("achievements.close_call.commentary"),
                AchievementCategory.Survival, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "potion_addict", StringLoader.Get("achievements.potion_addict.title"), StringLoader.Get("achievements.potion_addict.description"),
                StringLoader.Get("achievements.potion_addict.commentary"),
                AchievementCategory.Survival, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "iron_frame", StringLoader.Get("achievements.iron_frame.title"), StringLoader.Get("achievements.iron_frame.description"),
                StringLoader.Get("achievements.iron_frame.commentary"),
                AchievementCategory.Survival, reward: LootBoxTier.Gold));

            Register(new AchievementData(
                "back_from_the_brink", StringLoader.Get("achievements.back_from_the_brink.title"), StringLoader.Get("achievements.back_from_the_brink.description"),
                StringLoader.Get("achievements.back_from_the_brink.commentary"),
                AchievementCategory.Survival, reward: LootBoxTier.Silver));

            // -- Class --

            Register(new AchievementData(
                "class_chosen", StringLoader.Get("achievements.class_chosen.title"), StringLoader.Get("achievements.class_chosen.description"),
                StringLoader.Get("achievements.class_chosen.commentary"),
                AchievementCategory.Class, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "spell_slinger", StringLoader.Get("achievements.spell_slinger.title"), StringLoader.Get("achievements.spell_slinger.description"),
                StringLoader.Get("achievements.spell_slinger.commentary"),
                AchievementCategory.Class, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "critical_streak", StringLoader.Get("achievements.critical_streak.title"), StringLoader.Get("achievements.critical_streak.description"),
                StringLoader.Get("achievements.critical_streak.commentary"),
                AchievementCategory.Class, reward: LootBoxTier.Silver));

            // -- Meta --

            Register(new AchievementData(
                "hoarder", StringLoader.Get("achievements.hoarder.title"), StringLoader.Get("achievements.hoarder.description"),
                StringLoader.Get("achievements.hoarder.commentary"),
                AchievementCategory.Meta, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "well_equipped", StringLoader.Get("achievements.well_equipped.title"), StringLoader.Get("achievements.well_equipped.description"),
                StringLoader.Get("achievements.well_equipped.commentary"),
                AchievementCategory.Meta, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "achievement_hunter", StringLoader.Get("achievements.achievement_hunter.title"), StringLoader.Get("achievements.achievement_hunter.description"),
                StringLoader.Get("achievements.achievement_hunter.commentary"),
                AchievementCategory.Meta, reward: LootBoxTier.Diamond));

            Register(new AchievementData(
                "speed_runner", StringLoader.Get("achievements.speed_runner.title"), StringLoader.Get("achievements.speed_runner.description"),
                StringLoader.Get("achievements.speed_runner.commentary"),
                AchievementCategory.Meta, hidden: true, reward: LootBoxTier.Legendary));

            Register(new AchievementData(
                "level_5", StringLoader.Get("achievements.level_5.title"), StringLoader.Get("achievements.level_5.description"),
                StringLoader.Get("achievements.level_5.commentary"),
                AchievementCategory.Class, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "level_10", StringLoader.Get("achievements.level_10.title"), StringLoader.Get("achievements.level_10.description"),
                StringLoader.Get("achievements.level_10.commentary"),
                AchievementCategory.Class, reward: LootBoxTier.Silver));

            Godot.GD.Print($"[AchievementRegistry] Initialized {_achievements.Count} achievements");
        }

        private static void Register(AchievementData data)
        {
            _achievements[data.Id] = data;
        }

        public static AchievementData Get(string id)
        {
            return _achievements.TryGetValue(id, out var data) ? data : null;
        }
    }
}
