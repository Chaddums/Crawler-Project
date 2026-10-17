using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Sightlines, visual clarity and enemy paths on the canyon maps (playtest 2026-10: the walls
    /// were too tall and made the firing look wrong, and enemies and towers saw each other through
    /// them). Every canyon map is built headless and measured: how much of the floor in reach a
    /// tower on the lip sees and one set back from it, how many places to build see each floor
    /// cell, that two floors with a wall between them never see each other, how much of the floor
    /// and the walkers on it the play camera sees (from four sides, at the play zoom and further
    /// out), and that every route is two cells wide and reaches the Spire. Then in a battle: a
    /// tower doesn't shoot what's behind rock and does once it's in sight, an armed enemy doesn't
    /// shoot a tower it can't see and does once it can, BIT's auto-fire skips an enemy behind a
    /// wall and its aimed shot stops at the rock, and through a wave no tower's shot runs along the
    /// ground (they aim at bodies they can see) and no walker stalls.
    /// Writes test-reports/sightlines/maps.json. CANYON_WALL=h builds every map at height h.
    /// </summary>
    public class SightlinesTestSuite : ITestSuite
    {
        public string SuiteName => "sightlines";

        /// <summary>A typical gun's reach, for the map measures.</summary>
        private const float Reach = 12f;

        private sealed class MapStats
        {
            public string Layout;
            public float Wall, LipSees, BackSees, Covered, CamBody15, CamFeet15, CamBody25, CamBody40;
            public int Floor, Lips, Backs, WallPairs, WallPairsSeen, Narrow, Unreached;
        }

        public async Task Run(TestContext ctx)
        {
            var stats = new List<MapStats>();
            foreach (var layout in CanyonLayouts())
            {
                var s = MeasureMap(ctx, layout);
                if (s != null) stats.Add(s);
            }
            Report(ctx, stats);
            await Battle(ctx);
        }

        internal static List<string> CanyonLayouts()
        {
            var layouts = new SortedSet<string>();
            foreach (int planet in new[] { 1, 2 })
                foreach (var region in TerritoryManager.GetPlanet(planet)?.Regions ?? new List<TerritoryRegion>())
                    foreach (var site in region.Sites)
                        if (!string.IsNullOrEmpty(site.MapLayout) && CanyonMaps.Exists(site.MapLayout)) layouts.Add(site.MapLayout);
            return layouts.ToList();
        }

        // ── Each map, built headless ──

        private static MapStats MeasureMap(TestContext ctx, string layout)
        {
            string prefix = $"sightlines/{layout}";
            var grid = new VineGrid { Width = Constants.VINE_MAP_WIDTH, Height = Constants.VINE_MAP_HEIGHT };
            ctx.Tree.CurrentScene.AddChild(grid);
            try
            {
                VineMapLayouts.BuildMap(grid, layout);
                if (!grid.IsCanyon) return null;
                var s = new MapStats { Layout = layout, Wall = grid.CanyonWallHeight };
                int w = grid.Width, h = grid.Height;
                float cs = Constants.VINE_CELL_SIZE;

                var floor = new List<Vector2I>();
                var isFloor = new bool[w, h];
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        if (grid.IsWalkable(x, y)) { isFloor[x, y] = true; floor.Add(new Vector2I(x, y)); }
                s.Floor = floor.Count;

                // How far each cell is from the floor (8 ways): the face is 1, the lip 2
                var dist = new int[w, h];
                var q = new Queue<Vector2I>();
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        dist[x, y] = isFloor[x, y] ? 0 : int.MaxValue;
                        if (isFloor[x, y]) q.Enqueue(new Vector2I(x, y));
                    }
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var n = c + new Vector2I(dx, dy);
                            if (!grid.InBounds(n) || dist[n.X, n.Y] <= dist[c.X, c.Y] + 1) continue;
                            dist[n.X, n.Y] = dist[c.X, c.Y] + 1;
                            q.Enqueue(n);
                        }
                }

                // What each place to build near the floor sees of the floor in reach
                var ledges = new List<Vector2I>();
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        if (grid.GetCell(x, y) == VineCellType.Ledge && grid.CanPlace(x, y) && dist[x, y] <= 5) ledges.Add(new Vector2I(x, y));
                var watchers = new Dictionary<Vector2I, int>();
                float lipSum = 0f, backSum = 0f;
                foreach (var l in ledges)
                {
                    int inReach = 0, seen = 0;
                    var eye = Sight.TowerEyeAt(grid, l);
                    foreach (var f in floor)
                    {
                        if (((Vector2)(f - l)).Length() * cs > Reach) continue;
                        inReach++;
                        if (!grid.HasLineOfSight(eye, Sight.WalkerAt(grid, f))) continue;
                        seen++;
                        watchers[f] = watchers.GetValueOrDefault(f) + 1;
                    }
                    if (inReach < 4) continue;
                    float share = (float)seen / inReach;
                    if (dist[l.X, l.Y] == 2) { lipSum += share; s.Lips++; }
                    else if (dist[l.X, l.Y] == 3) { backSum += share; s.Backs++; }
                }
                s.LipSees = s.Lips > 0 ? lipSum / s.Lips : 0f;
                s.BackSees = s.Backs > 0 ? backSum / s.Backs : 0f;
                s.Covered = floor.Count > 0 ? (float)floor.Count(f => watchers.GetValueOrDefault(f) >= 3) / floor.Count : 0f;

                // Two floors with a wall between them: close as the crow flies, far apart on foot
                var steps = FloorSteps(grid, isFloor, floor);
                for (int i = 0; i < floor.Count; i += 2)
                    for (int j = i + 1; j < floor.Count; j++)
                    {
                        var a = floor[i]; var b = floor[j];
                        float flat = ((Vector2)(b - a)).Length();
                        if (flat < 2f || flat > 7f) continue;
                        if (!steps.TryGetValue(a, out var fromA) || fromA[b.X, b.Y] < flat * 1.5f + 3) continue;
                        if (!CrossesLedge(grid, a, b)) continue;
                        s.WallPairs++;
                        if (grid.HasLineOfSight(Sight.WalkerAt(grid, a), Sight.WalkerAt(grid, b))) s.WallPairsSeen++;
                    }

                // The play camera: centred on the Spire and along the floor, from four sides
                s.CamBody15 = CameraSees(grid, floor, 15f, Sight.WALKER_UP);
                s.CamFeet15 = CameraSees(grid, floor, 15f, 0.12f);
                s.CamBody25 = CameraSees(grid, floor, 25f, Sight.WALKER_UP);
                s.CamBody40 = CameraSees(grid, floor, 40f, Sight.WALKER_UP);

                // Paths: two cells wide everywhere, and every floor cell reaches the Spire
                foreach (var f in floor)
                {
                    bool wide = false;
                    for (int dx = -1; dx <= 0 && !wide; dx++)
                        for (int dy = -1; dy <= 0 && !wide; dy++)
                        {
                            var o = f + new Vector2I(dx, dy);
                            wide = Fl(isFloor, o) && Fl(isFloor, o + new Vector2I(1, 0)) && Fl(isFloor, o + new Vector2I(0, 1)) && Fl(isFloor, o + new Vector2I(1, 1));
                        }
                    if (!wide) s.Narrow++;
                }
                var toSpire = Bfs(grid, isFloor, grid.ExitPoint);
                s.Unreached = floor.Count(f => toSpire[f.X, f.Y] == int.MaxValue);

                ctx.StartTest();
                ctx.Assert(s.WallPairs >= 3 && s.WallPairsSeen == 0, $"{prefix}/no_sight_through_walls",
                    $"{s.WallPairsSeen} of {s.WallPairs} floor pairs with a wall between them see each other");
                ctx.Assert(s.LipSees >= 0.55f, $"{prefix}/lip_sees_the_floor", $"a tower on the lip sees {s.LipSees:P0} of the floor in reach ({s.Lips} lips)");
                ctx.Assert(s.Covered >= 0.95f, $"{prefix}/floor_is_coverable", $"{s.Covered:P0} of the floor is seen from 3 or more places to build");
                ctx.Assert(s.CamBody15 >= 0.9f && s.CamBody25 >= 0.95f, $"{prefix}/camera_sees_walkers",
                    $"walkers in view: {s.CamBody15:P0} at the play zoom, {s.CamBody25:P0} at 25, {s.CamBody40:P0} at 40 (feet {s.CamFeet15:P0})");
                ctx.Assert(s.Narrow == 0 && s.Unreached == 0, $"{prefix}/paths_wide_and_reach_spire",
                    $"{s.Narrow} floor cells narrower than two, {s.Unreached} that don't reach the Spire");
                return s;
            }
            catch (Exception e)
            {
                ctx.Assert(false, $"{prefix}/builds", e.Message);
                return null;
            }
            finally
            {
                if (GodotObject.IsInstanceValid(grid)) { ctx.Tree.CurrentScene.RemoveChild(grid); grid.QueueFree(); }
            }
        }

        private static bool Fl(bool[,] f, Vector2I c) => c.X >= 0 && c.Y >= 0 && c.X < f.GetLength(0) && c.Y < f.GetLength(1) && f[c.X, c.Y];

        /// <summary>The straight line from a to b (in plan) passes over a wall top.</summary>
        private static bool CrossesLedge(VineGrid grid, Vector2I a, Vector2I b)
        {
            var pa = new Vector2(a.X + 0.5f, a.Y + 0.5f);
            var pb = new Vector2(b.X + 0.5f, b.Y + 0.5f);
            int n = Mathf.CeilToInt((pb - pa).Length() * 4f);
            for (int k = 1; k < n; k++)
            {
                var p = pa.Lerp(pb, (float)k / n);
                var c = new Vector2I(Mathf.FloorToInt(p.X), Mathf.FloorToInt(p.Y));
                if (grid.InBounds(c) && grid.GetCell(c) == VineCellType.Ledge) return true;
            }
            return false;
        }

        private static int[,] Bfs(VineGrid grid, bool[,] isFloor, Vector2I from)
        {
            int w = grid.Width, h = grid.Height;
            var d = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) d[x, y] = int.MaxValue;
            if (!grid.InBounds(from)) return d;
            var q = new Queue<Vector2I>();
            d[from.X, from.Y] = 0;
            q.Enqueue(from);
            var dirs = new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var dd in dirs)
                {
                    var n = c + dd;
                    if (!Fl(isFloor, n) || d[n.X, n.Y] != int.MaxValue) continue;
                    d[n.X, n.Y] = d[c.X, c.Y] + 1;
                    q.Enqueue(n);
                }
            }
            return d;
        }

        private static Dictionary<Vector2I, int[,]> FloorSteps(VineGrid grid, bool[,] isFloor, List<Vector2I> floor)
        {
            var all = new Dictionary<Vector2I, int[,]>();
            for (int i = 0; i < floor.Count; i += 2) all[floor[i]] = Bfs(grid, isFloor, floor[i]);
            return all;
        }

        /// <summary>
        /// The share of floor cells in view whose walker (at <paramref name="up"/>) the camera sees,
        /// with the camera at <paramref name="zoom"/> (TDCamera: 55 degrees down, 75 degree view)
        /// centred on the Spire and on every fifth floor cell, from four sides.
        /// </summary>
        private static float CameraSees(VineGrid grid, List<Vector2I> floor, float zoom, float up)
        {
            float ang = Mathf.DegToRad(Constants.CAMERA_ANGLE);
            float height = zoom * Mathf.Sin(ang), off = zoom * Mathf.Cos(ang);
            float tanV = Mathf.Tan(Mathf.DegToRad(37.5f)), tanH = tanV * 16f / 9f;
            var centres = new List<Vector2I> { grid.ExitPoint };
            for (int i = 0; i < floor.Count; i += 5) centres.Add(floor[i]);
            int inView = 0, seen = 0;
            foreach (var c in centres)
            {
                var target = grid.GridToWorld(c);
                target.Y = grid.GetWorldHeight(target.X, target.Z);
                foreach (float yawDeg in new[] { 0f, 90f, 180f, 270f })
                {
                    float yaw = Mathf.DegToRad(yawDeg);
                    var cam = target + new Vector3(Mathf.Sin(yaw) * off, height, Mathf.Cos(yaw) * off);
                    var fwd = (target - cam).Normalized();
                    var right = fwd.Cross(Vector3.Up).Normalized();
                    var upv = right.Cross(fwd);
                    foreach (var f in floor)
                    {
                        var p = grid.GridToWorld(f);
                        p.Y = grid.GetWorldHeight(p.X, p.Z) + up;
                        var d = p - cam;
                        float z = d.Dot(fwd);
                        if (z <= 0.5f || Mathf.Abs(d.Dot(upv)) > z * tanV || Mathf.Abs(d.Dot(right)) > z * tanH) continue;
                        inView++;
                        if (grid.HasLineOfSight(cam, p, 0f)) seen++;
                    }
                }
            }
            return inView > 0 ? (float)seen / inView : 1f;
        }

        private static void Report(TestContext ctx, List<MapStats> stats)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"maps\": [\n");
            for (int i = 0; i < stats.Count; i++)
            {
                var s = stats[i];
                sb.Append(System.FormattableString.Invariant(
                    $"    {{\"layout\": \"{s.Layout}\", \"wall\": {s.Wall:0.00}, \"floor\": {s.Floor}, \"lipSees\": {s.LipSees:0.000}, \"backSees\": {s.BackSees:0.000}, \"lips\": {s.Lips}, \"backs\": {s.Backs}, \"covered\": {s.Covered:0.000}, \"wallPairs\": {s.WallPairs}, \"wallPairsSeen\": {s.WallPairsSeen}, \"camBody15\": {s.CamBody15:0.000}, \"camFeet15\": {s.CamFeet15:0.000}, \"camBody25\": {s.CamBody25:0.000}, \"camBody40\": {s.CamBody40:0.000}, \"narrow\": {s.Narrow}, \"unreached\": {s.Unreached}}}"));
                sb.Append(i < stats.Count - 1 ? ",\n" : "\n");
                GD.Print(System.FormattableString.Invariant(
                    $"[Sightlines] {s.Layout,-14} wall {s.Wall:0.0}  lip sees {s.LipSees:P0}, one back {s.BackSees:P0}, floor covered {s.Covered:P0}, camera walkers {s.CamBody15:P0}/{s.CamBody25:P0}/{s.CamBody40:P0} feet {s.CamFeet15:P0}, wall pairs {s.WallPairsSeen}/{s.WallPairs}, narrow {s.Narrow}"));
            }
            sb.Append("  ]\n}\n");
            try
            {
                string dir = ProjectSettings.GlobalizePath("res://test-reports/sightlines");
                System.IO.Directory.CreateDirectory(dir);
                string tag = System.Environment.GetEnvironmentVariable("CANYON_WALL");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, string.IsNullOrEmpty(tag) ? "maps.json" : $"maps_{tag}.json"), sb.ToString());
            }
            catch (Exception e) { GD.PushWarning($"[Sightlines] report: {e.Message}"); }
        }

        // ── In a battle ──

        private Node _scene;
        private VineGrid _grid;
        private readonly List<VineEnemy> _dummies = new();

        private async Task Battle(TestContext ctx)
        {
            if (System.Environment.GetEnvironmentVariable("SIGHT_MAPS_ONLY") == "1") return;
            var gm = GameManager.Instance;
            ctx.StartTest();
            gm.CurrentPlanet = 1;
            gm.CurrentTerritorySectionId = "p1_ng_relay";
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(inBuild, "sightlines/battle_loads");
            if (!inBuild) return;
            _scene = ctx.Tree.CurrentScene;
            _grid = ServiceLocator.Get<VineGrid>();
            if (!_grid.IsCanyon) { ctx.Assert(false, "sightlines/battle_is_a_canyon"); return; }
            var waves = ServiceLocator.Get<VineWaveManager>();
            waves.PauseAutoStart = true;
            gm.AddResources(5000);
            gm.SetCoreLives(999);
            _grid.Harvester?.IncreaseMaxHP(1e6f);
            var bit = ServiceLocator.Get<VinePlayer>();
            var spire = _grid.Harvester;
            if (bit != null) bit.ProcessMode = Node.ProcessModeEnum.Disabled;
            if (spire != null) spire.ProcessMode = Node.ProcessModeEnum.Disabled;
            Engine.TimeScale = 3.0;
            try
            {
                await TowerSight(ctx);
                await EnemySight(ctx);
            }
            finally
            {
                Engine.TimeScale = 1.0;
                ClearDummies();
                if (bit != null && GodotObject.IsInstanceValid(bit)) bit.ProcessMode = Node.ProcessModeEnum.Inherit;
                if (spire != null && GodotObject.IsInstanceValid(spire)) spire.ProcessMode = Node.ProcessModeEnum.Inherit;
            }
            await BitSight(ctx, bit);
            await WaveLooks(ctx, waves);
        }

        private VineEnemy Dummy(Vector3 at, float hp = 1e5f, float attackRange = 0f, float attackDamage = 0f)
        {
            var e = new VineEnemy();
            _scene.AddChild(e);
            e.Initialize("Sight target", VineEnemyFaction.Scavenger, hp, 0f, 0, new Color(0.8f, 0.4f, 0.3f), _grid.WorldToGrid(at),
                false, attackRange, attackDamage, attackRange > 0f ? 0.5f : 0f);
            e.GlobalPosition = new Vector3(at.X, _grid.GetWorldHeight(at.X, at.Z), at.Z);
            _dummies.Add(e);
            return e;
        }

        private void ClearDummies()
        {
            foreach (var e in _dummies) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            _dummies.Clear();
        }

        private IEnumerable<Vector2I> FloorCells()
        {
            for (int x = 0; x < _grid.Width; x++)
                for (int y = 0; y < _grid.Height; y++)
                {
                    var c = new Vector2I(x, y);
                    if (_grid.IsWalkable(c) && _grid.GetCell(c) == VineCellType.Empty && (c - _grid.ExitPoint).Length() > 4) yield return c;
                }
        }

        private IEnumerable<Vector2I> Lips()
        {
            for (int x = 1; x < _grid.Width - 1; x++)
                for (int y = 1; y < _grid.Height - 1; y++)
                {
                    var c = new Vector2I(x, y);
                    if (_grid.GetCell(c) != VineCellType.Ledge || !_grid.CanPlace(c) || _grid.GetNode(c) != null || _grid.IsResourceNode(c)) continue;
                    bool lip = false;
                    for (int dx = -1; dx <= 1 && !lip; dx++)
                        for (int dy = -1; dy <= 1 && !lip; dy++)
                            lip = _grid.GetCell(c + new Vector2I(dx, dy)) == VineCellType.Cliff;
                    if (lip) yield return c;
                }
        }

        private float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

        /// <summary>A lip, a floor cell it sees in reach and one in reach it can't see (behind a wall if it can).</summary>
        private (Vector2I lip, Vector2I seen, Vector2I hidden)? FindSightPair(float reach)
        {
            var floor = FloorCells().ToList();
            (Vector2I, Vector2I, Vector2I)? fallback = null;
            foreach (var l in Lips())
            {
                var eye = Sight.TowerEyeAt(_grid, l);
                Vector2I? seen = null, hidden = null, under = null;
                foreach (var f in floor)
                {
                    float d = Flat(_grid.GridToWorld(l), _grid.GridToWorld(f));
                    if (d < 3f || d > reach) continue;
                    bool sees = _grid.HasLineOfSight(eye, Sight.WalkerAt(_grid, f));
                    if (sees) seen ??= f;
                    else if (CrossesLedge(_grid, l, f) && d > 5f) hidden ??= f;
                    else under ??= f;
                }
                if (seen.HasValue && hidden.HasValue) return (l, seen.Value, hidden.Value);
                if (seen.HasValue && under.HasValue) fallback ??= (l, seen.Value, under.Value);
            }
            return fallback;
        }

        /// <summary>A Junk Turret on the lip holds fire at a target behind rock, and shoots it once it's in sight.</summary>
        private async Task TowerSight(TestContext ctx)
        {
            ctx.StartTest();
            var range = VineNodeRegistry.Get(VineNodeType.DamageTower)?.Range ?? 12f;
            var pair = FindSightPair(range - 1.5f);
            if (pair == null) { ctx.Assert(false, "sightlines/tower_holds_fire_behind_rock", "no lip with a hidden floor cell in reach"); return; }
            var (lip, seen, hidden) = pair.Value;
            var placer = ServiceLocator.Get<VinePlacer>();
            var tower = placer.TestPlaceAt(VineNodeType.DamageTower, lip);
            if (tower == null) { ctx.Assert(false, "sightlines/tower_holds_fire_behind_rock", $"no tower at {lip}: {placer.WhyNotType(lip)}"); return; }
            await ctx.Wait(0.3f);

            var behind = Dummy(_grid.GridToWorld(hidden));
            await ctx.Wait(0.2f);
            bool hiddenOk = !tower.Sees(behind);
            float d0 = Flat(tower.GlobalPosition, behind.GlobalPosition);
            await ctx.Wait(2.5f);
            float lost = 1e5f - behind.CurrentHealth;
            ctx.Assert(hiddenOk && lost <= 0f, "sightlines/tower_holds_fire_behind_rock",
                $"tower {lip}, target {hidden} {d0:0.0} out (range {tower.CurrentRange:0.0}); sees it {!hiddenOk}, it lost {lost:0}");

            ctx.StartTest();
            var inView = Dummy(_grid.GridToWorld(seen));
            await ctx.Wait(2.5f);
            float lost2 = 1e5f - inView.CurrentHealth;
            ctx.Assert(tower.Sees(inView) && lost2 > 0f, "sightlines/tower_fires_in_sight",
                $"target {seen} {Flat(tower.GlobalPosition, inView.GlobalPosition):0.0} out: sees it {tower.Sees(inView)}, it lost {lost2:0}");

            // Its shots go where it looked: to the body, clear of the lip
            ctx.StartTest();
            var eye = tower.EyePoint;
            var body = inView.BodyCentre;
            ctx.Assert(_grid.HasLineOfSight(eye, body) && eye.Y > tower.GlobalPosition.Y, "sightlines/tower_eye_is_its_muzzle",
                $"eye {eye.Y - tower.GlobalPosition.Y:0.00} over the tower, line to the body clear");
            ClearDummies();
            _grid.RemoveNode(lip);
            await ctx.Wait(0.3f);
        }

        /// <summary>An armed enemy below doesn't shoot a tower it can't see, and does once it can.</summary>
        private async Task EnemySight(TestContext ctx)
        {
            ctx.StartTest();
            const float reach = 10f;
            var pair = FindSightPair(reach - 1f);
            if (pair == null) { ctx.Assert(false, "sightlines/enemy_holds_fire_behind_rock", "no lip with a hidden floor cell in reach"); return; }
            var (lip, seen, hidden) = pair.Value;
            var placer = ServiceLocator.Get<VinePlacer>();
            var tower = placer.TestPlaceAt(VineNodeType.BuffEmitter, lip); // a tower that shoots nothing back
            if (tower == null) { ctx.Assert(false, "sightlines/enemy_holds_fire_behind_rock", $"no tower at {lip}: {placer.WhyNotType(lip)}"); return; }
            tower.NodeMaxHealth = 1e6f;
            tower.NodeCurrentHealth = 1e6f;
            await ctx.Wait(0.3f);
            var gunner = Dummy(_grid.GridToWorld(hidden), 1e6f, reach, 10f);
            await ctx.Wait(3f);
            float lost = 1e6f - tower.NodeCurrentHealth;
            ctx.Assert(lost <= 0f, "sightlines/enemy_holds_fire_behind_rock",
                $"gunner at {hidden}, {Flat(gunner.GlobalPosition, tower.GlobalPosition):0.0} from the tower at {lip}: the tower lost {lost:0}");
            ClearDummies();

            ctx.StartTest();
            var gunner2 = Dummy(_grid.GridToWorld(seen), 1e6f, reach, 10f);
            await ctx.Wait(3f);
            float lost2 = 1e6f - tower.NodeCurrentHealth;
            ctx.Assert(lost2 > 0f, "sightlines/enemy_fires_in_sight",
                $"gunner at {seen}, {Flat(gunner2.GlobalPosition, tower.GlobalPosition):0.0} from the tower: the tower lost {lost2:0}");
            ClearDummies();
            _grid.RemoveNode(lip);
            await ctx.Wait(0.3f);
        }

        /// <summary>BIT's auto-fire skips an enemy behind a wall, and an aimed shot at it stops at the rock.</summary>
        private async Task BitSight(TestContext ctx, VinePlayer bit)
        {
            ctx.StartTest();
            if (bit == null) { ctx.Assert(false, "sightlines/bit_holds_fire_behind_rock", "no BIT"); return; }
            float range = bit.EffectiveRange;
            var floor = FloorCells().ToList();
            Vector2I? me = null, them = null;
            foreach (var a in floor)
            {
                foreach (var b in floor)
                {
                    float d = Flat(_grid.GridToWorld(a), _grid.GridToWorld(b));
                    if (d < 3f || d > range - 1f) continue;
                    var chestA = Sight.WalkerAt(_grid, a) + Vector3.Up * 0.4f;
                    if (_grid.HasLineOfSight(chestA, Sight.WalkerAt(_grid, b))) continue;
                    me = a; them = b;
                    break;
                }
                if (me.HasValue) break;
            }
            if (!me.HasValue) { ctx.Assert(false, "sightlines/bit_holds_fire_behind_rock", $"no two floors within {range:0} with rock between"); return; }
            var p = _grid.GridToWorld(me.Value);
            bit.GlobalPosition = new Vector3(p.X, _grid.GetWorldHeight(p.X, p.Z), p.Z);
            await ctx.Wait(0.3f);
            var target = Dummy(_grid.GridToWorld(them.Value));
            await ctx.Wait(2f);
            float lost = 1e5f - target.CurrentHealth;
            ctx.Assert(lost <= 0f, "sightlines/bit_holds_fire_behind_rock",
                $"BIT at {me}, target at {them} {Flat(bit.GlobalPosition, target.GlobalPosition):0.0} away behind a wall: it lost {lost:0}");

            ctx.StartTest();
            int rock0 = bit.RockShots, shots0 = bit.AimedShots;
            bit.TestAimPoint = target.GlobalPosition;
            var mid = ctx.Tree.Root.GetVisibleRect().Size * 0.5f;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = mid, GlobalPosition = mid });
            await ctx.Wait(1.2f);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = mid, GlobalPosition = mid });
            await ctx.Wait(0.3f);
            bit.TestAimPoint = null;
            float lost2 = 1e5f - target.CurrentHealth;
            ctx.Assert(bit.AimedShots > shots0 && bit.RockShots > rock0 && lost2 <= 0f, "sightlines/aimed_shot_stops_at_rock",
                $"{bit.AimedShots - shots0} aimed shots, {bit.RockShots - rock0} into the rock, the target lost {lost2:0}");
            ClearDummies();
            await ctx.Wait(0.2f);
        }

        /// <summary>
        /// Through a wave with Junk Turrets along the route: their shots fly clear of the ground
        /// (they aim at bodies they can see), enemy shots too, and no walker stalls on the floor.
        /// </summary>
        private async Task WaveLooks(TestContext ctx, VineWaveManager waves)
        {
            ctx.StartTest();
            var pf = ServiceLocator.Get<VinePathfinder>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var route = pf.GetCachedPath(_grid.ActiveEntryRegions[0].Center) ?? new List<Vector2I>();
            var towers = new List<VineNode>();
            foreach (var c in TestCells.BesideRoute(_grid, route, 6, 4))
            {
                var t = placer.TestPlaceAt(VineNodeType.DamageTower, c);
                if (t != null) towers.Add(t);
            }
            ctx.Assert(towers.Count >= 4, "sightlines/wave_towers_placed", $"{towers.Count} Junk Turrets on the lip");

            var seenProj = new HashSet<ulong>();
            var skimmed = new HashSet<ulong>();
            int towerShots = 0, towerSkims = 0, enemyShots = 0, enemySkims = 0, buried = 0;
            var lastMove = new Dictionary<VineEnemy, (Vector3 at, ulong t)>();
            int stalls = 0;
            string stallNote = "";
            Engine.TimeScale = 3.0;
            ulong t0 = Time.GetTicksMsec();
            try
            {
                waves.StartWave();
                var tough = new HashSet<VineEnemy>();
                while (Time.GetTicksMsec() - t0 < 25000)
                {
                    await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.PhysicsFrame);
                    // Tough enough to walk the whole route under fire
                    foreach (var e in Roster.Enemies(ctx.Tree))
                        if (GodotObject.IsInstanceValid(e) && e.IsAlive && tough.Add(e)) e.SetChaosHP(1e6f);
                    foreach (var child in _scene.GetChildren())
                    {
                        if (child is not VineProjectile vp || !GodotObject.IsInstanceValid(vp)) continue;
                        ulong id = vp.GetInstanceId();
                        bool fromTower = towers.Any(t => GodotObject.IsInstanceValid(t) && Flat(t.GlobalPosition, vp.Origin) < 1.6f && vp.Origin.Y > t.GlobalPosition.Y - 0.2f);
                        if (seenProj.Add(id)) { if (fromTower) towerShots++; else enemyShots++; }
                        if ((vp.Skimmed || vp.HitGround) && skimmed.Add(id))
                        {
                            if (fromTower) towerSkims++; else enemySkims++;
                            if (vp.HitGround) buried++;
                        }
                    }
                    ulong now = Time.GetTicksMsec();
                    foreach (var e in Roster.Enemies(ctx.Tree))
                    {
                        if (!GodotObject.IsInstanceValid(e) || !e.IsAlive || e.IsFlying || e.IsRaider || !e.OnGrid || e.IsStunned || e.AtBarricade) continue;
                        var at = e.GlobalPosition;
                        if (Flat(at, _grid.GridToWorld(_grid.ExitPoint)) < 6f) continue;
                        if (!lastMove.TryGetValue(e, out var lm) || Flat(lm.at, at) > 0.6f) { lastMove[e] = (at, now); continue; }
                        // Game time: 4 s at 3x
                        if (now - lm.t > 4000 / 3)
                        {
                            stalls++;
                            if (stallNote.Length == 0) stallNote = $" first {e.EnemyName} at {_grid.WorldToGrid(at)} ({_grid.GetCell(_grid.WorldToGrid(at))})";
                            lastMove[e] = (at, now);
                        }
                    }
                    if (!waves.WaveActive && Time.GetTicksMsec() - t0 > 3000) break;
                    if (Time.GetTicksMsec() - t0 > 20000 && lastMove.Count > 0) break;
                }
            }
            finally { Engine.TimeScale = 1.0; }
            ctx.StartTest();
            ctx.Assert(towerShots >= 10 && towerSkims <= towerShots / 50, "sightlines/tower_shots_clear_the_ground",
                $"{towerSkims} of {towerShots} turret shots ran along the ground");
            ctx.StartTest();
            ctx.Assert(enemyShots == 0 || enemySkims <= Math.Max(1, enemyShots / 20), "sightlines/enemy_shots_clear_the_ground",
                $"{enemySkims} of {enemyShots} other shots ran along the ground ({buried} into it)");
            ctx.StartTest();
            ctx.Assert(lastMove.Count >= 5 && stalls == 0, "sightlines/walkers_never_stall",
                $"{stalls} stalls among {lastMove.Count} walkers{stallNote}");
            foreach (var e in Roster.Enemies(ctx.Tree)) e.QueueFree();
            await ctx.Wait(0.3f);
        }
    }
}
