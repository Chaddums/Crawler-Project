using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Area3D that deals damage to overlapping HurtBoxes.
    /// Attach to weapons, ability effects, etc.
    /// </summary>
    public partial class HitBox : Area3D
    {
        [Export] public Team OwnerTeam { get; set; } = Team.Player;

        private DamageInfo _pendingDamage;
        private bool _hasPending;

        public override void _Ready()
        {
            AreaEntered += OnAreaEntered;
            Monitoring = false; // Disabled by default, enabled during attacks
        }

        /// <summary>
        /// Activate the hitbox with damage data for one attack.
        /// </summary>
        public void Fire(DamageInfo damage)
        {
            _pendingDamage = damage;
            _hasPending = true;
            Monitoring = true;

            // Auto-disable after a short window
            GetTree().CreateTimer(0.15).Timeout += () =>
            {
                Monitoring = false;
                _hasPending = false;
            };
        }

        private void OnAreaEntered(Area3D other)
        {
            if (!_hasPending) return;

            if (other is HurtBox hurtBox && hurtBox.Team != OwnerTeam)
            {
                hurtBox.ReceiveDamage(_pendingDamage);
            }
        }

        public override void _ExitTree()
        {
            AreaEntered -= OnAreaEntered;
        }
    }
}
