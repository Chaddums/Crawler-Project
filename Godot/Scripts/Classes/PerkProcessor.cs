using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Applies gameplay-changing perk effects to combat events.
    /// Centralized logic so perks don't scatter across every system.
    /// Attached as a child node of PlayerController.
    /// </summary>
    public partial class PerkProcessor : Node
    {
        private PlayerController _player;
        private PlayerClassController _classCtrl;
        private PlayerStats _stats;
        private HealthComponent _health;

        // Emergency Repairs cooldown
        private float _emergencyRepairsCooldown;
        private const float EMERGENCY_REPAIRS_CD = 60f;

        // Momentum tracking
        private float _momentumTimer;
        private const float MOMENTUM_MAX_BONUS = 0.30f; // +30% max
        private const float MOMENTUM_RAMP_TIME = 4f;    // seconds to reach max

        // Overclocked self-damage
        private float _overclockedTickTimer;
        private const float OVERCLOCKED_TICK_RATE = 1f;
        private const float OVERCLOCKED_DPS = 5f;

        public float MomentumBonus { get; private set; }

        public override void _Ready()
        {
            _player = GetParent<PlayerController>();
            _classCtrl = _player.GetNode<PlayerClassController>("PlayerClassController");
            _stats = _player.GetNode<PlayerStats>("PlayerStats");
            _health = _player.GetNode<HealthComponent>("HealthComponent");

            _health.OnDamaged += OnPlayerDamaged;
        }

        public override void _ExitTree()
        {
            if (_health != null)
                _health.OnDamaged -= OnPlayerDamaged;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (_emergencyRepairsCooldown > 0f)
                _emergencyRepairsCooldown -= dt;

            // Momentum perk: build bonus while moving
            if (HasPerk(Perks.Momentum))
            {
                if (_player.Velocity.LengthSquared() > 0.5f)
                    _momentumTimer = Mathf.Min(_momentumTimer + dt, MOMENTUM_RAMP_TIME);
                else
                    _momentumTimer = Mathf.Max(_momentumTimer - dt * 2f, 0f); // decays faster

                MomentumBonus = (_momentumTimer / MOMENTUM_RAMP_TIME) * MOMENTUM_MAX_BONUS;
            }

            // Overclocked perk: self-damage tick
            if (HasPerk(Perks.Overclocked))
            {
                _overclockedTickTimer -= dt;
                if (_overclockedTickTimer <= 0f)
                {
                    _overclockedTickTimer = OVERCLOCKED_TICK_RATE;
                    if (_health.IsAlive && _health.CurrentHealth > OVERCLOCKED_DPS + 1f)
                    {
                        var selfDamage = new DamageInfo
                        {
                            RawDamage = OVERCLOCKED_DPS,
                            FinalDamage = OVERCLOCKED_DPS,
                            DamageType = DamageType.Physical,
                            Attacker = _player,
                            Target = _player
                        };
                        _health.TakeDamage(selfDamage);
                    }
                }
            }
        }

        // =================================================================
        // OUTGOING DAMAGE MODIFIERS — called by PlayerCombat before dealing
        // =================================================================

        /// <summary>
        /// Modify outgoing damage based on active perks.
        /// Call this after DamageCalculator but before TakeDamage.
        /// </summary>
        public float ModifyOutgoingDamage(float damage, bool isAbility)
        {
            float mult = 1f;

            // Glass Cannon: +50% damage dealt
            if (HasPerk(Perks.GlassCannon))
                mult += 0.50f;

            // Berserker Protocol: +2% damage per 1% HP missing
            if (HasPerk(Perks.BerserkerProtocol))
            {
                float missingPct = 1f - _health.HealthPercent;
                mult += missingPct * 2f; // At 50% HP: +100% damage. At 10% HP: +180%.
            }

            // Momentum: up to +30% while moving
            if (HasPerk(Perks.Momentum))
                mult += MomentumBonus;

            // Overcharge: abilities deal +40% damage
            if (isAbility && HasPerk(Perks.Overcharge))
                mult += 0.40f;

            // Powered Strike: basic attacks +50% if mana was consumed (handled in PlayerCombat)
            // Entropy Field: +60% total damage (converts to DoT, handled elsewhere)
            if (HasPerk(Perks.EntropyField))
                mult += 0.60f;

            // Exploit Weakness: +15% to debuffed targets (would need target check — approximated here)
            // This is better checked at hit time in PlayerCombat

            return damage * mult;
        }

        /// <summary>
        /// Get the mana cost multiplier for abilities.
        /// </summary>
        public float GetAbilityManaCostMultiplier()
        {
            float mult = 1f;

            // Overcharge: abilities cost 30% more mana
            if (HasPerk(Perks.Overcharge))
                mult += 0.30f;

            // War Machine pinnacle: abilities cost 25% less
            if (HasPerk(Perks.WarMachine))
                mult -= 0.25f;

            return Mathf.Max(0.1f, mult);
        }

        /// <summary>
        /// Check if basic attack should consume mana for bonus damage (Powered Strike).
        /// Returns the bonus multiplier (1.0 = no bonus).
        /// </summary>
        public float TryPoweredStrike()
        {
            if (!HasPerk(Perks.PoweredStrike)) return 1f;
            if (_stats.CurrentMana < 10f) return 1f;

            _stats.SpendMana(10f);
            return 1.5f; // +50% damage
        }

        // =================================================================
        // INCOMING DAMAGE MODIFIERS — called from OnPlayerDamaged
        // =================================================================

        private void OnPlayerDamaged(DamageInfo damage)
        {
            // Thorns Protocol: reflect 15% back to attacker
            if (HasPerk(Perks.ThornsProtocol) && damage.Attacker is Node3D attackerNode)
            {
                var attackerHealth = FindDamageable(attackerNode);
                if (attackerHealth != null && attackerHealth.IsAlive)
                {
                    float thornsDmg = damage.FinalDamage * 0.15f;
                    var thornsInfo = new DamageInfo
                    {
                        RawDamage = thornsDmg,
                        FinalDamage = thornsDmg,
                        DamageType = DamageType.Physical,
                        Attacker = _player,
                        Target = attackerNode
                    };
                    attackerHealth.TakeDamage(thornsInfo);
                }
            }

            // Emergency Repairs: heal 20% when below 25% HP
            if (HasPerk(Perks.EmergencyRepairs) && _emergencyRepairsCooldown <= 0f)
            {
                if (_health.IsAlive && _health.HealthPercent < 0.25f)
                {
                    float healAmount = _health.MaxHealth * 0.20f;
                    _health.Heal(healAmount);
                    _emergencyRepairsCooldown = EMERGENCY_REPAIRS_CD;
                    GD.Print($"[PerkProcessor] Emergency Repairs triggered! Healed {healAmount:F0} HP");
                }
            }
        }

        /// <summary>
        /// Modify incoming damage before it's applied to health.
        /// Called by the damage pipeline.
        /// </summary>
        public float ModifyIncomingDamage(float damage)
        {
            float mult = 1f;

            // Glass Cannon: +30% damage taken
            if (HasPerk(Perks.GlassCannon))
                mult += 0.30f;

            return damage * mult;
        }

        /// <summary>
        /// Mana Shield: should damage go to mana instead of HP?
        /// Returns true if mana absorbed the damage.
        /// </summary>
        public bool TryManaShieldAbsorb(float damage)
        {
            if (!HasPerk(Perks.ManaShield)) return false;
            if (_stats.CurrentMana <= 0f) return false;

            // Absorb damage from mana. If mana runs out, remaining goes to HP.
            if (_stats.CurrentMana >= damage)
            {
                _stats.SpendMana(damage);
                return true; // Fully absorbed
            }

            // Partial absorption
            float remaining = damage - _stats.CurrentMana;
            _stats.SpendMana(_stats.CurrentMana);
            // Let the remaining damage go through to HP
            return false;
        }

        /// <summary>
        /// Modify healing received.
        /// </summary>
        public float ModifyHealing(float healAmount)
        {
            // Iron Fortress: +50% healing received
            if (HasPerk(Perks.IronFortress))
                healAmount *= 1.5f;

            return healAmount;
        }

        /// <summary>
        /// Check if dash is allowed (Iron Fortress disables it).
        /// </summary>
        public bool CanDash()
        {
            if (HasPerk(Perks.IronFortress)) return false;
            return true;
        }

        /// <summary>
        /// Get bonus dash charges from perks.
        /// </summary>
        public int GetBonusDashCharges()
        {
            if (HasPerk(Perks.AssaultFrame)) return 2;
            return 0;
        }

        private bool HasPerk(string perkId) => _classCtrl?.HasPerk(perkId) ?? false;

        private static IDamageable FindDamageable(Node node)
        {
            Node current = node;
            while (current != null)
            {
                if (current is IDamageable d) return d;
                var health = current.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health != null) return health;
                current = current.GetParent();
            }
            return null;
        }
    }
}
