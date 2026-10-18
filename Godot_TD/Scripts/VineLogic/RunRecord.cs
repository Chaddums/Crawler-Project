using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What happened in the run, for the debrief: how it ended and why (what got through to the
    /// Spire, the last blow), the numbers (waves, kills, leaks, towers built and wrecked, BIT's
    /// level, crates), the Spire's health at every clear, and the moments worth a line (a frame,
    /// a boss down, a relic, the Spire under half). A playtester finished a run and nothing said
    /// what had happened. Fed by the battle as it plays; begun with each battle scene.
    /// </summary>
    public static class RunRecord
    {
        public static string Where { get; private set; } = "";
        public static int Kills { get; private set; }
        public static int BitKills { get; private set; }
        public static int BossesDown { get; private set; }
        public static int Leaks { get; private set; }
        public static float SpireLost { get; private set; }
        public static int BitDowns { get; private set; }
        public static int TowersBuilt { get; private set; }
        public static int Crates { get; private set; }
        public static int Relics { get; private set; }
        public static int Level { get; private set; } = 1;
        public static string Frame { get; private set; } = "";
        public static int WavesCleared { get; private set; }
        /// <summary>What dealt the blow that brought the Spire down (null: it never fell).</summary>
        public static string LastBlow { get; private set; }
        public static bool EndedEarly { get; set; }

        /// <summary>The Spire's health (0..1) when each wave was cleared, in order.</summary>
        public static readonly List<float> SpireAtClear = new();
        private static readonly Dictionary<string, (int n, float dmg)> _leakedBy = new();
        private static readonly List<(int wave, int rank, string text)> _moments = new();
        private static ulong _startMs, _endMs;
        private static bool _underHalf;

        public static void Begin(string where)
        {
            Where = where ?? "";
            Kills = BitKills = BossesDown = Leaks = BitDowns = TowersBuilt = Crates = Relics = WavesCleared = 0;
            SpireLost = 0f;
            Level = 1;
            Frame = "";
            LastBlow = null;
            EndedEarly = false;
            _underHalf = false;
            SpireAtClear.Clear();
            _leakedBy.Clear();
            _moments.Clear();
            _startMs = Time.GetTicksMsec();
            _endMs = 0;
        }

        private static int Wave => GameManager.Instance?.CurrentWave ?? 0;

        /// <summary>A moment: lower rank is kept first when there are too many to show.</summary>
        private static void Moment(int rank, string text) => _moments.Add((Mathf.Max(1, Wave), rank, text));

        public static void Killed(VineEnemy e)
        {
            if (e == null) return;
            Kills++;
            if (e.LastHitBy == "BIT") BitKills++;
            if ((e.IsBoss && !e.IsCommander) || e.IsAscendant)
            {
                BossesDown++;
                Moment(1, $"{e.EnemyName} destroyed{(e.LastHitBy == "BIT" ? " by BIT" : "")}");
            }
        }

        public static void Leaked(string who, float damage, bool spireFell, float spireShare)
        {
            who = string.IsNullOrEmpty(who) ? "An enemy" : who;
            Leaks++;
            SpireLost += damage;
            var had = _leakedBy.TryGetValue(who, out var v) ? v : (0, 0f);
            _leakedBy[who] = (had.Item1 + 1, had.Item2 + damage);
            SpireNow(spireShare);
            if (spireFell) Fell(who);
        }

        public static void BitDown(float spireDamage, bool spireFell, float spireShare)
        {
            BitDowns++;
            SpireLost += spireDamage;
            if (spireDamage > 0f)
            {
                var had = _leakedBy.TryGetValue("BIT going down", out var v) ? v : (0, 0f);
                _leakedBy["BIT going down"] = (had.Item1 + 1, had.Item2 + spireDamage);
            }
            SpireNow(spireShare);
            if (spireFell) Fell("BIT going down");
        }

        private static void SpireNow(float share)
        {
            if (!_underHalf && share < 0.5f && share > 0f)
            {
                _underHalf = true;
                Moment(2, "The Spire dropped under half health");
            }
        }

        private static void Fell(string who)
        {
            if (LastBlow != null) return;
            LastBlow = who;
            Moment(0, $"The Spire fell: the last blow was {(who == "BIT going down" ? "BIT going down" : who + " getting through")}");
        }

        public static void Built() => TowersBuilt++;

        public static void LevelUp(int level, string frame)
        {
            Level = level;
            if (!string.IsNullOrEmpty(frame) && frame != Frame)
            {
                Frame = frame;
                Moment(1, $"BIT reached level {level} and climbed into the {frame}");
            }
        }

        public static void Crate(LootTier tier)
        {
            Crates++;
            if (tier != null && tier.Rank >= 2) Moment(3, $"BIT picked up a {tier.Name}");
        }

        public static void Relic(string name)
        {
            Relics++;
            Moment(2, $"Found a relic: {name}");
        }

        public static void WaveCleared(int wave, float spireShare)
        {
            WavesCleared = Mathf.Max(WavesCleared, wave);
            SpireAtClear.Add(Mathf.Clamp(spireShare, 0f, 1f));
        }

        public static void End()
        {
            if (_endMs == 0) _endMs = Time.GetTicksMsec();
        }

        public static float Seconds => ((_endMs > 0 ? _endMs : Time.GetTicksMsec()) - _startMs) / 1000f;

        /// <summary>What got through most, by Spire damage: (name, times, share of the Spire damage).</summary>
        public static (string who, int n, float share)? TopLeak()
        {
            if (_leakedBy.Count == 0 || SpireLost <= 0f) return null;
            var top = _leakedBy.OrderByDescending(kv => kv.Value.dmg).First();
            return (top.Key, top.Value.n, top.Value.dmg / SpireLost);
        }

        /// <summary>How the run ended and why, in a sentence or two.</summary>
        public static string Headline(bool victory, bool bossRun, int wave)
        {
            wave = Mathf.Max(1, wave);
            if (victory)
                return bossRun ? $"Boss defeated on wave {wave}." : $"{(Where.Length > 0 ? Where : "The site")} secured: you held to wave {wave}.";
            if (LastBlow == null)
                return EndedEarly ? $"You ended the run on wave {wave}." : $"The run ended on wave {wave}.";
            string s = $"The Spire fell on wave {wave}.";
            var top = TopLeak();
            if (top.HasValue)
            {
                var t = top.Value;
                s += t.who == "BIT going down"
                    ? $" BIT went down {t.n} time{(t.n == 1 ? "" : "s")}, which did {Mathf.RoundToInt(t.share * 100)}% of the damage to the Spire."
                    : $" {t.who} got through {t.n} time{(t.n == 1 ? "" : "s")} and did {Mathf.RoundToInt(t.share * 100)}% of the damage to the Spire.";
                if (LastBlow != t.who) s += LastBlow == "BIT going down" ? " BIT going down was the last blow." : $" {LastBlow} landed the last blow.";
            }
            return s;
        }

        /// <summary>The run's numbers, as (label, value) pairs.</summary>
        public static List<(string label, string value)> Stats()
        {
            int wrecked = 0;
            foreach (TowerLoss l in System.Enum.GetValues(typeof(TowerLoss))) wrecked += Losses.Lost(l);
            int earned = 0;
            foreach (IncomeSource src in System.Enum.GetValues(typeof(IncomeSource))) earned += Income.Run(src);
            int secs = Mathf.RoundToInt(Seconds);
            var list = new List<(string, string)>
            {
                ("Waves cleared", WavesCleared.ToString()),
                ("Time", $"{secs / 60}:{secs % 60:00}"),
                ("Enemies destroyed", BitKills > 0 ? $"{Kills} ({BitKills} by BIT)" : Kills.ToString()),
                ("Bosses down", BossesDown.ToString()),
                ("Got through", Leaks == 0 ? "none" : $"{Leaks} ({Mathf.RoundToInt(SpireLost)} Spire damage)"),
                ("Towers built", wrecked > 0 ? $"{TowersBuilt} ({wrecked} wrecked)" : TowersBuilt.ToString()),
                ("BIT", Frame.Length > 0 ? $"level {Level}, {Frame}" : $"level {Level}"),
                ("Resources earned", earned.ToString()),
            };
            if (BitDowns > 0) list.Add(("BIT went down", BitDowns.ToString()));
            if (Crates > 0 || Relics > 0) list.Add(("Crates, relics", $"{Crates}, {Relics}"));
            return list;
        }

        /// <summary>Up to <paramref name="max"/> moments in wave order, the most telling kept when there are more.</summary>
        public static List<string> Moments(int max = 8)
        {
            var keep = _moments.Select((m, i) => (m, i)).OrderBy(x => x.m.rank).ThenBy(x => x.i).Take(max)
                .OrderBy(x => x.m.wave).ThenBy(x => x.i);
            return keep.Select(x => $"Wave {x.m.wave}: {x.m.text}").ToList();
        }
    }
}
