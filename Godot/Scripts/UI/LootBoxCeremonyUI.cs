using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Full-screen overlay for dramatic loot box opening ceremony.
    /// Dims screen, shakes box, bursts with particles, reveals items one-by-one.
    /// </summary>
    public partial class LootBoxCeremonyUI : CanvasLayer
    {
        private ColorRect _dimOverlay;
        private ColorRect _flashOverlay;
        private Control _root;
        private PanelContainer _boxVisual;
        private VBoxContainer _itemList;
        private Label _collectPrompt;
        private List<ItemInstance> _revealedItems = new();
        private LootBoxTier _tier;

        /// <summary>
        /// Fired after the player collects all items and the ceremony fades out.
        /// </summary>
        public event Action CeremonyCollected;

        public override void _Ready()
        {
            Layer = 60;
        }

        public void StartCeremony(LootBoxData boxData)
        {
            _tier = boxData.Tier;
            _revealedItems = LootBoxFactory.OpenLootBox(boxData);

            BuildUI();
            AnimateOpening();
        }

        private void BuildUI()
        {
            _root = new Control();
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            // Dim overlay
            _dimOverlay = new ColorRect();
            _dimOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _dimOverlay.Color = new Color(0, 0, 0, 0f);
            _dimOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
            _root.AddChild(_dimOverlay);

            // Flash overlay (white, transparent, above dim)
            _flashOverlay = new ColorRect();
            _flashOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _flashOverlay.Color = new Color(1, 1, 1, 0f);
            _flashOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _root.AddChild(_flashOverlay);

            // Box visual — centered
            _boxVisual = new PanelContainer();
            _boxVisual.CustomMinimumSize = new Vector2(120, 120);

            var tierColor = GetTierColor(_tier);
            var boxStyle = new StyleBoxFlat();
            boxStyle.BgColor = tierColor * new Color(1, 1, 1, 0.3f);
            boxStyle.BorderColor = tierColor;
            boxStyle.BorderWidthBottom = 3;
            boxStyle.BorderWidthTop = 3;
            boxStyle.BorderWidthLeft = 3;
            boxStyle.BorderWidthRight = 3;
            boxStyle.CornerRadiusBottomLeft = 8;
            boxStyle.CornerRadiusBottomRight = 8;
            boxStyle.CornerRadiusTopLeft = 8;
            boxStyle.CornerRadiusTopRight = 8;
            _boxVisual.AddThemeStyleboxOverride("panel", boxStyle);

            var boxLabel = new Label();
            boxLabel.Text = StringLoader.Get("ui.lootBox.boxLabel", ("{tier}", _tier.ToString()));
            boxLabel.HorizontalAlignment = HorizontalAlignment.Center;
            boxLabel.VerticalAlignment = VerticalAlignment.Center;
            boxLabel.AddThemeFontSizeOverride("font_size", 18);
            boxLabel.AddThemeColorOverride("font_color", tierColor);
            _boxVisual.AddChild(boxLabel);

            var viewport = GetViewport().GetVisibleRect().Size;
            _boxVisual.Position = new Vector2(viewport.X / 2 - 60, viewport.Y / 2 - 60);
            _root.AddChild(_boxVisual);

            // Item list (hidden initially)
            _itemList = new VBoxContainer();
            _itemList.Position = new Vector2(viewport.X / 2 - 200, viewport.Y / 2 - 80);
            _itemList.CustomMinimumSize = new Vector2(400, 0);
            _itemList.AddThemeConstantOverride("separation", 6);
            _itemList.Visible = false;
            _root.AddChild(_itemList);

            // Collect prompt
            _collectPrompt = new Label();
            _collectPrompt.Text = StringLoader.Get("ui.lootBox.collectPrompt");
            _collectPrompt.AddThemeFontSizeOverride("font_size", 20);
            _collectPrompt.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            _collectPrompt.HorizontalAlignment = HorizontalAlignment.Center;
            _collectPrompt.Position = new Vector2(viewport.X / 2 - 100, viewport.Y / 2 + 160);
            _collectPrompt.Visible = false;
            _root.AddChild(_collectPrompt);
        }

        private void AnimateOpening()
        {
            var tween = CreateTween();

            // 1. Dim screen
            tween.TweenProperty(_dimOverlay, "color:a", 0.7f, 0.3f);

            // 2. Box appears (scale up)
            _boxVisual.Scale = Vector2.Zero;
            _boxVisual.PivotOffset = new Vector2(60, 60);
            tween.TweenProperty(_boxVisual, "scale", Vector2.One, 0.3f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);

            // 3. Tier-scaled shake
            float shakeDuration = _tier switch
            {
                LootBoxTier.Bronze => 0.6f,
                LootBoxTier.Silver => 0.8f,
                LootBoxTier.Gold => 1.0f,
                LootBoxTier.Diamond => 1.4f,
                LootBoxTier.Legendary => 1.8f,
                _ => 1.0f
            };
            tween.TweenCallback(Callable.From(() => ShakeBox(shakeDuration)));
            tween.TweenInterval(shakeDuration);

            // 4. Box bursts — hide box, show items, screen flash + camera shake
            tween.TweenCallback(Callable.From(() =>
            {
                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("box_open");

                // Screen flash — alpha scales by tier
                float flashAlpha = _tier switch
                {
                    LootBoxTier.Bronze => 0.1f,
                    LootBoxTier.Silver => 0.2f,
                    LootBoxTier.Gold => 0.3f,
                    LootBoxTier.Diamond => 0.4f,
                    LootBoxTier.Legendary => 0.5f,
                    _ => 0.2f
                };
                var flashTween = CreateTween();
                flashTween.TweenProperty(_flashOverlay, "color:a", flashAlpha, 0.05f);
                flashTween.TweenProperty(_flashOverlay, "color:a", 0f, 0.3f);

                // Camera shake for Diamond+
                if (_tier >= LootBoxTier.Diamond && ServiceLocator.TryGet<IsometricCamera>(out var camera))
                {
                    float trauma = _tier == LootBoxTier.Legendary ? 0.5f : 0.3f;
                    camera.Shake(trauma);
                }

                // Burst scale
                var burst = CreateTween();
                burst.TweenProperty(_boxVisual, "scale", new Vector2(1.5f, 1.5f), 0.1f);
                burst.TweenProperty(_boxVisual, "modulate:a", 0f, 0.15f);
                burst.TweenCallback(Callable.From(() => _boxVisual.Visible = false));
            }));

            tween.TweenInterval(0.3f);

            // 5. Reveal items one by one
            tween.TweenCallback(Callable.From(() =>
            {
                _itemList.Visible = true;
                RevealItems();
            }));
        }

        private void ShakeBox(float duration)
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("box_shake");

            var basePos = _boxVisual.Position;
            var shakeTween = CreateTween();
            int shakeSteps = (int)(duration / 0.05f);

            for (int i = 0; i < shakeSteps; i++)
            {
                float intensity = 2f + (float)i / shakeSteps * 8f; // Increasing intensity
                float offsetX = (float)GD.RandRange(-intensity, intensity);
                float offsetY = (float)GD.RandRange(-intensity, intensity);
                shakeTween.TweenProperty(_boxVisual, "position",
                    basePos + new Vector2(offsetX, offsetY), 0.05f);
            }
            shakeTween.TweenProperty(_boxVisual, "position", basePos, 0.05f);
        }

        private void RevealItems()
        {
            // Tier-scaled stagger between item reveals
            float stagger = _tier switch
            {
                LootBoxTier.Bronze => 0.4f,
                LootBoxTier.Silver => 0.5f,
                LootBoxTier.Gold => 0.6f,
                LootBoxTier.Diamond => 0.75f,
                LootBoxTier.Legendary => 0.9f,
                _ => 0.4f
            };

            float delay = 0f;
            int total = _revealedItems.Count;

            for (int idx = 0; idx < total; idx++)
            {
                var item = _revealedItems[idx];
                int capturedIdx = idx;

                var itemPanel = CreateItemRevealPanel(item);
                itemPanel.Modulate = new Color(1, 1, 1, 0);
                _itemList.AddChild(itemPanel);

                var itemTween = CreateTween();
                itemTween.TweenInterval(delay);
                itemTween.TweenCallback(Callable.From(() =>
                {
                    if (ServiceLocator.TryGet<AudioManager>(out var audio))
                        audio.PlaySFXByName("item_reveal");

                    // Per-item narration
                    string narration = BuildItemNarration(item, capturedIdx, total);
                    if (narration != null)
                    {
                        if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                            commentary.QueueLine(narration.StartsWith("BIT:") ? "BIT" : "AXIS",
                                narration, CommentaryPriority.High, CommentaryCategory.LootReaction);
                        TtsHelper.Speak(narration);
                    }
                }));
                itemTween.TweenProperty(itemPanel, "modulate:a", 1f, 0.3f);

                delay += stagger;
            }

            // Show collect prompt after all items revealed
            var promptTween = CreateTween();
            promptTween.TweenInterval(delay + 0.3f);
            promptTween.TweenCallback(Callable.From(() =>
            {
                _collectPrompt.Visible = true;
                _dimOverlay.GuiInput += OnCollectClick;
            }));
        }

        private static string BuildItemNarration(ItemInstance item, int index, int total)
        {
            bool isFirst = index == 0;
            bool isLast = index == total - 1;
            bool isEpicPlus = item.Rarity >= ItemRarity.Epic;

            // AXIS narrates first, last, and Epic+ items; BIT narrates the rest
            bool isAxis = isFirst || isLast || isEpicPlus;
            string speaker = isAxis ? "AXIS" : "BIT";

            // Build affix readout
            string affixText = "";
            if (item.Affixes.Count > 0)
            {
                var parts = item.Affixes.Select(a =>
                    a.Data.ModType == ModifierType.Percent
                        ? $"+{a.RolledValue:F0}% {a.Data.Stat}"
                        : $"+{a.RolledValue:F0} {a.Data.Stat}");
                affixText = $" ({string.Join(", ", parts)})";
            }

            string name = item.GetDisplayName();

            if (!isFirst && !isLast && !isEpicPlus)
                return StringLoader.Get("lootNarration.normalItem", ("{name}", name), ("{affixes}", affixText));

            if (isEpicPlus)
                return StringLoader.Get("lootNarration.epicItem", ("{rarity}", item.Rarity.ToString()), ("{name}", name), ("{affixes}", affixText));

            if (isFirst)
                return StringLoader.Get("lootNarration.firstItem", ("{name}", name), ("{affixes}", affixText));

            // isLast
            return StringLoader.Get("lootNarration.lastItem", ("{name}", name), ("{affixes}", affixText));
        }

        private PanelContainer CreateItemRevealPanel(ItemInstance item)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(420, 44);

            var rarityColor = GetRarityColor(item.Rarity);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            style.BorderColor = rarityColor;
            style.BorderWidthLeft = 3;
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            panel.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 10);
            vbox.AddChild(hbox);

            // Rarity dot
            var dot = new Label();
            dot.Text = "*";
            dot.AddThemeFontSizeOverride("font_size", 16);
            dot.AddThemeColorOverride("font_color", rarityColor);
            hbox.AddChild(dot);

            // Item name
            var nameLabel = new Label();
            nameLabel.Text = item.GetDisplayName();
            nameLabel.AddThemeFontSizeOverride("font_size", 15);
            nameLabel.AddThemeColorOverride("font_color", rarityColor);
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(nameLabel);

            // Rarity label
            var rarityLabel = new Label();
            rarityLabel.Text = item.Rarity.ToString();
            rarityLabel.AddThemeFontSizeOverride("font_size", 12);
            rarityLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            hbox.AddChild(rarityLabel);

            // Affix summary (if item has affixes)
            if (item.Affixes.Count > 0)
            {
                var affixParts = item.Affixes.Select(a =>
                    a.Data.ModType == ModifierType.Percent
                        ? $"+{a.RolledValue:F0}% {a.Data.Stat}"
                        : $"+{a.RolledValue:F0} {a.Data.Stat}");

                var affixLabel = new Label();
                affixLabel.Text = string.Join(", ", affixParts);
                affixLabel.AddThemeFontSizeOverride("font_size", 11);
                affixLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.6f, 1f));
                vbox.AddChild(affixLabel);
            }

            return panel;
        }

        private void OnCollectClick(InputEvent ev)
        {
            if (ev is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
                return;

            CollectAll();
        }

        private void CollectAll()
        {
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            foreach (var item in _revealedItems)
                player.Inventory.TryAddItem(item);

            // Fire event
            var openedData = new LootBoxOpenedData
            {
                Tier = _tier,
                Items = new List<object>(_revealedItems.ConvertAll(i => (object)i))
            };
            GameEvents.OnLootBoxOpened?.Invoke(openedData);

            // Fade out and clean up
            var tween = CreateTween();
            tween.TweenProperty(_root, "modulate:a", 0f, 0.3f);
            tween.TweenCallback(Callable.From(() =>
            {
                CeremonyCollected?.Invoke();
                QueueFree();
            }));
        }

        private static Color GetTierColor(LootBoxTier tier)
        {
            return tier switch
            {
                LootBoxTier.Bronze => new Color(0.8f, 0.5f, 0.2f),
                LootBoxTier.Silver => new Color(0.8f, 0.8f, 0.9f),
                LootBoxTier.Gold => new Color(1f, 0.84f, 0f),
                LootBoxTier.Diamond => new Color(0.4f, 0.9f, 1f),
                LootBoxTier.Legendary => new Color(0.7f, 0.3f, 0.9f),
                _ => Colors.White
            };
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.7f, 0.7f, 0.7f),
                ItemRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Epic => new Color(0.7f, 0.3f, 0.9f),
                ItemRarity.Legendary => new Color(1f, 0.5f, 0f),
                ItemRarity.Absurd => new Color(1f, 0.2f, 0.4f),
                _ => Colors.White
            };
        }
    }
}
