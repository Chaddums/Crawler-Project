using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class DoorController : MonoBehaviour, IInteractable
    {
        [Header("State")]
        [SerializeField] private bool _startLocked;

        [Header("Visuals")]
        [SerializeField] private Renderer _doorRenderer;
        [SerializeField] private Material _unlockedMaterial;
        [SerializeField] private Material _lockedMaterial;
        [SerializeField] private GameObject _lockVisual;

        [Header("Animation")]
        [SerializeField] private Animator _animator;

        private bool _isLocked;
        private bool _isOpen;

        private static readonly int AnimOpen = Animator.StringToHash("Open");
        private static readonly int AnimClose = Animator.StringToHash("Close");

        // --- IInteractable ---
        public string InteractionPrompt => _isLocked ? "Locked" : (_isOpen ? "Close Door" : "Open Door");
        public bool CanInteract => !_isLocked;

        private void Awake()
        {
            _isLocked = _startLocked;
            _isOpen = false;
        }

        private void Start()
        {
            UpdateVisuals();
        }

        /// <summary>
        /// Locks the door. Closes it first if it is open.
        /// </summary>
        public void Lock()
        {
            _isLocked = true;

            if (_isOpen)
                SetOpen(false);

            UpdateVisuals();
        }

        /// <summary>
        /// Unlocks the door so it can be interacted with.
        /// </summary>
        public void Unlock()
        {
            _isLocked = false;
            UpdateVisuals();
        }

        /// <summary>
        /// IInteractable implementation. Opens or closes the door when the player interacts.
        /// </summary>
        public void Interact(GameObject playerObj)
        {
            if (_isLocked)
            {
                Debug.Log("[DoorController] Door is locked.");
                return;
            }

            SetOpen(!_isOpen);
        }

        private void SetOpen(bool open)
        {
            _isOpen = open;

            if (_animator != null)
            {
                _animator.SetTrigger(_isOpen ? AnimOpen : AnimClose);
            }
            else
            {
                // Fallback: simply toggle the door renderer visibility
                if (_doorRenderer != null)
                    _doorRenderer.enabled = !_isOpen;
            }
        }

        private void UpdateVisuals()
        {
            // Swap material to reflect locked / unlocked state
            if (_doorRenderer != null)
            {
                Material mat = _isLocked ? _lockedMaterial : _unlockedMaterial;
                if (mat != null)
                    _doorRenderer.material = mat;
            }

            // Toggle lock icon / visual
            if (_lockVisual != null)
                _lockVisual.SetActive(_isLocked);
        }
    }
}
