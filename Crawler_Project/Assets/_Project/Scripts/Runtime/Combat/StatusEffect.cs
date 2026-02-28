using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class StatusEffect
    {
        public StatusEffectData Data;
        public float RemainingDuration;
        public float TickTimer;
        public GameObject Source;
        public GameObject Target;

        public bool IsExpired => RemainingDuration <= 0f;

        public StatusEffect(StatusEffectData data, GameObject source, GameObject target)
        {
            Data = data;
            Source = source;
            Target = target;
            RemainingDuration = data.Duration;
            TickTimer = data.TickInterval;
        }

        public void Tick(float deltaTime)
        {
            RemainingDuration -= deltaTime;

            if (Data.TickInterval > 0)
            {
                TickTimer -= deltaTime;
                if (TickTimer <= 0)
                {
                    TickTimer = Data.TickInterval;
                    ApplyTick();
                }
            }
        }

        private void ApplyTick()
        {
            if (Target == null) return;

            var damageable = Target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            if (Data.TickDamage > 0)
            {
                var tickDamage = new DamageInfo
                {
                    RawDamage = Data.TickDamage,
                    FinalDamage = Data.TickDamage,
                    IsCritical = false,
                    DamageType = Data.DamageType,
                    Attacker = Source,
                    Target = Target,
                    HitPoint = Target.transform.position
                };

                damageable.TakeDamage(tickDamage);
            }
        }

        public void OnApply(StatBlock targetStats)
        {
            if (Data.StatModifications == null) return;

            foreach (var mod in Data.StatModifications)
            {
                mod.Source = this;
                targetStats.AddModifier(mod);
            }
        }

        public void OnRemove(StatBlock targetStats)
        {
            targetStats.RemoveModifiersFromSource(this);
        }
    }
}
