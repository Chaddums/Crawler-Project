using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The run's difficulty curve and the money that keeps pace with it, read from
    /// Data/difficulty_scaling.json ("climb" and "economy").
    ///
    /// Every wave is harder than the last from wave 1: enemy health follows a table of
    /// (wave, multiplier) points, joined smoothly (log-linear) and compounding at
    /// "hp_growth_after" past the last point. Enemy damage grows a little each wave. A leak
    /// costs the Spire a set amount for the wave (never a share of the enemy's health), capped
    /// at a share of the Spire, so one enemy getting through never ends a run.
    ///
    /// Rewards grow with the waves too: kill bounties, the wave bonus past the authored waves,
    /// resource nodes, interest on what is banked, and the greed bonus for sending waves early.
    /// </summary>
    public static class Climb
    {
        private static bool _loaded;

        // ── Climb ──
        private static List<(int wave, float mult)> _hp = new()
        {
            (1, 1f), (3, 1.8f), (5, 2.8f), (10, 4.5f), (15, 5.5f), (20, 7f), (30, 16f), (40, 30f), (50, 52f),
        };
        private static float _hpGrowthAfter = 1.055f;
        private static float _damagePerWave = 0.02f;
        private static float _leakBase = 8f, _leakPerWave = 0.6f;
        private static float _leakCommanderMult = 2.5f, _leakBossMult = 4f;
        private static float _leakCapShare = 0.12f, _leakCapShareBig = 0.25f;
        private static float _spireMend = 0.04f;

        // ── Economy ──
        private static float _bountyPerWave = 0.02f;
        private static float _bonusAfterAuthored = 50f, _bonusGrowth = 1.07f;
        private static float _nodePerWave = 0.04f;
        private static float _interestPct = 5f, _interestCapBase = 10f, _interestCapPerWave = 3f;
        private static float _greedBountyPerStack = 0.2f, _greedBonusPerStack = 0.25f;
        private static float _towerCostStep = 0.06f;

        /// <summary>Enemy health at a wave, times what the wave data says (1 at wave 1).</summary>
        public static float Hp(int wave)
        {
            Load();
            if (wave <= _hp[0].wave) return _hp[0].mult;
            for (int i = 1; i < _hp.Count; i++)
            {
                if (wave > _hp[i].wave) continue;
                var (w0, m0) = _hp[i - 1];
                var (w1, m1) = _hp[i];
                float t = (wave - w0) / (float)Math.Max(1, w1 - w0);
                // Log-linear: each wave between two points grows by the same share
                return m0 * Mathf.Pow(m1 / m0, t);
            }
            var (wl, ml) = _hp[_hp.Count - 1];
            return ml * Mathf.Pow(_hpGrowthAfter, wave - wl);
        }

        /// <summary>How much harder this wave's enemies are than the last one's (1.12 = 12% more health).</summary>
        public static float StepAt(int wave) => wave <= 1 ? 1f : Hp(wave) / Hp(wave - 1);

        /// <summary>Enemy damage at a wave: grows a share a wave.</summary>
        public static float Damage(int wave) { Load(); return 1f + _damagePerWave * Math.Max(0, wave - 1); }

        /// <summary>
        /// What one enemy that reaches the Spire takes off it. A set amount for the wave, more for
        /// commanders and bosses, capped at a share of the Spire's full health.
        /// </summary>
        public static float LeakDamage(int wave, float spireMax, bool commander, bool boss)
        {
            Load();
            float plain = _leakBase + _leakPerWave * Math.Max(0, wave);
            float mult = boss ? _leakBossMult : commander ? _leakCommanderMult : 1f;
            float cap = spireMax * (boss || commander ? _leakCapShareBig : _leakCapShare);
            return Mathf.Min(plain * mult, cap);
        }

        /// <summary>Share of its full health the Spire mends when a wave clears.</summary>
        public static float SpireMend { get { Load(); return _spireMend; } }

        /// <summary>Kill bounties at a wave, times the enemy's own value.</summary>
        public static float Bounty(int wave) { Load(); return 1f + _bountyPerWave * Math.Max(0, wave - 1); }

        /// <summary>The wave-clear bonus for a wave past the authored ones.</summary>
        public static int ProceduralBonus(int wave, int authoredCount)
        {
            Load();
            return Mathf.RoundToInt(_bonusAfterAuthored * Mathf.Pow(_bonusGrowth, Math.Max(0, wave - authoredCount)));
        }

        /// <summary>What a captured resource node pays a tick at a wave, times its base.</summary>
        public static float NodeRate(int wave) { Load(); return 1f + _nodePerWave * Math.Max(0, wave - 1); }

        /// <summary>Interest paid on banked Resources when a wave clears (percent).</summary>
        public static float InterestPct { get { Load(); return _interestPct; } }

        /// <summary>The most interest one wave clear can pay.</summary>
        public static int InterestCap(int wave) { Load(); return Mathf.RoundToInt(_interestCapBase + _interestCapPerWave * Math.Max(0, wave)); }

        /// <summary>Kill bounty bonus for an enemy of a wave sent <paramref name="depth"/> waves early (1 + share x depth).</summary>
        public static float GreedBounty(int depth) { Load(); return 1f + _greedBountyPerStack * Math.Max(0, depth); }

        /// <summary>Wave-clear bonus for a wave sent <paramref name="depth"/> waves early.</summary>
        public static float GreedBonus(int depth) { Load(); return 1f + _greedBonusPerStack * Math.Max(0, depth); }

        /// <summary>
        /// Supply: each tower of a type already standing makes the next of that type this share
        /// dearer (walls and drills have their own prices). Mixing types is cheaper than piling
        /// up one, and past a point an upgrade is worth more than another tower.
        /// </summary>
        public static float TowerCostStep { get { Load(); return _towerCostStep; } }

        /// <summary>Towers of this type standing now.</summary>
        public static int Standing(VineNodeType type)
        {
            if (Engine.GetMainLoop() is not SceneTree tree) return 0;
            int n = 0;
            foreach (var node in tree.GetNodesInGroup(Constants.GROUP_VINE_NODE))
                if (node is VineNode vn && !vn.IsDestroyed && vn.Data?.Type == type && vn.IsInsideTree()) n++;
            return n;
        }

        /// <summary>The supply price of the next tower of a type: base x (1 + step x standing).</summary>
        public static int SupplyCost(int baseCost, int standing)
            => Mathf.Max(1, Mathf.RoundToInt(baseCost * (1f + TowerCostStep * Math.Max(0, standing))));

        /// <summary>Tests: read the file again (after changing it, or to undo a test's overrides).</summary>
        public static void Reload() { _loaded = false; Load(); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            const string path = "res://Data/difficulty_scaling.json";
            if (!Godot.FileAccess.FileExists(path)) return;
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) return;
            var d = parsed.AsGodotDictionary();
            if (d.ContainsKey("climb") && d["climb"].VariantType == Variant.Type.Dictionary)
            {
                var c = d["climb"].AsGodotDictionary();
                float F(string k, float v) => c.ContainsKey(k) ? (float)c[k].AsDouble() : v;
                if (c.ContainsKey("hp") && c["hp"].VariantType == Variant.Type.Array)
                {
                    var points = new List<(int, float)>();
                    foreach (var p in c["hp"].AsGodotArray())
                    {
                        var pair = p.AsGodotArray();
                        if (pair.Count < 2) continue;
                        points.Add(((int)pair[0].AsDouble(), Mathf.Max(0.05f, (float)pair[1].AsDouble())));
                    }
                    points.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    if (points.Count > 0) _hp = points;
                }
                _hpGrowthAfter = F("hp_growth_after", _hpGrowthAfter);
                _damagePerWave = F("damage_per_wave", _damagePerWave);
                _leakBase = F("leak_base", _leakBase);
                _leakPerWave = F("leak_per_wave", _leakPerWave);
                _leakCommanderMult = F("leak_commander_mult", _leakCommanderMult);
                _leakBossMult = F("leak_boss_mult", _leakBossMult);
                _leakCapShare = F("leak_cap_share", _leakCapShare);
                _leakCapShareBig = F("leak_cap_share_big", _leakCapShareBig);
                _spireMend = F("spire_mend_share", _spireMend);
            }
            if (d.ContainsKey("economy") && d["economy"].VariantType == Variant.Type.Dictionary)
            {
                var e = d["economy"].AsGodotDictionary();
                float F(string k, float v) => e.ContainsKey(k) ? (float)e[k].AsDouble() : v;
                _bountyPerWave = F("bounty_per_wave", _bountyPerWave);
                _bonusAfterAuthored = F("bonus_after_authored", _bonusAfterAuthored);
                _bonusGrowth = F("bonus_growth", _bonusGrowth);
                _nodePerWave = F("node_per_wave", _nodePerWave);
                _interestPct = F("interest_pct", _interestPct);
                _interestCapBase = F("interest_cap_base", _interestCapBase);
                _interestCapPerWave = F("interest_cap_per_wave", _interestCapPerWave);
                _greedBountyPerStack = F("greed_bounty_per_stack", _greedBountyPerStack);
                _greedBonusPerStack = F("greed_bonus_per_stack", _greedBonusPerStack);
                _towerCostStep = F("tower_cost_step", _towerCostStep);
            }
        }
    }
}
