using System;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The relics this battle is playing with, one per kind (Data/relics.json), and what each
    /// changes, read live by the systems they touch (BIT's weapon, tower damage and costs, the
    /// dome, the Spire). <see cref="Begin"/> takes them from the inventory when a battle starts and
    /// again whenever one is equipped mid-run (from a crate's reveal); <see cref="Changed"/> tells
    /// the battle to re-dress BIT, the towers and the Spire.
    /// </summary>
    public static class RelicRun
    {
        public const string CryoFists = "cryo_fists", StormLance = "storm_lance", DroneBay = "drone_bay";
        public const string MoltenRounds = "molten_rounds", LivingWalls = "living_walls", GravityRams = "gravity_rams";
        public const string OrbitalUplink = "orbital_uplink", OverchargedDome = "overcharged_dome", SiphonCore = "siphon_core";

        /// <summary>The relic in each slot this battle (null: empty).</summary>
        public static string Bit { get; private set; }
        public static string Towers { get; private set; }
        public static string Spire { get; private set; }

        /// <summary>A slot changed: (kind, the relic that was there, the relic now there).</summary>
        public static event Action<string, string, string> Changed;

        public static bool Has(string id) => id != null && (Bit == id || Towers == id || Spire == id);

        /// <summary>A number from a relic's tuning.</summary>
        public static float T(string id, string key, float fallback = 0f) => RelicRegistry.Get(id)?.T(key, fallback) ?? fallback;

        /// <summary>Take the equipped relics from the inventory (battle start, and after an equip).</summary>
        public static void Begin()
        {
            Set("bit", RelicInventory.EquippedOfKind("bit"));
            Set("towers", RelicInventory.EquippedOfKind("towers"));
            Set("spire", RelicInventory.EquippedOfKind("spire"));
        }

        /// <summary>Empty every slot (tests, the menus).</summary>
        public static void Clear() { Set("bit", null); Set("towers", null); Set("spire", null); }

        private static void Set(string kind, string id)
        {
            string old = kind switch { "bit" => Bit, "towers" => Towers, _ => Spire };
            if (old == id) return;
            switch (kind)
            {
                case "bit": Bit = id; break;
                case "towers": Towers = id; break;
                default: Spire = id; break;
            }
            GD.Print($"[Relics] {RelicRegistry.KindName(kind)}: {(id == null ? "none" : RelicRegistry.Get(id)?.Name ?? id)}");
            Changed?.Invoke(kind, old, id);
        }

        // ── What they change, for the systems that read them ──

        /// <summary>Molten Rounds: Junk Turrets and Scatter Cannons set what they hit burning.</summary>
        public static bool Ignites(VineNodeType t) => Towers == MoltenRounds && t is VineNodeType.DamageTower or VineNodeType.ScatterCannon;

        /// <summary>A tower relic's damage trade-off on this type (Molten Rounds: Tesla and Flak).</summary>
        public static float TowerDamageMult(VineNodeType t)
            => Towers == MoltenRounds && t is VineNodeType.TeslaCoil or VineNodeType.FlakBattery ? T(MoltenRounds, "otherMult", 0.75f) : 1f;

        /// <summary>Living Walls: walls cost more.</summary>
        public static float WallCostMult => Towers == LivingWalls ? T(LivingWalls, "costMult", 2f) : 1f;

        public static bool GravityPull => Towers == GravityRams;

        /// <summary>Overcharged Dome: how much bigger the dome is.</summary>
        public static float DomeRadiusMult => Spire == OverchargedDome ? T(OverchargedDome, "radiusMult", 1.5f) : 1f;
        /// <summary>Overcharged Dome: extra fire rate for towers inside, and the cut for those outside.</summary>
        public static float DomeInsideRate => Spire == OverchargedDome ? T(OverchargedDome, "insideRate", 0.25f) : 0f;
        public static float DomeOutsideRate => Spire == OverchargedDome ? T(OverchargedDome, "outsideRate", -0.25f) : 0f;

        /// <summary>Siphon Core: the Spire's health share.</summary>
        public static float SpireHpMult => Spire == SiphonCore ? T(SiphonCore, "hpMult", 0.5f) : 1f;

        /// <summary>Orbital Uplink: the Spire's guns are quiet and it calls lances instead.</summary>
        public static bool Uplink => Spire == OrbitalUplink;
    }
}
