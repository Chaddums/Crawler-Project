using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Builds the expanded passive tree (~216 nodes) with 3 sub-branches per class,
    /// threshold gates, mutually exclusive keystones, and an expanded inner ring.
    /// Phase 1 of the passive tree expansion plan.
    /// </summary>
    public static class PassiveTreeBuilder
    {
        private static PassiveTreeData _tree;
        private static bool _built;

        private struct BranchInfo
        {
            public string TrunkMidId;
            public string CenterBranchEndId;
        }

        public static PassiveTreeData Tree
        {
            get
            {
                if (!_built) Build();
                return _tree;
            }
        }

        public static void Build()
        {
            if (_built) return;
            _tree = new PassiveTreeData();

            const float outerRadius = 20f;
            var classes = new[] {
                BotFrameType.Scrapheap, BotFrameType.TinCan,
                BotFrameType.SparkPlug, BotFrameType.RustBucket,
                BotFrameType.NoiseBox, BotFrameType.Clunker
            };

            var classPositions = new Dictionary<BotFrameType, Vector2>();
            var classStartIds = new Dictionary<BotFrameType, string>();
            var branchInfos = new Dictionary<BotFrameType, BranchInfo>();

            // 1. Class start nodes
            for (int i = 0; i < classes.Length; i++)
            {
                var cls = classes[i];
                var pos = HexPos(i, outerRadius);
                classPositions[cls] = pos;
                var id = $"start_{cls}";
                var node = new PassiveNodeData(id, $"{cls} Start", SkillNodeType.ClassStart, pos);
                node.ClassStartFor = cls;
                _tree.AddNode(node);
                classStartIds[cls] = id;
            }

            // 2. Build each class trunk + 3 sub-branches
            foreach (var cls in classes)
                branchInfos[cls] = BuildClassBranch(cls, classStartIds[cls], classPositions[cls]);

            // 3. Keystones (2 per class, mutually exclusive) + Pinnacles
            foreach (var cls in classes)
                BuildKeystones(cls, classStartIds[cls], classPositions[cls]);

            // 4. Inner ring (12 nodes)
            BuildInnerRing(branchInfos, classes);

            // 5. Cross-class bridges
            BuildBridges(classes, classPositions, branchInfos);

            // 6. Core sockets
            BuildCoreSockets(classes, classPositions, branchInfos);

            _built = true;
            GD.Print($"[PassiveTreeBuilder] Built expanded tree with {_tree.Nodes.Count} nodes");
        }

        // =====================================================================
        // CLASS BRANCH DISPATCHER
        // =====================================================================

        private static BranchInfo BuildClassBranch(BotFrameType cls, string startId, Vector2 classPos)
        {
            return cls switch
            {
                BotFrameType.Scrapheap => BuildScrapheap(startId, classPos),
                BotFrameType.TinCan => BuildTinCan(startId, classPos),
                BotFrameType.SparkPlug => BuildSparkPlug(startId, classPos),
                BotFrameType.RustBucket => BuildRustBucket(startId, classPos),
                BotFrameType.NoiseBox => BuildNoiseBox(startId, classPos),
                BotFrameType.Clunker => BuildClunker(startId, classPos),
                _ => new BranchInfo()
            };
        }

        // =====================================================================
        // SCRAPHEAP — STR/CON Heavy Brawler
        // =====================================================================

        private static BranchInfo BuildScrapheap(string startId, Vector2 classPos)
        {
            var dir = classPos.Normalized();
            var splitPos = classPos * 0.55f;
            const float fan = 0.6f;

            // Trunk
            var t0 = AddBranch("sh_t0", "+8 STR, +10 HP", classPos * 0.82f, "sh_trunk",
                StatType.Strength, 8f, StatType.MaxHealth, 10f);
            var t1 = AddBranch("sh_t1", "+20 Max HP", splitPos, "sh_trunk",
                StatType.MaxHealth, 20f, StatType.Armor, 3f);
            _tree.ConnectNodes(startId, t0.Id);
            _tree.ConnectNodes(t0.Id, t1.Id);

            // A: Juggernaut (HP/Armor) — left fan
            const string jug = "sh_juggernaut";
            var pA = SubPos(splitPos, dir.Rotated(fan) * 5.0f, 8);
            var j0 = AddBranch("sh_j0", "Reinforced Frame", pA[0], jug, StatType.MaxHealth, 30f, StatType.Armor, 5f);
            var j1 = AddBranch("sh_j1", "Hardened Alloy", pA[1], jug, StatType.Armor, 8f, StatType.MaxHealth, 15f);
            var j2 = AddNotable("sh_j2", "Blast Shield", pA[2], jug,
                "AoE damage reduced by 40%. +2 armor per enemy within 5m (max 10).",
                Perks.ExplosionDampener, (StatType.Armor, ModifierType.Flat, 5f));
            var j3 = AddBranch("sh_j3", "Layered Plating", pA[3], jug, StatType.Armor, 5f, StatType.MaxHealth, 15f);
            var j4 = AddBranch("sh_j4", "Shock Absorber", pA[4], jug, StatType.Armor, 5f, StatType.Constitution, 3f);
            var j5 = AddNotable("sh_j5", "Impact Dampener", pA[5], jug,
                "10% of damage taken stored (max 200). Next basic releases stored damage.",
                Perks.KineticBattery, (StatType.MaxHealth, ModifierType.Flat, 15f));
            var j6 = AddBranch("sh_j6", "Titanium Core", pA[6], jug, StatType.MaxHealth, 25f, StatType.Constitution, 4f);
            var j7 = AddCap("sh_j7", "Unstoppable", pA[7], jug, 6,
                "Above 80% HP: immune to stun/slow/knockback. Below 50%: +30% damage, +20% speed.",
                Perks.UnstoppableForce, (StatType.MaxHealth, ModifierType.Flat, 20f), (StatType.Armor, ModifierType.Flat, 5f));
            Chain(t1.Id, j0, j1, j2, j3, j4, j5, j6, j7);

            // B: Berserker (damage/risk) — center, connects to inner ring
            const string ber = "sh_berserker";
            var pB = SubPos(splitPos, dir * 5.0f, 8);
            var b0 = AddBranch("sh_b0", "Overdriven Servos", pB[0], ber, StatType.AttackSpeed, 0.10f, StatType.Strength, 5f);
            var b1 = AddBranch("sh_b1", "Frenzied Wiring", pB[1], ber, StatType.AttackSpeed, 0.05f, StatType.CritChance, 0.03f);
            var b2 = AddNotable("sh_b2", "Blood Oil", pB[2], ber,
                "Basic attacks heal 3% damage dealt. Each heal reduces armor by 1 for 3s.",
                Perks.LeakingFuel, (StatType.Strength, ModifierType.Flat, 5f));
            var b3 = AddBranch("sh_b3", "Stripped Limiters", pB[3], ber, StatType.CritDamage, 0.15f, StatType.CritChance, 0.05f);
            var b4 = AddBranch("sh_b4", "Short Circuit", pB[4], ber, StatType.Strength, 5f, StatType.AttackSpeed, 0.05f);
            var b5 = AddNotable("sh_b5", "Rage Core", pB[5], ber,
                "Gain 1 Rage per hit taken (max 10). Each: +3% damage, +2% AS. At 10: AoE discharge.",
                Perks.RageAccumulator, (StatType.Strength, ModifierType.Flat, 5f));
            var b6 = AddBranch("sh_b6", "Meltdown Protocol", pB[6], ber, StatType.Strength, 8f);
            var b7 = AddCap("sh_b7", "Overdrive", pB[7], ber, 6,
                "HP drains 3%/s. +50% damage, +30% AS. Basic heals 5% dealt. Toggle on/off.",
                Perks.RedLine, (StatType.Strength, ModifierType.Flat, 10f), (StatType.AttackSpeed, ModifierType.Flat, 0.10f));
            Chain(t1.Id, b0, b1, b2, b3, b4, b5, b6, b7);

            // C: Fortress (shields/retaliation) — right fan
            const string fort = "sh_fortress";
            var pC = SubPos(splitPos, dir.Rotated(-fan) * 5.0f, 8);
            var f0 = AddBranch("sh_f0", "Reactive Hull", pC[0], fort, StatType.Armor, 10f);
            var f1 = AddBranch("sh_f1", "Mag-Lock Plating", pC[1], fort, StatType.Armor, 5f, StatType.MaxHealth, 10f);
            var f2 = AddNotable("sh_f2", "Spiked Chassis", pC[2], fort,
                "Melee attackers take 20 Lightning damage + stunned 0.3s. 1s CD per target.",
                Perks.ElectrifiedHull, (StatType.Armor, ModifierType.Flat, 5f));
            var f3 = AddBranch("sh_f3", "Bulwark Stance", pC[3], fort, StatType.Armor, 8f, StatType.MaxHealth, 10f);
            var f4 = AddBranch("sh_f4", "Anchored Frame", pC[4], fort, StatType.Armor, 5f, StatType.MaxHealth, 15f);
            var f5 = AddNotable("sh_f5", "Fortress Wall", pC[5], fort,
                "Every 8s, taunt pulse (10m). Taunted enemies: -15% damage to allies, +10% to you.",
                Perks.TauntEmitter, (StatType.MaxHealth, ModifierType.Flat, 10f), (StatType.Armor, ModifierType.Flat, 3f));
            var f6 = AddBranch("sh_f6", "Siege Mode", pC[6], fort, StatType.Armor, 10f);
            var f7 = AddCap("sh_f7", "Iron Curtain", pC[7], fort, 6,
                "Below 25% HP: immobile 3s, +100% armor, reflect 50% damage, heal 5%/s. 90s CD.",
                Perks.LastStand, (StatType.Armor, ModifierType.Flat, 8f), (StatType.MaxHealth, ModifierType.Flat, 15f));
            Chain(t1.Id, f0, f1, f2, f3, f4, f5, f6, f7);

            return new BranchInfo { TrunkMidId = t0.Id, CenterBranchEndId = b7.Id };
        }

        // =====================================================================
        // TINCAN — CON/STR Defender/Support
        // =====================================================================

        private static BranchInfo BuildTinCan(string startId, Vector2 classPos)
        {
            var dir = classPos.Normalized();
            var splitPos = classPos * 0.55f;
            const float fan = 0.6f;

            var t0 = AddBranch("tc_t0", "+25 Max HP", classPos * 0.82f, "tc_trunk",
                StatType.MaxHealth, 25f);
            var t1 = AddBranch("tc_t1", "+5 Armor, +10 HP", splitPos, "tc_trunk",
                StatType.Armor, 5f, StatType.MaxHealth, 10f);
            _tree.ConnectNodes(startId, t0.Id);
            _tree.ConnectNodes(t0.Id, t1.Id);

            // A: Bulwark (Block/Parry) — left
            const string bul = "tc_bulwark";
            var pA = SubPos(splitPos, dir.Rotated(fan) * 5.0f, 8);
            var a0 = AddBranch("tc_b0", "Reinforced Joints", pA[0], bul, StatType.Armor, 10f, StatType.Constitution, 5f);
            var a1 = AddBranch("tc_b1", "Quick Calibration", pA[1], bul, StatType.AttackSpeed, 0.05f, StatType.CooldownReduction, 0.03f);
            var a2 = AddNotable("tc_b2", "Deflector Array", pA[2], bul,
                "15% chance to negate damage. On parry: next attack +40% damage within 1s.",
                Perks.AutoParry, (StatType.Armor, ModifierType.Flat, 5f));
            var a3 = AddBranch("tc_b3", "Hardened Servos", pA[3], bul, StatType.Armor, 8f, StatType.CritChance, 0.05f);
            var a4 = AddBranch("tc_b4", "Counter-Weight", pA[4], bul, StatType.Strength, 5f, StatType.Armor, 3f);
            var a5 = AddNotable("tc_b5", "Adaptive Shield", pA[5], bul,
                "After taking elemental damage, gain 30% resistance to that element for 5s.",
                Perks.ElementalTuning, (StatType.Armor, ModifierType.Flat, 5f));
            var a6 = AddBranch("tc_b6", "Locked Joints", pA[6], bul, StatType.Armor, 10f);
            var a7 = AddCap("tc_b7", "Mirror Plating", pA[7], bul, 6,
                "Every 4th hit taken is reflected at 200% damage. Visible counter.",
                Perks.FullReflect, (StatType.Armor, ModifierType.Flat, 8f));
            Chain(t1.Id, a0, a1, a2, a3, a4, a5, a6, a7);

            // B: Guardian (Ally Buffs/Healing) — center
            const string gua = "tc_guardian";
            var pB = SubPos(splitPos, dir * 5.0f, 8);
            var b0 = AddBranch("tc_g0", "Broadcast Antenna", pB[0], gua, StatType.MaxMana, 10f, StatType.Charisma, 5f);
            var b1 = AddBranch("tc_g1", "Signal Boost", pB[1], gua, StatType.MaxHealth, 10f, StatType.CooldownReduction, 0.05f);
            var b2 = AddNotable("tc_g2", "Field Medic", pB[2], gua,
                "On kill, drop repair field (3m, 5s) that heals 3% max HP/s to allies.",
                Perks.RepairBeacon, (StatType.MaxHealth, ModifierType.Flat, 10f));
            var b3 = AddBranch("tc_g3", "Extended Range", pB[3], gua, StatType.Charisma, 5f, StatType.Intelligence, 3f);
            var b4 = AddBranch("tc_g4", "Sympathetic Link", pB[4], gua, StatType.Armor, 5f, StatType.MaxHealth, 10f);
            var b5 = AddNotable("tc_g5", "Rallying Cry", pB[5], gua,
                "On ability use, nearby allies gain +10% attack speed for 3s. 5s CD.",
                Perks.OverclockAllies, (StatType.Charisma, ModifierType.Flat, 5f));
            var b6 = AddBranch("tc_g6", "Shared Power", pB[6], gua, StatType.MaxHealth, 10f, StatType.Charisma, 3f);
            var b7 = AddCap("tc_g7", "Network Hub", pB[7], gua, 6,
                "Stat bonuses shared at 20% to allies in 8m. Absorb lethal ally damage (30s CD).",
                Perks.DistributedProcessing, (StatType.MaxHealth, ModifierType.Flat, 15f), (StatType.Charisma, ModifierType.Flat, 5f));
            Chain(t1.Id, b0, b1, b2, b3, b4, b5, b6, b7);

            // C: Sentinel (Counter-Attack) — right
            const string sen = "tc_sentinel";
            var pC = SubPos(splitPos, dir.Rotated(-fan) * 5.0f, 8);
            var c0 = AddBranch("tc_s0", "Targeting Subroutine", pC[0], sen, StatType.CritChance, 0.05f, StatType.CritDamage, 0.10f);
            var c1 = AddBranch("tc_s1", "Overwatch Module", pC[1], sen, StatType.Strength, 5f, StatType.Dexterity, 3f);
            var c2 = AddNotable("tc_s2", "Interceptor", pC[2], sen,
                "When ally hit, 20% chance to fire retaliatory shot at 150% weapon damage.",
                Perks.InterceptProtocol, (StatType.Strength, ModifierType.Flat, 5f));
            var c3 = AddBranch("tc_s3", "Threat Assessment", pC[3], sen, StatType.Strength, 5f);
            var c4 = AddBranch("tc_s4", "Tracking Lock", pC[4], sen, StatType.CritChance, 0.03f, StatType.CritDamage, 0.08f);
            var c5 = AddNotable("tc_s5", "Mark Target", pC[5], sen,
                "Crits mark enemies for 4s. Marked take +15% from all sources. 1 mark active.",
                Perks.VulnerabilityScanner, (StatType.CritChance, ModifierType.Flat, 0.05f));
            var c6 = AddBranch("tc_s6", "Combat Analysis", pC[6], sen, StatType.CritChance, 0.03f, StatType.Strength, 3f);
            var c7 = AddCap("tc_s7", "Overwatch", pC[7], sen, 6,
                "Standing still 2s+: attack range doubles, +25% CritChance, attacks pierce.",
                Perks.SentinelProtocol, (StatType.CritChance, ModifierType.Flat, 0.05f), (StatType.CritDamage, ModifierType.Flat, 0.15f));
            Chain(t1.Id, c0, c1, c2, c3, c4, c5, c6, c7);

            return new BranchInfo { TrunkMidId = t0.Id, CenterBranchEndId = b7.Id };
        }

        // =====================================================================
        // SPARKPLUG — INT/DEX Energy Caster
        // =====================================================================

        private static BranchInfo BuildSparkPlug(string startId, Vector2 classPos)
        {
            var dir = classPos.Normalized();
            var splitPos = classPos * 0.55f;
            const float fan = 0.6f;

            var t0 = AddBranch("sp_t0", "+8 INT, +10 Mana", classPos * 0.82f, "sp_trunk",
                StatType.Intelligence, 8f, StatType.MaxMana, 10f);
            var t1 = AddBranch("sp_t1", "+20 Max Mana", splitPos, "sp_trunk",
                StatType.MaxMana, 20f, StatType.Intelligence, 3f);
            _tree.ConnectNodes(startId, t0.Id);
            _tree.ConnectNodes(t0.Id, t1.Id);

            // A: Overcharge (Mana→Damage) — left
            const string ovc = "sp_overcharge";
            var pA = SubPos(splitPos, dir.Rotated(fan) * 5.0f, 8);
            var a0 = AddBranch("sp_o0", "Power Surge", pA[0], ovc, StatType.Intelligence, 8f);
            var a1 = AddBranch("sp_o1", "Volatile Capacitor", pA[1], ovc, StatType.CritDamage, 0.15f, StatType.MaxMana, 10f);
            var a2 = AddNotable("sp_o2", "Mana Burn", pA[2], ovc,
                "Abilities deal bonus damage equal to 8% of your current mana.",
                Perks.EnergyOverload, (StatType.Intelligence, ModifierType.Flat, 5f));
            var a3 = AddBranch("sp_o3", "Focused Beam", pA[3], ovc, StatType.Intelligence, 5f, StatType.CritChance, 0.03f);
            var a4 = AddBranch("sp_o4", "Unstable Core", pA[4], ovc, StatType.Intelligence, 5f, StatType.MaxMana, 10f);
            var a5 = AddNotable("sp_o5", "Arcane Detonation", pA[5], ovc,
                "Spend 100+ mana in 3s: AoE explosion (200% INT damage). 8s CD.",
                Perks.ManaBomb, (StatType.Intelligence, ModifierType.Flat, 5f));
            var a6 = AddBranch("sp_o6", "Power Siphon", pA[6], ovc, StatType.MaxMana, 15f, StatType.Intelligence, 3f);
            var a7 = AddCap("sp_o7", "Critical Mass", pA[7], ovc, 6,
                "Crit ability hits: 20% chance to reset that ability's CD (diminishing).",
                Perks.Supernova, (StatType.CritChance, ModifierType.Flat, 0.05f), (StatType.Intelligence, ModifierType.Flat, 5f));
            Chain(t1.Id, a0, a1, a2, a3, a4, a5, a6, a7);

            // B: Conduit (Chain/AoE) — center
            const string con = "sp_conduit";
            var pB = SubPos(splitPos, dir * 5.0f, 8);
            var b0 = AddBranch("sp_d0", "Wide Frequency", pB[0], con, StatType.Intelligence, 5f);
            var b1 = AddBranch("sp_d1", "Charged Air", pB[1], con, StatType.Intelligence, 5f, StatType.CritChance, 0.03f);
            var b2 = AddNotable("sp_d2", "Arc Welder", pB[2], con,
                "Lightning chains to 2 targets at 30%. Chained get -10% Lightning res.",
                Perks.LightningRod, (StatType.Intelligence, ModifierType.Flat, 5f));
            var b3 = AddBranch("sp_d3", "Static Field", pB[3], con, StatType.Intelligence, 3f, StatType.MaxMana, 10f);
            var b4 = AddBranch("sp_d4", "Charged Ground", pB[4], con, StatType.Intelligence, 5f);
            var b5 = AddNotable("sp_d5", "Cascade Failure", pB[5], con,
                "Status-effected enemy dies: status jumps to 2 nearby at 80% duration.",
                Perks.StatusCascade, (StatType.Intelligence, ModifierType.Flat, 5f));
            var b6 = AddBranch("sp_d6", "Overloaded Grid", pB[6], con, StatType.Intelligence, 5f, StatType.CritChance, 0.03f);
            var b7 = AddCap("sp_d7", "Chain Reaction", pB[7], con, 6,
                "All damage: 10% chance to chain (50% damage, up to 3 bounces).",
                Perks.Propagation, (StatType.Intelligence, ModifierType.Flat, 8f));
            Chain(t1.Id, b0, b1, b2, b3, b4, b5, b6, b7);

            // C: Capacitor (Mana Shield/Regen/CDR) — right
            const string cap = "sp_capacitor";
            var pC = SubPos(splitPos, dir.Rotated(-fan) * 5.0f, 8);
            var c0 = AddBranch("sp_c0", "Efficient Wiring", pC[0], cap, StatType.MaxMana, 15f);
            var c1 = AddBranch("sp_c1", "Thermal Vent", pC[1], cap, StatType.CooldownReduction, 0.05f, StatType.MaxMana, 10f);
            var c2 = AddNotable("sp_c2", "Mana Weave", pC[2], cap,
                "Standing still: +3% mana regen/s. Above 80% mana: +10% armor.",
                Perks.RegenerationField, (StatType.MaxMana, ModifierType.Flat, 10f));
            var c3 = AddBranch("sp_c3", "Battery Bank", pC[3], cap, StatType.MaxMana, 20f);
            var c4 = AddBranch("sp_c4", "Flow State", pC[4], cap, StatType.CooldownReduction, 0.03f, StatType.MaxMana, 10f);
            var c5 = AddNotable("sp_c5", "Power Recycler", pC[5], cap,
                "Every 4th ability cast costs 0 mana. Counter visible on UI.",
                Perks.SpellEcho, (StatType.MaxMana, ModifierType.Flat, 15f));
            var c6 = AddBranch("sp_c6", "Deep Reserve", pC[6], cap, StatType.MaxMana, 15f, StatType.MaxHealth, 10f);
            var c7 = AddCap("sp_c7", "Infinite Loop", pC[7], cap, 6,
                "CDs tick 50% faster while mana >50%. Below 25%: refill 30% + 3s CD. 45s CD.",
                Perks.PerpetualEngine, (StatType.MaxMana, ModifierType.Flat, 20f), (StatType.CooldownReduction, ModifierType.Flat, 0.05f));
            Chain(t1.Id, c0, c1, c2, c3, c4, c5, c6, c7);

            return new BranchInfo { TrunkMidId = t0.Id, CenterBranchEndId = b7.Id };
        }

        // =====================================================================
        // RUSTBUCKET — DEX/STR Rogue/Stealth
        // =====================================================================

        private static BranchInfo BuildRustBucket(string startId, Vector2 classPos)
        {
            var dir = classPos.Normalized();
            var splitPos = classPos * 0.55f;
            const float fan = 0.6f;

            var t0 = AddBranch("rb_t0", "+8 DEX, +5% AS", classPos * 0.82f, "rb_trunk",
                StatType.Dexterity, 8f, StatType.AttackSpeed, 0.05f);
            var t1 = AddBranch("rb_t1", "+3% Crit, +5 DEX", splitPos, "rb_trunk",
                StatType.CritChance, 0.03f, StatType.Dexterity, 5f);
            _tree.ConnectNodes(startId, t0.Id);
            _tree.ConnectNodes(t0.Id, t1.Id);

            // A: Infiltrator (Crit/Backstab) — left
            const string inf = "rb_infiltrator";
            var pA = SubPos(splitPos, dir.Rotated(fan) * 5.0f, 8);
            var a0 = AddBranch("rb_i0", "Precision Targeting", pA[0], inf, StatType.CritChance, 0.05f, StatType.Dexterity, 5f);
            var a1 = AddBranch("rb_i1", "Weak Point Scanner", pA[1], inf, StatType.CritDamage, 0.15f);
            var a2 = AddNotable("rb_i2", "Exposed Wiring", pA[2], inf,
                "Crits reduce target armor by 10% for 4s (stacks 3x = -30%).",
                Perks.ArmorShred, (StatType.CritChance, ModifierType.Flat, 0.03f));
            var a3 = AddBranch("rb_i3", "Surgical Strike", pA[3], inf, StatType.CritDamage, 0.10f, StatType.Dexterity, 3f);
            var a4 = AddBranch("rb_i4", "Lethal Calibration", pA[4], inf, StatType.CritChance, 0.03f, StatType.CritDamage, 0.10f);
            var a5 = AddNotable("rb_i5", "Execution Protocol", pA[5], inf,
                "Targets below 20% HP take +50% damage. Low-HP kills: +25% rare loot.",
                Perks.Execute, (StatType.CritDamage, ModifierType.Flat, 0.10f));
            var a6 = AddBranch("rb_i6", "Hunter's Mark", pA[6], inf, StatType.CritChance, 0.03f, StatType.Dexterity, 3f);
            var a7 = AddCap("rb_i7", "Assassin Core", pA[7], inf, 6,
                "Every 5th crit: Death Mark (4s). Death Marked: 3x next hit. Kill = CD reset.",
                Perks.DeathMark, (StatType.CritChance, ModifierType.Flat, 0.05f), (StatType.CritDamage, ModifierType.Flat, 0.20f));
            Chain(t1.Id, a0, a1, a2, a3, a4, a5, a6, a7);

            // B: Saboteur (Traps/DoT) — center
            const string sab = "rb_saboteur";
            var pB = SubPos(splitPos, dir * 5.0f, 8);
            var b0 = AddBranch("rb_s0", "Corrosive Rounds", pB[0], sab, StatType.Dexterity, 5f);
            var b1 = AddBranch("rb_s1", "Acid Bath", pB[1], sab, StatType.Dexterity, 5f, StatType.Intelligence, 3f);
            var b2 = AddNotable("rb_s2", "Toxic Payload", pB[2], sab,
                "Poisoned enemies explode on death into poison cloud (3m, 4s, 50% DPS).",
                Perks.PoisonCloud, (StatType.Dexterity, ModifierType.Flat, 5f));
            var b3 = AddBranch("rb_s3", "Weakening Agent", pB[3], sab, StatType.Dexterity, 3f, StatType.Charisma, 3f);
            var b4 = AddBranch("rb_s4", "Lingering Effect", pB[4], sab, StatType.Dexterity, 5f);
            var b5 = AddNotable("rb_s5", "Crippling Strike", pB[5], sab,
                "Enemies with 3+ debuffs: +25% damage taken, -30% move speed.",
                Perks.SystemicFailure, (StatType.Dexterity, ModifierType.Flat, 5f));
            var b6 = AddBranch("rb_s6", "Viral Load", pB[6], sab, StatType.Dexterity, 5f, StatType.Intelligence, 3f);
            var b7 = AddCap("rb_s7", "Pandemic", pB[7], sab, 6,
                "DoTs can crit (50% crit chance). DoT crits: 150% tick + spread to 1 nearby.",
                Perks.ViralCascade, (StatType.CritChance, ModifierType.Flat, 0.05f), (StatType.Dexterity, ModifierType.Flat, 5f));
            Chain(t1.Id, b0, b1, b2, b3, b4, b5, b6, b7);

            // C: Scavenger (Loot/Economy) — right
            const string scv = "rb_scavenger";
            var pC = SubPos(splitPos, dir.Rotated(-fan) * 5.0f, 8);
            var c0 = AddBranch("rb_v0", "Salvage Scanner", pC[0], scv, StatType.Luck, 10f);
            var c1 = AddBranch("rb_v1", "Quick Hands", pC[1], scv, StatType.AttackSpeed, 0.10f, StatType.MoveSpeed, 0.5f);
            var c2 = AddNotable("rb_v2", "Opportunist", pC[2], scv,
                "Pickup radius doubled. Pickups heal 3% HP + 5% speed for 2s.",
                Perks.ScrapMagnet, (StatType.Luck, ModifierType.Flat, 5f));
            var c3 = AddBranch("rb_v3", "Lucky Find", pC[3], scv, StatType.Luck, 8f);
            var c4 = AddBranch("rb_v4", "Efficient Recycler", pC[4], scv, StatType.MaxHealth, 10f, StatType.Luck, 5f);
            var c5 = AddNotable("rb_v5", "Bargain Hunter", pC[5], scv,
                "Shop -25%. Sell +50%. Every 500g: +1% damage (max 20%).",
                Perks.VendorDiscount, (StatType.Luck, ModifierType.Flat, 5f));
            var c6 = AddBranch("rb_v6", "Treasure Sense", pC[6], scv, StatType.Luck, 10f);
            var c7 = AddCap("rb_v7", "Jackpot", pC[7], scv, 6,
                "5% kill → consumable. Bosses +1 loot box. Every 10th pickup → random buff.",
                Perks.GoldenTouch, (StatType.Luck, ModifierType.Flat, 10f));
            Chain(t1.Id, c0, c1, c2, c3, c4, c5, c6, c7);

            return new BranchInfo { TrunkMidId = t0.Id, CenterBranchEndId = b7.Id };
        }

        // =====================================================================
        // NOISEBOX — CHA/INT Bard/Controller
        // =====================================================================

        private static BranchInfo BuildNoiseBox(string startId, Vector2 classPos)
        {
            var dir = classPos.Normalized();
            var splitPos = classPos * 0.55f;
            const float fan = 0.6f;

            var t0 = AddBranch("nb_t0", "+8 CHA, +10 Mana", classPos * 0.82f, "nb_trunk",
                StatType.Charisma, 8f, StatType.MaxMana, 10f);
            var t1 = AddBranch("nb_t1", "+5 INT, +15 Mana", splitPos, "nb_trunk",
                StatType.Intelligence, 5f, StatType.MaxMana, 15f);
            _tree.ConnectNodes(startId, t0.Id);
            _tree.ConnectNodes(t0.Id, t1.Id);

            // A: Broadcast (Aura Buffs/Debuffs) — left
            const string bro = "nb_broadcast";
            var pA = SubPos(splitPos, dir.Rotated(fan) * 5.0f, 8);
            var a0 = AddBranch("nb_b0", "Signal Amplifier", pA[0], bro, StatType.Charisma, 5f, StatType.Intelligence, 3f);
            var a1 = AddBranch("nb_b1", "Wide Band", pA[1], bro, StatType.Charisma, 5f, StatType.MaxMana, 10f);
            var a2 = AddNotable("nb_b2", "Morale Booster", pA[2], bro,
                "Allies in 8m: +10% damage, +5% speed. You: doubled bonuses.",
                Perks.RallyFrequency, (StatType.Charisma, ModifierType.Flat, 5f));
            var a3 = AddBranch("nb_b3", "Interference Pattern", pA[3], bro, StatType.Charisma, 5f);
            var a4 = AddBranch("nb_b4", "Jamming Signal", pA[4], bro, StatType.Charisma, 5f, StatType.Intelligence, 3f);
            var a5 = AddNotable("nb_b5", "Disruptive Aura", pA[5], bro,
                "Every 6s pulse (8m): enemies -20% damage 3s. Debuffed also slowed 15%.",
                Perks.FrequencyJam, (StatType.Charisma, ModifierType.Flat, 5f));
            var a6 = AddBranch("nb_b6", "Resonant Field", pA[6], bro, StatType.Charisma, 5f);
            var a7 = AddCap("nb_b7", "Conductor", pA[7], bro, 6,
                "Auras stack. Each active: +8% ALL aura effects. 3+ auras: enemies can't regen.",
                Perks.Orchestrator, (StatType.Charisma, ModifierType.Flat, 8f));
            Chain(t1.Id, a0, a1, a2, a3, a4, a5, a6, a7);

            // B: Dissonance (Confusion/Fear) — center
            const string dis = "nb_dissonance";
            var pB = SubPos(splitPos, dir * 5.0f, 8);
            var b0 = AddBranch("nb_d0", "Feedback Spike", pB[0], dis, StatType.Intelligence, 5f);
            var b1 = AddBranch("nb_d1", "Disorienting Burst", pB[1], dis, StatType.Intelligence, 5f, StatType.Charisma, 3f);
            var b2 = AddNotable("nb_d2", "Cacophony", pB[2], dis,
                "Every 10s scream (6m). 50% flee/attack ally 2s. Bosses: -20% damage.",
                Perks.SonicOverload, (StatType.Charisma, ModifierType.Flat, 5f));
            var b3 = AddBranch("nb_d3", "Noise Floor", pB[3], dis, StatType.Intelligence, 5f, StatType.Charisma, 3f);
            var b4 = AddBranch("nb_d4", "Signal Corruption", pB[4], dis, StatType.Charisma, 5f);
            var b5 = AddNotable("nb_d5", "Mind Worm", pB[5], dis,
                "Confused enemies: +30% damage taken. Confused kills grant XP + on-kill.",
                Perks.NeuralVirus, (StatType.Intelligence, ModifierType.Flat, 5f));
            var b6 = AddBranch("nb_d6", "Echo Chamber", pB[6], dis, StatType.Intelligence, 5f, StatType.Charisma, 3f);
            var b7 = AddCap("nb_d7", "Pandemonium", pB[7], dis, 6,
                "Confused/feared: 15%/s to switch sides permanently. Max 3. +50% damage.",
                Perks.TotalChaos, (StatType.Charisma, ModifierType.Flat, 8f), (StatType.Intelligence, ModifierType.Flat, 5f));
            Chain(t1.Id, b0, b1, b2, b3, b4, b5, b6, b7);

            // C: Resonance (Status Amplification) — right
            const string res = "nb_resonance";
            var pC = SubPos(splitPos, dir.Rotated(-fan) * 5.0f, 8);
            var c0 = AddBranch("nb_r0", "Harmonic Frequency", pC[0], res, StatType.Intelligence, 5f);
            var c1 = AddBranch("nb_r1", "Sympathetic Vibration", pC[1], res, StatType.Intelligence, 5f, StatType.Charisma, 3f);
            var c2 = AddNotable("nb_r2", "Combo Amplifier", pC[2], res,
                "2nd element on target: combo burst (Fire+Ice=blind, etc.).",
                Perks.ElementSynergy, (StatType.Intelligence, ModifierType.Flat, 5f));
            var c3 = AddBranch("nb_r3", "Resonant Frequency", pC[3], res, StatType.Charisma, 5f);
            var c4 = AddBranch("nb_r4", "Sustained Oscillation", pC[4], res, StatType.Charisma, 5f, StatType.Intelligence, 3f);
            var c5 = AddNotable("nb_r5", "Frequency Lock", pC[5], res,
                "Full-duration status: 30% chance to become permanent. Max 2 per enemy.",
                Perks.PermanentDebuff, (StatType.Charisma, ModifierType.Flat, 5f));
            var c6 = AddBranch("nb_r6", "Deep Vibration", pC[6], res, StatType.Intelligence, 5f);
            var c7 = AddCap("nb_r7", "Harmonic Convergence", pC[7], res, 6,
                "4+ status enemies detonate for 500% DoT burst. Spreads all status in 5m.",
                Perks.UltimateCombo, (StatType.Intelligence, ModifierType.Flat, 8f), (StatType.Charisma, ModifierType.Flat, 5f));
            Chain(t1.Id, c0, c1, c2, c3, c4, c5, c6, c7);

            return new BranchInfo { TrunkMidId = t0.Id, CenterBranchEndId = b7.Id };
        }

        // =====================================================================
        // CLUNKER — STR/INT Brawler/Berserker
        // =====================================================================

        private static BranchInfo BuildClunker(string startId, Vector2 classPos)
        {
            var dir = classPos.Normalized();
            var splitPos = classPos * 0.55f;
            const float fan = 0.6f;

            var t0 = AddBranch("cl_t0", "+5 STR, +5 INT", classPos * 0.82f, "cl_trunk",
                StatType.Strength, 5f, StatType.Intelligence, 5f);
            var t1 = AddBranch("cl_t1", "+15 HP, +10 Mana", splitPos, "cl_trunk",
                StatType.MaxHealth, 15f, StatType.MaxMana, 10f);
            _tree.ConnectNodes(startId, t0.Id);
            _tree.ConnectNodes(t0.Id, t1.Id);

            // A: Piston (Melee DPS/Speed) — left
            const string pis = "cl_piston";
            var pA = SubPos(splitPos, dir.Rotated(fan) * 5.0f, 8);
            var a0 = AddBranch("cl_p0", "Rapid Pistons", pA[0], pis, StatType.AttackSpeed, 0.10f, StatType.Strength, 5f);
            var a1 = AddBranch("cl_p1", "Precision Gears", pA[1], pis, StatType.CritChance, 0.05f, StatType.CritDamage, 0.10f);
            var a2 = AddNotable("cl_p2", "Combo Driver", pA[2], pis,
                "Each hit on same target: +5% damage (max +30%). 6+: cleave 40%.",
                Perks.ComboEngine, (StatType.AttackSpeed, ModifierType.Flat, 0.05f));
            var a3 = AddBranch("cl_p3", "Lubricated Joints", pA[3], pis, StatType.AttackSpeed, 0.08f, StatType.MoveSpeed, 0.5f);
            var a4 = AddBranch("cl_p4", "Revving Up", pA[4], pis, StatType.AttackSpeed, 0.05f, StatType.Strength, 3f);
            var a5 = AddNotable("cl_p5", "Piledriver", pA[5], pis,
                "Every 5th basic: charged strike 250% damage + knockback. Always crits.",
                Perks.ImpactCharge, (StatType.Strength, ModifierType.Flat, 5f));
            var a6 = AddBranch("cl_p6", "Perpetual Motion", pA[6], pis, StatType.AttackSpeed, 0.05f, StatType.MoveSpeed, 0.5f);
            var a7 = AddCap("cl_p7", "Machine Gun", pA[7], pis, 6,
                "No AS cap. +1% AS per hit (infinite stacks, 10s). -1% MaxHP/s above 150% AS.",
                Perks.InfiniteCombo, (StatType.AttackSpeed, ModifierType.Flat, 0.10f), (StatType.Strength, ModifierType.Flat, 5f));
            Chain(t1.Id, a0, a1, a2, a3, a4, a5, a6, a7);

            // B: Wrecking Ball (AoE/Knockback) — center
            const string wrk = "cl_wrecking";
            var pB = SubPos(splitPos, dir * 5.0f, 8);
            var b0 = AddBranch("cl_w0", "Heavy Frame", pB[0], wrk, StatType.Strength, 8f);
            var b1 = AddBranch("cl_w1", "Seismic Treads", pB[1], wrk, StatType.Strength, 5f, StatType.MaxHealth, 10f);
            var b2 = AddNotable("cl_w2", "Ground Pound", pB[2], wrk,
                "Dash into enemies: 100% STR Physical + stagger. Wall = double damage.",
                Perks.SeismicSlam, (StatType.Strength, ModifierType.Flat, 5f));
            var b3 = AddBranch("cl_w3", "Massive Impact", pB[3], wrk, StatType.Strength, 5f);
            var b4 = AddBranch("cl_w4", "Collateral Damage", pB[4], wrk, StatType.Strength, 5f, StatType.MaxHealth, 10f);
            var b5 = AddNotable("cl_w5", "Wrecking Swing", pB[5], wrk,
                "Basic attacks hit 180° arc at 60%. Destructibles always drop loot.",
                Perks.Demolition, (StatType.Strength, ModifierType.Flat, 5f));
            var b6 = AddBranch("cl_w6", "Earthquake Treads", pB[6], wrk, StatType.Strength, 5f, StatType.MoveSpeed, 0.5f);
            var b7 = AddCap("cl_w7", "Meteor Drop", pB[7], wrk, 6,
                "Hold dash to charge (2s). Leap AoE: 300-800% STR. Landing: 3s slow (-40%).",
                Perks.OrbitalStrike, (StatType.Strength, ModifierType.Flat, 10f), (StatType.MaxHealth, ModifierType.Flat, 15f));
            Chain(t1.Id, b0, b1, b2, b3, b4, b5, b6, b7);

            // C: Scrap Engine (On-Kill/Momentum) — right
            const string scr = "cl_scrapengine";
            var pC = SubPos(splitPos, dir.Rotated(-fan) * 5.0f, 8);
            var c0 = AddBranch("cl_s0", "Kill Fuel", pC[0], scr, StatType.MaxHealth, 10f, StatType.Strength, 5f);
            var c1 = AddBranch("cl_s1", "Battle Hunger", pC[1], scr, StatType.Strength, 5f, StatType.AttackSpeed, 0.05f);
            var c2 = AddNotable("cl_s2", "Salvage Protocol", pC[2], scr,
                "Kills: 20% chance temp part (Blade/Shield/Booster, 15s).",
                Perks.PartScavenger, (StatType.Strength, ModifierType.Flat, 5f));
            var c3 = AddBranch("cl_s3", "Bloodlust Wiring", pC[3], scr, StatType.AttackSpeed, 0.05f, StatType.Strength, 3f);
            var c4 = AddBranch("cl_s4", "Feeding Frenzy", pC[4], scr, StatType.Strength, 5f, StatType.MaxHealth, 10f);
            var c5 = AddNotable("cl_s5", "Momentum Engine", pC[5], scr,
                "Kills within 2s: +20% damage per chain (max +100%). Breaks after 2s.",
                Perks.KillChain, (StatType.Strength, ModifierType.Flat, 5f));
            var c6 = AddBranch("cl_s6", "Second Wind", pC[6], scr, StatType.MaxHealth, 15f, StatType.Strength, 3f);
            var c7 = AddCap("cl_s7", "Annihilation Engine", pC[7], scr, 6,
                "Below 15% HP: instant execute. +5% threshold per chain (max 35%). Kills explode.",
                Perks.Exterminator, (StatType.Strength, ModifierType.Flat, 8f), (StatType.MaxHealth, ModifierType.Flat, 10f));
            Chain(t1.Id, c0, c1, c2, c3, c4, c5, c6, c7);

            return new BranchInfo { TrunkMidId = t0.Id, CenterBranchEndId = b7.Id };
        }

        // =====================================================================
        // KEYSTONES (2 per class, mutually exclusive) + PINNACLES
        // =====================================================================

        private static void BuildKeystones(BotFrameType cls, string startId, Vector2 classPos)
        {
            var dir = classPos.Normalized();
            var perp = new Vector2(-dir.Y, dir.X);
            var ksAPos = classPos + perp * 3f;
            var ksBPos = classPos - perp * 3f;
            var pinPos = classPos + dir * 2.5f;

            string ksAId = $"ks_{cls}_a";
            string ksBId = $"ks_{cls}_b";
            string pinId = $"pin_{cls}";

            PassiveNodeData ksA, ksB, pin;

            switch (cls)
            {
                case BotFrameType.Scrapheap:
                    ksA = Keystone(ksAId, "Berserker Protocol", ksAPos,
                        "+2% damage per 1% HP missing. Below 30%: +15% AS. -25% MaxHP.",
                        Perks.BerserkerProtocol,
                        (StatType.MaxHealth, ModifierType.Percent, -0.25f));
                    ksB = Keystone(ksBId, "Ablative Plating", ksBPos,
                        "First hit each room = 0 damage. Reset every 10s. +20% armor. -20% speed.",
                        Perks.AblativePlating,
                        (StatType.Armor, ModifierType.Percent, 0.20f),
                        (StatType.MoveSpeed, ModifierType.Percent, -0.20f));
                    pin = Pinnacle(pinId, "Juggernaut Frame", pinPos,
                        "+30% size, +50 HP, +8 armor, immune to knockback. -15% speed.",
                        Perks.JuggernautFrame,
                        (StatType.MaxHealth, ModifierType.Flat, 50f),
                        (StatType.Armor, ModifierType.Flat, 8f),
                        (StatType.MoveSpeed, ModifierType.Percent, -0.15f));
                    break;

                case BotFrameType.TinCan:
                    ksA = Keystone(ksAId, "Iron Fortress", ksAPos,
                        "+50% healing. +30 Armor. Heals affect allies (4m). No dash. -15% speed.",
                        Perks.IronFortress,
                        (StatType.MaxHealth, ModifierType.Flat, 30f),
                        (StatType.Armor, ModifierType.Flat, 10f),
                        (StatType.MoveSpeed, ModifierType.Percent, -0.15f));
                    ksB = Keystone(ksBId, "Galvanic Core", ksBPos,
                        "Armor added as flat ability damage. +20% CDR. -40% basic damage. -20% HP.",
                        Perks.GalvanicCore,
                        (StatType.CooldownReduction, ModifierType.Flat, 0.20f),
                        (StatType.MaxHealth, ModifierType.Percent, -0.20f));
                    pin = Pinnacle(pinId, "Siege Plating", pinPos,
                        "+100 HP, +15 armor. Allies within 6m take 20% less damage.",
                        Perks.SiegePlating,
                        (StatType.MaxHealth, ModifierType.Flat, 100f),
                        (StatType.Armor, ModifierType.Flat, 15f));
                    break;

                case BotFrameType.SparkPlug:
                    ksA = Keystone(ksAId, "Mana Shield", ksAPos,
                        "30% damage from mana. Mana >50%: +15% ability damage. -30% regen.",
                        Perks.ManaShield,
                        (StatType.MaxMana, ModifierType.Flat, 50f));
                    ksB = Keystone(ksBId, "Arcane Conduit", ksBPos,
                        "3+ targets refund 40% mana. Status +50% longer. -25% single target.",
                        Perks.ArcaneConduit,
                        (StatType.MaxMana, ModifierType.Flat, 30f),
                        (StatType.MaxHealth, ModifierType.Percent, -0.15f));
                    pin = Pinnacle(pinId, "Arc Reactor", pinPos,
                        "+50 mana, 20% CDR. Lightning aura damages nearby enemies.",
                        Perks.ArcReactor,
                        (StatType.MaxMana, ModifierType.Flat, 50f),
                        (StatType.CooldownReduction, ModifierType.Flat, 0.20f));
                    break;

                case BotFrameType.RustBucket:
                    ksA = Keystone(ksAId, "Glass Cannon", ksAPos,
                        "+50% damage. Crits +25% bonus. +40% taken. -20% HP.",
                        Perks.GlassCannon,
                        (StatType.CritChance, ModifierType.Flat, 0.10f),
                        (StatType.MaxHealth, ModifierType.Percent, -0.20f));
                    ksB = Keystone(ksBId, "Shadow Processor", ksBPos,
                        "First hit +80% (ambush). Post-dash invisible 1s. 2nd+ hits -20%.",
                        Perks.ShadowProcessor,
                        (StatType.Dexterity, ModifierType.Flat, 10f),
                        (StatType.Armor, ModifierType.Flat, -5f));
                    pin = Pinnacle(pinId, "Assault Frame", pinPos,
                        "+2 dash charges, +25% AS, +15% crit, +20% speed.",
                        Perks.AssaultFrame,
                        (StatType.AttackSpeed, ModifierType.Percent, 0.25f),
                        (StatType.CritChance, ModifierType.Flat, 0.15f),
                        (StatType.MoveSpeed, ModifierType.Percent, 0.20f));
                    break;

                case BotFrameType.NoiseBox:
                    ksA = Keystone(ksAId, "Entropy Field", ksAPos,
                        "8m aura: 3% HP/s Dark. Per debuffed enemy +5% ability (max 30%). -15% healing.",
                        Perks.EntropyField,
                        (StatType.Charisma, ModifierType.Flat, 5f),
                        (StatType.Intelligence, ModifierType.Flat, 5f));
                    ksB = Keystone(ksBId, "Harmonic Resonance", ksBPos,
                        "Abilities apply status AoE (3m). +20% duration. -25% damage, +20% cost.",
                        Perks.HarmonicResonance,
                        (StatType.Charisma, ModifierType.Flat, 8f),
                        (StatType.Intelligence, ModifierType.Flat, 3f));
                    pin = Pinnacle(pinId, "Broadcast Tower", pinPos,
                        "Debuffs spread to enemies within 4m. +50% status duration.",
                        Perks.BroadcastTower,
                        (StatType.Charisma, ModifierType.Percent, 0.30f),
                        (StatType.MaxMana, ModifierType.Flat, 20f));
                    break;

                default: // Clunker
                    ksA = Keystone(ksAId, "Overclocked", ksAPos,
                        "+25% all stats. Basic = +5% target max HP. 5 DPS. Can't heal above 80%.",
                        Perks.Overclocked,
                        (StatType.Strength, ModifierType.Percent, 0.25f),
                        (StatType.Intelligence, ModifierType.Percent, 0.25f),
                        (StatType.Dexterity, ModifierType.Percent, 0.25f));
                    ksB = Keystone(ksBId, "Rampage Core", ksBPos,
                        "Kill within 5s: +10% damage, +5% speed (5 stacks). At 5: no CD 3s.",
                        Perks.RampageCore,
                        (StatType.Strength, ModifierType.Flat, 5f),
                        (StatType.MaxMana, ModifierType.Percent, -0.20f));
                    pin = Pinnacle(pinId, "War Machine", pinPos,
                        "+25% size, +30 HP, +20 mana. -25% ability cost. +10% all damage.",
                        Perks.WarMachine,
                        (StatType.MaxHealth, ModifierType.Flat, 30f),
                        (StatType.MaxMana, ModifierType.Flat, 20f),
                        (StatType.Strength, ModifierType.Flat, 5f),
                        (StatType.Intelligence, ModifierType.Flat, 5f));
                    break;
            }

            ksA.MutuallyExclusiveWith = ksBId;
            ksB.MutuallyExclusiveWith = ksAId;

            _tree.AddNode(ksA);
            _tree.AddNode(ksB);
            _tree.AddNode(pin);

            _tree.ConnectNodes(startId, ksA.Id);
            _tree.ConnectNodes(startId, ksB.Id);
            _tree.ConnectNodes(ksA.Id, pin.Id);
            _tree.ConnectNodes(ksB.Id, pin.Id);
        }

        // =====================================================================
        // INNER RING — 12 nodes (6 defensive + 6 offensive)
        // =====================================================================

        private static void BuildInnerRing(Dictionary<BotFrameType, BranchInfo> branchInfos,
            BotFrameType[] classes)
        {
            const float ringRadius = 3.5f;
            const int ringCount = 12;
            string[] ringIds = new string[ringCount];

            // Defensive nodes at class-aligned positions (even indices)
            var defNodes = new (string name, string desc, string perkId,
                StatType s1, float v1, StatType s2, float v2)[]
            {
                ("Emergency Repairs", "Below 25% HP: heal 20% MaxHP, +50% armor 3s. 60s CD.",
                    Perks.EmergencyRepairs, StatType.MaxHealth, 15f, StatType.Armor, 3f),
                ("Hardened Shell", "", "",
                    StatType.Armor, 8f, StatType.MaxHealth, 15f),
                ("Power Reserve", "", "",
                    StatType.MaxMana, 20f, StatType.CooldownReduction, 0.03f),
                ("Adaptive Plating", "+3% armor per enemy in 8m (max 15%). +2% damage.",
                    Perks.AdaptivePlating, StatType.Armor, 5f, StatType.MaxHealth, 10f),
                ("Quick Recovery", "", "",
                    StatType.Constitution, 4f, StatType.MaxHealth, 15f),
                ("Core Stability", "", "",
                    StatType.MaxHealth, 10f, StatType.MaxMana, 10f),
            };

            // Offensive nodes between classes (odd indices)
            var offNodes = new (string name, string desc, string perkId,
                StatType s1, float v1, StatType s2, float v2)[]
            {
                ("Targeting Matrix", "", "",
                    StatType.CritChance, 0.05f, StatType.CritDamage, 0.10f),
                ("Overclock Module", "", "",
                    StatType.AttackSpeed, 0.08f, StatType.Intelligence, 5f),
                ("Elemental Converter", "Damage type cycles every 5 hits. +10% for weakness.",
                    Perks.TypeShift, StatType.Intelligence, 3f, StatType.Strength, 3f),
                ("Multi-Target", "Single-target attacks: 25% AoE in 3m around target.",
                    Perks.SplashProtocol, StatType.Strength, 3f, StatType.Dexterity, 3f),
                ("Scavenger Module", "", "",
                    StatType.Luck, 8f, StatType.Dexterity, 3f),
                ("Survivor's Instinct", "Below 30%: +20% damage, +20% speed, +10% dodge.",
                    Perks.LastResort, StatType.MaxHealth, 10f, StatType.Strength, 3f),
            };

            for (int i = 0; i < ringCount; i++)
            {
                float angle = i * Mathf.Pi * 2f / ringCount - Mathf.Pi / 2f;
                var pos = new Vector2(Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius);
                var id = $"ring_{i}";

                bool isClassAligned = (i % 2 == 0);
                int dataIndex = isClassAligned ? i / 2 : i / 2;
                var d = isClassAligned ? defNodes[dataIndex] : offNodes[dataIndex];

                PassiveNodeData node;
                if (!string.IsNullOrEmpty(d.perkId))
                {
                    node = Notable(id, d.name, pos, d.desc, d.perkId,
                        (d.s1, ModifierType.Flat, d.v1));
                    if (d.v2 != 0f) node.AddBonus(d.s2, ModifierType.Flat, d.v2);
                }
                else
                {
                    node = new PassiveNodeData(id, d.name, SkillNodeType.Basic, pos);
                    node.AddBonus(d.s1, ModifierType.Flat, d.v1);
                    if (d.v2 != 0f) node.AddBonus(d.s2, ModifierType.Flat, d.v2);
                }

                _tree.AddNode(node);
                ringIds[i] = id;
            }

            // Connect ring in a circle
            for (int i = 0; i < ringCount; i++)
                _tree.ConnectNodes(ringIds[i], ringIds[(i + 1) % ringCount]);

            // Connect center sub-branch ends to class-aligned ring nodes
            for (int i = 0; i < classes.Length; i++)
                _tree.ConnectNodes(branchInfos[classes[i]].CenterBranchEndId, ringIds[i * 2]);
        }

        // =====================================================================
        // CROSS-CLASS BRIDGES — enhanced synergy perks
        // =====================================================================

        private static void BuildBridges(BotFrameType[] classes,
            Dictionary<BotFrameType, Vector2> classPositions,
            Dictionary<BotFrameType, BranchInfo> branchInfos)
        {
            var bridgePerks = new (string name, string desc, string perkId,
                StatType s1, float v1, StatType s2, float v2)[]
            {
                ("Juggernaut Link",
                    "Armor increases healing (1% per 10, max 20%). Healed: +5% damage 3s.",
                    Perks.JuggernautLink, StatType.Strength, 5f, StatType.Armor, 5f),
                ("Mana Armor",
                    "15% max mana as flat armor. Mana spent → shield (10%, max 50).",
                    Perks.ManaArmor, StatType.MaxHealth, 15f, StatType.MaxMana, 15f),
                ("Energy Blade",
                    "Crit damage dealt again as Lightning (30%). Crits restore 3% mana.",
                    Perks.EnergyBlade, StatType.Intelligence, 4f, StatType.Dexterity, 4f),
                ("Predator Protocol",
                    "+20% to debuffed. +10% per unique debuff on target (max 40%).",
                    Perks.ExploitWeakness, StatType.Dexterity, 4f, StatType.Charisma, 4f),
                ("Feedback Frenzy",
                    "Kill speed +5% status damage (max 25%). Status kills: repair orbs.",
                    Perks.FeedbackFrenzy, StatType.Charisma, 4f, StatType.Intelligence, 4f),
                ("Berserker Network",
                    "On kill: +15% speed, +8% crit 4s. Kills extend + refresh.",
                    Perks.AdrenalineRush, StatType.Strength, 4f, StatType.MoveSpeed, 0.5f),
            };

            int nodeId = 0;
            for (int i = 0; i < classes.Length; i++)
            {
                int next = (i + 1) % classes.Length;
                var clsA = classes[i];
                var clsB = classes[next];

                var posA = classPositions[clsA] * 0.68f;
                var posB = classPositions[clsB] * 0.68f;
                var midPos = (posA + posB) * 0.5f;
                var bp = bridgePerks[i];

                var connA = new PassiveNodeData($"bconn_{nodeId++}", "+8 HP",
                    SkillNodeType.Basic, posA.Lerp(midPos, 0.4f));
                connA.AddBonus(StatType.MaxHealth, ModifierType.Flat, 8f);
                _tree.AddNode(connA);

                var connB = new PassiveNodeData($"bconn_{nodeId++}", "+8 HP",
                    SkillNodeType.Basic, posB.Lerp(midPos, 0.4f));
                connB.AddBonus(StatType.MaxHealth, ModifierType.Flat, 8f);
                _tree.AddNode(connB);

                var bridge = Notable($"bridge_{i}", bp.name, midPos, bp.desc, bp.perkId,
                    (bp.s1, ModifierType.Flat, bp.v1));
                bridge.AddBonus(bp.s2, ModifierType.Flat, bp.v2);
                _tree.AddNode(bridge);

                _tree.ConnectNodes(branchInfos[clsA].TrunkMidId, connA.Id);
                _tree.ConnectNodes(connA.Id, bridge.Id);
                _tree.ConnectNodes(branchInfos[clsB].TrunkMidId, connB.Id);
                _tree.ConnectNodes(connB.Id, bridge.Id);
            }
        }

        // =====================================================================
        // CORE SOCKETS — graft slots
        // =====================================================================

        private static void BuildCoreSockets(BotFrameType[] classes,
            Dictionary<BotFrameType, Vector2> classPositions,
            Dictionary<BotFrameType, BranchInfo> branchInfos)
        {
            foreach (var cls in classes)
            {
                var dir = classPositions[cls].Normalized();
                var perp = new Vector2(-dir.Y, dir.X);
                var corePos = classPositions[cls] * 0.68f + perp * 3.5f;
                var coreId = $"core_{cls}";
                var core = new PassiveNodeData(coreId, "Graft Socket", SkillNodeType.CoreSocket, corePos);
                core.Description = "Bolt a harvested organ or living parasite onto your frame.";
                _tree.AddNode(core);
                _tree.ConnectNodes(branchInfos[cls].TrunkMidId, coreId);
            }
        }

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        private static Vector2 HexPos(int index, float radius)
        {
            float angle = index * Mathf.Pi * 2f / 6f - Mathf.Pi / 2f;
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        /// <summary>
        /// Compute positions for N nodes along a sub-branch from splitPos to endPos.
        /// </summary>
        private static Vector2[] SubPos(Vector2 from, Vector2 to, int count)
        {
            var positions = new Vector2[count];
            for (int i = 0; i < count; i++)
                positions[i] = from.Lerp(to, (float)(i + 1) / (count + 1));
            return positions;
        }

        /// <summary>
        /// Connect a chain of nodes: startId → nodes[0] → nodes[1] → ... → nodes[n-1]
        /// </summary>
        private static void Chain(string startId, params PassiveNodeData[] nodes)
        {
            string prevId = startId;
            foreach (var node in nodes)
            {
                _tree.ConnectNodes(prevId, node.Id);
                prevId = node.Id;
            }
        }

        /// <summary>
        /// Create a basic node with flat stat bonuses, assign to a sub-branch, and add to tree.
        /// </summary>
        private static PassiveNodeData AddBranch(string id, string name, Vector2 pos,
            string branchId, StatType s1, float v1, StatType s2 = 0, float v2 = 0f)
        {
            var node = new PassiveNodeData(id, name, SkillNodeType.Basic, pos);
            node.SubBranchId = branchId;
            node.AddBonus(s1, ModifierType.Flat, v1);
            if (v2 != 0f) node.AddBonus(s2, ModifierType.Flat, v2);
            _tree.AddNode(node);
            return node;
        }

        /// <summary>
        /// Create a notable node with perk, assign to a sub-branch, and add to tree.
        /// </summary>
        private static PassiveNodeData AddNotable(string id, string name, Vector2 pos,
            string branchId, string desc, string perkId,
            params (StatType stat, ModifierType mod, float val)[] bonuses)
        {
            var node = new PassiveNodeData(id, name, SkillNodeType.Notable, pos);
            node.Description = desc;
            node.PerkId = perkId;
            node.SubBranchId = branchId;
            foreach (var (stat, mod, val) in bonuses)
                node.AddBonus(stat, mod, val);
            _tree.AddNode(node);
            return node;
        }

        /// <summary>
        /// Create a capstone node with threshold gate, assign to a sub-branch, and add to tree.
        /// </summary>
        private static PassiveNodeData AddCap(string id, string name, Vector2 pos,
            string branchId, int threshold, string desc, string perkId,
            params (StatType stat, ModifierType mod, float val)[] bonuses)
        {
            var node = new PassiveNodeData(id, name, SkillNodeType.Capstone, pos);
            node.Description = desc;
            node.PerkId = perkId;
            node.SubBranchId = branchId;
            node.RequiredPointsInBranch = threshold;
            node.RequiredBranchId = branchId;
            foreach (var (stat, mod, val) in bonuses)
                node.AddBonus(stat, mod, val);
            _tree.AddNode(node);
            return node;
        }

        /// <summary>
        /// Create a notable node (does NOT add to tree — for infrastructure use).
        /// </summary>
        private static PassiveNodeData Notable(string id, string name, Vector2 pos,
            string description, string perkId,
            params (StatType stat, ModifierType mod, float val)[] bonuses)
        {
            var node = new PassiveNodeData(id, name, SkillNodeType.Notable, pos);
            node.Description = description;
            node.PerkId = perkId;
            foreach (var (stat, mod, val) in bonuses)
                node.AddBonus(stat, mod, val);
            return node;
        }

        /// <summary>
        /// Create a keystone node (does NOT add to tree).
        /// </summary>
        private static PassiveNodeData Keystone(string id, string name, Vector2 pos,
            string description, string perkId,
            params (StatType stat, ModifierType mod, float val)[] bonuses)
        {
            var node = new PassiveNodeData(id, name, SkillNodeType.Keystone, pos);
            node.Description = description;
            node.PerkId = perkId;
            foreach (var (stat, mod, val) in bonuses)
                node.AddBonus(stat, mod, val);
            return node;
        }

        /// <summary>
        /// Create a pinnacle node (does NOT add to tree).
        /// </summary>
        private static PassiveNodeData Pinnacle(string id, string name, Vector2 pos,
            string description, string perkId,
            params (StatType stat, ModifierType mod, float val)[] bonuses)
        {
            var node = new PassiveNodeData(id, name, SkillNodeType.Pinnacle, pos);
            node.Description = description;
            node.PerkId = perkId;
            foreach (var (stat, mod, val) in bonuses)
                node.AddBonus(stat, mod, val);
            return node;
        }
    }
}
