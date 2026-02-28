using System.Text;
using TMPro;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class TooltipUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _statsText;
        [SerializeField] private TextMeshProUGUI _flavorText;

        [Header("Positioning")]
        [SerializeField] private Vector2 _offset = new Vector2(16f, -16f);
        [SerializeField] private Canvas _parentCanvas;

        private bool _isVisible;
        private RectTransform _canvasRect;

        private void Awake()
        {
            if (_parentCanvas != null)
                _canvasRect = _parentCanvas.GetComponent<RectTransform>();

            Hide();
        }

        private void Update()
        {
            if (_isVisible)
                FollowMouse();
        }

        public void Show(ItemInstance item)
        {
            if (item == null || item.Data == null)
            {
                Hide();
                return;
            }

            // Title with rarity color
            if (_titleText != null)
            {
                string colorHex = UIColors.GetRarityColorHex(item.Data.Rarity);
                string stackText = item.StackCount > 1 ? $" x{item.StackCount}" : string.Empty;
                _titleText.text = $"<color={colorHex}>{item.Data.ItemName}{stackText}</color>";
            }

            // Description
            if (_descriptionText != null)
                _descriptionText.text = item.Data.Description ?? string.Empty;

            // Stats
            if (_statsText != null)
            {
                var sb = new StringBuilder();

                if (item.Data is EquipmentData equipData)
                {
                    foreach (var stat in equipData.FixedStats)
                    {
                        AppendStatLine(sb, stat);
                    }
                }

                if (item.RolledStats != null && item.RolledStats.Count > 0)
                {
                    foreach (var stat in item.RolledStats)
                    {
                        AppendStatLine(sb, stat, true);
                    }
                }

                _statsText.text = sb.ToString();
                _statsText.gameObject.SetActive(sb.Length > 0);
            }

            // Flavor text
            if (_flavorText != null)
            {
                _flavorText.text = !string.IsNullOrEmpty(item.Data.FlavorText)
                    ? $"<i>{item.Data.FlavorText}</i>"
                    : string.Empty;
                _flavorText.gameObject.SetActive(!string.IsNullOrEmpty(item.Data.FlavorText));
            }

            ShowPanel();
        }

        public void Show(AbilityData ability)
        {
            if (ability == null)
            {
                Hide();
                return;
            }

            if (_titleText != null)
                _titleText.text = ability.AbilityName;

            if (_descriptionText != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine(ability.Description);
                sb.AppendLine();

                if (ability.BaseDamage > 0)
                    sb.AppendLine($"Damage: {ability.BaseDamage} ({ability.DamageType})");
                if (ability.Cooldown > 0)
                    sb.AppendLine($"Cooldown: {ability.Cooldown:F1}s");
                if (ability.ManaCost > 0)
                    sb.AppendLine($"Mana Cost: {ability.ManaCost:F0}");
                if (ability.Range > 0)
                    sb.AppendLine($"Range: {ability.Range:F1}");

                _descriptionText.text = sb.ToString();
            }

            if (_statsText != null)
            {
                _statsText.text = string.Empty;
                _statsText.gameObject.SetActive(false);
            }

            if (_flavorText != null)
            {
                _flavorText.text = !string.IsNullOrEmpty(ability.FlavorText)
                    ? $"<i>{ability.FlavorText}</i>"
                    : string.Empty;
                _flavorText.gameObject.SetActive(!string.IsNullOrEmpty(ability.FlavorText));
            }

            ShowPanel();
        }

        /// <summary>
        /// Text-only tooltip overload. Hides stats and flavor text, shows only title + description.
        /// Useful for stat descriptions, hover info, etc.
        /// </summary>
        public void Show(string title, string description)
        {
            if (_titleText != null)
                _titleText.text = title ?? string.Empty;

            if (_descriptionText != null)
                _descriptionText.text = description ?? string.Empty;

            if (_statsText != null)
            {
                _statsText.text = string.Empty;
                _statsText.gameObject.SetActive(false);
            }

            if (_flavorText != null)
            {
                _flavorText.text = string.Empty;
                _flavorText.gameObject.SetActive(false);
            }

            ShowPanel();
        }

        public void Hide()
        {
            _isVisible = false;

            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        private void ShowPanel()
        {
            _isVisible = true;

            if (_panel != null)
                _panel.gameObject.SetActive(true);

            FollowMouse();
        }

        private void FollowMouse()
        {
            if (_panel == null) return;

            Vector2 mousePos = Input.mousePosition;
            Vector2 anchoredPos = mousePos + _offset;

            // Clamp to screen bounds
            if (_canvasRect != null)
            {
                Vector2 panelSize = _panel.sizeDelta;
                float canvasWidth = _canvasRect.rect.width;
                float canvasHeight = _canvasRect.rect.height;

                if (anchoredPos.x + panelSize.x > canvasWidth)
                    anchoredPos.x = mousePos.x - panelSize.x - _offset.x;

                if (anchoredPos.y - panelSize.y < 0)
                    anchoredPos.y = mousePos.y + panelSize.y + Mathf.Abs(_offset.y);
            }

            _panel.position = anchoredPos;
        }

        private void AppendStatLine(StringBuilder sb, StatModifier mod, bool isRolled = false)
        {
            string prefix = mod.Value >= 0 ? "+" : string.Empty;
            string suffix = mod.ModType == ModifierType.Percent ? "%" : string.Empty;
            string color = isRolled ? "#66CCFF" : "#CCCCCC";
            sb.AppendLine($"<color={color}>{prefix}{mod.Value:F1}{suffix} {FormatStatName(mod.StatType)}</color>");
        }

        private string FormatStatName(StatType type)
        {
            switch (type)
            {
                case StatType.MaxHealth: return "Max Health";
                case StatType.MaxMana: return "Max Mana";
                case StatType.CritChance: return "Crit Chance";
                case StatType.CritDamage: return "Crit Damage";
                case StatType.AttackSpeed: return "Attack Speed";
                case StatType.MoveSpeed: return "Move Speed";
                case StatType.CooldownReduction: return "Cooldown Reduction";
                default: return type.ToString();
            }
        }
    }
}
