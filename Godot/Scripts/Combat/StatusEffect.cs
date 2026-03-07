using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Runtime instance of a status effect applied to an entity.
    /// </summary>
    public class StatusEffect
    {
        public StatusEffectData Data { get; private set; }
        public float RemainingDuration { get; private set; }
        public float TickTimer { get; private set; }
        public bool IsExpired => RemainingDuration <= 0;

        private readonly List<StatModifier> _appliedMods = new();
        private StatBlock _targetStats;

        public StatusEffect(StatusEffectData data)
        {
            Data = data;
            RemainingDuration = data.Duration;
            TickTimer = data.TickInterval;
        }

        /// <summary>
        /// Apply stat modifications to the target.
        /// </summary>
        public void ApplyTo(StatBlock stats)
        {
            _targetStats = stats;
            foreach (var modData in Data.StatModifications)
            {
                var mod = new StatModifier(modData.StatType, modData.ModType, modData.Value, this);
                stats.AddModifier(mod);
                _appliedMods.Add(mod);
            }
        }

        /// <summary>
        /// Remove stat modifications from the target.
        /// </summary>
        public void RemoveFrom(StatBlock stats)
        {
            stats.RemoveModifiersFromSource(this);
            _appliedMods.Clear();
        }

        /// <summary>
        /// Tick the effect. Returns damage dealt this frame (if any).
        /// </summary>
        public float Tick(float delta)
        {
            RemainingDuration -= delta;
            float damageThisTick = 0f;

            if (Data.TickDamage > 0 && Data.TickInterval > 0)
            {
                TickTimer -= delta;
                if (TickTimer <= 0)
                {
                    damageThisTick = Data.TickDamage;
                    TickTimer += Data.TickInterval;
                }
            }

            return damageThisTick;
        }
    }
}
