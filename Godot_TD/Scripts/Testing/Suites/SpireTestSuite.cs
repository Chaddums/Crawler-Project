using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// BIT at the Spire in a real battle: F opens the Spire menu only within reach; every
    /// upgrade costs what it says, in the right currency, and does what it says; buying through
    /// the menu's buttons works and is refused without the money; standing still at a damaged
    /// Spire repairs it with BIT's Materials (moving doesn't); refill and training spend banked
    /// Materials; G climbs in (BIT hidden and safe), the cannon hits where it aims, G climbs out;
    /// Materials mode banks a share of every drop.
    /// </summary>
    public class SpireTestSuite : ITestSuite
    {
        public string SuiteName => "spire";

        public async Task Run(TestContext ctx)
        {
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "spire/battle_loads");
            if (!ok) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            var grid = ServiceLocator.Get<VineGrid>();
            var h = grid.Harvester;
            ServiceLocator.TryGet<VinePlayer>(out var player);
            var st = SpireStation.Current;
            var gm = GameManager.Instance;
            ctx.StartTest();
            ctx.Assert(st != null && h != null && player != null, "spire/station_exists");
            if (st == null || h == null || player == null) return;
            await ctx.Wait(0.5f);

            Vector3 Near() { var p = h.GlobalPosition + new Vector3(Constants.VINE_CELL_SIZE * 1.4f, 0, 0); return new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z); }
            Vector3 Far() { var p = h.GlobalPosition + new Vector3(-14f, 0, 0); return new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z); }
            async Task Key(Key k)
            {
                Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
                await ctx.Wait(0.05f);
                Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
                await ctx.Wait(0.15f);
            }

            // ── Reach ──
            ctx.StartTest();
            player.GlobalPosition = Far();
            await ctx.Wait(0.2f);
            await Key(Godot.Key.F);
            bool farOpens = st.PanelOpen;
            player.GlobalPosition = Near();
            await ctx.Wait(0.2f);
            bool prompt = st.InReach;
            await Key(Godot.Key.F);
            bool nearOpens = st.PanelOpen;
            await Key(Godot.Key.F);
            bool closes = !st.PanelOpen;
            ctx.Assert(!farOpens, "spire/f_far_does_nothing");
            ctx.Assert(prompt && nearOpens, "spire/f_near_opens_menu", $"in reach {prompt}, open {nearOpens}");
            ctx.Assert(closes, "spire/f_closes_menu");

            // ── Upgrades ──
            gm.AddResources(2000);
            h.AddMaterials(2000);
            float dmg0 = player.EffectiveDamage, rate0 = player.EffectiveAttackSpeed, range0 = player.EffectiveRange;
            float maxMat0 = player.MaxMaterials, hp0 = h.MaxHP;
            foreach (var u in st.Data.Upgrades)
            {
                ctx.StartTest();
                int res = gm.CurrentResources;
                float mat = st.MaterialsBanked;
                int cost = u.CostAt(st.LevelOf(u.Id));
                bool bought = st.Buy(u.Id);
                float spentRes = res - gm.CurrentResources;
                float spentMat = mat - st.MaterialsBanked;
                bool right = u.UsesMaterials ? Mathf.IsEqualApprox(spentMat, cost) && spentRes == 0 : spentRes == cost && Mathf.IsZeroApprox(spentMat);
                ctx.Assert(bought && st.LevelOf(u.Id) == 1 && right, $"spire/buy_{u.Id}",
                    $"bought {bought}, level {st.LevelOf(u.Id)}, spent {spentRes} Resources and {spentMat:F1} Materials for a cost of {cost} {u.Currency}");
            }
            ctx.StartTest();
            float Step(string id) => st.Upgrade(id).Step;
            ctx.Assert(Mathf.IsEqualApprox(player.EffectiveDamage / dmg0, 1f + Step("bit_weapon")), "spire/weapon_raises_damage",
                $"{dmg0:F1} -> {player.EffectiveDamage:F1}");
            ctx.Assert(Mathf.IsEqualApprox(player.EffectiveAttackSpeed / rate0, 1f + Step("bit_trigger")), "spire/trigger_raises_rate",
                $"{rate0:F2} -> {player.EffectiveAttackSpeed:F2}");
            ctx.Assert(Mathf.IsEqualApprox(player.EffectiveRange - range0, Step("bit_reach")), "spire/reach_raises_range",
                $"{range0:F1} -> {player.EffectiveRange:F1}");
            ctx.Assert(Mathf.IsEqualApprox(player.MaxMaterials - maxMat0, 40f) && Mathf.IsEqualApprox(player.AbilityPower, (1f + Step("bit_core")) * RoleRun.AbilityPowerMult),
                "spire/core_raises_abilities", $"max Materials {maxMat0} -> {player.MaxMaterials}, power {player.AbilityPower:F2}");
            ctx.Assert(Mathf.IsEqualApprox(h.MaxHP - hp0, hp0 * Step("spire_plating"), 0.5f), "spire/plating_raises_hp",
                $"{hp0} -> {h.MaxHP}");
            ctx.Assert(Mathf.IsEqualApprox(st.SpireDamageMult, 1f + Step("spire_guns")) && Mathf.IsEqualApprox(st.SpireRangeBonus, Step("spire_reach")),
                "spire/guns_and_reach", $"damage x{st.SpireDamageMult:F2}, range +{st.SpireRangeBonus:F1}");
            // What's bought shows on the Spire: a gun per Spire Guns level, plates for Plating
            var gun0 = h.ExtraGunLooks.FirstOrDefault();
            ctx.Assert(h.ExtraGunCount == st.LevelOf("spire_guns") && gun0 != null && gun0.IsVisibleInTree(),
                "spire/guns_show_on_the_spire", $"{h.ExtraGunCount} guns for level {st.LevelOf("spire_guns")}");
            ctx.Assert(h.PlatingShown == st.LevelOf("spire_plating"), "spire/plating_shows", $"plates at level {h.PlatingShown}");
            {
                // The bought gun shoots by itself during a wave
                int shots0 = h.ExtraGunShots;
                var gunTarget = new VineEnemy();
                ctx.Tree.CurrentScene.AddChild(gunTarget);
                var at = h.GlobalPosition + new Vector3(6f, 0, 3f);
                var grid2 = ServiceLocator.Get<VineGrid>();
                gunTarget.Initialize("Gun Target", VineEnemyFaction.Brute, 1e6f, 0f, 0, new Color(1, 0.3f, 0.3f), grid2.WorldToGrid(at));
                gunTarget.GlobalPosition = new Vector3(at.X, grid2.GetWorldHeight(at.X, at.Z), at.Z);
                var phase = gm.CurrentPhase;
                gm.SetPhase(GamePhase.Wave);
                bool shot = await ctx.WaitUntil(() => h.ExtraGunShots > shots0, 4f);
                gm.SetPhase(phase);
                gunTarget.QueueFree();
                ctx.Assert(shot, "spire/bought_gun_fires", $"{h.ExtraGunShots - shots0} shots");
            }

            // Strikes: buy a charge with Resources, fire it with its key where you aim
            {
                ctx.StartTest();
                gm.AddResources(5000);
                var grid3 = ServiceLocator.Get<VineGrid>();
                VineEnemy Target(Vector3 at, float hp, EnemyTraits traits = EnemyTraits.None)
                {
                    var e = new VineEnemy();
                    ctx.Tree.CurrentScene.AddChild(e);
                    e.Initialize("Strike Target", VineEnemyFaction.Brute, hp, 0f, 0, new Color(1, 0.3f, 0.3f), grid3.WorldToGrid(at));
                    e.GlobalPosition = new Vector3(at.X, grid3.GetWorldHeight(at.X, at.Z), at.Z);
                    if (traits != EnemyTraits.None) e.SetTraits(traits);
                    return e;
                }
                foreach (var sk in st.Data.Strikes)
                {
                    int money = gm.CurrentResources, cost = st.StrikeCost(sk.Id), have = st.Charges(sk.Id);
                    bool bought = st.BuyStrike(sk.Id);
                    ctx.Assert(bought && st.Charges(sk.Id) == have + 1 && money - gm.CurrentResources == cost, $"spire/strike_{sk.Id}_buys",
                        $"bought {bought}, {st.Charges(sk.Id)} ready, paid {money - gm.CurrentResources} of {cost}");
                }
                var spot = h.GlobalPosition + new Vector3(8f, 0, 5f);
                var lanceT = Target(spot, 1e6f);
                float lanceHp0 = lanceT.CurrentHealth;
                st.TestAimPoint = lanceT.GlobalPosition;
                // Key 1 fires the first strike where the aim is
                Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.Key1, PhysicalKeycode = Godot.Key.Key1, Pressed = true });
                await ctx.Wait(0.1f);
                Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.Key1, PhysicalKeycode = Godot.Key.Key1, Pressed = false });
                ctx.Assert(lanceT.CurrentHealth < lanceHp0 - st.Strike("lance").DamageAt(gm.CurrentWave) * 0.6f && st.Charges("lance") == 0,
                    "spire/lance_hits_hard", $"took {lanceHp0 - lanceT.CurrentHealth:F0}, {st.Charges("lance")} left");
                lanceT.QueueFree();
                var group = new System.Collections.Generic.List<VineEnemy>();
                for (int i = 0; i < 5; i++) group.Add(Target(spot + new Vector3(i * 1.2f - 2.4f, 0, (i % 2) * 1.5f), 1e6f, i == 0 ? EnemyTraits.Shielded : EnemyTraits.None));
                st.FireStrike("barrage", spot);
                await ctx.Wait(st.Strike("barrage").Seconds + 0.6f);
                int hurt = group.Count(e => e.CurrentHealth < 1e6f - 1f || e.ShieldHP < e.ShieldMax - 1f);
                ctx.Assert(hurt >= 3, "spire/barrage_covers_an_area", $"{hurt} of 5 hit");
                st.FireStrike("emp", spot);
                await ctx.Wait(0.1f);
                ctx.Assert(group.All(e => e.IsStunned) && group[0].ShieldHP <= 0.01f, "spire/emp_stuns_and_strips",
                    $"stunned {group.Count(e => e.IsStunned)}/5, shield {group[0].ShieldHP:F0}");
                foreach (var e in group) e.QueueFree();
                st.TestAimPoint = null;
                // The HUD lists charges that are ready
                st.BuyStrike("emp");
                await ctx.Wait(0.7f);
                var hudStrikes = ctx.Tree.CurrentScene.FindChildren("*", "Label", true, false).OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("STRIKES READY"));
                ctx.Assert(hudStrikes != null && hudStrikes.IsVisibleInTree() && hudStrikes.Text.Contains("EMP"), "spire/strikes_on_hud", hudStrikes?.Text ?? "none");
                st.FireStrike("emp", spot);
            }

            // Through the menu's buttons, and refused without the money
            ctx.StartTest();
            st.OpenPanel();
            await ctx.Wait(0.3f);
            var panel = All<SpirePanel>(ctx.Tree.Root).FirstOrDefault();
            var btn = panel?.BuyButton("bit_weapon");
            int before = st.LevelOf("bit_weapon");
            btn?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.3f);
            bool viaButton = st.LevelOf("bit_weapon") == before + 1;
            h.SpendMaterials(st.MaterialsBanked);
            await ctx.Wait(0.35f);
            bool refusedNow = st.CantBuy("bit_trigger") == "Needs Materials" && !st.Buy("bit_trigger")
                && panel?.BuyButton("bit_trigger")?.Disabled == true;
            st.ClosePanel();
            ctx.Assert(btn != null && viaButton, "spire/menu_button_buys");
            ctx.Assert(refusedNow, "spire/refused_without_materials");

            // ── Repair: holding F repairs, standing there doesn't ──
            ctx.StartTest();
            player.GlobalPosition = Near();
            h.TakeDamage(60f);
            player.CurrentMaterials = player.MaxMaterials;
            float regen = player.MaterialsRegen;
            player.MaterialsRegen = 0f; // count only what repairing costs
            float hpIdle = h.CurrentHP;
            await ctx.Wait(1f);
            float idleHealed = h.CurrentHP - hpIdle;
            ctx.Assert(idleHealed < 0.5f, "spire/standing_doesnt_repair", $"healed {idleHealed:F1} without F held");
            float hpA = h.CurrentHP, matA = player.CurrentMaterials;
            st.TestHoldRepair = true;
            await ctx.Wait(2f);
            st.TestHoldRepair = false;
            float healed = h.CurrentHP - hpA, used = matA - player.CurrentMaterials;
            player.MaterialsRegen = regen;
            ctx.Assert(healed > 15f && used > 0f && Mathf.IsEqualApprox(used / healed, st.Data.Repair.MaterialsPerHp, 0.05f),
                "spire/holding_f_repairs", $"healed {healed:F1} for {used:F1} Materials");
            // The real key: held past the tap time it repairs, and letting go doesn't open the menu
            h.TakeDamage(30f);
            Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.F, PhysicalKeycode = Godot.Key.F, Pressed = true });
            await ctx.Wait(0.8f);
            bool repairingByKey = st.Repairing;
            Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.F, PhysicalKeycode = Godot.Key.F, Pressed = false });
            await ctx.Wait(0.2f);
            ctx.Assert(repairingByKey, "spire/held_f_key_repairs");
            ctx.Assert(!st.PanelOpen, "spire/hold_doesnt_open_menu");

            // ── Bank ──
            ctx.StartTest();
            player.GlobalPosition = Near();
            h.AddMaterials(200);
            player.CurrentMaterials = 0;
            float bank0 = st.MaterialsBanked;
            bool refilled = st.Refill();
            ctx.Assert(refilled && Mathf.IsEqualApprox(player.CurrentMaterials, player.MaxMaterials)
                && Mathf.IsEqualApprox(bank0 - st.MaterialsBanked, st.Data.Bank.RefillCost), "spire/refill",
                $"BIT {player.CurrentMaterials:F0}/{player.MaxMaterials:F0}, bank {bank0:F0} -> {st.MaterialsBanked:F0}");
            float xp0 = player.Progression.TotalXp;
            bool trained = st.Train();
            ctx.Assert(trained && Mathf.IsEqualApprox(player.Progression.TotalXp - xp0, st.Data.Bank.TrainXp), "spire/train",
                $"XP {xp0:F0} -> {player.Progression.TotalXp:F0}");

            // ── Climb in ──
            ctx.StartTest();
            player.GlobalPosition = Near();
            await ctx.Wait(0.2f);
            var mapCam = ctx.Tree.Root.GetViewport().GetCamera3D();
            await Key(Godot.Key.G);
            bool docked = st.Docked && player.IsDocked && !player.VisualRoot.Visible;
            // Climbing in goes straight to first person
            bool firstPerson = st.GunnerView && ctx.Tree.Root.GetViewport().GetCamera3D() == st.GunnerCamera;
            float php = player.CurrentHP;
            player.TakeDamage(20f);
            bool safe = Mathf.IsEqualApprox(player.CurrentHP, php);
            var target = new VineEnemy();
            ctx.Tree.CurrentScene.AddChild(target);
            var tp = h.GlobalPosition + new Vector3(8f, 0, 3f);
            target.Initialize("Cannon target", VineEnemyFaction.Brute, 5000f, 0f, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(tp));
            target.GlobalPosition = new Vector3(tp.X, grid.GetWorldHeight(tp.X, tp.Z), tp.Z);
            await ctx.Wait(0.1f);
            st.TestAimPoint = target.GlobalPosition;
            st.FireHeld = true;
            await ctx.Wait(1.0f);
            st.FireHeld = false;
            st.TestAimPoint = null;
            float lost = 5000f - target.CurrentHealth;
            ctx.Assert(docked, "spire/g_climbs_in", $"station {st.Docked}, BIT docked {player.IsDocked}, visible {player.VisualRoot.Visible}");
            ctx.Assert(safe, "spire/safe_inside");
            ctx.Assert(st.ShotsFired >= 2 && lost >= st.Data.Dock.Damage, "spire/cannon_hits_aim",
                $"{st.ShotsFired} shots, target lost {lost:F0}");

            // ── Gunner view: V while docked ──
            ctx.StartTest();
            ctx.Assert(firstPerson, "spire/climb_in_is_first_person", $"gunner view {firstPerson}");
            var gcam = st.GunnerCamera;
            bool fp = st.GunnerView && gcam != null && ctx.Tree.Root.GetViewport().GetCamera3D() == gcam
                && gcam.GlobalPosition.Y > h.GlobalPosition.Y + h.ModelTop;
            ctx.Assert(fp, "spire/gunner_view_opens", gcam == null ? "no gunner camera" : $"camera at {gcam.GlobalPosition.Y - h.GlobalPosition.Y:F1} above the Spire base (top {h.ModelTop:F1})");
            if (gcam != null)
            {
                // Look at the target: the crosshair lands on it and the cannon hits it
                // (the camera sits out past the Spire's top on the side it faces, so settle it twice)
                for (int pass = 0; pass < 3; pass++)
                {
                    var to = target.GlobalPosition + Vector3.Up * 0.3f - gcam.GlobalPosition;
                    st.GunnerYaw = Mathf.Atan2(-to.X, -to.Z);
                    st.GunnerPitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
                    await ctx.Wait(0.1f);
                }
                var aim = st.GunnerAim();
                float miss = aim.HasValue ? new Vector2(aim.Value.X - target.GlobalPosition.X, aim.Value.Z - target.GlobalPosition.Z).Length() : 99f;
                float tgt0 = target.CurrentHealth;
                st.FireHeld = true;
                await ctx.Wait(1.0f);
                st.FireHeld = false;
                ctx.Assert(miss < 1.2f && target.CurrentHealth < tgt0, "spire/gunner_view_hits_crosshair",
                    $"crosshair {miss:F2} from the target, target {tgt0:F0} -> {target.CurrentHealth:F0}");

                // The mouse turns the view
                float yaw0 = st.GunnerYaw, pitch0 = st.GunnerPitch;
                Input.ParseInputEvent(new InputEventMouseMotion { Relative = new Vector2(80, 30) });
                await ctx.Wait(0.1f);
                ctx.Assert(!Mathf.IsEqualApprox(yaw0, st.GunnerYaw) && st.GunnerPitch < pitch0, "spire/gunner_view_mouse_look",
                    $"yaw {yaw0:F2} -> {st.GunnerYaw:F2}, pitch {pitch0:F2} -> {st.GunnerPitch:F2}");

                await Key(Godot.Key.V);
                ctx.Assert(!st.GunnerView && ctx.Tree.Root.GetViewport().GetCamera3D() == mapCam && st.Docked,
                    "spire/gunner_view_v_back_to_map", $"gunner {st.GunnerView}, docked {st.Docked}");

                // Pausing gives the mouse back
                await Key(Godot.Key.V);
                ctx.Tree.Paused = true;
                await ctx.Wait(0.1f);
                bool leftOnPause = !st.GunnerView;
                ctx.Tree.Paused = false;
                await ctx.Wait(0.1f);
                ctx.Assert(leftOnPause && ctx.Tree.Root.GetViewport().GetCamera3D() == mapCam, "spire/gunner_view_leaves_on_pause");

                // Climbing out from gunner view puts the map camera back
                await Key(Godot.Key.V);
            }
            await Key(Godot.Key.G);
            ctx.Assert(!st.GunnerView && ctx.Tree.Root.GetViewport().GetCamera3D() == mapCam, "spire/climb_out_leaves_gunner_view");
            var out_ = player.GlobalPosition - h.GlobalPosition;
            out_.Y = 0;
            ctx.Assert(!st.Docked && !player.IsDocked && player.VisualRoot.Visible && out_.Length() > 1f && st.InReach,
                "spire/g_climbs_out", $"docked {st.Docked}, {out_.Length():F1} from the Spire");
            if (GodotObject.IsInstanceValid(target)) target.QueueFree();

            // ── Materials mode banks drops ──
            ctx.StartTest();
            h.SelectMaterialType(MaterialType.Chaos);
            if (h.CurrentMode != MiningMode.Materials) h.ToggleMode();
            float bankC = st.MaterialsBanked;
            int resC = gm.CurrentResources;
            GameEvents.OnResourcesDropped?.Invoke(h.GlobalPosition, 100);
            float banked = st.MaterialsBanked - bankC;
            int gained = gm.CurrentResources - resC;
            if (h.CurrentMode == MiningMode.Materials) h.ToggleMode();
            ctx.Assert(banked > 0 && Mathf.Abs(banked - (banked + gained) * st.Data.MaterialsMode.DropShare) <= 1f,
                "spire/materials_mode_banks_drops", $"banked {banked:F0}, Resources {gained}");

            // ── A perk pick arriving with the Spire menu open: the menu closes, the pick is on top ──
            ctx.StartTest();
            bool autoWas = gm.AutoResolvePerks;
            gm.AutoResolvePerks = false;
            player.GlobalPosition = Near();
            await ctx.Wait(0.2f);
            st.OpenPanel();
            await ctx.Wait(0.2f);
            bool menuWasOpen = st.PanelOpen;
            GameEvents.OnWaveMilestone?.Invoke(5, "perk_select");
            await ctx.Wait(0.4f);
            var pick = All<VinePerkScreen>(ctx.Tree.CurrentScene).FirstOrDefault();
            var panelLayer = All<SpirePanel>(ctx.Tree.CurrentScene).FirstOrDefault()?.Layer ?? 0;
            ctx.Assert(menuWasOpen && !st.PanelOpen && pick != null && pick.Layer > panelLayer,
                "spire/perk_pick_over_spire_menu", $"menu open before {menuWasOpen}, after {st.PanelOpen}, pick {(pick == null ? "missing" : $"layer {pick.Layer} vs menu {panelLayer}")}");
            if (pick != null)
            {
                Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.Key1, PhysicalKeycode = Godot.Key.Key1, Pressed = true });
                await ctx.Wait(0.05f);
                Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.Key1, PhysicalKeycode = Godot.Key.Key1, Pressed = false });
                await ctx.Wait(0.4f);
            }
            ctx.Assert(!VinePerkScreen.IsOverlayOpen && !ctx.Tree.Paused, "spire/perk_pick_then_play_on", $"overlay {VinePerkScreen.IsOverlayOpen}, paused {ctx.Tree.Paused}");
            gm.AutoResolvePerks = autoWas;

            if (waves != null) waves.PauseAutoStart = false;
        }

        private static System.Collections.Generic.IEnumerable<T> All<T>(Node root) where T : class
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            foreach (var child in root.GetChildren())
                foreach (var x in All<T>(child)) yield return x;
        }
    }
}
