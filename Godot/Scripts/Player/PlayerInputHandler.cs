using System;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Translates Godot InputMap actions into C# events consumed by other player scripts.
    /// </summary>
    public partial class PlayerInputHandler : Node
    {
        public event Action<Vector2> OnMoveInput;
        public event Action OnClickToMove;
        public event Action<int> OnAbilityInput;
        public event Action OnBasicAttack;
        public event Action OnInteract;
        public event Action OnInventoryToggle;
        public event Action OnCharacterSheetToggle;
        public event Action OnPause;

        private bool _inputEnabled = true;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!_inputEnabled) return;

            // Click-to-move (right mouse button)
            if (@event.IsActionPressed("click_to_move"))
            {
                OnClickToMove?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            // Basic attack (left mouse button)
            if (@event.IsActionPressed("basic_attack"))
            {
                OnBasicAttack?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            // Interact
            if (@event.IsActionPressed("interact"))
            {
                OnInteract?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            // Ability slots 1-6
            for (int i = 1; i <= Constants.MAX_ABILITY_SLOTS; i++)
            {
                if (@event.IsActionPressed($"ability_{i}"))
                {
                    OnAbilityInput?.Invoke(i - 1);
                    GetViewport().SetInputAsHandled();
                    break;
                }
            }

            // UI toggles (these fire even when gameplay input is disabled)
            if (@event.IsActionPressed("inventory"))
            {
                OnInventoryToggle?.Invoke();
                GameEvents.OnInventoryToggled?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            if (@event.IsActionPressed("character_sheet"))
            {
                OnCharacterSheetToggle?.Invoke();
                GameEvents.OnCharacterSheetToggled?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            if (@event.IsActionPressed("pause"))
            {
                OnPause?.Invoke();
                GameEvents.OnPauseToggled?.Invoke();
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_inputEnabled) return;

            // Continuous WASD movement
            var input = new Vector2(
                Input.GetAxis("move_left", "move_right"),
                Input.GetAxis("move_down", "move_up")
            );

            OnMoveInput?.Invoke(input);
        }

        public void EnableInput() => _inputEnabled = true;
        public void DisableInput() => _inputEnabled = false;
    }
}
