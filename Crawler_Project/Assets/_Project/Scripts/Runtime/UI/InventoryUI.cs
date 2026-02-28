using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class InventoryUI : UIPanel, IExclusivePanel
    {
        [Header("Layout")]
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private GameObject _slotPrefab;

        [Header("References")]
        [SerializeField] private TooltipUI _tooltip;
        [SerializeField] private TextMeshProUGUI _slotCountText;

        [Header("Drag-Drop")]
        [SerializeField] private Canvas _rootCanvas;
        [SerializeField] private RectTransform _dropZone;

        public event Action<ItemInstance> OnItemClicked;

        private PlayerInventory _inventory;
        private readonly List<InventorySlotView> _slotViews = new();

        // Shared drag state
        private static InventorySlotView _dragSource;
        private static GameObject _dragGhost;

        public static InventorySlotView DragSource => _dragSource;

        private void OnEnable()
        {
            GameEvents.OnInventoryToggled += HandleToggle;
        }

        private void OnDisable()
        {
            GameEvents.OnInventoryToggled -= HandleToggle;
        }

        private void Start()
        {
            SetVisibleImmediate(false);
        }

        public void Open(PlayerInventory inventory)
        {
            _inventory = inventory;
            _inventory.OnInventoryChanged += RefreshSlots;
            Show();
            RefreshSlots();
        }

        public void Close()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= RefreshSlots;

            Hide();
            CancelDrag();

            if (_tooltip != null)
                _tooltip.Hide();
        }

        protected override void OnAfterHide()
        {
            // Cleanup after hide animation completes
        }

        public void RefreshSlots()
        {
            if (_inventory == null) return;

            // Clear existing slot views
            ClearSlots();

            // Create slot UI for each item
            foreach (var item in _inventory.Items)
            {
                CreateSlotView(item);
            }

            // Update slot count display
            if (_slotCountText != null)
                _slotCountText.text = $"{_inventory.UsedSlots} / {_inventory.MaxSlots}";
        }

        private void CreateSlotView(ItemInstance item)
        {
            if (_slotPrefab == null || _slotContainer == null) return;

            GameObject slotObj = Instantiate(_slotPrefab, _slotContainer);
            var view = slotObj.GetComponent<InventorySlotView>();

            if (view == null)
            {
                view = slotObj.AddComponent<InventorySlotView>();
            }

            view.Setup(item, this);
            _slotViews.Add(view);
        }

        private void ClearSlots()
        {
            foreach (var view in _slotViews)
            {
                if (view != null)
                    Destroy(view.gameObject);
            }
            _slotViews.Clear();
        }

        // --- Drag-Drop API used by InventorySlotView ---

        public void BeginDrag(InventorySlotView source, PointerEventData eventData)
        {
            if (source.Item == null) return;

            _dragSource = source;

            // Create ghost icon that follows the pointer
            _dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _dragGhost.transform.SetParent(_rootCanvas != null ? _rootCanvas.transform : transform.root, false);

            var ghostImage = _dragGhost.GetComponent<Image>();
            ghostImage.sprite = source.Item.Data.Icon;
            ghostImage.raycastTarget = false;

            var ghostRect = _dragGhost.GetComponent<RectTransform>();
            ghostRect.sizeDelta = new Vector2(50f, 50f);

            var ghostGroup = _dragGhost.GetComponent<CanvasGroup>();
            ghostGroup.alpha = 0.75f;
            ghostGroup.blocksRaycasts = false;

            // Dim the source slot
            source.SetDragVisual(true);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (_dragGhost == null) return;
            _dragGhost.transform.position = eventData.position;
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (_dragSource == null) { CancelDrag(); return; }

            // Check if dropped on a valid slot
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            bool handled = false;
            foreach (var result in results)
            {
                var targetSlot = result.gameObject.GetComponent<InventorySlotView>();
                if (targetSlot != null && targetSlot != _dragSource)
                {
                    HandleDrop(_dragSource, targetSlot);
                    handled = true;
                    break;
                }
            }

            // If dropped outside all slots — check if outside the inventory panel entirely
            if (!handled && _dropZone != null)
            {
                bool insidePanel = RectTransformUtility.RectangleContainsScreenPoint(
                    _dropZone, eventData.position, eventData.pressEventCamera);

                if (!insidePanel && _inventory != null && _dragSource.Item != null)
                {
                    UISfx.Instance?.PlayItemDrop();
                    _inventory.DropItem(_dragSource.Item);
                }
            }

            CancelDrag();
        }

        private void HandleDrop(InventorySlotView source, InventorySlotView target)
        {
            if (_inventory == null) return;

            int indexA = _inventory.IndexOf(source.Item);
            int indexB = _inventory.IndexOf(target.Item);
            _inventory.SwapItems(indexA, indexB);
        }

        private void CancelDrag()
        {
            if (_dragGhost != null)
                Destroy(_dragGhost);
            _dragGhost = null;

            if (_dragSource != null)
                _dragSource.SetDragVisual(false);
            _dragSource = null;
        }

        // --- Click actions (still supported alongside drag) ---

        public void HandleSlotClicked(ItemInstance item)
        {
            if (item == null) return;

            if (item.Data is EquipmentData)
            {
                UISfx.Instance?.PlayItemEquip();
                _inventory?.EquipItem(item);
            }
            else if (item.Data is ConsumableData)
            {
                UISfx.Instance?.PlayClick();
                _inventory?.UseConsumable(item);
            }

            OnItemClicked?.Invoke(item);
        }

        public void HandleSlotHoverEnter(ItemInstance item)
        {
            if (_tooltip != null && item != null)
                _tooltip.Show(item);
        }

        public void HandleSlotHoverExit(ItemInstance item)
        {
            if (_tooltip != null)
                _tooltip.Hide();
        }

        private void HandleToggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                    Open(player.Inventory);
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= RefreshSlots;
        }
    }

    public class InventorySlotView : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _stackText;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private CanvasGroup _slotCanvasGroup;

        private ItemInstance _item;
        private InventoryUI _parentUI;
        private bool _isDragging;
        private Vector3 _originalScale;

        public ItemInstance Item => _item;

        public void Setup(ItemInstance item, InventoryUI parentUI)
        {
            _item = item;
            _parentUI = parentUI;
            _originalScale = transform.localScale;

            if (_iconImage == null)
                _iconImage = GetComponentInChildren<Image>();

            if (_iconImage != null && item.Data.Icon != null)
            {
                _iconImage.sprite = item.Data.Icon;
                _iconImage.enabled = true;
            }

            if (_stackText != null)
            {
                _stackText.text = item.StackCount > 1 ? item.StackCount.ToString() : string.Empty;
            }

            if (_rarityBorder != null)
            {
                _rarityBorder.color = UIColors.GetRarityColor(item.Data.Rarity);
            }

            if (_slotCanvasGroup == null)
                _slotCanvasGroup = GetComponent<CanvasGroup>();
        }

        public void SetDragVisual(bool dragging)
        {
            if (_slotCanvasGroup != null)
                _slotCanvasGroup.alpha = dragging ? 0.4f : 1f;
        }

        // --- Pointer Events ---

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isDragging) return;
            _parentUI?.HandleSlotClicked(_item);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _parentUI?.HandleSlotHoverEnter(_item);
            UISfx.Instance?.PlayHover();
            transform.localScale = _originalScale * 1.05f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _parentUI?.HandleSlotHoverExit(_item);
            transform.localScale = _originalScale;
        }

        // --- Drag Events ---

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _parentUI?.BeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _parentUI?.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _parentUI?.EndDrag(eventData);
            _isDragging = false;
        }
    }
}
