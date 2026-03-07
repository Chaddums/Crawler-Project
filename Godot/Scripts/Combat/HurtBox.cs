using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Area3D attached to damageable entities. Receives damage from HitBoxes.
    /// </summary>
    public partial class HurtBox : Area3D
    {
        [Export] public Team Team { get; set; } = Team.Player;

        private IDamageable _damageable;
        private IKnockbackable _knockbackable;

        public override void _Ready()
        {
            // Walk up to find the IDamageable parent
            Node parent = GetParent();
            while (parent != null)
            {
                if (_damageable == null && parent is IDamageable d)
                    _damageable = d;
                if (_knockbackable == null && parent is IKnockbackable k)
                    _knockbackable = k;
                if (_damageable != null) break;
                parent = parent.GetParent();
            }

            // Try HealthComponent as a child of the owner
            if (_damageable == null)
            {
                var owner = GetParent();
                if (owner != null)
                {
                    var health = owner.GetNodeOrNull<HealthComponent>("HealthComponent");
                    if (health != null)
                        _damageable = health;
                }
            }
        }

        public void ReceiveDamage(DamageInfo damage)
        {
            if (_damageable == null || !_damageable.IsAlive) return;

            _damageable.TakeDamage(damage);

            // Apply knockback/stun if applicable
            if (_knockbackable != null)
            {
                if (damage.KnockbackForce > 0 && damage.Attacker is Node3D attackerNode)
                    _knockbackable.ApplyKnockback(attackerNode.GlobalPosition, damage.KnockbackForce);

                if (damage.StunDuration > 0)
                    _knockbackable.ApplyStun(damage.StunDuration);
            }
        }
    }
}
