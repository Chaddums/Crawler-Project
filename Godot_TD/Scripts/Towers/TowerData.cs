using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Static data definition for a tower type. Registry holds all of these.
    /// </summary>
    public class TowerData
    {
        public string Id;
        public string Name;
        public string Description;
        public TowerType Type;
        public int ResourceCost;
        public int MaxModSlots;
        public DamageType DamageType;

        // Base stats
        public float BaseDamage;
        public float BaseRange;
        public float BaseFireRate;      // Shots per second
        public float BaseHealth;
        public float SplashRadius;       // 0 = single target

        // Visual
        public Color TintColor;
        public float TowerHeight;
    }

    /// <summary>
    /// All tower definitions in the game.
    /// </summary>
    public static class TowerRegistry
    {
        private static readonly Dictionary<TowerType, TowerData> _towers = new();
        private static bool _initialized;

        public static TowerData Get(TowerType type)
        {
            EnsureInit();
            return _towers.TryGetValue(type, out var data) ? data : null;
        }

        public static IEnumerable<TowerData> GetAll()
        {
            EnsureInit();
            return _towers.Values;
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            Register(new TowerData {
                Id = "blaster", Name = "Blaster",
                Description = "Reliable single-target plinker. Fast, cheap, gets the job done.",
                Type = TowerType.Blaster,
                ResourceCost = 15, MaxModSlots = 2,
                DamageType = DamageType.Physical,
                BaseDamage = 8, BaseRange = 6f, BaseFireRate = 2f, BaseHealth = 50,
                TintColor = new Color(0.7f, 0.7f, 0.6f), TowerHeight = 1.2f
            });

            Register(new TowerData {
                Id = "scatter", Name = "Scatter Cannon",
                Description = "Short-range burst. Shreds groups but don't expect finesse.",
                Type = TowerType.Scatter,
                ResourceCost = 25, MaxModSlots = 2,
                DamageType = DamageType.Physical,
                BaseDamage = 15, BaseRange = 3.5f, BaseFireRate = 1f, BaseHealth = 65,
                SplashRadius = 1.5f,
                TintColor = new Color(0.6f, 0.5f, 0.4f), TowerHeight = 1f
            });

            Register(new TowerData {
                Id = "zapper", Name = "Zapper",
                Description = "Chain lightning arcs between clustered enemies. Loves crowds.",
                Type = TowerType.Zapper,
                ResourceCost = 35, MaxModSlots = 2,
                DamageType = DamageType.Lightning,
                BaseDamage = 6, BaseRange = 5f, BaseFireRate = 1.5f, BaseHealth = 40,
                TintColor = new Color(0.3f, 0.5f, 0.9f), TowerHeight = 1.5f
            });

            Register(new TowerData {
                Id = "incinerator", Name = "Incinerator",
                Description = "Continuous flame cone. Melts armor, ignites everything.",
                Type = TowerType.Incinerator,
                ResourceCost = 40, MaxModSlots = 2,
                DamageType = DamageType.Fire,
                BaseDamage = 4, BaseRange = 4f, BaseFireRate = 5f, BaseHealth = 45,
                SplashRadius = 1f,
                TintColor = new Color(0.9f, 0.4f, 0.1f), TowerHeight = 1.3f
            });

            Register(new TowerData {
                Id = "freezer", Name = "Cryo Emitter",
                Description = "Slows everything in range. Doesn't kill, but nothing escapes.",
                Type = TowerType.Freezer,
                ResourceCost = 30, MaxModSlots = 1,
                DamageType = DamageType.Ice,
                BaseDamage = 2, BaseRange = 5f, BaseFireRate = 1f, BaseHealth = 55,
                SplashRadius = 3f,
                TintColor = new Color(0.4f, 0.7f, 0.9f), TowerHeight = 1.1f
            });

            Register(new TowerData {
                Id = "mortar", Name = "Mortar",
                Description = "Lobs explosive shells across the map. Slow but devastating.",
                Type = TowerType.Mortar,
                ResourceCost = 50, MaxModSlots = 3,
                DamageType = DamageType.Fire,
                BaseDamage = 30, BaseRange = 10f, BaseFireRate = 0.4f, BaseHealth = 60,
                SplashRadius = 2f,
                TintColor = new Color(0.4f, 0.35f, 0.3f), TowerHeight = 0.9f
            });

            Register(new TowerData {
                Id = "sniper", Name = "Rail Driver",
                Description = "Extreme range, extreme damage. One shot, one problem solved.",
                Type = TowerType.Sniper,
                ResourceCost = 55, MaxModSlots = 2,
                DamageType = DamageType.Physical,
                BaseDamage = 50, BaseRange = 14f, BaseFireRate = 0.3f, BaseHealth = 35,
                TintColor = new Color(0.5f, 0.5f, 0.5f), TowerHeight = 1.8f
            });

            Register(new TowerData {
                Id = "recycler", Name = "Scrap Recycler",
                Description = "Doesn't shoot. Auto-collects nearby scrap and boosts neighbors.",
                Type = TowerType.Recycler,
                ResourceCost = 20, MaxModSlots = 1,
                DamageType = DamageType.Physical,
                BaseDamage = 0, BaseRange = 4f, BaseFireRate = 0f, BaseHealth = 80,
                TintColor = new Color(0.3f, 0.6f, 0.3f), TowerHeight = 0.8f
            });
        }

        private static void Register(TowerData data)
        {
            _towers[data.Type] = data;
        }
    }
}
