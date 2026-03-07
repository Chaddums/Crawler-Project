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
        private const float BLADE_RING_RADIUS = 3.5f;
        private const float BLADE_RING_DAMAGE_MULT = 0.5f;
        private const float BLADE_RING_SPIN_SPEED = 3f;

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
                _player.Inventory.OnEquipmentChanged += OnEquipmentChanged;
        }

        public override void _ExitTree()
        {
            if (_player?.Inventory != null)
                _player.Inventory.OnEquipmentChanged -= OnEquipmentChanged;
            RemoveBladeRing();
        }

        private void OnEquipmentChanged(EquipmentSlot slot, ItemInstance item)
        {
            if (slot != EquipmentSlot.MainHand) return;

            var equipData = item?.BaseData as EquipmentData;
            if (equipData?.WeaponType == WeaponType.BladeRing)
            {
                AttachBladeRing();
                RemoveWeaponVisual();
            }
            else
            {
                RemoveBladeRing();
                SwapWeaponVisual(item);
            }
        }

        private void SwapWeaponVisual(ItemInstance item)
        {
            RemoveWeaponVisual();
            if (item == null) return;

            // Find the WeaponMount marker on the player body
            var body = _player.GetNodeOrNull<Node3D>("PlayerBody");
            if (body == null) return;

            var mount = FindWeaponMount(body);
            if (mount == null) return;

            // Build weapon model from item
            var weaponModel = CharacterMeshBuilder.BuildItemModel(item);
            if (weaponModel == null) return;

            CharacterMeshBuilder.ScaleModelToFit(weaponModel, 0.6f);
            weaponModel.Name = "EquippedWeapon";

            // Remove default weapon from mount (if any)
            foreach (var child in mount.GetChildren())
            {
                if (child is Node3D existing && existing.Name != "EquippedWeapon")
                    existing.QueueFree();
            }

            mount.AddChild(weaponModel);
            _weaponVisual = weaponModel;
        }

        private void RemoveWeaponVisual()
        {
            if (_weaponVisual != null && GodotObject.IsInstanceValid(_weaponVisual))
                _weaponVisual.QueueFree();
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

        private void AttachBladeRing()
        {
            if (_bladeRingActive) return;
            _bladeRingVisual = CharacterMeshBuilder.BuildBladeRing();
            _player.AddChild(_bladeRingVisual);
            _bladeRingActive = true;
            _bladeRingTickTimer = 0f;
        }

        private void RemoveBladeRing()
        {
            if (!_bladeRingActive) return;
            _bladeRingVisual?.QueueFree();
            _bladeRingVisual = null;
            _bladeRingActive = false;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (_basicAttackCooldown > 0)
                _basicAttackCooldown -= dt;

            float cdr = _playerStats.GetStat(StatType.CooldownReduction);
            for (int i = 0; i < _abilitySlots.Length; i++)
                _abilitySlots[i].TickCooldown(dt, cdr);

            // Blade ring spin + AoE damage
            if (_bladeRingActive)
            {
                if (GodotObject.IsInstanceValid(_bladeRingVisual))
                    _bladeRingVisual.RotateY(BLADE_RING_SPIN_SPEED * dt);

                _bladeRingTickTimer -= dt;
                if (_bladeRingTickTimer <= 0f)
                {
                    _bladeRingTickTimer = BLADE_RING_TICK_RATE;
                    BladeRingDamageTick();
                }
            }
        }

        private void BladeRingDamageTick()
        {
            if (!_player.IsInsideTree()) return;

            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = BLADE_RING_RADIUS };
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

                var health = FindDamageable(collider);
                if (health == null || !health.IsAlive) continue;

                var hitPoint = node3d.GlobalPosition + Vector3.Up * 0.5f;
                var damage = DamageCalculator.CalculateBasicAttack(
                    _playerStats.Stats, _player, node3d, hitPoint, Team.Player);
                damage.FinalDamage *= BLADE_RING_DAMAGE_MULT;

                if (ServiceLocator.TryGet<CombatManager>(out var combat))
                    damage.FinalDamage *= combat.ComboDamageMultiplier;

                health.TakeDamage(damage);
            }
        }

        public void HandleBasicAttack()
        {
            if (!_player.IsInsideTree()) return;
            if (_basicAttackCooldown > 0) return;

            // Face toward cursor before attacking
            FaceTowardCursor();

            // Trigger attack animation
            _animatable ??= _player.Animatable;
            _animatable?.SetState(AnimState.Attack);

            // Play gun sound
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("projectile");

            float attackSpeed = _playerStats.GetStat(StatType.AttackSpeed);
            float rate = BASIC_ATTACK_RATE / Mathf.Max(0.1f, 1f + attackSpeed);
            _basicAttackCooldown = rate;

            // Aim toward cursor
            var cursorPos = GetCursorWorldPosition();
            var aimDir = (cursorPos - _player.GlobalPosition).Flat().Normalized();
            if (aimDir.LengthSquared() < 0.001f)
                aimDir = -_player.GlobalTransform.Basis.Z;

            var muzzlePos = _player.GlobalPosition + Vector3.Up * 0.9f + aimDir * 0.5f;

            // Hitscan: find all enemies in range, pick the one closest to aim line
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = BASIC_SHOT_RANGE };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition + Vector3.Up * 0.9f),
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
                if (perpDist > 3.5f) continue; // Max tolerance off aim line

                if (perpDist < bestDist)
                {
                    bestDist = perpDist;
                    bestTarget = node3d;
                }
            }

            Vector3 tracerEnd = muzzlePos + aimDir * BASIC_SHOT_RANGE;

            if (bestTarget != null)
            {
                var hitPoint = bestTarget.GlobalPosition + Vector3.Up * 0.8f;
                tracerEnd = hitPoint;

                var health = FindDamageable(bestTarget);
                if (health != null && health.IsAlive)
                {
                    var damage = DamageCalculator.CalculateBasicAttack(
                        _playerStats.Stats, _player, bestTarget, hitPoint, Team.Player);

                    // Perk: Powered Strike — consume mana for +50% basic attack damage
                    var perkProc = _player.PerkProcessor;
                    if (perkProc != null)
                    {
                        damage.FinalDamage *= perkProc.TryPoweredStrike();
                        damage.FinalDamage = perkProc.ModifyOutgoingDamage(damage.FinalDamage, false);
                        damage.FinalDamage *= perkProc.GetExploitWeaknessMultiplier(bestTarget);

                        // Impact Driver: 20% chance to stagger
                        float stunTime = perkProc.TryImpactDriver();
                        if (stunTime > 0f)
                            damage.StunDuration = stunTime;
                    }

                    if (ServiceLocator.TryGet<CombatManager>(out var combat))
                        damage.FinalDamage *= combat.ComboDamageMultiplier;

                    health.TakeDamage(damage);

                    // Vampiric Core: lifesteal
                    perkProc?.TryVampiricLifesteal(damage.FinalDamage);

                    // Apply stun from Impact Driver
                    if (damage.StunDuration > 0f && bestTarget is IKnockbackable kb)
                        kb.ApplyStun(damage.StunDuration);

                    // Impact VFX at hit point
                    var impact = VfxFactory.CreateImpactBurst(
                        Projectile.GetDamageTypeColor(DamageType.Physical));
                    _player.GetTree().Root.AddChild(impact);
                    impact.GlobalPosition = hitPoint;

                    GD.Print($"[PlayerCombat] Basic attack hit for {damage.FinalDamage:F1}" +
                        (damage.IsCritical ? " CRIT!" : ""));
                }
            }

            // Bullet tracer from muzzle to hit/max range
            SpawnBulletTracer(muzzlePos, tracerEnd);

            // Class-specific muzzle flash VFX
            var className = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
            SpawnMuzzleFlash(className);
        }

        public void HandleAbilityInput(int slotIndex)
        {
            if (!_player.IsInsideTree()) return;
            if (slotIndex < 0 || slotIndex >= _abilitySlots.Length) return;

            var slot = _abilitySlots[slotIndex];
            if (slot.IsEmpty || !slot.IsReady) return;

            // Check mana (modified by perks)
            float manaCost = slot.Data.ManaCost;
            var perkMana = _player.PerkProcessor;
            if (perkMana != null)
                manaCost *= perkMana.GetAbilityManaCostMultiplier();

            if (!_playerStats.SpendMana(manaCost))
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

            // Play sound
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("swing");

            // Find targets via sphere overlap (works for melee and AoE)
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            float range = slot.Data.Range;
            var shape = new SphereShape3D { Radius = range };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition + Vector3.Up * 0.9f),
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

                // AoE: hit all enemies in range
                foreach (var result in results)
                {
                    var collider = (Node)result["collider"];
                    if (collider is Node3D node3d)
                    {
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
                        }
                    }
                }
            }
            else
            {
                // Single target: hit nearest enemy in cursor direction
                float closestDist = float.MaxValue;
                Node closestEnemy = null;

                foreach (var result in results)
                {
                    var collider = (Node)result["collider"];
                    if (collider is Node3D node3d)
                    {
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

                    // Melee slash arc VFX
                    if (slot.Data.Type == AbilityType.Melee)
                    {
                        var slashDir = (hitPoint - _player.GlobalPosition).Flat().Normalized();
                        var slashColor = Projectile.GetDamageTypeColor(slot.Data.DamageType);
                        var slash = VfxFactory.CreateMeleeSlashArc(slashColor, slashDir);
                        _player.GetTree().Root.AddChild(slash);
                        slash.GlobalPosition = _player.GlobalPosition;
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

                        // Chain Lightning: arc to 1 additional nearby target at 50% damage
                        if (closestEnemy is Node3D hitNode)
                            TryChainLightning(hitNode, slot.Data, damage.FinalDamage);
                    }
                }
            }

            GD.Print($"[PlayerCombat] Used ability: {slot.Data.AbilityName}");
        }

        private void SpawnProjectile(AbilityData ability, float amplifierMult = 1f)
        {
            // Aim toward cursor position (player already facing cursor from HandleAbilityInput)
            var cursorPos = GetCursorWorldPosition();
            var aimDir = (cursorPos - _player.GlobalPosition).Flat().Normalized();

            // Fallback if cursor is directly on top of player
            if (aimDir.LengthSquared() < 0.001f)
                aimDir = -_player.GlobalTransform.Basis.Z;

            // Build damage info aimed at cursor point
            var hitPoint = _player.GlobalPosition + aimDir * ability.Range;
            var damageInfo = DamageCalculator.CalculateAbilityDamage(
                ability, _playerStats.Stats, _player, _player, hitPoint, Team.Player);
            damageInfo.FinalDamage *= amplifierMult;

            // Spawn projectile
            var proj = new Projectile();
            _player.GetTree().Root.AddChild(proj);
            proj.GlobalPosition = _player.GlobalPosition + Vector3.Up * 0.9f + aimDir * 0.5f;
            proj.Initialize(aimDir, 15f, ability.Range, damageInfo, Team.Player, ability.DamageType);

            // Play projectile sound
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("projectile");

            // Arcane circle at feet for MagicUser
            var className = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
            if (className == BotFrameType.SparkPlug)
            {
                var circle = VfxFactory.CreateArcaneCircle(new Color(0.5f, 0.3f, 1f));
                _player.GetTree().Root.AddChild(circle);
                circle.GlobalPosition = _player.GlobalPosition;
            }
        }

        public void SetAbility(int slotIndex, AbilityData ability)
        {
            if (slotIndex >= 0 && slotIndex < _abilitySlots.Length)
                _abilitySlots[slotIndex] = new AbilitySlot(ability);
        }

        public AbilitySlot GetSlot(int index)
        {
            return (index >= 0 && index < _abilitySlots.Length) ? _abilitySlots[index] : null;
        }

        private void SpawnMuzzleFlash(BotFrameType className)
        {
            // Muzzle position: in front of player at gun height
            var muzzlePos = _player.GlobalPosition + Vector3.Up * 0.9f
                + (-_player.GlobalTransform.Basis.Z * 0.8f);

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

        private void SpawnBulletTracer(Vector3 from, Vector3 to)
        {
            var tracer = new MeshInstance3D();
            float length = from.DistanceTo(to);
            var tracerMesh = new BoxMesh { Size = new Vector3(0.03f, 0.03f, length) };
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
