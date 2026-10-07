using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Loads every animated character the way the game does (same normalise, ground, split and
    /// animator setup) and checks what each AnimState actually plays: a real clip, the right
    /// loop mode, tracks that resolve to bones, bones that move, and no root drift.
    ///
    /// With a display (Xvfb works) it also renders a contact sheet per character to
    /// test-reports/anim/&lt;name&gt;.png: one row per state, four poses across the clip.
    /// "anim" is headless-safe; "anim-sheets" renders.
    /// </summary>
    public class AnimationTestSuite : ITestSuite
    {
        private readonly bool _sheets;
        public AnimationTestSuite(bool sheets = false) { _sheets = sheets; }
        public string SuiteName => _sheets ? "anim-sheets" : "anim";

        internal record Subject(string Name, string Path, bool IsBit);

        internal static readonly Subject[] Subjects =
        {
            new("BIT", AssetLibrary.COMPANION_BIT, true),
            new("Scavenger", AssetLibrary.ENEMY_SCRAP_RAT, false),
            new("Brute", AssetLibrary.ENEMY_QUAD_SHELL, false),
            new("Ghost", AssetLibrary.ENEMY_TRILOBITE, false),
            new("Swarm", AssetLibrary.ENEMY_SPARK_DRONE, false),
        };

        // What the game asks each kind of character to play
        private static readonly AnimState[] EnemyStates = { AnimState.Walk, AnimState.Death };
        private static readonly string[] BitClips = { "Idle", "Run", "Attack", "Attack_R", "Attack_L", "Death" };

        public async Task Run(TestContext ctx)
        {
            ctx.Tree.UnloadCurrentScene();
            await Frames(ctx, 2);

            var stage = new Node3D { Name = "AnimStage" };
            ctx.Tree.Root.AddChild(stage);
            if (_sheets) BuildStudio(stage);

            foreach (var s in Subjects)
            {
                if (!ResourceLoader.Exists(s.Path))
                {
                    // Paid models aren't in a fresh clone (LFS); skip rather than fail
                    GD.Print($"[AnimAudit] {s.Name}: {s.Path} not present, skipped");
                    continue;
                }
                var holder = new Node3D { Name = s.Name };
                stage.AddChild(holder);
                var (model, animator) = BuildLikeGame(holder, s);
                await Frames(ctx, 2);
                if (s.IsBit) FinishBitSetup(model, animator);
                await Frames(ctx, 1);

                Audit(ctx, s, model, animator);
                if (_sheets) await RenderSheet(ctx, s, model, animator);

                holder.QueueFree();
                await Frames(ctx, 2);
            }
            stage.QueueFree();
        }

        // ── Same steps as VinePlayer.BuildVisual / VineEnemy.BuildVisual ──

        internal static (Node3D model, CharacterAnimator animator) BuildLikeGame(Node3D holder, Subject s)
        {
            var model = AssetLibrary.InstantiateNormalized(s.Path);
            holder.AddChild(model);
            AssetLibrary.GroundModel(model);
            AssetLibrary.ApplyPlayerTexture(model, s.Path);
            if (!s.IsBit) CharacterAnimator.SplitMonolithicAnimation(model);
            var animator = new CharacterAnimator();
            holder.AddChild(animator);
            animator.Initialize(model);
            if (!s.IsBit) animator.SetState(AnimState.Walk);
            return (model, animator);
        }

        internal static void FinishBitSetup(Node3D model, CharacterAnimator animator)
        {
            VinePlayer.SplitBitAnimations(model);
            animator.Initialize(model);
            animator.PlayCustom("Idle");
        }

        // ── Checks ──

        private static void Audit(TestContext ctx, Subject s, Node3D model, CharacterAnimator animator)
        {
            string p = $"anim/{s.Name}";
            ctx.StartTest();
            ctx.Assert(animator.IsInitialized, $"{p}/has_animation_player");
            if (!animator.IsInitialized) return;
            var ap = animator.AnimPlayer;

            var report = new StringBuilder($"[AnimAudit] {s.Name} ({s.Path})\n");
            foreach (var name in ap.GetAnimationList())
            {
                var a = ap.GetAnimation(name);
                var (broken, total) = CountBrokenTracks(ap, a);
                report.Append($"    clip '{name}' len={a.Length:0.00}s loop={a.LoopMode} tracks={total} broken={broken} moving={MovingTracks(a)}\n");
            }

            // Which clip does each state the game uses actually play?
            var used = new Dictionary<string, string>();
            if (s.IsBit)
            {
                foreach (var clip in BitClips)
                {
                    used[clip] = ap.HasAnimation(clip) ? clip : null;
                    ctx.Assert(ap.HasAnimation(clip), $"{p}/clip_{clip}", $"BIT plays '{clip}' by name; clips: {string.Join(", ", ap.GetAnimationList())}");
                }
            }
            else
            {
                foreach (var st in EnemyStates)
                {
                    animator.Play(st);
                    used[st.ToString()] = ap.CurrentAnimation;
                }
            }
            foreach (var (k, v) in used) report.Append($"    {k} -> {v ?? "(none)"}\n");
            GD.Print(report.ToString());

            string walk = s.IsBit ? used.GetValueOrDefault("Run") : used.GetValueOrDefault("Walk");
            string death = used.GetValueOrDefault("Death");

            // Facing: with the game's yaw (atan2(dir.x, dir.z) + the model's offset) and dir = +Z,
            // the front legs must be ahead of the back legs
            var facing = FrontDirection(model, AssetLibrary.GetFacingYawOffset(s.Path));
            if (facing != null)
                ctx.Assert(Mathf.RadToDeg(facing.Value.AngleTo(Vector3.Back)) < 45f, $"{p}/faces_movement",
                    $"Front legs point {facing.Value} when walking toward +Z (walks sideways or backwards)");

            if (animator.IsProcedural)
            {
                // No usable clips: the procedural gait/bob must actually move the model
                ctx.Assert(ProceduralMoves(animator, model), $"{p}/move_procedural",
                    "Model has no clips and the procedural fallback didn't move it");
            }
            else if (walk != null)
            {
                var wa = ap.GetAnimation(walk);
                ctx.Assert(wa.LoopMode != Animation.LoopModeEnum.None, $"{p}/move_clip_loops", $"'{walk}' does not loop");
                ctx.Assert(MovingTracks(wa) > 0, $"{p}/move_clip_moves", $"'{walk}' has no moving tracks");
                float drift = RootDrift(ap, wa, model);
                ctx.Assert(drift < 0.25f, $"{p}/no_root_drift", $"'{walk}' moves the root {drift:0.00} model-heights per loop (slides/teleports)");
                float offset = RootOffset(ap, walk, model);
                ctx.Assert(offset <= 1.01f, $"{p}/no_root_offset", $"'{walk}' draws the skinned mesh {offset:0.0} model-sizes from where its bounds put it (floats away from where it was grounded)");
            }

            if (!s.IsBit)
            {
                if (animator.HasClip(AnimState.Death))
                {
                    ctx.Assert(ap.GetAnimation(death).LoopMode == Animation.LoopModeEnum.None,
                        $"{p}/death_does_not_loop", $"'{death}' loops");
                }
                else
                {
                    ctx.Assert(ProceduralDeathTips(animator), $"{p}/death_procedural",
                        "No death clip, and the procedural death didn't tip the model over");
                }

                // One-shots must not loop (Rig|Hit imported looping and every reaction stuck)
                foreach (var st in new[] { AnimState.Hit, AnimState.Attack })
                {
                    if (!animator.HasClip(st)) continue;
                    animator.Play(st);
                    var clip = ap.CurrentAnimation;
                    if (!string.IsNullOrEmpty(clip))
                        ctx.Assert(ap.GetAnimation(clip).LoopMode == Animation.LoopModeEnum.None,
                            $"{p}/{st.ToString().ToLower()}_does_not_loop", $"'{clip}' loops");
                }
            }
            else if (death != null)
            {
                ctx.Assert(ap.GetAnimation(death).LoopMode == Animation.LoopModeEnum.None,
                    $"{p}/death_does_not_loop", $"'{death}' loops");
            }

            foreach (var name in used.Values.Where(v => !string.IsNullOrEmpty(v)).Distinct())
            {
                var (broken, total) = CountBrokenTracks(ap, ap.GetAnimation(name));
                ctx.Assert(broken == 0, $"{p}/tracks_resolve_{Safe(name)}", $"'{name}': {broken} of {total} tracks point at nodes or bones that don't exist");
            }
        }

        /// <summary>
        /// Horizontal direction from the back legs to the front legs (bones named front/back),
        /// with the model turned by <paramref name="yaw"/>. Null when the rig doesn't name them.
        /// </summary>
        private static Vector3? FrontDirection(Node3D model, float yaw)
        {
            var sk = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
            if (sk == null) return null;
            var saved = model.Rotation;
            model.Rotation = new Vector3(0, yaw, 0);
            Vector3 front = Vector3.Zero, back = Vector3.Zero;
            int nf = 0, nb = 0;
            for (int b = 0; b < sk.GetBoneCount(); b++)
            {
                string n = sk.GetBoneName(b).ToLower();
                var w = (sk.GlobalTransform * sk.GetBoneGlobalRest(b)).Origin;
                if (n.Contains("front")) { front += w; nf++; }
                else if (n.Contains("back")) { back += w; nb++; }
            }
            model.Rotation = saved;
            if (nf == 0 || nb == 0) return null;
            var d = front / nf - back / nb;
            d.Y = 0;
            return d.LengthSquared() > 1e-8f ? d.Normalized() : null;
        }

        /// <summary>Step the procedural fallback and see whether the pivot or a bone pose changed.</summary>
        private static bool ProceduralMoves(CharacterAnimator animator, Node3D model)
        {
            animator.SetState(AnimState.Walk);
            var pivot = animator.ProceduralPivot;
            var sk = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
            var before = pivot?.Transform ?? Transform3D.Identity;
            var bones = sk == null ? new List<Quaternion>()
                : Enumerable.Range(0, sk.GetBoneCount()).Select(b => sk.GetBonePoseRotation(b)).ToList();
            animator._Process(0.12);
            animator._Process(0.12);
            bool pivotMoved = pivot != null && !pivot.Transform.IsEqualApprox(before);
            bool boneMoved = sk != null && Enumerable.Range(0, sk.GetBoneCount()).Any(b => !sk.GetBonePoseRotation(b).IsEqualApprox(bones[b]));
            return pivotMoved || boneMoved;
        }

        /// <summary>Run the procedural death to the end and check the model rolled well over.</summary>
        private static bool ProceduralDeathTips(CharacterAnimator animator)
        {
            var pivot = animator.ProceduralPivot;
            if (pivot == null) return false;
            var b0 = pivot.GlobalTransform.Basis.Orthonormalized();
            var before = pivot.Transform;
            animator.SetState(AnimState.Death);
            var tw = animator.ProceduralDeathTween;
            if (tw == null) return false;
            tw.Pause();
            tw.CustomStep(1.0);
            var b1 = pivot.GlobalTransform.Basis.Orthonormalized();
            var q = (b1 * b0.Inverse()).GetRotationQuaternion();
            float degrees = Mathf.RadToDeg(2f * Mathf.Acos(Mathf.Min(1f, Mathf.Abs(q.W))));
            // Stand it back up for the rows that follow
            tw.Kill();
            pivot.Transform = before;
            animator.SetState(AnimState.Walk);
            return degrees > 45f;
        }

        /// <summary>
        /// How far the skin actually displaces the mesh at the clip's first frame, in mesh sizes:
        /// (posed bone * inverse bind).origin. The Swarm drone measured ~3 (158 units on a
        /// 52-unit mesh) before the fix, in rest and clip alike.
        /// </summary>
        internal static float RootOffset(AnimationPlayer ap, string clip, Node3D model)
        {
            float worst = 0f;
            var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
            // Measured against the whole model, not each part: an antenna swinging round the
            // body is many antenna-sizes from its bind position and that's fine
            float modelSize = 0f;
            foreach (var m in meshes)
                if (m.Mesh != null) { var z = m.Mesh.GetAabb().Size; modelSize = Mathf.Max(modelSize, Mathf.Max(z.X, Mathf.Max(z.Y, z.Z))); }
            if (modelSize <= 0f) return 0f;
            foreach (var mi in meshes)
            {
                if (mi.Skin == null || mi.Skin.GetBindCount() == 0 || mi.Mesh == null) continue;
                if (mi.GetNodeOrNull(mi.Skeleton) is not Skeleton3D sk) continue;
                string bn = mi.Skin.GetBindName(0);
                int bone = !string.IsNullOrEmpty(bn) ? sk.FindBone(bn) : mi.Skin.GetBindBone(0);
                if (bone < 0) continue;
                ap.Play(clip);
                ap.Seek(0, true);
                var d = (sk.GetBoneGlobalPose(bone) * mi.Skin.GetBindPose(0)).Origin.Length();
                worst = Mathf.Max(worst, d / modelSize);
            }
            return worst;
        }

        private static string Safe(string n) => new string(n.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        internal static (int broken, int total) CountBrokenTracks(AnimationPlayer ap, Animation a)
        {
            var root = ap.GetNodeOrNull(ap.RootNode);
            if (root == null) return (a.GetTrackCount(), a.GetTrackCount());
            int broken = 0;
            for (int t = 0; t < a.GetTrackCount(); t++)
            {
                var path = a.TrackGetPath(t);
                var nodePath = new NodePath(path.GetConcatenatedNames());
                var node = root.GetNodeOrNull(nodePath);
                if (node == null) { broken++; continue; }
                if (path.GetSubNameCount() > 0 && node is Skeleton3D sk && sk.FindBone(path.GetSubName(0)) < 0) broken++;
            }
            return (broken, a.GetTrackCount());
        }

        internal static int MovingTracks(Animation a)
        {
            int moving = 0;
            for (int t = 0; t < a.GetTrackCount(); t++)
            {
                int n = a.TrackGetKeyCount(t);
                if (n < 2) continue;
                var first = a.TrackGetKeyValue(t, 0);
                for (int k = 1; k < n; k++)
                {
                    if (!first.Equals(a.TrackGetKeyValue(t, k))) { moving++; break; }
                }
            }
            return moving;
        }

        /// <summary>Largest horizontal travel of any position track across the clip, in model heights.</summary>
        internal static float RootDrift(AnimationPlayer ap, Animation a, Node3D model)
        {
            // Measure in the model root's own space: tracks are in skeleton space, which may be
            // Z-up and scaled (the Rig| mechs sit under a 100x, -90 X node); the root-space AABB
            // excludes the model's own normalisation scale, so neither side applies it
            var root = ap.GetNodeOrNull(ap.RootNode);
            var aabb = AssetLibrary.GetCombinedAABB(model);
            float height = Mathf.Max(0.001f, aabb.Size.Y);
            float worst = 0f;
            for (int t = 0; t < a.GetTrackCount(); t++)
            {
                if (a.TrackGetType(t) != Animation.TrackType.Position3D) continue;
                int n = a.TrackGetKeyCount(t);
                if (n < 2) continue;
                var path = a.TrackGetPath(t);
                var target = root?.GetNodeOrNull<Node3D>(new NodePath(path.GetConcatenatedNames()));
                if (target == null) continue;
                var toRoot = (model.GlobalTransform.AffineInverse() * target.GlobalTransform).Basis;
                var p0 = (Vector3)a.TrackGetKeyValue(t, 0);
                var p1 = (Vector3)a.TrackGetKeyValue(t, n - 1);
                var d = toRoot * (p1 - p0);
                worst = Mathf.Max(worst, new Vector2(d.X, d.Z).Length());
            }
            return worst / height;
        }

        // ── Contact sheets ──

        private static void BuildStudio(Node3D stage)
        {
            var env = new WorldEnvironment { Environment = new Environment {
                BackgroundMode = Environment.BGMode.Color, BackgroundColor = new Color(0.16f, 0.17f, 0.2f),
                AmbientLightSource = Environment.AmbientSource.Color, AmbientLightColor = new Color(0.55f, 0.58f, 0.65f), AmbientLightEnergy = 0.7f } };
            stage.AddChild(env);
            var sun = new DirectionalLight3D { LightEnergy = 1.3f, ShadowEnabled = true };
            stage.AddChild(sun);
            sun.RotationDegrees = new Vector3(-40, -30, 0);
            var floor = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(8, 8) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.31f, 0.34f) } };
            stage.AddChild(floor);
            var cam = new Camera3D { Fov = 40, Name = "SheetCam" };
            stage.AddChild(cam);
            cam.GlobalPosition = new Vector3(2.3f, 1.5f, 3.4f);
            cam.LookAt(new Vector3(0, 0.6f, 0), Vector3.Up);
            cam.MakeCurrent();
        }

        private static async Task RenderSheet(TestContext ctx, Subject s, Node3D model, CharacterAnimator animator)
        {
            var ap = animator.AnimPlayer;
            const int cellW = 320, cellH = 320, cols = 4;

            // Each row: a label, the clip it plays (or "procedural"), and how to pose column c
            var rows = new List<(string label, string clip, System.Action<int> pose)>();
            void ClipRow(string label, string clip) => rows.Add((label, clip, c =>
            {
                ap.Play(clip);
                ap.Pause();
                ap.Seek(ap.GetAnimation(clip).Length * c / cols, true);
            }));

            if (s.IsBit)
            {
                foreach (var c in BitClips) if (ap != null && ap.HasAnimation(c)) ClipRow(c, c);
            }
            else
            {
                foreach (var st in new[] { AnimState.Idle, AnimState.Walk, AnimState.Attack, AnimState.Hit, AnimState.Death })
                {
                    var state = st;
                    if (state == AnimState.Death && !animator.HasClip(AnimState.Death))
                    {
                        rows.Add(("Death", "procedural", c =>
                        {
                            if (c == 0) { animator.SetState(AnimState.Idle); animator.SetState(AnimState.Death); animator.ProceduralDeathTween?.Pause(); }
                            animator.ProceduralDeathTween?.CustomStep(c == 0 ? 0.0 : 0.15);
                        }));
                    }
                    else if (animator.IsProcedural)
                    {
                        rows.Add((state.ToString(), "procedural", c =>
                        {
                            if (c == 0) animator.SetState(state);
                            animator._Process(0.0873); // an eighth of a stride per column
                        }));
                    }
                    else
                    {
                        animator.Play(state);
                        string clip = ap.CurrentAnimation;
                        if (!string.IsNullOrEmpty(clip)) ClipRow(state.ToString(), clip);
                    }
                }
            }

            string dir = ProjectSettings.GlobalizePath("res://test-reports/anim");
            System.IO.Directory.CreateDirectory(dir);
            await RenderFacing(ctx, s, model, dir); // before the rows: the death row leaves it lying down

            var sheet = Image.CreateEmpty(cellW * cols, cellH * rows.Count, false, Image.Format.Rgba8);
            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    rows[r].pose(c);
                    sheet.BlitRect(await Grab(ctx, cellW, cellH), new Rect2I(0, 0, cellW, cellH), new Vector2I(c * cellW, r * cellH));
                }
                GD.Print($"[AnimSheet] {s.Name} row {r}: {rows[r].label} -> {rows[r].clip}");
            }
            sheet.SavePng($"{dir}/{s.Name}.png");
            System.IO.File.WriteAllText($"{dir}/{s.Name}.txt", string.Join("\n", rows.Select(x => $"{x.label}\t{x.clip}")));
        }

        /// <summary>
        /// Top-down shot with the model turned the way the game turns it when walking toward +Z
        /// (yaw = atan2(dir.x, dir.z) = 0), and an arrow on the floor pointing +Z. The model's
        /// front should point along the arrow.
        /// </summary>
        private static async Task RenderFacing(TestContext ctx, Subject s, Node3D model, string dir)
        {
            var stage = model.GetParent().GetParent<Node3D>();
            var cam = stage.GetNodeOrNull<Camera3D>("SheetCam");
            if (cam == null) return;
            var savedPos = cam.GlobalTransform;
            var arrow = new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(0.5f, 0.9f, 0.02f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.2f, 0.2f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            };
            stage.AddChild(arrow);
            // Prism points along +Y; lay it flat pointing +Z, ahead of the model
            arrow.RotationDegrees = new Vector3(90, 0, 0);
            arrow.Position = new Vector3(0, 0.02f, 1.3f);
            model.Rotation = new Vector3(0, Mathf.Atan2(0f, 1f) + AssetLibrary.GetFacingYawOffset(s.Path), 0);
            cam.GlobalPosition = new Vector3(0, 5.5f, 0.4f);
            cam.LookAt(new Vector3(0, 0, 0.4f), Vector3.Forward);
            var img = await Grab(ctx, 480, 480);
            img.SavePng($"{dir}/{s.Name}_facing.png");
            // Straight-on from +Z: the model's face should look at the camera
            cam.GlobalPosition = new Vector3(0, 0.9f, 4.2f);
            cam.LookAt(new Vector3(0, 0.45f, 0), Vector3.Up);
            img = await Grab(ctx, 480, 480);
            img.SavePng($"{dir}/{s.Name}_front.png");
            arrow.QueueFree();
            cam.GlobalTransform = savedPos;
        }

        private static async Task<Image> Grab(TestContext ctx, int w, int h)
        {
            await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = ctx.Tree.Root.GetViewport().GetTexture().GetImage();
            int side = Mathf.Min(img.GetWidth(), img.GetHeight());
            var crop = img.GetRegion(new Rect2I((img.GetWidth() - side) / 2, (img.GetHeight() - side) / 2, side, side));
            crop.Resize(w, h);
            crop.Convert(Image.Format.Rgba8);
            return crop;
        }

        private static async Task Frames(TestContext ctx, int n)
        {
            for (int i = 0; i < n; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }
    }
}
