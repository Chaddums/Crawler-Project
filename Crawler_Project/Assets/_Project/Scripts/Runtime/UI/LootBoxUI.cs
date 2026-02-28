using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class LootBoxUI : UIPanel, IExclusivePanel
    {
        [Header("Item Display")]
        [SerializeField] private Transform _itemSlotContainer;
        [SerializeField] private GameObject _itemSlotPrefab;

        [Header("Controls")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Timing")]
        [SerializeField] private float _itemRevealDelay = 0.5f;
        [SerializeField] private float _itemRevealDuration = 0.4f;

        [Header("Visual")]
        [SerializeField] private float _itemPunchScale = 1.3f;

        private readonly List<GameObject> _spawnedSlots = new();
        private Coroutine _revealCoroutine;

        protected override void Awake()
        {
            base.Awake();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            SetVisibleImmediate(false);
        }

        private void OnEnable()
        {
            GameEvents.OnLootBoxOpened += HandleLootBoxOpened;
        }

        private void OnDisable()
        {
            GameEvents.OnLootBoxOpened -= HandleLootBoxOpened;
        }

        private void HandleLootBoxOpened(LootBoxOpenedData data)
        {
            var items = data.Items?.OfType<ItemInstance>().ToList();
            Open(items, data.Tier);
        }

        public void Open(List<ItemInstance> items, LootBoxTier tier = LootBoxTier.Bronze)
        {
            if (items == null || items.Count == 0) return;

            if (_titleText != null)
                _titleText.text = $"{tier} Loot Box";

            ClearSlots();

            if (_revealCoroutine != null)
                StopCoroutine(_revealCoroutine);

            _revealCoroutine = StartCoroutine(RevealItemsCoroutine(items));
        }

        public void Close()
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }

            ClearSlots();
            Hide();
        }

        private IEnumerator RevealItemsCoroutine(List<ItemInstance> items)
        {
            // Show panel with base class animation
            if (_closeButton != null)
                _closeButton.interactable = false;
            Show();

            // Wait for show animation
            yield return new WaitForSecondsRealtime(0.2f);

            // Pre-create all slots (hidden)
            var slotData = new List<(GameObject obj, Image icon, Image border, TextMeshProUGUI name, CanvasGroup cg, ItemInstance item)>();

            foreach (var item in items)
            {
                if (_itemSlotPrefab == null || _itemSlotContainer == null) continue;

                GameObject slotObj = Instantiate(_itemSlotPrefab, _itemSlotContainer);
                _spawnedSlots.Add(slotObj);

                var icon = slotObj.transform.Find("Icon")?.GetComponent<Image>();
                var border = slotObj.transform.Find("Border")?.GetComponent<Image>();
                var nameText = slotObj.GetComponentInChildren<TextMeshProUGUI>();
                var cg = slotObj.GetComponent<CanvasGroup>();

                if (cg == null)
                    cg = slotObj.AddComponent<CanvasGroup>();

                cg.alpha = 0f;

                // Setup item data
                if (icon != null && item.Data.Icon != null)
                {
                    icon.sprite = item.Data.Icon;
                    icon.enabled = true;
                }

                if (border != null)
                    border.color = UIColors.GetRarityColor(item.Data.Rarity);

                if (nameText != null)
                {
                    string colorHex = UIColors.GetRarityColorHex(item.Data.Rarity);
                    nameText.text = $"<color={colorHex}>{item.Data.ItemName}</color>";
                }

                slotData.Add((slotObj, icon, border, nameText, cg, item));
            }

            // Reveal items one by one with dramatic timing
            foreach (var (obj, icon, border, nameText, cg, item) in slotData)
            {
                yield return new WaitForSeconds(_itemRevealDelay);
                UISfx.Instance?.PlayLootReveal(item.Data.Rarity);
                yield return StartCoroutine(RevealSingleItem(obj, cg));
            }

            // Enable close button after all items revealed
            if (_closeButton != null)
                _closeButton.interactable = true;
        }

        private IEnumerator RevealSingleItem(GameObject slotObj, CanvasGroup cg)
        {
            var rectTransform = slotObj.GetComponent<RectTransform>();
            Vector3 originalScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
            float elapsed = 0f;

            while (elapsed < _itemRevealDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _itemRevealDuration;

                // Fade in
                cg.alpha = Mathf.Clamp01(t * 2f); // Fade in during first half

                // Scale punch
                if (rectTransform != null)
                {
                    float scaleT = t < 0.5f
                        ? Mathf.Lerp(0.5f, _itemPunchScale, t * 2f)
                        : Mathf.Lerp(_itemPunchScale, 1f, (t - 0.5f) * 2f);
                    rectTransform.localScale = originalScale * scaleT;
                }

                yield return null;
            }

            cg.alpha = 1f;
            if (rectTransform != null)
                rectTransform.localScale = originalScale;
        }

        private void ClearSlots()
        {
            foreach (var slot in _spawnedSlots)
            {
                if (slot != null)
                    Destroy(slot);
            }
            _spawnedSlots.Clear();
        }

        private void OnDestroy()
        {
            ClearSlots();
        }
    }
}
