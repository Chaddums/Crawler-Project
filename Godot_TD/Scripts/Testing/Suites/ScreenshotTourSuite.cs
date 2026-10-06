using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Visual smoke tour: loads a Grid Prime battle, places a few towers, runs a wave and saves
    /// screenshots to test-reports/screenshots/tour_*.png. Needs a real display (or a virtual one
    /// such as Xvfb) — skipped in --headless. Not part of "all"; run with --suite=screens.
    /// </summary>
    public class ScreenshotTourSuite : ITestSuite
    {
        public string SuiteName => "screens";

        public async Task Run(TestContext ctx)
        {
            if (DisplayServer.GetName() == "headless")
            {
                GD.Print("[ScreenshotTour] Headless display — nothing to capture");
                return;
            }

            var gm = GameManager.Instance;
            gm.CurrentTerritorySectionId = null;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.CurrentPlanet = 1;
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(inBuild, "screens/battle_loads");
            if (!inBuild) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm)) wm.PauseAutoStart = true;
            await ctx.Wait(1.5f);
            Save(ctx, "tour_01_build_phase");

            // A short line of towers alongside the enemy path
            var grid = ServiceLocator.Get<VineGrid>();
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            var path = grid.ActiveEntryRegions.Count > 0 && pf != null
                ? pf.GetCachedPath(grid.ActiveEntryRegions[0].Cells[grid.ActiveEntryRegions[0].Cells.Count / 2]) : null;
            int placed = 0;
            if (path != null)
            {
                var types = new[] { VineNodeType.DamageTower, VineNodeType.SlowField, VineNodeType.TeslaCoil, VineNodeType.ScatterCannon };
                // Near the Spire end of the path so combat happens in the default camera frame
                for (int i = System.Math.Max(2, path.Count - 16); i < path.Count - 3 && placed < 8; i += 2)
                {
                    foreach (var off in new[] { new Vector2I(0, 2), new Vector2I(0, -2), new Vector2I(2, 0), new Vector2I(-2, 0) })
                    {
                        var c = path[i] + off;
                        if (!grid.CanPlace(c) || pf.WouldBlockAllPaths(c)) continue;
                        var node = new VineNode();
                        node.Initialize(VineNodeRegistry.Get(types[placed % types.Length]));
                        if (grid.PlaceNode(node, c)) { placed++; break; }
                        node.QueueFree();
                    }
                }
            }
            await ctx.Wait(1.0f);
            Save(ctx, "tour_02_towers_placed");

            if (wm != null)
            {
                wm.PauseAutoStart = false;
                grid.Harvester?.IncreaseMaxHP(100000f);
                wm.StartWave();
                // Wait for the first enemy to come within ~12 units of the Spire, then burst-capture
                var spire = grid.GridToWorld(grid.ExitPoint);
                await ctx.WaitUntil(() =>
                {
                    foreach (var n in ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
                        if (n is Node3D e && e.GlobalPosition.DistanceTo(spire) < 12f) return true;
                    return false;
                }, 40f);
                for (int k = 0; k < 4; k++)
                {
                    Save(ctx, $"tour_03_combat_{k}");
                    await ctx.Wait(0.35f);
                }
            }
            ctx.Assert(true, "screens/captured", $"Placed {placed} towers; screenshots in test-reports/screenshots/");
        }

        private static void Save(TestContext ctx, string name)
        {
            var path = ctx.CaptureScreenshot(name);
            ctx.Assert(path != null, $"screens/{name}");
        }
    }
}
