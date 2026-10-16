using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The threat readout: how a coming wave (or a stack of waves sent at once) measures up
    /// against the defence as it stands, so the stakes are clear before the wave is sent.
    ///
    /// Each enemy group of the wave is weighed against every tower: the tower's damage a second
    /// (with every bonus), how its hits land on the group's armour and shields, whether it reaches
    /// flyers, and how long one enemy of the group spends in its reach along the route it walks
    /// (slowed by tar and rams; flyers fly straight). A tower busy with one group can't shoot
    /// another, so time is shared when groups overlap. BIT and the Spire's guns count too. The
    /// ratio of damage the defence can deal to the health coming at it gives the verdict:
    /// Holding, Tight or Breaking, with the enemies likely to get through and what they would
    /// cost the Spire, and the weak spot that lets them through.
    ///
    /// It is a forecast, not a simulation: it assumes towers keep their targets and BIT fights
    /// part of the time. The calibration (Uptime) is set from autoplay runs.
    /// </summary>
    public static class ThreatModel
    {
        public enum Verdict { Holding, Tight, Breaking }

        /// <summary>
        /// Calibration of the defence's damage against what happens (autoplay). It was 1: after
        /// the wreck and tower-toughness changes the card called BREAKING on waves that leaked
        /// nothing (raw 0.5 to 0.7x leaked a third of the time, about one enemy; 0.7 to 1.15x
        /// almost never), so a player learned to ignore it. At 1.6 the bands match again: under
        /// 0.5x most waves leak several, 0.8 to 1.15x a few leak one or two.
        /// </summary>
        public const float Uptime = 1.6f;
        /// <summary>The weakest group needs this much more damage than health to be "Holding".</summary>
        public const float HoldingAt = 1.6f;
        /// <summary>Below this, some get through (towers lost mid-wave, crowds at the door). From autoplay runs.</summary>
        public const float BreakingAt = 1.15f;
        /// <summary>BREAKING: the leaks coming would take at least this share of the Spire's current health.</summary>
        public const float SeriousShare = 0.16f;
        /// <summary>Share of BIT's damage counted (it moves, docks, dies).</summary>
        public const float BitShare = 0.35f;
        /// <summary>
        /// Share of anti-air damage that lands on flyers: they come in a clump straight over
        /// everything, so guns that also face ground enemies turn too late (autoplay: a flying
        /// surge the plain estimate gave 1.05x got through whole).
        /// </summary>
        public const float FlyerShare = 0.6f;


        public class Report
        {
            public int From, To;
            /// <summary>Enemy health coming (with the climb).</summary>
            public float Health;
            /// <summary>Damage the defence can deal to it.</summary>
            public float Damage;
            /// <summary>Damage over health for the weakest group (the one most likely to get through).</summary>
            public float Ratio = float.PositiveInfinity;
            public Verdict Verdict;
            /// <summary>Enemies likely to get through, and what they'd take off the Spire.</summary>
            public int Leaks;
            public float SpireDamage;
            /// <summary>The weak spot, in words ("Flyers: nothing but BIT reaches them"), or null.</summary>
            public string Gap;
            /// <summary>The Overclock cast last wave is counted (the card says so).</summary>
            public bool Overclock;
            public EnemyTraits GapTrait;
            /// <summary>Enemies in it.</summary>
            public int Enemies;
            /// <summary>What clearing it pays: wave bonuses (greed included), the greed part, kills, drills, interest.</summary>
            public int Bonus, Greed, Kills, Drills, Interest;
            public int Pay => Bonus + Kills + Drills + Interest;
            /// <summary>Share of the defence's damage left once enemy fire has taken its towers (1: none lost).</summary>
            public float TowersKept = 1f;
            /// <summary>Each enemy group and how it measures up (bots and tests).</summary>
            public List<GroupResult> Groups = new();

            public string VerdictWord => Verdict switch { Verdict.Holding => "HOLDING", Verdict.Tight => "TIGHT", _ => "BREAKING" };
        }

        public struct GroupResult
        {
            public EnemyTraits Traits;
            public int Count, Wave;
            public float Health, Ratio;
            public bool Flying => (Traits & EnemyTraits.Flying) != 0;
        }

        private struct Group
        {
            public int Count, Wave, Entry;
            public float Hp, Speed, Start, Span, Value;
            /// <summary>Its gun: damage a second and reach (0: none).</summary>
            public float GunDps, GunRange;
            public EnemyTraits Traits;
            public VineEnemyFaction Faction;
            public bool Boss, Commander;
        }

        private struct Defender
        {
            public Vector3 Pos;
            public float Hp;
            public VineNode.Firepower F;
            public bool Roams; // BIT: anywhere, part of the time
        }

        private class Route
        {
            public List<Vector3> Points = new();
            public bool Air;
            /// <summary>The entry region it starts from (VineEntryRegion.Index).</summary>
            public int Entry = -1;
            // Per point: slow there (ground routes)
            public List<float> Slow = new();
        }

        /// <summary>Waves <paramref name="from"/> to <paramref name="to"/> sent together (one wave when equal).</summary>
        public static Report Assess(int from, int to, bool sentEarly = false)
        {
            var r = new Report { From = from, To = to };
            if (!ServiceLocator.TryGet<VineWaveManager>(out var wm) || !ServiceLocator.TryGet<VineGrid>(out var grid)) return r;
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null) return r;

            // ── The enemies ──
            var groups = new List<Group>();
            int firstDepth = sentEarly ? wm.NextDepth : 0;
            // Sent while a wave runs: what's still on the field (and still to come) fights alongside
            if (sentEarly && wm.WaveActive && wm.FieldFrom > 0)
            {
                float share = Mathf.Clamp(wm.EnemiesRemaining / (float)Mathf.Max(1, wm.FieldTotal), 0f, 1f);
                for (int w = wm.FieldFrom; w < from; w++)
                {
                    var data = wm.WaveDataAt(w);
                    if (data == null) continue;
                    float climb = Climb.EnemyHp(w) * (CorruptionManager.IsChaosWave(w) ? 1.3f : 1f);
                    foreach (var s in data.Surges)
                    {
                        int left = Mathf.RoundToInt(s.Count * share);
                        if (left <= 0) continue;
                        groups.Add(new Group
                        {
                            Count = left, Wave = w, Entry = s.EntryIndex, Hp = s.Health * climb, Speed = Mathf.Max(0.5f, s.Speed),
                            Start = 0f, Span = Mathf.Max(0.5f, left * Mathf.Max(0.05f, s.SpawnInterval)),
                            Traits = s.Traits, Faction = s.Faction, Boss = s.IsBoss,
                        });
                    }
                }
            }
            for (int w = from; w <= to; w++)
            {
                var data = wm.WaveDataAt(w);
                if (data == null) continue;
                // AXIS chaos: for most of the wave every enemy is berserk (tougher, takes less),
                // and three drops of reinforcements land in the field (below); the wave's own
                // enemies carry CHAOS_WAVE_HP_SHARE of the climb
                bool chaosWave = CorruptionManager.IsChaosWave(w);
                float climb = Climb.EnemyHp(w) * (chaosWave ? CorruptionManager.CHAOS_WAVE_HP_SHARE : 1f);
                float chaos = chaosWave ? 1f + 0.7f * (CorruptionManager.CHAOS_HP_MULT / CorruptionManager.CHAOS_TAKEN - 1f) : 1f;
                float offset = (w - from) * Constants.STACK_STAGGER;
                int depth = firstDepth + (w - from);
                // Drops are worth more while chaos is on (most of the wave)
                float pay = Climb.Bounty(w) * (chaosWave ? Climb.ChaosDropMult : 1f);
                foreach (var s in data.Surges)
                {
                    if (s.Count <= 0) continue;
                    float span = Mathf.Max(0.5f, s.Count * Mathf.Max(0.05f, s.SpawnInterval));
                    groups.Add(new Group
                    {
                        Count = s.Count, Wave = w, Entry = s.EntryIndex,
                        Hp = s.Health * climb * chaos, Speed = Mathf.Max(0.5f, s.Speed),
                        Start = offset + s.StartDelay, Span = span,
                        GunRange = s.AttackRange, GunDps = s.AttackRange > 0f && s.AttackInterval > 0f ? s.AttackDamage / s.AttackInterval : 0f,
                        Traits = s.Traits, Faction = s.Faction, Boss = s.IsBoss,
                        Value = s.ResourceValue * pay * Climb.GreedBounty(depth),
                    });
                    if (s.Commander != null)
                        groups.Add(new Group
                        {
                            Count = 1, Wave = w, Entry = s.EntryIndex,
                            Hp = s.Commander.Health * climb * chaos, Speed = Mathf.Max(0.5f, s.Commander.Speed),
                            Start = offset + s.StartDelay, Span = 0.5f,
                            Faction = s.Commander.Faction, Commander = true,
                            Value = s.Commander.ResourceValue * pay * Climb.GreedBounty(depth) * MetaRun.BountyMult,
                        });
                }
                if (chaosWave && data.Surges.Count > 0)
                {
                    // Reinforcements: drawn from the wave's own enemies, tougher, landing mid-field
                    var s0 = data.Surges.OrderByDescending(s => s.Count).First();
                    groups.Add(new Group
                    {
                        Count = 3 * CorruptionManager.DropSize(w), Wave = w, Entry = -1,
                        Hp = s0.Health * climb * CorruptionManager.CHAOS_DROP_HP_MULT * chaos, Speed = Mathf.Max(0.5f, s0.Speed),
                        Start = offset + 20f, Span = 24f, Traits = s0.Traits, Faction = s0.Faction,
                        Value = s0.ResourceValue * pay,
                    });
                }
                r.Bonus += wm.WaveBonusAt(w, depth);
                r.Greed += wm.WaveBonusAt(w, depth) - wm.WaveBonusAt(w, 0);
            }
            if (groups.Count == 0) return r;

            // ── The defence and the routes (built once a frame: the card asks about several waves) ──
            var def = DefenceNow(grid, tree);
            r.Overclock = OverclockHabit.Any;
            var defenders = def.Defenders;
            var ground = def.Ground;
            var air = def.Air;
            if (ground.Count == 0) return r;

            // ── Damage each defender can put into each group ──
            float corrode = PowerType.Chaos ? 1f + 0.6f * PowerType.CorrodeStacks * PowerType.CorrodePerStack : 1f;
            // Environment's hit slow: about that much longer in every tower's reach once a tower has hit it
            if (PowerType.Environment) corrode *= 1f / (1f - 0.7f * PowerType.HitSlow);
            float earliest = float.MaxValue, latest = 0f;
            foreach (var g in groups) { earliest = Mathf.Min(earliest, g.Start); latest = Mathf.Max(latest, g.Start + g.Span); }

            var dealt = new float[groups.Count];
            // One pair of scratch rows for every defender (a pair each was most of what the card
            // allocated with 300 towers, every refresh, for each wave it looks at)
            var contact = new float[groups.Count];
            var rate = new float[groups.Count];
            for (int di = 0; di < defenders.Count; di++)
            {
                var d = defenders[di];
                if (d.F.Dps <= 0f) continue;
                System.Array.Clear(contact);
                System.Array.Clear(rate);
                float busy = 0f, longest = 0f;
                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    bool flying = (g.Traits & EnemyTraits.Flying) != 0;
                    if (flying ? !d.F.Air : !d.F.Ground) continue;
                    float exposure = def.Time(di, flying, g);
                    if (exposure <= 0f) continue;
                    float eff = HitShare(d.F.Kind, g.Traits) * (flying ? d.F.AirMult * FlyerShare : 1f) * corrode;
                    // How many of the group are in reach at once, on average (multi-target towers use it)
                    float together = Mathf.Max(1f, g.Count * exposure / (g.Span + exposure));
                    contact[i] = Mathf.Min(g.Count * exposure, g.Span + exposure);
                    rate[i] = d.F.Dps * eff * Mathf.Min(d.F.Targets, together);
                    busy += contact[i];
                    longest = Mathf.Max(longest, exposure);
                }
                // One tower can't be in two places: when groups overlap, its time is shared
                float window = (latest - earliest) + longest;
                float share = busy > window && busy > 0f ? window / busy : 1f;
                for (int i = 0; i < groups.Count; i++)
                    dealt[i] += rate[i] * contact[i] * share * Uptime;
            }

            // ── Towers lost on the way: enemies shoot the towers they pass, and a tower that falls
            // halfway through stops shooting. Few towers against a big stack lose most of the
            // fight this way (autoplay: five turrets under three waves sent at once) ──
            float towerHp = 0f;
            foreach (var d in defenders) if (!d.Roams && d.Hp > 0f) towerHp += d.Hp;
            if (towerHp > 0f)
            {
                var nearestGround = def.NearestGround;
                var nearestAir = def.NearestAir;
                float shooting = 0f;
                foreach (var g in groups)
                {
                    float dps = GunOf(g, out float reach);
                    if (dps <= 0f) continue;
                    bool flying = (g.Traits & EnemyTraits.Flying) != 0;
                    // Seconds one enemy spends with a tower in its reach, along its route
                    float near = 0f;
                    var nearest = flying ? nearestAir : nearestGround;
                    int nr = 0;
                    foreach (var dists in nearest)
                    {
                        float t = 0f;
                        foreach (float dist in dists) if (dist <= reach) t += Constants.VINE_CELL_SIZE / g.Speed;
                        near += t; nr++;
                    }
                    if (nr > 0) near /= nr;
                    shooting += g.Count * dps * Climb.Damage(g.Wave) * Climb.TowerTaken(g.Wave) * near * 0.5f; // half the time it's shooting something else or reloading
                }
                float lost = Mathf.Min(towerHp, shooting);
                float keep = 1f - 0.5f * lost / towerHp;
                for (int i = 0; i < dealt.Length; i++) dealt[i] *= keep;
                r.TowersKept = keep;
            }

            // ── Verdict ──
            float spireMax = grid.Harvester?.MaxHP ?? 200f;
            var worstTrait = EnemyTraits.None;
            float worst = float.PositiveInfinity;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                float hp = g.Count * g.Hp;
                r.Health += hp;
                r.Damage += Mathf.Min(dealt[i], hp * 3f);
                r.Enemies += g.Count;
                r.Kills += Mathf.RoundToInt(g.Count * g.Value);
                float ratio = hp > 0f ? dealt[i] / hp : float.PositiveInfinity;
                r.Groups.Add(new GroupResult { Traits = g.Traits, Count = g.Count, Wave = g.Wave, Health = hp, Ratio = ratio });
                if (ratio < worst && hp > 0.03f * Mathf.Max(1f, r.Health))
                {
                    worst = ratio;
                    worstTrait = g.Traits & (EnemyTraits.Armoured | EnemyTraits.Flying | EnemyTraits.Shielded);
                }
                if (ratio < BreakingAt)
                {
                    // The share that gets through grows with the square of the shortfall. Autoplay
                    // (six runs, 2026-10): under 0.5x every wave leaked; 0.5 to 0.8x, 57% of waves,
                    // about 2 enemies each; 0.8 to 1.15x, 22%; 1.15 to 1.6x, 15%; above 1.6x, 2%
                    float short_ = Mathf.Clamp((BreakingAt - ratio) / BreakingAt, 0f, 1f);
                    int through = Mathf.Clamp(Mathf.RoundToInt(g.Count * short_ * short_), 0, g.Count);
                    if (through == 0) through = 1;
                    r.Leaks += through;
                    r.SpireDamage += through * Climb.LeakDamage(g.Wave, spireMax, g.Commander, g.Boss && !g.Commander);
                }
            }
            r.Ratio = worst;
            // BREAKING means the Spire gets hurt badly (a sixth of what it has left, or the weakest
            // group taking under half the damage it needs). It used to be any leak at all: with
            // leaks cheap and the Spire mending, the card said BREAKING wave after wave while
            // nothing happened, then the wave that did break looked like every other
            float spireNow = Mathf.Max(1f, grid.Harvester?.CurrentHP ?? spireMax);
            bool serious = r.SpireDamage >= SeriousShare * spireNow || worst < BreakingAt * 0.45f;
            r.Verdict = serious ? Verdict.Breaking : r.Leaks > 0 || worst < HoldingAt ? Verdict.Tight : Verdict.Holding;
            r.GapTrait = worstTrait;
            if (r.Verdict != Verdict.Holding) r.Gap = GapWords(worstTrait, defenders);
            r.Drills = SalvageDrill.PayoutFor(from, to);
            r.Interest = VineWaveManager.InterestOn(GameManager.Instance?.CurrentResources ?? 0, to);
            return r;
        }

        /// <summary>The first wave from <paramref name="from"/> (within <paramref name="horizon"/>) the defence as it stands would break on, or -1.</summary>
        public static int BreaksAt(int from, int horizon = 8)
        {
            for (int w = from; w < from + horizon; w++)
                if (Assess(w, w).Verdict == Verdict.Breaking) return w;
            return -1;
        }

        /// <summary>
        /// The defence as it stands and the routes enemies take, with what each tower reaches of
        /// each route summed once (the slow there counted in), so a wave or a stack is weighed in
        /// a moment. Kept for the frame it was built in.
        /// </summary>
        private class Defence
        {
            public List<Defender> Defenders = new();
            public List<Route> Ground = new(), Air = new();
            public List<float[]> NearestGround, NearestAir;
            // Per defender, per route: the sum over points in reach of 1 / (1 - slow) (full, and Brutes' half)
            public float[][] GroundSum, GroundSumBrute, AirSum;

            /// <summary>Seconds one enemy of the group spends in defender <paramref name="di"/>'s reach, on average over its routes.</summary>
            public float Time(int di, bool flying, Group g)
            {
                var routes = flying ? Air : Ground;
                var sums = flying ? AirSum[di] : g.Faction == VineEnemyFaction.Brute ? GroundSumBrute[di] : GroundSum[di];
                float total = 0f;
                int n = 0;
                // The group's own entry, if it has a route (no lambda: this runs for every defender
                // and group, and the closures were the biggest allocation with 300 towers)
                bool own = false;
                if (g.Entry >= 0)
                    for (int ri = 0; ri < routes.Count && !own; ri++) own = routes[ri].Entry == g.Entry;
                for (int ri = 0; ri < routes.Count; ri++)
                {
                    if (own && routes[ri].Entry != g.Entry) continue;
                    total += sums[ri];
                    n++;
                }
                return n > 0 ? total / n * Constants.VINE_CELL_SIZE / g.Speed : 0f;
            }
        }

        private static Defence _defence;
        private static ulong _defenceFrame = ulong.MaxValue;
        private static long _defenceSig;

        private static Defence DefenceNow(VineGrid grid, SceneTree tree)
        {
            ulong frame = Engine.GetProcessFrames();
            // Same frame and the same towers (a bot buying several in one frame changes them)
            long sig = 0;
            foreach (var n in Roster.Nodes(tree))
                if (GodotObject.IsInstanceValid(n) && !n.IsDestroyed)
                    sig = sig * 31 + n.GridPosition.X * 977 + n.GridPosition.Y * 131 + n.Level * 7 + (n.Branch != null ? 3 : 0) + n.Overcharge * 11;
            sig = sig * 31 + OverclockHabit.Version;
            if (_defence != null && _defenceFrame == frame && _defenceSig == sig) return _defence;
            _defenceSig = sig;
            var def = new Defence();
            foreach (var n in Roster.Nodes(tree))
            {
                if (!GodotObject.IsInstanceValid(n) || n.IsDestroyed || n.Data?.Category != VineNodeCategory.Effect) continue;
                var f = n.GetFirepower();
                if (f.Range <= 0f || (f.Dps <= 0f && f.Slow <= 0f)) continue;
                // The Overclock this tower got last wave, if the player has been casting it
                f.Dps *= OverclockHabit.Gain(n);
                def.Defenders.Add(new Defender { Pos = n.GlobalPosition, F = f, Hp = n.NodeCurrentHealth });
            }
            if (grid.Harvester != null && grid.Harvester.GunDps > 0f)
                def.Defenders.Add(new Defender
                {
                    Pos = grid.Harvester.GlobalPosition,
                    F = new VineNode.Firepower { Dps = grid.Harvester.GunDps, Targets = 1f, Range = grid.Harvester.GunRange, Kind = DamageKind.Normal, Air = true, Ground = true, AirMult = 1f },
                });
            if (ServiceLocator.TryGet<VinePlayer>(out var bit) && bit.IsAlive)
                def.Defenders.Add(new Defender
                {
                    Roams = true,
                    F = new VineNode.Firepower { Dps = bit.EffectiveDamage * bit.EffectiveAttackSpeed * BitShare, Targets = 1f, Kind = DamageKind.Heavy, Air = true, Ground = true, AirMult = 1f },
                });

            // The maze from each open entry, and the straight line flyers take
            var exit = grid.GridToWorld(grid.ExitPoint);
            if (ServiceLocator.TryGet<VinePathfinder>(out var pf))
            {
                foreach (var reg in grid.ActiveEntryRegions)
                {
                    var path = pf.GetCachedPath(reg.Center);
                    var g = new Route { Entry = reg.Index };
                    if (path != null) foreach (var c in path) g.Points.Add(grid.GridToWorld(c));
                    if (g.Points.Count == 0) g.Points.Add(grid.GridToWorld(reg.Center));
                    def.Ground.Add(g);
                    var a = new Route { Air = true, Entry = reg.Index };
                    var start = grid.GridToWorld(reg.Center);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(start.DistanceTo(exit) / Constants.VINE_CELL_SIZE));
                    for (int i = 0; i <= steps; i++) a.Points.Add(start.Lerp(exit, i / (float)steps));
                    def.Air.Add(a);
                }
            }
            foreach (var route in def.Ground)
                foreach (var p in route.Points)
                {
                    float slow = 0f;
                    foreach (var d in def.Defenders)
                        if (d.F.Slow > 0f && !d.Roams && Flat(d.Pos, p) <= d.F.Range) slow = Mathf.Max(slow, d.F.Slow);
                    route.Slow.Add(slow);
                }
            foreach (var route in def.Air) foreach (var _ in route.Points) route.Slow.Add(0f);

            int nd = def.Defenders.Count;
            def.GroundSum = new float[nd][]; def.GroundSumBrute = new float[nd][]; def.AirSum = new float[nd][];
            for (int di = 0; di < nd; di++)
            {
                var d = def.Defenders[di];
                def.GroundSum[di] = new float[def.Ground.Count];
                def.GroundSumBrute[di] = new float[def.Ground.Count];
                def.AirSum[di] = new float[def.Air.Count];
                for (int ri = 0; ri < def.Ground.Count; ri++)
                {
                    var route = def.Ground[ri];
                    for (int i = 0; i < route.Points.Count; i++)
                    {
                        if (!d.Roams && Flat(d.Pos, route.Points[i]) > d.F.Range) continue;
                        def.GroundSum[di][ri] += 1f / Mathf.Max(0.1f, 1f - route.Slow[i]);
                        def.GroundSumBrute[di][ri] += 1f / Mathf.Max(0.1f, 1f - route.Slow[i] * 0.5f);
                    }
                }
                for (int ri = 0; ri < def.Air.Count; ri++)
                {
                    var route = def.Air[ri];
                    for (int i = 0; i < route.Points.Count; i++)
                        if (d.Roams || Flat(d.Pos, route.Points[i]) <= d.F.Range) def.AirSum[di][ri] += 1f;
                }
            }
            def.NearestGround = Nearest(def.Ground, def.Defenders);
            def.NearestAir = Nearest(def.Air, def.Defenders);
            _defence = def;
            _defenceFrame = frame;
            return def;
        }

        private static List<float[]> Nearest(List<Route> routes, List<Defender> defenders)
        {
            var list = new List<float[]>();
            foreach (var route in routes)
            {
                var d = new float[route.Points.Count];
                for (int i = 0; i < d.Length; i++)
                {
                    float best = float.MaxValue;
                    foreach (var t in defenders)
                        if (!t.Roams && t.Hp > 0f) best = Mathf.Min(best, Flat(t.Pos, route.Points[i]));
                    d[i] = best;
                }
                list.Add(d);
            }
            return list;
        }

        /// <summary>A group's gun as it comes (faction defaults unless the wave says), before the climb.</summary>
        private static float GunOf(Group g, out float reach)
        {
            (float range, float damage, float interval) = g.Faction switch
            {
                VineEnemyFaction.Scavenger => (6f, 4f, 1.5f),
                VineEnemyFaction.Brute => (3.6f, 10f, 2.5f),
                VineEnemyFaction.Ghost => (7.2f, 3f, 2f),
                VineEnemyFaction.Swarm => (4.8f, 2f, 1f),
                _ => (Constants.ENEMY_ATTACK_RANGE, Constants.ENEMY_ATTACK_DAMAGE, Constants.ENEMY_FIRE_INTERVAL),
            };
            if (g.GunRange > 0f) { range = g.GunRange; damage = g.GunDps; interval = 1f; }
            reach = range;
            return interval > 0f ? damage / interval : 0f;
        }

        /// <summary>How much of a hit of <paramref name="kind"/> counts against an enemy with <paramref name="traits"/>.</summary>
        public static float HitShare(DamageKind kind, EnemyTraits traits)
        {
            float m = 1f;
            if ((traits & EnemyTraits.Armoured) != 0)
                m *= kind switch
                {
                    DamageKind.Light or DamageKind.Electric => VineEnemy.ARMOUR_LIGHT,
                    DamageKind.Normal => VineEnemy.ARMOUR_NORMAL,
                    _ => 1f,
                };
            if ((traits & EnemyTraits.Shielded) != 0)
            {
                float s = kind switch { DamageKind.Electric => VineEnemy.SHIELD_ELECTRIC, DamageKind.Light => VineEnemy.SHIELD_LIGHT, _ => 1f };
                // The shield is extra health worth SHIELD_SHARE of it, struck at s times
                m *= 1f / (1f + VineEnemy.SHIELD_SHARE / s);
            }
            return m;
        }

        private static string GapWords(EnemyTraits t, List<Defender> defenders)
        {
            bool anyAir = false, anyHeavy = false, anyElectric = false;
            foreach (var d in defenders)
            {
                if (d.Roams || d.F.Dps <= 0f) continue;
                anyAir |= d.F.Air;
                anyHeavy |= d.F.Kind == DamageKind.Heavy;
                anyElectric |= d.F.Kind == DamageKind.Electric;
            }
            if ((t & EnemyTraits.Flying) != 0)
                return anyAir ? "Flyers: not enough anti-air (Flak, Tesla, Minigun)" : "Flyers: nothing but BIT can reach them. Build Flak or Tesla";
            if ((t & EnemyTraits.Armoured) != 0)
                return anyHeavy ? "Armour: not enough heavy guns (Junk Turret, Scatter)" : "Armour: your hits are too light. Build a Junk Turret or Scatter Cannon";
            if ((t & EnemyTraits.Shielded) != 0)
                return anyElectric ? "Shields: not enough Tesla to strip them" : "Shields: nothing strips them. Build a Tesla Coil";
            return "Not enough damage: upgrade, build more, or lengthen the maze";
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    }
}
