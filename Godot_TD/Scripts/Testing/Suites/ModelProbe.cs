using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>Diagnostic: candidate models for the Ascendants, 3.5 units tall, side by side in a battle.</summary>
    public class ModelProbe : ITestSuite
    {
        public string SuiteName => "modelprobe";

        public async Task Run(TestContext ctx)
        {
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "modelprobe/battle");
            if (!ok) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var row = TowerSheetSuite.FindRow(grid, 15);
            string[] paths = { "res://Models/Robot Warriors/robot_1.glb", AssetLibrary.PLAYER_GUN_ROBOT, AssetLibrary.PLAYER_CLUNKER,
                AssetLibrary.PLAYER_RUSTBUCKET, AssetLibrary.PLAYER_SPARKPLUG, AssetLibrary.ENEMY_GRUNT_MECH, AssetLibrary.ENEMY_QUAD_SHELL };
            // PROBE_PATHS=a,b,c (res:// paths), PROBE_HEIGHT, PROBE_CLIP (part of a clip name), PROBE_OUT (file name)
            string envPaths = System.Environment.GetEnvironmentVariable("PROBE_PATHS");
            if (!string.IsNullOrEmpty(envPaths)) paths = envPaths.Split(',');
            float height = float.TryParse(System.Environment.GetEnvironmentVariable("PROBE_HEIGHT"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hh) ? hh : 3.5f;
            string clip = System.Environment.GetEnvironmentVariable("PROBE_CLIP");
            string outName = System.Environment.GetEnvironmentVariable("PROBE_OUT") ?? "ascendant_models";
            var scene = ctx.Tree.CurrentScene;
            for (int i = 0; i < paths.Length; i++)
            {
                var m = AssetLibrary.InstantiateToHeight(paths[i], height);
                if (m == null) { GD.Print($"[Probe] {paths[i]} missing"); continue; }
                scene.AddChild(m);
                AssetLibrary.ApplyPlayerTexture(m, paths[i]);
                AssetLibrary.GroundModel(m);
                var w = grid.GridToWorld(row[1 + i * 2]);
                m.GlobalPosition = new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z) + m.Position.Y, w.Z);
                m.RotationDegrees = new Vector3(0, AssetLibrary.GetFacingYawOffset(paths[i]) * 57.3f, 0);
                var l = new Label3D { Text = paths[i].Split('/').Last(), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, PixelSize = 0.01f };
                scene.AddChild(l);
                l.GlobalPosition = new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z) + height + 0.8f, w.Z);
                var ap = FindAp(m);
                if (ap != null && ap.GetAnimationList().Length > 0)
                {
                    var list = ap.GetAnimationList();
                    string pick = clip != null ? list.FirstOrDefault(a => a.ToLower().Contains(clip.ToLower())) : null;
                    ap.Play(pick ?? list[0]);
                    if (pick != null) ap.Seek(ap.CurrentAnimationLength * 0.4f, true);
                }
                GD.Print($"[Probe] {paths[i]}: anims {(ap == null ? "none" : string.Join(",", ap.GetAnimationList()))}");
            }
            foreach (var l in All<CanvasLayer>(ctx.Tree.Root)) l.Visible = false;
            var cam = new Camera3D { Fov = 40f };
            scene.AddChild(cam);
            cam.Current = true;
            var first = grid.GridToWorld(row[1]); var last = grid.GridToWorld(row[1 + (paths.Length - 1) * 2]);
            var mid = (first + last) * 0.5f;
            mid.Y = grid.GetWorldHeight(mid.X, mid.Z);
            float span = Mathf.Max(first.DistanceTo(last), height * 2f);
            cam.GlobalPosition = mid + new Vector3(0, 0.45f, 1f).Normalized() * span * 0.95f;
            cam.LookAt(mid + Vector3.Up * height * 0.5f, Vector3.Up);
            // Even light so the bodies read (the battle's night lighting hides them)
            var key = new DirectionalLight3D { LightEnergy = 1.4f, ShadowEnabled = true };
            scene.AddChild(key);
            key.GlobalRotationDegrees = new Vector3(-40f, 25f, 0f);
            await ctx.Wait(0.6f);
            for (int i = 0; i < 3; i++) await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var dir = ProjectSettings.GlobalizePath("res://test-reports/probe");
            System.IO.Directory.CreateDirectory(dir);
            ctx.Tree.Root.GetTexture().GetImage().SavePng($"{dir}/{outName}.png");
        }

        private static AnimationPlayer FindAp(Node n)
        {
            if (n is AnimationPlayer ap) return ap;
            foreach (var c in n.GetChildren()) { var r = FindAp(c); if (r != null) return r; }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<T> All<T>(Node n) where T : Node
        {
            if (n is T t) yield return t;
            foreach (var c in n.GetChildren()) foreach (var x in All<T>(c)) yield return x;
        }
    }
}
