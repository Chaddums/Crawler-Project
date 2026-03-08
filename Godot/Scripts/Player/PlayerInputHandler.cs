using System;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Translates Godot InputMap actions into C# events consumed by other player scripts.
    /// Supports device filtering for local co-op: P1 uses keyboard+mouse, P2 uses gamepad.
    /// </summary>
    public partial class PlayerInputHandler : Node
    {
        public event Action<Vector2> OnMoveInput;
        public event Action OnClickToMove;
        public event Action<int> OnAbilityInput;
        public event Action OnBasicAttack;
        public event Action OnInteract;
        public event Action OnDash;
        public event Action OnJump;
        public event Action OnUseHealth;
        public event Action OnUseMana;
        public event Action OnInventoryToggle;
        public event Action OnCharacterSheetToggle;
        public event Action OnPause;

        /// <summary>Gamepad right-stick aim direction (normalized, zero if no input).</summary>
        public event Action<Vector2> OnAimInput;

        private bool _inputEnabled = true;
        private int _playerIndex; // 0 = P1 (KB+M), 1+ = P2 (gamepad)

        public int PlayerIndex => _playerIndex;
        public bool IsGamepad => _playerIndex > 0;

        public void SetPlayerIndex(int index)
        {
            _playerIndex = index;
        }

        /// <summary>
        /// Check if an input event belongs to this player's device.
        /// P1 accepts keyboard + mouse (device -1 or any non-joypad).
        /// P2 accepts joypad events only.
        /// </summary>
        private bool IsMyEvent(InputEvent @event)
        {
            if (_playerIndex == 0)
            {
                // P1: accept keyboard and mouse events (not joypad)
                return @event is not InputEventJoypadButton and not InputEventJoypadMotion;
            }
            else
            {
                // P2+: accept joypad events only
                return @event is InputEventJoypadButton or InputEventJoypadMotion;
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!_inputEnabled) return;
            if (!IsMyEvent(@event)) return;

            // Click-to-move (right mouse button) — P1 only
            if (!IsGamepad && @event.IsActionPressed("click_to_move"))
            {
                OnClickToMove?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            // Basic attack (left mouse button or gamepad RT/RB)
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

            // Dash
            if (@event.IsActionPressed("dash"))
            {
                OnDash?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            // Jump
            if (@event.IsActionPressed("jump"))
            {
                OnJump?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            // Quick-use consumables
            if (@event.IsActionPressed("use_health"))
            {
                OnUseHealth?.Invoke();
                GetViewport().SetInputAsHandled();
            }
            if (@event.IsActionPressed("use_mana"))
            {
                OnUseMana?.Invoke();
                GetViewport().SetInputAsHandled();
            }

            // Ability slots 1-6 (keyboard) or gamepad face buttons
            for (int i = 1; i <= Constants.MAX_ABILITY_SLOTS; i++)
            {
                if (@event.IsActionPressed($"ability_{i}"))
                {
                    OnAbilityInput?.Invoke(i - 1);
                    GetViewport().SetInputAsHandled();
                    break;
                }
            }

            // UI toggles
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

            if (IsGamepad)
            {
                // P2: Read left stick for movement
                var input = new Vector2(
                    Input.GetJoyAxis(0, JoyAxis.LeftX),
                    -Input.GetJoyAxis(0, JoyAxis.LeftY) // Invert Y: stick down = negative
                );
                if (input.LengthSquared() < 0.04f) input = Vector2.Zero;
                OnMoveInput?.Invoke(input);

                // Right stick for aiming
                var aim = new Vector2(
                    Input.GetJoyAxis(0, JoyAxis.RightX),
                    -Input.GetJoyAxis(0, JoyAxis.RightY)
                );
                if (aim.LengthSquared() < 0.04f) aim = Vector2.Zero;
                OnAimInput?.Invoke(aim);
            }
            else
            {
                // P1: WASD movement
                var input = new Vector2(
                    Input.GetAxis("move_left", "move_right"),
                    Input.GetAxis("move_down", "move_up")
                );
                OnMoveInput?.Invoke(input);
            }
        }

        public void EnableInput()
        {
            _inputEnabled = true;
            GD.Print("[PlayerInputHandler] Input ENABLED");
        }

        public void DisableInput()
        {
            _inputEnabled = false;
            GD.Print("[PlayerInputHandler] Input DISABLED");
        }
    }
}
