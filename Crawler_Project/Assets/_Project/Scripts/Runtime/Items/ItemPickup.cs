using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData defaultItem;

        [Header("Animation")]
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobFrequency = 2f;
        [SerializeField] private float rotateSpeed = 90f;

        private ItemInstance _itemInstance;
        private Vector3 _startPosition;

        public string InteractionPrompt =>
            _itemInstance != null ? $"Pick up {_itemInstance.Data.ItemName}" : "Pick up item";

        public bool CanInteract => true;

        private void Start()
        {
            _startPosition = transform.position;

            // If no runtime instance was provided, create one from the default data
            if (_itemInstance == null && defaultItem != null)
            {
                _itemInstance = ItemInstance.Create(defaultItem);
            }
        }

        private void Update()
        {
            // Bob up and down
            float yOffset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            transform.position = _startPosition + Vector3.up * yOffset;

            // Rotate around Y axis
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }

        /// <summary>
        /// Initialize this pickup with a specific item instance.
        /// Used for runtime-spawned drops from loot tables and enemies.
        /// </summary>
        public void Initialize(ItemInstance item)
        {
            _itemInstance = item;
            _startPosition = transform.position;
        }

        public void Interact(GameObject playerObj)
        {
            if (_itemInstance == null) return;

            var receiver = playerObj.GetComponent<IItemReceiver>();
            if (receiver == null) return;

            if (receiver.TryAddItem(_itemInstance))
            {
                Debug.Log($"[ItemPickup] {receiver.DisplayName} picked up {_itemInstance.Data.ItemName}.");
                Destroy(gameObject);
            }
            else
            {
                Debug.Log($"[ItemPickup] Inventory full. Cannot pick up {_itemInstance.Data.ItemName}.");
            }
        }
    }
}
