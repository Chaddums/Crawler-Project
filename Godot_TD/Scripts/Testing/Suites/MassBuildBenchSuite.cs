using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A mass build (a Swarm Doctrine run placed 473 towers; full perk trees field 250 to 300): a
    /// checkerboard of every kind of tower round the Spire, all at the top level with a branch, and
    /// a field kept full of tough enemies, held at 1x and then at 3x game speed. Measures the
    /// script time a frame (game logic: headless has no rendering), its biggest parts
    /// (FrameProfiler), objects and nodes, and asserts a budget. MASS_TOWERS (default 300) and
    /// MASS_ENEMIES (default 150) set the size. "massbuild" (not in "all").
    /// </summary>
    public class MassBuildBenchSuite : ITestSuite
    {
        public string SuiteName => "massbuild";

        private static readonly (VineNodeType type, string branch)[] Mix =
        {
            (VineNodeType.DamageTower, "railgun"), (VineNodeType.TeslaCoil, "storm"), (VineNodeType.FlakBattery, "shrapnel"),
            (VineNodeType.DamageTower, "minigun"), (VineNodeType.ScatterCannon, "shredder"), (VineNodeType.TeslaCoil, "storm"),
            (VineNodeType.FlakBattery, "skyguard"), (VineNodeType.SlowField, "glue"),
        };

        private static int Env(string name, int fallback)
            => int.TryParse(OS.GetEnvironment(name), out var v) && v > 0 ? v : fallback;

        public async Task Run(TestContext ctx)
        {
            int towers = Env("MASS_TOWERS", 300), enemies = Env("MASS_ENEMIES", 150);
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "massbuild/battle_loads", "no site"); return; }
            gm.CurrentPlanet = site.Planet;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = SpireData.Get("Bruteforge")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, "massbuild/battle_loads", site.Id);
            if (!ok) return;

            var grid = ServiceLocator.Get<VineGrid>();
            var waves = ServiceLocator.Get<VineWaveManager>();
            var pf = ServiceLocator.Get<VinePathfinder>();
            grid.Harvester?.IncreaseMaxHP(1e7f);
            gm.AddResources(1000000);
            waves.PauseAutoStart = true;

            // A checkerboard of the mix outward from the Spire, every kind at the top with a branch
            var exit = grid.ExitPoint;
            int placed = 0;
            var buildSw = Stopwatch.StartNew();
            for (int r = 2; r < 40 && placed < towers; r++)
                for (int dx = -r; dx <= r && placed < towers; dx++)
                    for (int dy = -r; dy <= r && placed < towers; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r || ((dx + dy) & 1) != 0) continue;
                        var c = exit + new Vector2I(dx, dy);
                        if (!grid.CanPlace(c) || pf.WouldBlockAllPaths(c)) continue;
                        var (type, branch) = Mix[placed % Mix.Length];
                        if (AutoPlaceHelper.PlaceAt(grid, c.X, c.Y, type))
                        {
                            grid.GetNode(c)?.ForceUpgrade(3, branch);
                            placed++;
                        }
                    }
            double buildMs = buildSw.Elapsed.TotalMilliseconds;
            await ctx.Wait(0.5f);
            GD.Print($"[MassBuild] {placed} towers placed in {buildMs:F0} ms");

            waves.RequestNextWave();
            await ctx.Wait(0.5f);
            var rng = new RandomNumberGenerator { Seed = 11 };
            int Fill()
            {
                int alive = Roster.Enemies(ctx.Tree).Count(e => e != null && e.IsAlive);
                int added = 0;
                for (int i = 0; i < 400 && alive + added < enemies; i++)
                {
                    var c = new Vector2I(rng.RandiRange(1, grid.Width - 2), rng.RandiRange(1, grid.Height - 2));
                    if ((c - exit).Length() < 18f || !grid.IsWalkable(c)) continue;
                    if (waves.SpawnReinforcement(c, 20000f) != null) added++; // tough: the fight stays on
                }
                return added;
            }
            Fill();
            await ctx.Wait(1f);

            var results = new List<string>();
            double p95At1 = 0;
            foreach (double speed in new[] { 1.0, 3.0 })
            {
                Engine.TimeScale = speed;
                await ctx.Wait(1f);
                var sw = Stopwatch.StartNew();
                var frameSw = Stopwatch.StartNew();
                var times = new List<double>();
                int frames = 0, peakObjects = 0, peakNodes = 0, minEnemies = int.MaxValue;
                float topUp = 0f;
                FrameProfiler.Reset();
                FrameProfiler.Enabled = true;
                while (sw.Elapsed.TotalSeconds < 10)
                {
                    if ((topUp -= (float)frameSw.Elapsed.TotalSeconds) <= 0f) { topUp = 0.25f; Fill(); }
                    frameSw.Restart();
                    await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
                    frames++;
                    times.Add(frameSw.Elapsed.TotalSeconds);
                    peakObjects = Math.Max(peakObjects, (int)Performance.GetMonitor(Performance.Monitor.ObjectCount));
                    peakNodes = Math.Max(peakNodes, (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
                    minEnemies = Math.Min(minEnemies, Roster.Enemies(ctx.Tree).Count(e => e != null && e.IsAlive));
                }
                FrameProfiler.Enabled = false;
                foreach (var (key, ms, calls) in FrameProfiler.Report(frames).Take(14))
                    GD.Print($"[MassBuild] x{speed:0} {key}: {ms:F2} ms a frame ({calls} calls)");
                times.Sort();
                double avg = times.Average() * 1000.0, p95 = times[(int)(times.Count * 0.95)] * 1000.0, worst = times[^1] * 1000.0;
                double fps = frames / sw.Elapsed.TotalSeconds;
                if (speed == 1.0) p95At1 = p95;
                string line = $"x{speed:0}: frame {avg:F2} ms (p95 {p95:F1}, worst {worst:F1}), {fps:F0} fps, {minEnemies}+ enemies, peak {peakObjects} objects, {peakNodes} nodes";
                results.Add(line);
                GD.Print($"[MassBuild] {placed} towers, {line}");
            }
            Engine.TimeScale = 1.0;

            ctx.StartTest();
            ctx.Assert(placed >= towers * 0.9f, "massbuild/scene_built", $"{placed} of {towers} towers");
            bool headless = DisplayServer.GetName() == "headless";
            ctx.Assert(!headless || p95At1 < 16.7, "massbuild/frame_time_budget",
                (headless ? "" : "(not checked with a display: software rendering) ") + $"{placed} towers: {string.Join("; ", results)} (headless: game logic only)");

            foreach (var e in Roster.Enemies(ctx.Tree).ToList())
                if (e != null && e.IsAlive) e.TakeDamage(1e9f);
            gm.AutoResolvePerks = false;
            waves.PauseAutoStart = false;
        }
    }
}
