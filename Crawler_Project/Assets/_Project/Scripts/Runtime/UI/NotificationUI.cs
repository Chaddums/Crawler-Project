using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Toast notification system. Listens to game events and shows
    /// auto-dismissing notifications for pickups, level ups, XP gains, etc.
    /// </summary>
    public class NotificationUI : MonoBehaviour
    {
        public static NotificationUI Instance { get; private set; }

        [Header("Layout")]
        [SerializeField] private Transform _container;
        [SerializeField] private GameObject _notificationPrefab;

        [Header("Timing")]
        [SerializeField] private float _fadeInDuration = 0.25f;
        [SerializeField] private float _holdDuration = 3f;
        [SerializeField] private float _fadeOutDuration = 0.4f;

        [Header("Limits")]
        [SerializeField] private int _maxVisible = 4;

        private readonly List<GameObject> _active = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.OnItemPickedUp += HandleItemPickedUp;
            GameEvents.OnPlayerLevelUp += HandleLevelUp;
            GameEvents.OnExperienceGained += HandleExperienceGained;
            GameEvents.OnAbilityUnlocked += HandleAbilityUnlocked;
        }

        private void OnDisable()
        {
            GameEvents.OnItemPickedUp -= HandleItemPickedUp;
            GameEvents.OnPlayerLevelUp -= HandleLevelUp;
            GameEvents.OnExperienceGained -= HandleExperienceGained;
            GameEvents.OnAbilityUnlocked -= HandleAbilityUnlocked;
        }

        private void HandleItemPickedUp(ScriptableObject itemData)
        {
            if (itemData is ItemData data)
            {
                string colorHex = UIColors.GetRarityColorHex(data.Rarity);
                ShowNotification($"Picked up <color={colorHex}>{data.ItemName}</color>", data.Icon);
                UISfx.Instance?.PlayItemPickup();
            }
        }

        private void HandleLevelUp(int newLevel)
        {
            ShowNotification($"<color=#FFD700>Level Up! You are now level {newLevel}</color>");
            UISfx.Instance?.PlayLevelUp();
        }

        private void HandleExperienceGained(int amount)
        {
            ShowNotification($"+{amount} XP");
        }

        private void HandleAbilityUnlocked(ScriptableObject abilityData)
        {
            if (abilityData is AbilityData data)
            {
                ShowNotification($"<color=#00CCFF>Ability Unlocked: {data.AbilityName}</color>", data.Icon);
                UISfx.Instance?.PlaySkillUnlock();
            }
        }

        public void ShowNotification(string message, Sprite icon = null)
        {
            if (_notificationPrefab == null || _container == null) return;

            // Enforce max visible — remove oldest
            while (_active.Count >= _maxVisible)
            {
                var oldest = _active[0];
                _active.RemoveAt(0);
                if (oldest != null)
                    Destroy(oldest);
            }

            GameObject notifObj = Instantiate(_notificationPrefab, _container);
            _active.Add(notifObj);

            // Setup text
            var text = notifObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = message;

            // Setup icon
            var iconImage = notifObj.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            StartCoroutine(NotificationLifecycle(notifObj));
        }

        private IEnumerator NotificationLifecycle(GameObject notifObj)
        {
            var cg = notifObj.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = notifObj.AddComponent<CanvasGroup>();

            // Fade in
            cg.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                if (notifObj == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                yield return null;
            }
            cg.alpha = 1f;

            // Hold
            yield return new WaitForSecondsRealtime(_holdDuration);
            if (notifObj == null) yield break;

            // Fade out
            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                if (notifObj == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);
                yield return null;
            }

            _active.Remove(notifObj);
            if (notifObj != null)
                Destroy(notifObj);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
