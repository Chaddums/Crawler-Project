using System;

namespace JunkyardTD
{
    /// <summary>Where Resources come from (the income ledger).</summary>
    public enum IncomeSource { Kills, Clear, Drills, Interest, Mining, Nodes, Crates, Rewards }

    /// <summary>
    /// The income ledger: every Resource the run earns, by source, for the wave in play and the
    /// run. The HUD's economy line and the autoplay report read it; balance work starts here
    /// ("where is the money coming from?").
    /// </summary>
    public static class Income
    {
        public static readonly int Count = Enum.GetValues(typeof(IncomeSource)).Length;
        private static readonly int[] _wave = new int[Count], _run = new int[Count], _last = new int[Count];

        public static void Add(IncomeSource source, int amount)
        {
            if (amount <= 0) return;
            _wave[(int)source] += amount;
            _run[(int)source] += amount;
        }

        /// <summary>Since the last <see cref="CloseWave"/> (the wave in play and the build before it).</summary>
        public static int Wave(IncomeSource s) => _wave[(int)s];
        /// <summary>What the last closed wave earned, by source.</summary>
        public static int Last(IncomeSource s) => _last[(int)s];
        public static int Run(IncomeSource s) => _run[(int)s];
        public static int LastTotal { get { int t = 0; foreach (var v in _last) t += v; return t; } }

        /// <summary>Clears closed this run (the autoplay report tells a cleared wave from a lost one).</summary>
        public static int Closes { get; private set; }

        /// <summary>A clear: what has come in since the last one becomes "last wave".</summary>
        public static void CloseWave()
        {
            Closes++;
            Array.Copy(_wave, _last, Count);
            Array.Clear(_wave, 0, Count);
        }

        public static void Reset()
        {
            Array.Clear(_wave, 0, Count);
            Array.Clear(_run, 0, Count);
            Array.Clear(_last, 0, Count);
        }

        /// <summary>The last wave's income in words, biggest first ("kills 120, clear 40, ...").</summary>
        public static string LastLine()
        {
            var parts = new System.Collections.Generic.List<(int v, string n)>();
            foreach (IncomeSource s in Enum.GetValues(typeof(IncomeSource)))
                if (_last[(int)s] > 0) parts.Add((_last[(int)s], s.ToString().ToLowerInvariant()));
            parts.Sort((a, b) => b.v.CompareTo(a.v));
            return string.Join(", ", parts.ConvertAll(p => $"{p.n} {p.v}"));
        }
    }
}
