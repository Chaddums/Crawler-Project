using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Service: tracks combo hits for the player.
    /// 2-second window, 10% bonus per hit, max 10 stacks.
    /// </summary>
    public partial class CombatManager : Node
    {
        private const float COMBO_WINDOW = 2f;
        private const float COMBO_BONUS_PER_HIT = 0.10f;
        private const int MAX_COMBO_STACKS = 10;

        private int _comboCount;
        private float _comboTimer;

        public int ComboCount => _comboCount;
        public float ComboDamageMultiplier => 1f + _comboCount * COMBO_BONUS_PER_HIT;

        public override void _Ready()
        {
            ServiceLocator.Register(this);
            GameEvents.OnDamageDealt += OnDamageDealt;
        }

        public override void _Process(double delta)
        {
            if (_comboCount > 0)
            {
                _comboTimer -= (float)delta;
                if (_comboTimer <= 0)
                {
                    _comboCount = 0;
                }
            }
        }

        private void OnDamageDealt(DamageInfo info)
        {
            // Only count player attacks
            if (info.Attacker == null) return;
            if (!info.Attacker.IsInGroup(Constants.GROUP_PLAYER)) return;

            _comboCount = Mathf.Min(_comboCount + 1, MAX_COMBO_STACKS);
            _comboTimer = COMBO_WINDOW;
            GameEvents.OnComboHit?.Invoke(_comboCount);
        }

        public void ResetCombo()
        {
            _comboCount = 0;
            _comboTimer = 0;
        }

        public override void _ExitTree()
        {
            GameEvents.OnDamageDealt -= OnDamageDealt;
            ServiceLocator.Unregister<CombatManager>();
        }
    }
}
