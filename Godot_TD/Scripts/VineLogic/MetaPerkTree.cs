using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    public enum MetaPerkLane { Network, Player, Harvester }

    public class MetaPerkNode
    {
        public int Id;
        public string Name;
        public string Description;
        public MetaPerkLane Lane;
        public int Tier;
        public bool IsNotable;
        public Action Apply;
    }

    /// <summary>
    /// Registry of all 25 meta perk nodes and allocation validation logic.
    /// Meta perks persist across runs via MetaPerkSave.
    /// </summary>
    public static class MetaPerkRegistry
    {
        private static List<MetaPerkNode> _nodes;

        public static List<MetaPerkNode> GetAll()
        {
            if (_nodes != null) return _nodes;
            _nodes = BuildNodes();
            return _nodes;
        }

        public static MetaPerkNode GetNode(int id)
        {
            var all = GetAll();
            if (id >= 0 && id < all.Count) return all[id];
            return null;
        }

        /// <summary>
        /// Check if a node can be allocated given the current set of allocated node IDs.
        /// </summary>
        public static bool CanAllocate(int nodeId, HashSet<int> allocated)
        {
            if (allocated.Contains(nodeId)) return false;
            var node = GetNode(nodeId);
            if (node == null) return false;

            // Root is always free/auto-allocated at startup
            if (node.Tier == 0) return false;

            // Only one node per tier — this enforces lane-locking between notables
            if (HasAllocatedInTier(allocated, node.Tier)) return false;

            switch (node.Tier)
            {
                case 1:
                    // Requires root (id 0)
                    return allocated.Contains(0);

                case 2:
                    // Lane-locked: requires the tier 1 node in the same lane
                    return HasAllocatedInLaneTier(allocated, node.Lane, 1);

                case 3:
                    // Notable crossover: requires ANY tier 2 node (can swap lanes)
                    return HasAllocatedInTier(allocated, 2);

                case 4:
                    // Free lane choice: requires ANY tier 3 notable
                    return HasAllocatedInTier(allocated, 3);

                case 5:
                    // Lane-locked: requires the tier 4 node in the same lane
                    return HasAllocatedInLaneTier(allocated, node.Lane, 4);

                case 6:
                    // Notable crossover: requires ANY tier 5 node (can swap lanes)
                    return HasAllocatedInTier(allocated, 5);

                case 7:
                    // Free lane choice: requires ANY tier 6 notable
                    return HasAllocatedInTier(allocated, 6);

                case 8:
                    // Lane-locked: requires the tier 7 node in the same lane
                    return HasAllocatedInLaneTier(allocated, node.Lane, 7);
            }

            return false;
        }

        private static bool HasAllocatedInTier(HashSet<int> allocated, int tier)
        {
            foreach (var node in GetAll())
            {
                if (node.Tier == tier && allocated.Contains(node.Id))
                    return true;
            }
            return false;
        }

        private static bool HasAllocatedInLaneTier(HashSet<int> allocated, MetaPerkLane lane, int tier)
        {
            foreach (var node in GetAll())
            {
                if (node.Tier == tier && node.Lane == lane && allocated.Contains(node.Id))
                    return true;
            }
            return false;
        }

        private static List<MetaPerkNode> BuildNodes()
        {
            return new List<MetaPerkNode>
            {
                // Tier 0 — Root (auto-allocated)
                new MetaPerkNode {
                    Id = 0, Name = "Neural Link", Description = "Connection established.",
                    Lane = MetaPerkLane.Network, Tier = 0, IsNotable = false,
                    Apply = () => { }
                },

                // Tier 1
                new MetaPerkNode {
                    Id = 1, Name = "Core Overclock", Description = "+10% tower DPS",
                    Lane = MetaPerkLane.Network, Tier = 1, IsNotable = false,
                    Apply = () => SignalTuningEditor.DamageTowerDPS *= 1.1f
                },
                new MetaPerkNode {
                    Id = 2, Name = "Armor Plating", Description = "+15 max HP",
                    Lane = MetaPerkLane.Player, Tier = 1, IsNotable = false,
                    Apply = () => SignalTuningEditor.PlayerMaxHPBonus += 15f
                },
                new MetaPerkNode {
                    Id = 3, Name = "Salvage Rig", Description = "+10 starting gold",
                    Lane = MetaPerkLane.Harvester, Tier = 1, IsNotable = false,
                    Apply = () => SignalTuningEditor.StartingGold += 10
                },

                // Tier 2
                new MetaPerkNode {
                    Id = 4, Name = "Antenna Array", Description = "+1 sensor range",
                    Lane = MetaPerkLane.Network, Tier = 2, IsNotable = false,
                    Apply = () => SignalTuningEditor.SensorRange += 1f
                },
                new MetaPerkNode {
                    Id = 5, Name = "Reflex Mod", Description = "+15% attack speed",
                    Lane = MetaPerkLane.Player, Tier = 2, IsNotable = false,
                    Apply = () => SignalTuningEditor.PlayerAttackSpeedMult *= 1.15f
                },
                new MetaPerkNode {
                    Id = 6, Name = "Wave Processor", Description = "+5 wave bonus gold",
                    Lane = MetaPerkLane.Harvester, Tier = 2, IsNotable = false,
                    Apply = () => SignalTuningEditor.WaveBonus += 5
                },

                // Tier 3 — Notables
                new MetaPerkNode {
                    Id = 7, Name = "Overclocked Grid", Description = "+25% tower DPS",
                    Lane = MetaPerkLane.Network, Tier = 3, IsNotable = true,
                    Apply = () => SignalTuningEditor.DamageTowerDPS *= 1.25f
                },
                new MetaPerkNode {
                    Id = 8, Name = "Reinforced Shell", Description = "+30 max HP",
                    Lane = MetaPerkLane.Player, Tier = 3, IsNotable = true,
                    Apply = () => SignalTuningEditor.PlayerMaxHPBonus += 30f
                },
                new MetaPerkNode {
                    Id = 9, Name = "Emergency Reserve", Description = "+1 core lives, +20 gold",
                    Lane = MetaPerkLane.Harvester, Tier = 3, IsNotable = true,
                    Apply = () => {
                        SignalTuningEditor.CoreLives += 1;
                        SignalTuningEditor.StartingGold += 20;
                    }
                },

                // Tier 4
                new MetaPerkNode {
                    Id = 10, Name = "Signal Boost", Description = "+15% signal speed",
                    Lane = MetaPerkLane.Network, Tier = 4, IsNotable = false,
                    Apply = () => SignalTuningEditor.SignalTravelSpeed *= 1.15f
                },
                new MetaPerkNode {
                    Id = 11, Name = "Power Strike", Description = "+20% attack damage",
                    Lane = MetaPerkLane.Player, Tier = 4, IsNotable = false,
                    Apply = () => SignalTuningEditor.PlayerAttackDamageMult *= 1.2f
                },
                new MetaPerkNode {
                    Id = 12, Name = "Efficient Recycler", Description = "+10% sell refund",
                    Lane = MetaPerkLane.Harvester, Tier = 4, IsNotable = false,
                    Apply = () => SignalTuningEditor.SellRefund = Math.Min(1f, SignalTuningEditor.SellRefund + 0.1f)
                },

                // Tier 5
                new MetaPerkNode {
                    Id = 13, Name = "Extended Barrel", Description = "+1 tower range",
                    Lane = MetaPerkLane.Network, Tier = 5, IsNotable = false,
                    Apply = () => SignalTuningEditor.DamageTowerRange += 1f
                },
                new MetaPerkNode {
                    Id = 14, Name = "Capacitor Bank", Description = "+20 max mana",
                    Lane = MetaPerkLane.Player, Tier = 5, IsNotable = false,
                    Apply = () => SignalTuningEditor.PlayerMaxManaBonus += 20f
                },
                new MetaPerkNode {
                    Id = 15, Name = "Hardened Core", Description = "+1 core lives",
                    Lane = MetaPerkLane.Harvester, Tier = 5, IsNotable = false,
                    Apply = () => SignalTuningEditor.CoreLives += 1
                },

                // Tier 6 — Notables
                new MetaPerkNode {
                    Id = 16, Name = "Precision Array", Description = "+2 tower range",
                    Lane = MetaPerkLane.Network, Tier = 6, IsNotable = true,
                    Apply = () => SignalTuningEditor.DamageTowerRange += 2f
                },
                new MetaPerkNode {
                    Id = 17, Name = "Battle Mod", Description = "+50% mana regen, +15% atk spd",
                    Lane = MetaPerkLane.Player, Tier = 6, IsNotable = true,
                    Apply = () => {
                        SignalTuningEditor.PlayerManaRegenMult *= 1.5f;
                        SignalTuningEditor.PlayerAttackSpeedMult *= 1.15f;
                    }
                },
                new MetaPerkNode {
                    Id = 18, Name = "Quantum Harvester", Description = "2x harvester income",
                    Lane = MetaPerkLane.Harvester, Tier = 6, IsNotable = true,
                    Apply = () => SignalTuningEditor.HarvesterIncomeMult *= 2f
                },

                // Tier 7
                new MetaPerkNode {
                    Id = 19, Name = "Viscous Coating", Description = "+10% slow",
                    Lane = MetaPerkLane.Network, Tier = 7, IsNotable = false,
                    Apply = () => SignalTuningEditor.SlowFieldAmount = Math.Min(0.9f, SignalTuningEditor.SlowFieldAmount + 0.1f)
                },
                new MetaPerkNode {
                    Id = 20, Name = "Mana Conduit", Description = "+25% mana regen",
                    Lane = MetaPerkLane.Player, Tier = 7, IsNotable = false,
                    Apply = () => SignalTuningEditor.PlayerManaRegenMult *= 1.25f
                },
                new MetaPerkNode {
                    Id = 21, Name = "Scrap Magnet", Description = "+15 starting gold",
                    Lane = MetaPerkLane.Harvester, Tier = 7, IsNotable = false,
                    Apply = () => SignalTuningEditor.StartingGold += 15
                },

                // Tier 8
                new MetaPerkNode {
                    Id = 22, Name = "Turret Mastery", Description = "+15% tower DPS",
                    Lane = MetaPerkLane.Network, Tier = 8, IsNotable = false,
                    Apply = () => SignalTuningEditor.DamageTowerDPS *= 1.15f
                },
                new MetaPerkNode {
                    Id = 23, Name = "Hull Upgrade", Description = "+15 max HP",
                    Lane = MetaPerkLane.Player, Tier = 8, IsNotable = false,
                    Apply = () => SignalTuningEditor.PlayerMaxHPBonus += 15f
                },
                new MetaPerkNode {
                    Id = 24, Name = "Income Stream", Description = "+1 harvester income",
                    Lane = MetaPerkLane.Harvester, Tier = 8, IsNotable = false,
                    Apply = () => SignalTuningEditor.HarvesterIncomeBonus += 1
                },
            };
        }
    }
}
