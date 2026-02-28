using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Node component that manages active status effects on an entity.
    /// </summary>
    public partial class StatusEffectManager : Node
    {
        private readonly List<StatusEffect> _activeEffects = new();
        private StatBlock _stats;
        private HealthComponent _health;

        public IReadOnlyList<StatusEffect> ActiveEffects => _activeEffects;

        public void Initialize(StatBlock stats, HealthComponent health)
        {
            _stats = stats;
            _health = health;
        }

        public void ApplyEffect(StatusEffectData data)
        {
            if (_stats == null) return;

            // Check if this effect is already active — refresh duration
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Data.Id == data.Id)
                {
                    _activeEffects[i].RemoveFrom(_stats);
                    _activeEffects.RemoveAt(i);
                    break;
                }
            }

            var effect = new StatusEffect(data);
            effect.ApplyTo(_stats);
            _activeEffects.Add(effect);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                float tickDamage = effect.Tick(dt);

                // Apply tick damage
                if (tickDamage > 0 && _health != null && _health.IsAlive)
                {
                    var info = new DamageInfo
                    {
                        RawDamage = tickDamage,
                        FinalDamage = tickDamage,
                        DamageType = effect.Data.TickDamageType,
                    };
                    _health.TakeDamage(info);
                }

                // Remove expired effects
                if (effect.IsExpired)
                {
                    effect.RemoveFrom(_stats);
                    _activeEffects.RemoveAt(i);
                }
            }
        }

        public void ClearAllEffects()
        {
            foreach (var effect in _activeEffects)
            {
                effect.RemoveFrom(_stats);
            }
            _activeEffects.Clear();
        }
    }
}
