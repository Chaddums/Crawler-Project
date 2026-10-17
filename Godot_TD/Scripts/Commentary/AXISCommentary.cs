using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// AXIS: nepo baby corporation that acquired BIT without understanding what it holds.
    /// Corporate. Performatively urgent. Never quite paying full attention. Speaks like a manager
    /// who scheduled a meeting they forgot about and is now winging it. Never admits uncertainty:
    /// reframes it as BIT's deficiency. Treats everything as if it's the first time it's happened.
    ///
    /// Speaks through <see cref="Comms"/>; the briefings and the run's story are
    /// <see cref="StoryDirector"/>'s. This is AXIS's chatter, at most one line of each kind a
    /// phase or wave (a remark on every tower placed was noise).
    /// </summary>
    public partial class AXISCommentary : Node
    {
        private readonly RandomNumberGenerator _rng = new();
        private bool _placedSaid, _leakSaid, _lowSaid;

        public override void _Ready()
        {
            _rng.Randomize();
            GameEvents.OnPhaseChanged += OnPhaseChanged;
            GameEvents.OnTowerPlaced += OnTowerPlaced;
            GameEvents.OnEnemyLeaked += OnLeaked;
            GameEvents.OnWaveCompleted += OnWaveCleared;
            GameEvents.OnPlayerDied += OnPlayerDied;
            GameEvents.OnHarvesterDamaged += OnSpireHit;
            ServiceLocator.Register(this);
        }

        public override void _ExitTree()
        {
            GameEvents.OnPhaseChanged -= OnPhaseChanged;
            GameEvents.OnTowerPlaced -= OnTowerPlaced;
            GameEvents.OnEnemyLeaked -= OnLeaked;
            GameEvents.OnWaveCompleted -= OnWaveCleared;
            GameEvents.OnPlayerDied -= OnPlayerDied;
            GameEvents.OnHarvesterDamaged -= OnSpireHit;
            ServiceLocator.Unregister<AXISCommentary>();
        }

        /// <summary>A line from AXIS because of what just happened: always shown.</summary>
        public void Say(string speaker, string text) => Comms.Say(speaker, text, story: true);

        private void TryComment(string[] lines, float chance = 0.4f)
        {
            if (_rng.Randf() < chance)
                Comms.Say(Comms.Axis, lines[_rng.RandiRange(0, lines.Length - 1)]);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Build) _placedSaid = false;
            else if (phase == GamePhase.Wave)
            {
                _leakSaid = false;
                _lowSaid = false;
                if ((GameManager.Instance?.CurrentWave ?? 0) > 1) TryComment(_waveStartLines, 0.25f);
            }
        }

        private void OnTowerPlaced(Node _)
        {
            if (_placedSaid) return;
            _placedSaid = true;
            TryComment(_towerPlacedLines, 0.3f);
        }

        private void OnLeaked(Node _, Vector3 __)
        {
            if (_leakSaid) return;
            _leakSaid = true;
            TryComment(_leakedLines, 0.7f);
        }

        private void OnWaveCleared(int _) => TryComment(_waveClearLines, 0.25f);
        private void OnPlayerDied() => TryComment(_playerDiedLines, 0.6f);

        private void OnSpireHit(float hp)
        {
            if (_lowSaid || hp >= Constants.VINE_HARVESTER_MAX_HP * 0.5f) return;
            _lowSaid = true;
            TryComment(_harvesterLowLines, 0.8f);
        }

        // --- Line pools ---
        // AXIS voice: corporate, performatively urgent, dismissive, never admits uncertainty.
        // Treats everything as first time. Reframes problems as BIT's deficiency.

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
            "Spire below half. I'd panic, but I delegate panic. To you.",
        };
    }
}
