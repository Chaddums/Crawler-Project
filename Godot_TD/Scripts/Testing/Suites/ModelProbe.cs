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
            var scene = ctx.Tree.CurrentScene;
            for (int i = 0; i < paths.Length; i++)
            {
                var m = AssetLibrary.InstantiateToHeight(paths[i], 3.5f);
                if (m == null) { GD.Print($"[Probe] {paths[i]} missing"); continue; }
                scene.AddChild(m);
                AssetLibrary.GroundModel(m);
                var w = grid.GridToWorld(row[1 + i * 2]);
                m.GlobalPosition = new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z) + m.Position.Y, w.Z);
                m.RotationDegrees = new Vector3(0, AssetLibrary.GetFacingYawOffset(paths[i]) * 57.3f, 0);
                var l = new Label3D { Text = paths[i].Split('/').Last(), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, PixelSize = 0.01f };
                scene.AddChild(l);
                l.GlobalPosition = new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z) + 4.3f, w.Z);
                var ap = FindAp(m);
                if (ap != null && ap.GetAnimationList().Length > 0) ap.Play(ap.GetAnimationList()[0]);
                GD.Print($"[Probe] {paths[i]}: anims {(ap == null ? "none" : string.Join(",", ap.GetAnimationList()))}");
            }
            foreach (var l in All<CanvasLayer>(ctx.Tree.Root)) l.Visible = false;
            var cam = new Camera3D { Fov = 40f };
            scene.AddChild(cam);
            cam.Current = true;
            var mid = (grid.GridToWorld(row[1]) + grid.GridToWorld(row[13])) * 0.5f;
            mid.Y = grid.GetWorldHeight(mid.X, mid.Z);
            cam.GlobalPosition = mid + new Vector3(0, 9f, 22f);
            cam.LookAt(mid + Vector3.Up * 1.8f, Vector3.Up);
            await ctx.Wait(0.6f);
            for (int i = 0; i < 3; i++) await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var dir = ProjectSettings.GlobalizePath("res://test-reports/probe");
            System.IO.Directory.CreateDirectory(dir);
            ctx.Tree.Root.GetTexture().GetImage().SavePng($"{dir}/ascendant_models.png");
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
