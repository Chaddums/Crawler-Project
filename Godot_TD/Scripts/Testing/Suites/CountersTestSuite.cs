using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Enemies that need an answer, and towers that can be upgraded into one. Traits: armour cuts
    /// light and electric hits to 30% and ordinary ones to 75% (heavy hits land in full, and
    /// Shredder strips it); a shield takes hits first, electric ones three times over, and grows
    /// back once left alone; flyers keep above the ground, go straight over walls at the Spire,
    /// and only anti-air towers shoot them (Flak first). The waves bring each trait in and the
    /// wave card names it and its answer. Upgrades: every tower has two levels and two branches,
    /// each costs what it says, raises what it says, a branch is one or the other, and selling
    /// returns the upgrades too. The tower panel opens on a click (BIT doesn't fire), buys,
    /// and closes on Esc. Headless.
    /// </summary>
    public class CountersTestSuite : ITestSuite
    {
        public string SuiteName => "counters";

        public async Task Run(TestContext ctx)
        {
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "counters/battle_loads");
            if (!ok) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            var grid = ServiceLocator.Get<VineGrid>();
            grid.Harvester?.IncreaseMaxHP(100000f);
            GameManager.Instance.AddResources(10000);

            Damage(ctx, grid);
            await Shield(ctx, grid);
            await Flight(ctx, grid);
            WaveData(ctx);
            await Upgrades(ctx, grid);
            await Panel(ctx, grid);

            if (waves != null) waves.PauseAutoStart = false;
        }

        private static VineEnemy Dummy(TestContext ctx, VineGrid grid, Vector3 at, float hp, EnemyTraits traits, float speed = 0f,
            VineEnemyFaction faction = VineEnemyFaction.Scavenger)
        {
            var e = new VineEnemy();
            ctx.Tree.CurrentScene.AddChild(e);
            e.Initialize("Dummy", faction, hp, speed, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(at));
            e.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
            if (traits != EnemyTraits.None) e.SetTraits(traits);
            return e;
        }

        private static Vector3 Spot(VineGrid grid, int i)
        {
            var row = TowerSheetSuite.FindRow(grid, 13);
            var c = row.Count > 0 ? row[i % row.Count] : grid.ExitPoint + new Vector2I(-6, i);
            var p = grid.GridToWorld(c);
            return new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
        }

        // ── Armour ──

        private static void Damage(TestContext ctx, VineGrid grid)
        {
            ctx.StartTest();
            var e = Dummy(ctx, grid, Spot(grid, 2), 1000f, EnemyTraits.Armoured);
            float Hit(DamageKind k) { float before = e.CurrentHealth; e.TakeDamage(10f, k); return before - e.CurrentHealth; }
            float light = Hit(DamageKind.Light), elec = Hit(DamageKind.Electric), normal = Hit(DamageKind.Normal), heavy = Hit(DamageKind.Heavy);
            ctx.Assert(Mathf.IsEqualApprox(light, 10f * VineEnemy.ARMOUR_LIGHT) && Mathf.IsEqualApprox(elec, 10f * VineEnemy.ARMOUR_LIGHT)
                && Mathf.IsEqualApprox(normal, 10f * VineEnemy.ARMOUR_NORMAL) && Mathf.IsEqualApprox(heavy, 10f),
                "counters/armour_by_kind", $"10 light {light:F2}, electric {elec:F2}, normal {normal:F2}, heavy {heavy:F2}");
            e.BreakArmour(1f);
            ctx.Assert(Mathf.IsEqualApprox(Hit(DamageKind.Light), 10f), "counters/armour_broken_takes_full", "a light hit on broken armour");

            // The shell: on its own rig beside the model, over the body, turning with it
            ctx.StartTest();
            var rig = e.ArmourRig;
            var issues = new List<string>();
            if (rig == null) issues.Add("no rig");
            else
            {
                if (rig.GetParent() != e) issues.Add($"rig under {rig.GetParent()?.Name}");
                float reach = e.BodyRadius * 1.6f + 0.2f;
                foreach (var plate in rig.GetChildren().OfType<Node3D>())
                {
                    var p = rig.Transform * plate.Position;
                    float side = new Vector2(p.X, p.Z).Length();
                    if (side > reach || p.Y < 0f || p.Y > 3.5f) issues.Add($"part at ({p.X:F2}, {p.Y:F2}, {p.Z:F2}), body {e.BodyRadius:F2}");
                }
                if (rig.GetChildCount() < 3) issues.Add($"{rig.GetChildCount()} parts");
                e.TestFaceYaw(1.1f);
                if (!Mathf.IsEqualApprox(rig.Rotation.Y, 1.1f)) issues.Add($"rig yaw {rig.Rotation.Y:F2} for a body at 1.10");
            }
            ctx.Assert(issues.Count == 0, "counters/armour_shell_on_body", string.Join("; ", issues));
            e.QueueFree();

            // The towers hit the way the cards say
            ctx.StartTest();
            var kinds = new Dictionary<VineNodeType, DamageKind>
            {
                { VineNodeType.DamageTower, DamageKind.Heavy }, { VineNodeType.ScatterCannon, DamageKind.Heavy },
                { VineNodeType.FlakBattery, DamageKind.Light }, { VineNodeType.TeslaCoil, DamageKind.Electric },
            };
            var wrong = new List<string>();
            foreach (var (t, k) in kinds)
            {
                var n = new VineNode();
                n.Initialize(VineNodeRegistry.Get(t));
                if (n.HitKind != k) wrong.Add($"{t} {n.HitKind}");
                bool air = t is VineNodeType.FlakBattery or VineNodeType.TeslaCoil;
                if (n.CanHitAir != air) wrong.Add($"{t} air {n.CanHitAir}");
                n.Free();
            }
            ctx.Assert(wrong.Count == 0, "counters/tower_kinds_and_air", string.Join("; ", wrong));
        }

        // ── Shields ──

        private static async Task Shield(TestContext ctx, VineGrid grid)
        {
            ctx.StartTest();
            var e = Dummy(ctx, grid, Spot(grid, 4), 100f, EnemyTraits.Shielded);
            ctx.Assert(Mathf.IsEqualApprox(e.ShieldMax, 100f * VineEnemy.SHIELD_SHARE), "counters/shield_size", $"shield {e.ShieldMax}");
            e.TakeDamage(10f, DamageKind.Normal);
            bool soaked = Mathf.IsEqualApprox(e.CurrentHealth, 100f) && Mathf.IsEqualApprox(e.ShieldHP, e.ShieldMax - 10f);
            e.TakeDamage(10f, DamageKind.Electric);
            bool tripled = Mathf.IsEqualApprox(e.ShieldHP, e.ShieldMax - 40f);
            ctx.Assert(soaked && tripled, "counters/shield_first_electric_triple",
                $"after a normal 10 and an electric 10: health {e.CurrentHealth:F0}, shield {e.ShieldHP:F0}/{e.ShieldMax:F0}");
            // Through the shield: the overflow reaches health at its own rate
            e.TakeDamage(30f, DamageKind.Normal);
            ctx.Assert(e.ShieldHP <= 0.01f && Mathf.IsEqualApprox(e.CurrentHealth, 90f), "counters/shield_overflow",
                $"health {e.CurrentHealth:F1} (want 90), shield {e.ShieldHP:F1}");
            float low = e.ShieldHP;
            await ctx.Wait(VineEnemy.SHIELD_REGEN_DELAY + 1.2f);
            ctx.Assert(e.ShieldHP > low + e.ShieldMax * VineEnemy.SHIELD_REGEN * 0.5f, "counters/shield_grows_back",
                $"shield {low:F1} -> {e.ShieldHP:F1} after {VineEnemy.SHIELD_REGEN_DELAY + 1.2f:F1} s unhit");
            e.QueueFree();
        }

        // ── Flight ──

        private static async Task Flight(TestContext ctx, VineGrid grid)
        {
            ctx.StartTest();
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            var region = grid.ActiveEntryRegions.First();
            var start = grid.GridToWorld(region.Center);
            var flyer = Dummy(ctx, grid, start, 1e6f, EnemyTraits.Flying, speed: 4f, faction: VineEnemyFaction.Swarm);
            await ctx.Wait(0.4f);
            var p = flyer.GlobalPosition;
            float above = p.Y - grid.GetWorldHeight(p.X, p.Z);
            ctx.Assert(above > VineEnemy.FLY_HEIGHT * 0.7f, "counters/flyer_keeps_height", $"{above:F2} above the ground");

            // Straight at the Spire: its track stays near the straight line, wherever the path goes
            var spire = grid.GridToWorld(grid.ExitPoint);
            float maxOff = 0f;
            for (int i = 0; i < 10 && GodotObject.IsInstanceValid(flyer) && flyer.IsAlive; i++)
            {
                await ctx.Wait(0.3f);
                if (!GodotObject.IsInstanceValid(flyer)) break;
                var q = flyer.GlobalPosition;
                var a = new Vector2(start.X, start.Z); var b = new Vector2(spire.X, spire.Z); var c = new Vector2(q.X, q.Z);
                var ab = b - a;
                float t = Mathf.Clamp((c - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
                maxOff = Mathf.Max(maxOff, (a + ab * t).DistanceTo(c));
            }
            ctx.Assert(maxOff < 1.0f, "counters/flyer_goes_straight", $"strayed {maxOff:F2} from the straight line to the Spire");

            // Ground towers don't shoot it; anti-air does, and Flak takes it before a ground target
            ctx.StartTest();
            var at = Spot(grid, 6);
            var air = Dummy(ctx, grid, at + new Vector3(3f, 0, 0), 1e6f, EnemyTraits.Flying, faction: VineEnemyFaction.Swarm);
            var cell = grid.WorldToGrid(at);
            var dealt = new Dictionary<VineNodeType, float>();
            foreach (var t in new[] { VineNodeType.DamageTower, VineNodeType.ScatterCannon, VineNodeType.FlakBattery, VineNodeType.TeslaCoil })
            {
                var n = new VineNode();
                n.Initialize(VineNodeRegistry.Get(t));
                if (!grid.CanPlace(cell) || !grid.PlaceNode(n, cell)) { n.QueueFree(); continue; }
                air.GlobalPosition = new Vector3(at.X + 3f, grid.GetWorldHeight(at.X + 3f, at.Z) + VineEnemy.FLY_HEIGHT, at.Z);
                air.SetProcess(false); // hold it in place
                float before = air.CurrentHealth;
                await ctx.Wait(2.5f);
                dealt[t] = before - air.CurrentHealth;
                grid.RemoveNode(cell);
                await ctx.Wait(0.1f);
            }
            ctx.Assert(dealt.GetValueOrDefault(VineNodeType.DamageTower) <= 0f && dealt.GetValueOrDefault(VineNodeType.ScatterCannon) <= 0f,
                "counters/ground_towers_miss_flyers", $"Junk Turret {dealt.GetValueOrDefault(VineNodeType.DamageTower):F0}, Scatter {dealt.GetValueOrDefault(VineNodeType.ScatterCannon):F0}");
            ctx.Assert(dealt.GetValueOrDefault(VineNodeType.FlakBattery) > 0f && dealt.GetValueOrDefault(VineNodeType.TeslaCoil) > 0f,
                "counters/anti_air_hits_flyers", $"Flak {dealt.GetValueOrDefault(VineNodeType.FlakBattery):F0}, Tesla {dealt.GetValueOrDefault(VineNodeType.TeslaCoil):F0}");
            air.QueueFree();
            if (GodotObject.IsInstanceValid(flyer)) flyer.QueueFree();
        }

        // ── The waves bring them, and the card says what answers them ──

        private static void WaveData(TestContext ctx)
        {
            foreach (int planet in new[] { 1, 2 })
            {
                ctx.StartTest();
                var w = VineWaveLoader.LoadPlanetWaves(planet);
                EnemyTraits first(int upTo) { var t = EnemyTraits.None; for (int i = 0; i < upTo && i < w.Count; i++) foreach (var s in w[i].Surges) t |= s.Traits; return t; }
                var by8 = first(8);
                ctx.Assert((by8 & EnemyTraits.Armoured) != 0 && (by8 & EnemyTraits.Flying) != 0 && (by8 & EnemyTraits.Shielded) != 0,
                    $"counters/P{planet}/traits_by_wave_8", $"traits in waves 1 to 8: {by8}");
                ctx.Assert(first(3) == EnemyTraits.None, $"counters/P{planet}/first_waves_plain", $"traits in waves 1 to 3: {first(3)}");
                int lastAuthored = w.Count;
                var late = EnemyTraits.None; foreach (var s in w[lastAuthored - 1].Surges) late |= s.Traits;
                ctx.Assert(late != EnemyTraits.None, $"counters/P{planet}/traits_carry_late", "the last authored wave (procedural waves copy the last ones) has traits");
            }
            VineWaveLoader.LoadPlanetWaves(1);
            ctx.StartTest();
            var miss = new[] { EnemyTraits.Armoured, EnemyTraits.Flying, EnemyTraits.Shielded }.Where(t => VineHUD.TraitCounter(t).Length == 0).ToList();
            ctx.Assert(miss.Count == 0, "counters/card_names_answers", $"no counter text for {string.Join(", ", miss)}");
        }

        // ── Upgrades ──

        private static async Task Upgrades(TestContext ctx, VineGrid grid)
        {
            var at = Spot(grid, 8);
            var cell = grid.WorldToGrid(at);
            foreach (var type in VineDraftScreen.GetRoleNodes(0))
            {
                var data = VineNodeRegistry.Get(type);
                string p = $"counters/upgrade/{type}";
                ctx.StartTest();
                var plan = TowerUpgradePlan.For(data.Id);
                ctx.Assert(plan != null && plan.Levels.Count == 2 && plan.Branches.Count == 2, $"{p}/plan", "two levels and two branches");
                if (plan == null) continue;
                var n = new VineNode();
                n.Initialize(data);
                if (!grid.CanPlace(cell) || !grid.PlaceNode(n, cell)) { n.QueueFree(); ctx.Assert(false, $"{p}/placed"); continue; }
                await ctx.Wait(0.05f);
                var gm = GameManager.Instance;
                int sell0 = n.SellValue;
                string stat0 = n.StatLine();
                float dm0 = n.UpgradeDamageMult, rg0 = n.CurrentRange;

                // Branch refused before the top level
                ctx.Assert(n.CantBranch(plan.Branches[0].Id) != null && !n.TryBranch(plan.Branches[0].Id), $"{p}/no_branch_early");
                int r0 = gm.CurrentResources;
                bool up1 = n.TryUpgrade();
                ctx.Assert(up1 && n.Level == 2 && gm.CurrentResources == r0 - plan.Levels[0].Cost, $"{p}/level_2_costs",
                    $"level {n.Level}, spent {r0 - gm.CurrentResources} (want {plan.Levels[0].Cost})");
                bool up2 = n.TryUpgrade();
                ctx.Assert(up2 && n.Level == 3 && !n.TryUpgrade(), $"{p}/level_3_then_top", $"level {n.Level}");
                bool raised = (plan.Levels[0].Damage > 0 && n.UpgradeDamageMult > dm0) || (plan.Levels[0].Range > 0 && n.CurrentRange > rg0)
                    || plan.Levels[0].Hp > 0 || plan.Levels[0].Buff > 0 || plan.Levels[0].Slow > 0 || plan.Levels[0].Force > 0;
                ctx.Assert(raised && n.StatLine() != stat0 || type is VineNodeType.BarrierWall or VineNodeType.BuffEmitter, $"{p}/levels_raise_stats",
                    $"{stat0} -> {n.StatLine()}");
                var b0 = plan.Branches[0];
                int r1 = gm.CurrentResources;
                ctx.Assert(n.TryBranch(b0.Id) && n.Branch?.Id == b0.Id && gm.CurrentResources == r1 - b0.Cost, $"{p}/branch_costs",
                    $"branch {n.Branch?.Id}, spent {r1 - gm.CurrentResources} (want {b0.Cost})");
                ctx.Assert(!n.TryBranch(plan.Branches[1].Id) && n.Branch?.Id == b0.Id, $"{p}/one_branch_only");
                foreach (var g in b0.Grants) ctx.Assert(n.Has(g), $"{p}/branch_grants_{g}");
                int spent = plan.Levels[0].Cost + plan.Levels[1].Cost + b0.Cost;
                ctx.Assert(n.SellValue == Mathf.RoundToInt((data.ResourceCost + spent) * SignalTuningEditor.SellRefund) && n.SellValue > sell0,
                    $"{p}/sell_returns_upgrades", $"sells for {n.SellValue} (was {sell0})");
                grid.RemoveNode(cell);
                await ctx.Wait(0.1f);
            }

            // A branch that matters: the Minigun turns a Junk Turret into anti-air
            ctx.StartTest();
            var m = new VineNode();
            m.Initialize(VineNodeRegistry.Get(VineNodeType.DamageTower));
            if (grid.PlaceNode(m, cell))
            {
                m.ForceUpgrade(3, "minigun");
                ctx.Assert(m.CanHitAir && m.HitKind == DamageKind.Normal, "counters/minigun_hits_air", $"air {m.CanHitAir}, kind {m.HitKind}");
                grid.RemoveNode(cell);
            }
            else m.QueueFree();
            await ctx.Wait(0.1f);
        }

        // ── The panel, through real input ──

        private static async Task Panel(TestContext ctx, VineGrid grid)
        {
            ctx.StartTest();
            var vp = ctx.Tree.Root;
            var cam = vp.GetCamera3D();
            var insp = TowerInspector.Current;
            ServiceLocator.TryGet<VinePlayer>(out var player);
            ctx.Assert(insp != null && cam != null && player != null, "counters/panel/exists");
            if (insp == null || cam == null || player == null) return;

            // A tower in the middle of the screen
            VineNode tower = null;
            var size = vp.GetVisibleRect().Size;
            for (int x = 2; x < grid.Width - 2 && tower == null; x++)
                for (int y = 2; y < grid.Height - 2 && tower == null; y++)
                {
                    var c = new Vector2I(x, y);
                    if (!grid.CanPlace(c)) continue;
                    ServiceLocator.TryGet<VinePathfinder>(out var pf);
                    if (pf != null && pf.WouldBlockAllPaths(c)) continue;
                    var w = grid.GridToWorld(c) + Vector3.Up * 0.5f;
                    if (cam.IsPositionBehind(w)) continue;
                    var s = cam.UnprojectPosition(w);
                    if (s.X < size.X * 0.35f || s.X > size.X * 0.65f || s.Y < size.Y * 0.3f || s.Y > size.Y * 0.6f) continue;
                    var n = new VineNode();
                    n.Initialize(VineNodeRegistry.Get(VineNodeType.DamageTower));
                    if (grid.PlaceNode(n, c)) tower = n; else n.QueueFree();
                }
            ctx.Assert(tower != null, "counters/panel/tower_on_screen");
            if (tower == null) return;
            await ctx.Wait(0.2f);
            var at = cam.UnprojectPosition(tower.GlobalPosition + Vector3.Up * 0.5f);
            int shots0 = player.AimedShots;
            await Click(ctx, at);
            await ctx.Wait(0.3f);
            ctx.Assert(insp.PanelOpen && insp.Selected == tower, "counters/panel/click_opens", $"open {insp.PanelOpen}, selected {(insp.Selected == tower ? "it" : "other")}");
            ctx.Assert(player.AimedShots == shots0 && !player.IsAiming, "counters/panel/bit_holds_fire", $"{player.AimedShots - shots0} aimed shots");

            var panel = PlaytestSuite.All<TowerPanel>(ctx.Tree.Root).FirstOrDefault();
            var btn = panel?.UpgradeButton;
            ctx.Assert(btn != null && btn.IsVisibleInTree() && !btn.Disabled, "counters/panel/upgrade_button");
            if (btn != null)
            {
                btn.EmitSignal(BaseButton.SignalName.Pressed);
                await ctx.Wait(0.1f);
                ctx.Assert(tower.Level == 2, "counters/panel/upgrade_buys", $"level {tower.Level}");
            }
            vp.PushInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
            await ctx.Wait(0.1f);
            vp.PushInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
            await ctx.Wait(0.2f);
            var pause = PlaytestSuite.All<PauseMenu>(ctx.Tree.Root).FirstOrDefault();
            ctx.Assert(!insp.PanelOpen && (pause == null || !pause.Visible), "counters/panel/esc_closes_not_pause",
                $"panel {insp.PanelOpen}, pause {pause?.Visible}");
            if (pause?.Visible == true) pause.Resume();
            grid.RemoveNode(tower.GridPosition);
            await ctx.Wait(0.1f);
        }

        private static async Task Click(TestContext ctx, Vector2 pos)
        {
            var vp = ctx.Tree.Root;
            vp.WarpMouse(pos);
            vp.PushInput(new InputEventMouseMotion { Position = pos, GlobalPosition = pos }, true);
            await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
            vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = pos, GlobalPosition = pos }, true);
            for (int i = 0; i < 2; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
            vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = pos, GlobalPosition = pos }, true);
            for (int i = 0; i < 2; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }
    }
}
