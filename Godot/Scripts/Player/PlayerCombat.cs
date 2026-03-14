using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Player combat node: gun-based basic attacks + 6 ability slots with cooldown ticking.
    /// Basic attacks fire projectiles toward cursor with class-specific muzzle flash VFX.
    /// </summary>
    public partial class PlayerCombat : Node, IAttacker
    {
        private PlayerController _player;
        private PlayerStats _playerStats;
        private IAnimatable _animatable;
        private readonly AbilitySlot[] _abilitySlots = new AbilitySlot[Constants.MAX_ABILITY_SLOTS];

        private float _basicAttackCooldown;
        private const float BASIC_ATTACK_RATE = 0.8f;
        private const float BASIC_SHOT_RANGE = 30f;
        private Camera3D _camera;

        // Weapon visual state
        private Node3D _weaponVisual;

        // Blade ring state
        private Node3D _bladeRingVisual;
        private bool _bladeRingActive;
        private float _bladeRingTickTimer;
        private const float BLADE_RING_TICK_RATE = 0.5f;
        private const float BLADE_RING_RADIUS = 7f;
        private const float BLADE_RING_DAMAGE_MULT = 0.5f;
        private const float BLADE_RING_SPIN_SPEED = 4f;

        // Pistol magazine state
        private int _pistolAmmo = 12;
        private const int PISTOL_MAG_SIZE = 12;
        private float _reloadTimer;
        private const float PISTOL_RELOAD_TIME = 1.5f;
        private bool _isReloading;

        // AoE weapon type tracking
        private WeaponType _aoeWeaponType;
        private bool _equipSubscribed;

        // Fire point from character config (editor-tweakable)
        private float _fireSide;
        private float _fireHeight = 0.9f;
        private float _fireForward = 0.8f;

        // Cached WeaponMount from FBX model (if available)
        private Marker3D _weaponMount;

        public StatBlock Stats => _playerStats?.Stats;
        public Node3D Node => _player;
        public Team Team => Team.Player;

        public override void _Ready()
        {
            _player = GetParent<PlayerController>();
            _playerStats = _player.GetNode<PlayerStats>("PlayerStats");

            for (int i = 0; i < _abilitySlots.Length; i++)
                _abilitySlots[i] = new AbilitySlot();

            // Defer subscription: _player.Inventory is null until PlayerController._Ready runs
            CallDeferred(nameof(SubscribeEquipmentChanges));
        }

        private void SubscribeEquipmentChanges()
        {
            if (_player?.Inventory != null)
            {
                _player.Inventory.OnEquipmentChanged += OnEquipmentChanged;
                _equipSubscribed = true;
            }

            // Load fire point from editor config
            var frame = _player?.ClassController?.CurrentClass ?? BotFrameType.TinCan;
            var (s, h, f) = CharacterConfigLoader.GetFirePoint(frame);
            _fireSide = s;
            _fireHeight = h;
            _fireForward = f;

            // Cache WeaponMount from FBX model (if present)
            var body = _player?.GetNodeOrNull<Node3D>("PlayerBody");
            if (body != null)
                _weaponMount = FindWeaponMount(body);
        }

        public override void _ExitTree()
        {
            if (_player?.Inventory != null)
                _player.Inventory.OnEquipmentChanged -= OnEquipmentChanged;
            RemoveAoEWeapon();
        }

        private void OnEquipmentChanged(EquipmentSlot slot, ItemInstance item)
        {
            if (slot != EquipmentSlot.MainHand) return;

            // Reset magazine on weapon swap so new weapons start fully loaded
            _pistolAmmo = PISTOL_MAG_SIZE;
            _isReloading = false;
            _basicAttackCooldown = 0;

            var equipData = item?.BaseData as EquipmentData;
            if (equipData?.WeaponType is WeaponType.BladeRing or WeaponType.FlailChain
                or WeaponType.ShockCoil or WeaponType.FlameThrower)
            {
                AttachAoEWeapon(equipData.WeaponType);
                RemoveWeaponVisual();
            }
            else
            {
                RemoveAoEWeapon();
                SwapWeaponVisual(item);
            }
        }

        private void SwapWeaponVisual(ItemInstance item)
        {
            RemoveWeaponVisual();
            if (item == null) return;

            var body = _player.GetNodeOrNull<Node3D>("PlayerBody");
            if (body == null) return;

            // Check configured mount type for this frame + weapon combo
            var frame = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
            var equipData = item?.BaseData as EquipmentData;
            var weaponType = equipData?.WeaponType ?? WeaponType.None;
            var mountType = CharacterConfigLoader.GetWeaponMountType(frame, weaponType);

            // Find or create the appropriate mount point
            Marker3D mount;
            if (mountType == WeaponMountType.HandHeld)
            {
                mount = FindWeaponMount(body);
            }
            else
            {
                // Parent the mount to the correct body pivot so weapons follow animation
                string pivotName = CharacterMeshBuilder.GetMountParentPivot(mountType);
                Node3D parentPivot = FindPivotRecursive(body, pivotName) ?? body;

                string mountName = $"Mount_{mountType}";
                mount = parentPivot.GetNodeOrNull<Marker3D>(mountName);
                if (mount == null)
                {
                    mount = new Marker3D();
                    mount.Name = mountName;

                    Vector3 bodySpacePos = CharacterMeshBuilder.GetMountPosition(frame, mountType);
                    if (parentPivot != body)
                    {
                        Vector3 pivotPos = CharacterMeshBuilder.GetPositionRelativeToPublic(parentPivot, body);
                        mount.Position = bodySpacePos - pivotPos;
                    }
                    else
                    {
                        mount.Position = bodySpacePos;
                    }

                    parentPivot.AddChild(mount);
                }
            }

            if (mount == null) return;

            // Build weapon model from item
            var weaponModel = CharacterMeshBuilder.BuildItemModel(item);
            if (weaponModel == null) return;

            weaponModel.Name = "EquippedWeapon";
            weaponModel.RotationDegrees = CharacterMeshBuilder.GetMountRotation(mountType);

            // Remove default weapon from mount (if any) — synchronous removal
            foreach (var child in mount.GetChildren())
            {
                if (child is Node3D existing)
                {
                    mount.RemoveChild(existing);
                    existing.QueueFree();
                }
            }

            // Add to mount FIRST so parent scale is available for compensation
            mount.AddChild(weaponModel);

            // Scale to desired WORLD size, compensating for body scale
            CharacterMeshBuilder.ScaleWeaponToWorldSize(weaponModel, 0.6f);
            float mountScale = CharacterMeshBuilder.GetMountScale(mountType);
            weaponModel.Scale *= mountScale;

            _weaponVisual = weaponModel;
        }

        private void RemoveWeaponVisual()
        {
            if (_weaponVisual != null && GodotObject.IsInstanceValid(_weaponVisual))
            {
                if (_weaponVisual.IsInsideTree())
                    _weaponVisual.GetParent()?.RemoveChild(_weaponVisual);
                _weaponVisual.QueueFree();
            }
            _weaponVisual = null;
        }

        private static Marker3D FindWeaponMount(Node root)
        {
            if (root is Marker3D m && m.Name == "WeaponMount") return m;
            foreach (var child in root.GetChildren())
            {
                if (child is Node node)
                {
                    var found = FindWeaponMount(node);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static Node3D FindPivotRecursive(Node parent, string name)
        {
            if (parent == null) return null;
            foreach (var child in parent.GetChildren())
            {
                if (child is Node3D n3d && n3d.Name.ToString() == name)
                    return n3d;
                var found = FindPivotRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void AttachAoEWeapon(WeaponType type)
        {
            if (_bladeRingActive) RemoveAoEWeapon();
            _aoeWeaponType = type;
            _bladeRingVisual = type switch
            {
                WeaponType.FlailChain => CharacterMeshBuilder.BuildFlailChain(),
                WeaponType.ShockCoil => CharacterMeshBuilder.BuildShockCoil(),
                WeaponType.FlameThrower => CharacterMeshBuilder.BuildFlameThrower(),
                _ => CharacterMeshBuilder.BuildBladeRing()
            };
            _player.AddChild(_bladeRingVisual);
            _bladeRingActive = true;
            _bladeRingTickTimer = 0f;
        }

        private void RemoveAoEWeapon()
        {
            if (!_bladeRingActive) return;
            _bladeRingVisual?.QueueFree();
            _bladeRingVisual = null;
            _bladeRingActive = false;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Safety: re-subscribe if equipment event was lost (e.g. scene transition)
            if (_player?.Inventory != null && !_equipSubscribed)
            {
                _player.Inventory.OnEquipmentChanged -= OnEquipmentChanged;
                _player.Inventory.OnEquipmentChanged += OnEquipmentChanged;
                _equipSubscribed = true;
            }

            if (_basicAttackCooldown > 0)
                _basicAttackCooldown -= dt;

            // Pistol reload timer
            if (_isReloading)
            {
                _reloadTimer -= dt;
                if (_reloadTimer <= 0f)
                {
                    _isReloading = false;
                    _pistolAmmo = PISTOL_MAG_SIZE;
                    GD.Print("[PlayerCombat] Pistol reloaded");
                }
            }

            float cdr = _playerStats.GetStat(StatType.CooldownReduction);
            for (int i = 0; i < _abilitySlots.Length; i++)
                _abilitySlots[i].TickCooldown(dt, cdr);

            // AoE weapon spin + damage
            if (_bladeRingActive)
            {
                if (GodotObject.IsInstanceValid(_bladeRingVisual))
                {
                    if (_aoeWeaponType == WeaponType.BladeRing || _aoeWeaponType == WeaponType.FlailChain)
                        _bladeRingVisual.RotateY(BLADE_RING_SPIN_SPEED * dt);
                }

                _bladeRingTickTimer -= dt;
                if (_bladeRingTickTimer <= 0f)
                {
                    _bladeRingTickTimer = _aoeWeaponType switch
                    {
                        WeaponType.ShockCoil => 0.3f,    // faster ticks, lower damage
                        WeaponType.FlameThrower => 0.25f, // fast ticks
                        _ => BLADE_RING_TICK_RATE         // 0.5f
                    };
                    AoEWeaponDamageTick();
                }
            }
        }

        private void AoEWeaponDamageTick()
        {
            if (!_player.IsInsideTree()) return;

            float radius = _aoeWeaponType switch
            {
                WeaponType.FlailChain => 5f,
                WeaponType.ShockCoil => 6f,
                WeaponType.FlameThrower => 8f,
                _ => BLADE_RING_RADIUS // 7f
            };
            float dmgMult = _aoeWeaponType switch
            {
                WeaponType.ShockCoil => 0.3f,
                WeaponType.FlameThrower => 0.35f,
                WeaponType.FlailChain => 0.7f,
                _ => BLADE_RING_DAMAGE_MULT // 0.5f
            };
            DamageType dmgType = _aoeWeaponType switch
            {
                WeaponType.ShockCoil => DamageType.Lightning,
                WeaponType.FlameThrower => DamageType.Fire,
                _ => DamageType.Physical
            };

            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = radius };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition + Vector3.Up * 0.5f),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                if (collider is not Node3D node3d) continue;

                // FlailChain: only hits enemies in front arc (180 degrees)
                if (_aoeWeaponType == WeaponType.FlailChain)
                {
                    var toEnemy = (node3d.GlobalPosition - _player.GlobalPosition).Flat().Normalized();
                    var facing = -_player.GlobalTransform.Basis.Z.Flat().Normalized();
                    if (toEnemy.Dot(facing) < -0.2f) continue;
                }

                // FlameThrower: only hits in a forward cone (60 degrees)
                if (_aoeWeaponType == WeaponType.FlameThrower)
                {
                    var toEnemy = (node3d.GlobalPosition - _player.GlobalPosition).Flat().Normalized();
                    var facing = -_player.GlobalTransform.Basis.Z.Flat().Normalized();
                    if (toEnemy.Dot(facing) < 0.5f) continue; // ~60 degree cone
                }

                var health = FindDamageable(collider);
                if (health == null || !health.IsAlive) continue;

                var hitPoint = node3d.GlobalPosition + Vector3.Up * 0.5f;
                var damage = DamageCalculator.CalculateBasicAttack(
                    _playerStats.Stats, _player, node3d, hitPoint, Team.Player);
                damage.FinalDamage *= dmgMult;
                damage.DamageType = dmgType;

                if (ServiceLocator.TryGet<CombatManager>(out var combat))
                    damage.FinalDamage *= combat.ComboDamageMultiplier;

                health.TakeDamage(damage);
            }
        }

        public void HandleBasicAttack()
        {
            if (!_player.IsInsideTree()) return;
            if (_basicAttackCooldown > 0) return;

            // Determine weapon type from equipped item
            ItemInstance equipped = null;
            _player.Inventory?.Equipped.TryGetValue(EquipmentSlot.MainHand, out equipped);
            var equipData = equipped?.BaseData as EquipmentData;
            var weaponType = equipData?.WeaponType ?? WeaponType.Pistol;

            // Pistol: check magazine
            if (weaponType == WeaponType.Pistol)
            {
                if (_isReloading) return;
                if (_pistolAmmo <= 0)
                {
                    _isReloading = true;
                    _reloadTimer = PISTOL_RELOAD_TIME;
                    GD.Print("[PlayerCombat] Pistol reloading...");
                    if (ServiceLocator.TryGet<AudioManager>(out var reloadAudio))
                        reloadAudio.PlayRandomSFXByName("reload");
                    return;
                }
            }

            // Face toward cursor before attacking
            FaceTowardCursor();

            // Trigger attack animation
            _animatable ??= _player.Animatable;
            _animatable?.SetState(AnimState.Attack);

            // Play weapon-specific sound (with random variation)
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
            {
                string sfx = weaponType switch
                {
                    WeaponType.Rifle => "rifle",
                    WeaponType.Shotgun => "shotgun_blast",
                    WeaponType.Launcher => "launcher_fire",
                    _ => "projectile"
                };
                audio.PlayRandomSFXByName(sfx);
            }

            // Weapon-specific fire rate
            float attackSpeed = _playerStats.GetStat(StatType.AttackSpeed);
            float baseRate = weaponType switch
            {
                WeaponType.Rifle => 0.5f,
                WeaponType.Shotgun => 0.8f,
                WeaponType.Launcher => 1.5f,
                WeaponType.Repeater => 0.5f,
                _ => 0.1f // Pistol — fast semi-auto
            };
            float rate = baseRate / Mathf.Max(0.1f, 1f + attackSpeed);
            _basicAttackCooldown = rate;

            // Weapon-specific parameters
            float aimTolerance = weaponType switch
            {
                WeaponType.Rifle => 3f,
                WeaponType.Shotgun => 5f,
                WeaponType.Repeater => 4.5f,
                _ => 3.5f // Pistol default
            };
            float shotRange = weaponType switch
            {
                WeaponType.Rifle => 40f,
                WeaponType.Shotgun => 12f,
                WeaponType.Repeater => 25f,
                _ => BASIC_SHOT_RANGE // 30m for Pistol
            };
            float damageMult = weaponType switch
            {
                WeaponType.Rifle => 1.5f,
                WeaponType.Repeater => 0.5f,
                WeaponType.Shotgun => 0.4f, // per pellet
                _ => 1f
            };
            float tracerWidth = weaponType switch
            {
                WeaponType.Rifle => 0.1f,
                WeaponType.Repeater => 0.02f,
                _ => 0.03f
            };

            // Aim toward cursor
            var cursorPos = GetCursorWorldPosition();
            var aimDir = (cursorPos - _player.GlobalPosition).Flat().Normalized();
            if (aimDir.LengthSquared() < 0.001f)
                aimDir = -_player.GlobalTransform.Basis.Z;

            // Use WeaponMount position if available (FBX models), otherwise computed offset
            Vector3 muzzlePos;
            if (_weaponMount != null && GodotObject.IsInstanceValid(_weaponMount))
            {
                muzzlePos = _weaponMount.GlobalPosition;
            }
            else
            {
                var sideOffset = _player.GlobalTransform.Basis.X * _fireSide;
                muzzlePos = _player.GlobalPosition + Vector3.Up * _fireHeight + sideOffset + aimDir * 0.5f;
            }

            // --- Launcher: fire a projectile instead of hitscan ---
            if (weaponType == WeaponType.Launcher)
            {
                var hitPoint = _player.GlobalPosition + aimDir * shotRange;
                var damageInfo = DamageCalculator.CalculateBasicAttack(
                    _playerStats.Stats, _player, _player, hitPoint, Team.Player);

                // Apply perk modifiers
                var perkProc = _player.PerkProcessor;
                if (perkProc != null)
                {
                    damageInfo.FinalDamage *= perkProc.TryPoweredStrike();
                    damageInfo.FinalDamage = perkProc.ModifyOutgoingDamage(damageInfo.FinalDamage, false);
                }
                if (ServiceLocator.TryGet<CombatManager>(out var combatMgr))
                    damageInfo.FinalDamage *= combatMgr.ComboDamageMultiplier;

                var proj = new Projectile();
                _player.GetTree().Root.AddChild(proj);
                proj.GlobalPosition = muzzlePos;
                proj.Initialize(aimDir, 10f, 20f, damageInfo, Team.Player, DamageType.Fire);
                proj.SetAoE(4f, 0.5f);
                proj.Scale = Vector3.One * 1.5f; // Visually larger projectile

                // Heavy screen shake
                if (ServiceLocator.TryGet<IsometricCamera>(out var launcherCam))
                    launcherCam.Shake(0.25f);

                // Class-specific muzzle flash VFX
                var className = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
                SpawnMuzzleFlash(className);
                return;
            }

            // --- Shotgun: multiple pellet hitscan ---
            if (weaponType == WeaponType.Shotgun)
            {
                // Heavy screen shake
                if (ServiceLocator.TryGet<IsometricCamera>(out var shotgunCam))
                    shotgunCam.Shake(0.2f);

                for (int pellet = 0; pellet < 5; pellet++)
                {
                    float spreadAngle = (GD.Randf() - 0.5f) * 2f * Mathf.DegToRad(15f);
                    var pelletDir = aimDir.Rotated(Vector3.Up, spreadAngle);

                    var pelletTarget = FindHitscanTarget(pelletDir, shotRange, aimTolerance);
                    Vector3 pelletEnd = muzzlePos + pelletDir * shotRange;

                    if (pelletTarget != null)
                    {
                        var hitPoint = pelletTarget.GlobalPosition + Vector3.Up * 0.8f;
                        pelletEnd = hitPoint;

                        var health = FindDamageable(pelletTarget);
                        if (health != null && health.IsAlive)
                        {
                            var damage = DamageCalculator.CalculateBasicAttack(
                                _playerStats.Stats, _player, pelletTarget, hitPoint, Team.Player);
                            damage.FinalDamage *= damageMult;

                            ApplyBasicAttackPerks(ref damage, pelletTarget);

                            health.TakeDamage(damage);

                            _player.PerkProcessor?.TryVampiricLifesteal(damage.FinalDamage);

                            // Impact VFX
                            var impact = VfxFactory.CreateImpactBurst(
                                Projectile.GetDamageTypeColor(DamageType.Physical));
                            _player.GetTree().Root.AddChild(impact);
                            impact.GlobalPosition = hitPoint;
                        }
                    }

                }

                // Shotgun cone VFX instead of individual tracers
                SpawnShotgunCone(muzzlePos, aimDir, shotRange);

                // Class-specific muzzle flash VFX
                var sgClassName = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
                SpawnMuzzleFlash(sgClassName);
                return;
            }

            // --- Repeater: 3-round burst ---
            if (weaponType == WeaponType.Repeater)
            {
                FireRepeaterBurst(aimDir, muzzlePos, shotRange, aimTolerance, damageMult, tracerWidth);
                var rpClassName = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
                SpawnMuzzleFlash(rpClassName);
                return;
            }

            // --- Single-shot hitscan: Pistol, Rifle, Repeater ---
            var bestTarget = FindHitscanTarget(aimDir, shotRange, aimTolerance);
            Vector3 tracerEnd = muzzlePos + aimDir * shotRange;

            if (bestTarget != null)
            {
                var hitPoint = bestTarget.GlobalPosition + Vector3.Up * 0.8f;
                tracerEnd = hitPoint;

                var health = FindDamageable(bestTarget);
                if (health != null && health.IsAlive)
                {
                    var damage = DamageCalculator.CalculateBasicAttack(
                        _playerStats.Stats, _player, bestTarget, hitPoint, Team.Player);
                    damage.FinalDamage *= damageMult;

                    ApplyBasicAttackPerks(ref damage, bestTarget);

                    health.TakeDamage(damage);

                    // Vampiric Core: lifesteal
                    _player.PerkProcessor?.TryVampiricLifesteal(damage.FinalDamage);

                    // Mythic: Storm Caller — chain to 3 extra targets
                    if (bestTarget is Node3D basicHitNode)
                        _player.PerkProcessor?.TryStormCallerChain(basicHitNode, damage.FinalDamage, damage.DamageType);

                    // Mythic: Void Heart — spawn void rift
                    _player.PerkProcessor?.TryVoidHeartRift(hitPoint, damage.FinalDamage);

                    // Apply stun from Impact Driver
                    if (damage.StunDuration > 0f && bestTarget is IKnockbackable kb)
                        kb.ApplyStun(damage.StunDuration);

                    // Impact VFX at hit point
                    var impact = VfxFactory.CreateImpactBurst(
                        Projectile.GetDamageTypeColor(DamageType.Physical));
                    _player.GetTree().Root.AddChild(impact);
                    impact.GlobalPosition = hitPoint;

                    GD.Print($"[PlayerCombat] {weaponType} hit for {damage.FinalDamage:F1}" +
                        (damage.IsCritical ? " CRIT!" : ""));
                }
            }

            // Bullet tracer from muzzle to hit/max range
            SpawnBulletTracer(muzzlePos, tracerEnd, tracerWidth);

            // Class-specific muzzle flash VFX
            var className2 = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
            SpawnMuzzleFlash(className2);

            // Rifle: screen shake on fire
            if (weaponType == WeaponType.Rifle)
            {
                if (ServiceLocator.TryGet<IsometricCamera>(out var rifleCam))
                    rifleCam.Shake(0.12f);
            }

            // Pistol ammo tracking
            if (weaponType == WeaponType.Pistol)
            {
                _pistolAmmo--;
                if (_pistolAmmo <= 0)
                {
                    _isReloading = true;
                    _reloadTimer = PISTOL_RELOAD_TIME;
                    GD.Print("[PlayerCombat] Pistol magazine empty, reloading...");
                }
            }
        }

        private void SpawnShotgunCone(Vector3 origin, Vector3 direction, float range)
        {
            // Create a cone mesh oriented along the aim direction
            var cone = new MeshInstance3D();
            float coneLength = range * 0.8f;
            float coneEndRadius = coneLength * Mathf.Tan(Mathf.DegToRad(15f));
            var coneMesh = new CylinderMesh
            {
                TopRadius = 0f,
                BottomRadius = coneEndRadius,
                Height = coneLength,
                RadialSegments = 12
            };
            cone.Mesh = coneMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(1f, 0.9f, 0.5f, 0.3f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(1f, 0.8f, 0.3f);
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            cone.MaterialOverride = mat;

            _player.GetTree().Root.AddChild(cone);

            // Position at origin, pointing along direction
            // CylinderMesh points along Y axis, we need to rotate it to point along direction
            cone.GlobalPosition = origin + direction * (coneLength / 2f);
            // Look in the aim direction, then rotate so the cylinder's Y axis aligns
            var up = Mathf.Abs(direction.Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
            cone.LookAt(cone.GlobalPosition + direction, up);
            cone.RotateObjectLocal(Vector3.Right, Mathf.DegToRad(90f));

            FadeAndFree(cone, 0.12f);
        }

        private void FireRepeaterBurst(Vector3 aimDir, Vector3 muzzlePos, float range, float tolerance, float damageMult, float tracerWidth)
        {
            // Fire 3 rounds with 0.08s between each
            for (int burst = 0; burst < 3; burst++)
            {
                float delay = burst * 0.08f;
                var capturedDir = aimDir;
                var capturedMuzzle = muzzlePos;

                if (burst == 0)
                {
                    FireSingleHitscan(capturedDir, capturedMuzzle, range, tolerance, damageMult, tracerWidth);
                }
                else
                {
                    _player.GetTree().CreateTimer(delay).Timeout += () =>
                    {
                        if (!_player.IsInsideTree()) return;
                        // Slight spread on follow-up shots
                        float spread = (GD.Randf() - 0.5f) * Mathf.DegToRad(5f);
                        var burstDir = capturedDir.Rotated(Vector3.Up, spread);
                        FireSingleHitscan(burstDir, capturedMuzzle, range, tolerance, damageMult, tracerWidth);

                        if (ServiceLocator.TryGet<AudioManager>(out var burstAudio))
                            burstAudio.PlayRandomSFXByName("projectile");
                    };
                }
            }
        }

        private void FireSingleHitscan(Vector3 aimDir, Vector3 muzzlePos, float range, float tolerance, float damageMult, float tracerWidth)
        {
            var target = FindHitscanTarget(aimDir, range, tolerance);
            Vector3 tracerEnd = muzzlePos + aimDir * range;

            if (target != null)
            {
                var hitPoint = target.GlobalPosition + Vector3.Up * 0.8f;
                tracerEnd = hitPoint;

                var health = FindDamageable(target);
                if (health != null && health.IsAlive)
                {
                    var damage = DamageCalculator.CalculateBasicAttack(
                        _playerStats.Stats, _player, target, hitPoint, Team.Player);
                    damage.FinalDamage *= damageMult;

                    ApplyBasicAttackPerks(ref damage, target);
                    health.TakeDamage(damage);
                    _player.PerkProcessor?.TryVampiricLifesteal(damage.FinalDamage);

                    var impact = VfxFactory.CreateImpactBurst(
                        Projectile.GetDamageTypeColor(DamageType.Physical));
                    _player.GetTree().Root.AddChild(impact);
                    impact.GlobalPosition = hitPoint;
                }
            }

            SpawnBulletTracer(muzzlePos, tracerEnd, tracerWidth);
        }

        /// <summary>
        /// Find the best hitscan target along a direction within range and aim tolerance.
        /// </summary>
        private Node3D FindHitscanTarget(Vector3 aimDir, float range, float tolerance)
        {
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = range };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition + Vector3.Up * _fireHeight),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            float bestDist = float.MaxValue;
            Node3D bestTarget = null;
            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                if (collider is not Node3D node3d) continue;

                var toEnemy = (node3d.GlobalPosition - _player.GlobalPosition).Flat();
                float along = toEnemy.Dot(aimDir);
                if (along <= 0) continue; // Behind player

                float perpDist = (toEnemy - aimDir * along).Length();
                if (perpDist > tolerance) continue;

                // LOS check — skip targets behind walls
                if (!HasLineOfSight(node3d)) continue;

                if (perpDist < bestDist)
                {
                    bestDist = perpDist;
                    bestTarget = node3d;
                }
            }
            return bestTarget;
        }

        /// <summary>
        /// Apply perk and combo modifiers common to all basic attack hitscan hits.
        /// </summary>
        private void ApplyBasicAttackPerks(ref DamageInfo damage, Node3D target)
        {
            var perkProc = _player.PerkProcessor;
            if (perkProc != null)
            {
                damage.FinalDamage *= perkProc.TryPoweredStrike();
                damage.FinalDamage = perkProc.ModifyOutgoingDamage(damage.FinalDamage, false);
                damage.FinalDamage *= perkProc.GetExploitWeaknessMultiplier(target);

                // Impact Driver: 20% chance to stagger
                float stunTime = perkProc.TryImpactDriver();
                if (stunTime > 0f)
                    damage.StunDuration = stunTime;
            }

            if (ServiceLocator.TryGet<CombatManager>(out var combat))
                damage.FinalDamage *= combat.ComboDamageMultiplier;
        }

        public void HandleAbilityInput(int slotIndex)
        {
            if (!_player.IsInsideTree()) return;
            if (slotIndex < 0 || slotIndex >= _abilitySlots.Length) return;

            var slot = _abilitySlots[slotIndex];
            if (slot.IsEmpty)
            {
                GD.Print($"[PlayerCombat] Ability slot {slotIndex} is empty");
                return;
            }
            if (!slot.IsReady)
            {
                GD.Print($"[PlayerCombat] Ability '{slot.Data.AbilityName}' on cooldown ({slot.CooldownRemaining:F1}s)");
                return;
            }

            // Check mana (modified by perks)
            float manaCost = slot.Data.ManaCost;
            var perkMana = _player.PerkProcessor;
            if (perkMana != null)
                manaCost *= perkMana.GetAbilityManaCostMultiplier();

            // Mythic: Blood Economy — pay HP instead of mana
            if (perkMana != null && perkMana.ShouldUseBloodEconomy())
            {
                if (!perkMana.SpendHealthForMana(manaCost))
                {
                    GD.Print("[PlayerCombat] Not enough HP for Blood Economy");
                    return;
                }
            }
            else if (!_playerStats.SpendMana(manaCost))
            {
                GD.Print("[PlayerCombat] Not enough mana");
                return;
            }

            // Face toward cursor before using ability
            FaceTowardCursor();

            slot.StartCooldown();

            _animatable ??= _player.Animatable;
            _animatable?.SetState(AnimState.Attack);

            // Singularity Core: pull enemies on ability cast
            perkMana?.TrySingularityPull();

            // Amplifier Core: ability level bonus multiplier
            float amplifierMult = perkMana?.GetAmplifierBonus(slot.Data.Id) ?? 1f;

            // Projectile abilities: spawn a traveling projectile
            if (slot.Data.Type == AbilityType.Projectile)
            {
                SpawnProjectile(slot.Data, amplifierMult);
                GD.Print($"[PlayerCombat] Fired projectile: {slot.Data.AbilityName}");
                return;
            }

            // Play sound (with random variation)
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlayRandomSFXByName("swing");

            // Find targets via sphere overlap (works for melee and AoE)
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            float range = slot.Data.Range;
            var shape = new SphereShape3D { Radius = range };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition + Vector3.Up * _fireHeight),
                CollisionMask = Constants.MASK_ENEMY
            };

            var results = spaceState.IntersectShape(queryParams);

            var perkAbility = _player.PerkProcessor;

            if (slot.Data.AoERadius > 0)
            {
                // AoE ground ring indicator
                var aoeColor = Projectile.GetDamageTypeColor(slot.Data.DamageType);
                var aoeRing = VfxFactory.CreateAoEIndicator(aoeColor, slot.Data.AoERadius);
                _player.GetTree().Root.AddChild(aoeRing);
                aoeRing.GlobalPosition = _player.GlobalPosition;

                // Per-ability AoE VFX
                SpawnAbilityVfx(slot.Data);

                // AoE: hit all enemies in range (with LOS check)
                foreach (var result in results)
                {
                    var collider = (Node)result["collider"];
                    if (collider is Node3D node3d)
                    {
                        if (!HasLineOfSight(node3d)) continue;
                        var health = FindDamageable(collider);
                        if (health != null && health.IsAlive)
                        {
                            var damage = DamageCalculator.CalculateAbilityDamage(
                                slot.Data, _playerStats.Stats, _player, collider, node3d.GlobalPosition, Team.Player);
                            damage.FinalDamage *= amplifierMult;
                            if (perkAbility != null)
                            {
                                damage.FinalDamage = perkAbility.ModifyOutgoingDamage(damage.FinalDamage, true);
                                damage.FinalDamage *= perkAbility.GetExploitWeaknessMultiplier(collider);
                            }
                            health.TakeDamage(damage);
                            perkAbility?.TryVampiricLifesteal(damage.FinalDamage);
                            TryResonanceOnHit(collider as Node3D);

                            // Mythic: Storm Caller + Void Heart
                            perkAbility?.TryStormCallerChain(node3d, damage.FinalDamage, damage.DamageType);
                            perkAbility?.TryVoidHeartRift(node3d.GlobalPosition, damage.FinalDamage);
                        }
                    }
                }
            }
            else
            {
                // Single target: hit nearest enemy in cursor direction (with LOS check)
                float closestDist = float.MaxValue;
                Node closestEnemy = null;

                foreach (var result in results)
                {
                    var collider = (Node)result["collider"];
                    if (collider is Node3D node3d)
                    {
                        if (!HasLineOfSight(node3d)) continue;
                        float dist = _player.GlobalPosition.FlatDistance(node3d.GlobalPosition);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestEnemy = collider;
                        }
                    }
                }

                if (closestEnemy != null)
                {
                    var hitPoint = closestEnemy is Node3D n ? n.GlobalPosition : _player.GlobalPosition;

                    // Melee VFX
                    if (slot.Data.Type == AbilityType.Melee)
                    {
                        var slashDir = (hitPoint - _player.GlobalPosition).Flat().Normalized();
                        var slashColor = Projectile.GetDamageTypeColor(slot.Data.DamageType);
                        var slash = VfxFactory.CreateMeleeSlashArc(slashColor, slashDir);
                        _player.GetTree().Root.AddChild(slash);
                        slash.GlobalPosition = _player.GlobalPosition;

                        // Per-ability melee VFX
                        SpawnAbilityVfx(slot.Data, hitPoint);
                    }

                    var health = FindDamageable(closestEnemy);
                    if (health != null && health.IsAlive)
                    {
                        var damage = DamageCalculator.CalculateAbilityDamage(
                            slot.Data, _playerStats.Stats, _player, closestEnemy, hitPoint, Team.Player);
                        damage.FinalDamage *= amplifierMult;
                        if (perkAbility != null)
                        {
                            damage.FinalDamage = perkAbility.ModifyOutgoingDamage(damage.FinalDamage, true);
                            damage.FinalDamage *= perkAbility.GetExploitWeaknessMultiplier(closestEnemy);
                        }
                        health.TakeDamage(damage);
                        perkAbility?.TryVampiricLifesteal(damage.FinalDamage);
                        TryResonanceOnHit(closestEnemy as Node3D);

                        // Mythic: Storm Caller + Void Heart
                        if (closestEnemy is Node3D stNode)
                        {
                            perkAbility?.TryStormCallerChain(stNode, damage.FinalDamage, damage.DamageType);
                            perkAbility?.TryVoidHeartRift(stNode.GlobalPosition, damage.FinalDamage);
                        }

                        // Chain Lightning: arc to 1 additional nearby target at 50% damage
                        if (closestEnemy is Node3D hitNode)
                            TryChainLightning(hitNode, slot.Data, damage.FinalDamage);
                    }
                }
            }

            GD.Print($"[PlayerCombat] Used ability: {slot.Data.AbilityName}");

            // Mythic: Echo Chamber — fire ability a second time at 60% damage
            if (perkAbility != null && perkAbility.ShouldEchoChamber())
            {
                _player.GetTree().CreateTimer(0.15f).Timeout += () =>
                {
                    if (!_player.IsInsideTree()) return;
                    FireEchoAbility(slot.Data, amplifierMult * PerkProcessor.EchoChamberDamageMult);
                };
            }
        }

        /// <summary>
        /// Echo Chamber: fire a second copy of the ability at reduced damage.
        /// Simplified version — no mana cost, no cooldown reset.
        /// </summary>
        private void FireEchoAbility(AbilityData abilityData, float damageMult)
        {
            if (abilityData.Type == AbilityType.Projectile)
            {
                SpawnProjectile(abilityData, damageMult);
                return;
            }

            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = abilityData.Range };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition + Vector3.Up * _fireHeight),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                if (collider is not Node3D node3d) continue;
                var health = FindDamageable(collider);
                if (health == null || !health.IsAlive) continue;

                var damage = DamageCalculator.CalculateAbilityDamage(
                    abilityData, _playerStats.Stats, _player, collider, node3d.GlobalPosition, Team.Player);
                damage.FinalDamage *= damageMult;
                health.TakeDamage(damage);

                // Only hit first target for single-target abilities
                if (abilityData.AoERadius <= 0) break;
            }

            // Echo VFX
            var echo = VfxFactory.CreateImpactBurst(new Color(0.5f, 0.8f, 1f));
            _player.GetTree().Root.AddChild(echo);
            echo.GlobalPosition = _player.GlobalPosition + Vector3.Up;
        }

        private void SpawnProjectile(AbilityData ability, float amplifierMult = 1f)
        {
            // Aim toward cursor position (player already facing cursor from HandleAbilityInput)
            var cursorPos = GetCursorWorldPosition();
            var aimDir = (cursorPos - _player.GlobalPosition).Flat().Normalized();

            // Fallback if cursor is directly on top of player
            if (aimDir.LengthSquared() < 0.001f)
                aimDir = -_player.GlobalTransform.Basis.Z;

            // Fire burst rounds (BurstCount defaults to 1 for non-burst abilities)
            for (int i = 0; i < ability.BurstCount; i++)
            {
                if (i == 0)
                {
                    SpawnSingleAbilityProjectile(ability, aimDir, amplifierMult);
                }
                else
                {
                    float delay = i * ability.BurstDelay;
                    var capturedDir = aimDir;
                    float spreadDeg = ability.BurstSpread;
                    _player.GetTree().CreateTimer(delay).Timeout += () =>
                    {
                        if (!_player.IsInsideTree()) return;
                        float spread = (GD.Randf() - 0.5f) * Mathf.DegToRad(spreadDeg);
                        var burstDir = capturedDir.Rotated(Vector3.Up, spread);
                        SpawnSingleAbilityProjectile(ability, burstDir, amplifierMult);
                    };
                }
            }

            // Arcane circle at feet for SparkPlug
            var className = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
            if (className == BotFrameType.SparkPlug)
            {
                var circle = VfxFactory.CreateArcaneCircle(new Color(0.5f, 0.3f, 1f));
                _player.GetTree().Root.AddChild(circle);
                circle.GlobalPosition = _player.GlobalPosition;
            }

            // Per-ability projectile VFX (cast-time effects at player position)
            SpawnAbilityVfx(ability);
        }

        private void SpawnSingleAbilityProjectile(AbilityData ability, Vector3 aimDir, float amplifierMult)
        {
            var hitPoint = _player.GlobalPosition + aimDir * ability.Range;
            var damageInfo = DamageCalculator.CalculateAbilityDamage(
                ability, _playerStats.Stats, _player, _player, hitPoint, Team.Player);
            damageInfo.FinalDamage *= amplifierMult;

            var proj = new Projectile();
            _player.GetTree().Root.AddChild(proj);
            proj.GlobalPosition = _player.GlobalPosition + Vector3.Up * _fireHeight + _player.GlobalTransform.Basis.X * _fireSide + aimDir * 0.5f;
            proj.Initialize(aimDir, 15f, ability.Range, damageInfo, Team.Player, ability.DamageType);

            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlayRandomSFXByName("projectile");
        }

        /// <summary>
        /// Spawn per-ability visual effects at the player position or hit point.
        /// </summary>
        private void SpawnAbilityVfx(AbilityData ability, Vector3? hitPoint = null)
        {
            var root = _player.GetTree().Root;
            var playerPos = _player.GlobalPosition;
            var target = hitPoint ?? playerPos;

            switch (ability.Id)
            {
                // --- Tin Can ---
                case "ability_burst_fire":
                    // Muzzle sparks
                    var muzzleSparks = VfxFactory.CreateMuzzleFlash(new Color(1f, 0.8f, 0.3f));
                    root.AddChild(muzzleSparks);
                    muzzleSparks.GlobalPosition = playerPos + Vector3.Up * _fireHeight + (-_player.GlobalTransform.Basis.Z * _fireForward);
                    break;

                case "ability_strike":
                    // Ground sparks on impact
                    var strikeSparks = VfxFactory.CreateGroundSparks(new Color(1f, 0.9f, 0.5f), 15);
                    root.AddChild(strikeSparks);
                    strikeSparks.GlobalPosition = target;
                    break;

                case "ability_shield_bash":
                    // Shockwave ring at impact + stun indicator
                    var bashWave = VfxFactory.CreateShockwaveRing(new Color(0.6f, 0.8f, 1f));
                    root.AddChild(bashWave);
                    bashWave.GlobalPosition = target;
                    var stunVfx = VfxFactory.CreateStunIndicator();
                    root.AddChild(stunVfx);
                    stunVfx.GlobalPosition = target + Vector3.Up * 1.5f;
                    break;

                case "ability_whirlwind":
                    // Spinning shockwave + ground sparks
                    var whirlWave = VfxFactory.CreateShockwaveRing(new Color(0.8f, 0.8f, 0.8f));
                    root.AddChild(whirlWave);
                    whirlWave.GlobalPosition = playerPos;
                    var whirlSparks = VfxFactory.CreateGroundSparks(new Color(0.7f, 0.7f, 0.7f), 25);
                    root.AddChild(whirlSparks);
                    whirlSparks.GlobalPosition = playerPos;
                    break;

                // --- Scrapheap ---
                case "ability_cannon_blast":
                    // Heavy muzzle flash + impact burst
                    var cannonFlash = VfxFactory.CreateMuzzleFlash(new Color(1f, 0.5f, 0.1f));
                    root.AddChild(cannonFlash);
                    cannonFlash.GlobalPosition = playerPos + Vector3.Up * _fireHeight + (-_player.GlobalTransform.Basis.Z * _fireForward);
                    break;

                case "ability_slam":
                    // Ground shockwave + heavy sparks
                    var slamWave = VfxFactory.CreateShockwaveRing(new Color(1f, 0.6f, 0.2f));
                    root.AddChild(slamWave);
                    slamWave.GlobalPosition = playerPos;
                    var slamSparks = VfxFactory.CreateGroundSparks(new Color(1f, 0.5f, 0.1f), 30);
                    root.AddChild(slamSparks);
                    slamSparks.GlobalPosition = playerPos;
                    break;

                case "ability_feral_roar":
                    // Expanding aura ring + shockwave
                    var roarAura = VfxFactory.CreateAuraRing(new Color(1f, 0.3f, 0.1f), ability.AoERadius);
                    root.AddChild(roarAura);
                    roarAura.GlobalPosition = playerPos;
                    var roarWave = VfxFactory.CreateShockwaveRing(new Color(1f, 0.4f, 0.1f));
                    root.AddChild(roarWave);
                    roarWave.GlobalPosition = playerPos;
                    break;

                case "ability_earthquake":
                    // Big ground impact + shockwave + sparks
                    var quakeWave = VfxFactory.CreateShockwaveRing(new Color(0.8f, 0.5f, 0.2f));
                    root.AddChild(quakeWave);
                    quakeWave.GlobalPosition = playerPos;
                    var quakeSparks = VfxFactory.CreateGroundSparks(new Color(0.7f, 0.4f, 0.1f), 40);
                    root.AddChild(quakeSparks);
                    quakeSparks.GlobalPosition = playerPos;
                    break;

                // --- Spark Plug ---
                case "ability_arcane_bolt":
                    // Arcane circle at cast point
                    var arcCircle = VfxFactory.CreateArcaneCircle(new Color(0.4f, 0.4f, 1f));
                    root.AddChild(arcCircle);
                    arcCircle.GlobalPosition = playerPos;
                    var arcSparks = VfxFactory.CreateElectricSparks(new Color(0.6f, 0.6f, 1f));
                    root.AddChild(arcSparks);
                    arcSparks.GlobalPosition = playerPos + Vector3.Up * _fireHeight;
                    break;

                case "ability_frost_nova":
                    // Freeze burst + icy shockwave
                    var frostBurst = VfxFactory.CreateFreezeBurst(new Color(0.5f, 0.8f, 1f));
                    root.AddChild(frostBurst);
                    frostBurst.GlobalPosition = playerPos;
                    var frostWave = VfxFactory.CreateShockwaveRing(new Color(0.3f, 0.7f, 1f));
                    root.AddChild(frostWave);
                    frostWave.GlobalPosition = playerPos;
                    break;

                case "ability_meteor":
                    // Fire trail + impact burst at target
                    var meteorCircle = VfxFactory.CreateArcaneCircle(new Color(1f, 0.4f, 0.1f));
                    root.AddChild(meteorCircle);
                    meteorCircle.GlobalPosition = playerPos;
                    break;

                // --- Rust Bucket ---
                case "ability_snipe_shot":
                    // Precise muzzle flash
                    var snipeFlash = VfxFactory.CreateMuzzleFlash(new Color(0.3f, 1f, 0.3f));
                    root.AddChild(snipeFlash);
                    snipeFlash.GlobalPosition = playerPos + Vector3.Up * _fireHeight + (-_player.GlobalTransform.Basis.Z * _fireForward);
                    break;

                case "ability_backstab":
                    // Quick slash sparks
                    var stabSparks = VfxFactory.CreateGroundSparks(new Color(0.8f, 0.2f, 0.2f), 12);
                    root.AddChild(stabSparks);
                    stabSparks.GlobalPosition = target;
                    var stabImpact = VfxFactory.CreateImpactBurst(new Color(1f, 0.2f, 0.2f));
                    root.AddChild(stabImpact);
                    stabImpact.GlobalPosition = target;
                    break;

                case "ability_smoke_bomb":
                    // Poison-style cloud (gray smoke)
                    var smoke = VfxFactory.CreatePoisonCloud(new Color(0.5f, 0.5f, 0.5f), ability.AoERadius);
                    root.AddChild(smoke);
                    smoke.GlobalPosition = playerPos;
                    break;

                case "ability_assassinate":
                    // Dark impact + ground sparks
                    var assImpact = VfxFactory.CreateImpactBurst(new Color(0.6f, 0.1f, 0.1f));
                    root.AddChild(assImpact);
                    assImpact.GlobalPosition = target;
                    var assSparks = VfxFactory.CreateGroundSparks(new Color(0.8f, 0.1f, 0.1f), 20);
                    root.AddChild(assSparks);
                    assSparks.GlobalPosition = target;
                    var assWave = VfxFactory.CreateShockwaveRing(new Color(0.5f, 0.1f, 0.1f));
                    root.AddChild(assWave);
                    assWave.GlobalPosition = target;
                    break;

                // --- Noise Box ---
                case "ability_dark_chord":
                    // Music notes + dark aura
                    var notes = VfxFactory.CreateMusicNotes(new Color(0.5f, 0.2f, 0.8f));
                    root.AddChild(notes);
                    notes.GlobalPosition = playerPos + Vector3.Up;
                    var darkAura = VfxFactory.CreateAuraRing(new Color(0.4f, 0.1f, 0.6f), ability.AoERadius);
                    root.AddChild(darkAura);
                    darkAura.GlobalPosition = playerPos;
                    break;

                case "ability_raise_dead":
                    // Dark arcane circle + pillar of energy
                    var deathCircle = VfxFactory.CreateArcaneCircle(new Color(0.4f, 0.1f, 0.6f));
                    root.AddChild(deathCircle);
                    deathCircle.GlobalPosition = playerPos;
                    var deathSparks = VfxFactory.CreateGroundSparks(new Color(0.5f, 0.2f, 0.7f), 20);
                    root.AddChild(deathSparks);
                    deathSparks.GlobalPosition = playerPos;
                    break;

                case "ability_death_ballad":
                    // Music notes (dark) + shockwave + aura
                    var balladNotes = VfxFactory.CreateMusicNotes(new Color(0.3f, 0.1f, 0.5f));
                    root.AddChild(balladNotes);
                    balladNotes.GlobalPosition = playerPos + Vector3.Up;
                    var balladWave = VfxFactory.CreateShockwaveRing(new Color(0.4f, 0.1f, 0.6f));
                    root.AddChild(balladWave);
                    balladWave.GlobalPosition = playerPos;
                    break;

                // --- Clunker ---
                case "ability_rivet_burst":
                    // Rapid muzzle sparks
                    var rivetFlash = VfxFactory.CreateMuzzleFlash(new Color(1f, 0.7f, 0.2f));
                    root.AddChild(rivetFlash);
                    rivetFlash.GlobalPosition = playerPos + Vector3.Up * _fireHeight + (-_player.GlobalTransform.Basis.Z * _fireForward);
                    var rivetSparks = VfxFactory.CreateElectricSparks(new Color(1f, 0.6f, 0.2f));
                    root.AddChild(rivetSparks);
                    rivetSparks.GlobalPosition = playerPos + Vector3.Up * _fireHeight;
                    break;

                case "ability_flurry":
                    // Rapid ground sparks
                    var flurrySparks = VfxFactory.CreateGroundSparks(new Color(0.9f, 0.7f, 0.3f), 20);
                    root.AddChild(flurrySparks);
                    flurrySparks.GlobalPosition = target;
                    break;

                case "ability_uppercut":
                    // Upward shockwave + sparks
                    var upperWave = VfxFactory.CreateShockwaveRing(new Color(1f, 0.8f, 0.3f));
                    root.AddChild(upperWave);
                    upperWave.GlobalPosition = target;
                    var upperSparks = VfxFactory.CreateGroundSparks(new Color(1f, 0.7f, 0.2f), 15);
                    root.AddChild(upperSparks);
                    upperSparks.GlobalPosition = target;
                    break;

                case "ability_hundred_fists":
                    // Rapid impacts + shockwave
                    var fistsWave = VfxFactory.CreateShockwaveRing(new Color(1f, 0.6f, 0.1f));
                    root.AddChild(fistsWave);
                    fistsWave.GlobalPosition = playerPos;
                    var fistsSparks = VfxFactory.CreateGroundSparks(new Color(1f, 0.5f, 0.1f), 35);
                    root.AddChild(fistsSparks);
                    fistsSparks.GlobalPosition = playerPos;
                    break;
            }
        }

        public void SetAbility(int slotIndex, AbilityData ability)
        {
            if (slotIndex >= 0 && slotIndex < _abilitySlots.Length)
            {
                _abilitySlots[slotIndex] = new AbilitySlot(ability);
                GD.Print($"[PlayerCombat] Slot {slotIndex} set to '{ability.AbilityName}' (type={ability.Type}, mana={ability.ManaCost}) on {GetInstanceId()}");
            }
        }

        public AbilitySlot GetSlot(int index)
        {
            return (index >= 0 && index < _abilitySlots.Length) ? _abilitySlots[index] : null;
        }

        private void SpawnMuzzleFlash(BotFrameType className)
        {
            // Use WeaponMount position if available, otherwise computed offset
            Vector3 muzzlePos;
            if (_weaponMount != null && GodotObject.IsInstanceValid(_weaponMount))
            {
                muzzlePos = _weaponMount.GlobalPosition;
            }
            else
            {
                muzzlePos = _player.GlobalPosition + Vector3.Up * _fireHeight
                    + _player.GlobalTransform.Basis.X * _fireSide
                    + (-_player.GlobalTransform.Basis.Z * _fireForward);
            }

            switch (className)
            {
                case BotFrameType.Scrapheap:
                    SpawnHeavyCannonFlash(muzzlePos);
                    break;
                case BotFrameType.SparkPlug:
                    SpawnArcCasterFlash(muzzlePos);
                    break;
                case BotFrameType.RustBucket:
                    SpawnNeedlerFlash(muzzlePos);
                    break;
                case BotFrameType.NoiseBox:
                    SpawnPulseEmitterFlash(muzzlePos);
                    break;
                case BotFrameType.Clunker:
                    SpawnRivetGunFlash(muzzlePos);
                    break;
                default:
                    SpawnBlasterFlash(muzzlePos);
                    break;
            }
        }

        private void SpawnHeavyCannonFlash(Vector3 pos)
        {
            // Big orange-yellow flash for Scrapheap's heavy cannon
            var flash = CreateFlashMesh(new Vector3(0.5f, 0.5f, 0.3f),
                new Color(1f, 0.7f, 0.2f, 0.9f), new Color(1f, 0.6f, 0.1f));
            _player.GetTree().Root.AddChild(flash);
            flash.GlobalPosition = pos;
            FadeAndFree(flash, 0.1f);

            if (ServiceLocator.TryGet<IsometricCamera>(out var camera))
                camera.Shake(0.1f);
        }

        private void SpawnBlasterFlash(Vector3 pos)
        {
            // Standard white-blue flash for TinCan's blaster
            var flash = CreateFlashMesh(new Vector3(0.3f, 0.3f, 0.2f),
                new Color(0.8f, 0.9f, 1f, 0.85f), new Color(0.7f, 0.85f, 1f));
            _player.GetTree().Root.AddChild(flash);
            flash.GlobalPosition = pos;
            FadeAndFree(flash, 0.08f);
        }

        private void SpawnArcCasterFlash(Vector3 pos)
        {
            // Electric yellow-white flash for SparkPlug
            var flash = CreateFlashMesh(new Vector3(0.35f, 0.35f, 0.25f),
                new Color(1f, 1f, 0.5f, 0.9f), new Color(1f, 1f, 0.3f));
            _player.GetTree().Root.AddChild(flash);
            flash.GlobalPosition = pos;
            FadeAndFree(flash, 0.1f);

            // Small arc ring
            var ring = VfxFactory.CreateShockwaveRing(new Color(0.8f, 0.9f, 1f));
            _player.GetTree().Root.AddChild(ring);
            ring.GlobalPosition = pos;
            ring.Scale = Vector3.One * 0.3f;
        }

        private void SpawnNeedlerFlash(Vector3 pos)
        {
            // Small, fast green-white dual flash for RustBucket
            var flash1 = CreateFlashMesh(new Vector3(0.15f, 0.15f, 0.1f),
                new Color(0.6f, 1f, 0.7f, 0.8f), new Color(0.5f, 0.9f, 0.6f));
            _player.GetTree().Root.AddChild(flash1);
            flash1.GlobalPosition = pos;
            FadeAndFree(flash1, 0.05f);

            // Offset second flash
            var flash2 = CreateFlashMesh(new Vector3(0.12f, 0.12f, 0.08f),
                new Color(0.6f, 1f, 0.7f, 0.8f), new Color(0.5f, 0.9f, 0.6f));
            _player.GetTree().Root.AddChild(flash2);
            flash2.GlobalPosition = pos + new Vector3(0.1f, 0.05f, 0);
            flash2.Visible = false;
            _player.GetTree().CreateTimer(0.03f).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(flash2))
                {
                    flash2.Visible = true;
                    FadeAndFree(flash2, 0.05f);
                }
            };
        }

        private void SpawnPulseEmitterFlash(Vector3 pos)
        {
            // Purple expanding pulse ring for NoiseBox
            var ring = VfxFactory.CreateShockwaveRing(new Color(0.6f, 0.2f, 0.8f));
            _player.GetTree().Root.AddChild(ring);
            ring.GlobalPosition = pos;
            ring.Scale = Vector3.One * 0.4f;

            var flash = CreateFlashMesh(new Vector3(0.25f, 0.25f, 0.15f),
                new Color(0.7f, 0.3f, 1f, 0.85f), new Color(0.6f, 0.2f, 0.9f));
            _player.GetTree().Root.AddChild(flash);
            flash.GlobalPosition = pos;
            FadeAndFree(flash, 0.1f);
        }

        private void SpawnRivetGunFlash(Vector3 pos)
        {
            // Orange spark flash for Clunker
            var flash = CreateFlashMesh(new Vector3(0.2f, 0.2f, 0.15f),
                new Color(1f, 0.8f, 0.3f, 0.9f), new Color(1f, 0.7f, 0.2f));
            _player.GetTree().Root.AddChild(flash);
            flash.GlobalPosition = pos;
            FadeAndFree(flash, 0.06f);

            var sparks = VfxFactory.CreateHitParticles(new Color(1f, 0.8f, 0.3f));
            _player.GetTree().Root.AddChild(sparks);
            sparks.GlobalPosition = pos;
        }

        private void SpawnBulletTracer(Vector3 from, Vector3 to, float width = 0.03f)
        {
            var tracer = new MeshInstance3D();
            float length = from.DistanceTo(to);
            var tracerMesh = new BoxMesh { Size = new Vector3(width, width, length) };
            tracer.Mesh = tracerMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(1f, 0.95f, 0.7f, 0.8f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(1f, 0.9f, 0.5f);
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            tracer.MaterialOverride = mat;

            _player.GetTree().Root.AddChild(tracer);
            tracer.GlobalPosition = (from + to) / 2f;

            var dir = (to - from).Normalized();
            if (dir.LengthSquared() > 0.001f)
                tracer.LookAt(tracer.GlobalPosition + dir, Vector3.Up);

            FadeAndFree(tracer, 0.08f);
        }

        private MeshInstance3D CreateFlashMesh(Vector3 size, Color albedo, Color emission)
        {
            var mesh = new MeshInstance3D();
            var boxMesh = new BoxMesh { Size = size };
            mesh.Mesh = boxMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = albedo;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = emission;
            mat.EmissionEnergyMultiplier = 3f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            mesh.MaterialOverride = mat;

            return mesh;
        }

        private void FadeAndFree(MeshInstance3D mesh, float duration)
        {
            var mat = mesh.MaterialOverride as StandardMaterial3D;
            if (mat == null) { mesh.QueueFree(); return; }

            var tween = mesh.CreateTween();
            tween.TweenProperty(mat, "albedo_color:a", 0f, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(mesh.QueueFree));
        }

        /// <summary>
        /// Raycast from camera through mouse cursor to the ground plane.
        /// Returns the world position the cursor points at.
        /// </summary>
        private Vector3 GetCursorWorldPosition()
        {
            if (_camera == null || !GodotObject.IsInstanceValid(_camera))
                _camera = _player.GetViewport().GetCamera3D();
            if (_camera == null) return _player.GlobalPosition + -_player.GlobalTransform.Basis.Z * 3f;

            var mousePos = _player.GetViewport().GetMousePosition();
            var from = _camera.ProjectRayOrigin(mousePos);
            var dir = _camera.ProjectRayNormal(mousePos);

            // Intersect with ground plane (Y = 0)
            if (Mathf.Abs(dir.Y) > 0.001f)
            {
                float t = -from.Y / dir.Y;
                if (t > 0f)
                    return from + dir * t;
            }

            // Fallback: raycast against physics
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, from + dir * 100f, Constants.MASK_GROUND);
            var result = spaceState.IntersectRay(query);
            if (result.Count > 0)
                return (Vector3)result["position"];

            return _player.GlobalPosition + -_player.GlobalTransform.Basis.Z * 3f;
        }

        /// <summary>
        /// Face the player toward the mouse cursor position.
        /// </summary>
        private void FaceTowardCursor()
        {
            var cursorPos = GetCursorWorldPosition();
            var faceDir = (cursorPos - _player.GlobalPosition).Flat();
            if (faceDir.LengthSquared() > 0.01f)
                _player.LookAt(_player.GlobalPosition + faceDir.Normalized(), Vector3.Up);
        }

        /// <summary>
        /// Chain Lightning perk: arc to 1 additional nearby target at 50% damage.
        /// </summary>
        private void TryChainLightning(Node3D hitTarget, AbilityData ability, float baseDamage)
        {
            var perk = _player.PerkProcessor;
            if (perk == null || !perk.HasChainLightning()) return;

            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 8f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, hitTarget.GlobalPosition),
                CollisionMask = Constants.MASK_ENEMY
            };
            var results = spaceState.IntersectShape(queryParams);

            float bestDist = float.MaxValue;
            Node3D bestTarget = null;
            foreach (var result in results)
            {
                var collider = result["collider"].As<Node3D>();
                if (collider == null || collider == hitTarget) continue;
                float dist = hitTarget.GlobalPosition.DistanceTo(collider.GlobalPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = collider;
                }
            }

            if (bestTarget == null) return;

            var chainHealth = FindDamageable(bestTarget);
            if (chainHealth == null || !chainHealth.IsAlive) return;

            float chainDamage = baseDamage * 0.5f;
            var chainInfo = new DamageInfo
            {
                RawDamage = chainDamage,
                FinalDamage = chainDamage,
                DamageType = DamageType.Lightning,
                Attacker = _player,
                Target = bestTarget,
                HitPoint = bestTarget.GlobalPosition
            };
            chainHealth.TakeDamage(chainInfo);

            // Lightning arc VFX (visible bolt between targets)
            var arc = VfxFactory.CreateLightningArc(
                hitTarget.GlobalPosition + Vector3.Up * 0.8f,
                bestTarget.GlobalPosition + Vector3.Up * 0.8f);
            _player.GetTree().Root.AddChild(arc);
        }

        /// <summary>
        /// Resonance perk: 10% chance to apply a random debuff on ability hit.
        /// </summary>
        private void TryResonanceOnHit(Node3D target)
        {
            if (target == null) return;
            var perk = _player.PerkProcessor;
            if (perk == null) return;

            var debuff = perk.TryResonance();
            if (debuff == null) return;

            // Find StatusEffectManager on target
            Node current = target;
            while (current != null)
            {
                var sem = current.GetNodeOrNull<StatusEffectManager>("StatusEffectManager");
                if (sem != null)
                {
                    sem.ApplyEffect(debuff);
                    perk.TryBroadcastSpread(target, debuff);
                    break;
                }
                current = current.GetParent();
            }
        }

        /// <summary>
        /// Check if there's a clear line of sight between the player and target (no walls blocking).
        /// </summary>
        private bool HasLineOfSight(Node3D target)
        {
            if (!_player.IsInsideTree()) return false;
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var from = _player.GlobalPosition + Vector3.Up * _fireHeight;
            var to = target.GlobalPosition + Vector3.Up * 0.5f;
            // Raycast against default layer (walls) only
            uint wallMask = 1u << (Constants.LAYER_DEFAULT - 1);
            var rayParams = PhysicsRayQueryParameters3D.Create(from, to, wallMask);
            var result = spaceState.IntersectRay(rayParams);
            if (result.Count == 0) return true;
            // If the hit body is a door barrier (child of RoomController), ignore it
            var hitCollider = result["collider"].As<Node3D>();
            if (hitCollider != null)
            {
                var parent = hitCollider.GetParent();
                if (parent is RoomController) return true;
            }
            return false;
        }

        private IDamageable FindDamageable(Node node)
        {
            // Walk up the tree to find an IDamageable or HealthComponent
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
