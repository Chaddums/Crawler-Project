using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(CompanionController))]
    public class CompanionAbilities : MonoBehaviour
    {
        private CompanionController _controller;
        private readonly List<AbilitySlot> _abilitySlots = new();

        public IReadOnlyList<AbilitySlot> AbilitySlots => _abilitySlots;

        private void Awake()
        {
            _controller = GetComponent<CompanionController>();
        }

        private void Update()
        {
            TickCooldowns();
        }

        /// <summary>
        /// Populates ability slots from the companion data's starting abilities.
        /// </summary>
        public void InitializeAbilities(List<AbilityData> abilities)
        {
            _abilitySlots.Clear();

            if (abilities == null) return;

            foreach (var ability in abilities)
            {
                var slot = new AbilitySlot();
                slot.Ability = ability;
                _abilitySlots.Add(slot);
            }
        }

        /// <summary>
        /// Ticks all ability slot cooldowns each frame, applying cooldown reduction from stats.
        /// </summary>
        private void TickCooldowns()
        {
            float cdr = _controller.Stats.GetStat(StatType.CooldownReduction);

            foreach (var slot in _abilitySlots)
            {
                slot.Tick(Time.deltaTime, cdr);
            }
        }

        /// <summary>
        /// Returns true if any equipped ability is off cooldown and ready to use.
        /// </summary>
        public bool HasReadyAbility()
        {
            foreach (var slot in _abilitySlots)
            {
                if (slot.Ability != null && slot.IsReady)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to pick and use the best available ability for the current situation.
        /// Evaluates abilities by priority: highest-damage ready ability that fits the context.
        /// Returns true if an ability was used.
        /// </summary>
        public bool TryUseAbility(Transform target)
        {
            if (target == null) return false;

            AbilitySlot bestSlot = null;
            float bestPriority = float.MinValue;

            foreach (var slot in _abilitySlots)
            {
                if (slot.Ability == null || !slot.IsReady) continue;

                float priority = EvaluateAbilityPriority(slot.Ability, target);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestSlot = slot;
                }
            }

            if (bestSlot == null) return false;

            // Execute the ability
            var executor = ServiceLocator.Get<AbilityExecutor>();
            if (executor != null)
            {
                var abilityUser = GetComponent<IAbilityUser>();
                var targetDamageable = target.GetComponent<IDamageable>();
                if (executor.Execute(bestSlot.Ability, _controller.Combat, target.position, targetDamageable))
                {
                    bestSlot.Use();
                    GameEvents.OnCompanionAbilityUsed?.Invoke(bestSlot.Ability as ScriptableObject);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Evaluates how suitable an ability is for the current target.
        /// Higher value = higher priority for selection.
        /// </summary>
        private float EvaluateAbilityPriority(AbilityData ability, Transform target)
        {
            float priority = ability.BaseDamage;
            float distToTarget = Vector3.Distance(transform.position, target.position);

            // Prefer melee abilities when close, ranged when far
            switch (ability.Type)
            {
                case AbilityType.Melee:
                    if (distToTarget <= _controller.AI.AttackRange)
                        priority += 20f;
                    else
                        priority -= 10f;
                    break;

                case AbilityType.Projectile:
                    if (distToTarget > _controller.AI.AttackRange)
                        priority += 15f;
                    break;

                case AbilityType.AoE:
                    // AoE is always useful, slight bonus
                    priority += 10f;
                    break;

                case AbilityType.Buff:
                    // Buff priority is lower in combat, handled separately
                    priority -= 5f;
                    break;
            }

            return priority;
        }

        public void AddAbility(AbilityData ability)
        {
            var slot = new AbilitySlot();
            slot.Ability = ability;
            _abilitySlots.Add(slot);
        }

        public void RemoveAbility(AbilityData ability)
        {
            _abilitySlots.RemoveAll(s => s.Ability == ability);
        }
    }
}
