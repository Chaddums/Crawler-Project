using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Waves come in themed blocks (Data/wave_themes.json): from <see cref="FirstWave"/>, every
    /// <see cref="BlockSize"/> waves share one theme (armour, flyers, shields, a swarm, hulks),
    /// drawn so each comes once in every round of five and never twice in a row. A trait theme
    /// gives its trait to most of the wave and takes the other counter traits away, so the build
    /// that answers it holds and the one that doesn't leaks; the wave card names the block, its
    /// answer and the next block, so a player can get ready. Every deep wave used to be forced to
    /// mix two of armour, shields and flight, so a balanced build was always right and every run,
    /// smart or careless, ended near the same wave (2026-11). The test harness sets <see cref="Off"/>.
    /// </summary>
    public static class WaveThemes
    {
        public class Theme
        {
            public string Id, Name, What, Answer;
            public EnemyTraits Trait;
            public float Share = 0.75f, HpMult = 1f, SpeedMult = 1f, CountMult = 1f;
            public int CountMax = 40;
            public Color Color = Colors.White;
        }

        public static int FirstWave { get; private set; } = 6;
        public static int BlockSize { get; private set; } = 5;
        public static IReadOnlyList<Theme> All => _themes;
        /// <summary>Authored and procedural waves as they were (the test harness sets it).</summary>
        public static bool Off { get; set; }

        private static readonly List<Theme> _themes = new();
        private static readonly List<Theme> _order = new();
        private static RandomNumberGenerator _rng;
        private const EnemyTraits Counters = EnemyTraits.Armoured | EnemyTraits.Shielded | EnemyTraits.Flying;

        static WaveThemes() { Load(); }

        /// <summary>A new run draws its own order of themes (WAVE_SEED fixes it).</summary>
        public static void BeginRun()
        {
            _order.Clear();
            _rng = new RandomNumberGenerator();
            if (ulong.TryParse(System.Environment.GetEnvironmentVariable("WAVE_SEED"), out var seed))
                _rng.Seed = seed * 7919UL + 17UL;
            else
                _rng.Randomize();
        }

        /// <summary>The block a wave is in (0 for the first themed block), or -1 before the first.</summary>
        public static int BlockOf(int wave) => wave < FirstWave ? -1 : (wave - FirstWave) / BlockSize;
        public static int BlockStart(int block) => FirstWave + block * BlockSize;
        public static int BlockEnd(int block) => BlockStart(block) + BlockSize - 1;

        /// <summary>The theme of a wave, or null (before the first block, themes off or none loaded).</summary>
        public static Theme At(int wave)
        {
            if (Off || _themes.Count == 0) return null;
            int block = BlockOf(wave);
            return block < 0 ? null : ThemeOfBlock(block);
        }

        public static Theme ThemeOfBlock(int block)
        {
            if (Off || _themes.Count == 0 || block < 0) return null;
            if (_rng == null) BeginRun();
            // Rounds of five: each theme once a round, the first of a round never the last of the one before
            while (_order.Count <= block)
            {
                var bag = _themes.ToList();
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = _rng.RandiRange(0, i);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }
                if (_order.Count > 0 && bag.Count > 1 && bag[0] == _order[^1]) (bag[0], bag[1]) = (bag[1], bag[0]);
                _order.AddRange(bag);
            }
            return _order[block];
        }

        /// <summary>The wave as its block makes it (a copy; boss surges are left alone), or the wave itself without a theme.</summary>
        public static VineWaveData Apply(VineWaveData src, int wave)
        {
            var theme = At(wave);
            if (src == null || theme == null) return src;
            var w = new VineWaveData
            {
                WaveNumber = src.WaveNumber > 0 ? src.WaveNumber : wave,
                Name = src.Name,
                BonusResources = src.BonusResources,
                IsBossWave = src.IsBossWave,
                CompletionMode = src.CompletionMode,
                CompletionTimer = src.CompletionTimer,
                CompletionKillCount = src.CompletionKillCount,
            };
            foreach (var s in src.Surges) w.Surges.Add(s.Clone());
            var plain = w.Surges.Where(s => !s.IsBoss).ToList();
            if (plain.Count == 0) return w;

            if (theme.Trait != EnemyTraits.None)
            {
                foreach (var s in plain) s.Traits &= ~Counters;
                int total = plain.Sum(s => s.Count), given = 0;
                foreach (var s in plain.OrderByDescending(s => s.Count))
                {
                    if (given >= theme.Share * total) break;
                    // Siege guns bed down on the ground
                    if (theme.Trait == EnemyTraits.Flying && (s.Traits & EnemyTraits.Siege) != 0) continue;
                    // Only light enemies fly: a heavy surge comes as Sky Buzzers with its health
                    if (theme.Trait == EnemyTraits.Flying && s.Faction is not (VineEnemyFaction.Swarm or VineEnemyFaction.Scavenger))
                    {
                        s.Faction = VineEnemyFaction.Swarm;
                        s.EnemyName = "Sky Buzzer";
                        s.AttackRange = s.AttackDamage = s.AttackInterval = 0f;
                    }
                    s.Traits |= theme.Trait;
                    s.Health *= theme.HpMult;
                    s.Speed *= theme.SpeedMult;
                    given += s.Count;
                }
            }
            if (!Mathf.IsEqualApprox(theme.CountMult, 1f))
                foreach (var s in plain)
                {
                    int n = Math.Max(1, (int)Math.Round(s.Count * theme.CountMult));
                    if (theme.CountMult > 1f) n = Math.Min(n, Math.Max(s.Count, theme.CountMax));
                    if ((s.Traits & EnemyTraits.Siege) != 0 && n > Siege.MaxPerSurge) n = Math.Max(s.Count, Siege.MaxPerSurge);
                    // The same health in all, over the same time: more of them weaker and closer
                    // together, or fewer of them tougher and further apart. With the spacing left
                    // alone a hulk block brought its health in half the time (it finished every bot
                    // run, 27 to 58 leaks a block) and a swarm in twice the time (the easiest block).
                    float k = (float)s.Count / n;
                    s.Health *= k;
                    s.SpawnInterval *= k;
                    s.Count = n;
                }
            return w;
        }

        /// <summary>The wave card's line for the next wave's block and the one after it, or null.</summary>
        public static string CardLine(int wave)
        {
            var t = At(wave);
            if (t == null)
            {
                var first = ThemeOfBlock(0);
                return first == null || wave > FirstWave ? null : $"From W{FirstWave}: {first.Name}, {first.What}. Answer: {first.Answer}";
            }
            int block = BlockOf(wave);
            var next = ThemeOfBlock(block + 1);
            return $"{t.Name} to W{BlockEnd(block)}: {t.What}. Answer: {t.Answer}"
                + (next != null ? $"\nThen {next.Name} (W{BlockStart(block + 1)}): {next.What}" : "");
        }

        private static void Load()
        {
            const string path = "res://Data/wave_themes.json";
            if (!FileAccess.FileExists(path)) return;
            var json = new Json();
            if (json.Parse(FileAccess.GetFileAsString(path)) != Error.Ok || json.Data.Obj is not Godot.Collections.Dictionary d)
            {
                GD.PushWarning("[WaveThemes] Data/wave_themes.json didn't parse; no themes");
                return;
            }
            if (d.TryGetValue("firstWave", out var fw)) FirstWave = (int)fw.AsInt64();
            if (d.TryGetValue("blockSize", out var bs)) BlockSize = Math.Max(1, (int)bs.AsInt64());
            if (!d.TryGetValue("themes", out var arr) || arr.Obj is not Godot.Collections.Array list) return;
            foreach (var v in list)
            {
                if (v.Obj is not Godot.Collections.Dictionary t) continue;
                float F(string k, float def) => t.TryGetValue(k, out var x) ? (float)x.AsDouble() : def;
                string S(string k) => t.TryGetValue(k, out var x) ? x.AsString() : "";
                var theme = new Theme
                {
                    Id = S("id"), Name = S("name"), What = S("what"), Answer = S("answer"),
                    Share = F("share", 0.75f), HpMult = F("hpMult", 1f), SpeedMult = F("speedMult", 1f),
                    CountMult = F("countMult", 1f), CountMax = (int)F("countMax", 40f),
                    Trait = S("trait").ToLowerInvariant() switch
                    {
                        "armoured" => EnemyTraits.Armoured,
                        "flying" => EnemyTraits.Flying,
                        "shielded" => EnemyTraits.Shielded,
                        _ => EnemyTraits.None,
                    },
                };
                if (t.TryGetValue("color", out var c) && c.Obj is Godot.Collections.Array ca && ca.Count >= 3)
                    theme.Color = new Color((float)ca[0].AsDouble(), (float)ca[1].AsDouble(), (float)ca[2].AsDouble());
                _themes.Add(theme);
            }
        }
    }
}
