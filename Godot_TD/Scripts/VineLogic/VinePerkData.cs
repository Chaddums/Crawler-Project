using System;
using System.Collections.Generic;
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

        public static List<PerkData> PickRandom(int count, List<PerkData> exclude)
        {
            var available = new List<PerkData>();
            var excludeIds = new HashSet<string>();
            if (exclude != null)
                foreach (var p in exclude) excludeIds.Add(p.Id);

            foreach (var perk in GetAll())
            {
                if (!excludeIds.Contains(perk.Id))
                    available.Add(perk);
            }

            // Shuffle and pick
            var rng = new RandomNumberGenerator();
            for (int i = available.Count - 1; i > 0; i--)
            {
                int j = rng.RandiRange(0, i);
                (available[i], available[j]) = (available[j], available[i]);
            }

            int take = Math.Min(count, available.Count);
            return available.GetRange(0, take);
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
                    Name = "Extended Antenna",
                    Description = "+1 sensor range",
                    Color = new Color(0.2f, 0.9f, 0.4f),
                    Apply = () => SignalTuningEditor.SensorRange += 1f
                },
                new PerkData {
                    Id = "scrap_windfall",
                    Name = "Scrap Windfall",
                    Description = "+40 gold immediately",
                    Color = new Color(0.95f, 0.85f, 0.2f),
                    Apply = () => GameManager.Instance?.AddScrap(40)
                },
                new PerkData {
                    Id = "fiber_optics",
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
                }
            };
        }
    }
}
