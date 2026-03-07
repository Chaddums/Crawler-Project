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
        private const float MOMENTUM_MAX_BONUS = 0.30f;
        private const float MOMENTUM_RAMP_TIME = 4f;

        // Overclocked self-damage
        private float _overclockedTickTimer;
        private const float OVERCLOCKED_TICK_RATE = 1f;
        private const float OVERCLOCKED_DPS = 5f;

        // Adrenaline Rush buff
        private float _adrenalineTimer;
        private const float ADRENALINE_DURATION = 3f;
        private const float ADRENALINE_SPEED_BONUS = 0.15f;
        private const float ADRENALINE_CRIT_BONUS = 0.05f;
        private StatModifier _adrenalineSpeedMod;
        private StatModifier _adrenaliaCritMod;

        // Reactive Plating stacks
        private int _reactivePlatingStacks;
        private float _reactivePlatingTimer;
        private const float REACTIVE_PLATING_DURATION = 3f;
        private const float REACTIVE_PLATING_ARMOR = 5f;
        private const int REACTIVE_PLATING_MAX_STACKS = 3;
        private StatModifier _reactivePlatingMod;

        // Corrosive Aura tick
        private float _corrosiveAuraTickTimer;
        private const float CORROSIVE_AURA_TICK_RATE = 0.5f;
        private const float CORROSIVE_AURA_RADIUS = 5f;

        // Adaptive Plating tick
        private float _adaptivePlatingTickTimer;
        private const float ADAPTIVE_PLATING_TICK_RATE = 0.5f;
        private const float ADAPTIVE_PLATING_RADIUS = 8f;
        private const float ADAPTIVE_PLATING_DR_PER_ENEMY = 0.03f;
        private const float ADAPTIVE_PLATING_MAX_DR = 0.15f;
        private StatModifier _adaptivePlatingMod;

        // Feedback Loop heal tick
        private float _feedbackLoopTickTimer;
        private const float FEEDBACK_LOOP_TICK_RATE = 1f;
        private const float FEEDBACK_LOOP_HEAL_PCT = 0.02f;

        // Arc Reactor aura
        private float _arcReactorTickTimer;
        private const float ARC_REACTOR_TICK_RATE = 0.5f;
        private const float ARC_REACTOR_RADIUS = 4f;
        private const float ARC_REACTOR_DPS = 8f;

        // Siege Plating ally aura
        private float _siegePlatingTickTimer;
        private const float SIEGE_PLATING_TICK_RATE = 0.5f;
        private const float SIEGE_PLATING_RADIUS = 6f;

        // Pinnacle scale applied flag
        private bool _pinnacleScaleApplied;

        // Pinnacle aura VFX
        private GpuParticles3D _pinnacleAuraVfx;

        // Singularity Core cooldown
        private float _singularityCooldown;
        private const float SINGULARITY_CD = 5f;
        private const float SINGULARITY_RADIUS = 8f;

        // Prismatic Core element rotation
        private static readonly DamageType[] PrismaticElements =
        {
            DamageType.Fire, DamageType.Ice, DamageType.Lightning,
            DamageType.Poison, DamageType.Dark
        };

        // --- Mythic: Immortal Engine ---
        private float _immortalEngineRegenTimer;
        private float _immortalEngineInvulnTimer;
        private const float IMMORTAL_REGEN_TICK = 0.25f;
        private const float IMMORTAL_REGEN_PCT = 0.05f; // 5% max HP per second
        private const float IMMORTAL_INVULN_DURATION = 2f;

        // --- Mythic: Devourer ---
        private int _devourerKillStacks;

        // --- Mythic: Time Loop (Paradox Gland) ---
        private bool _timeLoopUsedThisFloor;
        private Vector3 _timeLoopPosition;
        private float _timeLoopHealth;
        private float _timeLoopMana;
        private float _timeLoopSnapshotTimer;
        private const float TIME_LOOP_SNAPSHOT_INTERVAL = 0.5f;

        // --- Mythic: Void Heart ---
        private float _voidHeartCooldown;
        private const float VOID_HEART_CD = 0.5f;

        // --- Mythic: Storm Caller ---
        // (Chains from all damage — checked at hit time)

        // --- Mythic: Echo Chamber ---
        // (Double-cast checked at ability use time)

        // --- Mythic: Blood Economy (Hemorrhage Engine) ---
        // (HP-for-mana checked at ability use time)

        public float MomentumBonus { get; private set; }
        public bool HasAdrenalineRush => _adrenalineTimer > 0f;
        public int DevourerStacks => _devourerKillStacks;

        public override void _Ready()
        {
            _player = GetParent<PlayerController>();
            _classCtrl = _player.GetNode<PlayerClassController>("PlayerClassController");
            _stats = _player.GetNode<PlayerStats>("PlayerStats");
            _health = _player.GetNode<HealthComponent>("HealthComponent");

            _health.OnDamaged += OnPlayerDamaged;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnSectorEntered += OnSectorEntered;
        }

        public override void _ExitTree()
        {
            if (_health != null)
                _health.OnDamaged -= OnPlayerDamaged;
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnSectorEntered -= OnSectorEntered;

            // Clean up stat modifiers
            RemoveAdrenalineMods();
            RemoveReactivePlatingMod();
            RemoveAdaptivePlatingMod();

            // Clean up aura VFX
            if (_pinnacleAuraVfx != null && GodotObject.IsInstanceValid(_pinnacleAuraVfx))
                _pinnacleAuraVfx.QueueFree();
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
                    _momentumTimer = Mathf.Max(_momentumTimer - dt * 2f, 0f);

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

            // Adrenaline Rush: tick down buff timer
            if (_adrenalineTimer > 0f)
            {
                _adrenalineTimer -= dt;
                if (_adrenalineTimer <= 0f)
                    RemoveAdrenalineMods();
            }

            // Reactive Plating: tick down stack timer
            if (_reactivePlatingStacks > 0)
            {
                _reactivePlatingTimer -= dt;
                if (_reactivePlatingTimer <= 0f)
                {
                    _reactivePlatingStacks = 0;
                    RemoveReactivePlatingMod();
                }
            }

            // Corrosive Aura: apply armor debuff to nearby enemies
            if (HasPerk(Perks.CorrosiveAura))
                TickCorrosiveAura(dt);

            // Adaptive Plating: DR per nearby enemy
            if (HasPerk(Perks.AdaptivePlating))
                TickAdaptivePlating(dt);

            // Feedback Loop: heal while status effects are active on enemies
            if (HasPerk(Perks.FeedbackLoop))
                TickFeedbackLoop(dt);

            // Arc Reactor pinnacle: lightning aura
            if (HasPerk(Perks.ArcReactor))
                TickArcReactor(dt);

            // Siege Plating pinnacle: allies take 20% less damage near you
            if (HasPerk(Perks.SiegePlating))
                TickSiegePlating(dt);

            // Pinnacle size bonuses (apply once after body is built)
            if (!_pinnacleScaleApplied && _player.BodyRoot != null)
                ApplyPinnacleScale();

            // Singularity Core cooldown
            if (_singularityCooldown > 0f)
                _singularityCooldown -= dt;

            // Mythic: Immortal Engine — regen 5% max HP/s
            if (HasPerk(Perks.MythicImmortalEngine))
                TickImmortalEngine(dt);

            // Mythic: Time Loop — snapshot position every 0.5s
            if (HasPerk(Perks.MythicTimeLoop) && !_timeLoopUsedThisFloor)
                TickTimeLoopSnapshot(dt);

            // Mythic: Void Heart cooldown
            if (_voidHeartCooldown > 0f)
                _voidHeartCooldown -= dt;
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

            // War Machine pinnacle: +10% all damage
            if (HasPerk(Perks.WarMachine))
                mult += 0.10f;

            // Mythic: Devourer — +0.5% damage per kill stack
            if (HasPerk(Perks.MythicDevourer) && _devourerKillStacks > 0)
                mult += _devourerKillStacks * 0.005f;

            return damage * mult;
        }

        /// <summary>
        /// Get damage multiplier from Amplifier Core level bonus.
        /// Each +1 ability level = +25% damage.
        /// </summary>
        public float GetAmplifierBonus(string abilityId)
        {
            int bonus = GetAbilityLevelBonus(abilityId);
            return bonus > 0 ? 1f + bonus * 0.25f : 1f;
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

            // Mythic: Echo Chamber — double mana cost (abilities fire twice)
            if (HasPerk(Perks.MythicEchoChamber))
                mult += 1.0f;

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

            // Reactive Plating: gain +5 armor per stack (max 3) for 3s when hit
            ApplyReactivePlating();

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

            // Mythic: Immortal Engine — survive lethal damage
            if (HasPerk(Perks.MythicImmortalEngine) && _health.CurrentHealth <= 1f && _immortalEngineInvulnTimer <= 0f)
            {
                _immortalEngineInvulnTimer = IMMORTAL_INVULN_DURATION;
                _health.Heal(1f); // Keep alive
                GD.Print("[PerkProcessor] Immortal Engine: refusing to die!");
            }

            // Mythic: Time Loop — rewind on lethal damage
            if (HasPerk(Perks.MythicTimeLoop) && !_timeLoopUsedThisFloor && _health.CurrentHealth <= 1f)
            {
                TriggerTimeLoop();
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

        /// <summary>
        /// Exploit Weakness: +15% damage to targets with active debuffs.
        /// Called at hit time with the target node so we can check its StatusEffectManager.
        /// </summary>
        public float GetExploitWeaknessMultiplier(Node target)
        {
            if (!HasPerk(Perks.ExploitWeakness)) return 1f;

            var sem = FindStatusEffectManager(target);
            if (sem == null) return 1f;

            foreach (var effect in sem.ActiveEffects)
            {
                if (effect.Data.IsDebuff)
                    return 1.15f;
            }
            return 1f;
        }

        /// <summary>
        /// Impact Driver: 20% chance to stagger (stun) enemies on basic attacks.
        /// Returns stun duration to apply (0 if no proc).
        /// </summary>
        public float TryImpactDriver()
        {
            if (!HasPerk(Perks.ImpactDriver)) return 0f;
            return GD.Randf() < 0.20f ? 0.5f : 0f;
        }

        /// <summary>
        /// Resonance: 10% chance to apply a random debuff on ability hit.
        /// Returns a StatusEffectData to apply, or null.
        /// </summary>
        public StatusEffectData TryResonance()
        {
            if (!HasPerk(Perks.Resonance)) return null;
            if (GD.Randf() >= 0.10f) return null;

            // Pick a random debuff type
            int roll = (int)(GD.Randf() * 3f);
            return roll switch
            {
                0 => CreateDebuff("resonance_slow", "Disrupted", StatType.MoveSpeed, ModifierType.Percent, -0.20f, 3f),
                1 => CreateDebuff("resonance_weaken", "Weakened", StatType.Armor, ModifierType.Flat, -5f, 3f),
                _ => CreateDebuff("resonance_fragile", "Fragile", StatType.CritChance, ModifierType.Flat, -0.10f, 3f),
            };
        }

        /// <summary>
        /// Broadcast Tower: when a debuff is applied, try to spread it to nearby enemies.
        /// </summary>
        public void TryBroadcastSpread(Node3D target, StatusEffectData debuff)
        {
            if (!HasPerk(Perks.BroadcastTower)) return;
            if (debuff == null || !debuff.IsDebuff) return;

            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 6f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, target.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                if (collider == target) continue;

                var sem = FindStatusEffectManager(collider);
                if (sem != null)
                {
                    // Spread with +50% duration from Broadcast Tower
                    var spreadData = new StatusEffectData
                    {
                        Id = debuff.Id,
                        EffectName = debuff.EffectName,
                        Duration = debuff.Duration * 1.5f,
                        TickInterval = debuff.TickInterval,
                        TickDamage = debuff.TickDamage,
                        TickDamageType = debuff.TickDamageType,
                        IsDebuff = true
                    };
                    foreach (var mod in debuff.StatModifications)
                        spreadData.StatModifications.Add(mod);
                    sem.ApplyEffect(spreadData);
                }
            }
        }

        /// <summary>
        /// Smoke Screen: should a smoke cloud spawn after this dash?
        /// </summary>
        public bool ShouldSpawnSmokeCloud()
        {
            return HasPerk(Perks.SmokeScreen);
        }

        /// <summary>
        /// Scrap Recycler: 15% chance to drop a repair orb on kill.
        /// Returns true if an orb should spawn.
        /// </summary>
        public bool TryScrapRecycler()
        {
            if (!HasPerk(Perks.ScrapRecycler)) return false;
            return GD.Randf() < 0.15f;
        }

        /// <summary>
        /// Check if Ricochet Rounds is active (projectile bounce).
        /// </summary>
        public bool HasRicochetRounds() => HasPerk(Perks.RicochetRounds);

        /// <summary>
        /// Check if Chain Lightning is active (ability hit arc).
        /// </summary>
        public bool HasChainLightning() => HasPerk(Perks.ChainLightning);

        // =================================================================
        // TICKING AURA EFFECTS
        // =================================================================

        private void TickCorrosiveAura(float dt)
        {
            _corrosiveAuraTickTimer -= dt;
            if (_corrosiveAuraTickTimer > 0f) return;
            _corrosiveAuraTickTimer = CORROSIVE_AURA_TICK_RATE;

            if (!_player.IsInsideTree()) return;
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = CORROSIVE_AURA_RADIUS };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            var debuff = CreateDebuff("corrosive_aura", "Corroded",
                StatType.Armor, ModifierType.Percent, -0.15f, 1f);

            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                var sem = FindStatusEffectManager(collider);
                sem?.ApplyEffect(debuff);
            }
        }

        private void TickAdaptivePlating(float dt)
        {
            _adaptivePlatingTickTimer -= dt;
            if (_adaptivePlatingTickTimer > 0f) return;
            _adaptivePlatingTickTimer = ADAPTIVE_PLATING_TICK_RATE;

            if (!_player.IsInsideTree()) return;
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = ADAPTIVE_PLATING_RADIUS };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            int nearbyCount = Mathf.Min(results.Count, 5);
            float dr = Mathf.Min(nearbyCount * ADAPTIVE_PLATING_DR_PER_ENEMY, ADAPTIVE_PLATING_MAX_DR);

            RemoveAdaptivePlatingMod();
            if (dr > 0f)
            {
                _adaptivePlatingMod = new StatModifier(StatType.Armor, ModifierType.Percent, dr, this);
                _stats.Stats.AddModifier(_adaptivePlatingMod);
            }
        }

        private void TickFeedbackLoop(float dt)
        {
            _feedbackLoopTickTimer -= dt;
            if (_feedbackLoopTickTimer > 0f) return;
            _feedbackLoopTickTimer = FEEDBACK_LOOP_TICK_RATE;

            if (!_player.IsInsideTree() || !_health.IsAlive) return;

            // Check if any nearby enemy has status effects we applied
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 12f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            int affectedCount = 0;
            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                var sem = FindStatusEffectManager(collider);
                if (sem != null && sem.ActiveEffects.Count > 0)
                    affectedCount++;
            }

            if (affectedCount > 0)
            {
                float healAmount = _health.MaxHealth * FEEDBACK_LOOP_HEAL_PCT * affectedCount;
                _health.Heal(healAmount);
            }
        }

        private void TickArcReactor(float dt)
        {
            _arcReactorTickTimer -= dt;
            if (_arcReactorTickTimer > 0f) return;
            _arcReactorTickTimer = ARC_REACTOR_TICK_RATE;

            if (!_player.IsInsideTree()) return;
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = ARC_REACTOR_RADIUS };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            float tickDamage = ARC_REACTOR_DPS * ARC_REACTOR_TICK_RATE;
            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                var damageable = FindDamageable(collider);
                if (damageable != null && damageable.IsAlive)
                {
                    var dmgInfo = new DamageInfo
                    {
                        RawDamage = tickDamage,
                        FinalDamage = tickDamage,
                        DamageType = DamageType.Lightning,
                        Attacker = _player,
                        Target = collider
                    };
                    damageable.TakeDamage(dmgInfo);
                }
            }
        }

        private void TickSiegePlating(float dt)
        {
            _siegePlatingTickTimer -= dt;
            if (_siegePlatingTickTimer > 0f) return;
            _siegePlatingTickTimer = SIEGE_PLATING_TICK_RATE;

            if (!_player.IsInsideTree()) return;

            // Apply 20% damage reduction debuff to nearby allies
            foreach (var ally in PlayerManager.Players)
            {
                if (ally == null || !GodotObject.IsInstanceValid(ally)) continue;
                if (ally == _player) continue;

                float dist = _player.GlobalPosition.FlatDistance(ally.GlobalPosition);
                if (dist <= SIEGE_PLATING_RADIUS)
                {
                    var sem = ally.GetNodeOrNull<StatusEffectManager>("StatusEffectManager");
                    if (sem != null)
                    {
                        var buff = new StatusEffectData
                        {
                            Id = "siege_plating_dr",
                            EffectName = "Siege Plating",
                            Duration = 1f,
                            IsDebuff = false
                        };
                        buff.AddStatMod(StatType.Armor, ModifierType.Percent, 0.20f);
                        sem.ApplyEffect(buff);
                    }
                }
            }
        }

        /// <summary>
        /// Apply pinnacle size bonuses once the player body is built.
        /// JuggernautFrame: +30% size, WarMachine: +25% size.
        /// </summary>
        private void ApplyPinnacleScale()
        {
            _pinnacleScaleApplied = true;

            float scaleBonus = 1f;
            if (HasPerk(Perks.JuggernautFrame))
                scaleBonus = 1.30f;
            else if (HasPerk(Perks.WarMachine))
                scaleBonus = 1.25f;

            if (scaleBonus > 1f)
            {
                _player.BodyRoot.Scale = Vector3.One * scaleBonus;
                GD.Print($"[PerkProcessor] Pinnacle scale applied: {scaleBonus:F2}x");
            }

            // Pinnacle aura VFX
            SpawnPinnacleAura();
        }

        private void SpawnPinnacleAura()
        {
            Color? auraColor = null;
            float auraRadius = 2f;

            if (HasPerk(Perks.ArcReactor))
            {
                auraColor = new Color(0.5f, 0.7f, 1f); // Electric blue
                auraRadius = ARC_REACTOR_RADIUS;
            }
            else if (HasPerk(Perks.SiegePlating))
            {
                auraColor = new Color(0.9f, 0.7f, 0.2f); // Golden shield
                auraRadius = SIEGE_PLATING_RADIUS;
            }
            else if (HasPerk(Perks.JuggernautFrame))
            {
                auraColor = new Color(0.8f, 0.3f, 0.2f); // Red-orange strength
                auraRadius = 1.5f;
            }
            else if (HasPerk(Perks.WarMachine))
            {
                auraColor = new Color(0.9f, 0.2f, 0.2f); // Red aggression
                auraRadius = 2f;
            }
            else if (HasPerk(Perks.AssaultFrame))
            {
                auraColor = new Color(0.3f, 0.9f, 0.5f); // Green agility
                auraRadius = 1.5f;
            }
            else if (HasPerk(Perks.BroadcastTower))
            {
                auraColor = new Color(0.6f, 0.3f, 0.9f); // Purple debuff spread
                auraRadius = 3f;
            }

            if (auraColor.HasValue)
            {
                _pinnacleAuraVfx = VfxFactory.CreateAuraRing(auraColor.Value, auraRadius);
                _player.AddChild(_pinnacleAuraVfx);
            }
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        private void OnEnemyKilled(Node enemy)
        {
            // Adrenaline Rush: +15% speed, +5% crit for 3s after kill
            if (HasPerk(Perks.AdrenalineRush))
            {
                RemoveAdrenalineMods();
                _adrenalineTimer = ADRENALINE_DURATION;
                _adrenalineSpeedMod = new StatModifier(StatType.MoveSpeed, ModifierType.Percent, ADRENALINE_SPEED_BONUS, this);
                _adrenaliaCritMod = new StatModifier(StatType.CritChance, ModifierType.Flat, ADRENALINE_CRIT_BONUS, this);
                _stats.Stats.AddModifier(_adrenalineSpeedMod);
                _stats.Stats.AddModifier(_adrenaliaCritMod);
            }

            // Scrap Recycler: 15% chance to drop repair orb
            if (TryScrapRecycler() && enemy is Node3D enemyNode)
            {
                SpawnRepairOrb(enemyNode.GlobalPosition);
            }

            // Volatile Core: enemy explodes on death
            TryVolatileExplosion(enemy);

            // Mythic: Devourer — permanent damage stack per kill
            if (HasPerk(Perks.MythicDevourer))
            {
                _devourerKillStacks++;
                if (_devourerKillStacks % 50 == 0)
                    GD.Print($"[PerkProcessor] Devourer stacks: {_devourerKillStacks} (+{_devourerKillStacks * 0.5f:F1}% damage)");
            }

            // Mythic: Neural Hijack — 15% chance to convert killed enemy to temp ally
            if (HasPerk(Perks.MythicNeuralHijack))
                TryNeuralHijack(enemy);
        }

        private void OnSectorEntered(int sector)
        {
            OnFloorChanged();
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private void RemoveAdrenalineMods()
        {
            if (_adrenalineSpeedMod != null)
            {
                _stats.Stats.RemoveModifier(_adrenalineSpeedMod);
                _adrenalineSpeedMod = null;
            }
            if (_adrenaliaCritMod != null)
            {
                _stats.Stats.RemoveModifier(_adrenaliaCritMod);
                _adrenaliaCritMod = null;
            }
        }

        private void RemoveReactivePlatingMod()
        {
            if (_reactivePlatingMod != null)
            {
                _stats.Stats.RemoveModifier(_reactivePlatingMod);
                _reactivePlatingMod = null;
            }
        }

        private void RemoveAdaptivePlatingMod()
        {
            if (_adaptivePlatingMod != null)
            {
                _stats.Stats.RemoveModifier(_adaptivePlatingMod);
                _adaptivePlatingMod = null;
            }
        }

        private void ApplyReactivePlating()
        {
            if (!HasPerk(Perks.ReactivePlating)) return;

            _reactivePlatingStacks = Mathf.Min(_reactivePlatingStacks + 1, REACTIVE_PLATING_MAX_STACKS);
            _reactivePlatingTimer = REACTIVE_PLATING_DURATION;

            // Update the armor modifier to match current stacks
            RemoveReactivePlatingMod();
            float armorBonus = REACTIVE_PLATING_ARMOR * _reactivePlatingStacks;
            _reactivePlatingMod = new StatModifier(StatType.Armor, ModifierType.Flat, armorBonus, this);
            _stats.Stats.AddModifier(_reactivePlatingMod);
        }

        private void SpawnRepairOrb(Vector3 position)
        {
            // Simple heal pickup — create a small glowing orb that heals on contact
            var orb = new Area3D();
            orb.Name = "RepairOrb";

            var collisionShape = new CollisionShape3D();
            collisionShape.Shape = new SphereShape3D { Radius = 1f };
            orb.AddChild(collisionShape);

            var mesh = new MeshInstance3D();
            var sphere = new SphereMesh { Radius = 0.25f, Height = 0.5f };
            mesh.Mesh = sphere;
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 1f, 0.3f, 0.9f),
                EmissionEnabled = true,
                Emission = new Color(0.3f, 1f, 0.4f),
                EmissionEnergyMultiplier = 2f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            mesh.MaterialOverride = mat;
            orb.AddChild(mesh);

            orb.CollisionLayer = 0;
            orb.CollisionMask = Constants.MASK_PLAYER;
            orb.Monitoring = true;

            _player.GetTree().Root.AddChild(orb);
            orb.GlobalPosition = position + Vector3.Up * 0.5f;

            orb.BodyEntered += (body) =>
            {
                if (body is PlayerController pc)
                {
                    pc.Health.Heal(pc.Health.MaxHealth * 0.10f);
                    if (GodotObject.IsInstanceValid(orb))
                        orb.QueueFree();
                }
            };

            // Auto-expire after 10 seconds
            orb.GetTree().CreateTimer(10f).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(orb) && orb.IsInsideTree())
                    orb.QueueFree();
            };
        }

        private static StatusEffectData CreateDebuff(string id, string name,
            StatType stat, ModifierType modType, float value, float duration)
        {
            var data = new StatusEffectData
            {
                Id = id,
                EffectName = name,
                Duration = duration,
                IsDebuff = true
            };
            data.AddStatMod(stat, modType, value);
            return data;
        }

        private static StatusEffectManager FindStatusEffectManager(Node node)
        {
            Node current = node;
            while (current != null)
            {
                var sem = current.GetNodeOrNull<StatusEffectManager>("StatusEffectManager");
                if (sem != null) return sem;
                current = current.GetParent();
            }
            return null;
        }

        // =================================================================
        // SALVAGE CORE PERK EFFECTS
        // =================================================================

        /// <summary>
        /// Vampiric Core: 3% lifesteal on all damage dealt.
        /// Called after dealing damage to heal the player.
        /// </summary>
        public void TryVampiricLifesteal(float damageDealt)
        {
            if (!HasPerk(Perks.CoreVampiric)) return;
            float heal = damageDealt * 0.03f;
            if (heal > 0.1f)
                _health.Heal(heal);
        }

        /// <summary>
        /// Volatile Core: enemy explodes on death for 30% max HP AoE.
        /// Called from OnEnemyKilled.
        /// </summary>
        private void TryVolatileExplosion(Node enemy)
        {
            if (!HasPerk(Perks.CoreVolatile)) return;
            if (enemy is not Node3D enemyNode) return;

            // Get enemy's max HP for explosion damage
            var enemyHealth = enemyNode.GetNodeOrNull<HealthComponent>("HealthComponent");
            float maxHp = enemyHealth?.MaxHealth ?? 50f;
            float explosionDmg = maxHp * 0.30f;

            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 4f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, enemyNode.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                if (collider == enemy) continue;
                var damageable = FindDamageable(collider);
                if (damageable != null && damageable.IsAlive)
                {
                    var dmg = new DamageInfo
                    {
                        RawDamage = explosionDmg,
                        FinalDamage = explosionDmg,
                        DamageType = DamageType.Fire,
                        Attacker = _player,
                        Target = collider
                    };
                    damageable.TakeDamage(dmg);
                }
            }

            // Explosion VFX
            var impact = VfxFactory.CreateImpactBurst(new Color(1f, 0.5f, 0.1f));
            _player.GetTree().Root.AddChild(impact);
            impact.GlobalPosition = enemyNode.GlobalPosition;

            var aoeRing = VfxFactory.CreateAoEIndicator(new Color(1f, 0.4f, 0.1f), 4f, 0.4f);
            _player.GetTree().Root.AddChild(aoeRing);
            aoeRing.GlobalPosition = enemyNode.GlobalPosition;
        }

        /// <summary>
        /// Prismatic Core: convert damage to a random element.
        /// Returns a randomized DamageType each call.
        /// </summary>
        public DamageType GetPrismaticElement(DamageType original)
        {
            if (!HasPerk(Perks.CorePrismatic)) return original;
            return PrismaticElements[(int)(GD.Randf() * PrismaticElements.Length) % PrismaticElements.Length];
        }

        /// <summary>
        /// Singularity Core: pull nearby enemies toward player on ability cast.
        /// </summary>
        public void TrySingularityPull()
        {
            if (!HasPerk(Perks.CoreSingularity)) return;
            if (_singularityCooldown > 0f) return;
            _singularityCooldown = SINGULARITY_CD;

            if (!_player.IsInsideTree()) return;
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = SINGULARITY_RADIUS };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            foreach (var result in results)
            {
                var collider = result["collider"].As<Node3D>();
                if (collider == null) continue;

                // Pull toward player
                var pullDir = (_player.GlobalPosition - collider.GlobalPosition).Normalized();
                float pullDist = _player.GlobalPosition.DistanceTo(collider.GlobalPosition);
                float pullAmount = Mathf.Min(pullDist * 0.5f, 4f);

                if (collider is CharacterBody3D body)
                {
                    body.Velocity += pullDir * pullAmount * 10f;
                }
                else
                {
                    collider.GlobalPosition += pullDir * pullAmount;
                }
            }
        }

        /// <summary>
        /// Get ability level bonus from Amplifier Cores.
        /// </summary>
        public int GetAbilityLevelBonus(string abilityId)
        {
            return _classCtrl?.PassiveTree?.GetAbilityLevelBonus(abilityId) ?? 0;
        }

        // =================================================================
        // MYTHIC PERK EFFECTS
        // =================================================================

        private void TickImmortalEngine(float dt)
        {
            // Invulnerability timer
            if (_immortalEngineInvulnTimer > 0f)
            {
                _immortalEngineInvulnTimer -= dt;
                // Keep HP at minimum 1 during invuln
                if (_health.CurrentHealth < 1f)
                    _health.Heal(1f);
            }

            // Passive regen: 5% max HP per second
            _immortalEngineRegenTimer -= dt;
            if (_immortalEngineRegenTimer <= 0f)
            {
                _immortalEngineRegenTimer = IMMORTAL_REGEN_TICK;
                if (_health.IsAlive)
                    _health.Heal(_health.MaxHealth * IMMORTAL_REGEN_PCT * IMMORTAL_REGEN_TICK);
            }
        }

        /// <summary>
        /// Immortal Engine: should lethal damage be blocked?
        /// Called by HealthComponent before applying lethal hit.
        /// </summary>
        public bool ShouldSurviveLethal()
        {
            if (!HasPerk(Perks.MythicImmortalEngine)) return false;
            return _immortalEngineInvulnTimer > 0f;
        }

        private void TryNeuralHijack(Node enemy)
        {
            if (GD.Randf() >= 0.15f) return; // 15% chance
            if (enemy is not EnemyController ec) return;

            var deathPos = ec.GlobalPosition;
            var enemyData = ec.Data;
            if (enemyData == null || enemyData.IsBoss) return; // Can't convert bosses

            // Spawn a converted copy as a temporary ally
            var tree = _player.GetTree();
            var root = tree.Root;

            tree.CreateTimer(0.3f).Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(root)) return;

                // Create a visual indicator — green tinted ally
                var allyOrb = new Area3D();
                allyOrb.Name = "HijackedAlly";

                var collisionShape = new CollisionShape3D();
                collisionShape.Shape = new SphereShape3D { Radius = 2f };
                allyOrb.AddChild(collisionShape);

                // Simple visual representation
                var mesh = new MeshInstance3D();
                var capsule = new CapsuleMesh { Radius = 0.4f, Height = 1.2f };
                mesh.Mesh = capsule;
                var mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.2f, 0.8f, 0.4f, 0.8f),
                    EmissionEnabled = true,
                    Emission = new Color(0.1f, 1f, 0.3f),
                    EmissionEnergyMultiplier = 1.5f,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                };
                mesh.MaterialOverride = mat;
                allyOrb.AddChild(mesh);

                allyOrb.CollisionLayer = 0;
                allyOrb.CollisionMask = Constants.MASK_ENEMY;
                allyOrb.Monitoring = true;

                root.AddChild(allyOrb);
                allyOrb.GlobalPosition = deathPos + Vector3.Up * 0.6f;

                // Deal damage to enemies it touches
                float allyDamage = _stats.Stats.GetStat(StatType.Strength) * 2f;
                float damageTimer = 0f;
                allyOrb.SetPhysicsProcess(true);

                // Simple AI: move toward nearest enemy and deal AoE damage
                float lifetime = 20f;
                var moveTarget = Vector3.Zero;
                var random = new System.Random();

                var timerNode = new Timer();
                timerNode.WaitTime = 0.5f;
                timerNode.Autostart = true;
                allyOrb.AddChild(timerNode);
                timerNode.Timeout += () =>
                {
                    if (!GodotObject.IsInstanceValid(allyOrb)) return;
                    lifetime -= 0.5f;
                    if (lifetime <= 0f)
                    {
                        allyOrb.QueueFree();
                        return;
                    }

                    // Find nearest enemy and move toward it
                    var spaceState = allyOrb.GetWorld3D().DirectSpaceState;
                    var shape = new SphereShape3D { Radius = 10f };
                    var queryParams = new PhysicsShapeQueryParameters3D
                    {
                        Shape = shape,
                        Transform = new Transform3D(Basis.Identity, allyOrb.GlobalPosition),
                        CollisionMask = Constants.MASK_ENEMY
                    };
                    var results = spaceState.IntersectShape(queryParams);

                    Node3D closest = null;
                    float bestDist = float.MaxValue;
                    foreach (var r in results)
                    {
                        var c = r["collider"].As<Node3D>();
                        if (c == null) continue;
                        float d = allyOrb.GlobalPosition.DistanceTo(c.GlobalPosition);
                        if (d < bestDist) { bestDist = d; closest = c; }
                    }

                    if (closest != null)
                    {
                        var dir = (closest.GlobalPosition - allyOrb.GlobalPosition).Normalized();
                        allyOrb.GlobalPosition += dir * 2f; // Move toward enemy

                        // Deal damage in small radius
                        if (bestDist < 2.5f)
                        {
                            var dmgShape = new SphereShape3D { Radius = 2f };
                            var dmgQuery = new PhysicsShapeQueryParameters3D
                            {
                                Shape = dmgShape,
                                Transform = new Transform3D(Basis.Identity, allyOrb.GlobalPosition),
                                CollisionMask = Constants.MASK_ENEMY
                            };
                            var dmgResults = spaceState.IntersectShape(dmgQuery);
                            foreach (var dr in dmgResults)
                            {
                                var target = (Node)dr["collider"];
                                var hp = FindDamageable(target);
                                if (hp != null && hp.IsAlive)
                                {
                                    var dmg = new DamageInfo
                                    {
                                        RawDamage = allyDamage,
                                        FinalDamage = allyDamage,
                                        DamageType = DamageType.Dark,
                                        Attacker = _player,
                                        Target = target
                                    };
                                    hp.TakeDamage(dmg);
                                }
                            }
                        }
                    }
                };

                GD.Print($"[PerkProcessor] Neural Hijack: converted {enemyData.EnemyName} to ally for 20s!");
            };
        }

        private void TickTimeLoopSnapshot(float dt)
        {
            _timeLoopSnapshotTimer -= dt;
            if (_timeLoopSnapshotTimer > 0f) return;
            _timeLoopSnapshotTimer = TIME_LOOP_SNAPSHOT_INTERVAL;

            // Continuously snapshot player state (rolling 3s window approximation)
            _timeLoopPosition = _player.GlobalPosition;
            _timeLoopHealth = _health.CurrentHealth;
            _timeLoopMana = _stats.CurrentMana;
        }

        private void TriggerTimeLoop()
        {
            _timeLoopUsedThisFloor = true;

            // Rewind to snapshot
            _player.GlobalPosition = _timeLoopPosition;
            _health.Heal(_health.MaxHealth); // Full heal
            _stats.RestoreMana(_stats.MaxMana); // Full mana

            // VFX flash
            var flash = VfxFactory.CreateImpactBurst(new Color(0.4f, 0.8f, 1f));
            _player.GetTree().Root.AddChild(flash);
            flash.GlobalPosition = _player.GlobalPosition + Vector3.Up;

            GD.Print("[PerkProcessor] Paradox Gland: TIME REWIND! Restored to snapshot state.");
        }

        /// <summary>
        /// Reset time loop for new floor.
        /// Call this when the player enters a new floor/area.
        /// </summary>
        public void ResetTimeLoop()
        {
            _timeLoopUsedThisFloor = false;
        }

        /// <summary>
        /// Storm Caller: chain all damage to 3 extra targets at 40%.
        /// Called after any damage is dealt to an enemy.
        /// </summary>
        public void TryStormCallerChain(Node3D hitTarget, float damageDealt, DamageType damageType)
        {
            if (!HasPerk(Perks.MythicStormCaller)) return;
            if (!_player.IsInsideTree() || hitTarget == null) return;

            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 8f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, hitTarget.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            float chainDamage = damageDealt * 0.40f;
            int chainsLeft = 3;
            Node3D lastSource = hitTarget;

            foreach (var result in results)
            {
                if (chainsLeft <= 0) break;
                var collider = result["collider"].As<Node3D>();
                if (collider == null || collider == hitTarget) continue;

                var hp = FindDamageable(collider);
                if (hp == null || !hp.IsAlive) continue;

                var dmg = new DamageInfo
                {
                    RawDamage = chainDamage,
                    FinalDamage = chainDamage,
                    DamageType = damageType,
                    Attacker = _player,
                    Target = collider
                };
                hp.TakeDamage(dmg);

                // Lightning arc VFX
                var arc = VfxFactory.CreateLightningArc(
                    lastSource.GlobalPosition + Vector3.Up * 0.8f,
                    collider.GlobalPosition + Vector3.Up * 0.8f,
                    new Color(0.6f, 0.3f, 1f));
                _player.GetTree().Root.AddChild(arc);

                lastSource = collider;
                chainsLeft--;
            }
        }

        /// <summary>
        /// Void Heart: spawn a void rift at attack location.
        /// Called after dealing damage to an enemy.
        /// </summary>
        public void TryVoidHeartRift(Vector3 position, float baseDamage)
        {
            if (!HasPerk(Perks.MythicVoidHeart)) return;
            if (_voidHeartCooldown > 0f) return;
            _voidHeartCooldown = VOID_HEART_CD;

            float riftDps = baseDamage * 0.20f;
            float riftRadius = 3f;
            float riftDuration = 4f;

            // Spawn rift AoE
            var rift = new Area3D();
            rift.Name = "VoidRift";
            var collisionShape = new CollisionShape3D();
            collisionShape.Shape = new SphereShape3D { Radius = riftRadius };
            rift.AddChild(collisionShape);

            rift.CollisionLayer = 0;
            rift.CollisionMask = Constants.MASK_ENEMY;
            rift.Monitoring = true;

            _player.GetTree().Root.AddChild(rift);
            rift.GlobalPosition = position;

            // Dark VFX ring
            var vfx = VfxFactory.CreateAoEIndicator(new Color(0.3f, 0f, 0.5f), riftRadius, riftDuration);
            _player.GetTree().Root.AddChild(vfx);
            vfx.GlobalPosition = position;

            // Tick damage every 0.5s for duration
            float elapsed = 0f;
            var tickTimer = new Timer();
            tickTimer.WaitTime = 0.5f;
            tickTimer.Autostart = true;
            rift.AddChild(tickTimer);
            tickTimer.Timeout += () =>
            {
                elapsed += 0.5f;
                if (elapsed >= riftDuration || !GodotObject.IsInstanceValid(rift))
                {
                    if (GodotObject.IsInstanceValid(rift))
                        rift.QueueFree();
                    return;
                }

                var spaceState = rift.GetWorld3D().DirectSpaceState;
                var shape = new SphereShape3D { Radius = riftRadius };
                var queryParams = new PhysicsShapeQueryParameters3D
                {
                    Shape = shape,
                    Transform = new Transform3D(Basis.Identity, rift.GlobalPosition),
                    CollisionMask = Constants.MASK_ENEMY
                };
                var results = spaceState.IntersectShape(queryParams);

                float tickDmg = riftDps * 0.5f; // Half-second ticks
                foreach (var r in results)
                {
                    var target = (Node)r["collider"];
                    var hp = FindDamageable(target);
                    if (hp != null && hp.IsAlive)
                    {
                        var dmg = new DamageInfo
                        {
                            RawDamage = tickDmg,
                            FinalDamage = tickDmg,
                            DamageType = DamageType.Dark,
                            Attacker = _player,
                            Target = target
                        };
                        hp.TakeDamage(dmg);
                    }
                }
            };
        }

        /// <summary>
        /// Echo Chamber: should the ability fire a second time at 60% damage?
        /// Returns true if echo should fire.
        /// </summary>
        public bool ShouldEchoChamber() => HasPerk(Perks.MythicEchoChamber);

        /// <summary>
        /// Echo Chamber damage multiplier for the second cast.
        /// </summary>
        public const float EchoChamberDamageMult = 0.60f;

        /// <summary>
        /// Blood Economy (Hemorrhage Engine): should ability cost HP instead of mana?
        /// If active, returns true and the caller should deduct HP at 2:1 ratio.
        /// </summary>
        public bool ShouldUseBloodEconomy() => HasPerk(Perks.MythicBloodEconomy);

        /// <summary>
        /// Blood Economy: spend HP instead of mana. Returns true if paid.
        /// </summary>
        public bool SpendHealthForMana(float manaCost)
        {
            if (!HasPerk(Perks.MythicBloodEconomy)) return false;

            float hpCost = manaCost * 0.5f; // 2:1 ratio (mana costs half as HP)
            if (_health.CurrentHealth <= hpCost + 1f) return false; // Don't kill yourself

            var selfDamage = new DamageInfo
            {
                RawDamage = hpCost,
                FinalDamage = hpCost,
                DamageType = DamageType.Physical,
                Attacker = _player,
                Target = _player
            };
            _health.TakeDamage(selfDamage);
            return true;
        }

        /// <summary>
        /// Reset per-floor state (e.g., time loop availability).
        /// </summary>
        public void OnFloorChanged()
        {
            ResetTimeLoop();
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
