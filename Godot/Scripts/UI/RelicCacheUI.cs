using System;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Full-screen overlay for the unique Relic Cache opening ceremony.
    /// Unlike normal loot boxes, this reveals a SINGLE absurd relic with:
    /// 1. Screen dims, glowing cache appears with relic's unique color
    /// 2. Cache shakes violently (these are powerful items)
    /// 3. Cache bursts open — dramatic flash
    /// 4. AXIS reads the relic name with dramatic pause
    /// 5. Flavor text typewriters in
    /// 6. AXIS's commentary appears (his reaction to the absurd item)
    /// 7. Stat bonuses fade in
    /// 8. Player clicks to collect
    /// </summary>
    public partial class RelicCacheUI : CanvasLayer
    {
        private Control _root;
        private ColorRect _dimOverlay;
        private ColorRect _flashOverlay;
        private PanelContainer _cacheVisual;
        private VBoxContainer _revealPanel;
        private Label _relicNameLabel;
        private Label _descriptionLabel;
        private Label _flavorLabel;
        private Label _axisQuoteLabel;
        private Label _statBonusLabel;
        private Label _collectPrompt;
        private ColorRect _glowRect;

        private RelicData _relic;
        private ItemInstance _relicInstance;

        public event Action<ItemInstance> CeremonyCollected;

        public override void _Ready()
        {
            Layer = 65; // Above normal loot box ceremony
        }

        public void StartCeremony(RelicData relic)
        {
            _relic = relic;
            _relicInstance = new ItemInstance(relic, ItemRarity.Absurd);
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

            // Flash overlay
            _flashOverlay = new ColorRect();
            _flashOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _flashOverlay.Color = new Color(1, 1, 1, 0f);
            _flashOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _root.AddChild(_flashOverlay);

            var viewport = GetViewport().GetVisibleRect().Size;
            float cx = viewport.X / 2f;
            float cy = viewport.Y / 2f;

            // Pulsing glow behind cache (relic's unique color)
            _glowRect = new ColorRect();
            _glowRect.CustomMinimumSize = new Vector2(300, 300);
            _glowRect.Size = new Vector2(300, 300);
            _glowRect.Color = new Color(_relic.GlowColor.R, _relic.GlowColor.G, _relic.GlowColor.B, 0f);
            _glowRect.Position = new Vector2(cx - 150, cy - 150);
            _glowRect.MouseFilter = Control.MouseFilterEnum.Ignore;
            _root.AddChild(_glowRect);

            // Cache visual — ornate box in relic color
            _cacheVisual = new PanelContainer();
            _cacheVisual.CustomMinimumSize = new Vector2(140, 140);
            var style = new StyleBoxFlat();
            style.BgColor = new Color(_relic.GlowColor.R * 0.3f, _relic.GlowColor.G * 0.3f, _relic.GlowColor.B * 0.3f, 0.9f);
            style.BorderColor = _relic.GlowColor;
            style.BorderWidthBottom = 4;
            style.BorderWidthTop = 4;
            style.BorderWidthLeft = 4;
            style.BorderWidthRight = 4;
            style.CornerRadiusBottomLeft = 12;
            style.CornerRadiusBottomRight = 12;
            style.CornerRadiusTopLeft = 12;
            style.CornerRadiusTopRight = 12;
            _cacheVisual.AddThemeStyleboxOverride("panel", style);

            var cacheLabel = new Label();
            cacheLabel.Text = "RELIC CACHE";
            cacheLabel.HorizontalAlignment = HorizontalAlignment.Center;
            cacheLabel.VerticalAlignment = VerticalAlignment.Center;
            cacheLabel.AddThemeFontSizeOverride("font_size", 16);
            cacheLabel.AddThemeColorOverride("font_color", _relic.GlowColor);
            _cacheVisual.AddChild(cacheLabel);

            _cacheVisual.Position = new Vector2(cx - 70, cy - 70);
            _root.AddChild(_cacheVisual);

            // Reveal panel (hidden until cache bursts)
            _revealPanel = new VBoxContainer();
            _revealPanel.Position = new Vector2(cx - 280, cy - 140);
            _revealPanel.CustomMinimumSize = new Vector2(560, 0);
            _revealPanel.AddThemeConstantOverride("separation", 12);
            _revealPanel.Visible = false;
            _root.AddChild(_revealPanel);

            // Relic name
            _relicNameLabel = new Label();
            _relicNameLabel.Text = "";
            _relicNameLabel.AddThemeFontSizeOverride("font_size", 28);
            _relicNameLabel.AddThemeColorOverride("font_color", _relic.GlowColor);
            _relicNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _revealPanel.AddChild(_relicNameLabel);

            // Description (stat summary)
            _descriptionLabel = new Label();
            _descriptionLabel.Text = "";
            _descriptionLabel.AddThemeFontSizeOverride("font_size", 16);
            _descriptionLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
            _descriptionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _revealPanel.AddChild(_descriptionLabel);

            // Flavor text (lore)
            _flavorLabel = new Label();
            _flavorLabel.Text = "";
            _flavorLabel.AddThemeFontSizeOverride("font_size", 13);
            _flavorLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.5f));
            _flavorLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _flavorLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _revealPanel.AddChild(_flavorLabel);

            // Separator
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 8);
            _revealPanel.AddChild(sep);

            // AXIS quote
            _axisQuoteLabel = new Label();
            _axisQuoteLabel.Text = "";
            _axisQuoteLabel.AddThemeFontSizeOverride("font_size", 15);
            _axisQuoteLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
            _axisQuoteLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _axisQuoteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _revealPanel.AddChild(_axisQuoteLabel);

            // Stat bonuses
            _statBonusLabel = new Label();
            _statBonusLabel.Text = "";
            _statBonusLabel.AddThemeFontSizeOverride("font_size", 14);
            _statBonusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1f));
            _statBonusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _statBonusLabel.Modulate = new Color(1, 1, 1, 0);
            _revealPanel.AddChild(_statBonusLabel);

            // Collect prompt
            _collectPrompt = new Label();
            _collectPrompt.Text = "[Click to Equip]";
            _collectPrompt.AddThemeFontSizeOverride("font_size", 20);
            _collectPrompt.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            _collectPrompt.HorizontalAlignment = HorizontalAlignment.Center;
            _collectPrompt.Visible = false;
            _revealPanel.AddChild(_collectPrompt);
        }

        private void AnimateOpening()
        {
            var tween = CreateTween();

            // 1. Dim screen deeply (darker than normal loot boxes)
            tween.TweenProperty(_dimOverlay, "color:a", 0.85f, 0.4f);

            // 2. Cache appears with scale bounce
            _cacheVisual.Scale = Vector2.Zero;
            _cacheVisual.PivotOffset = new Vector2(70, 70);
            tween.TweenProperty(_cacheVisual, "scale", Vector2.One, 0.4f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);

            // 3. Start glow pulse
            tween.TweenCallback(Callable.From(StartGlowPulse));

            // 4. Violent shake (relics are powerful — long, intense shake)
            tween.TweenCallback(Callable.From(() => ShakeCache(2.0f, 20f)));
            tween.TweenInterval(2.0f);

            // 5. Flash and burst
            tween.TweenCallback(Callable.From(() =>
            {
                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("box_open");

                // Epic celebration VFX
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                {
                    CelebrationVfxManager.Play(
                        GetTree().Root,
                        player.GlobalPosition + Vector3.Up * 0.5f,
                        CelebrationTier.Legendary);
                }
            }));

            // White flash
            tween.TweenProperty(_flashOverlay, "color:a", 0.6f, 0.08f);
            tween.TweenProperty(_flashOverlay, "color:a", 0f, 0.4f);

            // Burst cache away
            tween.TweenCallback(Callable.From(() =>
            {
                var burst = CreateTween();
                burst.TweenProperty(_cacheVisual, "scale", new Vector2(2f, 2f), 0.12f);
                burst.TweenProperty(_cacheVisual, "modulate:a", 0f, 0.1f);
                burst.TweenCallback(Callable.From(() => _cacheVisual.Visible = false));
            }));

            tween.TweenInterval(0.5f);

            // 6. Reveal sequence
            tween.TweenCallback(Callable.From(RevealRelic));
        }

        private void StartGlowPulse()
        {
            var pulseTween = CreateTween();
            pulseTween.SetLoops(10000);
            pulseTween.TweenProperty(_glowRect, "color:a", 0.4f, 0.4f)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            pulseTween.TweenProperty(_glowRect, "color:a", 0.15f, 0.4f)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private void ShakeCache(float duration, float maxIntensity)
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("box_shake");

            var basePos = _cacheVisual.Position;
            var shakeTween = CreateTween();
            int shakeSteps = (int)(duration / 0.04f);

            for (int i = 0; i < shakeSteps; i++)
            {
                float t = (float)i / shakeSteps;
                float intensity = 3f + t * maxIntensity;
                float offsetX = (float)GD.RandRange(-intensity, intensity);
                float offsetY = (float)GD.RandRange(-intensity, intensity);
                shakeTween.TweenProperty(_cacheVisual, "position",
                    basePos + new Vector2(offsetX, offsetY), 0.04f);
            }
            shakeTween.TweenProperty(_cacheVisual, "position", basePos, 0.04f);
        }

        private void RevealRelic()
        {
            _revealPanel.Visible = true;

            var tween = CreateTween();

            // AXIS announces: "What have we here..."
            tween.TweenCallback(Callable.From(() =>
            {
                string intro = $"AXIS: What... is... THAT.";
                if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                    commentary.QueueLine("AXIS", intro, CommentaryPriority.Announcement, CommentaryCategory.LootReaction);
                TtsHelper.Speak(intro);
            }));
            tween.TweenInterval(1.2f);

            // Relic name appears with scale pop
            tween.TweenCallback(Callable.From(() =>
            {
                _relicNameLabel.Text = _relic.ItemName;
                _relicNameLabel.PivotOffset = new Vector2(_relicNameLabel.Size.X / 2, _relicNameLabel.Size.Y / 2);
                _relicNameLabel.Scale = new Vector2(0.5f, 0.5f);
                _relicNameLabel.Modulate = new Color(1, 1, 1, 0);

                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("item_reveal");
            }));
            tween.TweenProperty(_relicNameLabel, "scale", new Vector2(1.1f, 1.1f), 0.2f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.Parallel().TweenProperty(_relicNameLabel, "modulate:a", 1f, 0.15f);
            tween.TweenProperty(_relicNameLabel, "scale", Vector2.One, 0.15f);

            // AXIS reads the name aloud
            tween.TweenCallback(Callable.From(() =>
            {
                string nameRead = $"AXIS: {_relic.ItemName}.";
                TtsHelper.Speak(nameRead);
            }));
            tween.TweenInterval(1.0f);

            // Description fades in
            tween.TweenCallback(Callable.From(() =>
            {
                _descriptionLabel.Text = _relic.Description;
                _descriptionLabel.Modulate = new Color(1, 1, 1, 0);
            }));
            tween.TweenProperty(_descriptionLabel, "modulate:a", 1f, 0.4f);
            tween.TweenInterval(0.8f);

            // Flavor text (lore) fades in
            tween.TweenCallback(Callable.From(() =>
            {
                _flavorLabel.Text = _relic.FlavorText;
                _flavorLabel.Modulate = new Color(1, 1, 1, 0);
            }));
            tween.TweenProperty(_flavorLabel, "modulate:a", 1f, 0.6f);
            tween.TweenInterval(1.0f);

            // AXIS's reaction quote
            tween.TweenCallback(Callable.From(() =>
            {
                _axisQuoteLabel.Text = $"AXIS: \"{_relic.AxisQuote}\"";
                _axisQuoteLabel.Modulate = new Color(1, 1, 1, 0);

                string axisLine = $"AXIS: {_relic.AxisQuote}";
                if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                    commentary.QueueLine("AXIS", axisLine, CommentaryPriority.Announcement, CommentaryCategory.LootReaction);
                TtsHelper.Speak(axisLine);
            }));
            tween.TweenProperty(_axisQuoteLabel, "modulate:a", 1f, 0.5f);
            tween.TweenInterval(1.5f);

            // Stat bonuses appear
            tween.TweenCallback(Callable.From(() =>
            {
                _statBonusLabel.Text = BuildStatText();
            }));
            tween.TweenProperty(_statBonusLabel, "modulate:a", 1f, 0.4f);
            tween.TweenInterval(0.5f);

            // Show collect prompt
            tween.TweenCallback(Callable.From(() =>
            {
                _collectPrompt.Visible = true;
                _dimOverlay.GuiInput += OnCollectClick;
            }));
        }

        private string BuildStatText()
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var stat in _relic.BaseStatBonuses)
            {
                string sign = stat.Value >= 0 ? "+" : "";
                if (stat.ModType == ModifierType.Percent)
                    parts.Add($"{sign}{stat.Value:F0}% {stat.StatType}");
                else
                    parts.Add($"{sign}{stat.Value:F0} {stat.StatType}");
            }
            return string.Join("  |  ", parts);
        }

        private void OnCollectClick(InputEvent ev)
        {
            if (ev is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
                return;

            _dimOverlay.GuiInput -= OnCollectClick;
            CollectRelic();
        }

        private void CollectRelic()
        {
            if (ServiceLocator.TryGet<PlayerController>(out var player))
                player.Inventory.TryAddItem(_relicInstance);

            // Fade out and clean up
            var tween = CreateTween();
            tween.TweenProperty(_root, "modulate:a", 0f, 0.3f);
            tween.TweenCallback(Callable.From(() =>
            {
                CeremonyCollected?.Invoke(_relicInstance);
                QueueFree();
            }));
        }
    }
}
