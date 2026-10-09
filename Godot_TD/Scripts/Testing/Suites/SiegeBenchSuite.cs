using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The reported late game: about 40 Junk Turrets at the Minigun branch packed round the Spire
    /// (P1, first site) with 120 enemies on the field, held for 15 s. Measures the script time
    /// per frame (process + physics), objects and nodes, and asserts a budget. "siege" (also in
    /// "perf-heavy", not in "all"). With a display it also reports the frame rate.
    /// </summary>
    public class SiegeBenchSuite : ITestSuite
    {
        public string SuiteName => "siege";
        private const int Towers = 40;
        private const int Enemies = 120;

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "siege/battle_loads", "no site"); return; }
            gm.CurrentPlanet = site.Planet;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = SpireData.Get("Bruteforge")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, "siege/battle_loads", site.Id);
            if (!ok) return;

            var grid = ServiceLocator.Get<VineGrid>();
            var waves = ServiceLocator.Get<VineWaveManager>();
            var pf = ServiceLocator.Get<VinePathfinder>();
            grid.Harvester?.IncreaseMaxHP(1e7f);
            gm.AddResources(100000);
            waves.PauseAutoStart = true;

            // Turrets in rows two apart round the Spire, leaving lanes
            var exit = grid.ExitPoint;
            int placed = 0;
            for (int r = 3; r < 16 && placed < Towers; r++)
                for (int dx = -r; dx <= r && placed < Towers; dx++)
                    for (int dy = -r; dy <= r && placed < Towers; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r || (dx & 1) != 0) continue;
                        var c = exit + new Vector2I(dx, dy);
                        if (!grid.CanPlace(c) || pf.WouldBlockAllPaths(c)) continue;
                        if (AutoPlaceHelper.PlaceAt(grid, c.X, c.Y, VineNodeType.DamageTower))
                        {
                            grid.GetNode(c)?.ForceUpgrade(3, "minigun");
                            placed++;
                        }
                    }
            await ctx.Wait(0.5f);

            // 120 enemies from this wave's mix, spread over the open field away from the Spire
            waves.RequestNextWave();
            await ctx.Wait(0.5f);
            var rng = new RandomNumberGenerator { Seed = 7 };
            int spawned = 0;
            for (int i = 0; i < 4000 && spawned < Enemies; i++)
            {
                var c = new Vector2I(rng.RandiRange(1, grid.Width - 2), rng.RandiRange(1, grid.Height - 2));
                if ((c - exit).Length() < 14f || !grid.IsWalkable(c)) continue;
                if (waves.SpawnReinforcement(c, 400f) != null) spawned++; // tough enough to stay on the field
            }

            // Hold and measure
            await ctx.Wait(1f);
            // Fixed 60 Hz physics with uncapped frames: count the wall time each frame takes
            var sw = Stopwatch.StartNew();
            var frameSw = Stopwatch.StartNew();
            var times = new System.Collections.Generic.List<double>();
            int frames = 0;
            double script = 0, worst = 0;
            int peakObjects = 0, peakNodes = 0, minEnemies = int.MaxValue;
            int peakDraws = 0, peakPrims = 0, peakVisible = 0;
            float topUp = 0f;
            FrameProfiler.Reset();
            FrameProfiler.Enabled = true;
            while (sw.Elapsed.TotalSeconds < 15)
            {
                // Keep the field full: leakers are replaced
                if ((topUp -= (float)frameSw.Elapsed.TotalSeconds) <= 0f)
                {
                    topUp = 0.25f;
                    int alive = ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).OfType<VineEnemy>().Count(e => e.IsAlive);
                    for (int i = 0; i < 200 && alive < Enemies; i++)
                    {
                        var c = new Vector2I(rng.RandiRange(1, grid.Width - 2), rng.RandiRange(1, grid.Height - 2));
                        if ((c - exit).Length() < 14f || !grid.IsWalkable(c)) continue;
                        if (waves.SpawnReinforcement(c, 400f) != null) alive++;
                    }
                }
                frameSw.Restart();
                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
                frames++;
                double t = frameSw.Elapsed.TotalSeconds;
                times.Add(t);
                script += t;
                worst = Mathf.Max((float)worst, (float)t);
                peakObjects = Mathf.Max(peakObjects, (int)Performance.GetMonitor(Performance.Monitor.ObjectCount));
                peakNodes = Mathf.Max(peakNodes, (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
                peakDraws = Mathf.Max(peakDraws, (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
                peakPrims = Mathf.Max(peakPrims, (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
                peakVisible = Mathf.Max(peakVisible, (int)Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame));
                minEnemies = Mathf.Min(minEnemies, ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).Count);
            }
            FrameProfiler.Enabled = false;
            foreach (var (key, ms, calls) in FrameProfiler.Report(frames))
                GD.Print($"[Siege] script {key}: {ms:F2} ms a frame ({calls} calls)");
            double avgMs = script / frames * 1000.0;
            double fps = frames / sw.Elapsed.TotalSeconds;
            times.Sort();
            double p95 = times[(int)(times.Count * 0.95)] * 1000.0;
            GD.Print($"[Siege] {placed} miniguns, {spawned} enemies (at least {minEnemies} all along): frame {avgMs:F2} ms (p95 {p95:F1}, worst {worst * 1000:F1}), {fps:F0} fps, peak {peakObjects} objects, {peakNodes} nodes, {peakDraws} draw calls, {peakVisible} objects drawn, {peakPrims / 1000}k primitives");
            ctx.StartTest();
            ctx.Assert(placed >= Towers * 0.9f && spawned >= Enemies * 0.9f, "siege/scene_built", $"{placed} turrets, {spawned} enemies");
            bool headless = DisplayServer.GetName() == "headless";
            ctx.Assert(!headless || p95 < 16.0, "siege/frame_time_budget", (headless ? "" : "(not checked with a display: software rendering) ") +
                $"{avgMs:F2} ms a frame (p95 {p95:F1}, worst {worst * 1000:F1}) with {placed} miniguns and {minEnemies}+ enemies, {peakNodes} nodes, {fps:F0} fps (headless: game logic only)");

            Census(ctx.Tree.CurrentScene);
            if (DisplayServer.GetName() != "headless")
            {
                // What the renderer draws with each group hidden in turn
                async Task<(int draws, int prims)> Sample()
                {
                    int d = 0, p = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        d = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
                        p = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
                    }
                    return (d, p);
                }
                var all = await Sample();
                var enemyNodes = ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).OfType<Node3D>().ToList();
                foreach (var e in enemyNodes) e.Visible = false;
                var noEnemies = await Sample();
                foreach (var e in enemyNodes) e.Visible = true;
                var towerNodes = ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_NODE).OfType<Node3D>().ToList();
                foreach (var t in towerNodes) t.Visible = false;
                var noTowers = await Sample();
                foreach (var t in towerNodes) t.Visible = true;
                var rims = new List<(MeshInstance3D mi, Material m)>();
                foreach (var t in towerNodes)
                    foreach (var mi in t.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                        if (mi.MaterialOverlay != null) { rims.Add((mi, mi.MaterialOverlay)); mi.MaterialOverlay = null; }
                var noRims = await Sample();
                foreach (var (mi, m) in rims) mi.MaterialOverlay = m;
                var sun = ctx.Tree.CurrentScene.FindChildren("*", "DirectionalLight3D", true, false).OfType<DirectionalLight3D>().Where(l => l.ShadowEnabled).ToList();
                var modes = sun.Select(l => l.DirectionalShadowMode).ToList();
                foreach (var l in sun) l.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
                var four = await Sample();
                foreach (var l in sun) l.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits;
                var two = await Sample();
                for (int i = 0; i < sun.Count; i++) sun[i].DirectionalShadowMode = modes[i];
                foreach (var l in sun) l.ShadowEnabled = false;
                var noShadows = await Sample();
                foreach (var l in sun) l.ShadowEnabled = true;
                var vp = ctx.Tree.Root.GetViewport();
                float lod = vp.MeshLodThreshold;
                var lods = new List<string>();
                foreach (float th in new[] { 1f, 2f, 4f, 8f })
                {
                    vp.MeshLodThreshold = th;
                    var r = await Sample();
                    lods.Add($"{th:0} px {r.draws} / {r.prims / 1000}k");
                }
                vp.MeshLodThreshold = lod;
                GD.Print($"[Siege] drawn: as set {all.draws} draws / {all.prims / 1000}k prims; without enemies {noEnemies.draws} / {noEnemies.prims / 1000}k; without towers {noTowers.draws} / {noTowers.prims / 1000}k; without tower rims {noRims.draws} / {noRims.prims / 1000}k; sun 4 splits {four.draws} / {four.prims / 1000}k; 2 splits {two.draws} / {two.prims / 1000}k; no sun shadows {noShadows.draws} / {noShadows.prims / 1000}k");
                GD.Print($"[Siege] mesh LOD threshold (now {lod:0.#}): {string.Join("; ", lods)}");
            }

            foreach (var e in ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
                if (e is VineEnemy ve && ve.IsAlive) ve.TakeDamage(1e9f);
            gm.AutoResolvePerks = false;
            waves.PauseAutoStart = false;
        }

        /// <summary>Triangles and draws by what owns the mesh (towers, enemies, Spire, the rest).</summary>
        private static void Census(Node root)
        {
            var tris = new System.Collections.Generic.Dictionary<string, long>();
            var surf = new System.Collections.Generic.Dictionary<string, int>();
            var over = new System.Collections.Generic.Dictionary<string, int>();
            var shadow = new System.Collections.Generic.Dictionary<string, long>();
            foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                if (!mi.IsVisibleInTree() || mi.Mesh == null) continue;
                string cat = "other";
                for (Node n = mi; n != null; n = n.GetParent())
                {
                    if (n is VineNode) { cat = "towers"; break; }
                    if (n is VineEnemy) { cat = "enemies"; break; }
                    if (n is VineHarvester) { cat = "spire"; break; }
                    if (n is VinePlayer) { cat = "bit"; break; }
                    if (n is VineGrid) { cat = "grid"; break; }
                }
                long t = 0;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    var arr = mi.Mesh.SurfaceGetArrays(i);
                    int idx = arr[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? 0 : arr[(int)Mesh.ArrayType.Index].AsInt32Array().Length;
                    int vtx = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
                    t += (idx > 0 ? idx : vtx) / 3;
                }
                tris[cat] = tris.GetValueOrDefault(cat) + t;
                surf[cat] = surf.GetValueOrDefault(cat) + mi.Mesh.GetSurfaceCount();
                if (mi.MaterialOverlay != null) over[cat] = over.GetValueOrDefault(cat) + mi.Mesh.GetSurfaceCount();
                if (mi.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off) shadow[cat] = shadow.GetValueOrDefault(cat) + t;
            }
            foreach (var k in tris.Keys.OrderByDescending(k => tris[k]))
                GD.Print($"[Siege] census {k}: {tris[k] / 1000}k triangles, {surf[k]} surfaces, {over.GetValueOrDefault(k)} with an overlay, {shadow.GetValueOrDefault(k) / 1000}k triangles casting shadows");
        }
    }
}