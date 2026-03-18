using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Player character ability definition.
    /// </summary>
    public class VinePlayerAbility
    {
        public string Name;
        public string Description;
        public float Cooldown;
        public float CurrentCooldown;
        public float ManaCost;
        public float Range;
        public Color IconColor;

        public delegate void AbilityExecute(VinePlayer player);
        public AbilityExecute Execute;

        public bool IsReady => CurrentCooldown <= 0;
    }

    /// <summary>
    /// MOBA-style player character for Vine Logic TD.
    /// WASD movement, auto-attacks, 3 abilities (Q/E/R).
    /// </summary>
    public partial class VinePlayer : CharacterBody3D
    {
        public float MaxHP { get; set; }
        public float CurrentHP { get; private set; }
        public float MaxMana { get; set; }
        public float CurrentMana { get; private set; }
        public float MoveSpeed { get; set; } = Constants.VINE_PLAYER_MOVE_SPEED;
        public float AttackRange { get; set; } = Constants.VINE_PLAYER_ATTACK_RANGE;
        public float AttackDamage { get; set; }
        public float AttackSpeed { get; set; }
        public float ManaRegen { get; set; }
        public bool IsAlive => CurrentHP > 0;
        public int EnemiesKilledPersonally { get; set; }

        private float _attackCooldown;
        private float _respawnTimer;
        private bool _isDead;
        private VineGrid _grid;

        // Visual
        private Node3D _modelRoot;
        private CharacterAnimator _animator;
        private MeshInstance3D _healthBar;
        private MeshInstance3D _healthBarBg;
        private float _flashTimer;

        // Abilities
        private VinePlayerAbility[] _abilities;

        public override void _Ready()
        {
            // Apply meta perk bonuses from SignalTuningEditor statics
            MaxHP = Constants.VINE_PLAYER_MAX_HP + SignalTuningEditor.PlayerMaxHPBonus;
            MaxMana = Constants.VINE_PLAYER_MAX_MANA + SignalTuningEditor.PlayerMaxManaBonus;
            AttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE * SignalTuningEditor.PlayerAttackDamageMult;
            AttackSpeed = Constants.VINE_PLAYER_ATTACK_SPEED * SignalTuningEditor.PlayerAttackSpeedMult;
            ManaRegen = Constants.VINE_PLAYER_MANA_REGEN * SignalTuningEditor.PlayerManaRegenMult;

            CurrentHP = MaxHP;
            CurrentMana = MaxMana;

            _grid = ServiceLocator.Get<VineGrid>();

            // Collision shape
            var shape = new CollisionShape3D();
            var capsule = new CapsuleShape3D();
            capsule.Radius = 0.4f;
            capsule.Height = 1.2f;
            shape.Shape = capsule;
            shape.Position = new Vector3(0, 0.6f, 0);
            AddChild(shape);

            // Physics layers
            CollisionLayer = 1u << (Constants.LAYER_HERO - 1);
            CollisionMask = (1u << (Constants.LAYER_ENEMY - 1)) | (1u << (Constants.LAYER_GROUND - 1));

            AddToGroup(Constants.GROUP_VINE_PLAYER);

            BuildVisual();
            BuildHealthBar();

            _abilities = GetDefaultAbilities();

            ServiceLocator.Register(this);
            GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);
            GameEvents.OnPlayerManaChanged?.Invoke(CurrentMana, MaxMana);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Respawn timer
            if (_isDead)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0)
                    Respawn();
                return;
            }

            // Movement — only during wave phase (build phase uses WASD for camera)
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase == GamePhase.Wave || phase == GamePhase.WaveComplete)
                HandleMovement(dt);

            // Mana regen
            if (CurrentMana < MaxMana)
            {
                CurrentMana = Mathf.Min(MaxMana, CurrentMana + ManaRegen * dt);
                GameEvents.OnPlayerManaChanged?.Invoke(CurrentMana, MaxMana);
            }

            // Auto-attack
            _attackCooldown -= dt;
            if (_attackCooldown <= 0)
                TryAutoAttack();

            // Ability cooldowns
            for (int i = 0; i < _abilities.Length; i++)
            {
                if (_abilities[i].CurrentCooldown > 0)
                {
                    _abilities[i].CurrentCooldown -= dt;
                    GameEvents.OnAbilityCooldownChanged?.Invoke(i, _abilities[i].CurrentCooldown);
                }
            }

            // Ability input
            if (Input.IsActionJustPressed("ability_q")) TryUseAbility(0);
            if (Input.IsActionJustPressed("ability_e")) TryUseAbility(1);
            if (Input.IsActionJustPressed("ability_r")) TryUseAbility(2);

            // Flash decay
            if (_flashTimer > 0)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0) SetFlash(false);
            }

            UpdateHealthBar();
        }

        private void HandleMovement(float dt)
        {
            var input = Vector3.Zero;
            if (Input.IsActionPressed("camera_pan_up")) input.Z -= 1;
            if (Input.IsActionPressed("camera_pan_down")) input.Z += 1;
            if (Input.IsActionPressed("camera_pan_left")) input.X -= 1;
            if (Input.IsActionPressed("camera_pan_right")) input.X += 1;

            if (input.LengthSquared() > 0)
            {
                input = input.Normalized();
                Velocity = input * MoveSpeed;
                _animator?.SetState(AnimState.Run);

                // Face movement direction
                if (_modelRoot != null)
                {
                    float angle = Mathf.Atan2(input.X, input.Z);
                    _modelRoot.Rotation = new Vector3(0, angle, 0);
                }
            }
            else
            {
                Velocity = Vector3.Zero;
                _animator?.SetState(AnimState.Idle);
            }

            MoveAndSlide();

            // Clamp to grid bounds
            float cs = Constants.VINE_CELL_SIZE;
            var pos = GlobalPosition;
            pos.X = Mathf.Clamp(pos.X, 0, _grid.Width * cs);
            pos.Z = Mathf.Clamp(pos.Z, 0, _grid.Height * cs);
            pos.Y = _grid.GetWorldHeight(pos.X, pos.Z);
            GlobalPosition = pos;
        }

        private void TryAutoAttack()
        {
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            VineEnemy closest = null;
            float closestDist = AttackRange;

            foreach (var node in enemies)
            {
                if (node is not VineEnemy enemy || !enemy.IsAlive) continue;
                float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }

            if (closest == null) return;

            _attackCooldown = 1f / AttackSpeed;
            _animator?.SetState(AnimState.Attack);

            // Fire projectile VFX
            VfxFactory.SpawnProjectile(GetTree(), GlobalPosition + Vector3.Up * 0.5f,
                closest.GlobalPosition + Vector3.Up * 0.5f,
                PlanetTheme.Current.ProjectileColor);

            // Deal damage
            float prevHP = closest.CurrentHealth;
            closest.TakeDamage(AttackDamage);
            if (!closest.IsAlive && prevHP > 0)
                EnemiesKilledPersonally++;
        }

        private void TryUseAbility(int slot)
        {
            if (slot < 0 || slot >= _abilities.Length) return;
            var ability = _abilities[slot];
            if (!ability.IsReady) return;
            if (CurrentMana < ability.ManaCost) return;

            CurrentMana -= ability.ManaCost;
            ability.CurrentCooldown = ability.Cooldown;
            ability.Execute?.Invoke(this);

            GameEvents.OnPlayerManaChanged?.Invoke(CurrentMana, MaxMana);
            GameEvents.OnAbilityCooldownChanged?.Invoke(slot, ability.CurrentCooldown);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || _isDead) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);

            _flashTimer = 0.12f;
            SetFlash(true);

            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(0.3f, 0.2f);

            GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
                Die();
        }

        private void Die()
        {
            _isDead = true;
            _respawnTimer = Constants.VINE_PLAYER_RESPAWN_TIME;
            _animator?.SetState(AnimState.Death);

            // Death VFX
            VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition, new Color(0.2f, 0.6f, 1f), 8);

            // Penalty damage to harvester
            if (_grid?.Harvester != null && !_grid.Harvester.IsDestroyed)
                _grid.Harvester.TakeDamage(Constants.VINE_PLAYER_DEATH_PENALTY);

            // Hide visual
            if (_modelRoot != null) _modelRoot.Visible = false;
            if (_healthBar != null) _healthBar.Visible = false;
            if (_healthBarBg != null) _healthBarBg.Visible = false;

            GameEvents.OnPlayerDied?.Invoke();
        }

        private void Respawn()
        {
            _isDead = false;
            CurrentHP = MaxHP;
            CurrentMana = MaxMana;

            // Respawn at harvester
            if (_grid?.Harvester != null)
                GlobalPosition = _grid.Harvester.GlobalPosition + new Vector3(-4f, 0, 0);

            // Show visual
            if (_modelRoot != null) _modelRoot.Visible = true;
            if (_healthBar != null) _healthBar.Visible = true;
            if (_healthBarBg != null) _healthBarBg.Visible = true;

            GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);
            GameEvents.OnPlayerManaChanged?.Invoke(CurrentMana, MaxMana);
        }

        // ── Visuals ──

        private void BuildVisual()
        {
            _modelRoot = AssetLibrary.InstantiateNormalized(AssetLibrary.COMPANION_BIT);
            if (_modelRoot != null)
            {
                AddChild(_modelRoot);
                AssetLibrary.GroundModel(_modelRoot);

                // Keep BIT's original textures (LilRobot.png / LilRobotEyes.png)
                // Don't apply planet theme — BIT should look like BIT on every planet
                GD.Print("[VinePlayer] BIT model loaded with original textures");

                // Initialize animator for walk/run/attack/idle
                _animator = new CharacterAnimator();
                AddChild(_animator);
                _animator.Initialize(_modelRoot);
            }
            else
            {
                // Fallback: simple placeholder sphere
                _modelRoot = new Node3D();
                AddChild(_modelRoot);
                var sphere = new MeshInstance3D();
                sphere.Mesh = new SphereMesh { Radius = 0.4f, Height = 0.8f };
                sphere.Position = new Vector3(0, 0.5f, 0);
                var mat = AssetLibrary.MakeEmissive(new Color(0.2f, 0.7f, 1.0f));
                sphere.MaterialOverride = mat;
                _modelRoot.AddChild(sphere);
                GD.PushWarning("[VinePlayer] bit.fbx not found, using fallback sphere");
            }
        }

        private static void ApplyMaterialToAll(Node node, StandardMaterial3D mat)
        {
            if (node is MeshInstance3D mesh)
                mesh.MaterialOverride = mat;
            foreach (var child in node.GetChildren())
                ApplyMaterialToAll(child, mat);
        }

        private void BuildHealthBar()
        {
            float barY = 1.8f;

            _healthBarBg = new MeshInstance3D();
            _healthBarBg.Mesh = new BoxMesh { Size = new Vector3(0.8f, 0.08f, 0.08f) };
            _healthBarBg.Position = new Vector3(0, barY, 0);
            var bgMat = new StandardMaterial3D();
            bgMat.AlbedoColor = new Color(0.15f, 0.15f, 0.15f);
            bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBarBg.MaterialOverride = bgMat;
            AddChild(_healthBarBg);

            _healthBar = new MeshInstance3D();
            _healthBar.Mesh = new BoxMesh { Size = new Vector3(0.8f, 0.06f, 0.06f) };
            _healthBar.Position = new Vector3(0, barY, 0);
            var barMat = new StandardMaterial3D();
            barMat.AlbedoColor = new Color(0.2f, 0.7f, 1.0f);
            barMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBar.MaterialOverride = barMat;
            AddChild(_healthBar);
        }

        private void UpdateHealthBar()
        {
            if (_healthBar == null) return;
            float pct = Mathf.Clamp(CurrentHP / MaxHP, 0f, 1f);
            float halfBar = 0.4f;
            _healthBar.Scale = new Vector3(pct, 1, 1);
            _healthBar.Position = new Vector3((pct - 1f) * halfBar, _healthBar.Position.Y, 0);

            if (_healthBar.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = pct > 0.5f ? new Color(0.2f, 0.7f, 1.0f) :
                    pct > 0.25f ? new Color(0.9f, 0.7f, 0.1f) :
                    new Color(0.9f, 0.1f, 0.1f);
        }

        private void SetFlash(bool flash)
        {
            if (_modelRoot == null) return;
            SetFlashRecursive(_modelRoot, flash);
        }

        private static void SetFlashRecursive(Node node, bool flash)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
            {
                if (flash)
                {
                    mat.EmissionEnabled = true;
                    mat.Emission = Colors.White;
                    mat.EmissionEnergyMultiplier = 1.2f;
                }
                else
                {
                    bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
                    var accent = isScrapyard ? new Color(0.9f, 0.6f, 0.1f) : new Color(0.2f, 0.7f, 1.0f);
                    mat.Emission = accent;
                    mat.EmissionEnergyMultiplier = isScrapyard ? 0.1f : 0.2f;
                }
            }
            foreach (var child in node.GetChildren())
                SetFlashRecursive(child, flash);
        }

        // ── Abilities ──

        private static VinePlayerAbility[] GetDefaultAbilities()
        {
            return new VinePlayerAbility[]
            {
                new VinePlayerAbility {
                    Name = "Shock Blast",
                    Description = "AoE damage around player",
                    Cooldown = 4f, ManaCost = 15f, Range = 5f,
                    IconColor = new Color(0.9f, 0.8f, 0.2f),
                    Execute = player => {
                        // AoE damage around player
                        var enemies = player.GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
                        foreach (var node in enemies)
                        {
                            if (node is not VineEnemy enemy || !enemy.IsAlive) continue;
                            float dist = player.GlobalPosition.DistanceTo(enemy.GlobalPosition);
                            if (dist < 5f)
                            {
                                float prevHP = enemy.CurrentHealth;
                                enemy.TakeDamage(25f);
                                if (!enemy.IsAlive && prevHP > 0)
                                    player.EnemiesKilledPersonally++;
                            }
                        }
                        VfxFactory.SpawnDeathBurst(player.GetTree(), player.GlobalPosition + Vector3.Up * 0.5f,
                            new Color(0.9f, 0.8f, 0.2f), 10);
                    }
                },
                new VinePlayerAbility {
                    Name = "Repair Pulse",
                    Description = "Heal the harvester",
                    Cooldown = 8f, ManaCost = 25f, Range = 12f,
                    IconColor = new Color(0.2f, 0.9f, 0.4f),
                    Execute = player => {
                        var grid = ServiceLocator.Get<VineGrid>();
                        if (grid?.Harvester != null)
                        {
                            grid.Harvester.Heal(40f);
                            VfxFactory.SpawnDeathBurst(player.GetTree(),
                                grid.Harvester.GlobalPosition + Vector3.Up * 2f,
                                new Color(0.2f, 0.9f, 0.4f), 8);
                        }
                    }
                },
                new VinePlayerAbility {
                    Name = "Overclock",
                    Description = "Boost all towers in radius for 5s",
                    Cooldown = 15f, ManaCost = 40f, Range = 8f,
                    IconColor = new Color(0.6f, 0.3f, 0.9f),
                    Execute = player => {
                        // Boost towers in radius — buff their damage
                        var towers = player.GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE);
                        foreach (var node in towers)
                        {
                            if (node is not VineNode vine) continue;
                            if (vine.Data?.Category != VineNodeCategory.Effect) continue;
                            float dist = player.GlobalPosition.DistanceTo(vine.GlobalPosition);
                            if (dist < 8f)
                                vine.ReceiveBuff(1.5f); // Strong buff
                        }
                        VfxFactory.SpawnDeathBurst(player.GetTree(), player.GlobalPosition + Vector3.Up * 0.5f,
                            new Color(0.6f, 0.3f, 0.9f), 8);
                    }
                }
            };
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<VinePlayer>();
        }
    }
}
