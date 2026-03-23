using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Renders each tower/prop model in an isolated viewport and saves PNGs.
    /// Run: godot --path . -- --test-harness --suite=asset-preview
    /// Output: test-reports/asset-previews/*.png
    /// </summary>
    public class AssetPreviewSuite : ITestSuite
    {
        public string SuiteName => "asset-preview";

        public async Task Run(TestContext ctx)
        {
            GD.Print("[AssetPreviewSuite] Capturing all asset previews...");
            await VisualTestSuite.CaptureAllAssetPreviews(ctx);
            GD.Print("[AssetPreviewSuite] Complete.");
        }
    }
}
