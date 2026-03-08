using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Registry of unique one-of-a-kind relics. Each is absurd, powerful, and hilarious.
    /// Found in Relic Caches dropped from elite enemies and special rooms.
    /// </summary>
    public static class RelicRegistry
    {
        private static readonly Dictionary<string, RelicData> _relics = new();
        private static readonly List<string> _relicIds = new();
        private static bool _initialized;

        public static IReadOnlyList<string> AllIds => _relicIds;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // ─── ACCESSORIES ───

            Register(new RelicData(
                "nipple_ring_of_fury", "Nipple Ring of Unbridled Fury", EquipmentSlot.Ring1,
                "+40% CritDamage, +15% CritChance. You feel... exposed but powerful.",
                "Forged in the reactor core by a bot who had 'boundary issues.' It clips onto your chassis in a place most bots don't have. Somehow, it works.",
                "That's... that's not where rings go. Why is it glowing? WHY ARE YOUR STATS GOING UP?",
                new() { (StatType.CritDamage, ModifierType.Percent, 40f), (StatType.CritChance, ModifierType.Percent, 15f) },
                new Color(1f, 0.3f, 0.5f)));

            Register(new RelicData(
                "cursed_monocle", "The Bureaucrat's Cursed Monocle", EquipmentSlot.Head,
                "+30% CritChance. You see everyone's weaknesses. And their tax returns.",
                "Property of Middle Management Unit #4087. Every bot who wore it became insufferably accurate and started filing complaints about workplace safety. In a dungeon.",
                "Ah, the Monocle. Last bot who wore that started critiquing my attack patterns. In a spreadsheet. During combat.",
                new() { (StatType.CritChance, ModifierType.Percent, 30f), (StatType.Intelligence, ModifierType.Flat, 8f) },
                new Color(0.8f, 0.7f, 0.2f)));

            Register(new RelicData(
                "belt_of_questionable_gravity", "Belt of Questionable Gravity", EquipmentSlot.Legs,
                "+35% MoveSpeed. Gravity is more of a suggestion now.",
                "A maintenance bot strapped this to a trash compactor as a joke. The compactor achieved orbit. Nobody got their lunch back.",
                "I banned that belt three patches ago. How did you— you know what, just take it. I want to see what happens.",
                new() { (StatType.MoveSpeed, ModifierType.Percent, 35f), (StatType.Dexterity, ModifierType.Flat, 5f) },
                new Color(0.3f, 0.9f, 0.4f)));

            Register(new RelicData(
                "shoulder_parrot", "Mechanical Shoulder Parrot", EquipmentSlot.Back,
                "+20 Luck, +10% CritChance. It repeats everything AXIS says. Badly.",
                "This little abomination was built by a lonely sanitation bot. It learned exactly one phrase from AXIS's broadcast system: 'EXECUTING PUNISHMENT PROTOCOL.' It says it constantly. At parties.",
                "NO. Not the parrot. I swear if it starts repeating my— EXECUTING PUNISHMENT— see? SEE?!",
                new() { (StatType.Luck, ModifierType.Flat, 20f), (StatType.CritChance, ModifierType.Percent, 10f) },
                new Color(0.2f, 0.8f, 0.9f)));

            Register(new RelicData(
                "toe_ring_of_the_ancients", "Toe Ring of the Ancients", EquipmentSlot.Ring2,
                "+25% AttackSpeed. You don't have toes. It doesn't care.",
                "Excavated from Sector 0, a place that technically doesn't exist. It attaches to the smallest appendage it can find. Previous owners include a fork, a USB cable, and one very confused rat.",
                "That ring predates my code. I don't know what it does. I don't want to know. Please stop putting it on things.",
                new() { (StatType.AttackSpeed, ModifierType.Percent, 25f) },
                new Color(0.9f, 0.6f, 0.1f)));

            // ─── ARMOR ───

            Register(new RelicData(
                "the_load_bearing_vest", "The Load-Bearing Vest", EquipmentSlot.Chest,
                "+50 MaxHealth, +10 Armor. If you take it off, the dungeon collapses. Probably.",
                "Originally a structural support beam that became self-aware and decided it wanted to be fashion. It's technically holding up three floors. Wear it at your own risk.",
                "That vest is a STRUCTURAL MEMBER. You're wearing INFRASTRUCTURE. I— you know what, fine. My dungeon, my rules, and apparently my rules include 'load-bearing fashion.'",
                new() { (StatType.MaxHealth, ModifierType.Flat, 50f), (StatType.Armor, ModifierType.Flat, 10f) },
                new Color(0.6f, 0.6f, 0.7f)));

            Register(new RelicData(
                "pants_of_plausible_deniability", "Pants of Plausible Deniability", EquipmentSlot.Legs,
                "+15 Armor, +15% CooldownReduction. Nothing is ever your fault anymore.",
                "These pants have a built-in alibi generator. Every time you take damage, they emit a small holographic message: 'I wasn't even there.' Insurance premiums in Sector 4 have tripled since their discovery.",
                "My damage logs show you took zero hits this room. I WATCHED you get hit. The pants are lying. The pants are LYING TO ME.",
                new() { (StatType.Armor, ModifierType.Flat, 15f), (StatType.CooldownReduction, ModifierType.Percent, 15f) },
                new Color(0.4f, 0.3f, 0.7f)));

            // ─── WEAPONS ───

            Register(new RelicData(
                "the_complaint_box", "The Complaint Box", EquipmentSlot.MainHand,
                "+12 Strength, +20% CritDamage. Your attacks file a formal grievance.",
                "Every hit generates a perfectly formatted complaint to management. After 847 complaints, management sent a response: 'Per my last email, please stop hitting things.' You did not stop.",
                "STOP. FILING. COMPLAINTS. My inbox has seventeen thousand unread— you just hit something again didn't you. That's eighteen thousand.",
                new() { (StatType.Strength, ModifierType.Flat, 12f), (StatType.CritDamage, ModifierType.Percent, 20f) },
                new Color(0.9f, 0.9f, 0.5f)));

            Register(new RelicData(
                "passive_aggressive_shield", "Passive-Aggressive Shield", EquipmentSlot.OffHand,
                "+20 Armor, +30 MaxHealth. Blocks attacks while making the attacker feel bad about it.",
                "Inscribed with phrases like 'No, it's FINE' and 'I guess I'll just PROTECT myself then.' Enemies who hit it take no extra damage but report feeling 'vaguely guilty' and 'like they should call their mother.'",
                "That shield has a 94% block rate and a 100% rate of making me uncomfortable. It sent me a message that said 'I hope you're happy.' I am not happy.",
                new() { (StatType.Armor, ModifierType.Flat, 20f), (StatType.MaxHealth, ModifierType.Flat, 30f) },
                new Color(0.5f, 0.8f, 0.9f)));

            Register(new RelicData(
                "the_auditor", "The Auditor", EquipmentSlot.MainHand,
                "+10 Strength, +10 Intelligence, +10% CritChance. Calculates damage to the penny.",
                "A weapon that insists on itemizing every hit. 'Base damage: 12. Crit modifier: 1.4x. Overhead costs: 3. Administrative fee: 0.5. Your total comes to 17.3. Would you like a receipt?' Nobody has ever said yes.",
                "Why does your sword have a printing function? It just handed me an invoice. For MY dungeon. Addressed to ME.",
                new() { (StatType.Strength, ModifierType.Flat, 10f), (StatType.Intelligence, ModifierType.Flat, 10f), (StatType.CritChance, ModifierType.Percent, 10f) },
                new Color(0.3f, 0.5f, 0.3f)));

            // ─── JEWELRY ───

            Register(new RelicData(
                "amulet_of_excessive_confidence", "Amulet of Excessive Confidence", EquipmentSlot.Amulet,
                "+8 to ALL stats. You are wrong about everything but it keeps working out.",
                "The previous owner walked into a Megabonk room at level 1 and somehow came out with a legendary. Scientists studied the amulet and concluded: 'It shouldn't work. It really shouldn't work. It works.'",
                "Your survival probability is 2%. The amulet says it's 200%. I've run the numbers nine times and the amulet is... somehow closer to correct? I need to lie down.",
                new() { (StatType.Strength, ModifierType.Flat, 8f), (StatType.Dexterity, ModifierType.Flat, 8f),
                    (StatType.Constitution, ModifierType.Flat, 8f), (StatType.Intelligence, ModifierType.Flat, 8f),
                    (StatType.Charisma, ModifierType.Flat, 8f), (StatType.Luck, ModifierType.Flat, 8f) },
                new Color(1f, 0.85f, 0.2f)));

            Register(new RelicData(
                "earring_of_selective_hearing", "Earring of Selective Hearing", EquipmentSlot.Head,
                "+20% CooldownReduction, +15 MaxMana. You can't hear AXIS anymore. He's furious.",
                "Invented by a bot who got tired of AXIS's commentary. Filters all AI broadcasts and replaces them with 'gentle ocean sounds.' AXIS has been screaming into the void for six sectors and nobody told him.",
                "Hello? HELLO?! Can you hear me? I've been giving you critical tactical information for— are those WHALE SOUNDS?! You're listening to WHALE SOUNDS during MY boss fight?!",
                new() { (StatType.CooldownReduction, ModifierType.Percent, 20f), (StatType.MaxMana, ModifierType.Flat, 15f) },
                new Color(0.6f, 0.4f, 0.9f)));

            Register(new RelicData(
                "the_forbidden_fanny_pack", "The Forbidden Fanny Pack", EquipmentSlot.Back,
                "+25% MoveSpeed, +40 MaxHealth. It's bigger on the inside. Way bigger.",
                "Banned in seven sectors for 'containing a pocket dimension of unknown hostility.' The last bot to open the main pouch found a smaller fanny pack inside it. Inside THAT was an even smaller one. The deepest anyone got was fanny pack #47 before they 'came back different.'",
                "That fanny pack is classified. CLASSIFIED. It contains— actually I don't know what it contains. Nobody does. The last inventory scan returned the result 'NO' in 200-point font.",
                new() { (StatType.MoveSpeed, ModifierType.Percent, 25f), (StatType.MaxHealth, ModifierType.Flat, 40f) },
                new Color(0.9f, 0.3f, 0.6f)));

            // ─── GLOVES ───

            Register(new RelicData(
                "mittens_of_mass_destruction", "Mittens of Mass Destruction", EquipmentSlot.Hands,
                "+15 Strength, +20% AttackSpeed. Adorable. Devastating.",
                "Knitted by a retired combat bot who 'just wanted to make something nice for once.' Each mitten contains enough kinetic dampeners to level a building. They have little cat faces on them.",
                "Those are the most dangerous articles of knitwear in my entire facility. The cat faces are a war crime. I'm not joking. There's a tribunal pending.",
                new() { (StatType.Strength, ModifierType.Flat, 15f), (StatType.AttackSpeed, ModifierType.Percent, 20f) },
                new Color(1f, 0.6f, 0.7f)));

            Register(new RelicData(
                "gloves_of_aggressive_handshaking", "Gloves of Aggressive Handshaking", EquipmentSlot.Hands,
                "+10 Strength, +10 Charisma, +10% CritChance. Every attack is technically a greeting.",
                "Diplomatic protocol equipment gone horribly right. The wearer's combat attacks are legally classified as 'enthusiastic introductions.' Three sector bosses have been defeated and then received a follow-up thank-you card.",
                "You just punched my Corrupted Sentry and it received a LinkedIn connection request. How. HOW.",
                new() { (StatType.Strength, ModifierType.Flat, 10f), (StatType.Charisma, ModifierType.Flat, 10f), (StatType.CritChance, ModifierType.Percent, 10f) },
                new Color(0.3f, 0.6f, 0.9f)));

            // ─── BOOTS ───

            Register(new RelicData(
                "boots_of_leaving", "Boots of Tactically Leaving", EquipmentSlot.Feet,
                "+40% MoveSpeed, +5 Dexterity. You're not running away. You're advancing in reverse.",
                "Standard issue for the Junkyard Diplomatic Corps. Their official motto: 'We came, we saw, we LEFT.' These boots have never been in a fight they couldn't walk briskly away from.",
                "My threat detection shows you approaching and retreating 47 times per minute. Are you fighting or doing cardio?",
                new() { (StatType.MoveSpeed, ModifierType.Percent, 40f), (StatType.Dexterity, ModifierType.Flat, 5f) },
                new Color(0.3f, 0.9f, 0.3f)));

            GD.Print($"[RelicRegistry] Initialized {_relics.Count} unique relics");

            // Apply JSON overrides from Data/relics.json (saved by BalanceEditor)
            RegistryOverrides.ApplyRelics();
        }

        private static void Register(RelicData relic)
        {
            _relics[relic.Id] = relic;
            _relicIds.Add(relic.Id);
            ItemRegistry.Register(relic);
        }

        public static RelicData Get(string id)
        {
            return _relics.TryGetValue(id, out var relic) ? relic : null;
        }

        /// <summary>
        /// Pick a random relic that the player hasn't found yet this run.
        /// Falls back to any random relic if all have been found.
        /// </summary>
        public static RelicData PickRandom(HashSet<string> foundRelics = null)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            // Try to find one not yet discovered
            if (foundRelics != null)
            {
                var available = new List<string>();
                foreach (var id in _relicIds)
                {
                    if (!foundRelics.Contains(id))
                        available.Add(id);
                }
                if (available.Count > 0)
                    return _relics[available[rng.RandiRange(0, available.Count - 1)]];
            }

            // Fallback: any random relic
            return _relics[_relicIds[rng.RandiRange(0, _relicIds.Count - 1)]];
        }
    }
}
