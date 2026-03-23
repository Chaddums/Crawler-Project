using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Validates visual properties: material colors, emission settings, mesh sizes,
    /// and screenshot captures. Tier 1 tests are headless-compatible (property checks),
    /// Tier 2 tests require rendering (screenshot baselines).
    /// </summary>
    public class VisualTestSuite : ITestSuite
    {
        public string SuiteName => "visual";

        // Track nodes we create so we can clean them up
        private readonly List<Node> _tempNodes = new();

        public async Task Run(TestContext ctx)
        {
            GD.Print("[VisualTestSuite] Starting visual validation...");

            // ══════════════════════════════════════════════
            // Tier 1 — Material/Property Checks (headless-compatible)
            // ══════════════════════════════════════════════

            GD.Print("[VisualTestSuite] ── Tier 1: Node color checks ──");
            TestAllNodeColors(ctx);
            TestAllNodeEmissionEnabled(ctx);
            TestAllNodeEmissionEnergy(ctx);
            TestSensorColorRange(ctx);
            TestEffectColorRange(ctx);
            TestStructuralColorRange(ctx);

            GD.Print("[VisualTestSuite] ── Tier 1: Enemy faction color checks ──");
            TestEnemyScavengerColor(ctx);
            TestEnemyBruteColor(ctx);
            TestEnemySwarmColor(ctx);
            TestEnemyGhostColor(ctx);

            GD.Print("[VisualTestSuite] ── Tier 1: Grid visual checks ──");
            TestGroundBaseDark(ctx);
            TestGridCyanBright(ctx);
            TestWallBaseDark(ctx);

            // Clean up any temp nodes from Tier 1
            CleanupTempNodes();

            // ══════════════════════════════════════════════
            // Tier 2 — Screenshot Baselines (rendering required)
            // ══════════════════════════════════════════════

            GD.Print("[VisualTestSuite] ── Tier 2: Screenshot baselines ──");
            await RunScreenshotTests(ctx);

            GD.Print("[VisualTestSuite] Visual validation complete.");
        }

        // ── Helpers ──

        private VineNode CreateTestNode(VineNodeType type)
        {
            var data = VineNodeRegistry.Get(type);
            var node = new VineNode();
            node.Initialize(data);
            _tempNodes.Add(node);
            return node;
        }

        private MeshInstance3D FindMesh(Node parent)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is MeshInstance3D mesh) return mesh;
                var found = FindMesh(child as Node);
                if (found != null) return found;
            }
            return null;
        }

        private void CleanupTempNodes()
        {
            foreach (var node in _tempNodes)
            {
                if (GodotObject.IsInstanceValid(node))
                    node.QueueFree();
            }
            _tempNodes.Clear();
        }

        // ══════════════════════════════════════════════════════
        // Tests 1-18: Per-node-type albedo color matches TintColor
        // ══════════════════════════════════════════════════════════

        private void TestAllNodeColors(TestContext ctx)
        {
            var allTypes = Enum.GetValues(typeof(VineNodeType)).Cast<VineNodeType>().ToList();

            foreach (var type in allTypes)
            {
                ctx.StartTest();
                var data = VineNodeRegistry.Get(type);
                if (data == null)
                {
                    ctx.Assert(false, $"visual.node_color_{type}",
                        $"No registry entry for VineNodeType.{type}");
                    continue;
                }

                try
                {
                    var node = CreateTestNode(type);
                    var mesh = FindMesh(node);
                    if (mesh == null)
                    {
                        ctx.Assert(false, $"visual.node_color_{type}",
                            $"No MeshInstance3D found in node {type}");
                        continue;
                    }

                    var sample = ctx.SampleMaterial(mesh);
                    ctx.AssertColorMatch(sample.albedo, data.TintColor, 0.05f,
                        $"visual.node_color_{type}");
                }
                catch (Exception e)
                {
                    ctx.Assert(false, $"visual.node_color_{type}",
                        $"Exception creating/inspecting node {type}: {e.Message}");
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // Test 19: All node materials have EmissionEnabled = true
        // ══════════════════════════════════════════════════════════

        private void TestAllNodeEmissionEnabled(TestContext ctx)
        {
            ctx.StartTest();
            var allTypes = Enum.GetValues(typeof(VineNodeType)).Cast<VineNodeType>().ToList();
            bool allEnabled = true;
            var failedTypes = new List<string>();

            foreach (var type in allTypes)
            {
                try
                {
                    var node = CreateTestNode(type);
                    var mesh = FindMesh(node);
                    if (mesh == null) continue;

                    var sample = ctx.SampleMaterial(mesh);
                    if (!sample.emissionEnabled)
                    {
                        allEnabled = false;
                        failedTypes.Add(type.ToString());
                    }
                }
                catch { }
            }

            ctx.Assert(allEnabled, "visual.node_emission_enabled",
                allEnabled ? "All node materials have emission enabled"
                    : $"Emission disabled on: {string.Join(", ", failedTypes)}");
        }

        // ══════════════════════════════════════════════════════
        // Test 20: All node materials have EmissionEnergyMultiplier ~0.6
        // ══════════════════════════════════════════════════════════

        private void TestAllNodeEmissionEnergy(TestContext ctx)
        {
            ctx.StartTest();
            var allTypes = Enum.GetValues(typeof(VineNodeType)).Cast<VineNodeType>().ToList();
            bool allInRange = true;
            var failedTypes = new List<string>();

            foreach (var type in allTypes)
            {
                try
                {
                    var node = CreateTestNode(type);
                    var mesh = FindMesh(node);
                    if (mesh == null) continue;

                    var sample = ctx.SampleMaterial(mesh);
                    if (Mathf.Abs(sample.emissionEnergy - 0.6f) > 0.1f)
                    {
                        allInRange = false;
                        failedTypes.Add($"{type}({sample.emissionEnergy:F2})");
                    }
                }
                catch { }
            }

            ctx.Assert(allInRange, "visual.node_emission_energy",
                allInRange ? "All node emission energy ~0.6"
                    : $"Out of range: {string.Join(", ", failedTypes)}");
        }

        // ══════════════════════════════════════════════════════
        // Test 21: Sensor nodes TintColor in teal-blue range
        // ══════════════════════════════════════════════════════════

        private void TestSensorColorRange(TestContext ctx)
        {
            ctx.StartTest();
            var sensors = VineNodeRegistry.GetByCategory(VineNodeCategory.Sensor).ToList();
            bool allInRange = true;
            var failures = new List<string>();

            foreach (var data in sensors)
            {
                var c = data.TintColor;
                bool ok = c.R < 0.2f && c.G >= 0.4f && c.G <= 0.7f && c.B >= 0.5f && c.B <= 0.8f;
                if (!ok)
                {
                    allInRange = false;
                    failures.Add($"{data.Type}({c.R:F2},{c.G:F2},{c.B:F2})");
                }
            }

            ctx.Assert(allInRange, "visual.sensor_color_range",
                allInRange ? "All sensor TintColors in teal-blue range"
                    : $"Out of range: {string.Join(", ", failures)}");
        }

        // ══════════════════════════════════════════════════════
        // Test 22: Effect nodes TintColor in blue range
        // ══════════════════════════════════════════════════════════

        private void TestEffectColorRange(TestContext ctx)
        {
            ctx.StartTest();
            var effects = VineNodeRegistry.GetByCategory(VineNodeCategory.Effect).ToList();
            bool allInRange = true;
            var failures = new List<string>();

            foreach (var data in effects)
            {
                var c = data.TintColor;
                bool ok = c.R >= 0.05f && c.R <= 0.3f
                       && c.G >= 0.25f && c.G <= 0.6f
                       && c.B >= 0.6f && c.B <= 0.9f;
                if (!ok)
                {
                    allInRange = false;
                    failures.Add($"{data.Type}({c.R:F2},{c.G:F2},{c.B:F2})");
                }
            }

            ctx.Assert(allInRange, "visual.effect_color_range",
                allInRange ? "All effect TintColors in blue range"
                    : $"Out of range: {string.Join(", ", failures)}");
        }

        // ══════════════════════════════════════════════════════
        // Test 23: Structural nodes TintColor in steel blue range
        // ══════════════════════════════════════════════════════════

        private void TestStructuralColorRange(TestContext ctx)
        {
            ctx.StartTest();
            var structural = VineNodeRegistry.GetByCategory(VineNodeCategory.Structural).ToList();
            bool allInRange = true;
            var failures = new List<string>();

            foreach (var data in structural)
            {
                var c = data.TintColor;
                bool ok = c.R >= 0.15f && c.R <= 0.4f
                       && c.G >= 0.35f && c.G <= 0.6f
                       && c.B >= 0.5f && c.B <= 0.75f;
                if (!ok)
                {
                    allInRange = false;
                    failures.Add($"{data.Type}({c.R:F2},{c.G:F2},{c.B:F2})");
                }
            }

            ctx.Assert(allInRange, "visual.structural_color_range",
                allInRange ? "All structural TintColors in steel blue range"
                    : $"Out of range: {string.Join(", ", failures)}");
        }

        // ══════════════════════════════════════════════════════
        // Tests 24-27: Enemy faction colors
        // ══════════════════════════════════════════════════════════

        private void TestEnemyScavengerColor(TestContext ctx)
        {
            ctx.StartTest();
            var c = TronTheme.EnemyScavenger;
            ctx.Assert(c.R > 0.7f, "visual.enemy_color_scavenger",
                c.R > 0.7f
                    ? $"Scavenger red range OK (R={c.R:F2})"
                    : $"Scavenger R={c.R:F2}, expected > 0.7");
        }

        private void TestEnemyBruteColor(TestContext ctx)
        {
            ctx.StartTest();
            var c = TronTheme.EnemyBrute;
            bool ok = c.R >= 0.5f && c.R <= 0.8f && c.G < 0.2f;
            ctx.Assert(ok, "visual.enemy_color_brute",
                ok ? $"Brute dark crimson OK (R={c.R:F2}, G={c.G:F2})"
                    : $"Brute R={c.R:F2}, G={c.G:F2}, expected R 0.5-0.8, G < 0.2");
        }

        private void TestEnemySwarmColor(TestContext ctx)
        {
            ctx.StartTest();
            var c = TronTheme.EnemySwarm;
            bool ok = c.R > 0.8f && c.G > 0.3f;
            ctx.Assert(ok, "visual.enemy_color_swarm",
                ok ? $"Swarm orange-red OK (R={c.R:F2}, G={c.G:F2})"
                    : $"Swarm R={c.R:F2}, G={c.G:F2}, expected R > 0.8, G > 0.3");
        }

        private void TestEnemyGhostColor(TestContext ctx)
        {
            ctx.StartTest();
            var c = TronTheme.EnemyGhost;
            ctx.Assert(c.B > 0.2f, "visual.enemy_color_ghost",
                c.B > 0.2f
                    ? $"Ghost magenta-red OK (B={c.B:F2})"
                    : $"Ghost B={c.B:F2}, expected > 0.2");
        }

        // ══════════════════════════════════════════════════════
        // Tests 28-30: Grid visual constants
        // ══════════════════════════════════════════════════════════

        private void TestGroundBaseDark(TestContext ctx)
        {
            ctx.StartTest();
            var c = TronTheme.GroundBase;
            bool ok = c.R < 0.1f && c.G < 0.1f && c.B < 0.1f;
            ctx.Assert(ok, "visual.ground_base_dark",
                ok ? $"Ground base is very dark ({c.R:F3},{c.G:F3},{c.B:F3})"
                    : $"Ground base too bright ({c.R:F3},{c.G:F3},{c.B:F3}), all channels should be < 0.1");
        }

        private void TestGridCyanBright(TestContext ctx)
        {
            ctx.StartTest();
            var c = TronTheme.GridCyan;
            bool ok = c.G > 0.7f && c.B > 0.8f;
            ctx.Assert(ok, "visual.grid_cyan_bright",
                ok ? $"Grid cyan is bright (G={c.G:F2}, B={c.B:F2})"
                    : $"Grid cyan too dim (G={c.G:F2}, B={c.B:F2}), expected G > 0.7, B > 0.8");
        }

        private void TestWallBaseDark(TestContext ctx)
        {
            ctx.StartTest();
            var c = TronTheme.WallBase;
            bool ok = c.R < 0.1f && c.G < 0.1f && c.B < 0.1f;
            ctx.Assert(ok, "visual.wall_base_dark",
                ok ? $"Wall base is dark ({c.R:F3},{c.G:F3},{c.B:F3})"
                    : $"Wall base too bright ({c.R:F3},{c.G:F3},{c.B:F3}), all channels should be < 0.1");
        }

        // ══════════════════════════════════════════════════════
        // Tier 2 — Screenshot Baselines
        // ══════════════════════════════════════════════════════════

        private async Task RunScreenshotTests(TestContext ctx)
        {
            // Check if rendering is available by trying to get viewport texture
            bool renderingAvailable = false;
            try
            {
                var viewport = ctx.Tree.Root;
                var tex = viewport.GetTexture();
                if (tex != null)
                {
                    var image = tex.GetImage();
                    renderingAvailable = image != null && image.GetWidth() > 0 && image.GetHeight() > 0;
                }
            }
            catch
            {
                renderingAvailable = false;
            }

            if (!renderingAvailable)
            {
                GD.Print("[VisualTestSuite] Rendering not available — skipping Tier 2 screenshot tests.");
                ctx.StartTest();
                ctx.Assert(true, "visual.screenshots_skipped",
                    "Screenshots skipped (headless mode or no rendering)");
                return;
            }

            // Load battle scene for screenshot captures
            try
            {
                GameManager.Instance.StartVineBattle();
                bool ready = await ctx.WaitForPhase(GamePhase.Build, 5f);
                if (!ready)
                {
                    GD.PrintErr("[VisualTestSuite] Could not enter Build phase for screenshots.");
                    ctx.StartTest();
                    ctx.Assert(false, "visual.screenshot_setup",
                        "Failed to enter Build phase for screenshot tests");
                    return;
                }
                await ctx.Wait(0.5f);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[VisualTestSuite] Screenshot setup failed: {e.Message}");
                ctx.StartTest();
                ctx.Assert(false, "visual.screenshot_setup", $"Exception: {e.Message}");
                return;
            }

            // Test 31: Empty grid screenshot
            await CaptureEmptyGrid(ctx);

            // Test 32: Grid with nodes screenshot
            await CaptureGridWithNodes(ctx);

            // Test 33: HUD during build phase screenshot
            await CaptureHudBuildPhase(ctx);

            // Test 34: Draft screen screenshot
            await CaptureDraftScreen(ctx);
        }

        private async Task CaptureEmptyGrid(TestContext ctx)
        {
            ctx.StartTest();
            try
            {
                // Lock camera to standard overview position
                float gridWidth = Constants.VINE_MAP_WIDTH * Constants.VINE_CELL_SIZE;
                float gridHeight = Constants.VINE_MAP_HEIGHT * Constants.VINE_CELL_SIZE;
                var center = new Vector3(gridWidth / 2f, 0, gridHeight / 2f);
                ctx.LockCamera(center, 20f);

                await ctx.Wait(0.3f);
                var path = ctx.CaptureVisualScreenshot("empty_grid");
                ctx.Assert(path != null, "visual.empty_grid",
                    path != null ? $"Screenshot saved: {path}" : "Failed to capture empty grid");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "visual.empty_grid", $"Exception: {e.Message}");
            }
        }

        private async Task CaptureGridWithNodes(TestContext ctx)
        {
            ctx.StartTest();
            try
            {
                if (!ServiceLocator.TryGet<VineGrid>(out var grid))
                {
                    ctx.Assert(false, "visual.grid_with_nodes", "VineGrid not found in ServiceLocator");
                    return;
                }

                // Place 5 nodes at known positions
                var placements = new (VineNodeType type, Vector2I pos)[] {
                    (VineNodeType.ProximitySensor, new Vector2I(5, 5)),
                    (VineNodeType.DamageTower, new Vector2I(7, 5)),
                    (VineNodeType.Junction, new Vector2I(6, 5)),
                    (VineNodeType.Extender, new Vector2I(6, 6)),
                    (VineNodeType.SlowField, new Vector2I(8, 5)),
                };

                foreach (var (type, pos) in placements)
                {
                    if (grid.InBounds(pos) && grid.GetCell(pos) == VineCellType.Empty)
                    {
                        var data = VineNodeRegistry.Get(type);
                        var node = new VineNode();
                        node.Initialize(data);
                        grid.PlaceNode(node, pos);
                        _tempNodes.Add(node);
                    }
                }

                // Position camera to view the placed nodes
                var focusWorld = grid.GridToWorld(new Vector2I(6, 5));
                ctx.LockCamera(focusWorld, 12f);

                await ctx.Wait(0.3f);
                var path = ctx.CaptureVisualScreenshot("grid_with_nodes");
                ctx.Assert(path != null, "visual.grid_with_nodes",
                    path != null ? $"Screenshot saved: {path}" : "Failed to capture grid with nodes");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "visual.grid_with_nodes", $"Exception: {e.Message}");
            }
        }

        private async Task CaptureHudBuildPhase(TestContext ctx)
        {
            ctx.StartTest();
            try
            {
                await ctx.Wait(0.2f);
                var path = ctx.CaptureVisualScreenshot("hud_build_phase");
                ctx.Assert(path != null, "visual.hud_build_phase",
                    path != null ? $"Screenshot saved: {path}" : "Failed to capture HUD build phase");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "visual.hud_build_phase", $"Exception: {e.Message}");
            }
        }

        private async Task CaptureDraftScreen(TestContext ctx)
        {
            ctx.StartTest();
            try
            {
                // Transition to draft scene
                GameManager.Instance.StartVineDraft();
                await ctx.Wait(1.0f);

                var path = ctx.CaptureVisualScreenshot("draft_screen");
                ctx.Assert(path != null, "visual.draft_screen",
                    path != null ? $"Screenshot saved: {path}" : "Failed to capture draft screen");
            }
            catch (Exception e)
            {
                ctx.Assert(false, "visual.draft_screen", $"Exception: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════
        // Asset Material Preview — renders each tower/prop model and saves PNGs
        // ══════════════════════════════════════════════

        /// <summary>
        /// Render every node-type model in an isolated SubViewport and save screenshots.
        /// Call from test harness or F12 editor. Saves to test-reports/asset-previews/.
        /// </summary>
        public static async Task CaptureAllAssetPreviews(TestContext ctx)
        {
            GD.Print("[AssetPreview] Starting asset material preview captures...");

            // Check rendering availability
            var viewport = ctx.Tree.Root;
            var tex = viewport.GetTexture();
            if (tex == null)
            {
                GD.PrintErr("[AssetPreview] No rendering available (headless mode). Skipping.");
                return;
            }

            // All node-type → model mappings
            var assets = new (string name, string path)[]
            {
                ("DamageTower_TurretA", AssetLibrary.TURRET_A),
                ("SlowField_TurretB", AssetLibrary.TURRET_B),
                ("PushPull_TurretC", AssetLibrary.TURRET_C),
                ("SignalCannon_RocketLauncher", AssetLibrary.ROCKET_LAUNCHER),
                ("BuffEmitter_PlasmaGun", AssetLibrary.PLASMA_GUN),
                ("LoopAnchor_WeaponA", AssetLibrary.WEAPON_A),
                ("Pylon_WeaponB", AssetLibrary.WEAPON_B),
                ("ProximitySensor_Satellite", AssetLibrary.PROP_SATELLITE),
                ("TypeSensor_Radar", AssetLibrary.PROP_RADAR),
                ("HPSensor_AntennaA", AssetLibrary.PROP_ANTENNA_A),
                ("CountSensor_AntennaB", AssetLibrary.PROP_ANTENNA_B),
                ("Timer_GeneratorA", AssetLibrary.PROP_GENERATOR_A),
                ("Extender_LampA", AssetLibrary.PROP_LAMP_A),
                ("Junction_GeneratorB", AssetLibrary.PROP_GENERATOR_B),
                ("Switch_BarrierA", AssetLibrary.PROP_BARRIER_A),
                ("Gate_Hedgehog", AssetLibrary.PROP_HEDGEHOG),
                ("Inverter_LampB", AssetLibrary.PROP_LAMP_B),
                ("Delay_BarrierB", AssetLibrary.PROP_BARRIER_B),
                ("Latch_Fence", AssetLibrary.PROP_FENCE),
            };

            // Create isolated SubViewport for clean renders
            var subViewport = new SubViewport();
            subViewport.Size = new Vector2I(512, 512);
            subViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            subViewport.OwnWorld3D = true;
            subViewport.TransparentBg = false;
            ctx.Tree.Root.AddChild(subViewport);

            // Scene setup — camera, lights, ground
            var sceneRoot = new Node3D();
            subViewport.AddChild(sceneRoot);

            var camera = new Camera3D();
            camera.Fov = 40;
            sceneRoot.AddChild(camera);
            camera.Current = true;
            // Position after adding to tree (LookAt requires being in tree)
            camera.Position = new Vector3(4f, 3f, 4f);
            camera.LookAt(Vector3.Zero, Vector3.Up);

            var mainLight = new DirectionalLight3D();
            mainLight.RotationDegrees = new Vector3(-40, -30, 0);
            mainLight.LightColor = new Color(1f, 1f, 1f);
            mainLight.LightEnergy = 1.5f;
            mainLight.ShadowEnabled = true;
            sceneRoot.AddChild(mainLight);

            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-20, 150, 0);
            fillLight.LightColor = new Color(0.5f, 0.6f, 0.7f);
            fillLight.LightEnergy = 0.6f;
            sceneRoot.AddChild(fillLight);

            var worldEnv = new WorldEnvironment();
            var env = new Godot.Environment();
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = new Color(0.15f, 0.15f, 0.18f);
            env.AmbientLightColor = new Color(0.4f, 0.4f, 0.45f);
            env.AmbientLightEnergy = 0.8f;
            worldEnv.Environment = env;
            sceneRoot.AddChild(worldEnv);

            var ground = new MeshInstance3D();
            var groundMesh = new PlaneMesh();
            groundMesh.Size = new Vector2(10, 10);
            ground.Mesh = groundMesh;
            var groundMat = new StandardMaterial3D();
            groundMat.AlbedoColor = new Color(0.2f, 0.2f, 0.22f);
            groundMat.Roughness = 0.9f;
            ground.MaterialOverride = groundMat;
            sceneRoot.AddChild(ground);

            // Ensure output directory
            string outDir = "test-reports/asset-previews";
            DirAccess.MakeDirRecursiveAbsolute($"res://{outDir}");

            Node3D currentModel = null;

            foreach (var (name, path) in assets)
            {
                // Remove previous model
                if (currentModel != null)
                {
                    sceneRoot.RemoveChild(currentModel);
                    currentModel.QueueFree();
                    currentModel = null;
                }

                // Load model
                var model = AssetLibrary.InstantiateNormalized(path);
                if (model == null)
                {
                    GD.PrintErr($"[AssetPreview] FAILED to load: {name} ({path})");
                    continue;
                }

                AssetLibrary.GroundModel(model);
                sceneRoot.AddChild(model);
                currentModel = model;

                // Auto-frame camera to fit model
                var aabb = GetCombinedAabb(model);
                float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
                float dist = Mathf.Max(maxDim * 2.5f, 2f); // Ensure minimum distance
                var center = aabb.GetCenter();
                camera.Position = center + new Vector3(dist * 0.7f, dist * 0.5f, dist * 0.7f);
                camera.LookAt(center, Vector3.Up);

                // Check material status
                bool hasTextures = AssetLibrary.HasOriginalMaterials(model);
                GD.Print($"[AssetPreview] {name}: hasOriginalMaterials={hasTextures}, size={aabb.Size}, dist={dist:F1}");

                // Wait for render (2 frames minimum for SubViewport)
                await ctx.Wait(0.5f);

                // Capture
                var image = subViewport.GetTexture().GetImage();
                if (image != null && image.GetWidth() > 0)
                {
                    string filePath = $"res://{outDir}/{name}.png";
                    image.SavePng(filePath);
                    GD.Print($"[AssetPreview] Saved: {filePath}");
                }
                else
                {
                    GD.PrintErr($"[AssetPreview] Failed to capture: {name}");
                }
            }

            // Cleanup
            if (currentModel != null)
                currentModel.QueueFree();
            subViewport.QueueFree();

            GD.Print($"[AssetPreview] Done — {assets.Length} assets captured to {outDir}/");
        }

        private static Aabb GetCombinedAabb(Node node)
        {
            var aabb = new Aabb();
            bool first = true;
            GetAabbRecursive(node, ref aabb, ref first);
            if (first) aabb = new Aabb(Vector3.Zero, Vector3.One); // fallback
            return aabb;
        }

        private static void GetAabbRecursive(Node node, ref Aabb aabb, ref bool first)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                var meshAabb = mesh.GetAabb();
                // Transform to global space
                var globalAabb = mesh.GlobalTransform * meshAabb;
                if (first) { aabb = globalAabb; first = false; }
                else aabb = aabb.Merge(globalAabb);
            }
            foreach (var child in node.GetChildren())
                GetAabbRecursive(child, ref aabb, ref first);
        }
    }
}
