using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputHandler : MonoBehaviour
    {
        public event Action<Vector2> OnMoveInput;
        public event Action OnClickToMove;
        public event Action<int> OnAbilityInput;
        public event Action OnBasicAttack;
        public event Action OnInteract;
        public event Action OnInventoryToggle;
        public event Action OnCharacterSheetToggle;
        public event Action OnPause;

        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _clickAction;
        private InputAction _interactAction;
        private InputAction _inventoryAction;
        private InputAction _characterSheetAction;
        private InputAction _pauseAction;
        private InputAction[] _abilityActions;

        private bool _inputEnabled = true;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();

            _moveAction = _playerInput.actions["Move"];
            _clickAction = _playerInput.actions["ClickToMove"];
            _interactAction = _playerInput.actions["Interact"];
            _inventoryAction = _playerInput.actions["Inventory"];
            _characterSheetAction = _playerInput.actions["CharacterSheet"];
            _pauseAction = _playerInput.actions["Pause"];

            _abilityActions = new InputAction[Constants.MAX_ABILITY_SLOTS];
            for (int i = 0; i < Constants.MAX_ABILITY_SLOTS; i++)
            {
                string actionName = $"Ability{i + 1}";
                _abilityActions[i] = _playerInput.actions[actionName];
            }
        }

        private void OnEnable()
        {
            _moveAction.performed += HandleMove;
            _moveAction.canceled += HandleMove;
            _clickAction.performed += HandleClick;
            _interactAction.performed += HandleInteract;
            _inventoryAction.performed += HandleInventory;
            _characterSheetAction.performed += HandleCharacterSheet;
            _pauseAction.performed += HandlePause;

            for (int i = 0; i < _abilityActions.Length; i++)
            {
                if (_abilityActions[i] == null) continue;
                int slot = i;
                _abilityActions[i].performed += _ => HandleAbility(slot);
            }
        }

        private void OnDisable()
        {
            _moveAction.performed -= HandleMove;
            _moveAction.canceled -= HandleMove;
            _clickAction.performed -= HandleClick;
            _interactAction.performed -= HandleInteract;
            _inventoryAction.performed -= HandleInventory;
            _characterSheetAction.performed -= HandleCharacterSheet;
            _pauseAction.performed -= HandlePause;
        }

        private void HandleMove(InputAction.CallbackContext ctx)
        {
            if (!_inputEnabled) return;
            OnMoveInput?.Invoke(ctx.ReadValue<Vector2>());
        }

        private void HandleClick(InputAction.CallbackContext ctx)
        {
            if (!_inputEnabled) return;
            OnClickToMove?.Invoke();
        }

        private void HandleAbility(int slot)
        {
            if (!_inputEnabled) return;
            OnAbilityInput?.Invoke(slot);
        }

        private void HandleInteract(InputAction.CallbackContext ctx)
        {
            if (!_inputEnabled) return;
            OnInteract?.Invoke();
        }

        private void HandleInventory(InputAction.CallbackContext ctx)
        {
            OnInventoryToggle?.Invoke();
            GameEvents.OnInventoryToggled?.Invoke();
        }

        private void HandleCharacterSheet(InputAction.CallbackContext ctx)
        {
            OnCharacterSheetToggle?.Invoke();
            GameEvents.OnCharacterSheetToggled?.Invoke();
        }

        private void HandlePause(InputAction.CallbackContext ctx)
        {
            OnPause?.Invoke();
            GameEvents.OnPauseToggled?.Invoke();
        }

        public void EnableInput() => _inputEnabled = true;
        public void DisableInput() => _inputEnabled = false;
    }
}
