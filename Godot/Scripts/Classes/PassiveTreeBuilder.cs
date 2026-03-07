using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Procedurally builds the passive tree with gameplay-changing perks.
    /// 6 class starts in a hexagon, each with a branch of meaningful nodes
    /// leading to build-defining keystones and pinnacle transformations.
    /// Cross-class bridges and an inner defensive ring provide hybrid options.
    /// </summary>
    public static class PassiveTreeBuilder
    {
        private static PassiveTreeData _tree;
        private static bool _built;

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

            const float outerRadius = 8f;
            const float innerRadius = 3.5f;

            var classPositions = new Dictionary<BotFrameType, Vector2>
            {
                { BotFrameType.Scrapheap, HexPos(0, outerRadius) },
                { BotFrameType.TinCan, HexPos(1, outerRadius) },
                { BotFrameType.SparkPlug, HexPos(2, outerRadius) },
                { BotFrameType.RustBucket, HexPos(3, outerRadius) },
                { BotFrameType.NoiseBox, HexPos(4, outerRadius) },
                { BotFrameType.Clunker, HexPos(5, outerRadius) },
            };

            var classes = new[] {
                BotFrameType.Scrapheap, BotFrameType.TinCan,
                BotFrameType.SparkPlug, BotFrameType.RustBucket,
                BotFrameType.NoiseBox, BotFrameType.Clunker
            };

            // 1. Class start nodes
            var classStartIds = new Dictionary<BotFrameType, string>();
            foreach (var (cls, pos) in classPositions)
            {
                var id = $"start_{cls}";
                var node = new PassiveNodeData(id, $"{cls} Start", SkillNodeType.ClassStart, pos);
                node.ClassStartFor = cls;
                _tree.AddNode(node);
                classStartIds[cls] = id;
            }

            // 2. Build each class branch
            var branchEndIds = new Dictionary<BotFrameType, string>();
            var branchMidIds = new Dictionary<BotFrameType, string>();

            foreach (var cls in classes)
            {
                var startPos = classPositions[cls];
                var endPos = startPos.Normalized() * innerRadius;

                string prevId = classStartIds[cls];
                var branchNodes = GetClassBranch(cls, startPos, endPos);

                string midId = null;
                for (int i = 0; i < branchNodes.Count; i++)
                {
                    var bn = branchNodes[i];
                    _tree.AddNode(bn);
                    _tree.ConnectNodes(prevId, bn.Id);
                    prevId = bn.Id;

                    // Track mid notable for bridge connections
                    if (bn.NodeType == SkillNodeType.Notable && midId == null)
                        midId = bn.Id;
                }

                branchEndIds[cls] = prevId;
                branchMidIds[cls] = midId ?? prevId;

                // Keystone beyond start (opposite direction from branch)
                var keystonePos = startPos + startPos.Normalized() * 2f;
                var keystone = GetClassKeystone(cls, keystonePos);
                _tree.AddNode(keystone);
                _tree.ConnectNodes(classStartIds[cls], keystone.Id);

                // Pinnacle beyond keystone
                var pinnaclePos = startPos + startPos.Normalized() * 4f;
                var pinnacle = GetClassPinnacle(cls, pinnaclePos);
                _tree.AddNode(pinnacle);
                _tree.ConnectNodes(keystone.Id, pinnacle.Id);
            }

            // 3. Inner ring — connect branch ends + defensive nodes
            for (int i = 0; i < classes.Length; i++)
            {
                int next = (i + 1) % classes.Length;
                _tree.ConnectNodes(branchEndIds[classes[i]], branchEndIds[classes[next]]);
            }

            BuildInnerRing(branchEndIds, classes);

            // 4. Cross-class bridges
            BuildBridges(classes, classPositions, branchMidIds, outerRadius);

            // 5. Core sockets (one per class)
            BuildCoreSockets(classes, classPositions, branchMidIds, outerRadius);

            _built = true;
            GD.Print($"[PassiveTreeBuilder] Built tree with {_tree.Nodes.Count} nodes");
        }

        // =====================================================================
        // CLASS BRANCHES — each returns a list of nodes along the path
        // =====================================================================

        private static List<PassiveNodeData> GetClassBranch(BotFrameType cls,
            Vector2 startPos, Vector2 endPos)
        {
            return cls switch
            {
                BotFrameType.Scrapheap => BuildScrapheapBranch(startPos, endPos),
                BotFrameType.TinCan => BuildTinCanBranch(startPos, endPos),
                BotFrameType.SparkPlug => BuildSparkPlugBranch(startPos, endPos),
                BotFrameType.RustBucket => BuildRustBucketBranch(startPos, endPos),
                BotFrameType.NoiseBox => BuildNoiseBoxBranch(startPos, endPos),
                BotFrameType.Clunker => BuildClunkerBranch(startPos, endPos),
                _ => new List<PassiveNodeData>()
            };
        }

        // --- SCRAPHEAP: STR/CON Heavy Brawler ---
        private static List<PassiveNodeData> BuildScrapheapBranch(Vector2 start, Vector2 end)
        {
            var nodes = new List<PassiveNodeData>();
            int i = 0;

            nodes.Add(Basic($"sh_{i++}", "+8 Strength", Pos(start, end, 1, 8),
                StatType.Strength, 8f));

            nodes.Add(Basic($"sh_{i++}", "+20 Max HP", Pos(start, end, 2, 8),
                StatType.MaxHealth, 20f));

            nodes.Add(Notable($"sh_{i++}", "Impact Driver", Pos(start, end, 3, 8),
                "Basic attacks have 20% chance to stagger enemies for 0.5s.",
                Perks.ImpactDriver,
                (StatType.Strength, ModifierType.Flat, 3f)));

            nodes.Add(Basic($"sh_{i++}", "+4 Armor", Pos(start, end, 4, 8),
                StatType.Armor, 4f));

            nodes.Add(Basic($"sh_{i++}", "+15 Max HP", Pos(start, end, 5, 8),
                StatType.MaxHealth, 15f));

            nodes.Add(Notable($"sh_{i++}", "Momentum", Pos(start, end, 6, 8),
                "Deal up to +30% more damage the longer you move without stopping. Resets when stationary.",
                Perks.Momentum,
                (StatType.MoveSpeed, ModifierType.Flat, 0.5f)));

            nodes.Add(Basic($"sh_{i++}", "+10% Melee Damage", Pos(start, end, 7, 8),
                StatType.Strength, 5f));

            nodes.Add(Basic($"sh_{i}", "+3 Armor, +10 HP", Pos(start, end, 8, 8),
                StatType.Armor, 3f, StatType.MaxHealth, 10f));

            return nodes;
        }

        // --- TINCAN: CON/STR Tank ---
        private static List<PassiveNodeData> BuildTinCanBranch(Vector2 start, Vector2 end)
        {
            var nodes = new List<PassiveNodeData>();
            int i = 0;

            nodes.Add(Basic($"tc_{i++}", "+25 Max HP", Pos(start, end, 1, 8),
                StatType.MaxHealth, 25f));

            nodes.Add(Basic($"tc_{i++}", "+5 Armor", Pos(start, end, 2, 8),
                StatType.Armor, 5f));

            nodes.Add(Notable($"tc_{i++}", "Reactive Plating", Pos(start, end, 3, 8),
                "When hit, gain +5 armor for 3s. Stacks up to 3 times.",
                Perks.ReactivePlating,
                (StatType.Armor, ModifierType.Flat, 3f)));

            nodes.Add(Basic($"tc_{i++}", "+20 Max HP", Pos(start, end, 4, 8),
                StatType.MaxHealth, 20f));

            nodes.Add(Basic($"tc_{i++}", "+3 Constitution", Pos(start, end, 5, 8),
                StatType.Constitution, 3f));

            nodes.Add(Notable($"tc_{i++}", "Thorns Protocol", Pos(start, end, 6, 8),
                "Reflect 15% of damage taken back to the attacker.",
                Perks.ThornsProtocol,
                (StatType.MaxHealth, ModifierType.Flat, 15f)));

            nodes.Add(Basic($"tc_{i++}", "+5 Armor", Pos(start, end, 7, 8),
                StatType.Armor, 5f));

            nodes.Add(Basic($"tc_{i}", "+4 Strength, +15 HP", Pos(start, end, 8, 8),
                StatType.Strength, 4f, StatType.MaxHealth, 15f));

            return nodes;
        }

        // --- SPARKPLUG: INT/DEX Caster ---
        private static List<PassiveNodeData> BuildSparkPlugBranch(Vector2 start, Vector2 end)
        {
            var nodes = new List<PassiveNodeData>();
            int i = 0;

            nodes.Add(Basic($"sp_{i++}", "+8 Intelligence", Pos(start, end, 1, 8),
                StatType.Intelligence, 8f));

            nodes.Add(Basic($"sp_{i++}", "+20 Max Mana", Pos(start, end, 2, 8),
                StatType.MaxMana, 20f));

            nodes.Add(Notable($"sp_{i++}", "Overcharge", Pos(start, end, 3, 8),
                "Abilities cost 30% more mana but deal 40% more damage.",
                Perks.Overcharge,
                (StatType.Intelligence, ModifierType.Flat, 4f)));

            nodes.Add(Basic($"sp_{i++}", "+15 Max Mana", Pos(start, end, 4, 8),
                StatType.MaxMana, 15f));

            nodes.Add(Basic($"sp_{i++}", "+5% Cooldown Reduction", Pos(start, end, 5, 8),
                StatType.CooldownReduction, 0.05f));

            nodes.Add(Notable($"sp_{i++}", "Chain Lightning", Pos(start, end, 6, 8),
                "Ability hits arc to 1 additional nearby target at 50% damage.",
                Perks.ChainLightning,
                (StatType.Intelligence, ModifierType.Flat, 3f)));

            nodes.Add(Basic($"sp_{i++}", "+10 Max Mana", Pos(start, end, 7, 8),
                StatType.MaxMana, 10f));

            nodes.Add(Basic($"sp_{i}", "+5 INT, +10 Mana", Pos(start, end, 8, 8),
                StatType.Intelligence, 5f, StatType.MaxMana, 10f));

            return nodes;
        }

        // --- RUSTBUCKET: DEX/STR Agile ---
        private static List<PassiveNodeData> BuildRustBucketBranch(Vector2 start, Vector2 end)
        {
            var nodes = new List<PassiveNodeData>();
            int i = 0;

            nodes.Add(Basic($"rb_{i++}", "+8 Dexterity", Pos(start, end, 1, 8),
                StatType.Dexterity, 8f));

            nodes.Add(Basic($"rb_{i++}", "+5% Attack Speed", Pos(start, end, 2, 8),
                StatType.AttackSpeed, 0.05f));

            nodes.Add(Notable($"rb_{i++}", "Ricochet Rounds", Pos(start, end, 3, 8),
                "Projectiles bounce to 1 nearby enemy at 60% damage.",
                Perks.RicochetRounds,
                (StatType.Dexterity, ModifierType.Flat, 3f)));

            nodes.Add(Basic($"rb_{i++}", "+3% Crit Chance", Pos(start, end, 4, 8),
                StatType.CritChance, 0.03f));

            nodes.Add(Basic($"rb_{i++}", "+1 Move Speed", Pos(start, end, 5, 8),
                StatType.MoveSpeed, 1f));

            nodes.Add(Notable($"rb_{i++}", "Smoke Screen", Pos(start, end, 6, 8),
                "Dashing leaves a smoke cloud that blinds enemies for 2s.",
                Perks.SmokeScreen,
                (StatType.Dexterity, ModifierType.Flat, 4f)));

            nodes.Add(Basic($"rb_{i++}", "+5% Attack Speed", Pos(start, end, 7, 8),
                StatType.AttackSpeed, 0.05f));

            nodes.Add(Basic($"rb_{i}", "+5 DEX, +3% Crit", Pos(start, end, 8, 8),
                StatType.Dexterity, 5f, StatType.CritChance, 0.03f));

            return nodes;
        }

        // --- NOISEBOX: CHA/INT Support/Debuffer ---
        private static List<PassiveNodeData> BuildNoiseBoxBranch(Vector2 start, Vector2 end)
        {
            var nodes = new List<PassiveNodeData>();
            int i = 0;

            nodes.Add(Basic($"nb_{i++}", "+8 Charisma", Pos(start, end, 1, 8),
                StatType.Charisma, 8f));

            nodes.Add(Basic($"nb_{i++}", "+15 Max Mana", Pos(start, end, 2, 8),
                StatType.MaxMana, 15f));

            nodes.Add(Notable($"nb_{i++}", "Corrosive Aura", Pos(start, end, 3, 8),
                "Enemies within 5m have -15% armor. You corrode everything nearby.",
                Perks.CorrosiveAura,
                (StatType.Charisma, ModifierType.Flat, 4f)));

            nodes.Add(Basic($"nb_{i++}", "+5 Intelligence", Pos(start, end, 4, 8),
                StatType.Intelligence, 5f));

            nodes.Add(Basic($"nb_{i++}", "+10 Max Mana", Pos(start, end, 5, 8),
                StatType.MaxMana, 10f));

            nodes.Add(Notable($"nb_{i++}", "Feedback Loop", Pos(start, end, 6, 8),
                "Status effects you apply also heal you for 2% max HP per second.",
                Perks.FeedbackLoop,
                (StatType.Charisma, ModifierType.Flat, 3f)));

            nodes.Add(Basic($"nb_{i++}", "+5 Charisma", Pos(start, end, 7, 8),
                StatType.Charisma, 5f));

            nodes.Add(Basic($"nb_{i}", "+4 INT, +4 CHA", Pos(start, end, 8, 8),
                StatType.Intelligence, 4f, StatType.Charisma, 4f));

            return nodes;
        }

        // --- CLUNKER: STR/INT Heavy Hybrid ---
        private static List<PassiveNodeData> BuildClunkerBranch(Vector2 start, Vector2 end)
        {
            var nodes = new List<PassiveNodeData>();
            int i = 0;

            nodes.Add(Basic($"cl_{i++}", "+5 STR, +5 INT", Pos(start, end, 1, 8),
                StatType.Strength, 5f, StatType.Intelligence, 5f));

            nodes.Add(Basic($"cl_{i++}", "+15 HP, +10 Mana", Pos(start, end, 2, 8),
                StatType.MaxHealth, 15f, StatType.MaxMana, 10f));

            nodes.Add(Notable($"cl_{i++}", "Powered Strike", Pos(start, end, 3, 8),
                "Basic attacks consume 10 mana to deal 50% more damage. No mana = normal attacks.",
                Perks.PoweredStrike,
                (StatType.MaxMana, ModifierType.Flat, 10f)));

            nodes.Add(Basic($"cl_{i++}", "+4 Strength", Pos(start, end, 4, 8),
                StatType.Strength, 4f));

            nodes.Add(Basic($"cl_{i++}", "+4 Intelligence", Pos(start, end, 5, 8),
                StatType.Intelligence, 4f));

            nodes.Add(Notable($"cl_{i++}", "Scrap Recycler", Pos(start, end, 6, 8),
                "Killing enemies has 15% chance to drop a repair orb that heals for 10% max HP.",
                Perks.ScrapRecycler,
                (StatType.MaxHealth, ModifierType.Flat, 10f)));

            nodes.Add(Basic($"cl_{i++}", "+10 HP, +10 Mana", Pos(start, end, 7, 8),
                StatType.MaxHealth, 10f, StatType.MaxMana, 10f));

            nodes.Add(Basic($"cl_{i}", "+3 STR, +3 INT, +2 Armor", Pos(start, end, 8, 8),
                StatType.Strength, 3f, StatType.Intelligence, 3f));

            return nodes;
        }

        // =====================================================================
        // CLASS KEYSTONES — build-defining choices
        // =====================================================================

        private static PassiveNodeData GetClassKeystone(BotFrameType cls, Vector2 pos)
        {
            return cls switch
            {
                BotFrameType.Scrapheap => Keystone("ks_Scrapheap", "Berserker Protocol", pos,
                    "Gain +2% damage for every 1% of HP missing. Max HP reduced by 20%. " +
                    "The lower your health, the harder you hit.",
                    Perks.BerserkerProtocol,
                    (StatType.MaxHealth, ModifierType.Percent, -0.20f)),

                BotFrameType.TinCan => Keystone("ks_TinCan", "Iron Fortress", pos,
                    "All healing received increased by 50%. Dash is disabled. " +
                    "You are an immovable wall.",
                    Perks.IronFortress,
                    (StatType.MaxHealth, ModifierType.Flat, 30f),
                    (StatType.Armor, ModifierType.Flat, 5f)),

                BotFrameType.SparkPlug => Keystone("ks_SparkPlug", "Mana Shield", pos,
                    "All damage is taken from mana instead of HP. Your HP is set to 1. " +
                    "Your mana pool IS your life.",
                    Perks.ManaShield,
                    (StatType.MaxMana, ModifierType.Flat, 50f)),

                BotFrameType.RustBucket => Keystone("ks_RustBucket", "Glass Cannon", pos,
                    "+50% damage dealt. +30% damage taken. " +
                    "Kill them before they kill you.",
                    Perks.GlassCannon,
                    (StatType.CritChance, ModifierType.Flat, 0.10f)),

                BotFrameType.NoiseBox => Keystone("ks_NoiseBox", "Entropy Field", pos,
                    "All your direct damage converts to damage-over-time applied over 3s. " +
                    "Total damage increased by 60%. Everything you touch decays.",
                    Perks.EntropyField,
                    (StatType.Charisma, ModifierType.Flat, 5f),
                    (StatType.Intelligence, ModifierType.Flat, 5f)),

                BotFrameType.Clunker => Keystone("ks_Clunker", "Overclocked", pos,
                    "+25% to all base stats. Take 5 damage per second. " +
                    "Running hot — your systems can't sustain this forever.",
                    Perks.Overclocked,
                    (StatType.Strength, ModifierType.Percent, 0.25f),
                    (StatType.Intelligence, ModifierType.Percent, 0.25f),
                    (StatType.Dexterity, ModifierType.Percent, 0.25f)),

                _ => new PassiveNodeData("ks_unknown", "Unknown", SkillNodeType.Keystone, pos)
            };
        }

        // =====================================================================
        // CLASS PINNACLES — mech transformation nodes
        // =====================================================================

        private static PassiveNodeData GetClassPinnacle(BotFrameType cls, Vector2 pos)
        {
            return cls switch
            {
                BotFrameType.Scrapheap => Pinnacle("pin_Scrapheap", "Juggernaut Frame", pos,
                    "Frame upgrade: +30% model size, +50 HP, +8 armor, immune to knockback. " +
                    "-15% move speed. You are the unstoppable force.",
                    Perks.JuggernautFrame,
                    (StatType.MaxHealth, ModifierType.Flat, 50f),
                    (StatType.Armor, ModifierType.Flat, 8f),
                    (StatType.MoveSpeed, ModifierType.Percent, -0.15f)),

                BotFrameType.TinCan => Pinnacle("pin_TinCan", "Siege Plating", pos,
                    "Frame upgrade: +100 HP, +15 armor. Allies within 6m take 20% less damage. " +
                    "You become the shield for your team.",
                    Perks.SiegePlating,
                    (StatType.MaxHealth, ModifierType.Flat, 100f),
                    (StatType.Armor, ModifierType.Flat, 15f)),

                BotFrameType.SparkPlug => Pinnacle("pin_SparkPlug", "Arc Reactor", pos,
                    "Frame upgrade: +50 max mana, 20% CDR. Generate a lightning aura that " +
                    "deals constant damage to nearby enemies. Pure energy.",
                    Perks.ArcReactor,
                    (StatType.MaxMana, ModifierType.Flat, 50f),
                    (StatType.CooldownReduction, ModifierType.Flat, 0.20f)),

                BotFrameType.RustBucket => Pinnacle("pin_RustBucket", "Assault Frame", pos,
                    "Frame upgrade: +2 dash charges, +25% attack speed, +15% crit chance, " +
                    "+20% move speed. A blur of lethal precision.",
                    Perks.AssaultFrame,
                    (StatType.AttackSpeed, ModifierType.Percent, 0.25f),
                    (StatType.CritChance, ModifierType.Flat, 0.15f),
                    (StatType.MoveSpeed, ModifierType.Percent, 0.20f)),

                BotFrameType.NoiseBox => Pinnacle("pin_NoiseBox", "Broadcast Tower", pos,
                    "Frame upgrade: All debuffs you apply spread to enemies within 4m of the target. " +
                    "+50% status effect duration. Weaponized interference.",
                    Perks.BroadcastTower,
                    (StatType.Charisma, ModifierType.Percent, 0.30f),
                    (StatType.MaxMana, ModifierType.Flat, 20f)),

                BotFrameType.Clunker => Pinnacle("pin_Clunker", "War Machine", pos,
                    "Frame upgrade: +25% model size, +30 HP, +20 mana. Abilities cost 25% less. " +
                    "+10% all damage. The ultimate hybrid weapon platform.",
                    Perks.WarMachine,
                    (StatType.MaxHealth, ModifierType.Flat, 30f),
                    (StatType.MaxMana, ModifierType.Flat, 20f),
                    (StatType.Strength, ModifierType.Flat, 5f),
                    (StatType.Intelligence, ModifierType.Flat, 5f)),

                _ => new PassiveNodeData("pin_unknown", "Unknown", SkillNodeType.Pinnacle, pos)
            };
        }

        // =====================================================================
        // INNER RING — universal defensive/utility nodes
        // =====================================================================

        private static void BuildInnerRing(Dictionary<BotFrameType, string> branchEndIds,
            BotFrameType[] classes)
        {
            string prevDefId = null;
            string firstDefId = null;

            var ringNodes = new (string name, string desc, string perkId,
                StatType stat1, float val1, StatType stat2, float val2)[]
            {
                ("Emergency Repairs",
                    "Heal 20% max HP when dropping below 25% HP. 60s cooldown.",
                    Perks.EmergencyRepairs,
                    StatType.MaxHealth, 15f, StatType.Armor, 2f),
                ("Hardened Shell", "",  "",
                    StatType.Armor, 5f, StatType.MaxHealth, 20f),
                ("Power Reserve", "", "",
                    StatType.MaxMana, 25f, StatType.CooldownReduction, 0.03f),
                ("Adaptive Plating",
                    "+3% damage reduction per nearby enemy, max 15%.",
                    Perks.AdaptivePlating,
                    StatType.Armor, 3f, StatType.MaxHealth, 10f),
                ("Quick Recovery", "", "",
                    StatType.Constitution, 4f, StatType.MaxHealth, 15f),
                ("Core Stability", "", "",
                    StatType.MaxHealth, 10f, StatType.MaxMana, 10f),
            };

            for (int i = 0; i < 6; i++)
            {
                var pos = HexPos(i, 2f);
                var defId = $"def_{i}";
                var r = ringNodes[i];

                PassiveNodeData defNode;
                if (!string.IsNullOrEmpty(r.perkId))
                {
                    defNode = Notable(defId, r.name, pos, r.desc, r.perkId,
                        (r.stat1, ModifierType.Flat, r.val1));
                    if (r.val2 != 0f)
                        defNode.AddBonus(r.stat2, ModifierType.Flat, r.val2);
                }
                else
                {
                    defNode = new PassiveNodeData(defId, r.name, SkillNodeType.Basic, pos);
                    defNode.AddBonus(r.stat1, ModifierType.Flat, r.val1);
                    if (r.val2 != 0f)
                        defNode.AddBonus(r.stat2, ModifierType.Flat, r.val2);
                }

                _tree.AddNode(defNode);

                if (prevDefId != null)
                    _tree.ConnectNodes(prevDefId, defId);
                else
                    firstDefId = defId;

                _tree.ConnectNodes(branchEndIds[classes[i]], defId);
                prevDefId = defId;
            }

            if (firstDefId != null && prevDefId != null)
                _tree.ConnectNodes(prevDefId, firstDefId);
        }

        // =====================================================================
        // CROSS-CLASS BRIDGES — synergy perks between adjacent classes
        // =====================================================================

        private static void BuildBridges(BotFrameType[] classes,
            Dictionary<BotFrameType, Vector2> classPositions,
            Dictionary<BotFrameType, string> branchMidIds,
            float outerRadius)
        {
            // Bridge themes: each bridge notable has a gameplay perk
            var bridgePerks = new (string name, string desc, string perkId,
                StatType stat1, float val1, StatType stat2, float val2)[]
            {
                // Scrapheap ↔ TinCan: raw power + toughness
                ("Unstoppable Force", "+6 STR, +4 Armor", "",
                    StatType.Strength, 6f, StatType.Armor, 4f),
                // TinCan ↔ SparkPlug: tanky caster
                ("Fortified Mind", "+20 HP, +20 Mana", "",
                    StatType.MaxHealth, 20f, StatType.MaxMana, 20f),
                // SparkPlug ↔ RustBucket: ability + projectile synergy
                ("Resonance",
                    "Ability hits have 10% chance to apply a random debuff.",
                    Perks.Resonance,
                    StatType.Intelligence, 3f, StatType.Dexterity, 3f),
                // RustBucket ↔ NoiseBox: crit + debuff synergy
                ("Exploit Weakness",
                    "+15% damage to enemies affected by any debuff.",
                    Perks.ExploitWeakness,
                    StatType.Dexterity, 3f, StatType.Charisma, 3f),
                // NoiseBox ↔ Clunker: hybrid support
                ("Arcane Infusion", "+15 Mana, +4 INT", "",
                    StatType.MaxMana, 15f, StatType.Intelligence, 4f),
                // Clunker ↔ Scrapheap: melee + speed
                ("Adrenaline Rush",
                    "+15% move speed and +5% crit for 3s after killing an enemy.",
                    Perks.AdrenalineRush,
                    StatType.Strength, 3f, StatType.MoveSpeed, 0.5f),
            };

            int nodeId = 0;
            for (int i = 0; i < classes.Length; i++)
            {
                int next = (i + 1) % classes.Length;
                var clsA = classes[i];
                var clsB = classes[next];

                var posA = classPositions[clsA].Normalized() * (outerRadius * 0.55f);
                var posB = classPositions[clsB].Normalized() * (outerRadius * 0.55f);
                var midPos = (posA + posB) * 0.5f;

                var bp = bridgePerks[i];

                // Connector A → bridge
                var connA = Basic($"bconn_{nodeId++}", "+3 HP", posA.Lerp(midPos, 0.4f),
                    StatType.MaxHealth, 8f);
                _tree.AddNode(connA);

                // Connector B → bridge
                var connB = Basic($"bconn_{nodeId++}", "+3 HP", posB.Lerp(midPos, 0.4f),
                    StatType.MaxHealth, 8f);
                _tree.AddNode(connB);

                // Bridge notable
                PassiveNodeData bridge;
                if (!string.IsNullOrEmpty(bp.perkId))
                {
                    bridge = Notable($"bridge_{i}", bp.name, midPos, bp.desc, bp.perkId,
                        (bp.stat1, ModifierType.Flat, bp.val1));
                    bridge.AddBonus(bp.stat2, ModifierType.Flat, bp.val2);
                }
                else
                {
                    bridge = new PassiveNodeData($"bridge_{i}", bp.name, SkillNodeType.Notable, midPos);
                    bridge.Description = bp.desc;
                    bridge.AddBonus(bp.stat1, ModifierType.Flat, bp.val1);
                    bridge.AddBonus(bp.stat2, ModifierType.Flat, bp.val2);
                }
                _tree.AddNode(bridge);

                // Wire: branchMid_A → connA → bridge ← connB ← branchMid_B
                if (branchMidIds.TryGetValue(clsA, out var midA))
                    _tree.ConnectNodes(midA, connA.Id);
                _tree.ConnectNodes(connA.Id, bridge.Id);

                if (branchMidIds.TryGetValue(clsB, out var midB))
                    _tree.ConnectNodes(midB, connB.Id);
                _tree.ConnectNodes(connB.Id, bridge.Id);
            }
        }

        // =====================================================================
        // CORE SOCKETS — slottable upgrade points (replaces "jewels")
        // =====================================================================

        private static void BuildCoreSockets(BotFrameType[] classes,
            Dictionary<BotFrameType, Vector2> classPositions,
            Dictionary<BotFrameType, string> branchMidIds,
            float outerRadius)
        {
            foreach (var cls in classes)
            {
                var dir = classPositions[cls].Normalized();
                var perpendicular = new Vector2(-dir.Y, dir.X);
                var corePos = dir * (outerRadius * 0.55f) + perpendicular * 2f;
                var coreId = $"core_{cls}";
                var core = new PassiveNodeData(coreId, "Graft Socket", SkillNodeType.CoreSocket, corePos);
                core.Description = "Bolt a harvested organ or living parasite onto your frame. The things you find in the dungeon didn't die for nothing.";
                _tree.AddNode(core);

                if (branchMidIds.TryGetValue(cls, out var midId))
                    _tree.ConnectNodes(midId, coreId);
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

        private static Vector2 Pos(Vector2 start, Vector2 end, int step, int total)
        {
            return start.Lerp(end, (float)step / (total + 1));
        }

        private static PassiveNodeData Basic(string id, string name, Vector2 pos,
            StatType stat1, float val1, StatType stat2 = 0, float val2 = 0f)
        {
            var node = new PassiveNodeData(id, name, SkillNodeType.Basic, pos);
            node.AddBonus(stat1, ModifierType.Flat, val1);
            if (val2 != 0f) node.AddBonus(stat2, ModifierType.Flat, val2);
            return node;
        }

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
