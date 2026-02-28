using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Player combat node: basic attacks via raycast, 6 ability slots, cooldown ticking.
    /// </summary>
    public partial class PlayerCombat : Node, IAttacker
    {
        private PlayerController _player;
        private PlayerStats _playerStats;
        private readonly AbilitySlot[] _abilitySlots = new AbilitySlot[Constants.MAX_ABILITY_SLOTS];

        private float _basicAttackCooldown;
        private const float BASIC_ATTACK_RATE = 0.8f;

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
            if (_basicAttackCooldown > 0) return;

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

                // Face the target
                var faceDir = (hitPoint - _player.GlobalPosition).Flat();
                if (faceDir.LengthSquared() > 0.01f)
                    _player.LookAt(_player.GlobalPosition + faceDir.Normalized(), Vector3.Up);

                var health = FindDamageable(closestEnemy);
                if (health != null && health.IsAlive)
                {
                    var damage = DamageCalculator.CalculateBasicAttack(
                        _playerStats.Stats, _player, closestEnemy, hitPoint, Team.Player);

                    // Apply combo multiplier
                    if (ServiceLocator.TryGet<CombatManager>(out var combat))
                        damage.FinalDamage *= combat.ComboDamageMultiplier;

                    health.TakeDamage(damage);

                    GD.Print($"[PlayerCombat] Basic attack hit for {damage.FinalDamage:F1}" +
                        (damage.IsCritical ? " CRIT!" : ""));
                }
            }
        }

        public void HandleAbilityInput(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _abilitySlots.Length) return;

            var slot = _abilitySlots[slotIndex];
            if (slot.IsEmpty || !slot.IsReady) return;

            // Check mana
            if (!_playerStats.SpendMana(slot.Data.ManaCost))
            {
                GD.Print("[PlayerCombat] Not enough mana");
                return;
            }

            slot.StartCooldown();

            // For melee abilities, raycast
            if (slot.Data.Type == AbilityType.Melee)
            {
                var spaceState = _player.GetWorld3D().DirectSpaceState;
                var from = _player.GlobalPosition + Vector3.Up * 0.9f;
                var forward = -_player.GlobalBasis.Z;
                var to = from + forward * slot.Data.Range;

                var query = PhysicsRayQueryParameters3D.Create(from, to, Constants.MASK_ENEMY);
                var result = spaceState.IntersectRay(query);

                if (result.Count > 0)
                {
                    var collider = (Node)result["collider"];
                    var hitPoint = (Vector3)result["position"];
                    var health = FindDamageable(collider);

                    if (health != null && health.IsAlive)
                    {
                        var damage = DamageCalculator.CalculateAbilityDamage(
                            slot.Data, _playerStats.Stats, _player, collider, hitPoint, Team.Player);
                        health.TakeDamage(damage);
                    }
                }
            }

            GameEvents.OnAbilityUnlocked?.Invoke(slot.Data);
            GD.Print($"[PlayerCombat] Used ability: {slot.Data.AbilityName}");
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

        private IDamageable FindDamageable(Node node)
        {
            if (node is IDamageable d) return d;
            var health = node.GetNodeOrNull<HealthComponent>("HealthComponent");
            return health;
        }
    }
}
