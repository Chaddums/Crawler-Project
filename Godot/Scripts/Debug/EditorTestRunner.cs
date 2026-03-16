using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JunkbotArena.Editor;

namespace JunkbotArena
{
    /// <summary>
    /// Automated editor testing framework. Ctrl+Shift+T runs all tests.
    /// Opens each editor tab, captures screenshots, cycles through all
    /// module properties, then generates a markdown report.
    ///
    /// Now includes viewport content validation: screenshots are analyzed
    /// to detect empty viewports, clipped models, and unchanged animations.
    ///
    /// Phases:
    ///   1. Tab smoke test (all tabs)
    ///   2. Characters — bot frame cycling
    ///   3. Characters — growth tier cycling
    ///   4. Combatants — enemy/boss cycling + animations
    ///   5. Balance — sub-tab cycling
    ///   6. Dungeon — generate + collision toggle
    ///   7. Assets — category/asset cycling + collision
    ///   8. VFX — effect cycling
    ///   9. Strings — view filter cycling
    ///  10. Sound — audio category cycling
    ///  11. Sectors — sector table view
    ///  12. UI/UX — screen cycling + layout toggle
    /// </summary>
    public partial class EditorTestRunner : Node
    {
        private struct TestResult
        {
            public int Index;
            public string Tab;
            public string TestName;
            public string PngPath;
            public bool Success;
            public string Error;
            public string Warning;
        }

        private bool _running;
        private int _estimatedTotal = 154;
        private readonly List<TestResult> _results = new();

        // CLI automation fields
        private bool _autoExit;
        private string _phaseFilter;
        private readonly List<string> _phasesRun = new();

        // Progress HUD
        private CanvasLayer _hudLayer;
        private Label _progressLabel;
        private ProgressBar _progressBar;
        private int _totalSteps;
        private int _currentStep;

        // Tracks the previous viewport image for animation comparison (Phase 4)
        private Image _previousViewportImage;

        // Viewport validation thresholds
        private const float ContentThreshold = 0.02f;  // 2% of pixels must differ from background
        private const float ChannelDiffThreshold = 0.1f; // Per-channel difference to count as "different"
        private const float AnimSimilarityThreshold = 0.95f; // 95%+ similar = animation didn't change
        private const float ClipBottomFraction = 0.2f;  // Bottom 20% — model might be floor-clipped
        private const int SampleStep = 4; // Sample every 4th pixel for speed

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            BuildProgressHUD();

            // Check for CLI auto-run
            var args = OS.GetCmdlineUserArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith("--editor-test"))
                {
                    _autoExit = Array.Exists(args, a => a == "--editor-test-exit");
                    // Parse phase filter if specified (e.g. --editor-test=combatants)
                    if (arg.Contains("="))
                        _phaseFilter = arg.Split("=")[1].ToLower();
                    // Start after a delay to let editor initialize
                    CallDeferred(nameof(AutoStartTests));
                    break;
                }
            }
        }

        private async void AutoStartTests()
        {
            await Wait(2.0f); // Let everything initialize
            RunAllTests();
        }

        private bool ShouldRunPhase(int phaseNumber, string phaseName)
        {
            if (string.IsNullOrEmpty(_phaseFilter)) return true; // no filter = run all
            if (_phaseFilter == "all") return true;

            // Match by number or name
            return _phaseFilter == phaseNumber.ToString()
                || _phaseFilter == phaseName.ToLower();
        }

        private void BuildProgressHUD()
        {
            _hudLayer = new CanvasLayer();
            _hudLayer.Layer = 130; // Above everything
            _hudLayer.Visible = false;
            AddChild(_hudLayer);

            var panel = new PanelContainer();
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
            panel.OffsetTop = 8;
            panel.OffsetLeft = -250;
            panel.OffsetRight = 250;
            panel.OffsetBottom = 70;
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.12f, 0.92f);
            style.BorderColor = new Color(0.3f, 0.8f, 1.0f);
            style.BorderWidthBottom = style.BorderWidthTop = style.BorderWidthLeft = style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 6;
            style.ContentMarginLeft = style.ContentMarginRight = 12;
            style.ContentMarginTop = style.ContentMarginBottom = 8;
            panel.AddThemeStyleboxOverride("panel", style);
            _hudLayer.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            panel.AddChild(vbox);

            _progressLabel = new Label();
            _progressLabel.Text = "Editor Test Runner";
            _progressLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _progressLabel.AddThemeFontSizeOverride("font_size", 14);
            _progressLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1.0f));
            vbox.AddChild(_progressLabel);

            _progressBar = new ProgressBar();
            _progressBar.CustomMinimumSize = new Vector2(0, 16);
            _progressBar.ShowPercentage = false;
            vbox.AddChild(_progressBar);
        }

        private void ShowProgress(string phase, string detail, int step, int total)
        {
            _currentStep = step;
            _totalSteps = total;
            _hudLayer.Visible = true;
            _progressLabel.Text = $"[{step}/{total}] {phase}: {detail}";
            _progressBar.MaxValue = total;
            _progressBar.Value = step;
        }

        private void HideProgress()
        {
            _hudLayer.Visible = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo
                && key.Keycode == Key.T && key.CtrlPressed && key.ShiftPressed)
            {
                if (!_running)
                {
                    GetViewport().SetInputAsHandled();
                    RunAllTests();
                }
            }
        }

        private async void RunAllTests()
        {
            _running = true;
            _results.Clear();

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var projectDir = ProjectSettings.GlobalizePath("res://");
            var repoRoot = Path.GetDirectoryName(projectDir.TrimEnd('/', '\\'));
            var outputDir = Path.Combine(repoRoot, "test-reports", "editor-tests", timestamp);

            GD.Print($"[EditorTestRunner] Starting editor tests → {outputDir}");

            var editor = EditorManager.Instance;
            if (editor == null)
            {
                GD.PrintErr("[EditorTestRunner] EditorManager.Instance is null — aborting.");
                _running = false;
                return;
            }

            // Open editor if not visible
            var modules = editor.Modules;
            if (modules == null || modules.Count == 0)
            {
                GD.PrintErr("[EditorTestRunner] No editor modules registered — aborting.");
                _running = false;
                return;
            }

            // Ensure editor is open
            editor.Toggle();
            await Wait(0.5f);

            // Disable auto-rotate on all 3D viewport modules for consistent screenshots
            foreach (var mod in modules)
                mod.TestSetAutoRotate(false);

            int captureIndex = 0;
            _estimatedTotal = 350; // expanded combatant sweep: 19 enemies x 5 captures each + other phases

            // ════════════════════════════════════════════
            //  Phase 1: Tab smoke test
            // ════════════════════════════════════════════
            if (ShouldRunPhase(1, "smoke"))
            {
            _phasesRun.Add("smoke");
            GD.Print("[EditorTestRunner] Phase 1: Tab smoke tests");
            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                var tabName = module.PanelName;
                var tabDir = Path.Combine(outputDir, SanitizePath(tabName));

                try
                {
                    editor.SwitchToTab(tabName);
                    await Wait(0.3f);

                    EnsureDir(tabDir);
                    var fullPath = Path.Combine(tabDir, $"{captureIndex:D3}_smoke_full.png");
                    CaptureFullViewport(fullPath);

                    _results.Add(new TestResult
                    {
                        Index = captureIndex,
                        Tab = tabName,
                        TestName = "smoke",
                        PngPath = GetRelativePath(outputDir, fullPath),
                        Success = true,
                        Error = null,
                        Warning = null
                    });
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[EditorTestRunner] Phase 1 error on tab '{tabName}': {e.Message}");
                    _results.Add(new TestResult
                    {
                        Index = captureIndex,
                        Tab = tabName,
                        TestName = "smoke",
                        PngPath = "",
                        Success = false,
                        Error = e.Message,
                        Warning = null
                    });
                }

                captureIndex++;
            }

            } // end Phase 1

            // charModule is used by both Phase 2 and Phase 3
            var charModule = FindModule(modules, "Characters");

            // ════════════════════════════════════════════
            //  Phase 2: Characters — cycle all bot frames
            // ════════════════════════════════════════════
            if (ShouldRunPhase(2, "characters"))
            {
            _phasesRun.Add("characters");
            GD.Print("[EditorTestRunner] Phase 2: Character frame cycling");
            if (charModule != null)
            {
                editor.SwitchToTab("Characters");
                await Wait(0.3f);

                var charDir = Path.Combine(outputDir, "Characters");
                EnsureDir(charDir);

                // 6 bot frames
                string[] frameNames = { "TinCan", "Scrapheap", "SparkPlug", "RustBucket", "NoiseBox", "Clunker" };
                for (int f = 0; f < frameNames.Length; f++)
                {
                    try
                    {
                        charModule.TestCycleNext("frame");
                        await Wait(0.3f);

                        var testName = $"frame_{frameNames[f]}";
                        captureIndex = await CaptureWithViewport(charModule, charDir, outputDir, captureIndex, "Characters", testName);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 2 error on frame '{frameNames[f]}': {e.Message}");
                        RecordFailure(ref captureIndex, "Characters", $"frame_{frameNames[f]}", e.Message);
                    }
                }
            }
            else
            {
                GD.Print("[EditorTestRunner] Characters module not found — skipping Phase 2");
            }

            } // end Phase 2

            // ════════════════════════════════════════════
            //  Phase 3: Growth tier cycling (first frame)
            // ════════════════════════════════════════════
            if (ShouldRunPhase(3, "growth"))
            {
            _phasesRun.Add("growth");
            GD.Print("[EditorTestRunner] Phase 3: Growth tier cycling");
            if (charModule != null)
            {
                editor.SwitchToTab("Characters");
                await Wait(0.3f);

                var charDir = Path.Combine(outputDir, "Characters");
                EnsureDir(charDir);

                string[] tierNames = { "Base", "Plated", "Armored", "Heavy", "Evolved" };
                for (int t = 0; t < tierNames.Length; t++)
                {
                    try
                    {
                        charModule.TestCycleNext("growth");
                        await Wait(0.3f);

                        var testName = $"growth_{tierNames[t]}";
                        captureIndex = await CaptureWithViewport(charModule, charDir, outputDir, captureIndex, "Characters", testName);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 3 error on tier '{tierNames[t]}': {e.Message}");
                        RecordFailure(ref captureIndex, "Characters", $"growth_{tierNames[t]}", e.Message);
                    }
                }
            }

            } // end Phase 3

            // ════════════════════════════════════════════
            //  Phase 4: Combatants — ALL enemies + ALL animations per enemy
            // ════════════════════════════════════════════
            if (ShouldRunPhase(4, "combatants"))
            {
            _phasesRun.Add("combatants");
            GD.Print("[EditorTestRunner] Phase 4: Full combatant + animation sweep");
            try
            {
                var combatModule = FindModule(modules, "Combatants");
                if (combatModule != null)
                {
                    editor.SwitchToTab("Combatants");
                    await Wait(0.3f);

                    var combatDir = Path.Combine(outputDir, "Combatants");
                    EnsureDir(combatDir);

                    // ALL enemies + bosses in the game
                    string[] allCombatants = {
                        // Fodder
                        "calibration_target", "scrap_rat", "rust_mite", "wire_worm",
                        // Normal
                        "spark_drone", "junk_lurker", "patch_bot", "volt_sprinter",
                        "shard_lobber", "glitch_phantom", "overclock_drone",
                        // Elite
                        "decoy_unit", "scrap_golem",
                        // Mini-boss
                        "axis_disciple",
                        // Bosses
                        "corrupted_sentry", "rust_titan", "scrap_hydra", "null_warden", "axis_avatar"
                    };

                    string[] animStates = { "Idle", "Chase", "Attack", "Dead" };

                    // Try to get the BossEditor to read the actual combatant ID
                    var bossEditor = combatModule as BossEditor;

                    for (int c = 0; c < allCombatants.Length; c++)
                    {
                        try
                        {
                            // Select this combatant
                            combatModule.TestCycleNext("combatant");
                            await Wait(0.5f);

                            // Use the actual combatant ID from BossEditor if available,
                            // otherwise fall back to the hardcoded array
                            var actualId = bossEditor?.CurrentCombatantId;
                            var enemyName = actualId ?? allCombatants[c];
                            string nameWarning = null;

                            // If we have both, check for mismatch
                            if (actualId != null && actualId != allCombatants[c])
                            {
                                nameWarning = $"Expected '{allCombatants[c]}' but editor shows '{actualId}'";
                                GD.Print($"[EditorTestRunner] Combatant name mismatch: {nameWarning}");
                            }

                            // Capture idle pose (default) — with viewport validation
                            _previousViewportImage = null;
                            captureIndex = await CaptureWithViewportValidated(
                                combatModule, combatDir, outputDir, captureIndex,
                                "Combatants", $"{enemyName}_idle", nameWarning);

                            // Cycle through all animation states
                            for (int a = 0; a < animStates.Length; a++)
                            {
                                try
                                {
                                    combatModule.TestCycleNext("animation");
                                    await Wait(0.4f);

                                    captureIndex = await CaptureWithViewportValidated(
                                        combatModule, combatDir, outputDir, captureIndex,
                                        "Combatants", $"{enemyName}_{animStates[a]}",
                                        nameWarning);
                                }
                                catch (Exception ex)
                                {
                                    GD.PrintErr($"[EditorTestRunner] Phase 4 anim error '{enemyName}/{animStates[a]}': {ex.Message}");
                                    RecordFailure(ref captureIndex, "Combatants", $"{enemyName}_{animStates[a]}", ex.Message);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"[EditorTestRunner] Phase 4 error on combatant '{allCombatants[c]}': {e.Message}");
                            RecordFailure(ref captureIndex, "Combatants", $"{allCombatants[c]}", e.Message);
                        }
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] Combatants module not found — skipping Phase 4");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 4 error: {e.Message}");
            }

            } // end Phase 4

            // ════════════════════════════════════════════
            //  Phase 5: Balance — sub-tab cycling
            // ════════════════════════════════════════════
            if (ShouldRunPhase(5, "balance"))
            {
            _phasesRun.Add("balance");
            GD.Print("[EditorTestRunner] Phase 5: Balance sub-tabs");
            try
            {
                var balanceModule = FindModule(modules, "Balance");
                if (balanceModule != null)
                {
                    editor.SwitchToTab("Balance");
                    await Wait(0.3f);

                    var balanceDir = Path.Combine(outputDir, "Balance");
                    EnsureDir(balanceDir);

                    string[] subTabs = { "Abilities", "Enemies", "Equipment", "BotFrames", "Consumables", "Relics" };
                    for (int s = 0; s < subTabs.Length; s++)
                    {
                        try
                        {
                            balanceModule.TestCycleNext("subtab");
                            await Wait(0.3f);

                            var testName = $"subtab_{subTabs[s]}";
                            var fullPath = Path.Combine(balanceDir, $"{captureIndex:D3}_{testName}_full.png");
                            CaptureFullViewport(fullPath);
                            RecordSuccess(ref captureIndex, "Balance", testName, outputDir, fullPath);
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"[EditorTestRunner] Phase 5 error on subtab '{subTabs[s]}': {e.Message}");
                            RecordFailure(ref captureIndex, "Balance", $"subtab_{subTabs[s]}", e.Message);
                        }
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] Balance module not found — skipping Phase 5");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 5 error: {e.Message}");
            }

            } // end Phase 5

            // ════════════════════════════════════════════
            //  Phase 6: Dungeon — generate + collision
            // ════════════════════════════════════════════
            if (ShouldRunPhase(6, "dungeon"))
            {
            _phasesRun.Add("dungeon");
            GD.Print("[EditorTestRunner] Phase 6: Dungeon generation");
            try
            {
                var dungeonModule = FindModule(modules, "Dungeon");
                if (dungeonModule != null)
                {
                    editor.SwitchToTab("Dungeon");
                    await Wait(0.3f);

                    var dungeonDir = Path.Combine(outputDir, "Dungeon");
                    EnsureDir(dungeonDir);

                    // Generate a dungeon
                    try
                    {
                        dungeonModule.TestCycleNext("generate");
                        await Wait(0.5f);

                        var testName = "dungeon_generated";
                        captureIndex = await CaptureWithViewport(dungeonModule, dungeonDir, outputDir, captureIndex, "Dungeon", testName);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 6 generate error: {e.Message}");
                        RecordFailure(ref captureIndex, "Dungeon", "dungeon_generated", e.Message);
                    }

                    // Cycle layout
                    try
                    {
                        dungeonModule.TestCycleNext("layout");
                        await Wait(0.5f);

                        var testName = "dungeon_layout_cycled";
                        captureIndex = await CaptureWithViewport(dungeonModule, dungeonDir, outputDir, captureIndex, "Dungeon", testName);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 6 layout error: {e.Message}");
                        RecordFailure(ref captureIndex, "Dungeon", "dungeon_layout_cycled", e.Message);
                    }

                    // Toggle collision overlay
                    try
                    {
                        dungeonModule.TestCycleNext("collision");
                        await Wait(0.3f);

                        var testName = "dungeon_collision_toggle";
                        captureIndex = await CaptureWithViewport(dungeonModule, dungeonDir, outputDir, captureIndex, "Dungeon", testName);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 6 collision error: {e.Message}");
                        RecordFailure(ref captureIndex, "Dungeon", "dungeon_collision_toggle", e.Message);
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] Dungeon module not found — skipping Phase 6");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 6 error: {e.Message}");
            }

            } // end Phase 6

            // ════════════════════════════════════════════
            //  Phase 7: Assets — category/asset cycling
            // ════════════════════════════════════════════
            if (ShouldRunPhase(7, "assets"))
            {
            _phasesRun.Add("assets");
            GD.Print("[EditorTestRunner] Phase 7: Assets cycling");
            try
            {
                var assetModule = FindModule(modules, "Assets");
                if (assetModule != null)
                {
                    editor.SwitchToTab("Assets");
                    await Wait(0.3f);

                    var assetDir = Path.Combine(outputDir, "Assets");
                    EnsureDir(assetDir);

                    string[] categories = { "prop", "floor", "wall", "door", "detail" };
                    for (int cat = 0; cat < categories.Length; cat++)
                    {
                        try
                        {
                            assetModule.TestCycleNext("category");
                            await Wait(0.3f);

                            var catTestName = $"category_{categories[cat]}";
                            captureIndex = await CaptureWithViewport(assetModule, assetDir, outputDir, captureIndex, "Assets", catTestName);

                            // Select 2-3 assets within this category
                            for (int a = 0; a < 3; a++)
                            {
                                try
                                {
                                    assetModule.TestCycleNext("asset");
                                    await Wait(0.5f);

                                    var assetTestName = $"asset_{categories[cat]}_{a + 1}";
                                    captureIndex = await CaptureWithViewport(assetModule, assetDir, outputDir, captureIndex, "Assets", assetTestName);
                                }
                                catch (Exception e)
                                {
                                    GD.PrintErr($"[EditorTestRunner] Phase 7 asset error: {e.Message}");
                                    RecordFailure(ref captureIndex, "Assets", $"asset_{categories[cat]}_{a + 1}", e.Message);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"[EditorTestRunner] Phase 7 category error '{categories[cat]}': {e.Message}");
                            RecordFailure(ref captureIndex, "Assets", $"category_{categories[cat]}", e.Message);
                        }
                    }

                    // Toggle collision view
                    try
                    {
                        assetModule.TestCycleNext("collision");
                        await Wait(0.3f);

                        var collTestName = "asset_collision_toggle";
                        captureIndex = await CaptureWithViewport(assetModule, assetDir, outputDir, captureIndex, "Assets", collTestName);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 7 collision error: {e.Message}");
                        RecordFailure(ref captureIndex, "Assets", "asset_collision_toggle", e.Message);
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] Assets module not found — skipping Phase 7");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 7 error: {e.Message}");
            }

            } // end Phase 7

            // ════════════════════════════════════════════
            //  Phase 8: VFX — effect cycling
            // ════════════════════════════════════════════
            if (ShouldRunPhase(8, "vfx"))
            {
            _phasesRun.Add("vfx");
            GD.Print("[EditorTestRunner] Phase 8: VFX effect cycling");
            try
            {
                var vfxModule = FindModule(modules, "VFX");
                if (vfxModule != null)
                {
                    editor.SwitchToTab("VFX");
                    await Wait(0.3f);

                    var vfxDir = Path.Combine(outputDir, "VFX");
                    EnsureDir(vfxDir);

                    string[] effectSamples = { "hit_physical", "hit_fire", "death", "muzzle_flash", "heal", "celebration" };
                    for (int e = 0; e < effectSamples.Length; e++)
                    {
                        try
                        {
                            vfxModule.TestCycleNext("effect");
                            await Wait(0.15f); // Short wait — capture while particles are still visible

                            var testName = $"effect_{effectSamples[e]}";
                            captureIndex = await CaptureWithViewport(vfxModule, vfxDir, outputDir, captureIndex, "VFX", testName);
                        }
                        catch (Exception ex)
                        {
                            GD.PrintErr($"[EditorTestRunner] Phase 8 error on effect '{effectSamples[e]}': {ex.Message}");
                            RecordFailure(ref captureIndex, "VFX", $"effect_{effectSamples[e]}", ex.Message);
                        }
                    }

                    // Cycle through color presets on current effect
                    try
                    {
                        vfxModule.TestCycleNext("color");
                        await Wait(0.3f);

                        var colorTestName = "vfx_color_preset";
                        captureIndex = await CaptureWithViewport(vfxModule, vfxDir, outputDir, captureIndex, "VFX", colorTestName);
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 8 color error: {ex.Message}");
                        RecordFailure(ref captureIndex, "VFX", "vfx_color_preset", ex.Message);
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] VFX module not found — skipping Phase 8");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 8 error: {e.Message}");
            }

            } // end Phase 8

            // ════════════════════════════════════════════
            //  Phase 9: Strings — view filter cycling
            // ════════════════════════════════════════════
            if (ShouldRunPhase(9, "strings"))
            {
            _phasesRun.Add("strings");
            GD.Print("[EditorTestRunner] Phase 9: Strings view filters");
            try
            {
                var stringsModule = FindModule(modules, "Strings");
                if (stringsModule != null)
                {
                    editor.SwitchToTab("Strings");
                    await Wait(0.3f);

                    var stringsDir = Path.Combine(outputDir, "Strings");
                    EnsureDir(stringsDir);

                    string[] filterNames = { "All", "TempOnly", "FinalOnly", "StringsOnly", "DialogueOnly" };
                    for (int f = 0; f < filterNames.Length; f++)
                    {
                        try
                        {
                            stringsModule.TestCycleNext("filter");
                            await Wait(0.3f);

                            var testName = $"filter_{filterNames[f]}";
                            var fullPath = Path.Combine(stringsDir, $"{captureIndex:D3}_{testName}_full.png");
                            CaptureFullViewport(fullPath);
                            RecordSuccess(ref captureIndex, "Strings", testName, outputDir, fullPath);
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"[EditorTestRunner] Phase 9 error on filter '{filterNames[f]}': {e.Message}");
                            RecordFailure(ref captureIndex, "Strings", $"filter_{filterNames[f]}", e.Message);
                        }
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] Strings module not found — skipping Phase 9");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 9 error: {e.Message}");
            }

            } // end Phase 9

            // ════════════════════════════════════════════
            //  Phase 10: Sound — audio category cycling
            // ════════════════════════════════════════════
            if (ShouldRunPhase(10, "sound"))
            {
            _phasesRun.Add("sound");
            GD.Print("[EditorTestRunner] Phase 10: Sound category cycling");
            try
            {
                var soundModule = FindModule(modules, "Sound");
                if (soundModule != null)
                {
                    editor.SwitchToTab("Sound");
                    await Wait(0.3f);

                    var soundDir = Path.Combine(outputDir, "Sound");
                    EnsureDir(soundDir);

                    string[] audioCats = { "sfx", "music", "voice", "abilities", "ambient", "ui" };
                    for (int c = 0; c < audioCats.Length; c++)
                    {
                        try
                        {
                            soundModule.TestCycleNext("category");
                            await Wait(0.3f);

                            var testName = $"category_{audioCats[c]}";
                            var fullPath = Path.Combine(soundDir, $"{captureIndex:D3}_{testName}_full.png");
                            CaptureFullViewport(fullPath);
                            RecordSuccess(ref captureIndex, "Sound", testName, outputDir, fullPath);
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"[EditorTestRunner] Phase 10 error on category '{audioCats[c]}': {e.Message}");
                            RecordFailure(ref captureIndex, "Sound", $"category_{audioCats[c]}", e.Message);
                        }
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] Sound module not found — skipping Phase 10");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 10 error: {e.Message}");
            }

            } // end Phase 10

            // ════════════════════════════════════════════
            //  Phase 11: Sectors — sector table view
            // ════════════════════════════════════════════
            if (ShouldRunPhase(11, "sectors"))
            {
            _phasesRun.Add("sectors");
            GD.Print("[EditorTestRunner] Phase 11: Sectors");
            try
            {
                var sectorModule = FindModule(modules, "Sectors");
                if (sectorModule != null)
                {
                    editor.SwitchToTab("Sectors");
                    await Wait(0.3f);

                    var sectorDir = Path.Combine(outputDir, "Sectors");
                    EnsureDir(sectorDir);

                    // Capture initial table view
                    try
                    {
                        var fullPath = Path.Combine(sectorDir, $"{captureIndex:D3}_sector_table_full.png");
                        CaptureFullViewport(fullPath);
                        RecordSuccess(ref captureIndex, "Sectors", "sector_table", outputDir, fullPath);
                    }
                    catch (Exception e)
                    {
                        RecordFailure(ref captureIndex, "Sectors", "sector_table", e.Message);
                    }

                    // Cycle through sectors
                    for (int s = 0; s < 5; s++)
                    {
                        try
                        {
                            sectorModule.TestCycleNext("sector");
                            await Wait(0.3f);

                            var testName = $"sector_{s + 1}";
                            var fullPath = Path.Combine(sectorDir, $"{captureIndex:D3}_{testName}_full.png");
                            CaptureFullViewport(fullPath);
                            RecordSuccess(ref captureIndex, "Sectors", testName, outputDir, fullPath);
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"[EditorTestRunner] Phase 11 error on sector {s + 1}: {e.Message}");
                            RecordFailure(ref captureIndex, "Sectors", $"sector_{s + 1}", e.Message);
                        }
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] Sectors module not found — skipping Phase 11");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 11 error: {e.Message}");
            }

            } // end Phase 11

            // ════════════════════════════════════════════
            //  Phase 12: UI/UX — screen cycling + layout
            // ════════════════════════════════════════════
            if (ShouldRunPhase(12, "uiux"))
            {
            _phasesRun.Add("uiux");
            GD.Print("[EditorTestRunner] Phase 12: UI/UX screens");
            try
            {
                var uiModule = FindModule(modules, "UI/UX");
                if (uiModule != null)
                {
                    editor.SwitchToTab("UI/UX");
                    await Wait(0.3f);

                    var uiDir = Path.Combine(outputDir, "UI_UX");
                    EnsureDir(uiDir);

                    // Cycle through screens
                    // UI/UX module renders 2D UI with content at screen edges (HUD bars, minimap, etc.)
                    // so we skip 3D viewport validation — just capture full + viewport screenshots.
                    string[] screenNames = { "HUD", "Inventory", "PassiveTree" };
                    for (int s = 0; s < screenNames.Length; s++)
                    {
                        try
                        {
                            uiModule.TestCycleNext("screen");
                            await Wait(0.3f);

                            var testName = $"screen_{screenNames[s]}";
                            captureIndex = await CaptureWithViewportNoValidation(uiModule, uiDir, outputDir, captureIndex, "UI/UX", testName);
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"[EditorTestRunner] Phase 12 error on screen '{screenNames[s]}': {e.Message}");
                            RecordFailure(ref captureIndex, "UI/UX", $"screen_{screenNames[s]}", e.Message);
                        }
                    }

                    // Toggle layout mode
                    try
                    {
                        uiModule.TestCycleNext("layout");
                        await Wait(0.3f);

                        var layoutTestName = "layout_mode_on";
                        captureIndex = await CaptureWithViewportNoValidation(uiModule, uiDir, outputDir, captureIndex, "UI/UX", layoutTestName);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 12 layout error: {e.Message}");
                        RecordFailure(ref captureIndex, "UI/UX", "layout_mode_on", e.Message);
                    }

                    // Toggle layout mode off
                    try
                    {
                        uiModule.TestCycleNext("layout");
                        await Wait(0.3f);

                        var layoutTestName = "layout_mode_off";
                        captureIndex = await CaptureWithViewportNoValidation(uiModule, uiDir, outputDir, captureIndex, "UI/UX", layoutTestName);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EditorTestRunner] Phase 12 layout off error: {e.Message}");
                        RecordFailure(ref captureIndex, "UI/UX", "layout_mode_off", e.Message);
                    }
                }
                else
                {
                    GD.Print("[EditorTestRunner] UI/UX module not found — skipping Phase 12");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorTestRunner] Phase 12 error: {e.Message}");
            }

            } // end Phase 12

            // ════════════════════════════════════════════
            //  Generate report
            // ════════════════════════════════════════════
            GenerateReport(outputDir, timestamp);
            GenerateJsonReport(outputDir, timestamp);

            // Leave editor open so user can inspect final state

            int passed = 0;
            int failed = 0;
            int warned = 0;
            foreach (var r in _results)
            {
                if (r.Success) passed++;
                else failed++;
                if (!string.IsNullOrEmpty(r.Warning)) warned++;
            }

            HideProgress();

            GD.Print($"[EditorTestRunner] Done! {_results.Count} captures saved to {outputDir}");
            GD.Print($"[EditorTestRunner] Results: {passed} passed, {failed} failed, {warned} warnings, {_results.Count} total");

            _running = false;

            if (_autoExit)
            {
                int exitCode = failed > 0 ? 1 : 0;
                GD.Print($"[EditorTestRunner] Auto-exit with code {exitCode}");
                GetTree().Quit(exitCode);
            }
        }

        // ════════════════════════════════════════════════════
        //  Viewport Content Validation
        // ════════════════════════════════════════════════════

        /// <summary>
        /// Sample the top-left corner of the image to estimate background color.
        /// Returns the average color of the top-left 10x10 pixel block.
        /// </summary>
        private static Color SampleBackgroundColor(Image image)
        {
            float r = 0, g = 0, b = 0;
            int count = 0;
            int sampleSize = Math.Min(10, Math.Min(image.GetWidth(), image.GetHeight()));

            for (int y = 0; y < sampleSize; y++)
            {
                for (int x = 0; x < sampleSize; x++)
                {
                    var px = image.GetPixel(x, y);
                    r += px.R;
                    g += px.G;
                    b += px.B;
                    count++;
                }
            }

            if (count == 0) return new Color(0, 0, 0);
            return new Color(r / count, g / count, b / count);
        }

        /// <summary>
        /// Check if a pixel differs from the background color by more than the threshold
        /// in any single channel.
        /// </summary>
        private static bool PixelDiffersFromBackground(Color pixel, Color background)
        {
            return Math.Abs(pixel.R - background.R) > ChannelDiffThreshold
                || Math.Abs(pixel.G - background.G) > ChannelDiffThreshold
                || Math.Abs(pixel.B - background.B) > ChannelDiffThreshold;
        }

        /// <summary>
        /// Validate that the viewport contains a visible model (not empty/black/offscreen).
        /// Samples the center 50% of the image and checks how many pixels differ from the
        /// background reference color (sampled from the top-left corner).
        /// Returns true if the viewport has content, false if it appears empty.
        /// </summary>
        private static bool ValidateViewportHasContent(Image image, out float contentPercent)
        {
            contentPercent = 0f;
            int w = image.GetWidth();
            int h = image.GetHeight();
            if (w < 20 || h < 20) return false;

            var bgColor = SampleBackgroundColor(image);

            // Sample the center 50% region
            int x0 = w / 4;
            int x1 = w * 3 / 4;
            int y0 = h / 4;
            int y1 = h * 3 / 4;

            int totalSampled = 0;
            int diffCount = 0;

            for (int y = y0; y < y1; y += SampleStep)
            {
                for (int x = x0; x < x1; x += SampleStep)
                {
                    totalSampled++;
                    var px = image.GetPixel(x, y);
                    if (PixelDiffersFromBackground(px, bgColor))
                        diffCount++;
                }
            }

            if (totalSampled == 0) return false;
            contentPercent = (float)diffCount / totalSampled;
            return contentPercent >= ContentThreshold;
        }

        /// <summary>
        /// Check if the model appears only in the bottom portion of the image,
        /// which may indicate it is clipping through the floor.
        /// Returns true if model might be clipped (all content in bottom 20%).
        /// </summary>
        private static bool ValidateModelNotClipped(Image image, out string clipDetail)
        {
            clipDetail = null;
            int w = image.GetWidth();
            int h = image.GetHeight();
            if (w < 20 || h < 20) return false;

            var bgColor = SampleBackgroundColor(image);

            // Check the center horizontal band: top 80% vs bottom 20%
            int x0 = w / 4;
            int x1 = w * 3 / 4;
            int ySplit = (int)(h * (1f - ClipBottomFraction)); // 80% mark

            int topContent = 0;
            int topSampled = 0;
            int bottomContent = 0;
            int bottomSampled = 0;

            // Sample top 80%
            for (int y = 0; y < ySplit; y += SampleStep)
            {
                for (int x = x0; x < x1; x += SampleStep)
                {
                    topSampled++;
                    if (PixelDiffersFromBackground(image.GetPixel(x, y), bgColor))
                        topContent++;
                }
            }

            // Sample bottom 20%
            for (int y = ySplit; y < h; y += SampleStep)
            {
                for (int x = x0; x < x1; x += SampleStep)
                {
                    bottomSampled++;
                    if (PixelDiffersFromBackground(image.GetPixel(x, y), bgColor))
                        bottomContent++;
                }
            }

            // If there IS content in bottom but NONE in top, model is likely clipped
            if (bottomSampled > 0 && bottomContent > 0 && topSampled > 0 && topContent == 0)
            {
                float bottomPct = (float)bottomContent / bottomSampled * 100f;
                clipDetail = $"Model may be clipping through floor (content only in bottom {ClipBottomFraction * 100:F0}%, {bottomPct:F1}% pixels)";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if the model is rendering with broken textures (all-white default material
        /// or all-black missing material). Samples non-background pixels and checks if they
        /// are overwhelmingly a single flat color, indicating broken/missing PBR textures.
        /// Returns a warning/error string or null if textures look reasonable.
        /// </summary>
        private const float BrokenTextureFraction = 0.70f; // 70% of model pixels must be broken to flag
        private const float WhiteThreshold = 0.85f;        // Per-channel threshold for "near white"
        private const float BlackThreshold = 0.15f;        // Per-channel threshold for "near black"
        private const float LowVarianceThreshold = 0.03f;  // Avg per-channel deviation for "flat color"

        private static string ValidateTextureQuality(Image image, out bool texturesBroken)
        {
            texturesBroken = false;
            int w = image.GetWidth();
            int h = image.GetHeight();
            if (w < 20 || h < 20) return null;

            var bgColor = SampleBackgroundColor(image);

            // Collect non-background pixel colors from center 50%
            int x0 = w / 4, x1 = w * 3 / 4;
            int y0 = h / 4, y1 = h * 3 / 4;

            int contentCount = 0;
            int nearWhite = 0;
            int nearBlack = 0;
            float sumR = 0, sumG = 0, sumB = 0;

            for (int y = y0; y < y1; y += SampleStep)
            {
                for (int x = x0; x < x1; x += SampleStep)
                {
                    var px = image.GetPixel(x, y);
                    if (!PixelDiffersFromBackground(px, bgColor)) continue;

                    contentCount++;
                    sumR += px.R; sumG += px.G; sumB += px.B;

                    if (px.R > WhiteThreshold && px.G > WhiteThreshold && px.B > WhiteThreshold)
                        nearWhite++;
                    if (px.R < BlackThreshold && px.G < BlackThreshold && px.B < BlackThreshold)
                        nearBlack++;
                }
            }

            if (contentCount < 10) return null; // Not enough content pixels to judge

            float whitePct = (float)nearWhite / contentCount;
            float blackPct = (float)nearBlack / contentCount;

            if (whitePct >= BrokenTextureFraction)
            {
                texturesBroken = true;
                return $"Model appears to have default white texture ({whitePct * 100:F0}% white pixels)";
            }
            if (blackPct >= BrokenTextureFraction)
            {
                texturesBroken = true;
                return $"Model appears to have missing/black texture ({blackPct * 100:F0}% black pixels)";
            }

            // Check for flat/untextured model (very low color variance)
            float avgR = sumR / contentCount, avgG = sumG / contentCount, avgB = sumB / contentCount;
            float varR = 0, varG = 0, varB = 0;
            int varCount = 0;
            for (int y = y0; y < y1; y += SampleStep * 2) // coarser sample for variance
            {
                for (int x = x0; x < x1; x += SampleStep * 2)
                {
                    var px = image.GetPixel(x, y);
                    if (!PixelDiffersFromBackground(px, bgColor)) continue;
                    varR += Math.Abs(px.R - avgR);
                    varG += Math.Abs(px.G - avgG);
                    varB += Math.Abs(px.B - avgB);
                    varCount++;
                }
            }
            if (varCount > 0)
            {
                float avgDev = (varR + varG + varB) / (varCount * 3);
                if (avgDev < LowVarianceThreshold)
                    return $"Model appears untextured — flat color with very low variance ({avgDev:F3})";
            }

            return null;
        }

        /// <summary>
        /// Compare two images and return a similarity score from 0.0 (completely different)
        /// to 1.0 (identical). Samples every Nth pixel for speed.
        /// </summary>
        private static float CompareImages(Image a, Image b)
        {
            int wA = a.GetWidth();
            int hA = a.GetHeight();
            int wB = b.GetWidth();
            int hB = b.GetHeight();

            // If sizes differ, they're obviously different
            if (wA != wB || hA != hB)
                return 0f;

            int totalSampled = 0;
            int sameCount = 0;

            for (int y = 0; y < hA; y += SampleStep)
            {
                for (int x = 0; x < wA; x += SampleStep)
                {
                    totalSampled++;
                    var pxA = a.GetPixel(x, y);
                    var pxB = b.GetPixel(x, y);

                    // If no channel differs by more than the threshold, consider them the same
                    if (Math.Abs(pxA.R - pxB.R) <= ChannelDiffThreshold
                        && Math.Abs(pxA.G - pxB.G) <= ChannelDiffThreshold
                        && Math.Abs(pxA.B - pxB.B) <= ChannelDiffThreshold)
                    {
                        sameCount++;
                    }
                }
            }

            if (totalSampled == 0) return 1f;
            return (float)sameCount / totalSampled;
        }

        /// <summary>
        /// Run all viewport validations on an image and return a combined warning string
        /// (null if everything looks good).
        /// </summary>
        private static string RunViewportValidations(Image image, Image previousImage, out bool isEmpty, out bool texturesBroken, out bool animStatic)
        {
            isEmpty = false;
            texturesBroken = false;
            animStatic = false;
            var warnings = new List<string>();

            // 1. Check viewport has content
            if (!ValidateViewportHasContent(image, out float contentPct))
            {
                isEmpty = true;
                warnings.Add($"Viewport appears empty — model may not be loading ({contentPct * 100:F1}% content)");
            }

            // 2. Check model not clipped (only if viewport has some content)
            if (!isEmpty && ValidateModelNotClipped(image, out string clipDetail))
            {
                warnings.Add(clipDetail);
            }

            // 3. Check texture quality (white/black/untextured model)
            if (!isEmpty)
            {
                var texWarning = ValidateTextureQuality(image, out texturesBroken);
                if (texWarning != null)
                    warnings.Add(texWarning);
            }

            // 4. Compare with previous image for animation change detection
            if (!isEmpty && previousImage != null)
            {
                float similarity = CompareImages(image, previousImage);
                if (similarity >= AnimSimilarityThreshold)
                {
                    animStatic = true;
                    warnings.Add($"Animation state change not visible ({similarity * 100:F1}% similar to previous)");
                }
            }

            return warnings.Count > 0 ? string.Join("; ", warnings) : null;
        }

        // ════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════

        private async Task Wait(float seconds)
        {
            await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        }

        /// <summary>
        /// Capture full viewport + SubViewport (if available) for a module.
        /// Validates SubViewport content if present.
        /// Returns the updated captureIndex.
        /// </summary>
        private async Task<int> CaptureWithViewport(EditorPanel module, string dir, string outputDir, int captureIndex, string tabName, string testName)
        {
            ShowProgress(tabName, testName, captureIndex, _estimatedTotal);
            EnsureDir(dir);

            // Full editor capture
            var fullPath = Path.Combine(dir, $"{captureIndex:D3}_{testName}_full.png");
            CaptureFullViewport(fullPath);

            _results.Add(new TestResult
            {
                Index = captureIndex,
                Tab = tabName,
                TestName = testName,
                PngPath = GetRelativePath(outputDir, fullPath),
                Success = true,
                Error = null,
                Warning = null
            });
            captureIndex++;

            // SubViewport capture with validation
            var subVp = module.TestGetViewport();
            if (subVp != null)
            {
                var vpPath = Path.Combine(dir, $"{captureIndex:D3}_{testName}_viewport.png");
                var vpImage = CaptureSubViewportImage(subVp, vpPath);

                string warning = null;
                bool vpFailed = false;
                string errorMsg = null;

                if (vpImage != null)
                {
                    warning = RunViewportValidations(vpImage, null, out bool vpEmpty, out bool texBroken, out bool _);
                    if (vpEmpty)
                    {
                        vpFailed = true;
                        errorMsg = "Viewport appears empty — model may not be loading";
                        GD.PrintErr($"[EditorTestRunner] FAIL: {tabName}/{testName} viewport appears empty");
                    }
                    else if (texBroken && warning != null && warning.Contains("white"))
                    {
                        vpFailed = true;
                        errorMsg = warning;
                        GD.PrintErr($"[EditorTestRunner] FAIL: {tabName}/{testName} broken textures: {warning}");
                    }
                    else if (texBroken)
                    {
                        // Dark models may be intentional (dark metallic materials). WARN not FAIL.
                        GD.Print($"[EditorTestRunner] WARN: {tabName}/{testName} dark textures: {warning}");
                    }
                    else if (warning != null)
                    {
                        GD.Print($"[EditorTestRunner] WARN: {tabName}/{testName}: {warning}");
                    }
                }

                _results.Add(new TestResult
                {
                    Index = captureIndex,
                    Tab = tabName,
                    TestName = $"{testName} (viewport)",
                    PngPath = GetRelativePath(outputDir, vpPath),
                    Success = !vpFailed,
                    Error = vpFailed ? errorMsg : null,
                    Warning = vpFailed ? null : warning
                });
                captureIndex++;
            }

            // Small yield to keep things stable
            await Wait(0.1f);
            return captureIndex;
        }

        /// <summary>
        /// Capture full + SubViewport for 2D UI modules (UI/UX) without 3D content validation.
        /// UI previews render 2D controls at screen edges, so center-sampling would always fail.
        /// </summary>
        private async Task<int> CaptureWithViewportNoValidation(EditorPanel module, string dir, string outputDir, int captureIndex, string tabName, string testName)
        {
            ShowProgress(tabName, testName, captureIndex, _estimatedTotal);
            EnsureDir(dir);

            var fullPath = Path.Combine(dir, $"{captureIndex:D3}_{testName}_full.png");
            CaptureFullViewport(fullPath);
            RecordSuccess(ref captureIndex, tabName, testName, outputDir, fullPath);

            var subVp = module.TestGetViewport();
            if (subVp != null)
            {
                var vpPath = Path.Combine(dir, $"{captureIndex:D3}_{testName}_viewport.png");
                CaptureSubViewportImage(subVp, vpPath);
                _results.Add(new TestResult
                {
                    Index = captureIndex,
                    Tab = tabName,
                    TestName = $"{testName} (viewport)",
                    PngPath = GetRelativePath(outputDir, vpPath),
                    Success = true,
                    Error = null,
                    Warning = null
                });
                captureIndex++;
            }

            await Wait(0.1f);
            return captureIndex;
        }

        /// <summary>
        /// Enhanced capture for combatant phase: validates viewport content, compares with
        /// previous image for animation change detection, and uses actual combatant ID.
        /// Uses _previousViewportImage field for animation comparison (reset per combatant).
        /// </summary>
        private async Task<int> CaptureWithViewportValidated(
            EditorPanel module, string dir, string outputDir, int captureIndex,
            string tabName, string testName, string extraWarning)
        {
            ShowProgress(tabName, testName, captureIndex, _estimatedTotal);
            EnsureDir(dir);

            // Full editor capture
            var fullPath = Path.Combine(dir, $"{captureIndex:D3}_{testName}_full.png");
            CaptureFullViewport(fullPath);

            _results.Add(new TestResult
            {
                Index = captureIndex,
                Tab = tabName,
                TestName = testName,
                PngPath = GetRelativePath(outputDir, fullPath),
                Success = true,
                Error = null,
                Warning = extraWarning
            });
            captureIndex++;

            // SubViewport capture with full validation
            var subVp = module.TestGetViewport();
            if (subVp != null)
            {
                var vpPath = Path.Combine(dir, $"{captureIndex:D3}_{testName}_viewport.png");
                var vpImage = CaptureSubViewportImage(subVp, vpPath);

                string warning = null;
                bool vpFailed = false;
                string errorMsg = null;

                if (vpImage != null)
                {
                    warning = RunViewportValidations(vpImage, _previousViewportImage, out bool vpEmpty, out bool texBroken, out bool animStatic);
                    if (vpEmpty)
                    {
                        vpFailed = true;
                        errorMsg = "Viewport appears empty — model may not be loading";
                        GD.PrintErr($"[EditorTestRunner] FAIL: {tabName}/{testName} viewport appears empty");
                    }
                    else if (texBroken && warning != null && warning.Contains("white"))
                    {
                        vpFailed = true;
                        errorMsg = warning;
                        GD.PrintErr($"[EditorTestRunner] FAIL: {tabName}/{testName} broken textures: {warning}");
                    }
                    else if (texBroken)
                    {
                        // Dark models may be intentional (e.g. AXIS boss dark metallic). WARN not FAIL.
                        GD.Print($"[EditorTestRunner] WARN: {tabName}/{testName} dark textures: {warning}");
                    }
                    else if (animStatic)
                    {
                        // Animation state comparison is informational — procedural models
                        // use runtime tweening (not skeletal AnimationPlayer) so frame-to-frame
                        // captures can look identical. Treat as WARN, not FAIL.
                        GD.Print($"[EditorTestRunner] WARN: {tabName}/{testName} animation static: {warning}");
                    }
                    else if (warning != null)
                    {
                        GD.Print($"[EditorTestRunner] WARN: {tabName}/{testName}: {warning}");
                    }

                    // Combine extra warning (e.g. name mismatch) with validation warnings
                    if (extraWarning != null)
                    {
                        warning = warning != null ? $"{extraWarning}; {warning}" : extraWarning;
                    }

                    // Store for next comparison
                    _previousViewportImage = vpImage;
                }

                _results.Add(new TestResult
                {
                    Index = captureIndex,
                    Tab = tabName,
                    TestName = $"{testName} (viewport)",
                    PngPath = GetRelativePath(outputDir, vpPath),
                    Success = !vpFailed,
                    Error = vpFailed ? errorMsg : null,
                    Warning = vpFailed ? null : warning
                });
                captureIndex++;
            }

            await Wait(0.1f);
            return captureIndex;
        }

        private void RecordSuccess(ref int captureIndex, string tab, string testName, string outputDir, string fullPath)
        {
            ShowProgress(tab, testName, captureIndex, _estimatedTotal);
            _results.Add(new TestResult
            {
                Index = captureIndex,
                Tab = tab,
                TestName = testName,
                PngPath = GetRelativePath(outputDir, fullPath),
                Success = true,
                Error = null,
                Warning = null
            });
            captureIndex++;
        }

        private void RecordFailure(ref int captureIndex, string tab, string testName, string error)
        {
            _results.Add(new TestResult
            {
                Index = captureIndex,
                Tab = tab,
                TestName = testName,
                PngPath = "",
                Success = false,
                Error = error,
                Warning = null
            });
            captureIndex++;
        }

        private void CaptureFullViewport(string path)
        {
            var image = GetViewport().GetTexture().GetImage();
            if (image == null)
                throw new InvalidOperationException("GetViewport().GetTexture().GetImage() returned null");

            EnsureDir(Path.GetDirectoryName(path));
            image.SavePng(path);
        }

        private void CaptureSubViewport(SubViewport viewport, string path)
        {
            var image = viewport.GetTexture()?.GetImage();
            if (image == null)
                throw new InvalidOperationException("SubViewport image capture returned null");

            EnsureDir(Path.GetDirectoryName(path));
            image.SavePng(path);
        }

        /// <summary>
        /// Capture SubViewport image, save to disk, and return the Image for validation.
        /// Returns null if capture fails.
        /// </summary>
        private Image CaptureSubViewportImage(SubViewport viewport, string path)
        {
            var image = viewport.GetTexture()?.GetImage();
            if (image == null)
            {
                GD.PrintErr($"[EditorTestRunner] SubViewport image capture returned null for {path}");
                return null;
            }

            EnsureDir(Path.GetDirectoryName(path));
            image.SavePng(path);
            return image;
        }

        private static EditorPanel FindModule(IReadOnlyList<EditorPanel> modules, string name)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].PanelName == name)
                    return modules[i];
            }
            return null;
        }

        private static void EnsureDir(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static string SanitizePath(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            basePath = basePath.TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(basePath.Length).Replace('\\', '/');
            return fullPath.Replace('\\', '/');
        }

        private void GenerateReport(string outputDir, string timestamp)
        {
            int passed = 0;
            int failed = 0;
            int warned = 0;
            foreach (var r in _results)
            {
                if (r.Success) passed++;
                else failed++;
                if (!string.IsNullOrEmpty(r.Warning)) warned++;
            }

            var lines = new List<string>();
            lines.Add($"# Editor Test Report — {timestamp}");
            lines.Add("");
            lines.Add($"**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            lines.Add($"**Total:** {_results.Count} | **Passed:** {passed} | **Failed:** {failed} | **Warnings:** {warned}");
            lines.Add("");
            lines.Add("## Validation");
            lines.Add("");
            lines.Add("Screenshots are now validated for visual content:");
            lines.Add("- **Empty viewport detection:** Fails if <2% of center pixels differ from background");
            lines.Add("- **Floor clipping detection:** Warns if model content exists only in bottom 20%");
            lines.Add("- **Animation change detection:** Warns if consecutive animation states are >95% identical");
            lines.Add("- **Combatant ID verification:** Warns if expected combatant name mismatches actual selection");
            lines.Add("");
            lines.Add("## Phases");
            lines.Add("");
            lines.Add("| Phase | Description | Tests |");
            lines.Add("|-------|-------------|-------|");
            lines.Add("| 1 | Tab smoke test | All tabs |");
            lines.Add("| 2 | Characters — bot frames | 6 frames |");
            lines.Add("| 3 | Characters — growth tiers | 5 tiers |");
            lines.Add("| 4 | Combatants — enemies/bosses | 19 combatants + 4 anims |");
            lines.Add("| 5 | Balance — sub-tabs | 6 sub-tabs |");
            lines.Add("| 6 | Dungeon — generation | Generate + layout + collision |");
            lines.Add("| 7 | Assets — categories | 5 categories x 3 assets + collision |");
            lines.Add("| 8 | VFX — effects | 6 effects + color preset |");
            lines.Add("| 9 | Strings — filters | 5 view filters |");
            lines.Add("| 10 | Sound — categories | 6 audio categories |");
            lines.Add("| 11 | Sectors — table | Table + 5 sectors |");
            lines.Add("| 12 | UI/UX — screens | 3 screens + layout toggle |");
            lines.Add("");
            lines.Add("## Results");
            lines.Add("");
            lines.Add("| # | Tab | Test | Status | Warning | Screenshot |");
            lines.Add("|---|-----|------|--------|---------|------------|");

            foreach (var r in _results)
            {
                var status = r.Success ? "PASS" : "FAIL";
                var screenshot = !string.IsNullOrEmpty(r.PngPath) ? $"[{r.PngPath}]({r.PngPath})" : "—";
                var error = r.Success ? "" : $" ({r.Error})";
                var warning = !string.IsNullOrEmpty(r.Warning) ? r.Warning : "";
                lines.Add($"| {r.Index} | {r.Tab} | {r.TestName} | {status}{error} | {warning} | {screenshot} |");
            }

            // Summary of warnings if any
            if (warned > 0)
            {
                lines.Add("");
                lines.Add("## Warnings Summary");
                lines.Add("");
                foreach (var r in _results)
                {
                    if (!string.IsNullOrEmpty(r.Warning))
                    {
                        lines.Add($"- **{r.Tab}/{r.TestName}:** {r.Warning}");
                    }
                }
            }

            lines.Add("");
            lines.Add("---");
            lines.Add($"*Generated by EditorTestRunner at {DateTime.Now:HH:mm:ss}*");

            var reportPath = Path.Combine(outputDir, "report.md");
            File.WriteAllText(reportPath, string.Join("\n", lines));
            GD.Print($"[EditorTestRunner] Report written to {reportPath}");
        }

        private void GenerateJsonReport(string outputDir, string timestamp)
        {
            int passed = 0;
            int failed = 0;
            int warned = 0;
            foreach (var r in _results)
            {
                if (r.Success) passed++;
                else failed++;
                if (!string.IsNullOrEmpty(r.Warning)) warned++;
            }

            var dateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"timestamp\": \"{EscapeJson(timestamp)}\",");
            sb.AppendLine($"  \"date\": \"{EscapeJson(dateStr)}\",");
            sb.AppendLine($"  \"total\": {_results.Count},");
            sb.AppendLine($"  \"passed\": {passed},");
            sb.AppendLine($"  \"failed\": {failed},");
            sb.AppendLine($"  \"warnings\": {warned},");

            // phases_run
            sb.Append("  \"phases_run\": [");
            for (int i = 0; i < _phasesRun.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"\"{EscapeJson(_phasesRun[i])}\"");
            }
            sb.AppendLine("],");

            // results array
            sb.AppendLine("  \"results\": [");
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                var status = r.Success ? "PASS" : "FAIL";
                sb.Append("    {");
                sb.Append($" \"index\": {r.Index},");
                sb.Append($" \"tab\": \"{EscapeJson(r.Tab)}\",");
                sb.Append($" \"test\": \"{EscapeJson(r.TestName)}\",");
                sb.Append($" \"status\": \"{status}\",");
                sb.Append($" \"warning\": {JsonStringOrNull(r.Warning)},");
                sb.Append($" \"error\": {JsonStringOrNull(r.Error)},");
                sb.Append($" \"screenshot\": {JsonStringOrNull(r.PngPath)}");
                sb.Append(" }");
                if (i < _results.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");

            // failures array
            sb.AppendLine("  \"failures\": [");
            bool firstFailure = true;
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                if (r.Success) continue;
                if (!firstFailure) sb.AppendLine(",");
                firstFailure = false;
                sb.Append($"    {{ \"index\": {r.Index}, \"tab\": \"{EscapeJson(r.Tab)}\", \"test\": \"{EscapeJson(r.TestName)}\", \"error\": {JsonStringOrNull(r.Error)} }}");
            }
            if (!firstFailure) sb.AppendLine();
            sb.AppendLine("  ],");

            // warnings_list array
            sb.AppendLine("  \"warnings_list\": [");
            bool firstWarning = true;
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                if (string.IsNullOrEmpty(r.Warning)) continue;
                if (!firstWarning) sb.AppendLine(",");
                firstWarning = false;
                sb.Append($"    {{ \"index\": {r.Index}, \"tab\": \"{EscapeJson(r.Tab)}\", \"test\": \"{EscapeJson(r.TestName)}\", \"warning\": \"{EscapeJson(r.Warning)}\" }}");
            }
            if (!firstWarning) sb.AppendLine();
            sb.AppendLine("  ]");

            sb.AppendLine("}");

            var jsonPath = Path.Combine(outputDir, "results.json");
            File.WriteAllText(jsonPath, sb.ToString());
            GD.Print($"[EditorTestRunner] JSON report written to {jsonPath}");
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static string JsonStringOrNull(string s)
        {
            if (string.IsNullOrEmpty(s)) return "null";
            return $"\"{EscapeJson(s)}\"";
        }
    }
}
