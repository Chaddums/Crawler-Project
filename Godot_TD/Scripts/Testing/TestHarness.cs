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
            GetTree().Quit(exitCode);
        }

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
                "all" => new ITestSuite[]
                {
                    new AssetBVTSuite(),
                    new ContentTestSuite(),
                    new EditorTestSuite(),
                    new UITestSuite(),
                    new GameplayTestSuite(),
                    new MapValidationSuite(),
                    new VisualTestSuite(),
                    new IntegrationTestSuite()
                },
                _ => new ITestSuite[] { new ContentTestSuite() }
            };
        }
    }
}
