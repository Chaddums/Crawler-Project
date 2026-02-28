using System.Collections.Generic;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Static registry of ~25 DCC-flavored achievements across categories.
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

            // ── Combat ──

            Register(new AchievementData(
                "first_blood", "First Blood", "Kill your first enemy.",
                "Wow. You killed a grub. The audience is... not impressed.",
                AchievementCategory.Combat));

            Register(new AchievementData(
                "getting_warmed_up", "Getting Warmed Up", "Kill 10 enemies.",
                "Double digits! The AI is considering upgrading your threat level from 'mild inconvenience' to 'minor nuisance'.",
                AchievementCategory.Combat));

            Register(new AchievementData(
                "dungeon_menace", "Dungeon Menace", "Kill 50 enemies.",
                "50 kills! The other crawlers are watching.",
                AchievementCategory.Combat, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "boss_slayer", "Boss Slayer", "Defeat your first boss.",
                "You defeated a boss! Don't let it go to your head. That was the tutorial boss.",
                AchievementCategory.Combat, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "combo_master", "Combo Master", "Hit a 5x combo.",
                "The combat system approves of your button mashing.",
                AchievementCategory.Combat));

            Register(new AchievementData(
                "overkill", "Overkill", "Deal 100+ damage in a single hit.",
                "The damage number was so large it needed a bigger font.",
                AchievementCategory.Combat, reward: LootBoxTier.Gold));

            Register(new AchievementData(
                "pacifist_floor", "Pacifist Floor", "Complete a floor with 0 kills.",
                "You skipped every fight. The AI is... confused. And mildly offended.",
                AchievementCategory.Combat, hidden: true));

            // ── Exploration ──

            Register(new AchievementData(
                "floor_2", "Deeper We Go", "Reach floor 2.",
                "You survived floor 1. Statistically, this is where most crawlers die. Good luck!",
                AchievementCategory.Exploration));

            Register(new AchievementData(
                "floor_5", "Veteran Crawler", "Reach floor 5.",
                "Floor 5. The dungeon is starting to take you seriously.",
                AchievementCategory.Exploration, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "treasure_hunter", "Treasure Hunter", "Open 10 treasure rooms.",
                "Your looting instincts are finely tuned. The dungeon's insurance premiums are rising.",
                AchievementCategory.Exploration));

            Register(new AchievementData(
                "completionist", "Completionist", "Clear every room on a floor.",
                "You left no stone unturned. The dungeon custodian is filing a complaint.",
                AchievementCategory.Exploration));

            Register(new AchievementData(
                "stairwell_rush", "Stairwell Rush", "Enter the stairwell with less than 30 seconds remaining.",
                "Cutting it close! The audience loved that. Your heart did not.",
                AchievementCategory.Exploration, hidden: true));

            // ── Survival ──

            Register(new AchievementData(
                "close_call", "Close Call", "Survive with less than 10% HP.",
                "Your health bar is giving the audience anxiety.",
                AchievementCategory.Survival));

            Register(new AchievementData(
                "potion_addict", "Potion Addict", "Use 20 consumables.",
                "The dungeon pharmacy is running low. Please drink responsibly.",
                AchievementCategory.Survival));

            Register(new AchievementData(
                "iron_crawler", "Iron Crawler", "Complete a floor without using potions.",
                "No potions used. Either you're very skilled or very lucky. The AI suspects lucky.",
                AchievementCategory.Survival, reward: LootBoxTier.Gold));

            Register(new AchievementData(
                "back_from_the_brink", "Back from the Brink", "Heal from below 10% to above 80% HP with one consumable.",
                "That potion had to work overtime. It wants a raise.",
                AchievementCategory.Survival));

            // ── Class ──

            Register(new AchievementData(
                "class_chosen", "Identity Crisis Resolved", "Select a class.",
                "You chose a class. The AI has updated your obituary accordingly.",
                AchievementCategory.Class));

            Register(new AchievementData(
                "spell_slinger", "Spell Slinger", "Cast 50 abilities.",
                "50 ability casts. Your mana bar sends its regards.",
                AchievementCategory.Class));

            Register(new AchievementData(
                "critical_streak", "Critical Streak", "Land 3 critical hits in a row.",
                "Three crits in a row! The RNG gods are briefly on your side.",
                AchievementCategory.Class));

            // ── Meta ──

            Register(new AchievementData(
                "hoarder", "Professional Hoarder", "Have 25+ items in your inventory.",
                "Your inventory is an organizational nightmare. The dungeon is impressed.",
                AchievementCategory.Meta));

            Register(new AchievementData(
                "well_equipped", "Fully Loaded", "Fill all equipment slots.",
                "Every slot filled! You look like a walking armory. The dungeon approves.",
                AchievementCategory.Meta, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "achievement_hunter", "Achievement Hunter", "Unlock 15 achievements.",
                "You're collecting achievements about collecting achievements. Very meta.",
                AchievementCategory.Meta, reward: LootBoxTier.Gold));

            Register(new AchievementData(
                "speed_runner", "Speed Demon", "Complete floor 1 in under 120 seconds.",
                "Floor 1 speedrun complete! The audience is requesting you do that again. Backwards.",
                AchievementCategory.Meta, hidden: true, reward: LootBoxTier.Diamond));

            Register(new AchievementData(
                "level_5", "Settling In", "Reach level 5.",
                "Level 5. You might actually survive this floor.",
                AchievementCategory.Class));

            Register(new AchievementData(
                "level_10", "Double Digits", "Reach level 10.",
                "Level 10. Don't let it go to your head.",
                AchievementCategory.Class));

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
