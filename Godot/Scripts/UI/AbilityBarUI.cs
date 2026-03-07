using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// 6-slot horizontal ability bar, anchored bottom-center of HUD.
    /// Polls PlayerCombat each frame to update slot states.
    /// </summary>
    public partial class AbilityBarUI : Control
    {
        private readonly AbilitySlotUI[] _slots = new AbilitySlotUI[Constants.MAX_ABILITY_SLOTS];
        private bool _built;

        public override void _Ready()
        {
            BuildBar();
        }

        private void BuildBar()
        {
            // Position at bottom center
            SetAnchorsPreset(LayoutPreset.CenterBottom);
            GrowHorizontal = GrowDirection.Both;
            GrowVertical = GrowDirection.Begin;
            var totalWidth = Constants.MAX_ABILITY_SLOTS * 68 + 10;
            Position = new Vector2(960 - totalWidth / 2f, 1010);
            Size = new Vector2(totalWidth, 74);

            var container = new HBoxContainer();
            container.AddThemeConstantOverride("separation", 4);
            AddChild(container);

            for (int i = 0; i < Constants.MAX_ABILITY_SLOTS; i++)
            {
                var slotUI = new AbilitySlotUI();
                slotUI.Initialize(i);
                container.AddChild(slotUI);
                _slots[i] = slotUI;
            }

            _built = true;
        }

        public override void _Process(double delta)
        {
            if (!_built) return;

            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            for (int i = 0; i < Constants.MAX_ABILITY_SLOTS; i++)
            {
                var slot = player.Combat.GetSlot(i);
                _slots[i].UpdateSlot(slot);
            }
        }
    }
}
