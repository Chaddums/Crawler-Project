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
            await Abilities(ctx, grid, player);
            await Hurt(ctx, player);
            Shake(ctx, cam);

            ReleaseAll();
            cam.OrbitYaw = 0f;
            if (waves != null) waves.PauseAutoStart = false;
        }

        /// <summary>
        /// Getting hurt shows (a red edge round the screen and a red number over BIT), and BIT
        /// heals: by itself once unhit for a few seconds, and with Repair Pulse.
        /// </summary>
        private static async Task Hurt(TestContext ctx, VinePlayer player)
        {
            ctx.StartTest();
            var hud = ctx.Tree.CurrentScene.FindChildren("*", "CanvasLayer", true, false).OfType<VineHUD>().FirstOrDefault();
            player.Heal(player.MaxHP);
            await ctx.Wait(0.2f);
            int numbers = DamageNumbers.ShownCount;
            player.TakeDamage(player.MaxHP * 0.4f);
            await ctx.Wait(0.1f);
            ctx.Assert(hud != null && hud.HurtVignette > 0.3f, "player/hurt_shows_red_edge", $"edge {hud?.HurtVignette:F2}");
            ctx.Assert(!GameSettings.DamageNumbers || DamageNumbers.ShownCount > numbers && DamageNumbers.LastBitNumber > 0,
                "player/hurt_shows_number", $"-{DamageNumbers.LastBitNumber}");
            // Still while hit: no healing yet; then it comes back by itself
            float low = player.CurrentHP;
            await ctx.Wait(Constants.BIT_REGEN_DELAY * 0.5f);
            ctx.Assert(player.CurrentHP <= low + 0.5f, "player/no_regen_right_after_a_hit", $"{low:F0} -> {player.CurrentHP:F0}");
            Engine.TimeScale = 3.0;
            await ctx.Wait(Constants.BIT_REGEN_DELAY * 0.8f + 1.5f);
            Engine.TimeScale = 1.0;
            ctx.Assert(player.CurrentHP > low + 1f && player.Regenerating || player.CurrentHP >= player.MaxHP - 0.5f,
                "player/heals_by_itself", $"{low:F0} -> {player.CurrentHP:F0}");
            // Repair Pulse patches BIT too
            player.TakeDamage(player.MaxHP * 0.5f);
            float before = player.CurrentHP;
            player.CurrentMaterials = player.MaxMaterials;
            var pulse = player.GetAbilities().FirstOrDefault(a => a.Name == "Repair Pulse");
            pulse?.Execute(player);
            ctx.Assert(pulse != null && player.CurrentHP > before + player.MaxHP * 0.2f, "player/repair_pulse_heals_bit",
                $"{before:F0} -> {player.CurrentHP:F0} ({player.AbilityResult})");
            player.Heal(player.MaxHP);
        }

        private static readonly string[] Actions = { "camera_pan_up", "camera_pan_down", "camera_pan_left", "camera_pan_right" };
        private static void ReleaseAll() { foreach (var a in Actions) Input.ActionRelease(a); }

        /// <summary>
        /// Q, E and R each do something you can see and say what: Shock Blast hits and stuns what's
        /// around BIT, Repair Pulse mends a damaged tower, Overclock boosts the towers in reach;
        /// pressing one without the Materials says so instead of doing nothing.
        /// </summary>
        private static async Task Abilities(TestContext ctx, VineGrid grid, VinePlayer player)
        {
            ctx.StartTest();
            var at = OpenSpot(grid);
            player.GlobalPosition = at;
            var results = new List<(int slot, string text, bool used)>();
            System.Action<int, string, bool> rec = (a, b, c) => results.Add((a, b, c));
            GameEvents.OnAbilityUsed += rec;
            var scene = ctx.Tree.CurrentScene;
            var dummies = new List<VineEnemy>();
            for (int i = 0; i < 3; i++)
            {
                var e = new VineEnemy();
                scene.AddChild(e);
                var p = at + new Vector3(1.5f * (i - 1), 0, 2f);
                e.Initialize("Dummy", VineEnemyFaction.Scavenger, 5000f, 0f, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(p));
                e.GlobalPosition = new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
                dummies.Add(e);
            }
            // A tower in reach to repair and boost
            VineNode tower = null;
            var c0 = grid.WorldToGrid(at);
            foreach (var off in new[] { new Vector2I(3, 0), new Vector2I(-3, 0), new Vector2I(0, 3), new Vector2I(0, -3) })
                if (AutoPlaceHelper.PlaceAt(grid, c0.X + off.X, c0.Y + off.Y, VineNodeType.DamageTower)) { tower = grid.GetNode(c0 + off); break; }
            await ctx.Wait(0.3f);
            player.TestRefill();
            float hp0 = dummies.Sum(d => d.CurrentHealth);
            player.UseAbility(0);
            await ctx.Wait(0.1f);
            float hit = hp0 - dummies.Sum(d => d.CurrentHealth);
            bool stunned = dummies.All(d => d.IsStunned);
            var q = results.LastOrDefault(r => r.slot == 0);
            ctx.Assert(hit > 0f && stunned && q.used && q.text.Contains("hit 3"), "player/shock_blast_hits_and_stuns",
                $"{hit:F0} damage to 3 enemies, all stunned {stunned}, says \"{q.text}\"");

            ctx.StartTest();
            string repair = "no tower placed", boost = "no tower placed";
            bool repaired = false, boosted = false;
            if (tower != null)
            {
                tower.TakeDamage(tower.NodeMaxHealth * 0.6f);
                float before = tower.NodeCurrentHealth;
                player.TestRefill();
                player.UseAbility(1);
                await ctx.Wait(0.1f);
                repaired = tower.NodeCurrentHealth > before + 1f;
                repair = $"tower {before:F0} -> {tower.NodeCurrentHealth:F0}, says \"{results.LastOrDefault(r => r.slot == 1).text}\"";
                player.TestRefill();
                player.UseAbility(2);
                await ctx.Wait(0.1f);
                boosted = tower.BuffStrength >= Constants.ABILITY_OVERCLOCK_STRENGTH - 0.01f;
                boost = $"boost {tower.BuffStrength:F1}, says \"{results.LastOrDefault(r => r.slot == 2).text}\"";
            }
            ctx.Assert(repaired && boosted, "player/repair_and_overclock_reach_towers", $"{repair}; {boost}");

            // No Materials: it says so
            ctx.StartTest();
            player.TestDrain();
            results.Clear();
            player.UseAbility(0);
            var broke = results.LastOrDefault();
            ctx.Assert(!broke.used && broke.text.Contains("Materials"), "player/ability_says_why_not", $"\"{broke.text}\"");

            GameEvents.OnAbilityUsed -= rec;
            foreach (var d in dummies) if (GodotObject.IsInstanceValid(d)) d.QueueFree();
            if (tower != null) grid.RemoveNode(tower.GridPosition);
        }

        /// <summary>A crowd dying at once is a light rumble at most; a boss is a full shake that fades.</summary>
        private static void Shake(TestContext ctx, TDCamera cam)
        {
            ctx.StartTest();
            for (int i = 0; i < 40; i++) cam.Shake(0.3f, 0.2f);
            float crowd = cam.ShakeTrauma;
            cam.Shake(2.5f, 1.5f);
            float boss = cam.ShakeTrauma;
            for (int i = 0; i < 180; i++) cam._Process(1.0 / 60.0);
            float after = cam.ShakeTrauma;
            ctx.Assert(crowd <= Constants.SHAKE_MINOR_CAP + 0.001f && boss >= 0.9f && after < 0.05f, "player/shake_adds_up_and_fades",
                $"40 small shakes {crowd:F2} (cap {Constants.SHAKE_MINOR_CAP}), then a boss {boss:F2}, 3 s later {after:F2}");
        }

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
            var far = Dummy(ctx, grid, at + new Vector3(0, 0, -(player.EffectiveRange + 2f))); // out of auto-fire reach
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

            // An aimed shot reaches across the field (it used to stop after three cells)
            ctx.StartTest();
            // 26 units out, in whichever of eight directions stays on the field and furthest from
            // the Spire (a dummy standing on the Spire's cell leaks into it)
            var spire = grid.GridToWorld(grid.ExitPoint);
            Vector3 distantAt = at;
            float bestScore = -1f;
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.Pi / 4f;
                var c = at + new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * 26f;
                var cell = grid.WorldToGrid(c);
                if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) continue;
                float score = c.DistanceTo(spire);
                if (score > bestScore) { bestScore = score; distantAt = c; }
            }
            var distant = Dummy(ctx, grid, distantAt);
            float dBefore = distant.CurrentHealth;
            player.TestAimPoint = distant.GlobalPosition;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = mid, GlobalPosition = mid });
            await ctx.Wait(1.2f);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = mid, GlobalPosition = mid });
            await ctx.Wait(0.2f);
            player.TestAimPoint = null;
            float dist = new Vector2(distant.GlobalPosition.X - at.X, distant.GlobalPosition.Z - at.Z).Length();
            ctx.Assert(player.LastAimedHit == distant && distant.CurrentHealth < dBefore, "player/aimed_shot_reaches_far",
                $"target {dist:F0} away (auto-fire reach {player.EffectiveRange:F0}): hit {(player.LastAimedHit == distant ? "it" : "nothing")}");
            distant.QueueFree();
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

            // 2b. A shot whose line dips under the ground on the way (a rise between gun and
            // target, here a gun just below the surface) rides over it and still reaches a target
            // above the ground: BIT's shots used to vanish into hillsides
            ctx.StartTest();
            var p3 = new VineProjectile();
            scene.AddChild(p3);
            var low = at + new Vector3(0, 0, 0);
            low.Y = grid.GetWorldHeight(low.X, low.Z) - 0.3f;
            var up = at + new Vector3(6f, 0, 2f);
            up.Y = grid.GetWorldHeight(up.X, up.Z) + 0.6f;
            p3.Initialize(low, up, Colors.White, 30f);
            steps = 0;
            float deepest = float.MaxValue;
            while (GodotObject.IsInstanceValid(p3) && !p3.IsQueuedForDeletion() && steps < 100)
            {
                p3._Process(1.0 / 30.0);
                steps++;
                if (steps > 1)
                {
                    var q = p3.GlobalPosition;
                    deepest = Mathf.Min(deepest, q.Y - grid.GetWorldHeight(q.X, q.Z));
                }
            }
            ctx.Assert(!p3.HitGround && p3.GlobalPosition.DistanceTo(up) < 0.05f && deepest > -0.06f, "player/shot_skims_to_target",
                $"hit ground {p3.HitGround}, ended {p3.GlobalPosition.DistanceTo(up):F2} from the target, lowest {deepest:F2} above the surface");

            // 3. BIT's aimed shots all the way round (hills, slopes, misses) all finish in flight time
            ctx.StartTest();
            player.GlobalPosition = at;
            await ctx.Wait(0.3f);
            var live = new HashSet<VineProjectile>();
            int fired = 0, stuck = 0, buried = 0;
            var mid = ctx.Tree.Root.GetVisibleRect().Size * 0.5f;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = mid, GlobalPosition = mid });
            float flight = player.AimedRange / (18f * 1.4f) + 1f; // aimed shots fly AimedRange at 1.4x the gun speed
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
            float topSpeed = 0f;
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
                topSpeed = Mathf.Max(topSpeed, new Vector2(player.Velocity.X, player.Velocity.Z).Length());
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
            // The run is a sprint: it builds well past walking pace
            ctx.Assert(topSpeed >= player.MoveSpeed * 1.45f, "player/run_is_faster",
                $"top speed {topSpeed:F2} vs move speed {player.MoveSpeed:F2}");
            var clip = anim.GetAnimation("Run");
            ctx.Assert(clip != null && clip.LoopMode != Animation.LoopModeEnum.None, "player/run_clip_loops",
                $"Run clip ({clip?.Length:F2} s) loop mode {clip?.LoopMode}");

            // The loop is one clean stride: the last pose meets the first, and no frame stands
            // still (the old window started from a standstill and ended 274 degrees away from
            // its first pose, so the run stopped and started about once a second)
            ctx.StartTest();
            if (clip != null)
            {
                var rot = new List<int>();
                for (int t = 0; t < clip.GetTrackCount(); t++)
                    if (clip.TrackGetType(t) == Animation.TrackType.Rotation3D && clip.TrackGetKeyCount(t) > 1) rot.Add(t);
                Quaternion[] Pose(double time) => rot.Select(t => clip.RotationTrackInterpolate(t, time)).ToArray();
                float Diff(Quaternion[] a, Quaternion[] b) { float s = 0; for (int i = 0; i < a.Length; i++) s += Mathf.RadToDeg(a[i].AngleTo(b[i])); return s; }
                float seam = Diff(Pose(0), Pose(clip.Length - 0.0001));
                float stillest = float.MaxValue, typical = 0f;
                double stillAt = 0;
                int n = 0;
                for (double tt = 1.0 / 60.0; tt < clip.Length; tt += 1.0 / 60.0, n++)
                {
                    float d = Diff(Pose(tt - 1.0 / 60.0), Pose(tt));
                    if (d < stillest) { stillest = d; stillAt = tt; }
                    typical += d;
                }
                typical /= Mathf.Max(1, n);
                ctx.Assert(rot.Count > 0 && seam < 25f && stillest > 1f, "player/run_loop_seamless",
                    $"{rot.Count} bones: loop seam {seam:F1} deg, stillest frame {stillest:F1} deg at {stillAt:F3} s (typical {typical:F1})");
            }
        }
    }
}
