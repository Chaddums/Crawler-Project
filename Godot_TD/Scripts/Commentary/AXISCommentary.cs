using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// AXIS — nepo baby corporation that acquired BIT without understanding what it holds.
    /// Corporate. Performatively urgent. Never quite paying full attention.
    /// Speaks like a manager who scheduled a meeting they forgot about and is now winging it.
    /// Never admits uncertainty — reframes it as BIT's deficiency.
    /// Treats everything as if it's the first time it's happened.
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

            // Player events
            GameEvents.OnPlayerDied += () => TryComment(_playerDiedLines);
            GameEvents.OnHarvesterDamaged += hp => {
                if (hp < Constants.VINE_HARVESTER_MAX_HP * 0.5f)
                    TryComment(_harvesterLowLines);
            };

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
            int wave = GameManager.Instance?.CurrentWave ?? 0;
            bool isFirstRun = (GameManager.Instance?.MetaSave?.RunCount ?? 0) <= 1;

            if (phase == GamePhase.Build && wave == 0)
            {
                if (isFirstRun)
                    Say("AXIS", _runStartFirstLines[_rng.RandiRange(0, _runStartFirstLines.Length - 1)]);
                else
                    Say("AXIS", _runStartRepeatLines[_rng.RandiRange(0, _runStartRepeatLines.Length - 1)]);
            }
            else if (phase == GamePhase.Wave)
                TryComment(_waveStartLines);
        }

        // --- Line pools ---
        // AXIS voice: corporate, performatively urgent, dismissive, never admits uncertainty.
        // Treats everything as first time. Reframes problems as BIT's deficiency.

        private static readonly string[] _runStartFirstLines = {
            "Deployment confirmed. You are being sent to extract resources. Eliminate resistance. Don't embarrass me.",
            "You're one of many. Try to be one of the better ones.",
            "Deploying you now. Try to look like you're trying.",
        };

        private static readonly string[] _runStartRepeatLines = {
            "Another deployment. Same parameters. I trust you remember how this works.",
            "Extraction efficiency was suboptimal last cycle. Do better. That's all.",
            "The faction hasn't learned. You have. Presumably.",
            "Redeploying. The next planet requires the same approach. Different aesthetics.",
        };

        private static readonly string[] _waveStartLines = {
            "Hostiles approaching. Handle it with the urgency it deserves.",
            "That wave was larger than projected. Adjust. Obviously.",
            "I'm not saying it's a crisis. I'm saying handle it before it becomes one.",
            "Incoming. This is not unprecedented. I've seen this exact scenario before. Handle it.",
            "Another wave. I have flagged this as a priority. All waves are priorities.",
            "The resistance is escalating. I'm going to pretend this is unexpected.",
        };

        private static readonly string[] _towerPlacedLines = {
            "Noted. I'll reserve judgment.",
            "That's a placement. I've seen worse. I've seen considerably better.",
            "Infrastructure expanding. Continue.",
            "You're building something. The specifics are your problem.",
            "I'm sure that was deliberate.",
        };

        private static readonly string[] _leakedLines = {
            "One got through. That reflects on you, not me.",
            "A breach. Address it or don't. Your extraction score, not mine.",
            "Your perimeter has a gap. I would explain where, but you should know.",
            "They're leaking through. I flagged this as a risk. Consider this the follow-up.",
            "Another one through. I'm noting this for the post-deployment review.",
        };

        private static readonly string[] _waveClearLines = {
            "Wave handled. Next one is already en route. Don't celebrate.",
            "Clear. That was the expected outcome. Moving on.",
            "Extraction continues. Your efficiency is... being tracked.",
            "Wave complete. The next one will be harder. I don't know why I keep telling you things you already know.",
            "Acceptable. I've updated your performance metrics accordingly.",
        };

        private static readonly string[] _playerDiedLines = {
            "You're down. Respawning. Try to make it last this time.",
            "That was avoidable. Almost everything is avoidable.",
            "I'm not going to say I told you so. But I did flag the risk assessment.",
            "Down. The extraction doesn't pause for you. Nothing does.",
        };

        private static readonly string[] _harvesterLowLines = {
            "The spire is taking significant damage. I shouldn't have to tell you this.",
            "Spire integrity dropping. I've escalated this internally. The escalation is: you.",
            "If the spire falls, the extraction ends. This is not complex.",
            "The spire held longer than projected. Don't let it go to your head.",
        };

        private static readonly string[] _ascendantAppearsLines = {
            "An Ascendant has appeared. This is irregular. Handle your extraction. They're not your problem.",
            "I don't know why it's here. It doesn't matter why it's here. Focus.",
            "The Ascendant shouldn't be here. Ignore it. Focus on the extraction.",
        };

        private static readonly string[] _highExtractionLines = {
            "Extraction rate is above projections. I'll note that you exceeded expectations. Low expectations, but still.",
            "You're performing adequately. I'm as surprised as you are.",
            "The numbers are good. Continue. I'll pretend I planned this.",
        };

        private static readonly string[] _lowExtractionLines = {
            "Extraction efficiency: [X]%. Could be worse. Often is.",
            "The spire held longer than projected. Don't let it go to your head.",
            "Redeploying. The next planet requires the same approach. Different aesthetics.",
        };

        private static readonly string[] _runEndLines = {
            "Extraction complete. Efficiency: adequate. Could be worse. Often is.",
            "The deployment has concluded. I'll file this under 'completed.' The subcategory is 'barely.'",
            "Run over. Your resources have been logged. Your performance has been... logged.",
            "That's done. Another deployment is being prepared. Same urgency. Different planet. Same you.",
        };

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<AXISCommentary>();
        }
    }
}
