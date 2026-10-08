using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// BIT under real input in a battle: WASD moves BIT up/left/down/right on the screen
    /// whichever way the camera is turned; the naruto run starts the moment he moves and never
    /// drops out while a direction is held (attacks, key changes); he stops when let go.
    /// </summary>
    public class PlayerTestSuite : ITestSuite
    {
        public string SuiteName => "player";

        public async Task Run(TestContext ctx)
        {
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "player/battle_loads");
            if (!ok) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            var grid = ServiceLocator.Get<VineGrid>();
            ServiceLocator.TryGet<VinePlayer>(out var player);
            ServiceLocator.TryGet<TDCamera>(out var cam);
            ctx.StartTest();
            ctx.Assert(player != null && cam != null, "player/bit_and_camera_exist");
            if (player == null || cam == null) return;

            await Controls(ctx, grid, player, cam);
            await RunAnimation(ctx, grid, player, cam);
            await Attacks(ctx, grid, player);
            await ShotsLand(ctx, grid, player);

            ReleaseAll();
            cam.OrbitYaw = 0f;
            if (waves != null) waves.PauseAutoStart = false;
        }

        private static readonly string[] Actions = { "camera_pan_up", "camera_pan_down", "camera_pan_left", "camera_pan_right" };
        private static void ReleaseAll() { foreach (var a in Actions) Input.ActionRelease(a); }

        /// <summary>An open spot with room to move in every direction.</summary>
        private static Vector3 OpenSpot(VineGrid grid)
        {
            var row = TowerSheetSuite.FindRow(grid, 9);
            var c = row.Count > 0 ? row[4] : grid.ExitPoint + new Vector2I(-6, 0);
            var p = grid.GridToWorld(c);
            return new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
        }

        private static async Task Controls(TestContext ctx, VineGrid grid, VinePlayer player, TDCamera cam)
        {
            foreach (float deg in new[] { 0f, 90f, 200f })
            {
                ctx.StartTest();
                cam.OrbitYaw = Mathf.DegToRad(deg);
                player.GlobalPosition = OpenSpot(grid);
                await ctx.Wait(0.4f); // camera catches up
                // What "up the screen" is for this camera: its forward, flattened onto the ground
                var fwd = -cam.GlobalTransform.Basis.Z;
                fwd.Y = 0;
                fwd = fwd.Normalized();
                var right = cam.GlobalTransform.Basis.X;
                right.Y = 0;
                right = right.Normalized();

                var start = player.GlobalPosition;
                Input.ActionPress("camera_pan_up");
                await ctx.Wait(0.5f);
                Input.ActionRelease("camera_pan_up");
                var up = player.GlobalPosition - start;
                up.Y = 0;

                start = player.GlobalPosition;
                Input.ActionPress("camera_pan_right");
                await ctx.Wait(0.5f);
                Input.ActionRelease("camera_pan_right");
                var rt = player.GlobalPosition - start;
                rt.Y = 0;
                await ctx.Wait(0.3f);

                ctx.Assert(up.Length() > 0.5f && up.Normalized().Dot(fwd) > 0.9f, $"player/w_moves_up_screen_at_{deg:F0}deg",
                    $"W moved {up.Length():F2} at {Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(up.Normalized().Dot(fwd), -1f, 1f))):F0} deg from up the screen");
                ctx.Assert(rt.Length() > 0.5f && rt.Normalized().Dot(right) > 0.9f, $"player/d_moves_right_screen_at_{deg:F0}deg",
                    $"D moved {rt.Length():F2} at {Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(rt.Normalized().Dot(right), -1f, 1f))):F0} deg from screen right");
            }
            cam.OrbitYaw = 0f;
        }

        private static VineEnemy Dummy(TestContext ctx, VineGrid grid, Vector3 at, float hp = 1e5f)
        {
            var e = new VineEnemy();
            ctx.Tree.CurrentScene.AddChild(e);
            e.Initialize("Dummy", VineEnemyFaction.Scavenger, hp, 0f, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(at));
            e.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
            return e;
        }

        /// <summary>BIT shoots the nearest enemy by itself; holding the left button shoots where it aims instead.</summary>
        private static async Task Attacks(TestContext ctx, VineGrid grid, VinePlayer player)
        {
            player.GlobalPosition = OpenSpot(grid);
            await ctx.Wait(0.3f);
            var at = player.GlobalPosition;
            var near = Dummy(ctx, grid, at + new Vector3(2.5f, 0, 0));
            var far = Dummy(ctx, grid, at + new Vector3(0, 0, -5f));
            await ctx.Wait(2f);
            ctx.StartTest();
            float nearLost = 1e5f - near.CurrentHealth, farLost = 1e5f - far.CurrentHealth;
            ctx.Assert(nearLost > 0f && farLost <= 0f, "player/auto_fire_hits_nearest", $"nearest lost {nearLost:F0}, farther {farLost:F0}");

            ctx.StartTest();
            float farBefore = far.CurrentHealth;
            int shots0 = player.AimedShots;
            player.TestAimPoint = far.GlobalPosition;
            // In the middle of the screen: at (0,0) the click lands on the top bar and the UI keeps it
            var mid = ctx.Tree.Root.GetVisibleRect().Size * 0.5f;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = mid, GlobalPosition = mid });
            await ctx.Wait(1.2f);
            bool aiming = player.IsAiming;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = mid, GlobalPosition = mid });
            await ctx.Wait(0.2f);
            player.TestAimPoint = null;
            int shots = player.AimedShots - shots0;
            ctx.Assert(aiming && shots >= 1, "player/left_mouse_aims", $"aiming {aiming}, {shots} aimed shots");
            ctx.Assert(player.LastAimedHit == far && far.CurrentHealth < farBefore, "player/aimed_shot_hits_target",
                $"hit {(player.LastAimedHit == far ? "the aimed target" : player.LastAimedHit == near ? "the nearest" : "nothing")}");
            ctx.Assert(!player.IsAiming, "player/release_stops_aiming");
            ctx.Assert(Mathf.IsEqualApprox(farBefore - far.CurrentHealth, player.EffectiveDamage * shots, 0.5f) || shots > 1,
                "player/aimed_damage", $"lost {farBefore - far.CurrentHealth:F1} from {shots} shots of {player.EffectiveDamage:F1}");
            near.QueueFree();
            far.QueueFree();
        }

        /// <summary>
        /// Shots end where they should: on the target even at a low frame rate (a long step used to
        /// skip the arrival window and fly on into the ground), and at the ground when the line
        /// runs into it. BIT's aimed shots all around him must all finish in flight time.
        /// </summary>
        private static async Task ShotsLand(TestContext ctx, VineGrid grid, VinePlayer player)
        {
            var scene = ctx.Tree.CurrentScene;
            var at = OpenSpot(grid);
            float ground = grid.GetWorldHeight(at.X, at.Z);

            // 1. A fast shot stepped at 10 fps lands on its target, not past it
            ctx.StartTest();
            var p1 = new VineProjectile();
            scene.AddChild(p1);
            var from = at + new Vector3(0, 1.2f, 0);
            var to = at + new Vector3(7.3f, 0.5f, 0);
            to.Y = grid.GetWorldHeight(to.X, to.Z) + 0.5f;
            p1.Initialize(from, to, Colors.White, 40f);
            int steps = 0;
            float lowest = float.MaxValue;
            while (GodotObject.IsInstanceValid(p1) && !p1.IsQueuedForDeletion() && steps < 50)
            {
                p1._Process(0.1);
                steps++;
                var q = p1.GlobalPosition;
                lowest = Mathf.Min(lowest, q.Y - grid.GetWorldHeight(q.X, q.Z));
            }
            ctx.Assert(steps <= 3 && p1.GlobalPosition.DistanceTo(to) < 0.01f && !p1.HitGround, "player/shot_lands_at_low_fps",
                $"{steps} steps, ended {p1.GlobalPosition.DistanceTo(to):F2} from the target, under ground by {-lowest:F2}");

            // 2. A shot whose line runs into the ground stops there
            ctx.StartTest();
            var p2 = new VineProjectile();
            scene.AddChild(p2);
            var deep = at + new Vector3(0, 0, -6f);
            deep.Y = grid.GetWorldHeight(deep.X, deep.Z) - 3f;
            p2.Initialize(from, deep, Colors.White, 30f);
            steps = 0;
            while (GodotObject.IsInstanceValid(p2) && !p2.IsQueuedForDeletion() && steps < 100)
            {
                p2._Process(1.0 / 30.0);
                steps++;
            }
            var end = p2.GlobalPosition;
            float under = grid.GetWorldHeight(end.X, end.Z) - end.Y;
            ctx.Assert(p2.HitGround && under < 0.1f && end.DistanceTo(deep) > 1f, "player/shot_stops_at_ground",
                $"hit ground {p2.HitGround}, ended {under:F2} under the surface, {end.DistanceTo(deep):F1} short of the buried target");

            // 3. BIT's aimed shots all the way round (hills, slopes, misses) all finish in flight time
            ctx.StartTest();
            player.GlobalPosition = at;
            await ctx.Wait(0.3f);
            var live = new HashSet<VineProjectile>();
            int fired = 0, stuck = 0, buried = 0;
            var mid = ctx.Tree.Root.GetVisibleRect().Size * 0.5f;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = mid, GlobalPosition = mid });
            float flight = player.EffectiveRange / 18f + 1f;
            var born = new Dictionary<VineProjectile, float>();
            float t = 0f;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.Tau / 12f;
                player.TestAimPoint = at + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 10f;
                float until = t + 1f / Mathf.Max(0.1f, player.EffectiveAttackSpeed) + 0.1f;
                while (t < until)
                {
                    await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
                    t += (float)ctx.Tree.Root.GetProcessDeltaTime();
                    foreach (var n in scene.GetChildren())
                        if (n is VineProjectile vp && !n.IsQueuedForDeletion() && born.TryAdd(vp, t)) fired++;
                    foreach (var (vp, b) in born)
                    {
                        if (!GodotObject.IsInstanceValid(vp) || vp.IsQueuedForDeletion()) continue;
                        var q = vp.GlobalPosition;
                        if (q.Y < grid.GetWorldHeight(q.X, q.Z) - 0.15f) buried++;
                        if (t - b > flight) stuck++;
                    }
                }
            }
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = mid, GlobalPosition = mid });
            player.TestAimPoint = null;
            await ctx.Wait(flight);
            int left = born.Keys.Count(vp => GodotObject.IsInstanceValid(vp) && !vp.IsQueuedForDeletion());
            ctx.Assert(fired >= 10 && stuck == 0 && buried == 0 && left == 0, "player/aimed_shots_finish",
                $"{fired} shots: {stuck} frame(s) past flight time, {buried} frame(s) under the ground, {left} still flying");
        }

        private static async Task RunAnimation(TestContext ctx, VineGrid grid, VinePlayer player, TDCamera cam)
        {
            player.GlobalPosition = OpenSpot(grid);
            await ctx.Wait(0.4f);
            var anim = CharacterAnimator.FindAnimationPlayerPublic(player);
            ctx.StartTest();
            ctx.Assert(anim != null && anim.HasAnimation("Run"), "player/has_run_clip");
            if (anim == null) return;

            // Something to shoot at, so attacks happen while he runs
            var target = new VineEnemy();
            ctx.Tree.CurrentScene.AddChild(target);
            var tp = player.GlobalPosition + new Vector3(3f, 0, 0);
            target.Initialize("Run target", VineEnemyFaction.Scavenger, 1e5f, 0f, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(tp));
            target.GlobalPosition = new Vector3(tp.X, grid.GetWorldHeight(tp.X, tp.Z), tp.Z);

            ctx.StartTest();
            Input.ActionPress("camera_pan_up");
            await ctx.Wait(0.1f);
            bool runningAtOnce = player.IsNarutoRunning;

            // Hold for 3 s with two key changes, sampling every physics frame
            int frames = 0, offRun = 0;
            var notes = new HashSet<string>();
            float t0 = Time.GetTicksMsec() / 1000f;
            int phase = 0;
            while (Time.GetTicksMsec() / 1000f - t0 < 3f)
            {
                float t = Time.GetTicksMsec() / 1000f - t0;
                if (phase == 0 && t > 1f) { Input.ActionRelease("camera_pan_up"); phase = 1; }        // one frame with nothing held
                else if (phase == 1) { Input.ActionPress("camera_pan_left"); phase = 2; }
                else if (phase == 2 && t > 2f) { Input.ActionPress("camera_pan_down"); Input.ActionRelease("camera_pan_left"); phase = 3; }
                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.PhysicsFrame);
                frames++;
                if (anim.CurrentAnimation != "Run" || !anim.IsPlaying())
                {
                    offRun++;
                    notes.Add(string.IsNullOrEmpty(anim.CurrentAnimation) ? "(stopped)" : anim.CurrentAnimation);
                }
            }
            ReleaseAll();
            await ctx.Wait(0.4f);
            bool stopped = !player.IsNarutoRunning && anim.CurrentAnimation != "Run";
            if (GodotObject.IsInstanceValid(target)) target.QueueFree();

            ctx.Assert(runningAtOnce, "player/run_starts_at_once", "Not running 0.1 s after a direction was held");
            ctx.Assert(frames > 30 && offRun == 0, "player/run_never_drops",
                $"{offRun} of {frames} frames off the run clip ({string.Join(", ", notes)})");
            ctx.Assert(stopped, "player/stops_when_released", $"Still {anim.CurrentAnimation} after letting go");
            var clip = anim.GetAnimation("Run");
            ctx.Assert(clip != null && clip.LoopMode != Animation.LoopModeEnum.None, "player/run_clip_loops",
                $"Run clip ({clip?.Length:F2} s) loop mode {clip?.LoopMode}");
        }
    }
}
