using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// BIT: ancient AI cleanup script. Observations, not reactions. Data, not feelings. No
    /// exclamation. Ever. Specificity over generality. Vast implications delivered straight.
    ///
    /// Speaks through <see cref="Comms"/>. The run's story (briefings, memory, run end) is
    /// <see cref="StoryDirector"/>'s; this is BIT's chatter on milestones and late waves, at a 25%
    /// chance. It used to queue a line for a quarter of every kill, so a wave left minutes of
    /// lines to play out long after the moment.
    /// </summary>
    public partial class BITCommentary : Node
    {
        private readonly RandomNumberGenerator _rng = new();

        // Lines shown are not repeated until the pool is used up
        private readonly Dictionary<string, HashSet<int>> _shownIndices = new();

        private int _runCount;

        public override void _Ready()
        {
            _rng.Randomize();
            _runCount = GameManager.Instance?.MetaSave?.RunCount ?? 0;
            GameEvents.OnWaveMilestone += OnWaveMilestone;
            GameEvents.OnWaveCompleted += OnWaveComplete;
            GameEvents.OnBossSpawned += OnBossSpawned;
            ServiceLocator.Register(this);
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveMilestone -= OnWaveMilestone;
            GameEvents.OnWaveCompleted -= OnWaveComplete;
            GameEvents.OnBossSpawned -= OnBossSpawned;
            ServiceLocator.Unregister<BITCommentary>();
        }

        private void TryComment(string[] lines, string poolName)
        {
            if (_rng.Randf() > 0.25f) return;  // 25%: rarer than AXIS, lands harder
            if (!_shownIndices.TryGetValue(poolName, out var shown))
                _shownIndices[poolName] = shown = new HashSet<int>();
            if (shown.Count >= lines.Length) shown.Clear();
            int idx, attempts = 0;
            do { idx = _rng.RandiRange(0, lines.Length - 1); attempts++; }
            while (shown.Contains(idx) && attempts < 20);
            shown.Add(idx);
            Comms.Say(Comms.Bit, lines[idx]);
        }

        /// <summary>Something BIT says because of what just happened (Ascendants, relics): always shown.</summary>
        public void Say(string text) => Comms.Say(Comms.Bit, text, story: true);

        private void OnBossSpawned() => TryComment(_runCount >= 5 ? _ascendantRecognitionLines : _ascendantFirstLines, "ascendant");

        private void OnWaveMilestone(int wave, string type)
        {
            if (_runCount >= 5) TryComment(_milestoneRecognitionLines, "milestone_recognition");
            else TryComment(_milestoneLines, "milestone");
        }

        private void OnWaveComplete(int wave)
        {
            // Only later waves: early ones aren't interesting to BIT
            if (wave >= 5) TryComment(_waveCompleteLines, "wave_complete");
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
    }
}
