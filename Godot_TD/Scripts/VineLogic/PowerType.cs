using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The run's power type (Chaos, Power or Environment, picked when the Spire goes down) and
    /// its tier, which grows with the Materials banked this run (Data/power_types.json).
    ///
    /// Chaos: tower hits corrode enemies (each stack makes them take more), Shock Blast throws
    /// enemies back the way they came. Power: towers fire faster and reach further, Overclock
    /// lasts longer and reaches further. Environment: walls cost less and take more, tar and
    /// rams are stronger, resource nodes pay more, Repair Pulse fortifies what it repairs.
    ///
    /// It used to be cosmetic: the type only coloured the Materials the Spire mined.
    /// </summary>
    public static class PowerType
    {
        public static MaterialType Type { get; private set; } = MaterialType.None;
        /// <summary>Materials banked this run (spending them does not lower the tier).</summary>
        public static float Earned { get; private set; }
        public static int Tier { get; private set; }

        private static bool _loaded;
        private static int[] _tiers = { 0, 100, 250, 500, 900, 1500 };
        private static readonly Dictionary<string, float> _v = new();
        private static readonly Dictionary<string, string> _text = new();

        public static bool Chaos => Type == MaterialType.Chaos;
        public static bool Power => Type == MaterialType.Power;
        public static bool Environment => Type == MaterialType.Environment;

        /// <summary>The highest tier.</summary>
        public static int MaxTier { get { Load(); return _tiers.Length - 1; } }

        /// <summary>Materials needed for the next tier (-1 at the top).</summary>
        public static int NextTierAt { get { Load(); return Tier + 1 < _tiers.Length ? _tiers[Tier + 1] : -1; } }

        /// <summary>A new run: no type yet, nothing banked.</summary>
        public static void Reset()
        {
            Type = MaterialType.None;
            Earned = 0f;
            Tier = 0;
        }

        public static void Set(MaterialType type)
        {
            Load();
            Type = type;
            GameEvents.OnPowerTierChanged?.Invoke(Type, Tier);
        }

        /// <summary>Materials banked: the tier may go up.</summary>
        public static void AddEarned(float amount)
        {
            if (amount <= 0f) return;
            Load();
            Earned += amount;
            int tier = 0;
            for (int i = 0; i < _tiers.Length; i++) if (Earned + 0.001f >= _tiers[i]) tier = i;
            if (tier == Tier) return;
            Tier = tier;
            GameEvents.OnPowerTierChanged?.Invoke(Type, Tier);
            GD.Print($"[Power] {Type} tier {Tier} at {Earned:0} Materials banked, W{GameManager.Instance?.CurrentWave ?? 0}");
            if (Type != MaterialType.None)
                GameEvents.OnAnnouncement?.Invoke($"{Name(Type)} tier {Tier}: {TierLine()}");
        }

        /// <summary>Tests: set the tier straight.</summary>
        internal static void TestSet(MaterialType type, int tier)
        {
            Load();
            Type = type;
            Tier = Math.Clamp(tier, 0, _tiers.Length - 1);
            Earned = _tiers[Tier];
        }

        private static float V(string section, string key, float fallback = 0f)
        {
            Load();
            return _v.TryGetValue(section + "." + key, out var v) ? v : fallback;
        }

        private static float Grow(string section, string key, float fallback = 0f)
            => V(section, key, fallback) + V(section, key + "_per_tier") * Tier;

        // ── Chaos ──
        public static float CorrodePerStack => V("chaos", "corrode_per_stack", 0.06f);
        public static int CorrodeStacks => Chaos ? Mathf.RoundToInt(Grow("chaos", "corrode_stacks", 3f)) : 0;
        public static float CorrodeSeconds => V("chaos", "corrode_seconds", 3f);
        public static float ThrowCells => Chaos ? Grow("chaos", "throw_cells", 1.5f) : 0f;

        // ── Power ──
        public static float TowerRateMult => Power ? 1f + Grow("power", "rate", 0.1f) : 1f;
        public static float TowerRangeMult => Power ? 1f + Grow("power", "range", 0.08f) : 1f;
        public static float OverclockSecondsMult => Power ? 1f + Grow("power", "overclock_seconds", 0.5f) : 1f;
        public static float OverclockReachMult => Power ? 1f + Grow("power", "overclock_reach", 0.3f) : 1f;

        // ── Environment ──
        public static float WallCostMult => Environment ? V("environment", "wall_cost", 0.6f) : 1f;
        public static float WallHpMult => Environment ? 1f + Grow("environment", "wall_hp", 0.5f) : 1f;
        public static float SlowMult => Environment ? 1f + Grow("environment", "slow", 0.15f) : 1f;
        public static float RamMult => Environment ? 1f + Grow("environment", "ram", 0.2f) : 1f;
        public static float NodeMult => Environment ? 1f + Grow("environment", "node", 0.25f) : 1f;
        public static float FortifyTaken => V("environment", "fortify_taken", 0.6f);
        public static float FortifySeconds => Environment ? Grow("environment", "fortify_seconds", 8f) : 0f;

        public static string Name(MaterialType t) => t switch
        {
            MaterialType.Chaos => "Chaos",
            MaterialType.Power => "Power",
            MaterialType.Environment => "Environment",
            _ => "None",
        };

        /// <summary>What picking a type does (the picker).</summary>
        public static string PickText(MaterialType t)
        {
            Load();
            return _text.TryGetValue(Key(t) + ".pick", out var s) ? s : "";
        }

        /// <summary>What the current tier gives, in a line (HUD, announcements).</summary>
        public static string TierLine()
        {
            switch (Type)
            {
                case MaterialType.Chaos:
                    return $"hits corrode up to {CorrodeStacks} stacks (+{CorrodeStacks * CorrodePerStack * 100f:0}% damage taken), Shock Blast throws {ThrowCells:0.#} cells";
                case MaterialType.Power:
                    return $"towers +{(TowerRateMult - 1f) * 100f:0}% fire rate, +{(TowerRangeMult - 1f) * 100f:0}% range; Overclock +{(OverclockSecondsMult - 1f) * 100f:0}% time";
                case MaterialType.Environment:
                    return $"walls {(1f - WallCostMult) * 100f:0}% cheaper, +{(WallHpMult - 1f) * 100f:0}% health; tar +{(SlowMult - 1f) * 100f:0}%, rams +{(RamMult - 1f) * 100f:0}%, nodes +{(NodeMult - 1f) * 100f:0}%";
                default: return "";
            }
        }

        private static string Key(MaterialType t) => t switch
        {
            MaterialType.Chaos => "chaos",
            MaterialType.Power => "power",
            MaterialType.Environment => "environment",
            _ => "",
        };

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            const string path = "res://Data/power_types.json";
            if (!Godot.FileAccess.FileExists(path)) return;
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) return;
            var d = parsed.AsGodotDictionary();
            if (d.ContainsKey("tiers") && d["tiers"].VariantType == Variant.Type.Array)
            {
                var list = new List<int>();
                foreach (var t in d["tiers"].AsGodotArray()) list.Add((int)t.AsDouble());
                if (list.Count > 0) _tiers = list.ToArray();
            }
            foreach (var section in new[] { "chaos", "power", "environment" })
            {
                if (!d.ContainsKey(section) || d[section].VariantType != Variant.Type.Dictionary) continue;
                var s = d[section].AsGodotDictionary();
                foreach (var key in s.Keys)
                {
                    string k = key.AsString();
                    var val = s[key];
                    if (val.VariantType is Variant.Type.Float or Variant.Type.Int) _v[section + "." + k] = (float)val.AsDouble();
                    else if (val.VariantType == Variant.Type.String) _text[section + "." + k] = val.AsString();
                }
            }
        }
    }
}
