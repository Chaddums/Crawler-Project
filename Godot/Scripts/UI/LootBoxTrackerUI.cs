using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// HUD widget showing loot boxes collected this run.
    /// Lower tiers stack with a count badge. Relic caches get their own
    /// glowing slot with the relic's unique color.
    /// Sits on the left side below health/mana/xp bars.
    /// </summary>
    public partial class LootBoxTrackerUI : Control
    {
        private HBoxContainer _tierRow;
        private HBoxContainer _relicRow;
        private readonly Dictionary<LootBoxTier, TierSlot> _tierSlots = new();
        private readonly List<RelicSlot> _relicSlots = new();

        private struct TierSlot
        {
            public PanelContainer Panel;
            public Label CountLabel;
            public int Count;
        }

        private struct RelicSlot
        {
            public PanelContainer Panel;
            public string RelicId;
        }

        public override void _Ready()
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            AddChild(vbox);

            // Header
            var header = new Label();
            header.Text = "LOOT";
            header.AddThemeFontSizeOverride("font_size", 11);
            header.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.4f));
            vbox.AddChild(header);

            // Tier row — small stacked icons for Bronze through Legendary
            _tierRow = new HBoxContainer();
            _tierRow.AddThemeConstantOverride("separation", 3);
            vbox.AddChild(_tierRow);

            BuildTierSlot(LootBoxTier.Bronze, new Color(0.8f, 0.5f, 0.2f), "B");
            BuildTierSlot(LootBoxTier.Silver, new Color(0.8f, 0.8f, 0.9f), "S");
            BuildTierSlot(LootBoxTier.Gold, new Color(1f, 0.84f, 0f), "G");
            BuildTierSlot(LootBoxTier.Diamond, new Color(0.4f, 0.9f, 1f), "D");
            BuildTierSlot(LootBoxTier.Legendary, new Color(0.7f, 0.3f, 0.9f), "L");
            BuildTierSlot(LootBoxTier.Celestial, new Color(1f, 0.95f, 0.7f), "C");

            // Relic row — individual unique items
            _relicRow = new HBoxContainer();
            _relicRow.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(_relicRow);

            GameEvents.OnLootBoxOpened += OnLootBoxOpened;
            GameEvents.OnRelicCacheCollected += OnRelicCollected;
        }

        private void BuildTierSlot(LootBoxTier tier, Color color, string shortLabel)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(32, 32);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.8f);
            style.BorderColor = color * new Color(1, 1, 1, 0.4f);
            style.BorderWidthBottom = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            panel.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 0);
            panel.AddChild(vbox);

            // Tier letter
            var tierLabel = new Label();
            tierLabel.Text = shortLabel;
            tierLabel.AddThemeFontSizeOverride("font_size", 10);
            tierLabel.AddThemeColorOverride("font_color", color);
            tierLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(tierLabel);

            // Count
            var countLabel = new Label();
            countLabel.Text = "0";
            countLabel.AddThemeFontSizeOverride("font_size", 12);
            countLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            countLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(countLabel);

            _tierRow.AddChild(panel);

            _tierSlots[tier] = new TierSlot
            {
                Panel = panel,
                CountLabel = countLabel,
                Count = 0
            };
        }

        private void OnLootBoxOpened(LootBoxOpenedData data)
        {
            IncrementTier(data.Tier);
        }

        private void OnRelicCollected(RelicData relic)
        {
            AddRelicCache(relic);
        }

        public void IncrementTier(LootBoxTier tier)
        {
            if (!_tierSlots.TryGetValue(tier, out var slot)) return;

            slot.Count++;
            slot.CountLabel.Text = slot.Count.ToString();

            // Brighten the count color
            var tierColor = GetTierColor(tier);
            slot.CountLabel.AddThemeColorOverride("font_color", tierColor);

            _tierSlots[tier] = slot;

            // Pop animation
            AnimatePop(slot.Panel);
        }

        /// <summary>
        /// Add a unique relic cache to the tracker with its glow color.
        /// Called from RelicCacheUI when a relic is collected.
        /// </summary>
        public void AddRelicCache(RelicData relic)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(36, 36);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(relic.GlowColor.R * 0.2f, relic.GlowColor.G * 0.2f, relic.GlowColor.B * 0.2f, 0.9f);
            style.BorderColor = relic.GlowColor;
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
            panel.AddThemeStyleboxOverride("panel", style);

            // Relic initial (first letter of name)
            var label = new Label();
            label.Text = relic.ItemName.Length > 0 ? relic.ItemName[..1] : "?";
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_color", relic.GlowColor);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            panel.AddChild(label);

            // Tooltip on hover
            panel.TooltipText = $"{relic.ItemName}\n{relic.Description}";

            _relicRow.AddChild(panel);
            _relicSlots.Add(new RelicSlot { Panel = panel, RelicId = relic.Id });

            // Pop + glow pulse animation
            AnimatePop(panel);
            AnimateGlowPulse(panel, style, relic.GlowColor);
        }

        private void AnimatePop(Control target)
        {
            target.PivotOffset = target.CustomMinimumSize / 2;
            target.Scale = new Vector2(0.3f, 0.3f);
            var tween = CreateTween();
            tween.TweenProperty(target, "scale", new Vector2(1.2f, 1.2f), 0.15f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(target, "scale", Vector2.One, 0.1f);
        }

        private void AnimateGlowPulse(PanelContainer panel, StyleBoxFlat style, Color glowColor)
        {
            // Brief bright border pulse then settle
            var brightColor = new Color(
                Mathf.Min(1f, glowColor.R * 1.5f),
                Mathf.Min(1f, glowColor.G * 1.5f),
                Mathf.Min(1f, glowColor.B * 1.5f));

            var tween = CreateTween();
            tween.TweenMethod(
                Callable.From((Color c) => { style.BorderColor = c; }),
                brightColor, glowColor, 0.6f)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private static Color GetTierColor(LootBoxTier tier) => tier switch
        {
            LootBoxTier.Bronze => new Color(0.8f, 0.5f, 0.2f),
            LootBoxTier.Silver => new Color(0.8f, 0.8f, 0.9f),
            LootBoxTier.Gold => new Color(1f, 0.84f, 0f),
            LootBoxTier.Diamond => new Color(0.4f, 0.9f, 1f),
            LootBoxTier.Legendary => new Color(0.7f, 0.3f, 0.9f),
            LootBoxTier.Celestial => new Color(1f, 0.95f, 0.7f),
            _ => Colors.White
        };

        public override void _ExitTree()
        {
            GameEvents.OnLootBoxOpened -= OnLootBoxOpened;
            GameEvents.OnRelicCacheCollected -= OnRelicCollected;
        }
    }
}
