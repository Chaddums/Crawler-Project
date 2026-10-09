using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Diagnostic: how BIT's Run segment of the baked clip moves. Prints the pose change rate
    /// across the segment and the best loop windows (start and end poses that match, with
    /// matching motion), so the Run clip can loop one clean stride cycle. Headless.
    /// </summary>
    public class RunLoopProbe : ITestSuite
    {
        public string SuiteName => "runloop";

        public async Task Run(TestContext ctx)
        {
            ctx.StartTest();
            var model = AssetLibrary.InstantiateNormalized(AssetLibrary.COMPANION_BIT);
            ctx.Tree.Root.AddChild(model);
            await ctx.Wait(0.2f);
            var ap = FindAp(model);
            Animation src = null;
            foreach (var n in ap.GetAnimationList())
                if (n.ToLower().Contains("action") && !n.ToLower().Contains("poselib")) { src = ap.GetAnimation(n); break; }
            ctx.Assert(src != null, "runloop/source");
            if (src == null) return;
            var rotTracks = new List<int>();
            for (int t = 0; t < src.GetTrackCount(); t++)
                if (src.TrackGetType(t) == Animation.TrackType.Rotation3D && src.TrackGetKeyCount(t) > 1) rotTracks.Add(t);
            GD.Print($"[RunLoop] {rotTracks.Count} rotation tracks: {string.Join(", ", rotTracks.Select(t => src.TrackGetPath(t).ToString().Split(':').Last()))}");
            Quaternion[] Pose(double time) => rotTracks.Select(t => src.RotationTrackInterpolate(t, time)).ToArray();
            float Dist(Quaternion[] a, Quaternion[] b)
            {
                float s = 0;
                for (int i = 0; i < a.Length; i++) s += Mathf.RadToDeg(a[i].AngleTo(b[i]));
                return s;
            }
            const double step = 1.0 / 60.0;
            var times = new List<double>();
            for (double t = 3.0; t <= 4.3; t += step) times.Add(t);
            var poses = times.Select(Pose).ToList();
            // Pose change rate (degrees per frame, summed over bones)
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i < times.Count; i++)
            {
                if (times[i] < 3.3 || times[i] > 3.95) continue;
                sb.Append($"{times[i]:F2}:{Dist(poses[i], poses[i - 1]):F1} ");
            }
            GD.Print($"[RunLoop] rate: {sb}");
            // Best windows inside 3.17..4.13: pose(a)~pose(b) and motion(a)~motion(b)
            var cands = new List<(double a, double b, float score)>();
            for (int i = 1; i < times.Count - 1; i++)
            {
                if (times[i] < 3.15 || times[i] > 3.75) continue;
                for (int j = i + 18; j < times.Count - 1; j++)
                {
                    if (times[j] > 4.14) break;
                    float pd = Dist(poses[i], poses[j]);
                    // motion: compare deltas
                    float md = 0;
                    var da = poses[i + 1]; var db = poses[j + 1];
                    for (int k = 0; k < da.Length; k++)
                    {
                        var va = poses[i][k].Inverse() * da[k];
                        var vb = poses[j][k].Inverse() * db[k];
                        md += Mathf.RadToDeg(va.AngleTo(vb));
                    }
                    cands.Add((times[i], times[j], pd + md * 4f));
                }
            }
            foreach (var c in cands.OrderBy(c => c.score).Take(12))
                GD.Print($"[RunLoop] window {c.a:F3}..{c.b:F3} ({c.b - c.a:F3} s) score {c.score:F1}");
            // The current loop: 3.17 -> 4.13 seam
            GD.Print($"[RunLoop] current seam 3.17..4.13: pose diff {Dist(Pose(3.17), Pose(4.12)):F1}");
            model.QueueFree();
        }

        private static AnimationPlayer FindAp(Node n)
        {
            if (n is AnimationPlayer ap) return ap;
            foreach (var c in n.GetChildren()) { var r = FindAp(c); if (r != null) return r; }
            return null;
        }
    }
}
