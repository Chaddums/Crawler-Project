using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class AbilityBarUI : MonoBehaviour
    {
        [SerializeField] private AbilitySlotUI[] _slots;

        private IReadOnlyList<AbilitySlot> _abilitySlots;

        private static readonly string[] DefaultHotkeys = { "1", "2", "3", "4", "5", "6" };

        public void Initialize(IReadOnlyList<AbilitySlot> abilitySlots)
        {
            _abilitySlots = abilitySlots;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < _abilitySlots.Count)
                {
                    _slots[i].Bind(_abilitySlots[i]);
                    string hotkey = i < DefaultHotkeys.Length ? DefaultHotkeys[i] : (i + 1).ToString();
                    _slots[i].SetHotkeyLabel(hotkey);
                }
                else
                {
                    _slots[i].Clear();
                }
            }
        }

        private void Update()
        {
            if (_abilitySlots == null) return;

            for (int i = 0; i < _slots.Length && i < _abilitySlots.Count; i++)
            {
                _slots[i].UpdateCooldown();
            }
        }
    }

    [Serializable]
    public class AbilitySlotUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TextMeshProUGUI _hotkeyLabel;

        [Header("Colors")]
        [SerializeField] private Color _readyColor = Color.white;
        [SerializeField] private Color _cooldownColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        [SerializeField] private Color _emptySlotColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

        private AbilitySlot _slot;

        public void Bind(AbilitySlot slot)
        {
            _slot = slot;

            if (_slot != null && _slot.Ability != null)
            {
                if (_icon != null)
                {
                    _icon.sprite = _slot.Ability.Icon;
                    _icon.color = _readyColor;
                    _icon.enabled = _slot.Ability.Icon != null;
                }

                if (_cooldownOverlay != null)
                {
                    _cooldownOverlay.fillAmount = 0f;
                    _cooldownOverlay.enabled = true;
                }
            }
            else
            {
                Clear();
            }
        }

        public void Clear()
        {
            _slot = null;

            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.color = _emptySlotColor;
                _icon.enabled = true;
            }

            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.fillAmount = 0f;
                _cooldownOverlay.enabled = false;
            }
        }

        public void SetHotkeyLabel(string text)
        {
            if (_hotkeyLabel != null)
                _hotkeyLabel.text = text;
        }

        public void UpdateCooldown()
        {
            if (_slot == null || _slot.Ability == null) return;

            float cdPercent = _slot.CooldownPercent;

            if (_cooldownOverlay != null)
                _cooldownOverlay.fillAmount = cdPercent;

            if (_icon != null)
                _icon.color = cdPercent > 0f ? _cooldownColor : _readyColor;
        }
    }
}
