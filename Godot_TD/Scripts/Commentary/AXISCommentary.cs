using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// AXIS — the snarky AI antagonist from the Junkbot Arena universe.
    /// Provides sardonic commentary during TD battles.
    /// Same character, different game mode.
    /// </summary>
    public partial class AXISCommentary : Node
    {
        private Label _commentaryLabel;
        private float _displayTimer;
        private readonly Queue<(string speaker, string text)> _queue = new();
        private float _cooldown;

        private RandomNumberGenerator _rng = new();

        public override void _Ready()
        {
            // Commentary display
            var panel = new PanelContainer();
            var canvas = new CanvasLayer();
            canvas.Layer = 10;
            AddChild(canvas);

            panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            panel.OffsetTop = 60;
            panel.OffsetBottom = 100;
            panel.OffsetLeft = -250;
            panel.OffsetRight = 250;

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0.7f);
            style.CornerRadiusBottomLeft = 8;
            style.CornerRadiusBottomRight = 8;
            style.CornerRadiusTopLeft = 8;
            style.CornerRadiusTopRight = 8;
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            panel.AddThemeStyleboxOverride("panel", style);
            canvas.AddChild(panel);

            _commentaryLabel = new Label();
            _commentaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _commentaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _commentaryLabel.AddThemeFontSizeOverride("font_size", 16);
            panel.AddChild(_commentaryLabel);
            panel.Visible = false;

            // Hook events
            GameEvents.OnPhaseChanged += OnPhaseChanged;
            GameEvents.OnTowerPlaced += _ => TryComment(_towerPlacedLines);
            GameEvents.OnEnemyLeaked += (_, _) => TryComment(_leakedLines);
            GameEvents.OnWaveCompleted += _ => TryComment(_waveClearLines);
            GameEvents.OnCoreDestroyed += () => Say("AXIS", _rng.RandiRange(0, 1) == 0
                ? "Well. That was inevitable."
                : "I'd say I'm disappointed, but that implies I expected more.");

            ServiceLocator.Register(this);
        }

        public void Say(string speaker, string text)
        {
            _queue.Enqueue((speaker, text));
        }

        public override void _Process(double delta)
        {
            if (_displayTimer > 0)
            {
                _displayTimer -= (float)delta;
                if (_displayTimer <= 0)
                {
                    var panel = _commentaryLabel.GetParent<PanelContainer>();
                    if (panel != null) panel.Visible = false;
                }
            }

            _cooldown -= (float)delta;

            if (_queue.Count > 0 && _cooldown <= 0)
            {
                var (speaker, text) = _queue.Dequeue();
                _commentaryLabel.Text = $"[{speaker}] {text}";
                var panel = _commentaryLabel.GetParent<PanelContainer>();
                if (panel != null) panel.Visible = true;
                _displayTimer = 3f + text.Length * 0.04f;
                _cooldown = 2f;
            }
        }

        private void TryComment(string[] lines)
        {
            if (_rng.Randf() < 0.4f) // 40% chance to comment
                Say("AXIS", lines[_rng.RandiRange(0, lines.Length - 1)]);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Build && GameManager.Instance?.CurrentWave == 0)
                Say("AXIS", "Oh good, you're still here. Build something. Preferably before they arrive.");
            else if (phase == GamePhase.Wave)
                TryComment(_waveStartLines);
        }

        // --- Line pools ---

        private static readonly string[] _waveStartLines = {
            "Here they come. Try not to embarrass yourself.",
            "Another wave. Another opportunity for failure.",
            "I hope those towers are as competent as they look. Which is... not very.",
            "Deploying hostiles. Nothing personal. Actually, entirely personal.",
            "Let's see how your scrapheap defenses handle THIS."
        };

        private static readonly string[] _towerPlacedLines = {
            "Bold choice. Wrong, but bold.",
            "I would have put that somewhere else. Everywhere else, actually.",
            "That'll hold. For about three seconds.",
            "Interesting placement. I'm sure the enemies will appreciate the easy path.",
            "A tower! How quaint."
        };

        private static readonly string[] _leakedLines = {
            "One got through. Standards are slipping.",
            "That one's going to leave a mark.",
            "Your defense has holes. Specifically, all of it.",
            "Another leak. At this rate, your core is a suggestion.",
            "They're getting through. I'm trying not to enjoy this."
        };

        private static readonly string[] _waveClearLines = {
            "Acceptable. Barely.",
            "You survived. Lower the celebration — there's more coming.",
            "Wave clear. Don't let it go to your head.",
            "That was the easy one. Obviously.",
            "Survived? I suppose even broken clocks..."
        };

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<AXISCommentary>();
        }
    }
}
