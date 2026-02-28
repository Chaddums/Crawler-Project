using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class CharacterSheetUI : UIPanel, IExclusivePanel
    {
        [Header("Identity")]
        [SerializeField] private TextMeshProUGUI _playerNameText;
        [SerializeField] private TextMeshProUGUI _classNameText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private Image _classIcon;

        [Header("Experience")]
        [SerializeField] private Image _experienceFill;
        [SerializeField] private TextMeshProUGUI _experienceText;

        [Header("Core Stats")]
        [SerializeField] private TextMeshProUGUI _strengthText;
        [SerializeField] private TextMeshProUGUI _dexterityText;
        [SerializeField] private TextMeshProUGUI _constitutionText;
        [SerializeField] private TextMeshProUGUI _intelligenceText;
        [SerializeField] private TextMeshProUGUI _charismaText;
        [SerializeField] private TextMeshProUGUI _luckText;

        [Header("Derived Stats")]
        [SerializeField] private TextMeshProUGUI _maxHealthText;
        [SerializeField] private TextMeshProUGUI _maxManaText;
        [SerializeField] private TextMeshProUGUI _armorText;
        [SerializeField] private TextMeshProUGUI _critChanceText;
        [SerializeField] private TextMeshProUGUI _critDamageText;
        [SerializeField] private TextMeshProUGUI _attackSpeedText;
        [SerializeField] private TextMeshProUGUI _moveSpeedText;
        [SerializeField] private TextMeshProUGUI _cooldownReductionText;

        [Header("Equipment Slots")]
        [SerializeField] private EquipmentSlotView[] _equipmentSlotViews;

        private PlayerController _player;

        private void OnEnable()
        {
            GameEvents.OnCharacterSheetToggled += HandleToggle;
            GameEvents.OnPlayerLevelUp += HandleLevelUp;
        }

        private void OnDisable()
        {
            GameEvents.OnCharacterSheetToggled -= HandleToggle;
            GameEvents.OnPlayerLevelUp -= HandleLevelUp;
        }

        private void Start()
        {
            SetVisibleImmediate(false);
        }

        public void Open()
        {
            if (!ServiceLocator.TryGet(out _player)) return;

            _player.Inventory.OnEquipmentChanged += HandleEquipmentChanged;
            Show();
            Refresh();
        }

        public void Close()
        {
            if (_player != null)
                _player.Inventory.OnEquipmentChanged -= HandleEquipmentChanged;

            Hide();
        }

        public void Refresh()
        {
            if (_player == null) return;

            RefreshIdentity();
            RefreshStats();
            RefreshEquipment();
        }

        private void RefreshIdentity()
        {
            if (_playerNameText != null)
                _playerNameText.text = _player.PlayerName ?? "Crawler";

            if (_classNameText != null)
                _classNameText.text = _player.CurrentClassData != null
                    ? _player.CurrentClassData.ClassName
                    : "Unclassed";

            if (_levelText != null)
                _levelText.text = $"Level {_player.Stats.Level}";

            if (_classIcon != null && _player.CurrentClassData != null)
            {
                _classIcon.sprite = _player.CurrentClassData.ClassIcon;
                _classIcon.enabled = _player.CurrentClassData.ClassIcon != null;
            }

            if (_experienceFill != null)
            {
                float expPercent = _player.Stats.ExperienceToNextLevel > 0
                    ? (float)_player.Stats.Experience / _player.Stats.ExperienceToNextLevel
                    : 0f;
                _experienceFill.fillAmount = expPercent;
            }

            if (_experienceText != null)
                _experienceText.text = $"{_player.Stats.Experience} / {_player.Stats.ExperienceToNextLevel} XP";
        }

        private void RefreshStats()
        {
            var stats = _player.Stats;

            SetStatText(_strengthText, StatType.Strength, stats);
            SetStatText(_dexterityText, StatType.Dexterity, stats);
            SetStatText(_constitutionText, StatType.Constitution, stats);
            SetStatText(_intelligenceText, StatType.Intelligence, stats);
            SetStatText(_charismaText, StatType.Charisma, stats);
            SetStatText(_luckText, StatType.Luck, stats);

            SetStatText(_maxHealthText, StatType.MaxHealth, stats);
            SetStatText(_maxManaText, StatType.MaxMana, stats);
            SetStatText(_armorText, StatType.Armor, stats);
            SetStatText(_critChanceText, StatType.CritChance, stats, true);
            SetStatText(_critDamageText, StatType.CritDamage, stats, true);
            SetStatText(_attackSpeedText, StatType.AttackSpeed, stats);
            SetStatText(_moveSpeedText, StatType.MoveSpeed, stats);
            SetStatText(_cooldownReductionText, StatType.CooldownReduction, stats, true);
        }

        private void SetStatText(TextMeshProUGUI text, StatType type, PlayerStats stats, bool asPercent = false)
        {
            if (text == null) return;

            float value = stats.GetStat(type);
            text.text = asPercent
                ? $"{value * 100f:F1}%"
                : $"{value:F1}";
        }

        private void RefreshEquipment()
        {
            if (_equipmentSlotViews == null) return;

            foreach (var view in _equipmentSlotViews)
            {
                if (view == null) continue;

                ItemInstance equipped = _player.Inventory.GetEquipped(view.Slot);
                view.SetItem(equipped);
            }
        }

        private void HandleToggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        private void HandleLevelUp(int newLevel)
        {
            if (IsOpen)
                Refresh();
        }

        private void HandleEquipmentChanged(EquipmentSlot slot)
        {
            if (IsOpen)
            {
                RefreshStats();
                RefreshEquipment();
            }
        }

        private void OnDestroy()
        {
            if (_player != null)
                _player.Inventory.OnEquipmentChanged -= HandleEquipmentChanged;
        }
    }

    [Serializable]
    public class EquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private EquipmentSlot _slot;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Image _slotBackground;
        [SerializeField] private TextMeshProUGUI _slotLabel;

        public EquipmentSlot Slot => _slot;

        public void SetItem(ItemInstance item)
        {
            if (item != null && item.Data.Icon != null)
            {
                _itemIcon.sprite = item.Data.Icon;
                _itemIcon.enabled = true;
                _itemIcon.color = Color.white;
            }
            else
            {
                _itemIcon.sprite = null;
                _itemIcon.enabled = false;
            }
        }
    }
}
