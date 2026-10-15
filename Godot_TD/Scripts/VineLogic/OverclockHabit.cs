using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The Overclock a player has been casting, so the wave card can count it: each tower's share
    /// of the last wave it spent under BIT's Overclock. A player who keeps it rolling sees a card
    /// that knows; one who never casts it sees the plain card. Before, the card ignored it, and a
    /// run with BIT played called BREAKING for six waves that leaked one to five (2026-10).
    /// </summary>
    public static class OverclockHabit
    {
        private static readonly Dictionary<ulong, float> _now = new(), _last = new();
        private static float _clock;
        private static bool _open;
        /// <summary>Changes whenever the counted habit does (the card's defence cache keys on it).</summary>
        public static int Version { get; private set; }

        /// <summary>A new battle: nothing known.</summary>
        public static void Reset() { _now.Clear(); _last.Clear(); _clock = 0f; _open = false; Version++; }

        /// <summary>A wave (not one stacked on a running wave) starts.</summary>
        public static void WaveStarted() { _now.Clear(); _clock = 0f; _open = true; }

        /// <summary>Game time while a wave runs.</summary>
        public static void Tick(float dt) { if (_open) _clock += dt; }

        /// <summary>Overclock reached this tower for this long.</summary>
        public static void Boosted(VineNode n, float seconds)
        {
            if (n == null || seconds <= 0f) return;
            ulong id = n.GetInstanceId();
            _now[id] = (_now.TryGetValue(id, out var s) ? s : 0f) + seconds;
        }

        /// <summary>The wave is over: what each tower got becomes the habit the card counts.</summary>
        public static void WaveEnded()
        {
            if (!_open) return;
            _open = false;
            _last.Clear();
            Version++;
            if (_clock < 1f) return;
            foreach (var kv in _now) _last[kv.Key] = Mathf.Min(1f, kv.Value / _clock);
        }

        /// <summary>Share of the last wave this tower spent overclocked (0 to 1).</summary>
        public static float Share(VineNode n) => n != null && _last.TryGetValue(n.GetInstanceId(), out var s) ? s : 0f;

        /// <summary>Whether any tower was overclocked last wave.</summary>
        public static bool Any => _last.Count > 0;

        /// <summary>
        /// What that habit adds to this tower's damage a second: Overclock raises damage and fire
        /// rate together, replacing whatever boost it has now (boosts don't stack).
        /// </summary>
        public static float Gain(VineNode n)
        {
            float share = Share(n);
            if (share <= 0f) return 1f;
            float b = SignalTuningEditor.BuffDamageBonus;
            float full = (1f + Constants.ABILITY_OVERCLOCK_STRENGTH * b) * (1f + Constants.ABILITY_OVERCLOCK_STRENGTH * b);
            float cur = (1f + n.BuffStrength * b) * (1f + n.BuffStrength * b);
            return 1f + share * Mathf.Max(0f, full / cur - 1f);
        }
    }
}
