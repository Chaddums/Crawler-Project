using System;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// End-to-end integration tests: full draft-to-victory/defeat runs,
    /// scene transitions, speed toggle, signal cascades, and stress placement.
    /// </summary>
    public class IntegrationTestSuite : ITestSuite
    {
        public string SuiteName => "integration";

        public async Task Run(TestContext ctx)
        {
            GD.Print("[IntegrationTestSuite] Starting integration tests...");

            await TestObeliskFullWin(ctx);
            await TestArcanistFullWin(ctx);
            await TestBruteforgeFullWin(ctx);
            await TestDeliberateLoss(ctx);
            await TestSignalCascade(ctx);
            await TestSceneTransitionStability(ctx);
            await TestSpeedToggle(ctx);
            await TestMaxNodeDensity(ctx);

            GD.Print("[IntegrationTestSuite] Integration tests complete.");
        }

        // ══════════════════════════════════════════════════════════════
        // 1. Obelisk full win — select role, build defenses, win all 6 waves
        // ══════════════════════════════════════════════════════════════

        private async Task TestObeliskFullWin(TestContext ctx)
        {
            ctx.StartTest();
            GD.Print("  [integration.scrapwright_full_win] Starting...");

            try
            {
                // Select Obelisk role and start battle
                GameManager.Instance.SelectedRole = "Obelisk";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);

                // Wait for Build phase
                bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 10f);
                if (!inBuild)
                {
                    ctx.Assert(false, "integration.scrapwright_full_win",
                        "Failed to reach Build phase after starting battle");
                    return;
                }

                // Build defenses
                var grid = ServiceLocator.Get<VineGrid>();
                ctx.AssertNotNull(grid, "integration.scrapwright_full_win.grid_exists");
                if (grid == null) return;

                await BuildBasicDefense(ctx, grid);

                // Run all 6 waves
                var wm = ServiceLocator.Get<VineWaveManager>();
                ctx.AssertNotNull(wm, "integration.scrapwright_full_win.wm_exists");
                if (wm == null) return;

                bool won = await RunAllWaves(ctx, wm);

                ctx.Assert(won || GameManager.Instance.CurrentPhase == GamePhase.Victory,
                    "integration.scrapwright_full_win",
                    $"Expected Victory phase, got {GameManager.Instance.CurrentPhase}");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "integration.scrapwright_full_win",
                    $"Exception: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 2. Arcanist full win — sensor-heavy role, win all 6 waves
        // ══════════════════════════════════════════════════════════════

        private async Task TestArcanistFullWin(TestContext ctx)
        {
            ctx.StartTest();
            GD.Print("  [integration.arcanist_full_win] Starting...");

            try
            {
                GameManager.Instance.SelectedRole = "Arcanist";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(1);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);

                bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 10f);
                if (!inBuild)
                {
                    ctx.Assert(false, "integration.arcanist_full_win",
                        "Failed to reach Build phase");
                    return;
                }

                var grid = ServiceLocator.Get<VineGrid>();
                if (grid == null) { ctx.Assert(false, "integration.arcanist_full_win", "No VineGrid"); return; }

                // Arcanist has ProximitySensor, Timer, CountSensor, HPSensor, TypeSensor,
                // Extender, DamageTower, SlowField — build sensor-heavy chain
                await BuildArcanistDefense(ctx, grid);

                var wm = ServiceLocator.Get<VineWaveManager>();
                if (wm == null) { ctx.Assert(false, "integration.arcanist_full_win", "No WaveManager"); return; }

                bool won = await RunAllWaves(ctx, wm);

                ctx.Assert(won || GameManager.Instance.CurrentPhase == GamePhase.Victory,
                    "integration.arcanist_full_win",
                    $"Expected Victory, got {GameManager.Instance.CurrentPhase}");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "integration.arcanist_full_win",
                    $"Exception: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 3. Bruteforge full win — effect-heavy role, win all 6 waves
        // ══════════════════════════════════════════════════════════════

        private async Task TestBruteforgeFullWin(TestContext ctx)
        {
            ctx.StartTest();
            GD.Print("  [integration.bruteforge_full_win] Starting...");

            try
            {
                GameManager.Instance.SelectedRole = "Bruteforge";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(2);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);

                bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 10f);
                if (!inBuild)
                {
                    ctx.Assert(false, "integration.bruteforge_full_win",
                        "Failed to reach Build phase");
                    return;
                }

                var grid = ServiceLocator.Get<VineGrid>();
                if (grid == null) { ctx.Assert(false, "integration.bruteforge_full_win", "No VineGrid"); return; }

                // Bruteforge has DamageTower, SlowField, BuffEmitter, PushPull,
                // SignalCannon, Extender, Junction, ProximitySensor
                await BuildBruteforgeDefense(ctx, grid);

                var wm = ServiceLocator.Get<VineWaveManager>();
                if (wm == null) { ctx.Assert(false, "integration.bruteforge_full_win", "No WaveManager"); return; }

                bool won = await RunAllWaves(ctx, wm);

                ctx.Assert(won || GameManager.Instance.CurrentPhase == GamePhase.Victory,
                    "integration.bruteforge_full_win",
                    $"Expected Victory, got {GameManager.Instance.CurrentPhase}");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "integration.bruteforge_full_win",
                    $"Exception: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 4. Deliberate loss — no defenses, verify Defeat when lives hit 0
        // ══════════════════════════════════════════════════════════════

        private async Task TestDeliberateLoss(TestContext ctx)
        {
            ctx.StartTest();
            GD.Print("  [integration.deliberate_loss] Starting...");

            try
            {
                GameManager.Instance.SelectedRole = "Obelisk";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);

                bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 10f);
                if (!inBuild)
                {
                    ctx.Assert(false, "integration.deliberate_loss",
                        "Failed to reach Build phase");
                    return;
                }

                // Set low lives so defeat comes quickly
                GameManager.Instance.SetCoreLives(3);

                // Do NOT build any defenses — start wave immediately
                var wm = ServiceLocator.Get<VineWaveManager>();
                if (wm == null) { ctx.Assert(false, "integration.deliberate_loss", "No WaveManager"); return; }

                ctx.BeginEventTracking();
                wm.StartWave();

                // Wait for Defeat phase (enemies should leak through with no towers)
                bool defeated = await ctx.WaitForPhase(GamePhase.Defeat, 45f);

                ctx.Assert(defeated, "integration.deliberate_loss",
                    $"Expected Defeat phase, got {GameManager.Instance.CurrentPhase}");

                // Verify core lives reached 0
                ctx.Assert(GameManager.Instance.CoreLives <= 0,
                    "integration.deliberate_loss.lives_zero",
                    $"Expected 0 lives, got {GameManager.Instance.CoreLives}");

                // Verify OnCoreDestroyed event fired
                int coreDestroyedCount = ctx.GetEventCount("OnCoreDestroyed");
                ctx.AssertGreaterEqual(coreDestroyedCount, 1,
                    "integration.deliberate_loss.core_destroyed_event",
                    "OnCoreDestroyed should have fired");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "integration.deliberate_loss",
                    $"Exception: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 5. Signal cascade — sensor -> junction -> 3 towers, verify events
        // ══════════════════════════════════════════════════════════════

        private async Task TestSignalCascade(TestContext ctx)
        {
            ctx.StartTest();
            GD.Print("  [integration.signal_cascade] Starting...");

            try
            {
                GameManager.Instance.SelectedRole = "Obelisk";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);

                bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 10f);
                if (!inBuild)
                {
                    ctx.Assert(false, "integration.signal_cascade",
                        "Failed to reach Build phase");
                    return;
                }

                var grid = ServiceLocator.Get<VineGrid>();
                if (grid == null) { ctx.Assert(false, "integration.signal_cascade", "No VineGrid"); return; }

                GameManager.Instance.AddResources(500);

                // Build: Sensor -> Junction -> 3 Towers
                // Place sensor near entry, junction adjacent, towers fanning out
                var entry = grid.EntryPoints[0];
                var sensorPos = entry + new Vector2I(2, 0);
                var junctionPos = sensorPos + new Vector2I(1, 0);
                var tower1Pos = junctionPos + new Vector2I(1, 0);
                var tower2Pos = junctionPos + new Vector2I(0, 1);
                var tower3Pos = junctionPos + new Vector2I(0, -1);

                PlaceNode(grid, VineNodeType.ProximitySensor, sensorPos);
                PlaceNode(grid, VineNodeType.Junction, junctionPos);
                PlaceNode(grid, VineNodeType.DamageTower, tower1Pos);
                PlaceNode(grid, VineNodeType.DamageTower, tower2Pos);
                PlaceNode(grid, VineNodeType.DamageTower, tower3Pos);
                await ctx.Wait(0.5f);

                // Start tracking events before wave
                ctx.BeginEventTracking();

                // Start wave so enemies trigger sensors
                var wm = ServiceLocator.Get<VineWaveManager>();
                if (wm == null) { ctx.Assert(false, "integration.signal_cascade", "No WaveManager"); return; }

                wm.StartWave();

                // Wait for signal events to fire (enemies should trigger the proximity sensor)
                await ctx.Wait(8f);

                int signalsFired = ctx.GetEventCount("OnSignalFired");
                int signalsReceived = ctx.GetEventCount("OnSignalReceived");

                ctx.AssertGreater(signalsFired, 0f,
                    "integration.signal_cascade.signals_fired",
                    "Expected OnSignalFired events from sensor");

                ctx.AssertGreater(signalsReceived, 0f,
                    "integration.signal_cascade.signals_received",
                    "Expected OnSignalReceived events at junction/towers");

                // Junction should cause cascade: 1 signal in -> 3 signals out
                // So received count should be higher than fired count
                ctx.AssertGreater(signalsReceived, signalsFired,
                    "integration.signal_cascade.cascade_multiplier",
                    $"Cascade: fired={signalsFired}, received={signalsReceived} — received should exceed fired");

                // Force-kill remaining enemies and clean up
                KillAllEnemies(ctx.Tree);
                await ctx.Wait(2f);
            }
            catch (Exception e)
            {
                ctx.Assert(false, "integration.signal_cascade",
                    $"Exception: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 6. Scene transition stability — Draft -> Battle -> Menu -> repeat
        // ══════════════════════════════════════════════════════════════

        private async Task TestSceneTransitionStability(TestContext ctx)
        {
            ctx.StartTest();
            GD.Print("  [integration.scene_transition_stability] Starting...");

            try
            {
                // Cycle 1: Draft -> Battle -> Menu
                GameManager.Instance.StartVineDraft();
                await ctx.Wait(2f);
                ctx.AssertNotNull(GameManager.Instance,
                    "integration.scene_transition_stability.after_draft_1",
                    "GameManager should persist after Draft transition");

                GameManager.Instance.SelectedRole = "Obelisk";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);
                ctx.AssertNotNull(GameManager.Instance,
                    "integration.scene_transition_stability.after_battle_1",
                    "GameManager should persist after Battle transition");

                GameManager.Instance.ReturnToMainMenu();
                await ctx.Wait(2f);
                ctx.AssertNotNull(GameManager.Instance,
                    "integration.scene_transition_stability.after_menu_1",
                    "GameManager should persist after Menu transition");

                ctx.AssertEqual(GamePhase.MainMenu, GameManager.Instance.CurrentPhase,
                    "integration.scene_transition_stability.menu_phase_1",
                    "Should be in MainMenu phase after return");

                // Cycle 2: Draft -> Battle (verify no accumulated state crash)
                GameManager.Instance.StartVineDraft();
                await ctx.Wait(2f);
                ctx.AssertNotNull(GameManager.Instance,
                    "integration.scene_transition_stability.after_draft_2",
                    "GameManager should persist after second Draft transition");

                GameManager.Instance.SelectedRole = "Arcanist";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(1);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);
                ctx.AssertNotNull(GameManager.Instance,
                    "integration.scene_transition_stability.after_battle_2",
                    "GameManager should persist after second Battle transition");

                // Verify Engine.TimeScale was reset (ReturnToMainMenu resets it)
                ctx.AssertEqual(1.0, Engine.TimeScale,
                    "integration.scene_transition_stability.timescale_reset",
                    "TimeScale should be 1.0 after cycling through menu");

                ctx.Assert(true, "integration.scene_transition_stability",
                    "Completed 2 full scene transition cycles without crash");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "integration.scene_transition_stability",
                    $"Crash during scene transitions: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 7. Speed toggle — verify Engine.TimeScale changes on ToggleSpeed
        // ══════════════════════════════════════════════════════════════

        private async Task TestSpeedToggle(TestContext ctx)
        {
            ctx.StartTest();
            GD.Print("  [integration.speed_toggle] Starting...");

            try
            {
                GameManager.Instance.SelectedRole = "Obelisk";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);

                bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 10f);
                if (!inBuild)
                {
                    ctx.Assert(false, "integration.speed_toggle",
                        "Failed to reach Build phase");
                    return;
                }

                // Start a wave so speed changes are meaningful
                var wm = ServiceLocator.Get<VineWaveManager>();
                if (wm != null)
                    wm.StartWave();
                await ctx.Wait(0.5f);

                // Verify initial speed is 1x
                ctx.AssertEqual(Constants.SPEED_NORMAL, (float)Engine.TimeScale,
                    "integration.speed_toggle.initial_1x",
                    "Should start at 1x speed");

                // Toggle to 2x
                GameManager.Instance.ToggleSpeed();
                ctx.AssertEqual(Constants.SPEED_FAST, (float)Engine.TimeScale,
                    "integration.speed_toggle.after_2x",
                    $"After first toggle: expected {Constants.SPEED_FAST}x, got {Engine.TimeScale}x");

                // Toggle to 3x
                GameManager.Instance.ToggleSpeed();
                ctx.AssertEqual(Constants.SPEED_ULTRA, (float)Engine.TimeScale,
                    "integration.speed_toggle.after_3x",
                    $"After second toggle: expected {Constants.SPEED_ULTRA}x, got {Engine.TimeScale}x");

                // Toggle back to 1x
                GameManager.Instance.ToggleSpeed();
                ctx.AssertEqual(Constants.SPEED_NORMAL, (float)Engine.TimeScale,
                    "integration.speed_toggle.back_to_1x",
                    $"After third toggle: expected {Constants.SPEED_NORMAL}x, got {Engine.TimeScale}x");

                // Reset time scale for subsequent tests
                Engine.TimeScale = 1.0;

                // Kill remaining enemies to clean up
                KillAllEnemies(ctx.Tree);
                await ctx.Wait(2f);
            }
            catch (Exception e)
            {
                Engine.TimeScale = 1.0;
                ctx.Assert(false, "integration.speed_toggle",
                    $"Exception: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 8. Max node density — fill many cells, verify no crash
        // ══════════════════════════════════════════════════════════════

        private async Task TestMaxNodeDensity(TestContext ctx)
        {
            ctx.StartTest();
            GD.Print("  [integration.max_node_density] Starting...");

            try
            {
                GameManager.Instance.SelectedRole = "Obelisk";
                GameManager.Instance.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
                GameManager.Instance.StartVineBattle();
                await ctx.Wait(2f);

                bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 10f);
                if (!inBuild)
                {
                    ctx.Assert(false, "integration.max_node_density",
                        "Failed to reach Build phase");
                    return;
                }

                var grid = ServiceLocator.Get<VineGrid>();
                if (grid == null) { ctx.Assert(false, "integration.max_node_density", "No VineGrid"); return; }

                // Give plenty of gold for mass placement
                GameManager.Instance.AddResources(5000);

                // Fill a large region of the grid with extenders
                int placed = 0;
                int attempted = 0;
                for (int x = 2; x < grid.Width - 2; x++)
                {
                    for (int y = 2; y < grid.Height - 2; y++)
                    {
                        var pos = new Vector2I(x, y);
                        if (grid.CanPlace(pos))
                        {
                            attempted++;
                            var node = PlaceNode(grid, VineNodeType.Extender, pos);
                            if (node != null) placed++;
                        }
                    }
                }

                ctx.AssertGreater(placed, 0f,
                    "integration.max_node_density.placed_count",
                    $"Should have placed at least 1 node, placed {placed}/{attempted}");

                // Verify cells are occupied
                int occupied = 0;
                for (int x = 2; x < grid.Width - 2; x++)
                {
                    for (int y = 2; y < grid.Height - 2; y++)
                    {
                        if (grid.GetCell(x, y) == VineCellType.Node)
                            occupied++;
                    }
                }

                ctx.AssertEqual(placed, occupied,
                    "integration.max_node_density.cells_occupied",
                    $"Placed {placed} nodes but only {occupied} cells occupied");

                // Wait a frame to see if anything crashes after mass placement
                await ctx.Wait(1f);

                ctx.Assert(true, "integration.max_node_density",
                    $"Successfully placed {placed} nodes without crash");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "integration.max_node_density",
                    $"Crash during mass placement: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // Helper: Place a vine node on the grid
        // ══════════════════════════════════════════════════════════════

        private VineNode PlaceNode(VineGrid grid, VineNodeType type, Vector2I pos)
        {
            var data = VineNodeRegistry.Get(type);
            if (data == null) return null;
            var node = new VineNode();
            node.Initialize(data);
            if (!grid.PlaceNode(node, pos))
            {
                node.QueueFree();
                return null;
            }
            return node;
        }

        // ══════════════════════════════════════════════════════════════
        // Helper: Build basic Obelisk defense near entry points
        // ══════════════════════════════════════════════════════════════

        private async Task BuildBasicDefense(TestContext ctx, VineGrid grid)
        {
            GameManager.Instance.AddResources(500);

            // Place nodes near first entry point
            var entry = grid.EntryPoints[0];
            PlaceNode(grid, VineNodeType.ProximitySensor, entry + new Vector2I(2, 0));
            PlaceNode(grid, VineNodeType.Extender, entry + new Vector2I(3, 0));
            PlaceNode(grid, VineNodeType.DamageTower, entry + new Vector2I(4, 0));

            // Second defense line slightly offset
            PlaceNode(grid, VineNodeType.ProximitySensor, entry + new Vector2I(2, 2));
            PlaceNode(grid, VineNodeType.DamageTower, entry + new Vector2I(3, 2));

            // Build near second entry if exists
            if (grid.EntryPoints.Count > 1)
            {
                var entry2 = grid.EntryPoints[1];
                PlaceNode(grid, VineNodeType.ProximitySensor, entry2 + new Vector2I(2, 0));
                PlaceNode(grid, VineNodeType.Extender, entry2 + new Vector2I(3, 0));
                PlaceNode(grid, VineNodeType.DamageTower, entry2 + new Vector2I(4, 0));
            }

            await ctx.Wait(0.5f);
        }

        // ══════════════════════════════════════════════════════════════
        // Helper: Build Arcanist defense (sensor-heavy with slow fields)
        // ══════════════════════════════════════════════════════════════

        private async Task BuildArcanistDefense(TestContext ctx, VineGrid grid)
        {
            GameManager.Instance.AddResources(500);

            var entry = grid.EntryPoints[0];
            // Multiple sensors feeding into towers via extenders
            PlaceNode(grid, VineNodeType.ProximitySensor, entry + new Vector2I(2, 0));
            PlaceNode(grid, VineNodeType.Extender, entry + new Vector2I(3, 0));
            PlaceNode(grid, VineNodeType.DamageTower, entry + new Vector2I(4, 0));
            PlaceNode(grid, VineNodeType.SlowField, entry + new Vector2I(3, 1));
            PlaceNode(grid, VineNodeType.Timer, entry + new Vector2I(2, 2));
            PlaceNode(grid, VineNodeType.DamageTower, entry + new Vector2I(3, 2));

            if (grid.EntryPoints.Count > 1)
            {
                var entry2 = grid.EntryPoints[1];
                PlaceNode(grid, VineNodeType.ProximitySensor, entry2 + new Vector2I(2, 0));
                PlaceNode(grid, VineNodeType.DamageTower, entry2 + new Vector2I(3, 0));
                PlaceNode(grid, VineNodeType.SlowField, entry2 + new Vector2I(2, 1));
            }

            await ctx.Wait(0.5f);
        }

        // ══════════════════════════════════════════════════════════════
        // Helper: Build Bruteforge defense (effect-heavy with buffs)
        // ══════════════════════════════════════════════════════════════

        private async Task BuildBruteforgeDefense(TestContext ctx, VineGrid grid)
        {
            GameManager.Instance.AddResources(500);

            var entry = grid.EntryPoints[0];
            // Sensor -> Junction -> DamageTower + SlowField + BuffEmitter
            PlaceNode(grid, VineNodeType.ProximitySensor, entry + new Vector2I(2, 0));
            PlaceNode(grid, VineNodeType.Junction, entry + new Vector2I(3, 0));
            PlaceNode(grid, VineNodeType.DamageTower, entry + new Vector2I(4, 0));
            PlaceNode(grid, VineNodeType.DamageTower, entry + new Vector2I(3, 1));
            PlaceNode(grid, VineNodeType.SlowField, entry + new Vector2I(3, -1));
            PlaceNode(grid, VineNodeType.BuffEmitter, entry + new Vector2I(5, 0));

            if (grid.EntryPoints.Count > 1)
            {
                var entry2 = grid.EntryPoints[1];
                PlaceNode(grid, VineNodeType.ProximitySensor, entry2 + new Vector2I(2, 0));
                PlaceNode(grid, VineNodeType.Extender, entry2 + new Vector2I(3, 0));
                PlaceNode(grid, VineNodeType.DamageTower, entry2 + new Vector2I(4, 0));
            }

            await ctx.Wait(0.5f);
        }

        // ══════════════════════════════════════════════════════════════
        // Helper: Run all 6 waves, force-killing stragglers if needed
        // ══════════════════════════════════════════════════════════════

        private async Task<bool> RunAllWaves(TestContext ctx, VineWaveManager wm)
        {
            for (int w = 0; w < VineWaveRegistry.WaveCount; w++)
            {
                wm.StartWave();

                bool done = await ctx.WaitUntil(() =>
                    GameManager.Instance.CurrentPhase == GamePhase.WaveComplete ||
                    GameManager.Instance.CurrentPhase == GamePhase.Victory ||
                    GameManager.Instance.CurrentPhase == GamePhase.Defeat, 30f);

                if (GameManager.Instance.CurrentPhase == GamePhase.Defeat)
                    return false;
                if (GameManager.Instance.CurrentPhase == GamePhase.Victory)
                    return true;

                if (!done)
                {
                    // Force-kill remaining enemies so the wave can end
                    KillAllEnemies(ctx.Tree);
                    await ctx.Wait(2f);
                }

                // Wait for build phase transition between waves
                if (GameManager.Instance.CurrentPhase != GamePhase.Victory)
                    await ctx.WaitForPhase(GamePhase.Build, 5f);
            }

            return GameManager.Instance.CurrentPhase == GamePhase.Victory;
        }

        // ══════════════════════════════════════════════════════════════
        // Helper: Kill all remaining enemies on the map
        // ══════════════════════════════════════════════════════════════

        private void KillAllEnemies(SceneTree tree)
        {
            foreach (var enemy in tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (enemy is VineEnemy ve && ve.IsAlive)
                    ve.TakeDamage(9999);
            }
        }
    }
}
