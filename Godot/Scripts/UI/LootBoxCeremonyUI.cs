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
        private ItemRarity _bestRarity;
        private ColorRect _boxGlow;

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

            _bestRarity = ItemRarity.Common;
            foreach (var item in _revealedItems)
                if (item.Rarity > _bestRarity) _bestRarity = item.Rarity;

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

            // 2b. Glow aura behind box for Rare+ best rarity
            if (_bestRarity >= ItemRarity.Rare)
            {
                tween.TweenCallback(Callable.From(() => CreateBoxGlow()));
            }

            // 3. Tier-scaled shake with rarity-scaled intensity
            float shakeDuration = _tier switch
            {
                LootBoxTier.Bronze => 0.6f,
                LootBoxTier.Silver => 0.8f,
                LootBoxTier.Gold => 1.0f,
                LootBoxTier.Diamond => 1.4f,
                LootBoxTier.Legendary => 1.8f,
                LootBoxTier.Celestial => 2.5f,
                _ => 1.0f
            };
            float shakeMaxIntensity = _bestRarity switch
            {
                ItemRarity.Common => 6f,
                ItemRarity.Uncommon => 8f,
                ItemRarity.Rare => 10f,
                ItemRarity.Epic => 14f,
                _ => 18f // Legendary, Absurd
            };
            tween.TweenCallback(Callable.From(() => ShakeBox(shakeDuration, shakeMaxIntensity)));
            tween.TweenInterval(shakeDuration);

            // 4. Box bursts — tiered celebration via CelebrationVfxManager
            tween.TweenCallback(Callable.From(() =>
            {
                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("box_open");

                // Fire the tiered celebration in 3D space (light pillars, confetti, etc.)
                var celebTier = CelebrationVfxManager.TierFromLootBox(_tier, _bestRarity);
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                {
                    CelebrationVfxManager.Play(
                        GetTree().Root,
                        player.GlobalPosition + Vector3.Up * 0.5f,
                        celebTier);
                }

                // Destroy glow
                if (_boxGlow != null)
                {
                    _boxGlow.QueueFree();
                    _boxGlow = null;
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

        private void ShakeBox(float duration, float maxIntensity)
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("box_shake");

            var basePos = _boxVisual.Position;
            var shakeTween = CreateTween();
            int shakeSteps = (int)(duration / 0.05f);

            for (int i = 0; i < shakeSteps; i++)
            {
                float intensity = 2f + (float)i / shakeSteps * maxIntensity;
                float offsetX = (float)GD.RandRange(-intensity, intensity);
                float offsetY = (float)GD.RandRange(-intensity, intensity);
                shakeTween.TweenProperty(_boxVisual, "position",
                    basePos + new Vector2(offsetX, offsetY), 0.05f);
            }
            shakeTween.TweenProperty(_boxVisual, "position", basePos, 0.05f);
        }

        private void CreateBoxGlow()
        {
            float size = _bestRarity switch
            {
                ItemRarity.Rare => 160f,
                ItemRarity.Epic => 200f,
                ItemRarity.Legendary => 240f,
                ItemRarity.Absurd => 280f,
                _ => 160f
            };
            Color glowColor = _bestRarity switch
            {
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1f, 0.3f),
                ItemRarity.Epic => new Color(0.7f, 0.3f, 0.9f, 0.35f),
                ItemRarity.Legendary => new Color(1f, 0.5f, 0f, 0.4f),
                ItemRarity.Absurd => new Color(1f, 0.2f, 0.4f, 0.45f),
                _ => new Color(0.3f, 0.5f, 1f, 0.3f)
            };
            float pulseSpeed = _bestRarity switch
            {
                ItemRarity.Rare => 1.2f,
                ItemRarity.Epic => 0.8f,
                ItemRarity.Legendary => 0.5f,
                ItemRarity.Absurd => 0.35f,
                _ => 1.2f
            };
            float pulseMin = _bestRarity >= ItemRarity.Epic ? 0.2f : 0.15f;
            float pulseMax = _bestRarity >= ItemRarity.Legendary ? 0.6f : 0.5f;

            _boxGlow = new ColorRect();
            _boxGlow.CustomMinimumSize = new Vector2(size, size);
            _boxGlow.Size = new Vector2(size, size);
            _boxGlow.Color = glowColor;

            // Center the glow behind the box
            var boxCenter = _boxVisual.Position + new Vector2(60, 60);
            _boxGlow.Position = boxCenter - new Vector2(size / 2, size / 2);

            // Insert behind _boxVisual in the node tree
            _root.AddChild(_boxGlow);
            _root.MoveChild(_boxGlow, _boxVisual.GetIndex());

            // Pulse alpha loop
            var pulseTween = CreateTween();
            pulseTween.SetLoops(10000);
            pulseTween.TweenProperty(_boxGlow, "color:a", pulseMax, pulseSpeed)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            pulseTween.TweenProperty(_boxGlow, "color:a", pulseMin, pulseSpeed)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
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
                LootBoxTier.Celestial => 1.1f,
                _ => 0.4f
            };

            float delay = 0f;
            int total = _revealedItems.Count;

            for (int idx = 0; idx < total; idx++)
            {
                var item = _revealedItems[idx];
                int capturedIdx = idx;
                bool isEpicPlus = item.Rarity >= ItemRarity.Epic;
                bool isLegendaryPlus = item.Rarity >= ItemRarity.Legendary;

                var itemPanel = CreateItemRevealPanel(item);
                _itemList.AddChild(itemPanel);

                // Start offscreen right and transparent
                itemPanel.Modulate = new Color(1, 1, 1, 0);

                // Capture the natural position after layout, then offset
                // We use a deferred call so the layout has settled
                float slideOffset = 500f;
                var capturedPanel = itemPanel;
                var capturedItem = item;

                var itemTween = CreateTween();
                itemTween.TweenInterval(delay);

                // Play reveal SFX and narration at the start of this item's reveal
                itemTween.TweenCallback(Callable.From(() =>
                {
                    if (ServiceLocator.TryGet<AudioManager>(out var audio))
                        audio.PlaySFXByName("item_reveal");

                    // Per-item narration (gated by box tier)
                    string narration = BuildItemNarration(capturedItem, capturedIdx, total, _tier);
                    if (narration != null)
                    {
                        if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                            commentary.QueueLine(narration.StartsWith("BIT:") ? "BIT" : "AXIS",
                                narration, CommentaryPriority.High, CommentaryCategory.LootReaction);
                        TtsHelper.Speak(narration);
                    }

                    // Offset position for slide-in (applied just before animating)
                    capturedPanel.Position += new Vector2(slideOffset, 0);
                }));

                // Slide in from right with Back easing (overshoot)
                // The callback above offsets position.x by +slideOffset,
                // so we tween it back by -slideOffset relative to current.
                itemTween.TweenProperty(itemPanel, "position:x", -slideOffset, 0.3f)
                    .AsRelative()
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Back);

                // Fade in simultaneously (parallel with slide)
                var fadeTween = CreateTween();
                fadeTween.TweenInterval(delay);
                fadeTween.TweenProperty(itemPanel, "modulate:a", 1f, 0.15f);

                // Per-item celebration for Epic+ — tiered 3D VFX + UI panel effects
                if (isEpicPlus)
                {
                    float celebrationDelay = delay + 0.3f; // after slide completes
                    var celebTween = CreateTween();
                    celebTween.TweenInterval(celebrationDelay);
                    celebTween.TweenCallback(Callable.From(() =>
                    {
                        // Fire tiered 3D celebration at player position
                        var itemCelebTier = CelebrationVfxManager.TierFromRarity(capturedItem.Rarity);
                        if (ServiceLocator.TryGet<PlayerController>(out var player))
                        {
                            CelebrationVfxManager.Play(
                                GetTree().Root,
                                player.GlobalPosition + Vector3.Up * 0.5f,
                                itemCelebTier);
                        }

                        // Border glow pulse on the panel
                        var panelStyle = capturedPanel.GetThemeStylebox("panel") as StyleBoxFlat;
                        if (panelStyle != null)
                        {
                            var borderPulse = CreateTween();
                            borderPulse.TweenMethod(
                                Callable.From((int w) => SetPanelBorderWidth(panelStyle, w)),
                                3, 6, 0.15f);
                            borderPulse.TweenMethod(
                                Callable.From((int w) => SetPanelBorderWidth(panelStyle, w)),
                                6, 3, 0.15f);
                        }

                        // Legendary+ panel scale pop
                        if (isLegendaryPlus)
                        {
                            capturedPanel.PivotOffset = capturedPanel.Size / 2;
                            var popTween = CreateTween();
                            popTween.TweenProperty(capturedPanel, "scale",
                                new Vector2(1.08f, 1.08f), 0.2f)
                                .SetEase(Tween.EaseType.Out)
                                .SetTrans(Tween.TransitionType.Back);
                            popTween.TweenProperty(capturedPanel, "scale",
                                Vector2.One, 0.2f)
                                .SetEase(Tween.EaseType.InOut)
                                .SetTrans(Tween.TransitionType.Sine);
                        }
                    }));

                    // Extra stagger after Epic+ items for celebration breathing room
                    delay += 0.3f;
                }

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

        private static void SetPanelBorderWidth(StyleBoxFlat style, int width)
        {
            style.BorderWidthLeft = width;
            style.BorderWidthRight = width;
            style.BorderWidthTop = width;
            style.BorderWidthBottom = width;
        }

        private static string BuildItemNarration(ItemInstance item, int index, int total, LootBoxTier tier)
        {
            bool isFirst = index == 0;
            bool isLast = index == total - 1;
            bool isEpicPlus = item.Rarity >= ItemRarity.Epic;

            // Bronze/Silver boxes: only narrate Epic+ items
            if (tier <= LootBoxTier.Silver && !isEpicPlus)
                return null;

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
                LootBoxTier.Celestial => new Color(1f, 0.95f, 0.7f),
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
