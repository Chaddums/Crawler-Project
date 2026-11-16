using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Raiders: from "start_wave" on, while the player holds resource nodes or has Salvage
    /// Drills standing, a few enemies of each wave leave the path to wreck them (Data/
    /// difficulty_scaling.json "raids"). Income out on the map has to be defended, not just built.
    ///
    /// A raider wears a gold marker over its head, walks the maze to its target (Ghosts go
    /// straight), shoots it if it has a gun and hits it if not. When its target is gone it picks
    /// another; with nothing left to raid it goes back to the path. AXIS chaos takes raiders
    /// over like any other enemy.
    /// </summary>
    public partial class RaidManager : Node
    {
        private int _startWave = 3;
        private int _base = 1;
        private int _wavesPerExtra = 6;
        private int _max = 6;
        private float _meleeMult = 1.5f;

        private class Raid
        {
            public VineNode Target;
            public List<Vector2I> Path;
            public int PathIndex;
            public float RepathTimer, MeleeTimer;
            public Label3D Marker;
            /// <summary>At the end of its path, beside the target.</summary>
            public bool Beside;
            /// <summary>Walking back down to the floor (its raid is over, or it gave up).</summary>
            public bool Leaving;
            /// <summary>The nearest it has been to where it's going, and how long since that got better.</summary>
            public float Best = float.MaxValue, Stalled;
        }

        /// <summary>A raider that gets no nearer for this long, without hitting anything, gives up.</summary>
        public const float GiveUpAfter = 6f;
        /// <summary>Raiders that gave up this run (tests, the bot's log).</summary>
        public int GaveUp { get; private set; }
        // Never drafted twice: it would walk back to the spot it gave up at
        private readonly HashSet<ulong> _gaveUp = new();

        private readonly Dictionary<VineEnemy, Raid> _raiders = new();
        private readonly RandomNumberGenerator _rng = new();
        // Raiders still to draft for each wave sent
        private readonly Dictionary<int, int> _quota = new();
        private bool _announced;

        /// <summary>Raiders out now (tests, HUD).</summary>
        public int Active => _raiders.Count;
        /// <summary>Raiders drafted this run (tests).</summary>
        public int Drafted { get; private set; }

        public override void _Ready()
        {
            _rng.Randomize();
            Load();
            ServiceLocator.Register(this);
            GameEvents.OnWaveStarted += OnWaveStarted;
            GameEvents.OnWaveCompleted += OnWaveCompleted;
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveStarted -= OnWaveStarted;
            GameEvents.OnWaveCompleted -= OnWaveCompleted;
            if (ServiceLocator.TryGet<RaidManager>(out var m) && m == this) ServiceLocator.Unregister<RaidManager>();
        }

        /// <summary>How many raiders a wave sends while there is something to raid.</summary>
        public int RaidersFor(int wave)
            => wave < _startWave ? 0 : Mathf.Min(_max, _base + (wave - _startWave) / Mathf.Max(1, _wavesPerExtra));

        private void OnWaveStarted(int wave)
        {
            int n = RaidersFor(wave);
            if (n > 0) _quota[wave] = n;
        }

        private void OnWaveCompleted(int wave)
        {
            _quota.Clear();
            _announced = false;
        }

        /// <summary>What raiders go for: Salvage Drills (the ones on resource nodes among them).</summary>
        public static List<VineNode> Targets(VineGrid grid)
        {
            var list = new List<VineNode>();
            if (grid == null) return list;
            list.AddRange(SalvageDrill.All());
            return list;
        }

        public override void _Process(double delta)
        {
            long __pt = FrameProfiler.Start();
            try
            {
                float dt = (float)delta;
                if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
                bool chaos = ServiceLocator.TryGet<CorruptionManager>(out var cm) && cm.IsCorruptionActive;
                if (chaos) { ReleaseAll(); return; }
                if (_quota.Count > 0) Draft(grid);
                if (_raiders.Count == 0) return;

                var done = new List<VineEnemy>();
                foreach (var (ve, raid) in _raiders)
                {
                    if (!IsInstanceValid(ve) || !ve.IsAlive) { done.Add(ve); continue; }
                    raid.RepathTimer -= dt;
                    if (raid.Leaving) { if (!StepDown(ve, raid, grid, dt)) done.Add(ve); continue; }
                    bool gone = raid.Target == null || !IsInstanceValid(raid.Target) || raid.Target.IsDestroyed;
                    if (gone || raid.RepathTimer <= 0f)
                    {
                        if (!Retarget(ve, raid, grid)) { if (!StartLeaving(ve, raid, grid)) done.Add(ve); continue; }
                    }
                    var at = raid.Target.GlobalPosition;
                    float dist = Flat(ve.GlobalPosition, at);
                    float reach = ve.AttackRange > 0f ? Mathf.Min(ve.AttackRange * 0.8f, 5f) : 1.9f;
                    // A raider with a gun stops to shoot only once it can see the drill: below a
                    // wall it can't (Sight), and it stood there for good with the wave held open.
                    // One without a gun hits it from the cell beside it, corner cells included: a
                    // big body can't get its centre within 1.9 of a drill it stands diagonal to.
                    bool beside = raid.Beside && Cheb(grid.WorldToGrid(ve.GlobalPosition), raid.Target.GridPosition) <= 1;
                    if ((dist <= reach || beside) && (ve.AttackRange <= 0f || Sight.Between(ve, raid.Target)))
                    {
                        ve.Face(at, dt);
                        ve.ChaosStep(ve.GlobalPosition, 1f, dt); // ticks slows and stuns
                        if (ve.AttackRange <= 0f) Melee(ve, raid, dt);
                        raid.Stalled = 0f;
                        continue;
                    }
                    if (!Advance(ve, raid, grid, dt)) raid.RepathTimer = 0f;
                    // Getting no nearer: it gives up and walks back down. A raider that couldn't
                    // reach its drill stood on the wall for good, out of every gun's sight, and
                    // the wave never ended (both campaign bots, Buffer Overflow, 2026-11)
                    if (dist < raid.Best - 0.2f) { raid.Best = dist; raid.Stalled = 0f; }
                    else if (!ve.IsStunned && !ve.IsFrozen && (raid.Stalled += dt) > GiveUpAfter)
                    {
                        GaveUp++;
                        _gaveUp.Add(ve.GetInstanceId());
                        GD.Print($"[Raid] {ve.EnemyName} gave up on {raid.Target.Data?.Name} at {raid.Target.GridPosition} from {grid.WorldToGrid(ve.GlobalPosition)}, {dist:0.0} away");
                        if (!StartLeaving(ve, raid, grid)) done.Add(ve);
                    }
                }
                foreach (var ve in done) Release(ve);
            }
            finally { FrameProfiler.Stop("raids", __pt); }
        }

        /// <summary>Pick raiders from enemies that have just come onto the grid.</summary>
        private void Draft(VineGrid grid)
        {
            var targets = Targets(grid);
            if (targets.Count == 0) return;
            foreach (var ve in Roster.Enemies(GetTree()))
            {
                if (_quota.Count == 0) break;
                if (!IsInstanceValid(ve) || !ve.IsAlive || ve.IsWandering || ve.IsRaider || !ve.OnGrid) continue;
                if (_gaveUp.Contains(ve.GetInstanceId())) continue;
                if (ve.IsBoss || ve.IsCommander || ve.IsAscendant || ve.IsFlying) continue;
                if (!_quota.TryGetValue(ve.Wave, out int left) || left <= 0) continue;
                if (left <= 1) _quota.Remove(ve.Wave); else _quota[ve.Wave] = left - 1;
                MakeRaider(ve, grid);
            }
        }

        /// <summary>Tests: send <paramref name="ve"/> raiding now. False when there's nothing to raid.</summary>
        internal bool TestDraft(VineEnemy ve)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid) || Targets(grid).Count == 0) return false;
            MakeRaider(ve, grid);
            return ve.IsRaider;
        }

        private void MakeRaider(VineEnemy ve, VineGrid grid)
        {
            var raid = new Raid();
            _raiders[ve] = raid;
            ve.IsRaider = true;
            ve.IsWandering = true;
            raid.Marker = new Label3D
            {
                Name = "RaidMarker",
                Text = "RAID",
                FontSize = 40,
                OutlineSize = 10,
                Modulate = new Color(1f, 0.78f, 0.25f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                Position = new Vector3(0, (ve.IsBoss ? 3.8f : 2.3f), 0),
                PixelSize = 0.006f,
            };
            ve.AddChild(raid.Marker);
            Drafted++;
            if (!Retarget(ve, raid, grid)) { Release(ve); return; }
            if (!_announced)
            {
                _announced = true;
                string what = raid.Target.Data?.Type == VineNodeType.SalvageDrill ? "your Salvage Drills" : "the towers holding your resource nodes";
                GameEvents.OnAnnouncement?.Invoke($"Raiders are going for {what}");
            }
        }

        /// <summary>
        /// The nearest drill it can get beside, and the way there: over the floor and up the wall,
        /// round whatever stands on it (the path used to end at the nearest floor cell, and from
        /// there the raider walked straight at the drill until a tower was in its way). A drill
        /// walled in by towers isn't safe: the raider goes for the nearest tower in its way
        /// instead. False: nothing it can raid.
        /// </summary>
        private bool Retarget(VineEnemy ve, Raid raid, VineGrid grid)
        {
            raid.RepathTimer = _rng.RandfRange(1.5f, 2.2f);
            raid.Beside = false;
            raid.Target = null;
            raid.Path = null;
            var drills = Targets(grid);
            if (drills.Count == 0) return false;
            drills.Sort((a, b) => Flat(ve.GlobalPosition, a.GlobalPosition).CompareTo(Flat(ve.GlobalPosition, b.GlobalPosition)));
            if (ve.Faction == VineEnemyFaction.Ghost) { raid.Target = drills[0]; return true; }
            var from = grid.WorldToGrid(ve.GlobalPosition);
            foreach (var drill in drills)
            {
                var path = PathBeside(grid, from, drill.GridPosition);
                if (path == null) continue;
                raid.Target = drill;
                raid.Path = path;
                raid.PathIndex = path.Count > 1 ? 1 : 0;
                return true;
            }
            // Every drill is walled in: the nearest standing tower by the nearest drill that it can reach
            var near = drills[0].GridPosition;
            VineNode guard = null;
            List<Vector2I> way = null;
            float bestCost = float.MaxValue;
            foreach (var n in Roster.Nodes(GetTree()))
            {
                if (!IsInstanceValid(n) || n.IsDestroyed || n.Data == null || Cheb(n.GridPosition, near) > 2 || n == drills[0]) continue;
                var path = PathBeside(grid, from, n.GridPosition);
                if (path == null) continue;
                float c = path.Count + Cheb(n.GridPosition, near) * 3f;
                if (c < bestCost) { bestCost = c; guard = n; way = path; }
            }
            if (guard == null) return false;
            raid.Target = guard;
            raid.Path = way;
            raid.PathIndex = way.Count > 1 ? 1 : 0;
            return true;
        }

        private static List<Vector2I> PathBeside(VineGrid grid, Vector2I from, Vector2I cell)
            => ClimbPath(grid, from, c => Cheb(c, cell) <= 1, c => c.X != cell.X && c.Y != cell.Y);

        private static int Cheb(Vector2I a, Vector2I b) => Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));

        /// <summary>One step along the raid's path. False when it couldn't move.</summary>
        private static bool Advance(VineEnemy ve, Raid raid, VineGrid grid, float dt)
        {
            if (ve.Faction == VineEnemyFaction.Ghost) return ve.ChaosStep(raid.Target.GlobalPosition, 1f, dt);
            if (raid.Path == null) return false;
            if (raid.PathIndex >= raid.Path.Count) { raid.Beside = !raid.Leaving; return true; }
            var step = grid.GridToWorld(raid.Path[raid.PathIndex]);
            if (Flat(ve.GlobalPosition, step) < 0.25f)
            {
                raid.PathIndex++;
                if (raid.PathIndex >= raid.Path.Count) { raid.Beside = !raid.Leaving; return true; }
                step = grid.GridToWorld(raid.Path[raid.PathIndex]);
            }
            if (ve.ChaosStep(step, 1f, dt)) return true;
            // Off its cell's middle, the line to the next cell can clip a corner: square up first
            return ve.ChaosStep(grid.GridToWorld(grid.WorldToGrid(ve.GlobalPosition)), 1f, dt);
        }

        /// <summary>
        /// Its raid is over: from up on a wall it walks back down to the floor first (released
        /// there it couldn't move, and was despawned as stuck four seconds later). False when it
        /// can be released where it stands.
        /// </summary>
        private static bool StartLeaving(VineEnemy ve, Raid raid, VineGrid grid)
        {
            var here = grid.WorldToGrid(ve.GlobalPosition);
            if (ve.Faction == VineEnemyFaction.Ghost || grid.IsWalkable(here)) return false;
            raid.Path = ClimbPath(grid, here, c => grid.IsWalkable(c), null);
            if (raid.Path == null) return false;
            raid.PathIndex = raid.Path.Count > 1 ? 1 : 0;
            raid.Leaving = true;
            raid.Beside = false;
            raid.Target = null;
            raid.Best = float.MaxValue;
            raid.Stalled = 0f;
            return true;
        }

        /// <summary>A leaving raider's step. False once it is on the floor (or can't get there).</summary>
        private static bool StepDown(VineEnemy ve, Raid raid, VineGrid grid, float dt)
        {
            if (grid.IsWalkable(grid.WorldToGrid(ve.GlobalPosition)) && raid.PathIndex >= raid.Path.Count - 1) return false;
            if (raid.PathIndex >= raid.Path.Count) return false;
            bool moved = Advance(ve, raid, grid, dt);
            float left = raid.Path.Count - raid.PathIndex + Flat(ve.GlobalPosition, grid.GridToWorld(raid.Path[Mathf.Min(raid.PathIndex, raid.Path.Count - 1)])) * 0.5f;
            if (moved && left < raid.Best - 0.1f) { raid.Best = left; raid.Stalled = 0f; }
            else if ((raid.Stalled += dt) > GiveUpAfter) return false;
            return true;
        }

        private static readonly Vector2I[] Steps = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        /// <summary>
        /// The cheapest way over cells a raider can cross (the floor, and wall faces and tops at
        /// four times the cost, so it climbs late) from <paramref name="from"/> to the first cell
        /// that is a <paramref name="goal"/> (<paramref name="worse"/> goals cost a little more:
        /// a corner cell beside the drill when a side cell will do). Null when there is none.
        /// </summary>
        internal static List<Vector2I> ClimbPath(VineGrid grid, Vector2I from, System.Func<Vector2I, bool> goal, System.Func<Vector2I, bool> worse)
        {
            bool Passable(Vector2I c) => grid.InBounds(c) && (grid.IsWalkable(c) || grid.IsClimbable(c));
            if (!grid.InBounds(from)) return null;
            var cost = new Dictionary<Vector2I, float> { [from] = 0f };
            var came = new Dictionary<Vector2I, Vector2I>();
            var open = new PriorityQueue<Vector2I, float>();
            open.Enqueue(from, 0f);
            Vector2I? end = null;
            float endCost = float.MaxValue;
            while (open.TryDequeue(out var cur, out float pri))
            {
                if (pri > endCost) break;
                if (pri > cost[cur] + 0.001f) continue;
                if (goal(cur) && (cur == from || Passable(cur)))
                {
                    float total = pri + (worse != null && worse(cur) ? 1.5f : 0f);
                    if (total < endCost) { endCost = total; end = cur; }
                    continue;
                }
                foreach (var d in Steps)
                {
                    var n = cur + d;
                    if (!Passable(n)) continue;
                    float c = cost[cur] + (grid.IsWalkable(n) ? 1f : 4f);
                    if (cost.TryGetValue(n, out float old) && old <= c) continue;
                    cost[n] = c;
                    came[n] = cur;
                    open.Enqueue(n, c);
                }
            }
            if (end == null) return null;
            var path = new List<Vector2I> { end.Value };
            while (came.TryGetValue(path[^1], out var prev)) path.Add(prev);
            path.Reverse();
            return path;
        }

        /// <summary>Tests and logs: where a raider is in its raid.</summary>
        internal string Describe(VineEnemy ve)
        {
            if (!_raiders.TryGetValue(ve, out var raid)) return "not raiding";
            if (raid.Leaving) return $"leaving, {raid.PathIndex}/{raid.Path?.Count ?? 0} of its way down";
            if (raid.Target == null || !IsInstanceValid(raid.Target)) return "no target";
            return $"after the drill at {raid.Target.GridPosition}, {Flat(ve.GlobalPosition, raid.Target.GlobalPosition):0.0} away, path {raid.PathIndex}/{raid.Path?.Count ?? 0}{(raid.Beside ? ", beside it" : "")}, stalled {raid.Stalled:0.0} s";
        }

        private void Melee(VineEnemy ve, Raid raid, float dt)
        {
            raid.MeleeTimer -= dt;
            if (raid.MeleeTimer > 0f) return;
            raid.MeleeTimer = 1.2f;
            float dmg = Mathf.Max(4f, ve.AttackDamage) * _meleeMult * ve.DamageMultiplier;
            if (ve.Faction == VineEnemyFaction.Brute) dmg *= 1.6f;
            raid.Target.TakeDamage(dmg, TowerLoss.Raid);
            ve.PlayAttack();
        }

        /// <summary>Back to the path to the Spire.</summary>
        private void Release(VineEnemy ve)
        {
            if (_raiders.TryGetValue(ve, out var raid) && raid.Marker != null && IsInstanceValid(raid.Marker)) raid.Marker.QueueFree();
            _raiders.Remove(ve);
            if (!IsInstanceValid(ve)) return;
            ve.IsRaider = false;
            if (!ve.IsAlive) return;
            ve.IsWandering = false;
            ve.RepathToSpire();
        }

        private void ReleaseAll()
        {
            if (_raiders.Count == 0) return;
            foreach (var ve in new List<VineEnemy>(_raiders.Keys))
            {
                if (_raiders.TryGetValue(ve, out var raid) && raid.Marker != null && IsInstanceValid(raid.Marker)) raid.Marker.QueueFree();
                if (IsInstanceValid(ve)) ve.IsRaider = false; // chaos steers it now
            }
            _raiders.Clear();
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

        private void Load()
        {
            const string path = "res://Data/difficulty_scaling.json";
            if (!Godot.FileAccess.FileExists(path)) return;
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) return;
            var d = parsed.AsGodotDictionary();
            if (!d.ContainsKey("raids") || d["raids"].VariantType != Variant.Type.Dictionary) return;
            var r = d["raids"].AsGodotDictionary();
            float F(string k, float v) => r.ContainsKey(k) ? (float)r[k].AsDouble() : v;
            _startWave = (int)F("start_wave", _startWave);
            _base = (int)F("raiders", _base);
            _wavesPerExtra = (int)F("waves_per_extra", _wavesPerExtra);
            _max = (int)F("max", _max);
            _meleeMult = F("melee_mult", _meleeMult);
        }
    }
}
