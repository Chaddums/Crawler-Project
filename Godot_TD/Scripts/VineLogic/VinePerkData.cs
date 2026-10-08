using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A perk the player can pick between floors.
    /// Apply modifies SignalTuningEditor static fields (runtime tuning pattern).
    /// </summary>
    public class PerkData
    {
        public string Id;
        public string Name;
        public string Description;
        public Color Color;
        public Action Apply;
        /// <summary>False = kept for save compat but never offered (effect no longer exists).</summary>
        public bool Offered = true;
        /// <summary>The tower this perk changes (its play and its look), if any.</summary>
        public VineNodeType? Tower;
    }

    /// <summary>
    /// Registry of all available perks. Provides random selection excluding already-picked perks.
    /// </summary>
    public static class VinePerkRegistry
    {
        private static List<PerkData> _allPerks;

        public static List<PerkData> GetAll()
        {
            if (_allPerks != null) return _allPerks;
            _allPerks = BuildPerks();
            return _allPerks;
        }

        /// <summary>Weight of a tower perk in the draft when that tower isn't on the field yet.</summary>
        private const float UnbuiltTowerWeight = 0.35f;

        /// <summary>
        /// Up to <paramref name="count"/> different offered perks, not already picked. Perks for a
        /// tower the player hasn't built are less likely (still possible, to tempt a new build).
        /// </summary>
        public static List<PerkData> PickRandom(int count, List<PerkData> exclude)
        {
            var available = new List<PerkData>();
            var excludeIds = new HashSet<string>();
            if (exclude != null)
                foreach (var p in exclude) excludeIds.Add(p.Id);

            foreach (var perk in GetAll())
            {
                if (perk.Offered && !excludeIds.Contains(perk.Id))
                    available.Add(perk);
            }

            var built = BuiltTowers();
            var weights = available.Select(p => p.Tower is VineNodeType t && !built.Contains(t) ? UnbuiltTowerWeight : 1f).ToList();
            var rng = new RandomNumberGenerator();
            var picks = new List<PerkData>();
            while (picks.Count < count && available.Count > 0)
            {
                float roll = rng.Randf() * weights.Sum();
                int i = 0;
                while (i < available.Count - 1 && (roll -= weights[i]) > 0f) i++;
                picks.Add(available[i]);
                available.RemoveAt(i);
                weights.RemoveAt(i);
            }
            return picks;
        }

        /// <summary>Tower types standing on the field right now.</summary>
        private static HashSet<VineNodeType> BuiltTowers()
        {
            var set = new HashSet<VineNodeType>();
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return set;
            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
                if (grid.GetNode(x, y)?.Data is VineNodeData d) set.Add(d.Type);
            return set;
        }

        /// <summary>Whether the current run has picked this perk (no allocation: called per shot).</summary>
        public static bool IsActive(string id)
        {
            var perks = GameManager.Instance?.ActivePerks;
            if (perks == null) return false;
            for (int i = 0; i < perks.Count; i++)
                if (perks[i].Id == id) return true;
            return false;
        }

        private static List<PerkData> BuildPerks()
        {
            return new List<PerkData>
            {
                new PerkData {
                    Id = "overclocked_cores",
                    Name = "Overclocked Cores",
                    Description = "+20% tower damage",
                    Color = new Color(0.9f, 0.4f, 0.1f),
                    Apply = () => SignalTuningEditor.DamageTowerDPS *= 1.2f
                },
                new PerkData {
                    Id = "redundant_shielding",
                    Offered = false, // Spire HP is the loss condition; core lives never matter
                    Name = "Redundant Shielding",
                    Description = "+2 core lives",
                    Color = new Color(0.3f, 0.8f, 0.3f),
                    Apply = () => {
                        SignalTuningEditor.CoreLives += 2;
                        GameManager.Instance?.SetCoreLives(
                            (GameManager.Instance?.CoreLives ?? 0) + 2);
                    }
                },
                new PerkData {
                    Id = "salvage_discount",
                    Name = "Salvage Discount",
                    Description = "+15% sell refund",
                    Color = new Color(0.9f, 0.8f, 0.2f),
                    Apply = () => SignalTuningEditor.SellRefund = Math.Min(1f, SignalTuningEditor.SellRefund + 0.15f)
                },
                new PerkData {
                    Id = "extended_antenna",
                    Offered = false, // Sensors aren't buildable since the tower overhaul
                    Name = "Extended Antenna",
                    Description = "+1 sensor range",
                    Color = new Color(0.2f, 0.9f, 0.4f),
                    Apply = () => SignalTuningEditor.SensorRange += 1f
                },
                new PerkData {
                    Id = "scrap_windfall",
                    Name = "Resource Windfall",
                    Description = "+40 Resources immediately",
                    Color = new Color(0.95f, 0.85f, 0.2f),
                    Apply = () => GameManager.Instance?.AddResources(40)
                },
                new PerkData {
                    Id = "fiber_optics",
                    Offered = false, // Signal chains aren't buildable since the tower overhaul
                    Name = "Fiber Optics",
                    Description = "+30% signal travel speed",
                    Color = new Color(0.0f, 0.85f, 0.95f),
                    Apply = () => SignalTuningEditor.SignalTravelSpeed *= 1.3f
                },
                new PerkData {
                    Id = "viscous_tar",
                    Name = "Viscous Tar",
                    Description = "+15% slow field effectiveness",
                    Color = new Color(0.5f, 0.3f, 0.1f),
                    Apply = () => SignalTuningEditor.SlowFieldAmount = Math.Min(0.9f, SignalTuningEditor.SlowFieldAmount + 0.15f)
                },
                new PerkData {
                    Id = "long_barrel",
                    Name = "Long Barrel",
                    Description = "+1 turret range",
                    Color = new Color(0.7f, 0.4f, 0.9f),
                    Apply = () => SignalTuningEditor.DamageTowerRange += 1f
                },
                // Player-focused perks
                new PerkData {
                    Id = "hardened_shell",
                    Name = "Hardened Shell",
                    Description = "+30 player max HP",
                    Color = new Color(0.5f, 0.8f, 0.9f),
                    Apply = () => {
                        if (ServiceLocator.TryGet<VinePlayer>(out var player))
                            player.MaxHP += 30;
                    }
                },
                new PerkData {
                    Id = "quick_draw",
                    Name = "Quick Draw",
                    Description = "+25% player attack speed",
                    Color = new Color(0.9f, 0.5f, 0.2f),
                    Apply = () => {
                        if (ServiceLocator.TryGet<VinePlayer>(out var player))
                            player.AttackSpeed *= 1.25f;
                    }
                },
                new PerkData {
                    Id = "mana_surge",
                    Name = "Materials Surge",
                    Description = "+50% materials regen",
                    Color = new Color(0.3f, 0.4f, 0.95f),
                    Apply = () => {
                        if (ServiceLocator.TryGet<VinePlayer>(out var player))
                            player.MaterialsRegen *= 1.5f;
                    }
                },
                new PerkData {
                    Id = "overcharged_blast",
                    Name = "Overcharged Blast",
                    Description = "+40% player attack damage",
                    Color = new Color(0.95f, 0.8f, 0.2f),
                    Apply = () => {
                        if (ServiceLocator.TryGet<VinePlayer>(out var player))
                            player.AttackDamage *= 1.4f;
                    }
                },
                // ── Tower perks: each changes how one tower plays, and adds to its look ──
                new PerkData {
                    Id = "arc_conductor",
                    Name = "Arc Conductor",
                    Description = "Tesla Coils arc to 2 more enemies, and each arc keeps 80% of the damage (was 60%)",
                    Color = new Color(0.3f, 0.7f, 1f),
                    Tower = VineNodeType.TeslaCoil,
                },
                new PerkData {
                    Id = "tar_pools",
                    Name = "Tar Pools",
                    Description = "Tar Sprayer gobs leave a pool where they land: enemies in it are slowed 60% for 4 s",
                    Color = new Color(0.35f, 0.25f, 0.55f),
                    Tower = VineNodeType.SlowField,
                },
                new PerkData {
                    Id = "hydraulic_stun",
                    Name = "Hydraulic Stun",
                    Description = "Pneumatic Ram shoves stop enemies dead for 0.8 s (bosses and commanders for a third of that)",
                    Color = new Color(0.95f, 0.75f, 0.15f),
                    Tower = VineNodeType.PushPull,
                },
                new PerkData {
                    Id = "cluster_shells",
                    Name = "Cluster Shells",
                    Description = "Scatter Cannon blasts are 40% wider and hit just as hard at the edge",
                    Color = new Color(0.95f, 0.5f, 0.15f),
                    Tower = VineNodeType.ScatterCannon,
                },
                new PerkData {
                    Id = "piercing_rail",
                    Name = "Piercing Rail",
                    Description = "Junk Turret rounds punch through: up to 2 enemies behind the target take 60%",
                    Color = new Color(0.2f, 0.45f, 0.85f),
                    Tower = VineNodeType.DamageTower,
                },
                new PerkData {
                    Id = "relay_mesh",
                    Name = "Relay Mesh",
                    Description = "Overclock Relays boost towers up to 2 cells away, not just next door",
                    Color = new Color(0.1f, 0.6f, 0.8f),
                    Tower = VineNodeType.BuffEmitter,
                },
                new PerkData {
                    Id = "saturation_fire",
                    Name = "Saturation Fire",
                    Description = "Flak Batteries hit 3 more enemies with every burst",
                    Color = new Color(0.85f, 0.3f, 0.3f),
                    Tower = VineNodeType.FlakBattery,
                },
                new PerkData {
                    Id = "harvester_plating",
                    Name = "Harvester Plating",
                    Description = "+50 harvester max HP",
                    Color = new Color(0.4f, 0.9f, 0.5f),
                    Apply = () => {
                        // Raise max HP as described (was only a 50 HP heal)
                        if (ServiceLocator.TryGet<VineHarvester>(out var h))
                            h.IncreaseMaxHP(50);
                    }
                }
            };
        }
    }
}
