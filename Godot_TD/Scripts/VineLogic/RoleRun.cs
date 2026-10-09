using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What the role picked before a run changes for that run, read from the Spire's
    /// Data/Spires/*.json "role.bonuses". Every role builds every tower; the role decides how
    /// the run plays. Bruteforge (weapons): cheaper upgrades, harder Junk Turrets and Scatter
    /// Cannons, a Spire that gains guns. Arcanist (signals): stronger crew links and relays,
    /// harder Tesla Coils, a shield that recharges fast and shocks when it breaks. Obelisk
    /// (field control): a stronger BIT, harder slows and shoves, a beam that chains.
    /// Set by <see cref="GameManager.ApplyMetaPerks"/> after the perk tree; everything here is 1
    /// (or 0) when no role applies.
    /// </summary>
    public static class RoleRun
    {
        public static string Role { get; private set; } = "";

        public static float UpgradeCostMult = 1f, StrikeCostMult = 1f;
        public static int StartGuns;
        /// <summary>Bruteforge: one more Spire gun every this many waves (0 = off), up to <see cref="WaveGunsMax"/>.</summary>
        public static int WaveGunsEvery, WaveGunsMax;
        /// <summary>Added to each crew link's bonus (0.05 doubles it).</summary>
        public static float CrewBonusAdd;
        public static int RelayReach;
        public static float RelayStrengthMult = 1f;
        public static float ShieldRechargeMult = 1f;
        /// <summary>Arcanist: the shield breaking stuns everything within <see cref="ShieldPulseRadius"/> for this long (0 = off).</summary>
        public static float ShieldPulseStun, ShieldPulseRadius, ShieldPulseDamage;
        public static float BitDamageMult = 1f, BitHpBonus, AbilityCostMult = 1f, AbilityPowerMult = 1f;
        public static float SlowMult = 1f, ForceMult = 1f;
        public static float BeamMult = 1f;
        /// <summary>Obelisk: the beam jumps on to this many more enemies (0 = off).</summary>
        public static int BeamChain;
        public static float BeamChainShare = 0.5f, BeamChainRange = 7f;
        private static readonly Dictionary<VineNodeType, float> _typeDamage = new();

        /// <summary>Extra damage share for this tower type (0.2 = +20%).</summary>
        public static float TypeDamageOf(VineNodeType type) => _typeDamage.TryGetValue(type, out var v) ? v : 0f;

        /// <summary>Spire guns earned from waves so far (Bruteforge).</summary>
        public static int WaveGuns(int wave) => WaveGunsEvery > 0 ? Mathf.Clamp(wave / WaveGunsEvery, 0, WaveGunsMax) : 0;

        public static void Reset()
        {
            Role = "";
            UpgradeCostMult = 1f; StrikeCostMult = 1f; StartGuns = 0; WaveGunsEvery = 0; WaveGunsMax = 0;
            CrewBonusAdd = 0f; RelayReach = 0; RelayStrengthMult = 1f; ShieldRechargeMult = 1f;
            ShieldPulseStun = 0f; ShieldPulseRadius = 0f; ShieldPulseDamage = 0f;
            BitDamageMult = 1f; BitHpBonus = 0f; AbilityCostMult = 1f; AbilityPowerMult = 1f;
            SlowMult = 1f; ForceMult = 1f; BeamMult = 1f; BeamChain = 0; BeamChainShare = 0.5f; BeamChainRange = 7f;
            _typeDamage.Clear();
        }

        /// <summary>Set this run's role bonuses from its Spire data (unknown role: none).</summary>
        public static void Apply(string role)
        {
            Reset();
            var data = string.IsNullOrEmpty(role) ? null : SpireData.Get(role);
            if (data == null) return;
            Role = data.Id;
            foreach (var b in data.RoleBonuses)
            {
                float pct = b.Value / 100f;
                switch (b.Effect)
                {
                    case "upgrade_cost": UpgradeCostMult *= 1f - pct; break;
                    case "strike_cost": StrikeCostMult *= 1f - pct; break;
                    case "start_guns": StartGuns += Mathf.RoundToInt(b.Value); break;
                    case "wave_guns": WaveGunsEvery = Mathf.Max(1, Mathf.RoundToInt(b.Value)); WaveGunsMax = Mathf.RoundToInt(b.Max); break;
                    case "tower_damage":
                        foreach (var t in b.Towers) _typeDamage[t] = TypeDamageOf(t) + pct;
                        break;
                    case "crew_bonus": CrewBonusAdd += pct; break;
                    case "relay_reach": RelayReach = Mathf.Max(RelayReach, Mathf.RoundToInt(b.Value)); break;
                    case "relay_strength": RelayStrengthMult *= 1f + pct; break;
                    case "shield_recharge": ShieldRechargeMult *= 1f + pct; break;
                    case "shield_pulse": ShieldPulseStun = b.Value; ShieldPulseRadius = b.Radius; ShieldPulseDamage = b.Damage; break;
                    case "bit_damage": BitDamageMult *= 1f + pct; break;
                    case "bit_hp": BitHpBonus += b.Value; break;
                    case "ability_cost": AbilityCostMult *= 1f - pct; break;
                    case "ability_power": AbilityPowerMult *= 1f + pct; break;
                    case "slow": SlowMult *= 1f + pct; break;
                    case "shove": ForceMult *= 1f + pct; break;
                    case "beam_damage": BeamMult *= 1f + pct; break;
                    case "beam_chain":
                        BeamChain = Mathf.RoundToInt(b.Value);
                        if (b.Share > 0f) BeamChainShare = b.Share / 100f;
                        if (b.Range > 0f) BeamChainRange = b.Range;
                        break;
                    default: GD.PrintErr($"[RoleRun] {data.Id}: unknown role effect '{b.Effect}'"); break;
                }
            }
            GD.Print($"[RoleRun] {data.Id}: {data.RoleBonuses.Count} role bonuses");
        }
    }
}
