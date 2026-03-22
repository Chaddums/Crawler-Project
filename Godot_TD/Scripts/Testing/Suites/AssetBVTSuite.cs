using System.Threading.Tasks;

namespace JunkyardTD
{
    /// <summary>
    /// CI-runnable BVT suite. Wraps BVTRunner for TestHarness execution.
    /// Run: godot --test-harness --suite=bvt
    /// WARNs pass (informational), FAILs fail the suite.
    /// Scene-dependent checks (B, F) skipped in headless mode.
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
                ctx.Assert(
                    r.Status != BVTStatus.Fail,
                    r.TestName,
                    r.Status == BVTStatus.Fail ? r.Message :
                    r.Status == BVTStatus.Warn ? $"[WARN] {r.Message}" : ""
                );
            }

            await Task.CompletedTask;
        }
    }
}
