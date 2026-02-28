using System;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Static damage calculation functions.
    /// </summary>
    public static class DamageCalculator
    {
        private static readonly Random _rng = new();

        /// <summary>
        /// Calculate basic attack damage from attacker stats.
        /// </summary>
        public static DamageInfo CalculateBasicAttack(StatBlock attackerStats, Node attacker,
            Node target, Vector3 hitPoint, Team attackerTeam)
        {
            float baseDamage = attackerStats.GetStat(StatType.Strength);
            float critChance = attackerStats.GetStat(StatType.CritChance) / 100f;
            float critMultiplier = 1f + attackerStats.GetStat(StatType.CritDamage) / 100f;

            bool isCrit = _rng.NextDouble() < critChance;
            float rawDamage = baseDamage * (isCrit ? critMultiplier : 1f);

            return new DamageInfo
            {
                RawDamage = rawDamage,
                FinalDamage = rawDamage, // Reduction applied by target
                IsCritical = isCrit,
                DamageType = DamageType.Physical,
                Attacker = attacker,
                Target = target,
                HitPoint = hitPoint,
                KnockbackForce = 0f,
                StunDuration = 0f
            };
        }

        /// <summary>
        /// Calculate ability damage, scaling from a stat.
        /// </summary>
        public static DamageInfo CalculateAbilityDamage(AbilityData ability, StatBlock attackerStats,
            Node attacker, Node target, Vector3 hitPoint, Team attackerTeam)
        {
            float scalingValue = attackerStats.GetStat(ability.ScalingStat);
            float rawDamage = ability.BaseDamage + scalingValue * ability.ScalingRatio;

            float critChance = attackerStats.GetStat(StatType.CritChance) / 100f;
            float critMultiplier = 1f + attackerStats.GetStat(StatType.CritDamage) / 100f;
            bool isCrit = _rng.NextDouble() < critChance;

            if (isCrit) rawDamage *= critMultiplier;

            return new DamageInfo
            {
                RawDamage = rawDamage,
                FinalDamage = rawDamage,
                IsCritical = isCrit,
                DamageType = ability.DamageType,
                Attacker = attacker,
                Target = target,
                HitPoint = hitPoint,
                KnockbackForce = ability.KnockbackForce,
                StunDuration = ability.StunDuration
            };
        }

        /// <summary>
        /// Apply armor reduction with diminishing returns.
        /// Formula: reduction = armor / (armor + 100)
        /// </summary>
        public static float ApplyArmorReduction(float rawDamage, float armor)
        {
            if (armor <= 0) return rawDamage;
            float reduction = armor / (armor + 100f);
            return rawDamage * (1f - reduction);
        }

        /// <summary>
        /// Apply resistance for elemental damage types.
        /// Resistance is a flat percentage reduction (0-75% cap).
        /// </summary>
        public static float ApplyResistance(float rawDamage, float resistance)
        {
            float clampedRes = Mathf.Clamp(resistance / 100f, 0f, 0.75f);
            return rawDamage * (1f - clampedRes);
        }

        /// <summary>
        /// Full damage pipeline: raw → armor/resistance → final.
        /// </summary>
        public static DamageInfo ProcessDamage(DamageInfo info, StatBlock targetStats)
        {
            float armor = targetStats.GetStat(StatType.Armor);

            if (info.DamageType == DamageType.Physical)
            {
                info.FinalDamage = ApplyArmorReduction(info.RawDamage, armor);
            }
            else
            {
                // Elemental: no armor reduction, but could add resistance later
                info.FinalDamage = info.RawDamage;
            }

            // Minimum 1 damage
            if (info.FinalDamage < 1f) info.FinalDamage = 1f;

            return info;
        }
    }
}
