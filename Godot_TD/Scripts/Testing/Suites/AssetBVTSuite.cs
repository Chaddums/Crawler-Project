using System.Threading.Tasks;

namespace JunkyardTD
{
    /// <summary>
    /// CI-runnable BVT suite. Wraps BVTRunner for TestHarness execution.
    /// Run: godot --test-harness --suite=bvt
    ///
    /// Category K (Known Issues) failures are logged but don't fail the suite —
    /// they're expected to fail until someone fixes them.
    /// All other FAILs and WARNs are real failures.
    /// </summary>
    public class AssetBVTSuite : ITestSuite
    {
        public string SuiteName => "bvt";

        public async Task Run(TestContext ctx)
        {
            // Pass null for tree — headless skips model-loading checks (B, F categories)
            var results = BVTRunner.RunAll(tree: null);

            foreach (var r in results)
            {
                ctx.StartTest();

                if (r.Category == "K")
                {
                    // Known issues: log them but don't fail the suite
                    // When someone fixes one, it flips to PASS — that's the signal
                    ctx.Assert(true, r.TestName,
                        r.Status == BVTStatus.Fail ? $"[KNOWN] {r.Message}" :
                        r.Status == BVTStatus.Pass ? "[FIXED]" : "");
                }
                else
                {
                    // Everything else: FAIL and WARN are real failures
                    bool passes = r.Status == BVTStatus.Pass;
                    ctx.Assert(passes, r.TestName, r.Message);
                }
            }

            await Task.CompletedTask;
        }
    }
}
