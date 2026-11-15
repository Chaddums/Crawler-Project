using System;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The clean-wave streak (economy in Data/difficulty_scaling.json): each wave cleared with no
    /// more than <see cref="LeaksAllowed"/> enemies through adds one, more leaks than that break
    /// it, and kills and wave-clear bonuses pay <see cref="Mult"/> (1 + <see cref="PerWave"/> a
    /// wave of streak, up to <see cref="Cap"/> waves). Every run earned the same money whatever it
    /// built, so a defence played well and one played carelessly ended near the same wave; this
    /// pays a defence that holds. A strict no-leak rule paid the careless bot more (2026-11 runs:
    /// the smart one let one or two through more often, the careless one leaked in bursts), so
    /// a straggler or two doesn't break it.
    /// </summary>
    public static class Streak
    {
        public static float PerWave { get; private set; } = 0.05f;
        public static int Cap { get; private set; } = 10;
        public static int LeaksAllowed { get; private set; } = 2;

        /// <summary>Waves held in a row.</summary>
        public static int Count { get; private set; }
        public static int Best { get; private set; }
        /// <summary>Enemies through since the last clear.</summary>
        public static int LeaksThisClear { get; private set; }
        /// <summary>What kills and clear bonuses pay now.</summary>
        public static float Mult => 1f + PerWave * Math.Min(Count, Cap);

        static Streak() { Load(); }

        public static void Reset() { Count = 0; Best = 0; LeaksThisClear = 0; }

        public static void NoteLeak() => LeaksThisClear++;

        /// <summary>
        /// A clear covering <paramref name="waves"/> waves (a stack covers several): held, the
        /// streak grows by them; more than <see cref="LeaksAllowed"/> through, it's broken.
        /// Returns true if it held.
        /// </summary>
        public static bool OnClear(int waves, string addr = null)
        {
            bool held = LeaksThisClear <= LeaksAllowed;
            int before = Count;
            if (held)
            {
                Count += Math.Max(1, waves);
                Best = Math.Max(Best, Count);
                GD.Print($"[Streak] {addr}: {Count} clean (pay x{Mult:0.00})");
                if (before < Cap && Count >= Cap)
                    GameEvents.OnAnnouncement?.Invoke($"Clean streak {Count}: kills and clears pay +{Pct(Cap)}%");
                else if (before < 5 && Count >= 5)
                    GameEvents.OnAnnouncement?.Invoke($"Clean streak {Count}: kills and clears pay +{Pct(Count)}%");
            }
            else
            {
                Count = 0;
                GD.Print($"[Streak] {addr}: broken at {before} by {LeaksThisClear} leaks");
                if (before >= 3)
                    GameEvents.OnAnnouncement?.Invoke($"Streak broken at {before}: {LeaksThisClear} got through (more than {LeaksAllowed})");
            }
            LeaksThisClear = 0;
            return held;
        }

        private static int Pct(int waves) => (int)Math.Round(PerWave * Math.Min(waves, Cap) * 100f);

        /// <summary>Tests: set the streak.</summary>
        internal static void TestSet(int count) { Count = count; LeaksThisClear = 0; }

        /// <summary>The wave card's line, or null with no streak.</summary>
        public static string CardLine =>
            Count > 0 ? $"Clean streak {Count}: pay +{Pct(Count)}%" : null;

        private static void Load()
        {
            const string path = "res://Data/difficulty_scaling.json";
            if (!FileAccess.FileExists(path)) return;
            var json = new Json();
            if (json.Parse(FileAccess.GetFileAsString(path)) != Error.Ok || json.Data.Obj is not Godot.Collections.Dictionary d) return;
            if (!d.TryGetValue("economy", out var e) || e.Obj is not Godot.Collections.Dictionary ec) return;
            if (ec.TryGetValue("streak_per_wave", out var p)) PerWave = (float)p.AsDouble();
            if (ec.TryGetValue("streak_cap", out var c)) Cap = (int)c.AsInt64();
            if (ec.TryGetValue("streak_leaks_allowed", out var l)) LeaksAllowed = (int)l.AsInt64();
        }
    }
}
