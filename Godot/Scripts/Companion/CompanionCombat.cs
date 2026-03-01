using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Companion auto-attack: hits the current target when in Attack state.
    /// </summary>
    public partial class CompanionCombat : Node
    {
        private CompanionData _data;
        private StatBlock _stats;
        private CompanionController _controller;
        private CompanionAI _ai;
        private float _attackCooldown;

        public override void _Ready()
        {
            _ai = GetParent().GetNodeOrNull<CompanionAI>("CompanionAI");
        }

        public void Initialize(CompanionData data, StatBlock stats, CompanionController controller)
        {
            _data = data;
            _stats = stats;
            _controller = controller;
        }

        public override void _Process(double delta)
        {
            if (_attackCooldown > 0)
                _attackCooldown -= (float)delta;

            if (_ai?.CurrentState == CompanionAI.State.Attack && _attackCooldown <= 0)
                TryAttack();
        }

        private void TryAttack()
        {
            var target = _ai?.Target;
            if (target == null || !IsInstanceValid(target) || !target.IsInsideTree()) return;

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
                _stats, body, target, target.GlobalPosition, Team.Player);

            // Apply armor from target
            if (target is EnemyController enemyCtrl)
                damage = DamageCalculator.ProcessDamage(damage, enemyCtrl.Stats);

            damageable.TakeDamage(damage);
            _attackCooldown = _data?.AttackCooldown ?? 1.2f;
        }
    }
}
