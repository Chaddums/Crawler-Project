using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Enemy attack execution with cooldown.
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
            if (target == null || !IsInstanceValid(target)) return;

            // Find damageable on target
            IDamageable damageable = null;
            if (target is IDamageable d)
                damageable = d;
            else
            {
                var health = target.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health != null) damageable = health;
            }

            if (damageable == null || !damageable.IsAlive) return;

            var body = GetParent<CharacterBody3D>();
            var damage = DamageCalculator.CalculateBasicAttack(
                _stats, body, target, target.GlobalPosition, Team.Enemy);

            // Apply armor reduction from target stats
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                damage = DamageCalculator.ProcessDamage(damage, player.Stats.Stats);
            }

            damageable.TakeDamage(damage);
            _attackCooldown = _data?.AttackCooldown ?? 1.5f;

            GD.Print($"[EnemyCombat] {_data?.EnemyName} attacks for {damage.FinalDamage:F1}");
        }
    }
}
