using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Per-frame allocation guards. Headless runs are frame-uncapped, so anything that
    /// allocates a Resource every frame (meshes, materials, SurfaceTools) shows up here as
    /// steady resource-count growth long before it becomes an out-of-memory kill.
    ///
    /// Measured in frames, not seconds: a leak is "N resources per frame", independent of
    /// how fast the machine runs the loop.
    /// </summary>
    public class PerfTestSuite : ITestSuite
    {
        public string SuiteName => "perf";

        // Steady-state budget: a battle at rest should allocate ~0 engine objects per frame.
        // Allow headroom for timers/tweens/VFX particles that come and go.
        private const float MaxObjectGrowthPerFrame = 1.0f;
        private const int SampleFrames = 300;

        public async Task Run(TestContext ctx)
        {
            GD.Print("[PerfTestSuite] Starting per-frame allocation tests...");

            var logger = ErrorCaptureLogger.Install();
            logger.Reset();
            bool inBuild = await LoadBattle(ctx);
            ctx.Assert(inBuild, "perf/battle_loads", "Battle should reach Build phase");
            if (!inBuild) return;
            await WaitFrames(ctx, 30); // shaders compile on first draw
            CheckNoEngineErrors(ctx, logger, "health/battle_load_errors");

            await TestDomeGeometryIsCached(ctx);
            await TestIdleBattleResourceGrowth(ctx, "perf/idle_battle");

            int placed = FillGridWithTowers(ctx);
            ctx.AssertGreater(placed, 50f, "perf/dense_grid_placed",
                $"Dense-grid scenario should place many nodes (placed {placed})");
            await TestIdleBattleResourceGrowth(ctx, "perf/dense_grid");

            // Every free cell filled with connecting nodes — the integration.max_node_density
            // scenario, which grew to ~6 GB and was OOM-killed headless.
            int extenders = FillGridWithExtenders();
            ctx.AssertGreater(extenders, 50f, "perf/full_grid_placed",
                $"Full-grid scenario should place many nodes (placed {extenders})");
            await TestIdleBattleResourceGrowth(ctx, "perf/full_grid");
            await TestSingleEditCost(ctx);

            GD.Print("[PerfTestSuite] Complete.");
        }

        private async Task<bool> LoadBattle(TestContext ctx)
        {
            var gm = GameManager.Instance;
            gm.CurrentTerritorySectionId = null;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            return await ctx.WaitForPhase(GamePhase.Build, 15f);
        }

        // Missing source assets are expected in partial checkouts (models/textures/audio not
        // pulled); everything else — shader compile failures, script exceptions — is a bug.
        private static readonly string[] AssetNoise =
        {
            "Asset not found:", "Resource file not found:", "Error loading resource:", "Failed loading resource:",
            "No loader found for resource:", "Cannot open file", "FileAccess::exists(path)",
            "GDExtension dynamic library not found", "using fallback sphere",
        };

        private static void CheckNoEngineErrors(TestContext ctx, ErrorCaptureLogger logger, string name)
        {
            var (errors, _, _, _) = logger.Snapshot();
            var real = errors.FindAll(e => System.Array.TrueForAll(AssetNoise, n => !e.Contains(n)));
            foreach (var e in real) GD.PrintErr($"  [{name}] {e}");
            ctx.Assert(real.Count == 0, name,
                real.Count == 0 ? "No engine/script errors" : $"{real.Count} engine/script error(s): {string.Join(" | ", real.Take(3))}");
        }

        private static async Task WaitFrames(TestContext ctx, int frames)
        {
            for (int i = 0; i < frames; i++)
                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }

        private static ConversionDome FindDome(Node root)
        {
            if (root == null) return null;
            if (root is ConversionDome d) return d;
            foreach (var child in root.GetChildren())
            {
                var found = FindDome(child);
                if (found != null) return found;
            }
            return null;
        }

        // ── Dome: geometry is static between radius/color/terrain changes ──

        private async Task TestDomeGeometryIsCached(TestContext ctx)
        {
            ctx.StartTest();
            var dome = FindDome(ctx.Tree.CurrentScene);
            ctx.AssertNotNull(dome, "perf/dome_exists", "Battle scene should contain a ConversionDome");
            if (dome == null) return;

            await WaitFrames(ctx, 10); // let any startup rebuilds settle
            int before = dome.GeometryRebuildCount;
            await WaitFrames(ctx, SampleFrames);
            int rebuilds = dome.GeometryRebuildCount - before;

            ctx.Assert(rebuilds <= 2, "perf/dome_geometry_cached",
                $"Dome rebuilt its fog/floor geometry {rebuilds} times in {SampleFrames} idle frames (expected ~0)");
        }

        // ── Resource growth with nothing happening ──

        private async Task TestIdleBattleResourceGrowth(TestContext ctx, string name)
        {
            ctx.StartTest();
            // Hold the run in Build so no wave starts during the sample
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm)) wm.PauseAutoStart = true;

            await WaitFrames(ctx, 30);
            double res0 = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
            double obj0 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
            double nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);

            await WaitFrames(ctx, SampleFrames);

            double res1 = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
            double obj1 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
            double nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);

            float resPerFrame = (float)(res1 - res0) / SampleFrames;
            float objPerFrame = (float)(obj1 - obj0) / SampleFrames;
            GD.Print($"  [{name}] resources {res0}->{res1} ({resPerFrame:F2}/frame), " +
                     $"objects {obj0}->{obj1} ({objPerFrame:F2}/frame), nodes {nodes0}->{nodes1}");

            // ObjectResourceCount only counts cached (loaded) resources, so runtime meshes and
            // materials don't show there. ObjectCount covers every engine object.
            ctx.Assert(objPerFrame <= MaxObjectGrowthPerFrame, name + ".objects_per_frame",
                $"Engine objects grew {obj1 - obj0} over {SampleFrames} frames ({objPerFrame:F2}/frame, budget {MaxObjectGrowthPerFrame})");

            if (wm != null && GodotObject.IsInstanceValid(wm)) wm.PauseAutoStart = false;
        }

        /// <summary>
        /// Selling and re-placing one node in a large network must not touch every connection.
        /// (Each network change used to rebuild every connection's meshes: ~3 new objects per
        /// connection per edit.)
        /// </summary>
        private async Task TestSingleEditCost(TestContext ctx)
        {
            ctx.StartTest();
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            int connections = grid.GetConnections().Count();
            Vector2I? target = null;
            foreach (var conn in grid.GetConnections()) { target = conn.CellA; break; }
            if (target == null) { ctx.Assert(false, "perf/single_edit_cost", "No connected node to edit"); return; }

            var type = grid.GetNode(target.Value).Data.Type;
            await WaitFrames(ctx, 5);
            double obj0 = Performance.GetMonitor(Performance.Monitor.ObjectCount);

            grid.RemoveNode(target.Value);
            var data = VineNodeRegistry.Get(type);
            var node = new VineNode();
            node.Initialize(data);
            if (!grid.PlaceNode(node, target.Value)) node.QueueFree();
            await WaitFrames(ctx, 3);

            double delta = Performance.GetMonitor(Performance.Monitor.ObjectCount) - obj0;
            GD.Print($"  [perf/single_edit_cost] {connections} connections, object delta after sell+place: {delta}");
            ctx.Assert(delta < 100, "perf/single_edit_cost",
                $"Selling and re-placing one node changed the object count by {delta} with {connections} connections (expected a small constant)");
        }

        private static int FillGridWithExtenders()
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return 0;
            var data = VineNodeRegistry.Get(VineNodeType.Extender);
            if (data == null) return 0;
            int placed = 0;
            for (int x = 2; x < grid.Width - 2; x++)
            {
                for (int y = 2; y < grid.Height - 2; y++)
                {
                    var pos = new Vector2I(x, y);
                    if (!grid.CanPlace(pos)) continue;
                    var node = new VineNode();
                    node.Initialize(data);
                    if (grid.PlaceNode(node, pos)) placed++;
                    else node.QueueFree();
                }
            }
            GD.Print($"  [perf] Placed {placed} extenders for full-grid scenario");
            return placed;
        }

        // ── Dense grid: the scenario that ran out of memory in integration.max_node_density ──

        private static int FillGridWithTowers(TestContext ctx)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return 0;
            int placed = 0;
            var types = new List<VineNodeType> { VineNodeType.DamageTower, VineNodeType.BarrierWall, VineNodeType.SlowField };
            for (int x = 2; x < grid.Width - 2; x += 2)
            {
                for (int y = 2; y < grid.Height - 2; y += 2)
                {
                    var pos = new Vector2I(x, y);
                    if (!grid.CanPlace(pos)) continue;
                    var data = VineNodeRegistry.Get(types[placed % types.Count]);
                    if (data == null) continue;
                    var node = new VineNode();
                    node.Initialize(data);
                    if (grid.PlaceNode(node, pos)) placed++;
                    else node.QueueFree();
                }
            }
            GD.Print($"  [perf] Placed {placed} towers for dense-grid scenario");
            return placed;
        }
    }
}
