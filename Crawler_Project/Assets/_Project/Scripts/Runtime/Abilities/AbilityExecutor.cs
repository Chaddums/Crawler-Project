using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class AbilityExecutor : MonoBehaviour
    {
        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private static List<ScriptableObject> ToScriptableObjectList(List<StatusEffectData> effects)
        {
            if (effects == null || effects.Count == 0) return null;
            var list = new List<ScriptableObject>(effects.Count);
            for (int i = 0; i < effects.Count; i++)
                list.Add(effects[i]);
            return list;
        }

        public bool Execute(AbilityData ability, IAttacker caster, Vector3 targetPoint, IDamageable targetEntity)
        {
            if (ability == null || caster == null) return false;

            switch (ability.Type)
            {
                case AbilityType.Melee:
                    return ExecuteMelee(ability, caster, targetPoint);
                case AbilityType.Projectile:
                    return ExecuteProjectile(ability, caster, targetPoint);
                case AbilityType.AoE:
                    return ExecuteAoE(ability, caster, targetPoint);
                case AbilityType.Buff:
                    return ExecuteBuff(ability, caster);
                case AbilityType.Movement:
                    return ExecuteMovement(ability, caster, targetPoint);
                default:
                    return false;
            }
        }

        private bool ExecuteMelee(AbilityData ability, IAttacker caster, Vector3 targetPoint)
        {
            if (ability.EffectPrefab == null) return false;

            Vector3 direction = (targetPoint - caster.Transform.position).Flat().normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = caster.Transform.forward;

            Quaternion rotation = Quaternion.LookRotation(direction);
            var meleeObj = Object.Instantiate(
                ability.EffectPrefab,
                caster.Transform.position + direction * 0.5f + Vector3.up,
                rotation,
                caster.Transform
            );

            var hitBox = meleeObj.GetComponent<HitBox>();
            if (hitBox != null)
            {
                hitBox.Owner = caster;
                hitBox.SetTeam(caster.Team);
                hitBox.DamagePayload = DamageCalculator.CalculateAbilityDamage(
                    caster, ability.BaseDamage, ability.DamageType,
                    ability.ScalingStat, ability.ScalingRatio,
                    ToScriptableObjectList(ability.AppliedStatusEffects),
                    ability.KnockbackForce, ability.StunDuration);
            }

            Object.Destroy(meleeObj, 0.3f);

            if (ability.CastSound != null)
                AudioSource.PlayClipAtPoint(ability.CastSound, caster.Transform.position);

            return true;
        }

        private bool ExecuteProjectile(AbilityData ability, IAttacker caster, Vector3 targetPoint)
        {
            if (ability.EffectPrefab == null) return false;

            Vector3 spawnPos = caster.Transform.position + Vector3.up;
            Vector3 direction = (targetPoint - spawnPos).Flat().normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = caster.Transform.forward;

            Quaternion rotation = Quaternion.LookRotation(direction);
            var projObj = Object.Instantiate(ability.EffectPrefab, spawnPos, rotation);

            var projectile = projObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                var damageInfo = DamageCalculator.CalculateAbilityDamage(
                    caster, ability.BaseDamage, ability.DamageType,
                    ability.ScalingStat, ability.ScalingRatio,
                    ToScriptableObjectList(ability.AppliedStatusEffects),
                    ability.KnockbackForce, ability.StunDuration);
                projectile.Initialize(damageInfo, caster.Team);
            }

            if (ability.CastSound != null)
                AudioSource.PlayClipAtPoint(ability.CastSound, caster.Transform.position);

            return true;
        }

        private bool ExecuteAoE(AbilityData ability, IAttacker caster, Vector3 targetPoint)
        {
            if (ability.EffectPrefab == null) return false;

            float distance = Vector3.Distance(caster.Transform.position, targetPoint);
            if (distance > ability.Range)
            {
                Vector3 dir = (targetPoint - caster.Transform.position).normalized;
                targetPoint = caster.Transform.position + dir * ability.Range;
            }

            var aoeObj = Object.Instantiate(ability.EffectPrefab, targetPoint, Quaternion.identity);

            var hitBox = aoeObj.GetComponent<HitBox>();
            if (hitBox != null)
            {
                hitBox.Owner = caster;
                hitBox.SetTeam(caster.Team);
                hitBox.DamagePayload = DamageCalculator.CalculateAbilityDamage(
                    caster, ability.BaseDamage, ability.DamageType,
                    ability.ScalingStat, ability.ScalingRatio,
                    ToScriptableObjectList(ability.AppliedStatusEffects),
                    ability.KnockbackForce, ability.StunDuration);
            }

            float lifetime = ability.Duration > 0 ? ability.Duration : 1f;
            Object.Destroy(aoeObj, lifetime);

            if (ability.CastSound != null)
                AudioSource.PlayClipAtPoint(ability.CastSound, targetPoint);

            return true;
        }

        private bool ExecuteBuff(AbilityData ability, IAttacker caster)
        {
            if (ability.AppliedStatusEffects == null) return false;

            var statusManager = caster.Transform.GetComponent<StatusEffectManager>();
            if (statusManager == null) return false;

            foreach (var effect in ability.AppliedStatusEffects)
            {
                statusManager.ApplyEffect(effect, caster.Transform.gameObject);
            }

            if (ability.EffectPrefab != null)
            {
                var vfx = Object.Instantiate(ability.EffectPrefab, caster.Transform.position, Quaternion.identity, caster.Transform);
                float lifetime = ability.Duration > 0 ? ability.Duration : 2f;
                Object.Destroy(vfx, lifetime);
            }

            if (ability.CastSound != null)
                AudioSource.PlayClipAtPoint(ability.CastSound, caster.Transform.position);

            return true;
        }

        private bool ExecuteMovement(AbilityData ability, IAttacker caster, Vector3 targetPoint)
        {
            // Dash/teleport to target point within range
            Vector3 direction = (targetPoint - caster.Transform.position).Flat().normalized;
            float distance = Mathf.Min(
                Vector3.Distance(caster.Transform.position, targetPoint),
                ability.Range
            );

            Vector3 destination = caster.Transform.position + direction * distance;

            // Use NavMeshAgent if available
            var agent = caster.Transform.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.Warp(destination);
            }
            else
            {
                caster.Transform.position = destination;
            }

            if (ability.EffectPrefab != null)
            {
                var vfx = Object.Instantiate(ability.EffectPrefab, destination, Quaternion.identity);
                Object.Destroy(vfx, 1f);
            }

            if (ability.CastSound != null)
                AudioSource.PlayClipAtPoint(ability.CastSound, caster.Transform.position);

            return true;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AbilityExecutor>();
        }
    }
}
