using System;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Reusable health component. Attach as a child node to any entity that has HP.
    /// </summary>
    public partial class HealthComponent : Node, IDamageable
    {
        [Export] private float _maxHealth = 100f;
        [Export] private Team _team = Team.Player;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _maxHealth;
        public bool IsAlive => CurrentHealth > 0;
        public Node3D Node => GetParent<Node3D>();
        public Team Team => _team;
        public float HealthPercent => _maxHealth > 0 ? CurrentHealth / _maxHealth : 0f;

        public event Action OnDeath;
        public event Action<DamageInfo> OnDamaged;
        public event Action<float> OnHealed;
        public event Action<float, float> OnHealthChanged;

        public override void _Ready()
        {
            CurrentHealth = _maxHealth;
        }

        public void TakeDamage(DamageInfo damage)
        {
            if (!IsAlive) return;

            // Debug menu cheats
            if (_team == Team.Player && DebugMenu.GodMode)
            {
                OnDamaged?.Invoke(damage);
                return; // Invincible — take no damage
            }
            if (_team == Team.Enemy)
            {
                damage.FinalDamage *= DebugMenu.DamageMultiplier;
                if (DebugMenu.InstantKill)
                    damage.FinalDamage = _maxHealth + 1f;
            }

            // Player perk processing for incoming damage
            if (_team == Team.Player)
            {
                var player = GetParent<PlayerController>();
                var perk = player?.PerkProcessor;
                if (perk != null)
                {
                    damage.FinalDamage = perk.ModifyIncomingDamage(damage.FinalDamage);

                    // Mana Shield: absorb damage with mana
                    if (perk.TryManaShieldAbsorb(damage.FinalDamage))
                    {
                        // Fully absorbed — still fire events for VFX
                        OnDamaged?.Invoke(damage);
                        return;
                    }
                }
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage.FinalDamage);
            OnDamaged?.Invoke(damage);
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
            GameEvents.OnDamageDealt?.Invoke(damage);

            if (!IsAlive)
            {
                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;

            // Iron Fortress perk: +50% healing
            if (_team == Team.Player)
            {
                var perk = GetParent<PlayerController>()?.PerkProcessor;
                if (perk != null) amount = perk.ModifyHealing(amount);
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(_maxHealth, CurrentHealth + amount);
            float actualHeal = CurrentHealth - previousHealth;

            if (actualHeal > 0)
            {
                OnHealed?.Invoke(actualHeal);
                OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
            }
        }

        public void SetMaxHealth(float max, bool healToFull = false)
        {
            _maxHealth = max;
            if (healToFull)
                CurrentHealth = _maxHealth;
            else
                CurrentHealth = Mathf.Min(CurrentHealth, _maxHealth);

            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        public void SetCurrentHealth(float health)
        {
            CurrentHealth = Mathf.Clamp(health, 0, _maxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        public void SetTeam(Team team)
        {
            _team = team;
        }

        public void Revive(float healthPercent = 1f)
        {
            CurrentHealth = _maxHealth * Mathf.Clamp(healthPercent, 0f, 1f);
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }
    }
}
