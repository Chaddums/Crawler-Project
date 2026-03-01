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
                "first_blood", "First Scrap", "Destroy your first enemy.",
                "You dismantled a wire worm. AXIS is... not impressed.",
                AchievementCategory.Combat, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "getting_warmed_up", "Servos Warmed Up", "Destroy 10 enemies.",
                "Double digits! AXIS is upgrading your threat level from 'mild inconvenience' to 'minor nuisance'.",
                AchievementCategory.Combat, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "dungeon_menace", "Arena Menace", "Destroy 50 enemies.",
                "50 kills! The other scrappers are paying attention.",
                AchievementCategory.Combat, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "boss_slayer", "Boss Breaker", "Defeat your first boss.",
                "You defeated a boss! AXIS wants you to know that was the tutorial. Don't get cocky.",
                AchievementCategory.Combat, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "combo_master", "Combo Protocol", "Hit a 5x combo.",
                "AXIS: Your combat subroutines are adequate. BIT: That means he liked it.",
                AchievementCategory.Combat, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "overkill", "Overkill.exe", "Deal 100+ damage in a single hit.",
                "The damage readout needed a wider display. AXIS is recalibrating threat assessment.",
                AchievementCategory.Combat, reward: LootBoxTier.Diamond));

            Register(new AchievementData(
                "pacifist_floor", "Pacifist Protocol", "Complete a sector with 0 kills.",
                "You skipped every fight. AXIS is confused. And mildly offended.",
                AchievementCategory.Combat, hidden: true, reward: LootBoxTier.Gold));

            // -- Exploration --

            Register(new AchievementData(
                "floor_2", "Deeper Into the Arena", "Reach sector 2.",
                "You survived sector 1. Statistically, this is where most scrappers get recycled.",
                AchievementCategory.Exploration, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "floor_5", "Veteran Scrapper", "Reach sector 5.",
                "Sector 5. AXIS is starting to take you seriously. That's not a compliment.",
                AchievementCategory.Exploration, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "treasure_hunter", "Salvage Expert", "Open 10 treasure rooms.",
                "Your looting protocols are finely tuned. AXIS's insurance premiums are rising.",
                AchievementCategory.Exploration, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "completionist", "Full Sweep", "Clear every room in a sector.",
                "You left no panel unscrewed. The maintenance drones are filing a complaint.",
                AchievementCategory.Exploration, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "stairwell_rush", "Lift Dash", "Enter the lift with less than 30 seconds remaining.",
                "Cutting it close! BIT nearly had a meltdown. Your coolant pump did not enjoy that.",
                AchievementCategory.Exploration, hidden: true, reward: LootBoxTier.Gold));

            // -- Survival --

            Register(new AchievementData(
                "close_call", "Critical Integrity", "Survive with less than 10% HP.",
                "Your structural integrity readings are giving BIT anxiety.",
                AchievementCategory.Survival, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "potion_addict", "Repair Addict", "Use 20 consumables.",
                "The supply depot is running low. Please repair responsibly.",
                AchievementCategory.Survival, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "iron_frame", "Iron Frame", "Complete a sector without using repair kits.",
                "No repairs used. Either you're well-built or very lucky. AXIS suspects lucky.",
                AchievementCategory.Survival, reward: LootBoxTier.Gold));

            Register(new AchievementData(
                "back_from_the_brink", "Emergency Reboot", "Heal from below 10% to above 80% HP with one consumable.",
                "That repair kit worked overtime. It wants hazard pay.",
                AchievementCategory.Survival, reward: LootBoxTier.Silver));

            // -- Class --

            Register(new AchievementData(
                "class_chosen", "Frame Selected", "Select a bot frame.",
                "You chose a frame. AXIS has updated your recycling schedule accordingly.",
                AchievementCategory.Class, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "spell_slinger", "Discharge Protocol", "Cast 50 abilities.",
                "50 ability activations. Your energy core sends its regards.",
                AchievementCategory.Class, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "critical_streak", "Critical Streak", "Land 3 critical hits in a row.",
                "Three crits in a row! The RNG subroutine is briefly on your side.",
                AchievementCategory.Class, reward: LootBoxTier.Silver));

            // -- Meta --

            Register(new AchievementData(
                "hoarder", "Data Hoarder", "Have 25+ items in your inventory.",
                "Your inventory is an organizational disaster. AXIS is impressed and disgusted.",
                AchievementCategory.Meta, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "well_equipped", "Fully Loaded", "Fill all equipment slots.",
                "Every slot filled! You look like a walking scrapyard. AXIS approves.",
                AchievementCategory.Meta, reward: LootBoxTier.Silver));

            Register(new AchievementData(
                "achievement_hunter", "Achievement Protocol", "Unlock 15 achievements.",
                "You're collecting achievements about collecting achievements. Very recursive.",
                AchievementCategory.Meta, reward: LootBoxTier.Diamond));

            Register(new AchievementData(
                "speed_runner", "Speed Daemon", "Complete sector 1 in under 120 seconds.",
                "Sector 1 speedrun complete! AXIS is requesting you do that again. In reverse. On fire.",
                AchievementCategory.Meta, hidden: true, reward: LootBoxTier.Legendary));

            Register(new AchievementData(
                "level_5", "Powering Up", "Reach level 5.",
                "Level 5. You might actually survive this sector.",
                AchievementCategory.Class, reward: LootBoxTier.Bronze));

            Register(new AchievementData(
                "level_10", "Double Digits", "Reach level 10.",
                "Level 10. Don't let it go to your processors.",
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
