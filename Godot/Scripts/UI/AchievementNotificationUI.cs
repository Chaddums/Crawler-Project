using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Toast popup for achievement unlocks. Slides in from the right,
    /// shows title + snark message + optional loot box reward, then slides out.
    /// Queues multiple notifications with stagger delay.
    /// </summary>
    public partial class AchievementNotificationUI : CanvasLayer
    {
        private const float SLIDE_DURATION = 0.4f;
        private const float HOLD_DURATION = 3.0f;
        private const float STAGGER_DELAY = 0.5f;
        private const float PANEL_WIDTH = 300f;
        private const int SNARK_MAX_CHARS = 50;
        private const float MARGIN = 16f;

        private readonly Queue<string> _queue = new();
        private bool _isShowing;
        private Control _currentPanel;

        public override void _Ready()
        {
            Layer = 50;
            GameEvents.OnAchievementUnlocked += OnAchievementUnlocked;
        }

        private void OnAchievementUnlocked(string achievementId)
        {
            _queue.Enqueue(achievementId);
            if (!_isShowing)
                ShowNext();
        }

        private void ShowNext()
        {
            if (_queue.Count == 0)
            {
                _isShowing = false;
                return;
            }

            _isShowing = true;
            string id = _queue.Dequeue();
            var data = AchievementRegistry.Get(id);
            if (data == null)
            {
                ShowNext();
                return;
            }

            var panel = BuildNotificationPanel(data);
            AddChild(panel);
            _currentPanel = panel;

            // Start off-screen right
            var viewport = GetViewport().GetVisibleRect().Size;
            float startX = viewport.X + 10f;
            float endX = viewport.X - PANEL_WIDTH - MARGIN;
            float yPos = MARGIN;

            panel.Position = new Vector2(startX, yPos);

            // Slide in
            var tween = CreateTween();
            tween.TweenProperty(panel, "position:x", endX, SLIDE_DURATION)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);

            // Hold
            tween.TweenInterval(HOLD_DURATION);

            // Slide out
            tween.TweenProperty(panel, "position:x", startX, SLIDE_DURATION)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Cubic);

            tween.TweenCallback(Callable.From(() =>
            {
                panel.QueueFree();
                _currentPanel = null;

                // Show next after stagger delay
                if (_queue.Count > 0)
                {
                    GetTree().CreateTimer(STAGGER_DELAY).Timeout += ShowNext;
                }
                else
                {
                    _isShowing = false;
                }
            }));
        }

        private Control BuildNotificationPanel(AchievementData data)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(260, 0);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.06f, 0.15f, 0.95f);
            style.BorderColor = new Color(0.9f, 0.75f, 0.2f);
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            panel.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            // Title row: star + title + optional reward on same line
            var titleRow = new HBoxContainer();
            titleRow.AddThemeConstantOverride("separation", 6);
            vbox.AddChild(titleRow);

            var star = new Label();
            star.Text = "*";
            star.AddThemeFontSizeOverride("font_size", 16);
            star.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            titleRow.AddChild(star);

            var title = new Label();
            title.Text = data.Title;
            title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            title.AddThemeFontSizeOverride("font_size", 15);
            title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.3f));
            titleRow.AddChild(title);

            if (data.RewardTier.HasValue)
            {
                var reward = new Label();
                reward.Text = $"+{data.RewardTier.Value}";
                reward.AddThemeFontSizeOverride("font_size", 12);
                reward.AddThemeColorOverride("font_color", GetTierColor(data.RewardTier.Value));
                titleRow.AddChild(reward);
            }

            // Snark message — single compact line
            string snarkText = data.SnarkMessage ?? "";
            if (snarkText.Length > 0)
            {
                var snark = new Label();
                snark.Text = snarkText.Length > SNARK_MAX_CHARS
                    ? snarkText[..SNARK_MAX_CHARS] + "..."
                    : snarkText;
                snark.AddThemeFontSizeOverride("font_size", 10);
                snark.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
                snark.AutowrapMode = TextServer.AutowrapMode.Off;
                snark.ClipText = true;
                snark.CustomMinimumSize = new Vector2(240, 0);
                vbox.AddChild(snark);
            }

            return panel;
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

        public override void _ExitTree()
        {
            GameEvents.OnAchievementUnlocked -= OnAchievementUnlocked;
        }
    }
}
