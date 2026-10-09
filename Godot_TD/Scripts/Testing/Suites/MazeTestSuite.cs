using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// "Your network IS the maze." Enemies used to beeline at the Spire and walk straight through
    /// towers and walls; the A* path only kicked in when they got physically stuck. These tests
    /// wall off the straight line from every active entry (without sealing the route) and check
    /// that no non-Ghost enemy is ever standing inside a solid node cell.
    /// </summary>
    public class MazeTestSuite : ITestSuite
    {
        public string SuiteName => "maze";

        private VineGrid _grid;
        private VineWaveManager _wm;
        private VinePathfinder _pf;

        public async Task Run(TestContext ctx)
        {
            GD.Print("[MazeTestSuite] Starting maze tests...");

            var gm = GameManager.Instance;
            gm.CurrentTerritorySectionId = null;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 15f);
            ctx.Assert(inBuild, "maze/battle_loads", "Battle should reach Build phase");
            if (!inBuild) return;

            _grid = ServiceLocator.Get<VineGrid>();
            _wm = ServiceLocator.Get<VineWaveManager>();
            ServiceLocator.TryGet(out _pf);
            ctx.AssertNotNull(_pf, "maze/pathfinder_exists");
            if (_grid == null || _wm == null || _pf == null) return;

            await TestRouteAroundCup(ctx);

            int walls = WallOffBeelines();
            ctx.AssertGreater(walls, 3f, "maze/walls_placed",
                $"Should place a wall across the straight entry→Spire line (placed {walls})");

            await TestEnemiesRespectWalls(ctx);

            GD.Print("[MazeTestSuite] Complete.");
        }

        /// <summary>
        /// Place BarrierWalls along the straight line from each active entry to the Spire,
        /// skipping any cell whose wall would leave no route at all.
        /// </summary>
        private int WallOffBeelines()
        {
            var data = VineNodeRegistry.Get(VineNodeType.BarrierWall);
            if (data == null) return 0;
            int placed = 0;
            var exit = _grid.ExitPoint;

            foreach (var region in _grid.ActiveEntryRegions)
            {
                if (region.Cells.Count == 0) continue;
                var start = region.Cells[region.Cells.Count / 2];
                var delta = new Vector2(exit.X - start.X, exit.Y - start.Y);
                int steps = Mathf.CeilToInt(delta.Length() * 2f);
                var seen = new HashSet<Vector2I>();

                for (int i = 0; i <= steps; i++)
                {
                    var p = new Vector2(start.X, start.Y) + delta * (i / (float)steps);
                    var cell = new Vector2I(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y));
                    if (!seen.Add(cell)) continue;
                    // Leave room around the entry and the Spire
                    if (Mathf.Abs(cell.X - start.X) + Mathf.Abs(cell.Y - start.Y) < 3) continue;
                    if (Mathf.Abs(cell.X - exit.X) + Mathf.Abs(cell.Y - exit.Y) < 4) continue;
                    if (!_grid.CanPlace(cell) || _pf.WouldBlockAllPaths(cell)) continue;

                    var node = new VineNode();
                    node.Initialize(data);
                    if (_grid.PlaceNode(node, cell)) placed++;
                    else node.QueueFree();
                }
            }
            GD.Print($"  [maze] Placed {placed} walls across entry→Spire lines");
            return placed;
        }

        /// <summary>
        /// Real mazing: a cup of walls on the straight line, open toward the entry. Enemies used to
        /// beeline into it and only then look for a way round; now the route goes round it from
        /// the start, so no walker ever stands inside the cup. The route gets longer, the ground
        /// shows it, and a build ghost on the route shows the route it would make.
        /// </summary>
        private async Task TestRouteAroundCup(TestContext ctx)
        {
            ctx.StartTest();
            var region = _grid.ActiveEntryRegions.Count > 0 ? _grid.ActiveEntryRegions[0] : null;
            if (region == null) { ctx.Assert(false, "maze/cup_route", "no entry"); return; }
            var e0 = region.Center;
            var sp = _grid.ExitPoint;
            int before = _pf.FlowPath(e0)?.Count ?? 0;
            var d = new Vector2(sp.X - e0.X, sp.Y - e0.Y);
            float len = d.Length();
            d /= len;
            var pp = new Vector2(-d.Y, d.X);
            var mid = new Vector2(e0.X, e0.Y) + d * len * 0.5f;
            Vector2I At(float along, float side)
            {
                var v = mid + pp * side - d * along;
                return new Vector2I(Mathf.RoundToInt(v.X), Mathf.RoundToInt(v.Y));
            }
            var wallData = VineNodeRegistry.Get(VineNodeType.BarrierWall);
            var cup = new List<Vector2I>();
            var interior = new HashSet<Vector2I>();
            for (float k = -3; k <= 3; k += 0.5f) { var c = At(0, k); if (!cup.Contains(c)) cup.Add(c); }
            for (float j = 0.5f; j <= 3; j += 0.5f) foreach (int side in new[] { -3, 3 }) { var c = At(j, side); if (!cup.Contains(c)) cup.Add(c); }
            for (float j = 1; j <= 2; j += 0.5f) for (float k = -2; k <= 2; k += 0.5f) interior.Add(At(j, k));
            int placed = 0;
            foreach (var c in cup)
            {
                if (!_grid.CanPlace(c) || _pf.WouldBlockAllPaths(c)) continue;
                var node = new VineNode();
                node.Initialize(wallData);
                if (_grid.PlaceNode(node, c)) placed++; else node.QueueFree();
            }
            interior.ExceptWith(cup);
            await ctx.Wait(0.2f);
            var route = _pf.FlowPath(e0) ?? new List<Vector2I>();
            int inCup = 0;
            foreach (var c in route) if (interior.Contains(c)) inCup++;
            ctx.Assert(placed >= cup.Count - 3 && route.Count > before && inCup == 0, "maze/route_goes_round_the_cup",
                $"{placed}/{cup.Count} walls, route {before} -> {route.Count} cells, {inCup} route cells inside the cup");

            // The route on the ground, and the ghost's preview of a tower on it
            ctx.StartTest();
            var preview = PathPreviewNode(ctx.Tree.CurrentScene);
            await ctx.Wait(0.3f);
            int marks = preview?.RouteMarks ?? 0;
            string ghostNote = "no placer";
            bool ghostOk = false;
            if (ServiceLocator.TryGet<VinePlacer>(out var placer) && preview != null)
            {
                // A cell on the route, a few steps in: placing there must bend the preview
                Vector2I target = route.Count > 8 ? route[route.Count / 3] : route.Count > 0 ? route[0] : e0;
                placer.StartPlacing(VineNodeType.DamageTower);
                placer.TestSetGhost(target);
                await ctx.Wait(0.3f);
                var pv = preview.FirstPreview;
                ghostOk = preview.PreviewMarks > 0 && !pv.Contains(target);
                ghostNote = $"ghost on {target}: preview {preview.PreviewMarks} marks, avoids the cell {!pv.Contains(target)}";
                placer.CancelPlacing();
            }
            ctx.Assert(marks > 5 && ghostOk, "maze/route_drawn_and_previewed", $"{marks} marks on the route; {ghostNote}");

            // Walkers go round, never into the cup
            ctx.StartTest();
            GameManager.Instance.SetCoreLives(999);
            _grid.Harvester?.IncreaseMaxHP(100000f);
            Engine.TimeScale = 4.0;
            int samples = 0, inside = 0;
            ulong t0 = Time.GetTicksMsec();
            try
            {
                _wm.StartWave();
                while (Time.GetTicksMsec() - t0 < 40000)
                {
                    await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.PhysicsFrame);
                    foreach (var n in ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
                    {
                        if (n is not VineEnemy e || !e.IsAlive || e.Faction == VineEnemyFaction.Ghost || e.IsFlying) continue;
                        var c = _grid.WorldToGrid(e.GlobalPosition);
                        if (!_grid.InBounds(c)) continue;
                        samples++;
                        if (interior.Contains(c)) inside++;
                    }
                    if (!_wm.WaveActive && Time.GetTicksMsec() - t0 > 2000) break;
                }
            }
            finally { Engine.TimeScale = 1.0; }
            ctx.Assert(samples > 50 && inside <= samples / 200, "maze/walkers_go_round_the_cup",
                $"{inside} of {samples} walker samples inside the cup");
            foreach (var n in ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY)) if (n is VineEnemy ve) ve.QueueFree();
            await ctx.Wait(0.3f);
            foreach (var c in cup) if (_grid.GetNode(c) != null) _grid.RemoveNode(c);
            await ctx.WaitForPhase(GamePhase.Build, 10f);
        }

        private static PathPreview PathPreviewNode(Node n)
        {
            if (n is PathPreview p) return p;
            foreach (var c in n.GetChildren()) { var r = PathPreviewNode(c); if (r != null) return r; }
            return null;
        }

        private bool IsSolid(Vector2I cell)
        {
            if (!_grid.InBounds(cell)) return false;
            var node = _grid.GetNode(cell);
            return node != null && !node.IsWalkable && !node.IsDestroyed;
        }

        private async Task TestEnemiesRespectWalls(TestContext ctx)
        {
            ctx.StartTest();
            GameManager.Instance.SetCoreLives(999);
            _grid.Harvester?.IncreaseMaxHP(100000f); // keep the run alive while we watch
            Engine.TimeScale = 4.0;
            int observed = 0, samples = 0, intrusions = 0;
            var seenEnemies = new HashSet<ulong>();
            var examples = new List<string>();
            ulong startMs = Time.GetTicksMsec();
            try
            {
            _wm.StartWave();

            // Sample every frame until the wave ends (or 60 s wall clock)
            while (Time.GetTicksMsec() - startMs < 60000)
            {
                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.PhysicsFrame);
                foreach (var n in ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
                {
                    if (n is not VineEnemy e || !GodotObject.IsInstanceValid(e) || !e.IsAlive) continue;
                    if (e.Faction == VineEnemyFaction.Ghost) continue; // phase through walls by design
                    var cell = _grid.WorldToGrid(e.GlobalPosition);
                    if (!_grid.InBounds(cell)) continue; // still marching in from off-grid
                    samples++;
                    if (seenEnemies.Add(e.GetInstanceId())) observed++;
                    if (IsSolid(cell))
                    {
                        intrusions++;
                        if (examples.Count < 5) examples.Add($"{e.Faction} at ({cell.X},{cell.Y})");
                    }
                }
                if (!_wm.WaveActive && Time.GetTicksMsec() - startMs > 2000) break;
            }
            }
            finally
            {
                Engine.TimeScale = 1.0;
            }

            GD.Print($"  [maze] {observed} enemies, {samples} on-grid samples, {intrusions} inside walls");
            ctx.AssertGreater(observed, 0f, "maze/enemies_observed", "A wave should put enemies on the grid");
            ctx.Assert(intrusions == 0, "maze/enemies_respect_walls",
                $"{intrusions} of {samples} enemy samples were inside a solid node cell: {string.Join(", ", examples)}");
        }
    }
}
