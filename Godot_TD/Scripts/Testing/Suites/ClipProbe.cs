using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Renders every clip of one model, four poses each, to test-reports/anim/clips_&lt;name&gt;.png
    /// (CLIP_MODEL = res:// path, CLIP_HEIGHT = height to fit, default 1.3). Needs a display.
    /// For finding which clip a pack really uses for walking when the names lie.
    /// </summary>
    public class ClipProbe : ITestSuite
    {
        public string SuiteName => "clipprobe";

        public async Task Run(TestContext ctx)
        {
            string path = OS.GetEnvironment("CLIP_MODEL");
            float h = float.TryParse(OS.GetEnvironment("CLIP_HEIGHT"), out var hh) ? hh : 1.3f;
            ctx.StartTest();
            if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path)) { ctx.Assert(false, "clipprobe/model", path ?? "CLIP_MODEL not set"); return; }
            ctx.Tree.UnloadCurrentScene();
            await AnimationTestSuite.Frames(ctx, 2);
            var stage = new Node3D { Name = "AnimStage" };
            ctx.Tree.Root.AddChild(stage);
            AnimationTestSuite.BuildStudio(stage);
            var holder = new Node3D();
            stage.AddChild(holder);
            var model = AssetLibrary.InstantiateToHeight(path, 1.3f);
            holder.AddChild(model);
            AssetLibrary.GroundModel(model);
            AssetLibrary.ApplyPlayerTexture(model, path);
            var ap = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
            if (ap == null) { ctx.Assert(false, "clipprobe/player", "no AnimationPlayer"); return; }
            var clips = ap.GetAnimationList().Where(c => c != "RESET").ToArray();
            const int cell = 240, cols = 5;
            var sheet = Image.CreateEmpty(cell * cols, cell * clips.Length, false, Image.Format.Rgba8);
            for (int r = 0; r < clips.Length; r++)
            {
                var a = ap.GetAnimation(clips[r]);
                for (int c = 0; c < cols; c++)
                {
                    ap.Play(clips[r]); ap.Pause(); ap.Seek(a.Length * c / (cols - 1), true);
                    await AnimationTestSuite.Frames(ctx, 1);
                    sheet.BlitRect(await AnimationTestSuite.Grab(ctx, cell, cell), new Rect2I(0, 0, cell, cell), new Vector2I(c * cell, r * cell));
                }
                GD.Print($"[ClipProbe] row {r}: {clips[r]} ({a.Length:0.00}s)");
            }
            string dir = ProjectSettings.GlobalizePath("res://test-reports/anim");
            System.IO.Directory.CreateDirectory(dir);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            sheet.SavePng($"{dir}/clips_{name}.png");
            ctx.Assert(true, "clipprobe/rendered", $"{clips.Length} clips");
        }
    }
}
