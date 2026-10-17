using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// BIT's frames (Data/Mechs/bit.json "frames", <see cref="MechFrame"/>): at set levels BIT
    /// climbs into a bigger body and rides it, small and visible at the seat, with the frame's
    /// own guns, stats and Q, E and R. The playtest asked for it: "BIT should still be the main
    /// character, then as he levels you get into a bigger bot".
    ///
    /// BIT's own model (<c>_modelRoot</c>) stays BIT: its gear, level parts and relic weapons stay
    /// on it, and it is seated on the frame every step. The frame is a second model under a
    /// holder that takes the facing, lean and sway BIT's body used to take.
    /// </summary>
    public partial class VinePlayer
    {
        private sealed class FrameRig
        {
            public MechFrame Def;
            public Node3D Holder;
            public Node3D Model;
            public CharacterAnimator Animator;
            public Skeleton3D Skeleton;
            public BitArms Arms;
            public int SeatBone = -1;
            public readonly List<(int bone, MechMuzzle def)> Muzzles = new();
            public int NextMuzzle;
            public bool Quaternius;
            public string Clip = "";
            public float SelfRateMult = 1f, SelfRateTime;
        }

        private FrameRig _frame;

        /// <summary>The frame BIT rides (null: on foot).</summary>
        public MechFrame Frame => _frame?.Def;
        /// <summary>What the HUD calls BIT now: BIT, or the frame it rides.</summary>
        public string BodyName => _frame != null ? $"BIT · {_frame.Def.Name.ToUpper()}" : "BIT";
        /// <summary>The frame's model holder (tests, renders).</summary>
        internal Node3D FrameRoot => _frame?.Holder;
        /// <summary>BIT's body circle, or the frame's.</summary>
        internal float BodyRadius => _frame?.Def.BodyRadius ?? Constants.BIT_BODY_RADIUS;
        /// <summary>How tall BIT stands now, frame included (health bar, camera).</summary>
        internal float BodyHeight => (_frame?.Def.Height ?? Constants.PLAYER_HEIGHT) * (_look?.ShownGrowth ?? 1f);

        private float FrameDamage => _frame?.Def.Stats.Damage ?? 1f;
        private float FrameRate => (_frame?.Def.Stats.Rate ?? 1f) * (_frame?.SelfRateMult ?? 1f);
        private float FrameRange => _frame?.Def.Stats.Range ?? 0f;
        private float FrameSpeed => _frame?.Def.Stats.Speed ?? 1f;
        private float FrameSprint => _frame?.Def.Stats.Sprint ?? 1f;
        private float FrameTaken => _frame?.Def.Stats.Taken ?? 1f;

        /// <summary>The node that takes BIT's facing, lean and sway: the frame when riding one.</summary>
        private Node3D Body => _frame?.Holder ?? _modelRoot;

        // ── Climbing in ──

        /// <summary>
        /// Ride the frame <paramref name="level"/> calls for (MechAppearance calls this when the
        /// body is built and on every level change). <paramref name="animate"/>: the moment is
        /// announced and celebrated; otherwise it just happens (Head Start, tests).
        /// </summary>
        internal void ApplyFrameForLevel(int level, bool animate)
        {
            var sheet = MechSheet.Load(Constants.PLAYER_MECH_ID);
            var def = sheet.FrameFor(level);
            if (def?.Id == _frame?.Def.Id) return;
            ExitFrame();
            if (def == null || _modelRoot == null) return;
            EnterFrame(def, animate);
        }

        private void EnterFrame(MechFrame def, bool animate)
        {
            var model = AssetLibrary.InstantiateToHeight(def.Model, def.Height);
            if (model == null) { GD.PushWarning($"[BIT] Frame {def.Id}: no model at {def.Model}"); return; }
            var rig = new FrameRig { Def = def, Model = model, Quaternius = def.Rig == "quaternius" };
            rig.Holder = new Node3D { Name = $"Frame_{def.Id}" };
            AddChild(rig.Holder);
            rig.Holder.AddChild(model);
            AssetLibrary.GroundModel(model);
            if (def.Lift != 0f) model.Position += Vector3.Up * def.Lift;
            float yaw = AssetLibrary.GetFacingYawOffset(def.Model);
            if (yaw != 0f) model.Rotation = new Vector3(0, yaw, 0);
            if (rig.Quaternius) BitBody.PrepareClips(model);
            ApplyFrameHull(rig);
            rig.Animator = new CharacterAnimator { Name = "FrameAnimator" };
            AddChild(rig.Animator);
            rig.Animator.Initialize(model);
            rig.Skeleton = FindSkeleton(model);
            if (rig.Skeleton != null)
            {
                rig.SeatBone = rig.Skeleton.FindBone(def.Seat.Bone);
                foreach (var m in def.Muzzles)
                {
                    int b = rig.Skeleton.FindBone(m.Bone);
                    if (b >= 0) rig.Muzzles.Add((b, m));
                }
                if (rig.SeatBone < 0 || rig.Muzzles.Count < def.Muzzles.Count)
                {
                    var bones = Enumerable.Range(0, rig.Skeleton.GetBoneCount()).Select(i => rig.Skeleton.GetBoneName(i));
                    GD.PushWarning($"[BIT] Frame {def.Id}: seat bone '{def.Seat.Bone}' {(rig.SeatBone >= 0 ? "found" : "missing")}, {rig.Muzzles.Count}/{def.Muzzles.Count} muzzle bones; bones: {string.Join(", ", bones)}");
                }
                if (rig.Quaternius)
                {
                    rig.Arms = new BitArms { Name = "FrameArms" };
                    rig.Arms.Setup(model);
                    rig.Skeleton.AddChild(rig.Arms);
                }
            }
            _frame = rig;
            PlayFrameClip("Idle", 0f, 1f);
            // BIT rides: it stands still at the seat
            _animator?.PlayCustomLooping("Idle", 0.2f);
            _animator?.SetSpeed(0.6f);
            if (_arms != null) _arms.RunWanted = false;
            Body.Rotation = new Vector3(0, _smoothYaw, 0);
            ApplyBodyScale(1f);
            SeatPilot();

            // What the frame brings: health, and its own Q, E and R
            if (def.Stats.Health > 0f)
            {
                MaxHP += def.Stats.Health;
                CurrentHP = Mathf.Min(MaxHP, CurrentHP + def.Stats.Health);
                GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);
            }
            if (def.Abilities.Count == 3) SetAbilities(FrameAbilities(def));
            rig.Holder.Visible = _modelRoot.Visible && !_isDead;

            GD.Print($"[BIT] Climbed into the {def.Name} frame (level {def.Level})");
            if (!animate || !IsInsideTree()) return;
            var at = GlobalPosition + Vector3.Up * def.Height * 0.5f;
            VfxFactory.SpawnEnergyBurst(GetTree(), at, BitPalette.AccentBright, 18);
            VfxFactory.SpawnAreaPulse(GetTree(), GlobalPosition, def.Height * 1.4f, BitPalette.Accent, 0.7f, 1.0f);
            if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.35f, 0.3f);
            Celebration.Show($"Level {def.Level}: new frame", def.Name.ToUpper(),
                string.IsNullOrEmpty(def.Arrive) ? $"BIT climbs into the {def.Name}" : def.Arrive, BitPalette.Accent, 3.5f);
            if (!string.IsNullOrEmpty(def.Line)) Comms.Say(Comms.Bit, def.Line, story: true);
        }

        /// <summary>Back on foot (a new level 1, tests).</summary>
        private void ExitFrame()
        {
            if (_frame == null) return;
            var rig = _frame;
            _frame = null;
            foreach (var mesh in rig.Model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                _domeMaterials.Remove(mesh);
                _themedMaterials.Remove(mesh);
            }
            if (rig.Def.Stats.Health > 0f)
            {
                MaxHP = Mathf.Max(1f, MaxHP - rig.Def.Stats.Health);
                CurrentHP = Mathf.Min(CurrentHP, MaxHP);
                GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);
            }
            if (IsInstanceValid(rig.Animator)) rig.Animator.QueueFree();
            if (IsInstanceValid(rig.Holder)) rig.Holder.QueueFree();
            SetAbilities(GetDefaultAbilities());
            _modelRoot.Position = Vector3.Zero;
            _modelRoot.Rotation = new Vector3(0, _smoothYaw, 0);
            ApplyBodyScale(1f);
        }

        /// <summary>The frame in BIT's hull: its texture's panels on dark gunmetal, its paint glowing, the dome's white outline; outside, the planet look.</summary>
        private void ApplyFrameHull(FrameRig rig)
        {
            var tex = string.IsNullOrEmpty(rig.Def.Texture) ? null : GD.Load<Texture2D>(rig.Def.Texture);
            var hull = rig.Def.Hull;
            foreach (var mesh in rig.Model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                if (mesh.Mesh == null) { mesh.Visible = false; continue; }
                var dome = BitBody.MakeHull(tex, HullDark, HullLight, BitEyeColor, hull.Glow, new Color(0.7f, 0.8f, 1f), 0.4f);
                dome.NextPass = MakeOutline(new Color(0.92f, 0.94f, 1.0f), OutlineDome, 0.95f);
                float outGlow = hull.Glow * (OnScrapyard ? 0.7f : 0.9f);
                var themed = BitBody.MakeHull(tex, HullDarkOut, HullLightOut, BitPlanetAccent, outGlow, BitPlanetAccent, OnScrapyard ? 0.25f : 0.35f);
                if (!OnScrapyard) themed.NextPass = MakeOutline(BitPlanetAccent, OutlineOut, 0.9f);
                foreach (var m in new[] { dome, themed })
                {
                    m.SetShaderParameter("paint_lo", hull.PaintLo);
                    m.SetShaderParameter("paint_hi", hull.PaintHi);
                    m.SetShaderParameter("detail", hull.Detail);
                }
                _hullGlowBase[dome] = hull.Glow;
                _hullGlowBase[themed] = outGlow;
                _domeMaterials[mesh] = dome;
                _themedMaterials[mesh] = themed;
                mesh.MaterialOverride = _isInsideDome ? dome : themed;
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            }
        }

        // ── Every step ──

        /// <summary>Scale BIT (and the frame) to <paramref name="share"/> of full size (the emergence grows from 0.01).</summary>
        private void ApplyBodyScale(float share)
        {
            if (_modelRoot == null) return;
            if (_frame == null) { _modelRoot.Scale = Vector3.One * VisualScale * share; return; }
            _frame.Holder.Scale = Vector3.One * SignalTuningEditor.PlayerModelScale * (_look?.ShownGrowth ?? 1f) * share;
            _modelRoot.Scale = Vector3.One * _baseModelScale * SignalTuningEditor.PlayerModelScale * _frame.Def.Seat.Scale * share;
        }

        /// <summary>Put BIT on the frame's seat: the seat bone, the offset in the frame's facing, upright, facing with it.</summary>
        private void SeatPilot()
        {
            if (_frame == null || _modelRoot == null) return;
            var holder = _frame.Holder;
            var basis = holder.GlobalTransform.Basis.Orthonormalized();
            float s = holder.Scale.X;
            Vector3 at = holder.GlobalPosition + basis * new Vector3(0, _frame.Def.Height * 0.8f * s, 0);
            if (_frame.Skeleton != null && _frame.SeatBone >= 0)
                at = _frame.Skeleton.GlobalTransform * _frame.Skeleton.GetBoneGlobalPose(_frame.SeatBone).Origin;
            var o = _frame.Def.Seat.Offset;
            if (o != null && o.Length == 3) at += basis * new Vector3(-o[0], o[1], o[2]) * s;
            _modelRoot.GlobalPosition = at;
            _modelRoot.Rotation = new Vector3(0, holder.Rotation.Y, 0);
        }

        /// <summary>A clip on the frame: BIT's own clip names on a quaternius rig, else the nearest state.</summary>
        private void PlayFrameClip(string clip, float blend, float speed)
        {
            var a = _frame?.Animator;
            if (a == null) return;
            if (_frame.Quaternius)
            {
                // A fall or a stagger plays once
                if (clip is "Death" or "Hit") a.PlayCustom(clip);
                else a.PlayCustomLooping(clip, blend);
                a.SetSpeed(speed);
                return;
            }
            var state = clip switch { "Run" => AnimState.Run, "Walk" => AnimState.Walk, "Hit" => AnimState.Hit, "Death" => AnimState.Death, _ => AnimState.Idle };
            if (_frame.Clip != clip) { a.SetState(state); _frame.Clip = clip; }
            a.SetSpeed(speed);
        }

        /// <summary>A one-shot on the frame (a shot, a punch) when it has one.</summary>
        private void PlayFrameAction(string clip, float speed)
        {
            var a = _frame?.Animator;
            if (a == null) return;
            if (_frame.Quaternius && a.AnimPlayer != null && a.AnimPlayer.HasAnimation(clip))
            {
                if (a.AnimPlayer.CurrentAnimation == clip) a.AnimPlayer.Stop();
                a.PlayCustom(clip);
                a.SetSpeed(speed);
                var anim = a.AnimPlayer.GetAnimation(clip);
                _clipLock = anim != null ? (float)anim.Length / Mathf.Max(0.1f, speed) : 0.3f;
            }
            else if (!_frame.Quaternius && a.HasClip(AnimState.Attack))
            {
                a.SetState(AnimState.Attack);
                a.SetSpeed(speed);
                _frame.Clip = "Attack";
                _clipLock = 0.35f / Mathf.Max(0.1f, speed);
            }
        }

        private void UpdateFrame(float dt)
        {
            if (_frame == null) return;
            if (_frame.SelfRateTime > 0f && (_frame.SelfRateTime -= dt) <= 0f) _frame.SelfRateMult = 1f;
            if (_frame.Arms != null) _frame.Arms.RunWanted = _isNarutoRunning && !_isSlipping;
            SeatPilot();
        }

        /// <summary>BIT's model's scale now: its size on foot, or its seat's share of it riding a frame.</summary>
        internal float ModelScaleNow => _frame == null ? VisualScale : _baseModelScale * SignalTuningEditor.PlayerModelScale * _frame.Def.Seat.Scale;

        /// <summary>Where the frame's barrels are now, before the shot's offset (tests).</summary>
        internal Vector3[] FrameMuzzlesNow() => _frame?.Skeleton == null ? System.Array.Empty<Vector3>()
            : _frame.Muzzles.Select(m => _frame.Skeleton.GlobalTransform * _frame.Skeleton.GetBoneGlobalPose(m.bone).Origin).ToArray();

        /// <summary>Renders: the frame upright facing +Z at full size, BIT on its seat.</summary>
        internal void HoldFramePose()
        {
            if (_frame == null) return;
            _frame.Holder.Position = Vector3.Zero;
            _frame.Holder.Rotation = Vector3.Zero;
            ApplyBodyScale(1f);
            SeatPilot();
        }

        private void SetBodyVisible(bool visible)
        {
            if (_modelRoot != null) _modelRoot.Visible = visible;
            if (_frame != null) _frame.Holder.Visible = visible;
        }

        // ── The frame's guns ──

        /// <summary>The next barrel in turn and its hand (null when the frame has none, or BIT is on foot).</summary>
        private (Vector3 from, string hand)? FrameMuzzle(Vector3 toward)
        {
            if (_frame == null || _frame.Muzzles.Count == 0 || _frame.Skeleton == null) return null;
            var (bone, def) = _frame.Muzzles[_frame.NextMuzzle++ % _frame.Muzzles.Count];
            var p = _frame.Skeleton.GlobalTransform * _frame.Skeleton.GetBoneGlobalPose(bone).Origin;
            var dir = toward - p;
            dir.Y = 0f;
            dir = dir.LengthSquared() > 0.0001f ? dir.Normalized() : _frame.Holder.GlobalTransform.Basis.Z.Normalized();
            float s = _frame.Holder.Scale.X;
            return (p + dir * def.Forward * s + Vector3.Up * def.Up * s, def.Hand);
        }

        /// <summary>A frame's shell bursts: the rest near what it hit take a share.</summary>
        private void FrameSplash(VineEnemy hit, float dmg)
        {
            var shot = _frame?.Def.Shot;
            if (shot == null || shot.Splash <= 0f || hit == null) return;
            float r2 = shot.Splash * shot.Splash;
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (e == hit || !IsInstanceValid(e) || !e.IsAlive) continue;
                var d = e.GlobalPosition - hit.GlobalPosition;
                d.Y = 0f;
                if (d.LengthSquared() > r2) continue;
                float prev = e.CurrentHealth;
                e.TakeDamage(dmg * shot.SplashShare, DamageKind.Heavy);
                if (!e.IsAlive && prev > 0f) CreditKill();
            }
            VfxFactory.SpawnExplosion(GetTree(), hit.BodyCentre, shot.Splash, FrameShotColor);
        }

        private Color FrameShotColor => _frame == null ? BitEyeColor : ShotColor(new MechProjectile { Color = _frame.Def.Shot.Color });

        // ── The frame's Q, E and R ──

        private static VinePlayerAbility[] FrameAbilities(MechFrame def) => def.Abilities.Select(FrameAbility).ToArray();

        private static VinePlayerAbility FrameAbility(MechAbility a)
        {
            var color = a.Color.StartsWith("#") ? Color.FromHtml(a.Color) : new Color(1f, 0.8f, 0.25f);
            var ability = new VinePlayerAbility
            {
                Name = a.Name,
                Description = a.Text,
                Cooldown = a.Cooldown * MetaRun.AbilityCooldownMult,
                MaterialsCost = a.Cost * MetaRun.AbilityCostMult * RoleRun.AbilityCostMult,
                Range = a.Range,
                IconColor = color,
            };
            ability.Execute = a.Kind switch
            {
                "salvo" => p => Salvo(p, a, color),
                "slam" => p => Blast(p, a.Range, a.Damage, a.Stun, a.Shove, color),
                "repair" => p => Repair(p, a.Range, a.Repair, a.Seconds, color),
                "overclock" => p => OverclockTowers(p, a.Range, a.Seconds, a.Strength, color),
                _ => p => p.AbilityResult = "does nothing yet",
            };
            return ability;
        }

        // ── What abilities do (BIT's and the frames') ──

        /// <summary>Damage and stun everything within <paramref name="radius"/>; <paramref name="shoveCells"/> throws it away from BIT.</summary>
        internal static void Blast(VinePlayer player, float radius, float dmgMult, float stun, float shoveCells, Color color)
        {
            int hit = 0, kills = 0;
            float dmg = player.EffectiveDamage * dmgMult * player.AbilityPower;
            // Chaos power type: the blast throws enemies back the way they came and corrodes them fully
            float toss = PowerType.ThrowCells * Constants.VINE_CELL_SIZE;
            float shove = shoveCells * Constants.VINE_CELL_SIZE;
            var spire = ServiceLocator.Get<VineGrid>()?.Harvester;
            foreach (var enemy in Roster.Enemies(player.GetTree()))
            {
                if (enemy == null || !enemy.IsAlive) continue;
                if (player.GlobalPosition.DistanceTo(enemy.GlobalPosition) > radius) continue;
                float prevHP = enemy.CurrentHealth;
                enemy.TakeDamage(dmg, DamageKind.Heavy);
                enemy.ApplyStun(stun);
                if (enemy.IsAlive && shove > 0f)
                {
                    var away = enemy.GlobalPosition - player.GlobalPosition;
                    away.Y = 0;
                    if (away.LengthSquared() > 0.01f) enemy.ApplyKnockback(away.Normalized() * shove);
                }
                if (toss > 0f && enemy.IsAlive)
                {
                    enemy.Corrode(PowerType.CorrodeStacks);
                    var back = enemy.GlobalPosition - (spire?.GlobalPosition ?? player.GlobalPosition);
                    back.Y = 0;
                    if (back.LengthSquared() > 0.01f) enemy.ApplyKnockback(back.Normalized() * toss * player.AbilityPower);
                }
                hit++;
                if (!enemy.IsAlive && prevHP > 0) { player.CreditKill(); kills++; }
            }
            VfxFactory.SpawnEnergyBurst(player.GetTree(), player.GlobalPosition + Vector3.Up * 0.5f, color, 10);
            VfxFactory.SpawnAbilityRing(player.GetTree(), player.GlobalPosition, radius, color);
            if (shove > 0f && player.IsInsideTree() && ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.3f, 0.25f);
            player.AbilityResult = hit == 0 ? "nothing in reach" : kills > 0 ? $"hit {hit}, {kills} down" : $"hit {hit} for {dmg:0}";
            if (hit > 0 && toss > 0f) player.AbilityResult += ", thrown back and corroded";
        }

        /// <summary>
        /// The Spire (capped a wave), BIT and every tower within <paramref name="radius"/>, each tower
        /// by <paramref name="towerShare"/> of its health; <paramref name="fortifySeconds"/> above 0
        /// fortifies them too (as Environment's pulse does).
        /// </summary>
        internal static void Repair(VinePlayer player, float radius, float towerShare, float fortifySeconds, Color color)
        {
            var grid = ServiceLocator.Get<VineGrid>();
            float spire = 0f;
            bool spireCapped = false;
            if (grid?.Harvester != null)
            {
                float before = grid.Harvester.CurrentHP;
                // The Spire takes so much a wave from the pulse, so leaks still show on it
                int wave = ServiceLocator.TryGet<VineWaveManager>(out var wm) ? wm.CurrentWave : 0;
                if (wave != player._spireRepairWave) { player._spireRepairWave = wave; player._spireRepaired = 0f; }
                float room = Mathf.Max(0f, Constants.ABILITY_REPAIR_SPIRE_PER_WAVE * player.AbilityPower - player._spireRepaired);
                spireCapped = room <= 0.5f && before < grid.Harvester.MaxHP - 0.5f;
                grid.Harvester.Heal(Mathf.Min(room, Constants.ABILITY_REPAIR_SPIRE * player.AbilityPower));
                spire = grid.Harvester.CurrentHP - before;
                player._spireRepaired += spire;
                VfxFactory.SpawnEnergyBurst(player.GetTree(), grid.Harvester.GlobalPosition + Vector3.Up * 2f, new Color(0.2f, 0.9f, 0.4f), 8);
            }
            int towers = 0, fortified = 0;
            // Environment power type: everything in reach is fortified as well
            float fortify = (PowerType.FortifySeconds + fortifySeconds) * player.AbilityPower;
            foreach (var node in player.GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE))
            {
                if (node is not VineNode vine || vine.NodeMaxHealth <= 0 || vine.IsDestroyed) continue;
                if (player.GlobalPosition.DistanceTo(vine.GlobalPosition) > radius) continue;
                if (fortify > 0f)
                {
                    vine.Fortify(fortify);
                    fortified++;
                    VfxFactory.SpawnBuffMotes(player.GetTree(), vine.GlobalPosition, new Color(0.35f, 0.95f, 0.45f));
                }
                if (vine.NodeCurrentHealth >= vine.NodeMaxHealth - 0.5f) continue;
                vine.Repair(vine.NodeMaxHealth * towerShare * player.AbilityPower);
                VfxFactory.SpawnBuffMotes(player.GetTree(), vine.GlobalPosition, new Color(0.3f, 1f, 0.5f));
                towers++;
            }
            float bitBefore = player.CurrentHP;
            player.Heal(player.MaxHP * Constants.ABILITY_REPAIR_BIT_SHARE * player.AbilityPower);
            float self = player.CurrentHP - bitBefore;
            if (self > 0.5f) VfxFactory.SpawnBuffMotes(player.GetTree(), player.GlobalPosition, new Color(0.3f, 1f, 0.5f));
            VfxFactory.SpawnAbilityRing(player.GetTree(), player.GlobalPosition, radius, color);
            var parts = new List<string>();
            if (spire >= 0.5f) parts.Add($"Spire +{spire:0}");
            else if (spireCapped) parts.Add("the Spire is patched as far as it goes this wave");
            if (self >= 0.5f) parts.Add($"BIT +{self:0}");
            if (towers > 0) parts.Add($"{towers} tower(s) repaired");
            if (fortified > 0) parts.Add($"{fortified} fortified");
            player.AbilityResult = parts.Count == 0 ? "nothing needed repair" : string.Join(", ", parts);
        }

        /// <summary>
        /// Overclock every tower within <paramref name="radius"/> for <paramref name="seconds"/>
        /// (longer with ability power); <paramref name="selfRate"/> above 0 also makes the frame's
        /// own guns that much faster for as long.
        /// </summary>
        internal static void OverclockTowers(VinePlayer player, float radius, float seconds, float selfRate, Color color)
        {
            int boosted = 0;
            // Power type: Overclock reaches further and lasts longer
            float reach = radius * PowerType.OverclockReachMult;
            // Ability power (the Obelisk, the Spire's upgrades, the tree) makes it last longer,
            // as it makes the blast and the pulse stronger (it used to do nothing for Overclock)
            float time = seconds * PowerType.OverclockSecondsMult * player.AbilityPower;
            foreach (var node in player.GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE))
            {
                if (node is not VineNode vine) continue;
                if (vine.Data?.Category != VineNodeCategory.Effect) continue;
                if (vine.Data.Type is VineNodeType.BarrierWall or VineNodeType.BuffEmitter or VineNodeType.SalvageDrill) continue;
                if (player.GlobalPosition.DistanceTo(vine.GlobalPosition) > reach) continue;
                vine.ReceiveBuff(Constants.ABILITY_OVERCLOCK_STRENGTH, time);
                OverclockHabit.Boosted(vine, time);
                VfxFactory.SpawnBuffMotes(player.GetTree(), vine.GlobalPosition, color);
                boosted++;
            }
            if (selfRate > 0f && player._frame != null)
            {
                player._frame.SelfRateMult = 1f + selfRate;
                player._frame.SelfRateTime = time;
            }
            VfxFactory.SpawnEnergyBurst(player.GetTree(), player.GlobalPosition + Vector3.Up * 0.5f, color, 8);
            VfxFactory.SpawnAbilityRing(player.GetTree(), player.GlobalPosition, reach, color);
            player.AbilityResult = boosted == 0 ? "no towers in reach" : $"{boosted} tower(s) boosted for {time:0} s";
            if (selfRate > 0f && player._frame != null) player.AbilityResult += $", guns +{selfRate * 100:0}%";
        }

        /// <summary>Rockets at the toughest enemies in reach, one each (round again when there are fewer), each bursting.</summary>
        private static void Salvo(VinePlayer player, MechAbility a, Color color)
        {
            var targets = Roster.Enemies(player.GetTree())
                .Where(e => e != null && e.IsAlive && Flat(e.GlobalPosition, player.GlobalPosition) <= a.Range)
                .OrderByDescending(e => e.CurrentHealth).Take(Mathf.Max(1, a.Count)).ToList();
            if (targets.Count == 0) { player.AbilityResult = "nothing in reach"; return; }
            float dmg = player.EffectiveDamage * a.Damage * player.AbilityPower;
            var tree = player.GetTree();
            int count = Mathf.Max(1, a.Count), kills = 0, hits = 0;
            for (int i = 0; i < count; i++)
            {
                var t = targets[i % targets.Count];
                var from = player.GlobalPosition + Vector3.Up * player.BodyHeight * 0.85f;
                float delay = 0.28f + i * 0.07f;
                VfxFactory.SpawnShell(tree, from, t.BodyCentre, color, delay);
                var target = t;
                tree.CreateTimer(delay, false).Timeout += () =>
                {
                    if (!IsInstanceValid(player) || !IsInstanceValid(target)) return;
                    var at = target.GlobalPosition;
                    var prev = VineEnemy.HitSource;
                    VineEnemy.HitSource = "BIT";
                    try
                    {
                        foreach (var e in Roster.Enemies(tree))
                        {
                            if (e == null || !e.IsAlive) continue;
                            float d = Flat(e.GlobalPosition, at);
                            if (d > a.Radius) continue;
                            float before = e.CurrentHealth;
                            e.TakeDamage(e == target ? dmg : dmg * 0.5f, DamageKind.Heavy);
                            hits++;
                            if (!e.IsAlive && before > 0f) { player.CreditKill(); kills++; }
                        }
                    }
                    finally { VineEnemy.HitSource = prev; }
                    VfxFactory.SpawnExplosion(tree, at + Vector3.Up * 0.4f, a.Radius, color);
                };
            }
            VfxFactory.SpawnAbilityRing(tree, player.GlobalPosition, a.Range, color);
            player.AbilityResult = $"{count} rockets at {targets.Count} target(s), {dmg:0} each";
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    }
}
