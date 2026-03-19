using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Buff/debuff effect type.
    /// </summary>
    public enum BuffType
    {
        Speed,
        Damage,
        Armor,
        AttackRate,
        Regen,
        Slow
    }

    /// <summary>
    /// A single active buff or debuff effect.
    /// </summary>
    public struct BuffEffect
    {
        public string Id;
        public BuffType Type;
        public float Value;
        public float Duration;      // Total duration (<=0 means permanent)
        public float Remaining;      // Time remaining
        public Node Source;          // Who applied this effect
        public bool IsBuff;          // true = buff, false = debuff
    }

    /// <summary>
    /// BuffDebuffComponent - Manages buff/debuff stacking with duration + source tracking.
    /// Ported from HoldtheLine. Auto-disables processing when no effects are active.
    ///
    /// Attach to any Node3D. Call ApplyBuff/ApplyDebuff from external systems.
    /// The component modifies the host via the IBuffable interface or direct property access.
    /// </summary>
    public partial class BuffDebuffComponent : Node
    {
        /// <summary>Fired when a buff/debuff is added or removed.</summary>
        public event Action<string> BuffAdded;
        public event Action<string> BuffRemoved;

        private readonly Dictionary<string, BuffEffect> _activeEffects = new();
        private readonly List<string> _expiredKeys = new();

        // Cached references to the host node for applying effects
        private VineEnemy _enemyHost;
        private VineNode _nodeHost;

        public override void _Ready()
        {
            // Determine host type
            var parent = GetParent();
            _enemyHost = parent as VineEnemy;
            _nodeHost = parent as VineNode;

            // Start disabled — re-enable when effects are applied
            SetProcess(false);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _expiredKeys.Clear();

            foreach (var kvp in _activeEffects)
            {
                var effect = kvp.Value;
                if (effect.Duration > 0f) // <=0 means permanent
                {
                    effect.Remaining -= dt;
                    if (effect.Remaining <= 0f)
                    {
                        _expiredKeys.Add(kvp.Key);
                        continue;
                    }
                    _activeEffects[kvp.Key] = effect;
                }
            }

            // Remove expired effects
            foreach (var key in _expiredKeys)
            {
                var effect = _activeEffects[key];
                UnapplyEffect(effect);
                _activeEffects.Remove(key);
                BuffRemoved?.Invoke(key);
            }

            // Auto-disable when all effects have expired
            if (_activeEffects.Count == 0)
                SetProcess(false);
        }

        /// <summary>
        /// Apply a buff (positive effect) to the host.
        /// If a buff with the same ID already exists, it is refreshed (duration reset, value updated).
        /// </summary>
        public void ApplyBuff(string buffId, BuffType type, float value, float duration, Node source = null)
        {
            // If already active, unapply old value first
            if (_activeEffects.TryGetValue(buffId, out var existing))
                UnapplyEffect(existing);

            var effect = new BuffEffect
            {
                Id = buffId,
                Type = type,
                Value = value,
                Duration = duration,
                Remaining = duration,
                Source = source,
                IsBuff = true
            };

            _activeEffects[buffId] = effect;
            ApplyEffect(effect);
            SetProcess(true);
            BuffAdded?.Invoke(buffId);
        }

        /// <summary>
        /// Apply a debuff (negative effect) to the host.
        /// </summary>
        public void ApplyDebuff(string debuffId, BuffType type, float value, float duration, Node source = null)
        {
            if (_activeEffects.TryGetValue(debuffId, out var existing))
                UnapplyEffect(existing);

            var effect = new BuffEffect
            {
                Id = debuffId,
                Type = type,
                Value = value,
                Duration = duration,
                Remaining = duration,
                Source = source,
                IsBuff = false
            };

            _activeEffects[debuffId] = effect;
            ApplyEffect(effect);
            SetProcess(true);
        }

        /// <summary>
        /// Remove a specific buff/debuff by ID.
        /// </summary>
        public void RemoveEffect(string effectId)
        {
            if (_activeEffects.TryGetValue(effectId, out var effect))
            {
                UnapplyEffect(effect);
                _activeEffects.Remove(effectId);
                BuffRemoved?.Invoke(effectId);
            }
        }

        public bool HasEffect(string effectId)
        {
            return _activeEffects.ContainsKey(effectId);
        }

        public bool HasEffectOfType(BuffType type)
        {
            foreach (var effect in _activeEffects.Values)
            {
                if (effect.Type == type) return true;
            }
            return false;
        }

        /// <summary>
        /// Get the total modifier value for a given buff type (sum of all active effects).
        /// </summary>
        public float GetTotalModifier(BuffType type)
        {
            float total = 0f;
            foreach (var effect in _activeEffects.Values)
            {
                if (effect.Type != type) continue;
                total += effect.IsBuff ? effect.Value : -effect.Value;
            }
            return total;
        }

        /// <summary>
        /// Remove all active effects.
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _activeEffects)
                UnapplyEffect(kvp.Value);
            _activeEffects.Clear();
            SetProcess(false);
        }

        public int ActiveCount => _activeEffects.Count;

        // ── Effect application ──

        private void ApplyEffect(BuffEffect effect)
        {
            float sign = effect.IsBuff ? 1f : -1f;
            float value = effect.Value * sign;

            // Apply to VineEnemy host
            if (_enemyHost != null)
            {
                switch (effect.Type)
                {
                    case BuffType.Speed:
                        _enemyHost.SpeedMultiplier += value;
                        break;
                    case BuffType.Slow:
                        // Slow is always negative regardless of buff/debuff flag
                        _enemyHost.SpeedMultiplier = Mathf.Max(0.1f, _enemyHost.SpeedMultiplier - effect.Value);
                        break;
                    case BuffType.Damage:
                        _enemyHost.DamageMultiplier += value;
                        break;
                    case BuffType.Armor:
                        _enemyHost.ArmorBonus += value;
                        break;
                }
            }

            // Apply to VineNode host
            if (_nodeHost != null)
            {
                switch (effect.Type)
                {
                    case BuffType.Damage:
                        _nodeHost.ReceiveBuff(effect.Value);
                        break;
                    case BuffType.AttackRate:
                        _nodeHost.AttackRateMultiplier += value;
                        break;
                }
            }
        }

        private void UnapplyEffect(BuffEffect effect)
        {
            float sign = effect.IsBuff ? 1f : -1f;
            float value = effect.Value * sign;

            if (_enemyHost != null)
            {
                switch (effect.Type)
                {
                    case BuffType.Speed:
                        _enemyHost.SpeedMultiplier -= value;
                        break;
                    case BuffType.Slow:
                        _enemyHost.SpeedMultiplier = Mathf.Min(2f, _enemyHost.SpeedMultiplier + effect.Value);
                        break;
                    case BuffType.Damage:
                        _enemyHost.DamageMultiplier -= value;
                        break;
                    case BuffType.Armor:
                        _enemyHost.ArmorBonus -= value;
                        break;
                }
            }

            if (_nodeHost != null)
            {
                switch (effect.Type)
                {
                    case BuffType.AttackRate:
                        _nodeHost.AttackRateMultiplier -= value;
                        break;
                }
            }
        }
    }
}
