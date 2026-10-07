using System;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Autoload entry point for the test harness.
    /// Parses --test-harness --suite=X --request-id=Y from command line.
    /// If --test-harness is absent, QueueFree() immediately.
    /// </summary>
    public partial class TestHarness : Node
    {
        private string _suite = "all";
        private string _requestId;

        public override void _Ready()
        {
            var args = OS.GetCmdlineUserArgs();
            bool isTestRun = false;

            foreach (var arg in args)
            {
                if (arg == "--test-harness")
                    isTestRun = true;
                else if (arg.StartsWith("--suite="))
                    _suite = arg.Substring("--suite=".Length);
                else if (arg.StartsWith("--request-id="))
                    _requestId = arg.Substring("--request-id=".Length);
            }

            if (!isTestRun)
            {
                QueueFree();
                return;
            }

            if (string.IsNullOrEmpty(_requestId))
                _requestId = $"test_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{GD.Randi() % 10000:D4}";

            GD.Print($"[TestHarness] Starting suite={_suite} request={_requestId}");
            CallDeferred(nameof(RunTests));
        }

        private async void RunTests()
        {
            // Wait one frame for other autoloads to initialize
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var ctx = new TestContext(GetTree());
            int exitCode = 0;

            try
            {
                var suites = ResolveSuites(_suite);
                if (suites == null)
                {
                    GD.PrintErr($"[TestHarness] Unknown suite '{_suite}'. Valid: content, ui, gameplay, visual, " +
                                "integration, editor, bvt, asset-preview, maps, relics, flow, perf, maze, input, screens, all");
                    ctx.Assert(false, "harness.unknown_suite", $"Unknown suite '{_suite}'");
                    suites = System.Array.Empty<ITestSuite>();
                }
                foreach (var suite in suites)
                {
                    GD.Print($"\n[TestHarness] ═══ Running: {suite.SuiteName} ═══");
                    ctx.BeginEventTracking();
                    await suite.Run(ctx);
                }

                ctx.WriteResults(_requestId, _suite);

                // Determine exit code
                foreach (var result in ctx.Results)
                {
                    if (!result.Passed)
                    {
                        exitCode = 1;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[TestHarness] CRASH: {e.Message}\n{e.StackTrace}");
                exitCode = 2;

                // Write partial results
                try { ctx.WriteResults(_requestId, _suite); } catch { }
            }

            GD.Print($"[TestHarness] Exiting with code {exitCode}");
            await TearDownBeforeQuit();
            GetTree().Quit(exitCode);
        }

        private System.Threading.Tasks.Task TearDownBeforeQuit() => HeadlessShutdown.TearDown(this);

        private ITestSuite[] ResolveSuites(string name)
        {
            return name.ToLower() switch
            {
                "content" => new ITestSuite[] { new ContentTestSuite() },
                "ui" => new ITestSuite[] { new UITestSuite() },
                "gameplay" => new ITestSuite[] { new GameplayTestSuite() },
                "visual" => new ITestSuite[] { new VisualTestSuite() },
                "integration" => new ITestSuite[] { new IntegrationTestSuite() },
                "editor" => new ITestSuite[] { new EditorTestSuite() },
                "bvt" => new ITestSuite[] { new AssetBVTSuite() },
                "asset-preview" => new ITestSuite[] { new AssetPreviewSuite() },
                "map-validation" or "maps" => new ITestSuite[] { new MapValidationSuite() },
                "relics" => new ITestSuite[] { new RelicTestSuite() },
                "flow" => new ITestSuite[] { new RunFlowTestSuite() },
                "perf" => new ITestSuite[] { new PerfTestSuite() },
                "planets" => new ITestSuite[] { new PlanetsTestSuite() },
                "anim" => new ITestSuite[] { new AnimationTestSuite() },
                "textures" => new ITestSuite[] { new TextureBudgetSuite() },
                "fidelity" => new ITestSuite[] { new FidelityTestSuite() },
                "fidelity-sheets" => new ITestSuite[] { new FidelityTestSuite(sheets: true) }, // needs a display; not in "all"
                "anim-sheets" => new ITestSuite[] { new AnimationTestSuite(sheets: true) }, // needs a display; not in "all"
                "maze" => new ITestSuite[] { new MazeTestSuite() },
                "input" => new ITestSuite[] { new InputTestSuite() },
                "screens" => new ITestSuite[] { new ScreenshotTourSuite() }, // needs a display; not in "all"
                "all" => new ITestSuite[]
                {
                    new AssetBVTSuite(),
                    new ContentTestSuite(),
                    new EditorTestSuite(),
                    new UITestSuite(),
                    new GameplayTestSuite(),
                    new MapValidationSuite(),
                    new RelicTestSuite(),
                    new RunFlowTestSuite(),
                    new PerfTestSuite(),
                    new PlanetsTestSuite(),
                    new AnimationTestSuite(),
                    new TextureBudgetSuite(),
                    new MazeTestSuite(),
                    new InputTestSuite(),
                    new VisualTestSuite(),
                    new IntegrationTestSuite()
                },
                // Unknown suite names used to fall back to ContentTestSuite and report green
                _ => null
            };
        }
    }
}
