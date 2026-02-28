using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public static class DamageCalculator
    {
        public static DamageInfo CalculateBasicAttack(IAttacker attacker)
        {
            float baseDamage = attacker.Stats.GetStat(StatType.Strength);
            float critChance = attacker.Stats.GetStat(StatType.CritChance);
            float critMultiplier = Mathf.Max(1.5f, attacker.Stats.GetStat(StatType.CritDamage));
            bool isCrit = Random.value < critChance;

            float finalDamage = baseDamage;
            if (isCrit) finalDamage *= critMultiplier;

            return new DamageInfo
            {
                RawDamage = baseDamage,
                FinalDamage = finalDamage,
                IsCritical = isCrit,
                DamageType = DamageType.Physical,
                Attacker = attacker.Transform.gameObject,
                HitPoint = attacker.Transform.position,
                StatusEffects = null
            };
        }

        public static DamageInfo CalculateAbilityDamage(
            IAttacker attacker, float baseDmg, DamageType damageType,
            StatType scalingStat, float scalingRatio,
            List<ScriptableObject> statusEffects,
            float knockbackForce = 0f, float stunDuration = 0f)
        {
            float baseDamage = baseDmg;

            // Scale with stat
            if (scalingRatio > 0)
            {
                float scalingStatValue = attacker.Stats.GetStat(scalingStat);
                baseDamage += scalingStatValue * scalingRatio;
            }

            float critChance = attacker.Stats.GetStat(StatType.CritChance);
            float critMultiplier = Mathf.Max(1.5f, attacker.Stats.GetStat(StatType.CritDamage));
            bool isCrit = Random.value < critChance;

            float finalDamage = baseDamage;
            if (isCrit) finalDamage *= critMultiplier;

            return new DamageInfo
            {
                RawDamage = baseDamage,
                FinalDamage = finalDamage,
                IsCritical = isCrit,
                DamageType = damageType,
                Attacker = attacker.Transform.gameObject,
                HitPoint = attacker.Transform.position,
                StatusEffects = statusEffects,
                KnockbackForce = knockbackForce,
                StunDuration = stunDuration
            };
        }

        public static float ApplyArmor(float damage, float armor)
        {
            // Diminishing returns armor formula, capped at 80% reduction
            float reduction = Mathf.Min(0.8f, armor / (armor + 100f));
            return Mathf.Max(1f, damage * (1f - reduction));
        }

        public static float ApplyResistance(float damage, DamageType type, StatBlock targetStats)
        {
            if (type == DamageType.Physical) return damage;

            // Map elemental damage types to their resistance stat
            // Uses Intelligence as a general magic resistance for now
            float resistance = targetStats.GetStat(StatType.Intelligence) * 0.5f;
            float reduction = Mathf.Min(0.75f, resistance / (resistance + 100f));
            return Mathf.Max(1f, damage * (1f - reduction));
        }
    }
}
