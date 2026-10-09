using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The reported slowdown: frame rate falling the longer a run goes, even between waves with
    /// the field empty. A mixed defence of 30 towers on an unkillable Spire plays waves back to
    /// back at 4x (perks picked automatically) until <c>SOAK_WAVES</c> (default 30) or 15 min.
    /// At each build phase it counts nodes, objects and frame time, and by type what's in the
    /// scene; the check is that an empty field between waves costs about what it did early on.
    /// "soak" (not in "all").
    /// </summary>
    public class SoakTestSuite : ITestSuite
    {
        public string SuiteName => "soak";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "soak/battle_loads", "no site"); return; }
            int target = int.TryParse(System.Environment.GetEnvironmentVariable("SOAK_WAVES"), out var t) ? t : 30;
            // SOAK_STRONG=1: a strong late build (60 towers at the top level with a branch) on a
            // normal Spire, to see where the ramp after wave 20 ends it
            bool strong = System.Environment.GetEnvironmentVariable("SOAK_STRONG") == "1";
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = site.Planet;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = SpireData.Get("Bruteforge")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, "soak/battle_loads", site.Id);
            if (!ok) return;

            var grid = ServiceLocator.Get<VineGrid>();
            var waves = ServiceLocator.Get<VineWaveManager>();
            var pf = ServiceLocator.Get<VinePathfinder>();
            if (!strong) grid.Harvester?.IncreaseMaxHP(1e8f);
            gm.AddResources(200000);
            int want = strong ? 60 : 30;
            var cycle = new[] { VineNodeType.DamageTower, VineNodeType.FlakBattery, VineNodeType.TeslaCoil,
                VineNodeType.BuffEmitter, VineNodeType.ScatterCannon, VineNodeType.BarrierWall, VineNodeType.SlowField };
            var exit = grid.ExitPoint;
            int placed = 0;
            for (int r = 3; r < 18 && placed < want; r++)
                for (int dx = -r; dx <= r && placed < want; dx++)
                    for (int dy = -r; dy <= r && placed < want; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r || (dx & 1) != 0) continue;
                        var c = exit + new Vector2I(dx, dy);
                        if (!grid.CanPlace(c) || pf.WouldBlockAllPaths(c)) continue;
                        var type = cycle[placed % cycle.Length];
                        if (AutoPlaceHelper.PlaceAt(grid, c.X, c.Y, type))
                        {
                            var tn = grid.GetNode(c);
                            if (strong && tn?.UpgradePlan != null) tn.ForceUpgrade(3, tn.UpgradePlan.Branches.Count > 0 ? tn.UpgradePlan.Branches[placed % 2 % tn.UpgradePlan.Branches.Count].Id : null);
                            else if (placed % 2 == 0) tn?.ForceUpgrade(2, null);
                            placed++;
                        }
                    }
            GD.Print($"[Soak] placed {placed} towers; playing to wave {target}");

            var rows = new List<(int wave, int nodes, int objects, double frameMs, int enemies)>();
            Dictionary<string, int> early = null, late = null;
            Engine.TimeScale = 4.0;
            float clock = 0f;
            int lastLogged = -1;
            waves.RequestNextWave();
            while (clock < (strong ? 1500f : 900f))
            {
                await ctx.Wait(0.25f);
                clock += 0.25f;
                if (gm.CurrentPhase is GamePhase.Defeat or GamePhase.Debrief) break;
                if (gm.CurrentPhase == GamePhase.Build && !waves.WaveActive)
                {
                    int w = waves.CurrentWave;
                    if (strong && w != lastLogged) { lastLogged = w; GD.Print($"[Soak] strong build cleared W{w}, Spire {grid.Harvester?.CurrentHP:F0}/{grid.Harvester?.MaxHP:F0}, {ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_NODE).Count} nodes standing"); }
                    else if (w != lastLogged)
                    {
                        lastLogged = w;
                        // Let the last deaths and effects clear, then sample an empty field at 1x
                        Engine.TimeScale = 1.0;
                        await ctx.Wait(3.5f);
                        double ms = await FrameMs(ctx, 60);
                        int nodes = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                        int objects = (int)Performance.GetMonitor(Performance.Monitor.ObjectCount);
                        int enemies = ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).Count;
                        rows.Add((w, nodes, objects, ms, enemies));
                        GD.Print($"[Soak] after W{w}: {nodes} nodes, {objects} objects, {enemies} enemies left, {ms:F2} ms a frame (empty field)");
                        if (w >= 3 && early == null) early = Census(ctx.Tree.CurrentScene);
                        if (w >= target) { late = Census(ctx.Tree.CurrentScene); break; }
                        Engine.TimeScale = 4.0;
                    }
                    waves.RequestNextWave();
                }
            }
            Engine.TimeScale = 1.0;
            late ??= Census(ctx.Tree.CurrentScene);
            if (strong)
            {
                GD.Print($"[Soak] strong build reached wave {waves.CurrentWave} ({gm.CurrentPhase})");
                ctx.StartTest();
                ctx.Assert(true, "soak/strong_build_wave", $"wave {waves.CurrentWave}");
                gm.AutoResolvePerks = false;
                return;
            }

            ctx.StartTest();
            ctx.Assert(rows.Count >= 5, "soak/played_waves", $"{rows.Count} build phases, last wave {(rows.Count > 0 ? rows[^1].wave : 0)}");
            if (rows.Count < 5 || early == null) return;

            var growth = late.Keys.Union(early.Keys)
                .Select(k => (k, d: late.GetValueOrDefault(k) - early.GetValueOrDefault(k)))
                .Where(x => x.d != 0).OrderByDescending(x => System.Math.Abs(x.d)).Take(20).ToList();
            foreach (var (k, d) in growth)
                GD.Print($"[Soak] grew {d:+#;-#;0} {k} ({early.GetValueOrDefault(k)} -> {late.GetValueOrDefault(k)})");

            var first = rows.First(r => r.wave >= 3);
            var last = rows[^1];
            ctx.Assert(last.nodes <= first.nodes * 1.15 + 250, "soak/nodes_stay_flat",
                $"{first.nodes} nodes after W{first.wave}, {last.nodes} after W{last.wave}");
            ctx.Assert(last.objects <= first.objects * 1.15 + 1500, "soak/objects_stay_flat",
                $"{first.objects} objects after W{first.wave}, {last.objects} after W{last.wave}");
            ctx.Assert(last.frameMs <= first.frameMs * 1.35 + 0.5, "soak/empty_field_frame_flat",
                $"{first.frameMs:F2} ms after W{first.wave}, {last.frameMs:F2} ms after W{last.wave}");
            gm.AutoResolvePerks = false;
        }

        private static async Task<double> FrameMs(TestContext ctx, int frames)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
            return sw.Elapsed.TotalMilliseconds / frames;
        }

        /// <summary>Nodes by class, with the game object they belong to.</summary>
        private static Dictionary<string, int> Census(Node root)
        {
            var counts = new Dictionary<string, int>();
            void Walk(Node n, string owner)
            {
                string own = n switch
                {
                    VineNode => "tower", VineEnemy => "enemy", VinePlayer => "BIT", VineHarvester => "Spire",
                    VineGrid => "grid", ConversionDome => "dome", VineConnection => "link", Ascendant => "ascendant",
                    _ => owner,
                };
                string key = $"{n.GetType().Name} in {own}";
                counts[key] = counts.GetValueOrDefault(key) + 1;
                foreach (var c in n.GetChildren()) Walk(c, own);
            }
            Walk(root, "scene");
            return counts;
        }
    }
}
