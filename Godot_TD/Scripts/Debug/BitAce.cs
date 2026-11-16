using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// "bitSkill": "ace" in a bot's config: BIT played by someone good at it. <see cref="BitPilot"/>
    /// puts BIT on one spot for the wave and casts from there; this one walks it, through the
    /// same input a player's keys and mouse give (<see cref="VinePlayer.BotMove"/>,
    /// <see cref="VinePlayer.BotAim"/>), so it runs, sprints and collides as a player's BIT does.
    ///
    /// What it does, four times a second:
    /// - picks who to shoot with aimed fire (the left button held: it reaches across the field
    ///   where auto-fire reaches a few cells): an Empowerer tethered to something, a raider, a
    ///   siege enemy bedded down, then whoever is closest to getting through;
    /// - picks where to stand: in sight and reach of that target, out of reach of everything
    ///   else, among the towers when Overclock or Repair Pulse is ready, a short walk away;
    /// - backs off to the Spire and climbs in when badly hurt, and comes out patched up;
    /// - between fights walks over the crates in the field, then back to its post.
    /// The abilities and strikes are <see cref="BitPilot"/>'s, cast from wherever BIT is.
    /// It logs "[BitAce] Wn: walked a m, b aimed shots ..." each wave.
    /// </summary>
    public static class BitAce
    {
        public enum Job { Post, Fight, Retreat, Mend, Loot }

        private const float ThinkEvery = 0.25f;
        /// <summary>Further than an enemy shoots (6) and well clear of its touch.</summary>
        private const float Safe = 7.5f;
        /// <summary>Cells out from BIT searched for a place to stand.</summary>
        private const int Search = 16;
        private const float RetreatAt = 0.35f, BackAt = 0.9f;

        private static float _think, _stuck, _repath;
        private static readonly List<Vector2I> _path = new();
        private static Vector2I? _goal;
        private static Vector3 _lastPos;
        private static bool _retreating, _wasDown, _hasLast;
        private static int _wave, _docks, _crates, _downs, _aimed0, _kills0, _carried0;
        private static float _walked;
        private static readonly float[] _time = new float[5];

        /// <summary>What BIT is doing, who it is shooting and where it is going (tests, the log).</summary>
        public static Job Doing { get; private set; }
        public static VineEnemy Target { get; private set; }
        public static Vector2I? Goal => _goal;
        /// <summary>Metres walked this run (tests).</summary>
        public static float WalkedTotal { get; private set; }

        public static void Reset()
        {
            _path.Clear(); _goal = null; Target = null; Doing = Job.Post;
            _retreating = _wasDown = _hasLast = false;
            _think = _stuck = _repath = _walked = 0f;
            _wave = _docks = _crates = _downs = _aimed0 = _kills0 = 0;
            _carried0 = LootCrate.WalkedOver;
            WalkedTotal = 0f;
            Array.Clear(_time);
        }

        /// <summary>The build phase before a wave: log the wave just fought.</summary>
        public static void NewWave(VineGrid grid, int wave)
        {
            if (ServiceLocator.TryGet<VinePlayer>(out var bit))
            {
                if (_wave > 0)
                {
                    var (shock, repair, overclock, strikes) = BitPilot.TakeCasts();
                    float all = Mathf.Max(0.01f, _time.Sum());
                    string jobs = string.Join(", ", Enum.GetValues<Job>().Where(j => _time[(int)j] / all >= 0.05f)
                        .Select(j => $"{j.ToString().ToLowerInvariant()} {_time[(int)j] / all * 100f:0}%"));
                    GD.Print($"[BitAce] W{_wave}: walked {_walked:0} m, {bit.AimedShots - _aimed0} aimed shots, {bit.EnemiesKilledPersonally - _kills0} kills; {jobs}; "
                        + $"docked {_docks}, crates {_crates}, down {_downs}; shock {shock}, repair {repair}, overclock {overclock}, strikes {strikes}");
                }
                _aimed0 = bit.AimedShots;
                _kills0 = bit.EnemiesKilledPersonally;
            }
            _wave = wave + 1;
            _walked = 0f; _docks = _crates = _downs = 0;
            Array.Clear(_time);
        }

        public static void Tick(VineGrid grid, float dt)
        {
            if (grid == null || !ServiceLocator.TryGet<VinePlayer>(out var bit) || !GodotObject.IsInstanceValid(bit)) return;
            BitPilot.StrikeTick(dt);
            if (bit.IsDown || !bit.IsAlive || bit.IsEmerging)
            {
                if (bit.IsDown && !_wasDown) { _downs++; _retreating = false; }
                _wasDown = bit.IsDown;
                Stop(bit);
                _hasLast = false;
                return;
            }
            _wasDown = false;
            _time[(int)Doing] += dt;
            if (_hasLast && !bit.IsDocked)
            {
                float step = Flat(bit.GlobalPosition, _lastPos);
                if (step < 3f) { _walked += step; WalkedTotal += step; } // (a respawn or stepping out of the Spire is not a walk)
            }
            _lastPos = bit.GlobalPosition; _hasLast = true;

            if ((_think -= dt) <= 0f)
            {
                _think = ThinkEvery;
                Think(grid, bit);
            }
            Steer(grid, bit, dt);
            if (Trace && (_trace -= dt) <= 0f)
            {
                _trace = 1f;
                var c = grid.WorldToGrid(bit.GlobalPosition);
                GD.Print($"[BitAce/trace] {Doing} at ({c.X},{c.Y}) goal {(_goal is { } g ? $"({g.X},{g.Y})" : "none")} way {_path.Count} hp {bit.CurrentHP:0}/{bit.MaxHP:0} "
                    + $"target {(Target != null && GodotObject.IsInstanceValid(Target) ? $"{Target.EnemyName} {Flat(Target.GlobalPosition, bit.GlobalPosition):0.0} m" : "none")} aimed {bit.AimedShots} moving {bit.Velocity.Length():0.0}");
            }
        }

        /// <summary>ACE_TRACE=1: a line a second of where BIT is, what it's doing and who it's shooting.</summary>
        private static readonly bool Trace = OS.GetEnvironment("ACE_TRACE") == "1";
        private static float _trace;

        private static void Stop(VinePlayer bit)
        {
            bit.BotMove = Vector3.Zero;
            bit.BotAim(null);
            _path.Clear();
            _goal = null;
        }

        // ── Deciding ──

        private static void Think(VineGrid grid, VinePlayer bit)
        {
            var tree = bit.GetTree();
            if (tree == null) return;
            var station = SpireStation.Current;
            var spire = grid.Harvester;
            var foes = new List<VineEnemy>();
            foreach (var e in Roster.Enemies(tree))
                if (e != null && GodotObject.IsInstanceValid(e) && e.IsAlive) foes.Add(e);
            float hp = bit.CurrentHP / Mathf.Max(1f, bit.MaxHP);

            // In the Spire: out when patched up (sooner when the field is empty)
            if (bit.IsDocked)
            {
                Doing = Job.Mend;
                bit.BotMove = Vector3.Zero;
                _path.Clear(); _goal = null;
                if (hp >= BackAt || foes.Count == 0 && hp >= 0.6f) { station?.Undock(); _retreating = false; }
                return;
            }

            BitPilot.Strike(grid);
            BitPilot.Cast(grid, bit);

            Target = PickTarget(bit, foes, spire);
            bit.BotAim(Target?.GlobalPosition);
            // Nobody in sight and reach from here: the one worth most anywhere is who to go and find
            // (with no target it had no reason to move, and stood among the towers shooting nothing)
            var wanted = Target ?? MostWorth(bit, foes, spire);

            // Badly hurt: to the Spire and in (it heals five times faster inside)
            if (hp < RetreatAt && foes.Count > 0) _retreating = true;
            else if (hp >= 0.75f || foes.Count == 0) _retreating = false;
            if (_retreating && station != null && spire != null && !spire.IsDestroyed)
            {
                Doing = Job.Retreat;
                if (station.InReach)
                {
                    bit.BotAim(null);
                    station.Dock();
                    if (bit.IsDocked) { _docks++; return; }
                }
                GoTo(grid, bit, NearSpire(grid, bit, spire, foes));
                return;
            }

            // (crates BIT walked over, not the ones a safe room pulls in)
            if (LootCrate.WalkedOver > _carried0) _crates += LootCrate.WalkedOver - _carried0;
            _carried0 = LootCrate.WalkedOver;
            var crate = NearestCrate(grid, bit, foes);
            if (crate != null)
            {
                Doing = Job.Loot;
                GoTo(grid, bit, grid.WorldToGrid(crate.GlobalPosition));
                return;
            }

            if (foes.Count == 0)
            {
                Doing = Job.Post;
                var post = BitPilot.PickSpot(grid);
                GoTo(grid, bit, post != null ? grid.WorldToGrid(post.Value) : grid.WorldToGrid(bit.GlobalPosition));
                return;
            }
            Doing = Job.Fight;
            var here = bit.GlobalPosition;
            float near = float.MaxValue;
            foreach (var e in foes) near = Mathf.Min(near, Flat(e.GlobalPosition, here));
            var door = spire != null ? spire.GlobalPosition : here;
            // Overclock is up and the fight has reached the towers: worth standing among them for
            bool boost = OverclockUp(bit) && foes.Any(e => Flat(e.GlobalPosition, door) < 26f);
            if (Brawler(bit) || near < Safe || boost)
                // Too close to something (or an ability to place): the best cell a short walk away
                GoTo(grid, bit, BestStand(grid, bit, foes, wanted, spire, boost));
            else if (Target != null)
                // Safe, with someone in the sights: stand and shoot
                Hold(bit);
            else if (wanted != null && near > Safe + 2.5f)
                // Nobody in sight: toward the one worth most, until it comes into view
                GoTo(grid, bit, grid.WorldToGrid(wanted.GlobalPosition));
            else
                Hold(bit);
        }

        private static void Hold(VinePlayer bit)
        {
            _path.Clear();
            _goal = null;
            bit.BotMove = Vector3.Zero;
        }

        private static bool OverclockUp(VinePlayer bit)
        {
            var a = bit.GetAbilities();
            return a != null && a.Length >= 3 && a[2].CurrentCooldown <= 1.5f && bit.CurrentMaterials >= a[2].MaterialsCost;
        }

        private static VineEnemy MostWorth(VinePlayer bit, List<VineEnemy> foes, VineHarvester spire)
        {
            var door = spire != null ? spire.GlobalPosition : bit.GlobalPosition;
            VineEnemy best = null;
            float bestScore = 0f;
            foreach (var e in foes)
            {
                float s = Worth(e, door, bit.EffectiveDamage);
                if (s > bestScore) { bestScore = s; best = e; }
            }
            return best;
        }

        /// <summary>
        /// Who a good player holds the mouse on: what's worth most among those BIT can see and
        /// reach. An Empowerer's tether makes its target shrug off 60% of every hit, a raider is
        /// eating a drill, a bedded siege enemy is shelling the towers; after those, the enemy
        /// closest to getting through.
        /// </summary>
        public static VineEnemy PickTarget(VinePlayer bit, List<VineEnemy> foes, VineHarvester spire)
        {
            if (foes.Count == 0) return null;
            bool sight = Sight.Matters;
            var chest = bit.ChestPoint;
            float reach = AimReach(bit) * 0.97f;
            var door = spire != null ? spire.GlobalPosition : bit.GlobalPosition;
            VineEnemy best = null;
            float bestScore = 0f;
            foreach (var e in foes)
            {
                float d = Flat(e.GlobalPosition, bit.GlobalPosition);
                if (d > reach) continue;
                if (sight && !Sight.Clear(chest, e.BodyCentre)) continue;
                float s = Worth(e, door, bit.EffectiveDamage);
                // (a little for the one already being shot: no flicking between equals)
                if (e == Target) s *= 1.15f;
                if (s > bestScore) { bestScore = s; best = e; }
            }
            return best;
        }

        /// <summary>What an enemy is worth shooting, wherever it is.</summary>
        public static float Worth(VineEnemy e, Vector3 door, float shot)
        {
            float w = 1f;
            if (e.IsEmpowerer && e.IsTethering) w = 6f;
            else if (e.IsRaider) w = 5f;
            else if (e.IsSieging) w = 5f;
            else if (e.IsSiege) w = 3f;
            else if (e.IsCommander) w = 2.5f;
            else if (e.IsBoss) w = 2f;
            // The one about to get through
            w += 3f / (1f + Flat(e.GlobalPosition, door) / 8f);
            // One or two shots from dead: finish it
            if (e.CurrentHealth <= shot * 2f) w *= 1.3f;
            // Tethered: most of the shot is wasted until its Empowerer is dead
            if (e.Empowered) w *= 0.5f;
            return w;
        }

        /// <summary>How far BIT's held fire reaches with what it carries.</summary>
        public static float AimReach(VinePlayer bit) => bit.RelicWeapon switch
        {
            StormLance lance => lance.AimReach,
            CryoFists fists => fists.LungeRange,
            null => bit.AimedRange,
            var w => w.Reach,
        };

        /// <summary>True when what BIT carries fights up close (Cryo Fists): it closes in instead of keeping away.</summary>
        private static bool Brawler(VinePlayer bit) => bit.RelicWeapon is CryoFists;

        // ── Where to stand ──

        /// <summary>
        /// The cell to fight from: every cell a short walk away is scored for sight and reach of
        /// the target, distance from everything that can hurt, towers in reach of an ability
        /// that's ready, and the walk.
        /// </summary>
        private static Vector2I BestStand(VineGrid grid, VinePlayer bit, List<VineEnemy> foes, VineEnemy target, VineHarvester spire, bool overclock)
        {
            var from = grid.WorldToGrid(bit.GlobalPosition);
            var steps = Flood(grid, from, Search);
            if (steps.Count == 0) return from;
            var tree = bit.GetTree();
            bool sight = Sight.Matters, brawl = Brawler(bit);
            float reach = AimReach(bit) * 0.9f;
            float up = bit.ChestPoint.Y - bit.GlobalPosition.Y;
            var door = spire != null ? spire.GlobalPosition : bit.GlobalPosition;

            // Overclock to place: the towers it would reach from each cell
            var abilities = bit.GetAbilities();
            float boostReach = abilities != null && abilities.Length >= 3 ? abilities[2].Range * PowerType.OverclockReachMult : 0f;
            var towers = new List<Vector3>();
            if (overclock)
                foreach (var n in Roster.Nodes(tree))
                    if (n != null && !n.IsDestroyed && n.Data != null && BitPilot.Boostable(n)) towers.Add(n.GlobalPosition);

            var foePos = foes.Select(e => e.GlobalPosition).ToList();
            Vector2I best = from;
            float bestScore = float.NegativeInfinity;
            foreach (var kv in steps)
            {
                var c = kv.Key;
                // Every other cell is enough to choose between (and the one BIT is on, and the goal it has)
                if (((c.X + c.Y) & 1) == 1 && c != from && c != _goal) continue;
                var w = grid.GridToWorld(c);
                var p = new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z), w.Z);
                float near = float.MaxValue;
                foreach (var fp in foePos) near = Mathf.Min(near, Flat(fp, p));

                float s = -kv.Value * 0.12f - Flat(p, door) * 0.02f;
                if (brawl)
                {
                    // Up close with the target, but not in the middle of the rest
                    if (target != null) s -= Mathf.Abs(Flat(target.GlobalPosition, p) - 2.5f) * 0.8f;
                }
                else
                {
                    if (near < Safe) s -= (Safe - near) * 2.2f;
                    if (target != null)
                    {
                        float d = Flat(target.GlobalPosition, p);
                        bool sees = d <= reach && (!sight || Sight.Clear(p + Vector3.Up * up, target.BodyCentre));
                        s += sees ? 6f : -Mathf.Min(6f, Mathf.Max(0f, d - reach) * 0.3f + (d <= reach ? 3f : 0f));
                    }
                }
                if (overclock)
                {
                    int n = 0;
                    foreach (var t in towers) if (Flat(t, p) <= boostReach) n++;
                    s += Mathf.Min(8, n) * 0.6f;
                }
                // (stay on a goal that's still about as good: no dithering between neighbours)
                if (c == _goal) s += 1.5f;
                if (s > bestScore) { bestScore = s; best = c; }
            }
            return best;
        }

        /// <summary>A cell beside the Spire to climb in from, on the side away from the enemies.</summary>
        private static Vector2I NearSpire(VineGrid grid, VinePlayer bit, VineHarvester spire, List<VineEnemy> foes)
        {
            var from = grid.WorldToGrid(bit.GlobalPosition);
            var steps = Flood(grid, from, 80);
            float reach = (SpireStation.Current?.Data.Reach ?? 4.5f) + Constants.VINE_CELL_SIZE * 0.4f;
            Vector2I best = from;
            float bestScore = float.NegativeInfinity;
            foreach (var kv in steps)
            {
                var w = grid.GridToWorld(kv.Key);
                float d = Flat(w, spire.GlobalPosition);
                if (d > reach) continue;
                float near = float.MaxValue;
                foreach (var e in foes) near = Mathf.Min(near, Flat(e.GlobalPosition, w));
                float s = -kv.Value * 0.3f + Mathf.Min(near, Safe);
                if (s > bestScore) { bestScore = s; best = kv.Key; }
            }
            // No way to it from here: the closest BIT can get
            if (float.IsNegativeInfinity(bestScore))
                best = steps.Keys.OrderBy(c => Flat(grid.GridToWorld(c), spire.GlobalPosition)).FirstOrDefault(from);
            return best;
        }

        /// <summary>The nearest crate worth walking to now: any when the field is empty, a close and quiet one mid-fight.</summary>
        private static LootCrate NearestCrate(VineGrid grid, VinePlayer bit, List<VineEnemy> foes)
        {
            var tree = bit.GetTree();
            LootCrate best = null;
            float bestD = foes.Count == 0 ? float.MaxValue : 14f;
            foreach (var node in tree.GetNodesInGroup(LootCrate.GROUP))
            {
                if (node is not LootCrate crate || crate.Taken || !GodotObject.IsInstanceValid(crate)) continue;
                var c = grid.WorldToGrid(crate.GlobalPosition);
                if (!Walkable(grid, c)) continue;
                float d = Flat(crate.GlobalPosition, bit.GlobalPosition);
                if (d >= bestD) continue;
                if (foes.Count > 0 && foes.Any(e => Flat(e.GlobalPosition, crate.GlobalPosition) < Safe)) continue;
                bestD = d; best = crate;
            }
            return best;
        }

        // ── Walking ──

        /// <summary>Cells BIT can stand in (towers don't stop it; rock, props, pits and cliffs do).</summary>
        public static bool Walkable(VineGrid grid, Vector2I c) => grid.InBounds(c) && !VinePlayer.BlocksBit(grid.GetCell(c));

        private static readonly Vector2I[] Around =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
        };

        /// <summary>Every cell within <paramref name="max"/> steps of a cell, with its steps (diagonals never cut a corner).</summary>
        public static Dictionary<Vector2I, int> Flood(VineGrid grid, Vector2I from, int max, Dictionary<Vector2I, Vector2I> came = null)
        {
            var steps = new Dictionary<Vector2I, int>();
            if (!Walkable(grid, from))
            {
                // Pushed onto a blocked cell's edge: start from the nearest open one
                var open = Around.Select(d => from + d).FirstOrDefault(c => Walkable(grid, c), from);
                if (!Walkable(grid, open)) return steps;
                from = open;
            }
            var queue = new Queue<Vector2I>();
            steps[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int n = steps[c];
                if (n >= max) continue;
                foreach (var d in Around)
                {
                    var to = c + d;
                    if (steps.ContainsKey(to) || !Walkable(grid, to)) continue;
                    if (d.X != 0 && d.Y != 0 && (!Walkable(grid, new Vector2I(c.X + d.X, c.Y)) || !Walkable(grid, new Vector2I(c.X, c.Y + d.Y)))) continue;
                    steps[to] = n + 1;
                    came?.TryAdd(to, c);
                    queue.Enqueue(to);
                }
            }
            return steps;
        }

        /// <summary>The way from a cell to another (empty: there is none, or it is here).</summary>
        public static List<Vector2I> PathTo(VineGrid grid, Vector2I from, Vector2I to)
        {
            var came = new Dictionary<Vector2I, Vector2I>();
            var steps = Flood(grid, from, 400, came);
            var path = new List<Vector2I>();
            if (!steps.ContainsKey(to))
            {
                // Nowhere BIT can stand: the reachable cell nearest it
                if (steps.Count == 0) return path;
                to = steps.Keys.OrderBy(c => (c - to).LengthSquared()).First();
            }
            for (var c = to; steps[c] > 0; c = came[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        private static void GoTo(VineGrid grid, VinePlayer bit, Vector2I goal)
        {
            var at = grid.WorldToGrid(bit.GlobalPosition);
            if (_goal == goal && _path.Count > 0 && _repath > 0f) return;
            _goal = goal;
            _repath = 1.5f;
            _path.Clear();
            if (goal != at) _path.AddRange(PathTo(grid, at, goal));
        }

        /// <summary>Every frame: hold the stick toward the furthest cell of the way BIT can walk straight at.</summary>
        private static void Steer(VineGrid grid, VinePlayer bit, float dt)
        {
            _repath -= dt;
            if (bit.IsDocked || _path.Count == 0) { bit.BotMove = Vector3.Zero; _stuck = 0f; return; }
            var pos = bit.GlobalPosition;
            // Past the cells already reached
            while (_path.Count > 0 && Flat(grid.GridToWorld(_path[0]), pos) < Constants.VINE_CELL_SIZE * 0.3f) _path.RemoveAt(0);
            if (_path.Count == 0) { bit.BotMove = Vector3.Zero; return; }
            int ahead = 0;
            for (int i = Mathf.Min(_path.Count - 1, 6); i > 0; i--)
                if (Straight(grid, pos, grid.GridToWorld(_path[i]), bit.BodyRadius)) { ahead = i; break; }
            if (ahead > 0) _path.RemoveRange(0, ahead);
            var to = grid.GridToWorld(_path[0]);
            var dir = new Vector3(to.X - pos.X, 0f, to.Z - pos.Z);
            bit.BotMove = dir.LengthSquared() > 0.0004f ? dir.Normalized() : Vector3.Zero;

            // Held against something (a crowd, a corner): look for another way
            if (bit.Velocity.Length() < 0.3f) _stuck += dt; else _stuck = 0f;
            if (_stuck > 0.8f) { _stuck = 0f; _repath = 0f; _path.Clear(); _goal = null; }
        }

        /// <summary>True if BIT's body fits along the straight line between two points.</summary>
        private static bool Straight(VineGrid grid, Vector3 a, Vector3 b, float radius)
        {
            float len = Flat(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.4f));
            var side = new Vector3(-(b.Z - a.Z), 0f, b.X - a.X);
            side = side.LengthSquared() > 1e-6f ? side.Normalized() * (radius + 0.05f) : Vector3.Zero;
            for (int i = 0; i <= n; i++)
            {
                var p = a.Lerp(b, i / (float)n);
                if (!Walkable(grid, grid.WorldToGrid(p)) || !Walkable(grid, grid.WorldToGrid(p + side)) || !Walkable(grid, grid.WorldToGrid(p - side))) return false;
            }
            return true;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
