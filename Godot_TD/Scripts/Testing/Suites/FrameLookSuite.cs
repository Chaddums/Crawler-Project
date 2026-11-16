using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Probe (not in "all"): is each of BIT's frames moving? For BIT on foot and in each frame,
    /// standing, walking and shooting, it samples every bone's pose ten times over a second and a
    /// half and logs the clip playing, whether it advances, and how far the most-moved bone
    /// turned and travelled ("[FrameLook] titan shooting: clip Idle ... bones turned 0.0 deg" is a
    /// frame standing frozen while it fires). With a display it also saves six frames of each,
    /// close on BIT, to FRAME_LOOK (default test-reports/frame_look) for looking at side by side.
    /// </summary>
    public class FrameLookSuite : ITestSuite
    {
        public string SuiteName => "framelook";

        public async Task Run(TestContext ctx)
        {
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "framelook/battle_loads");
            if (!ok) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            var grid = ServiceLocator.Get<VineGrid>();
            var bit = ServiceLocator.Get<VinePlayer>();
            grid.Harvester?.IncreaseMaxHP(100000f);
            await ctx.Wait(2.5f);
            string dir = OS.GetEnvironment("FRAME_LOOK") is { Length: > 0 } d ? d : ProjectSettings.GlobalizePath("res://test-reports/frame_look");
            bool shoot = DisplayServer.GetName() != "headless";
            if (shoot) System.IO.Directory.CreateDirectory(dir);
            var sheet = MechSheet.Load(Constants.PLAYER_MECH_ID);
            var cam = ctx.Tree.Root.GetCamera3D();

            var stages = new List<(string Name, int Level)> { ("on_foot", 1) };
            stages.AddRange(sheet.Frames.Select(f => (f.Id, f.Level)));
            foreach (var (name, level) in stages)
            {
                bit.Look.SetLevel(level, animate: false);
                await ctx.Wait(0.6f);
                // Standing
                bit.BotMove = Vector3.Zero; bit.BotAim(null);
                GameSettings.BitAutoFire = false;
                await Measure(ctx, bit, cam, name, "standing", dir, shoot);
                // Walking (in a small square, so it stays where it is)
                await Measure(ctx, bit, cam, name, "walking", dir, shoot, walk: true);
                bit.BotMove = Vector3.Zero;
                await ctx.Wait(0.4f);
                // Shooting: something that won't die, a few metres off, auto-fire on
                var at = bit.GlobalPosition + new Vector3(4f, 0, 0);
                var foe = new VineEnemy();
                ctx.Tree.CurrentScene.AddChild(foe);
                foe.Initialize("Dummy", VineEnemyFaction.Scavenger, 1e8f, 0f, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(at));
                foe.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
                GameSettings.BitAutoFire = true;
                await ctx.Wait(0.3f);
                await Measure(ctx, bit, cam, name, "shooting", dir, shoot);
                // And with the mouse held on it (aimed fire)
                bit.BotAim(foe.GlobalPosition);
                await Measure(ctx, bit, cam, name, "aiming", dir, shoot);
                bit.BotAim(null);
                foe.QueueFree();
                GameSettings.BitAutoFire = false;
                await ctx.Wait(0.3f);
                // What's left lying about near BIT (a black cube stood by the Titan in the laptop's frames)
                foreach (var mi in ctx.Tree.Root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                {
                    if (!mi.IsVisibleInTree() || mi.Mesh is not BoxMesh box) continue;
                    var off = mi.GlobalPosition - bit.GlobalPosition;
                    if (new Vector2(off.X, off.Z).Length() > 7f || bit.IsAncestorOf(mi)) continue;
                    var size = box.Size * mi.GlobalBasis.Scale;
                    if (size.Length() > 6f) continue;
                    GD.Print($"[FrameLook] box near BIT after {name}: {mi.GetPath()} {size.X:0.00}x{size.Y:0.00}x{size.Z:0.00} at ({off.X:0.0}, {off.Y:0.0}, {off.Z:0.0}) {LookProbeSuite.Describe(mi.GetActiveMaterial(0), out _)}");
                }
            }
            bit.Look.SetLevel(1, animate: false);
            if (waves != null) waves.PauseAutoStart = false;
            ctx.Assert(true, "framelook/measured");
        }

        private static async Task Measure(TestContext ctx, VinePlayer bit, Camera3D cam, string who, string doing, string dir, bool shoot, bool walk = false)
        {
            var skel = bit.FrameSkeleton ?? bit.ModelRoot?.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
            var anim = bit.FrameAnimator?.AnimPlayer ?? CharacterAnimator.FindAnimationPlayerPublic(bit.FrameRoot ?? bit.ModelRoot);
            var first = Poses(skel);
            float turned = 0f, moved = 0f;
            var clips = new List<string>();
            double pos0 = anim?.CurrentAnimationPosition ?? 0, posMax = pos0, posMin = pos0;
            int playing = 0;
            var dirs = new[] { Vector3.Right, Vector3.Back, Vector3.Left, Vector3.Forward };
            for (int i = 0; i < 10; i++)
            {
                if (walk) bit.BotMove = dirs[(i / 3) % 4];
                // (standing is looked at over three seconds: its motion is slow)
                await ctx.Wait(doing == "standing" ? 0.3f : 0.15f);
                var now = Poses(skel);
                for (int b = 0; b < Mathf.Min(first.Count, now.Count); b++)
                {
                    turned = Mathf.Max(turned, Mathf.RadToDeg(first[b].Basis.GetRotationQuaternion().AngleTo(now[b].Basis.GetRotationQuaternion())));
                    moved = Mathf.Max(moved, first[b].Origin.DistanceTo(now[b].Origin));
                }
                if (anim != null)
                {
                    if (anim.IsPlaying()) playing++;
                    string c = anim.CurrentAnimation.ToString();
                    if (!clips.Contains(c)) clips.Add(c);
                    posMax = System.Math.Max(posMax, anim.CurrentAnimationPosition);
                    posMin = System.Math.Min(posMin, anim.CurrentAnimationPosition);
                }
                if (shoot && i is 1 or 3 or 5 or 7 or 9 && cam != null) Save(ctx, bit, cam, $"{dir}/{who}_{doing}_{i / 2}.png");
            }
            string frozen = turned < 1f && moved < 0.01f ? "  FROZEN" : "";
            GD.Print($"[FrameLook] {who} {doing}: clip {(clips.Count > 0 ? string.Join(">", clips) : "none")}, playing {playing}/10, position {posMin:0.00}..{posMax:0.00}; "
                + $"{first.Count} bones, most turned {turned:0.0} deg, most moved {moved:0.000} m{frozen}; aimed shots {bit.AimedShots}");
        }

        /// <summary>Each bone's pose in the skeleton's space.</summary>
        private static List<Transform3D> Poses(Skeleton3D skel)
        {
            var list = new List<Transform3D>();
            if (skel == null || !GodotObject.IsInstanceValid(skel)) return list;
            for (int i = 0; i < skel.GetBoneCount(); i++) list.Add(skel.GetBoneGlobalPose(i));
            return list;
        }

        private static void Save(TestContext ctx, VinePlayer bit, Camera3D cam, string file)
        {
            try
            {
                var img = ctx.Tree.Root.GetTexture().GetImage();
                var centre = cam.UnprojectPosition(bit.GlobalPosition + Vector3.Up * bit.BodyHeight * 0.5f);
                int half = 220;
                int x = Mathf.Clamp((int)centre.X - half, 0, Mathf.Max(0, img.GetWidth() - half * 2));
                int y = Mathf.Clamp((int)centre.Y - half, 0, Mathf.Max(0, img.GetHeight() - half * 2));
                img.GetRegion(new Rect2I(x, y, Mathf.Min(half * 2, img.GetWidth()), Mathf.Min(half * 2, img.GetHeight()))).SavePng(file);
            }
            catch (System.Exception e) { GD.PushWarning($"[FrameLook] {file}: {e.Message}"); }
        }
    }
}
