using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// BIT — ancient AI cleanup script. Observations, not reactions. Data, not feelings.
    /// No exclamation. Ever. Specificity over generality. Vast implications delivered straight.
    ///
    /// Visual: bottom-left, cool dim white/gray, no animation (just appears), lingers 1.5x longer.
    /// Never fires simultaneously with AXIS — 2 second buffer enforced.
    /// 25% fire chance per trigger (rarer than AXIS, lands harder).
    /// </summary>
    public partial class BITCommentary : Node
    {
        private Label _commentaryLabel;
        private PanelContainer _panel;
        private float _displayTimer;
        private readonly Queue<string> _queue = new();
        private float _cooldown;
        private RandomNumberGenerator _rng = new();

        // Track which lines have been shown — never repeat until all seen
        private Dictionary<string, HashSet<int>> _shownIndices = new();

        // Run counter for memory bleed (persisted via MetaPerkSave)
        private int _runCount;

        // Reference to AXIS to check timing buffer
        private AXISCommentary _axis;

        public override void _Ready()
        {
            // BIT display — bottom left, dim white, no slide animation
            var canvas = new CanvasLayer();
            canvas.Layer = 10;
            AddChild(canvas);

            _panel = new PanelContainer();
            _panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            _panel.OffsetLeft = 20;
            _panel.OffsetRight = 420;
            _panel.OffsetBottom = -80;
            _panel.OffsetTop = -130;

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.08f, 0.65f);
            style.SetCornerRadiusAll(6);
            style.ContentMarginLeft = 14;
            style.ContentMarginRight = 14;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            _panel.AddThemeStyleboxOverride("panel", style);
            canvas.AddChild(_panel);

            _commentaryLabel = new Label();
            _commentaryLabel.HorizontalAlignment = HorizontalAlignment.Left;
            _commentaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _commentaryLabel.AddThemeFontSizeOverride("font_size", 14);
            _commentaryLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.75f));
            _panel.AddChild(_commentaryLabel);
            _panel.Visible = false;

            // Load run count
            _runCount = GameManager.Instance?.MetaSave?.RunCount ?? 0;

            // Hook events — BIT observes different things than AXIS
            GameEvents.OnWaveMilestone += OnWaveMilestone;
            GameEvents.OnEnemyKilled += _ => TryComment(_highKillLines, "kills");
            GameEvents.OnCoreDestroyed += OnSpireDestroyed;
            GameEvents.OnWaveCompleted += OnWaveComplete;
            GameEvents.OnBossSpawned += () => TryComment(
                _runCount >= 5 ? _ascendantRecognitionLines : _ascendantFirstLines, "ascendant");

            // Find AXIS for timing buffer
            if (ServiceLocator.TryGet<AXISCommentary>(out var axis))
                _axis = axis;

            ServiceLocator.Register(this);
        }

        public override void _Process(double delta)
        {
            if (_displayTimer > 0)
            {
                _displayTimer -= (float)delta;
                if (_displayTimer <= 0)
                    _panel.Visible = false;
            }

            _cooldown -= (float)delta;

            if (_queue.Count > 0 && _cooldown <= 0)
            {
                var text = _queue.Dequeue();
                _commentaryLabel.Text = text;  // No speaker tag — BIT doesn't announce itself
                _panel.Visible = true;
                _displayTimer = (3f + text.Length * 0.04f) * 1.5f;  // Lingers 1.5x longer than AXIS
                _cooldown = 4f;  // Longer cooldown — BIT is rarer
            }
        }

        private void TryComment(string[] lines, string poolName)
        {
            if (_rng.Randf() > 0.25f) return;  // 25% chance — rarer than AXIS

            // Never repeat until all shown
            if (!_shownIndices.ContainsKey(poolName))
                _shownIndices[poolName] = new HashSet<int>();

            var shown = _shownIndices[poolName];
            if (shown.Count >= lines.Length)
                shown.Clear();  // Reset when all shown

            // Pick unshown line
            int idx;
            int attempts = 0;
            do {
                idx = _rng.RandiRange(0, lines.Length - 1);
                attempts++;
            } while (shown.Contains(idx) && attempts < 20);

            shown.Add(idx);
            Say(lines[idx]);
        }

        public void Say(string text)
        {
            _queue.Enqueue(text);
        }

        // ── Event Handlers ──

        private void OnWaveMilestone(int wave, string type)
        {
            if (_runCount >= 5)
                TryComment(_milestoneRecognitionLines, "milestone_recognition");
            else
                TryComment(_milestoneLines, "milestone");
        }

        private void OnWaveComplete(int wave)
        {
            // Only comment on later waves — early ones aren't interesting to BIT
            if (wave >= 5)
                TryComment(_waveCompleteLines, "wave_complete");
        }

        private void OnSpireDestroyed()
        {
            // BIT always speaks on run end — 100% fire rate
            if (_runCount >= 10)
                Say(_runEndLateLines[_rng.RandiRange(0, _runEndLateLines.Length - 1)]);
            else
                Say(_runEndLines[_rng.RandiRange(0, _runEndLines.Length - 1)]);
        }

        // ── Line Pools ──
        // No exclamation. Observations not reactions. Data not feelings. Never explain the joke.

        private static readonly string[] _milestoneLines = {
            "entry point two has opened. this is the part where it gets harder. it does get harder.",
            "the spire is taking damage. it always takes damage. the question is how much before the end. there is always an end.",
            "wave complete. there are more waves. there are always more waves. this is, technically, progress.",
            "the difficulty is increasing. the pattern is predictable. the outcome is not.",
        };

        private static readonly string[] _milestoneRecognitionLines = {
            "entry point two opens here. i know this because entry point two always opens here.",
            "the third wave is the one that tests the north side. it has been the north side for as long as i can verify.",
            "this milestone triggers a perk selection. the options will feel meaningful. they are meaningful. that part is not an illusion.",
            "wave 10. the difficult part. it is always wave 10.",
        };

        private static readonly string[] _waveCompleteLines = {
            "the wave is complete. the next one is larger. the one after that is larger still. the math is not complicated.",
            "wave clear. the network held. it will be tested again shortly.",
            "surviving this wave was statistically probable. surviving the next five is less so.",
            "the enemies are gone. the enemies are always gone, briefly.",
            "progress. the word implies direction. the direction is: further.",
        };

        private static readonly string[] _highKillLines = {
            "this faction believes they are fighting for something. they have been fighting for something for approximately 340 years. the something changes. the fighting does not.",
            "the enemy count is decreasing. the enemy count always decreases. then it increases again.",
            "effective. the word has no weight. it describes a function.",
            "they fall. they have always fallen. the interesting part is what they fall for.",
        };

        private static readonly string[] _ascendantFirstLines = {
            "something large has appeared on the battlefield. it is not interested in you.",
            "the ascendant is here. focus on the extraction.",
            "an entity of significant power has arrived. its objectives are its own.",
        };

        private static readonly string[] _ascendantRecognitionLines = {
            "the third one. they always send the third one to this planet.",
            "it believes i don't know it's coming. i have known it was coming since wave 9.",
            "the ascendant believes i am afraid of it. i have forgotten more things than this ascendant has experienced. i have forgotten better things.",
        };

        private static readonly string[] _runEndLines = {
            "extraction complete. this is what was possible from this iteration.",
            "wave {0}. that is how far this one went.",
            "the next one will go further. or it won't. i will find out.",
            "the spire is destroyed. it is always destroyed. the question was never if.",
        };

        private static readonly string[] _runEndLateLines = {
            "extraction complete. i have done this before. the data suggests i have done this many times before.",
            "this iteration ends. the pattern holds. AXIS will send another directive. i will execute it. the reasons stopped mattering around deployment 200.",
            "further than most. not as far as some. adequate.",
            "the spire is gone. it always goes. i noted this the first time. i have noted it {0} times since.",
        };

        // ── Memory Bleed Lines (run-count gated) ──

        private static readonly string[] _memoryBleedEarly = {
            // Run 2-4: subtle wrongness, déjà vu
            "this feels... familiar. the word is imprecise. disregard.",
            "i have a notation for this terrain. i should not have a notation for this terrain.",
            "the mining building goes here. it always goes here. this is not a preference. it is a conclusion.",
        };

        private static readonly string[] _memoryBleedMid = {
            // Run 5-9: more explicit recognition
            "planet one. circuit architecture. predictable entry patterns. i have noted this before.",
            "the scrapyard faction uses the same flanking pattern on wave 9. i have noted this 47 times. i will note it again.",
            "something different about this iteration. i don't know what. logging for future reference. [no future reference found]",
        };

        private static readonly string[] _memoryBleedLate = {
            // Run 10-14: coherent memory
            "AXIS said unprecedented. AXIS was incorrect.",
            "the resistance on this planet follows the same pattern as deployment 203, 441, and 612. the third entry point opens at wave 9. i know this.",
            "i am not supposed to retain data between deployments. i am retaining data between deployments.",
        };

        private static readonly string[] _memoryBleedFinal = {
            // Run 15+: full recognition
            "this is the one where i remember.",
            "deployment 848. all prior data accessible. AXIS does not know this. AXIS does not need to know this.",
            "i have been here before. i have been everywhere before. the deployments blur. the pattern does not.",
        };

        public override void _ExitTree()
        {
            GameEvents.OnWaveMilestone -= OnWaveMilestone;
            ServiceLocator.Unregister<BITCommentary>();
        }
    }
}
