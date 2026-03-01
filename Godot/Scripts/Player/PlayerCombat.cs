using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Player combat node: basic attacks via raycast, 6 ability slots, cooldown ticking.
    /// Class-specific attack VFX and projectile spawning for ranged abilities.
    /// </summary>
    public partial class PlayerCombat : Node, IAttacker
    {
        private PlayerController _player;
        private PlayerStats _playerStats;
        private IAnimatable _animatable;
        private readonly AbilitySlot[] _abilitySlots = new AbilitySlot[Constants.MAX_ABILITY_SLOTS];

        private float _basicAttackCooldown;
        private const float BASIC_ATTACK_RATE = 0.8f;
        private Camera3D _camera;

        public StatBlock Stats => _playerStats?.Stats;
        public Node3D Node => _player;
        public Team Team => Team.Player;

        public override void _Ready()
        {
            _player = GetParent<PlayerController>();
            _playerStats = _player.GetNode<PlayerStats>("PlayerStats");

            for (int i = 0; i < _abilitySlots.Length; i++)
                _abilitySlots[i] = new AbilitySlot();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (_basicAttackCooldown > 0)
                _basicAttackCooldown -= dt;

            float cdr = _playerStats.GetStat(StatType.CooldownReduction);
            for (int i = 0; i < _abilitySlots.Length; i++)
                _abilitySlots[i].TickCooldown(dt, cdr);
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

            // Play swing sound
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("swing");

            float attackSpeed = _playerStats.GetStat(StatType.AttackSpeed);
            float rate = BASIC_ATTACK_RATE / Mathf.Max(0.1f, 1f + attackSpeed);
            _basicAttackCooldown = rate;

            // Find nearest enemy within attack range using sphere overlap
            var spaceState = _player.GetWorld3D().DirectSpaceState;
            float range = Constants.DEFAULT_ATTACK_RANGE + 1f; // 3 unit radius
            var shape = new SphereShape3D { Radius = range };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, _player.GlobalPosition + Vector3.Up * 0.9f),
                CollisionMask = Constants.MASK_ENEMY
            };

            var results = spaceState.IntersectShape(queryParams);
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

                var health = FindDamageable(closestEnemy);
                if (health != null && health.IsAlive)
                {
                    var damage = DamageCalculator.CalculateBasicAttack(
                        _playerStats.Stats, _player, closestEnemy, hitPoint, Team.Player);

                    // Apply combo multiplier
                    if (ServiceLocator.TryGet<CombatManager>(out var combat))
                        damage.FinalDamage *= combat.ComboDamageMultiplier;

                    health.TakeDamage(damage);

                    // Class-specific attack VFX
                    var className = _player.ClassController?.CurrentClass ?? BotFrameType.TinCan;
                    SpawnAttackVFX(hitPoint, className);

                    GD.Print($"[PlayerCombat] Basic attack hit for {damage.FinalDamage:F1}" +
                        (damage.IsCritical ? " CRIT!" : ""));
                }
            }
        }

        public void HandleAbilityInput(int slotIndex)
        {
            if (!_player.IsInsideTree()) return;
            if (slotIndex < 0 || slotIndex >= _abilitySlots.Length) return;

            var slot = _abilitySlots[slotIndex];
            if (slot.IsEmpty || !slot.IsReady) return;

            // Check mana
            if (!_playerStats.SpendMana(slot.Data.ManaCost))
            {
                GD.Print("[PlayerCombat] Not enough mana");
                return;
            }

            // Face toward cursor before using ability
            FaceTowardCursor();

            slot.StartCooldown();

            _animatable ??= _player.Animatable;
            _animatable?.SetState(AnimState.Attack);

            // Projectile abilities: spawn a traveling projectile
            if (slot.Data.Type == AbilityType.Projectile)
            {
                SpawnProjectile(slot.Data);
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

            if (slot.Data.AoERadius > 0)
            {
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
                            health.TakeDamage(damage);
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

                    var health = FindDamageable(closestEnemy);
                    if (health != null && health.IsAlive)
                    {
                        var damage = DamageCalculator.CalculateAbilityDamage(
                            slot.Data, _playerStats.Stats, _player, closestEnemy, hitPoint, Team.Player);
                        health.TakeDamage(damage);
                    }
                }
            }

            GD.Print($"[PlayerCombat] Used ability: {slot.Data.AbilityName}");
        }

        private void SpawnProjectile(AbilityData ability)
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

            // Spawn projectile
            var proj = new Projectile();
            _player.GetTree().Root.AddChild(proj);
            proj.GlobalPosition = _player.GlobalPosition + Vector3.Up * 0.9f + aimDir * 0.5f;
            proj.Initialize(aimDir + Vector3.Up * 0.05f, 15f, ability.Range, damageInfo, Team.Player, ability.DamageType);

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

        private void SpawnAttackVFX(Vector3 hitPoint, BotFrameType className)
        {
            switch (className)
            {
                case BotFrameType.TinCan:
                    SpawnSteelSlash(hitPoint);
                    break;
                case BotFrameType.SparkPlug:
                    SpawnArcaneSlash(hitPoint);
                    break;
                case BotFrameType.RustBucket:
                    SpawnDoubleSlash(hitPoint);
                    break;
                case BotFrameType.Scrapheap:
                    SpawnClawRake(hitPoint);
                    break;
                case BotFrameType.NoiseBox:
                    SpawnDarkChord(hitPoint);
                    break;
                case BotFrameType.Clunker:
                    SpawnPunchFlash(hitPoint);
                    break;
                default:
                    SpawnSteelSlash(hitPoint);
                    break;
            }
        }

        private void SpawnSteelSlash(Vector3 hitPoint)
        {
            var slash = CreateSlashMesh(new Vector3(1.8f, 0.025f, 0.5f),
                new Color(0.75f, 0.78f, 0.85f, 0.8f), new Color(0.8f, 0.85f, 1f));
            PositionSlash(slash, hitPoint);
            FadeAndFree(slash, 0.15f);
        }

        private void SpawnArcaneSlash(Vector3 hitPoint)
        {
            var slash = CreateSlashMesh(new Vector3(1.2f, 0.02f, 0.3f),
                new Color(0.5f, 0.3f, 1f, 0.7f), new Color(0.6f, 0.3f, 1f));
            PositionSlash(slash, hitPoint);
            FadeAndFree(slash, 0.15f);
        }

        private void SpawnDoubleSlash(Vector3 hitPoint)
        {
            // First slash
            var slash1 = CreateSlashMesh(new Vector3(1.2f, 0.015f, 0.2f),
                new Color(0.7f, 0.7f, 0.8f, 0.8f), new Color(0.8f, 0.8f, 1f));
            PositionSlash(slash1, hitPoint, -0.15f);
            FadeAndFree(slash1, 0.1f);

            // Second slash, slightly delayed and offset
            var slash2 = CreateSlashMesh(new Vector3(1.2f, 0.015f, 0.2f),
                new Color(0.7f, 0.7f, 0.8f, 0.8f), new Color(0.8f, 0.8f, 1f));
            PositionSlash(slash2, hitPoint, 0.15f);
            slash2.Visible = false;
            _player.GetTree().CreateTimer(0.06f).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(slash2))
                {
                    slash2.Visible = true;
                    FadeAndFree(slash2, 0.1f);
                }
            };
        }

        private void SpawnClawRake(Vector3 hitPoint)
        {
            Color clawColor = new Color(0.9f, 0.3f, 0.2f, 0.8f);
            Color clawEmission = new Color(0.8f, 0.2f, 0.1f);

            for (int i = -1; i <= 1; i++)
            {
                var line = CreateSlashMesh(new Vector3(1.4f, 0.015f, 0.08f),
                    clawColor, clawEmission);
                _player.GetTree().Root.AddChild(line);

                var midPoint = (_player.GlobalPosition + hitPoint) / 2f;
                line.GlobalPosition = midPoint + Vector3.Up * (0.8f + i * 0.15f);

                var dir = (hitPoint - _player.GlobalPosition).Flat();
                if (dir.LengthSquared() > 0.01f)
                    line.LookAt(line.GlobalPosition + dir.Normalized(), Vector3.Up);

                FadeAndFree(line, 0.12f);
            }
        }

        private void SpawnDarkChord(Vector3 hitPoint)
        {
            // Purple wave ring
            var ring = VfxFactory.CreateShockwaveRing(new Color(0.5f, 0.2f, 0.7f));
            _player.GetTree().Root.AddChild(ring);
            ring.GlobalPosition = _player.GlobalPosition + Vector3.Up * 0.5f;

            // Dark note particles
            var notes = VfxFactory.CreateMusicNotes(new Color(0.6f, 0.2f, 0.8f));
            _player.GetTree().Root.AddChild(notes);
            notes.GlobalPosition = hitPoint + Vector3.Up * 0.5f;
        }

        private void SpawnPunchFlash(Vector3 hitPoint)
        {
            // Quick punch flash
            var flash = CreateSlashMesh(new Vector3(0.6f, 0.6f, 0.02f),
                new Color(1f, 0.9f, 0.5f, 0.9f), new Color(1f, 0.85f, 0.4f));
            _player.GetTree().Root.AddChild(flash);
            flash.GlobalPosition = hitPoint + Vector3.Up * 0.9f;
            FadeAndFree(flash, 0.08f);

            // Ground shockwave ring
            var ring = VfxFactory.CreateShockwaveRing(new Color(0.8f, 0.6f, 0.3f));
            _player.GetTree().Root.AddChild(ring);
            ring.GlobalPosition = hitPoint;
        }

        private MeshInstance3D CreateSlashMesh(Vector3 size, Color albedo, Color emission)
        {
            var slash = new MeshInstance3D();
            var slashMesh = new BoxMesh { Size = size };
            slash.Mesh = slashMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = albedo;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = emission;
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            slash.MaterialOverride = mat;

            return slash;
        }

        private void PositionSlash(MeshInstance3D slash, Vector3 hitPoint, float yOffset = 0f)
        {
            _player.GetTree().Root.AddChild(slash);

            var midPoint = (_player.GlobalPosition + hitPoint) / 2f;
            slash.GlobalPosition = midPoint + Vector3.Up * (0.9f + yOffset);

            var dir = (hitPoint - _player.GlobalPosition).Flat();
            if (dir.LengthSquared() > 0.01f)
                slash.LookAt(slash.GlobalPosition + dir.Normalized(), Vector3.Up);
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
            _camera ??= _player.GetViewport().GetCamera3D();
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

        private IDamageable FindDamageable(Node node)
        {
            if (node is IDamageable d) return d;
            var health = node.GetNodeOrNull<HealthComponent>("HealthComponent");
            return health;
        }
    }
}
