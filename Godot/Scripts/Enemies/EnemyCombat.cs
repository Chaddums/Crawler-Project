using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Enemy attack execution with cooldown.
    /// Supports melee and ranged (projectile) attacks based on EnemyBehavior.
    /// </summary>
    public partial class EnemyCombat : Node
    {
        private EnemyData _data;
        private StatBlock _stats;
        private EnemyController _controller;
        private EnemyAI _ai;
        private float _attackCooldown;

        public override void _Ready()
        {
            _ai = GetParent().GetNodeOrNull<EnemyAI>("EnemyAI");
        }

        public void Initialize(EnemyData data, StatBlock stats, EnemyController controller)
        {
            _data = data;
            _stats = stats;
            _controller = controller;
        }

        public override void _Process(double delta)
        {
            if (_attackCooldown > 0)
                _attackCooldown -= (float)delta;

            if (_ai?.CurrentState == EnemyAI.State.Attack && _attackCooldown <= 0)
            {
                TryAttack();
            }
        }

        private void TryAttack()
        {
            var target = _ai?.Target;
            if (target == null || !IsInstanceValid(target) || !target.IsInsideTree()) return;

            var behavior = _data?.Behavior ?? EnemyBehavior.Melee;

            if (behavior == EnemyBehavior.Ranged)
            {
                FireProjectile(target);
            }
            else if (behavior == EnemyBehavior.Healer)
            {
                // Healers don't attack — healing is handled in EnemyAI
                _attackCooldown = _data?.AttackCooldown ?? 3f;
                return;
            }
            else
            {
                MeleeAttack(target);
            }

            _attackCooldown = _data?.AttackCooldown ?? 1.5f;
        }

        private void MeleeAttack(Node3D target)
        {
            IDamageable damageable = FindDamageable(target);
            if (damageable == null || !damageable.IsAlive) return;

            var body = GetParent<CharacterBody3D>();
            var damage = DamageCalculator.CalculateBasicAttack(
                _stats, body, target, target.GlobalPosition, Team.Enemy);

            var targetPlayer = target as PlayerController ?? PlayerManager.GetNearestPlayer(body.GlobalPosition);
            if (targetPlayer != null)
                damage = DamageCalculator.ProcessDamage(damage, targetPlayer.Stats.Stats);

            damageable.TakeDamage(damage);
            GD.Print($"[EnemyCombat] {_data?.EnemyName} melee for {damage.FinalDamage:F1}");
        }

        private void FireProjectile(Node3D target)
        {
            var body = GetParent<CharacterBody3D>();
            var aimDir = (target.GlobalPosition - body.GlobalPosition).Flat().Normalized();

            if (aimDir.LengthSquared() < 0.001f)
                aimDir = -body.GlobalTransform.Basis.Z;

            var damage = DamageCalculator.CalculateBasicAttack(
                _stats, body, target, target.GlobalPosition, Team.Enemy);

            var nearestPlayer = PlayerManager.GetNearestPlayer(body.GlobalPosition);
            if (nearestPlayer != null)
                damage = DamageCalculator.ProcessDamage(damage, nearestPlayer.Stats.Stats);

            var proj = new Projectile();
            body.GetTree().Root.AddChild(proj);
            proj.GlobalPosition = body.GlobalPosition + Vector3.Up * 0.8f + aimDir * 0.5f;
            proj.Initialize(aimDir, 10f, _data?.AttackRange ?? 8f, damage, Team.Enemy, DamageType.Physical);

            GD.Print($"[EnemyCombat] {_data?.EnemyName} fires projectile");
        }

        private static IDamageable FindDamageable(Node target)
        {
            if (target is IDamageable d) return d;
            var health = target.GetNodeOrNull<HealthComponent>("HealthComponent");
            return health;
        }
    }
}
