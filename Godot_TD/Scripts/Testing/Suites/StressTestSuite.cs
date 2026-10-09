using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The reported crash: Send All pressed again later in the run (P1, Relay Station Alpha,
    /// Arcanist, Materials mode). A mixed defence on an unkillable Spire, Send All two seconds
    /// into waves 3 and 7 (and twice in a row on 11), at 4x, until wave 14 clears or the time
    /// runs out. The check is that the process lives; it also records the busiest moment.
    /// </summary>
    public class StressTestSuite : ITestSuite
    {
        public string SuiteName => "stress";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "stress/battle_loads", "no site"); return; }
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = site.Planet;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Arcanist";
            gm.AvailableNodes = SpireData.Get("Arcanist")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true; // the perk overlay pauses the run until someone clicks
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, "stress/battle_loads", site.Id);
            if (!ok) return;

            var grid = ServiceLocator.Get<VineGrid>();
            ServiceLocator.TryGet<VineWaveManager>(out var waves);
            grid.Harvester?.IncreaseMaxHP(1e6f);
            if (grid.Harvester != null && grid.Harvester.CurrentMode == MiningMode.Resources) grid.Harvester.ToggleMode();
            gm.AddResources(20000);
            var cycle = new[] { VineNodeType.DamageTower, VineNodeType.FlakBattery, VineNodeType.TeslaCoil,
                VineNodeType.ScatterCannon, VineNodeType.BuffEmitter, VineNodeType.SlowField, VineNodeType.PushPull };
            int placed = 0, cx = grid.Width / 2, cy = grid.Height / 2;
            for (int r = 2; r < 12 && placed < 28; r++)
                for (int dx = -r; dx <= r && placed < 28; dx++)
                    for (int dy = -r; dy <= r && placed < 28; dy++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        if ((dx + dy) % 2 != 0) continue;
                        if (AutoPlaceHelper.PlaceAt(grid, cx + dx, cy + dy, cycle[placed % cycle.Length])) placed++;
                    }
            GD.Print($"[Stress] placed {placed} towers, Spire in {grid.Harvester?.CurrentMode}");

            Engine.TimeScale = 4.0;
            var sendAt = new HashSet<int> { 3, 7, 11 };
            var done = new HashSet<int>();
            float clock = 0f, sinceStart = 0f;
            int peakEnemies = 0, peakObjects = 0;
            const float LIMIT = 900f;
            waves.RequestNextWave();
            while (clock < LIMIT)
            {
                await ctx.Wait(0.25f);
                clock += 0.25f;
                if (gm.CurrentPhase == GamePhase.Defeat || gm.CurrentPhase == GamePhase.Debrief) break;
                int w = waves.CurrentWave;
                if (gm.CurrentPhase == GamePhase.Build && !waves.WaveActive) { waves.RequestNextWave(); sinceStart = 0f; continue; }
                sinceStart += 0.25f;
                if (sendAt.Contains(w) && !done.Contains(w) && sinceStart >= 0.5f)
                {
                    done.Add(w);
                    GD.Print($"[Stress] Send All during W{w}");
                    waves.SendAllRemaining();
                    if (w == 11) { await ctx.Wait(0.1f); waves.SendAllRemaining(); }
                    done.Add(waves.CurrentWave);
                }
                int enemies = ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).Count;
                peakEnemies = Mathf.Max(peakEnemies, enemies);
                peakObjects = Mathf.Max(peakObjects, (int)Performance.GetMonitor(Performance.Monitor.ObjectCount));
                if (w >= 15 && !waves.WaveActive) break;
            }
            Engine.TimeScale = 1.0;
            gm.AutoResolvePerks = false;
            ctx.StartTest();
            ctx.Assert(waves.CurrentWave >= 12, "stress/send_all_twice_survives",
                $"reached W{waves.CurrentWave} in {clock:F0} s (phase {gm.CurrentPhase}); peak {peakEnemies} enemies, {peakObjects} objects");
        }
    }
}
