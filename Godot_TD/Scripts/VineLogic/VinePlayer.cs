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
        private float _bounceTimer;
        private bool _isMoving;
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
                _isMoving = true;
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
                _isMoving = false;
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

            // Low-gravity bouncy movement — model bobs up and down
            _bounceTimer += dt * (_isMoving ? 2.2f : 0.8f); // Slow, floaty bounces
            float bounceHeight = _isMoving ? 0.25f : 0.08f;  // Gentle low-gravity hops
            float bounce = Mathf.Abs(Mathf.Sin(_bounceTimer * Mathf.Pi)) * bounceHeight;
            if (_modelRoot != null)
                _modelRoot.Position = new Vector3(_modelRoot.Position.X, bounce, _modelRoot.Position.Z);

            // Slight tilt when moving — leans into movement direction
            if (_modelRoot != null && _isMoving)
            {
                float tiltAmount = Mathf.Sin(_bounceTimer * Mathf.Pi) * 8f; // degrees
                var rot = _modelRoot.RotationDegrees;
                _modelRoot.RotationDegrees = new Vector3(tiltAmount, rot.Y, 0);
            }
            else if (_modelRoot != null)
            {
                var rot = _modelRoot.RotationDegrees;
                _modelRoot.RotationDegrees = new Vector3(0, rot.Y, 0);
            }
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

            // BIT doesn't have attack animation — do a quick lunge toward target
            if (_modelRoot != null && closest != null)
            {
                var dir = (closest.GlobalPosition - GlobalPosition).Normalized();
                float lungeAngle = Mathf.Atan2(dir.X, dir.Z);
                _modelRoot.Rotation = new Vector3(15f * Mathf.DegToRad(1), lungeAngle, 0); // Lean forward
                // Reset after brief delay
                GetTree().CreateTimer(0.15).Timeout += () => {
                    if (_modelRoot != null && IsInstanceValid(_modelRoot))
                        _modelRoot.Rotation = new Vector3(0, _modelRoot.Rotation.Y, 0);
                };
            }

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

                // Tronify BIT — same treatment as the harvester:
                // dark black body + thin cyan inverted hull outline
                bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
                var accent = isScrapyard ? new Color(0.9f, 0.6f, 0.1f) : new Color(0.0f, 0.85f, 0.95f);
                ApplyBitTronOutline(_modelRoot, accent);
                BoostEyeEmission(_modelRoot, accent);

                GD.Print("[VinePlayer] BIT tronified — dark body + outline");

                // Initialize animator
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

        /// <summary>
        /// Apply the same dark body + outline treatment as the harvester.
        /// Uses a thin outline to avoid the balloon effect on BIT's geometry.
        /// </summary>
        private static Shader _bitOutlineShader;

        private static void ApplyBitTronOutline(Node node, Color accent)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                // Log every mesh so we can identify the problematic one
                var aabb = mesh.GetAabb();
                float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
                string meshName = mesh.Name.ToString().ToLower();
                GD.Print($"[VinePlayer] BIT mesh: '{mesh.Name}' aabb={aabb.Size} maxDim={maxDim:F2} surfaces={mesh.Mesh.GetSurfaceCount()}");

                // Hide sphere/shield/collision meshes — check by name or by being
                // disproportionately large compared to other meshes, or by having
                // transparent/invisible original material
                bool isSuspect = meshName.Contains("sphere") || meshName.Contains("shield")
                    || meshName.Contains("collision") || meshName.Contains("aura");

                // Also check if this mesh has only 1 surface and is roughly spherical
                // (all 3 AABB dimensions similar = sphere shape)
                if (!isSuspect && maxDim > 0.01f)
                {
                    float minDim = Mathf.Min(aabb.Size.X, Mathf.Min(aabb.Size.Y, aabb.Size.Z));
                    float ratio = minDim / maxDim;
                    // If ratio > 0.7 and it's the largest mesh, it's likely a sphere
                    if (ratio > 0.6f && maxDim > 0.5f)
                        isSuspect = true;
                }

                if (isSuspect)
                {
                    mesh.Visible = false;
                    GD.Print($"[VinePlayer] HIDING suspect mesh '{mesh.Name}' (size={maxDim:F2}, sphereRatio={aabb.Size})");
                    return;
                }

                // Dark body
                var bodyMat = new StandardMaterial3D();
                bodyMat.AlbedoColor = new Color(0.02f, 0.02f, 0.03f);
                bodyMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

                // Thin outline
                if (_bitOutlineShader == null)
                {
                    _bitOutlineShader = new Shader();
                    _bitOutlineShader.Code = @"
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 outline_color : source_color = vec3(0.0, 0.85, 0.95);
uniform float outline_width : hint_range(0.0, 0.3) = 0.02;
void vertex() { VERTEX += NORMAL * outline_width; }
void fragment() { ALBEDO = outline_color; ALPHA = 0.9; }
";
                }

                float modelScale = mesh.GetParent() is Node3D parent ? parent.Scale.X : 1f;
                float outlineWidth = 0.02f / Mathf.Max(modelScale, 0.01f);

                var outlineMat = new ShaderMaterial();
                outlineMat.Shader = _bitOutlineShader;
                outlineMat.SetShaderParameter("outline_color",
                    new Vector3(accent.R, accent.G, accent.B));
                outlineMat.SetShaderParameter("outline_width", Mathf.Clamp(outlineWidth, 0.005f, 0.08f));

                bodyMat.NextPass = outlineMat;
                mesh.MaterialOverride = bodyMat;
            }
            foreach (var child in node.GetChildren())
                ApplyBitTronOutline(child, accent);
        }

        /// <summary>
        /// OLD: Add Tron highlights without outline. Kept for reference.
        /// </summary>
        private static void TronHighlightAll_UNUSED(Node node, Color accent)
        {
            if (node is MeshInstance3D mesh)
            {
                // Get existing material or create one that preserves the model's look
                var existing = mesh.MaterialOverride as StandardMaterial3D
                    ?? mesh.Mesh?.SurfaceGetMaterial(0) as StandardMaterial3D;

                var mat = new StandardMaterial3D();
                if (existing?.AlbedoTexture != null)
                {
                    // Has texture — preserve it, darken slightly
                    mat.AlbedoTexture = existing.AlbedoTexture;
                    mat.AlbedoColor = existing.AlbedoColor.Darkened(0.2f);
                }
                else
                {
                    // No texture — use visible light grey (BIT's characteristic color)
                    mat.AlbedoColor = new Color(0.45f, 0.45f, 0.5f);
                }
                mat.Roughness = 0.7f;
                mat.Metallic = 0.3f;
                // Very subtle accent — just enough to hint, not overwhelm the grey body
                mat.EmissionEnabled = true;
                mat.Emission = accent;
                mat.EmissionEnergyMultiplier = 0.04f;
                mesh.MaterialOverride = mat;
            }
            foreach (var child in node.GetChildren())
                TronHighlightAll_UNUSED(child, accent);
        }

        private static void ApplyMaterialToAll(Node node, StandardMaterial3D mat)
        {
            if (node is MeshInstance3D mesh)
                mesh.MaterialOverride = mat;
            foreach (var child in node.GetChildren())
                ApplyMaterialToAll(child, mat);
        }

        /// <summary>
        /// Find eye meshes (by name containing "eye") and boost their emission.
        /// </summary>
        private static void BoostEyeEmission(Node node, Color accent)
        {
            if (node is MeshInstance3D mesh && node.Name.ToString().ToLower().Contains("eye"))
            {
                var eyeMat = new StandardMaterial3D();
                eyeMat.AlbedoColor = accent;
                eyeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                eyeMat.EmissionEnabled = true;
                eyeMat.Emission = accent;
                eyeMat.EmissionEnergyMultiplier = 2f;
                mesh.MaterialOverride = eyeMat;
            }
            foreach (var child in node.GetChildren())
                BoostEyeEmission(child, accent);
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
