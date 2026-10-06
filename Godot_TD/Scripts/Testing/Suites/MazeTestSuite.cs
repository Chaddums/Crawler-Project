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
