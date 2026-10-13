using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Relics (Data/relics.json), in memory, never on disk. The file: nine relics, three of each
    /// kind, each with a description, a trade-off and its numbers, all of rarities crates drop.
    /// Slots: one per kind, equipping replaces that kind's relic, other kinds stay on. Then each
    /// relic in a real battle (P1, Bruteforge): Cryo Fists (BIT punches in reach, chills, freezes in
    /// ice, a frozen death shatters into its neighbours, BIT takes less), Storm Lance (a beam that
    /// jumps, drains Materials, no jumps when dry), Drone Bay (drones fly out and shoot, and go when
    /// it comes off), each swap taking the old gear off; Molten Rounds (turrets set enemies burning,
    /// fire spreads on death, the turret wears its parts, Tesla weaker), Living Walls (double cost,
    /// thorns cut and slow, the wall wears them, the old tower relic's parts come off), Gravity Rams
    /// (enemies dragged in and crushed, taking more); Overcharged Dome (a bigger dome, faster inside,
    /// slower outside), Siphon Core (half the Spire, deaths in the dome heal it), Orbital Uplink (the
    /// Spire's guns quiet, a lance on the knot when charged). Last, a relic from a loot crate equipped
    /// from the reveal.
    /// </summary>
    public class RelicTestSuite : ITestSuite
    {
        private readonly bool _shots;
        public RelicTestSuite(bool shots = false) { _shots = shots; }
        public string SuiteName => _shots ? "relics-sheets" : "relics";
        private Camera3D _cam;

        /// <summary>relics-sheets: a picture of <paramref name="focus"/> from <paramref name="dist"/> away (null focus: the screen as it is).</summary>
        private async Task Shot(TestContext ctx, string name, Vector3? focus, float dist = 8f, float pitch = 0.75f)
        {
            if (!_shots) return;
            var tree = ctx.Tree;
            if (focus != null)
            {
                if (_cam == null || !GodotObject.IsInstanceValid(_cam)) { _cam = new Camera3D { Fov = 50f }; tree.CurrentScene.AddChild(_cam); }
                _cam.Current = true;
                _cam.GlobalPosition = focus.Value + new Vector3(0.35f, Mathf.Sin(pitch), Mathf.Cos(pitch)).Normalized() * dist;
                _cam.LookAt(focus.Value, Vector3.Up);
            }
            for (int i = 0; i < 2; i++) await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            string dir = ProjectSettings.GlobalizePath("res://test-reports/relics");
            System.IO.Directory.CreateDirectory(dir);
            tree.Root.GetTexture().GetImage().SavePng($"{dir}/{name}.png");
            if (focus != null && ServiceLocator.TryGet<TDCamera>(out var td) && GodotObject.IsInstanceValid(td)) td.Current = true;
        }

        private readonly List<VineEnemy> _spawned = new();

        public async Task Run(TestContext ctx)
        {
            if (_shots && DisplayServer.GetName() == "headless")
            {
                ctx.StartTest();
                ctx.Assert(false, "relics-sheets/needs_display", "Run with a display (Xvfb)");
                return;
            }
            var gm = GameManager.Instance;
            var oldSave = gm.MetaSave;
            bool autoFire = GameSettings.BitAutoFire;
            RelicInventory.UseInMemoryForTests();
            SuitManager.UseInMemoryForTests();
            MetaPerkSave.SuppressWrites = true;
            gm.MetaSave = new MetaPerkSaveData();
            gm.SuppressAutoDebrief = true;
            try
            {
                FileChecks(ctx);
                Slots(ctx);
                RelicInventory.UseInMemoryForTests();
                RelicRun.Clear();
                if (!await StartRun(ctx)) return;
                GameSettings.BitAutoFire = true;
                await CryoFists(ctx);
                await StormLance(ctx);
                await DroneBay(ctx);
                GameSettings.BitAutoFire = false;
                await MoltenRounds(ctx);
                await LivingWalls(ctx);
                await GravityRams(ctx);
                await OverchargedDome(ctx);
                await SiphonCore(ctx);
                await OrbitalUplink(ctx);
                await FromACrate(ctx);
                await Screen(ctx);
            }
            finally
            {
                foreach (var e in _spawned) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
                _spawned.Clear();
                GameSettings.BitAutoFire = autoFire;
                gm.MetaSave = oldSave;
                gm.SuppressAutoDebrief = false;
                gm.AutoResolvePerks = false;
                MetaPerkSave.SuppressWrites = false;
                RelicInventory.RestoreFromDisk();
                SuitManager.RestoreFromDisk();
                RelicRun.Clear();
                Engine.TimeScale = 1.0;
                if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = false;
            }
        }

        // ── The file ──

        private static void FileChecks(TestContext ctx)
        {
            ctx.StartTest();
            RelicRegistry.Reload();
            var all = RelicRegistry.All;
            ctx.Assert(all.Length == 9 && RelicRegistry.Kinds.All(k => all.Count(r => r.Kind == k) == 3), "relics/nine_three_of_each_kind",
                string.Join(", ", all.Select(r => $"{r.Id}:{r.Kind}")));
            ctx.Assert(all.Select(r => r.Id).Distinct().Count() == all.Length && all.Select(r => r.Name).Distinct().Count() == all.Length, "relics/unique");
            var bad = all.Where(r => string.IsNullOrEmpty(r.Name) || r.Desc.Length < 40 || string.IsNullOrEmpty(r.Tradeoff) || string.IsNullOrEmpty(r.Icon)
                || r.Tuning == null || r.Tuning.Count == 0 || r.Rarity is not ("rare" or "legendary")).Select(r => r.Id).ToList();
            ctx.Assert(bad.Count == 0, "relics/each_says_what_it_does_and_costs", string.Join(", ", bad));
            // Every relic can come out of a crate
            var dropRarities = LootTable.Tiers.SelectMany(t => t.Rewards.Where(l => l.Kind == "relic").SelectMany(l => l.Rarities)).Distinct().ToList();
            ctx.Assert(all.All(r => dropRarities.Contains(r.Rarity)), "relics/every_relic_drops", string.Join(",", dropRarities));
            ctx.Assert(RelicRegistry.Get("null-shard") == null && RelicRegistry.Get("void-beacon") == null, "relics/old_stat_relics_gone");
        }

        private static void Slots(TestContext ctx)
        {
            ctx.StartTest();
            RelicInventory.UseInMemoryForTests();
            foreach (var r in RelicRegistry.All) RelicInventory.ForceOwn(r.Id);
            bool a = RelicInventory.Equip(RelicRun.CryoFists);
            bool b = RelicInventory.Equip(RelicRun.MoltenRounds);
            bool c = RelicInventory.Equip(RelicRun.StormLance); // same kind as Cryo Fists: replaces it
            ctx.Assert(a && b && c && RelicInventory.EquippedOfKind("bit") == RelicRun.StormLance && RelicInventory.EquippedOfKind("towers") == RelicRun.MoltenRounds
                && !RelicInventory.IsEquipped(RelicRun.CryoFists) && RelicInventory.EquippedCount == 2, "relics/one_slot_per_kind",
                string.Join(", ", RelicInventory.Equipped));
            RelicInventory.Equip(RelicRun.SiphonCore);
            ctx.Assert(RelicInventory.EquippedCount == 3 && RelicInventory.MaxEquipSlots == 3, "relics/three_slots");
            ctx.Assert(!RelicInventory.Equip(RelicRun.SiphonCore) && RelicInventory.Unequip(RelicRun.SiphonCore) && RelicInventory.EquippedOfKind("spire") == null,
                "relics/unequip_empties_the_slot");
            RelicInventory.UseInMemoryForTests();
            ctx.Assert(!RelicInventory.Equip(RelicRun.CryoFists), "relics/cant_equip_unowned");
        }

        // ── BIT's weapon ──

        private async Task CryoFists(TestContext ctx)
        {
            ctx.StartTest();
            var bit = ServiceLocator.Get<VinePlayer>();
            bool on = EquipNow(RelicRun.CryoFists);
            await ctx.Wait(0.1f);
            var w = bit.RelicWeapon as CryoFists;
            ctx.Assert(on && w != null && RelicRun.Bit == RelicRun.CryoFists && bit.Look.HasGear("relic:cryo_fists") && bit.Look.WeaponsHidden,
                "relics/cryo_fists_on", $"weapon {bit.RelicWeapon?.Id}, gear {bit.Look?.HasGear("relic:cryo_fists")}");
            if (w == null) return;
            var a = Spawn(bit.GlobalPosition + new Vector3(1.7f, 0, 0.4f));
            await ctx.Wait(0.2f);
            float hp0 = a.CurrentHealth;
            bool frozen = await ctx.WaitUntil(() => a.IsFrozen, 6f);
            await Shot(ctx, "cryo_gear", bit.GlobalPosition + Vector3.Up * 0.8f, 3.2f, 0.35f);
            await Shot(ctx, "cryo_frozen", (bit.GlobalPosition + a.GlobalPosition) * 0.5f + Vector3.Up * 0.6f, 6f, 0.5f);
            ctx.Assert(w.Attacks >= 3 && a.CurrentHealth < hp0 && frozen && a.IceBlock != null, "relics/cryo_punches_chill_and_freeze",
                $"{w.Attacks} punches, -{hp0 - a.CurrentHealth:F0}, frozen {frozen}, ice {a.IceBlock != null}");
            // A frozen enemy that dies shatters into what stands next to it
            var b2 = Spawn(a.GlobalPosition + new Vector3(0, 0, 1.3f));
            await ctx.Wait(0.05f);
            int shatters = VineEnemy.ShatterCount;
            float bHp = b2.CurrentHealth;
            if (!a.IsFrozen) await ctx.WaitUntil(() => a.IsFrozen, 6f);
            a.TakeDamage(1e9f, DamageKind.Heavy);
            await Shot(ctx, "cryo_shatter", a.GlobalPosition + Vector3.Up * 0.6f, 7f, 0.5f);
            await ctx.Wait(0.05f);
            ctx.Assert(VineEnemy.ShatterCount == shatters + 1 && b2.CurrentHealth < bHp, "relics/cryo_frozen_death_shatters",
                $"shatters +{VineEnemy.ShatterCount - shatters}, neighbour -{bHp - b2.CurrentHealth:F0}");
            float before = bit.CurrentHP;
            bit.TakeDamage(10f);
            float lost = before - bit.CurrentHP;
            bit.Heal(100f);
            ctx.Assert(Mathf.Abs(lost - 10f * RelicRun.T(RelicRun.CryoFists, "damageTaken", 0.8f)) < 0.01f && w.MoveMult > 1f, "relics/cryo_takes_less_moves_faster",
                $"lost {lost:F1} of 10, move x{w.MoveMult:F2}");
            Clear();
        }

        private async Task StormLance(TestContext ctx)
        {
            ctx.StartTest();
            var bit = ServiceLocator.Get<VinePlayer>();
            EquipNow(RelicRun.StormLance);
            await ctx.Wait(0.1f);
            var w = bit.RelicWeapon as StormLance;
            ctx.Assert(w != null && !bit.Look.HasGear("relic:cryo_fists") && bit.Look.HasGear("relic:storm_lance"), "relics/swap_takes_old_gear_off",
                $"weapon {bit.RelicWeapon?.Id}, cryo gear {bit.Look?.HasGear("relic:cryo_fists")}");
            if (w == null) return;
            bit.TestRefill();
            var pack = Enumerable.Range(0, 4).Select(i => Spawn(bit.GlobalPosition + new Vector3(3f + i * 1.3f, 0, (i % 2) * 1f))).ToList();
            await ctx.Wait(0.1f);
            var hp = pack.Select(e => e.CurrentHealth).ToList();
            float mats = bit.CurrentMaterials;
            await ctx.Wait(0.8f);
            var fx = VfxParticles.Get(ctx.Tree);
            int bolts = 0;
            for (int i = 0; i < 6; i++) { bolts = Mathf.Max(bolts, fx?.CountOf(VfxParticles.Kind.Bolt) ?? 0); await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame); }
            ctx.Assert(w.Firing && bolts >= 6, "relics/lance_draws_a_beam", $"firing {w.Firing}, {bolts} bolt pieces, dropped {fx?.Dropped}");
            await Shot(ctx, "lance_gear", bit.GlobalPosition + Vector3.Up * 0.8f, 3.4f, 0.35f);
            await Shot(ctx, "lance_beam", bit.GlobalPosition + new Vector3(4f, 0.6f, 0.5f), 9f, 0.6f);
            int hurt = pack.Where((e, i) => e.CurrentHealth < hp[i]).Count();
            ctx.Assert(w.Attacks > 0 && w.LastJumps >= 2 && hurt >= 3 && bit.CurrentMaterials < mats, "relics/lance_jumps_and_drains",
                $"{w.Attacks} ticks, {w.LastJumps} jumps, {hurt}/4 hurt, Materials {mats:F0} -> {bit.CurrentMaterials:F0}");
            bit.TestDrain();
            await ctx.Wait(0.5f);
            ctx.Assert(w.Firing && w.LastJumps == 0, "relics/lance_dry_no_jumps", $"firing {w.Firing}, jumps {w.LastJumps}");
            bit.TestRefill();

            // Held fire reaches as far as a BIT behind its towers needs (it stopped at 9 and did
            // nothing in a real run), and with nothing in reach it crackles out so it shows
            ctx.StartTest();
            Clear();
            await ctx.Wait(0.2f);
            var farOne = Spawn(bit.GlobalPosition + new Vector3(18f, 0, 0.4f));
            await ctx.Wait(0.1f);
            float farHp = farOne.CurrentHealth;
            var mid = ctx.Tree.Root.GetVisibleRect().Size * 0.5f;
            bit.TestAimPoint = farOne.GlobalPosition;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = mid, GlobalPosition = mid });
            await ctx.Wait(0.6f);
            bool reached = farOne.CurrentHealth < farHp;
            int fizz0 = w.Fizzles;
            bit.TestAimPoint = bit.GlobalPosition + new Vector3(0, 0, -12f);
            await ctx.Wait(0.5f);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = mid, GlobalPosition = mid });
            bit.TestAimPoint = null;
            ctx.Assert(reached && w.Fizzles > fizz0, "relics/lance_aimed_reach",
                $"enemy 18 away hurt {reached} (aim reach {w.AimReach:F0}), fizzles {w.Fizzles - fizz0}");
            bit.TestRefill();
            bit.MaterialsRegen = SignalTuningEditor.PlayerMaterialsRegen;
            Clear();
        }

        private async Task DroneBay(TestContext ctx)
        {
            ctx.StartTest();
            var bit = ServiceLocator.Get<VinePlayer>();
            EquipNow(RelicRun.DroneBay);
            await ctx.Wait(0.3f);
            var w = bit.RelicWeapon as DroneBay;
            ctx.Assert(w != null && w.Drones.Count == w.Wanted && w.Wanted >= 3 && bit.Look.HasGear("relic:drone_bay"), "relics/drone_bay_launches",
                $"{w?.Drones.Count} drones of {w?.Wanted}");
            if (w == null) return;
            var e = Spawn(bit.GlobalPosition + new Vector3(7f, 0, 2f));
            await ctx.Wait(0.1f);
            float hp = e.CurrentHealth;
            int aimed = bit.AimedShots;
            await Shot(ctx, "drones_idle", bit.GlobalPosition + Vector3.Up * 1.2f, 5f, 0.45f);
            bool hit = await ctx.WaitUntil(() => e.CurrentHealth < hp, 4f);
            await ctx.Wait(0.4f);
            await Shot(ctx, "drones_attack", (bit.GlobalPosition + e.GlobalPosition) * 0.5f + Vector3.Up, 10f, 0.6f);
            float near = w.Drones.Min(d => d.GlobalPosition.DistanceTo(e.GlobalPosition));
            ctx.Assert(hit && w.Attacks > 0 && near < 5f && bit.AimedShots == aimed, "relics/drones_fly_out_and_shoot",
                $"hit {hit}, {w.Attacks} shots, nearest drone {near:F1} away");
            RelicInventory.Unequip(RelicRun.DroneBay);
            await ctx.Wait(0.2f);
            ctx.Assert(bit.RelicWeapon == null && ctx.Tree.GetNodesInGroup("BitDrone").Count == 0 && !bit.Look.HasGear("relic:drone_bay") && !bit.Look.WeaponsHidden,
                "relics/unequip_gives_the_gun_back", $"weapon {bit.RelicWeapon?.Id}, drones {ctx.Tree.GetNodesInGroup("BitDrone").Count}");
            Clear();
        }

        // ── Towers ──

        private VineNode _turret;

        private async Task MoltenRounds(TestContext ctx)
        {
            ctx.StartTest();
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var gm = GameManager.Instance;
            gm.AddResources(5000);
            var cell = FieldCell(grid, 0);
            _turret = placer.TestPlaceAt(VineNodeType.DamageTower, cell);
            EquipNow(RelicRun.MoltenRounds);
            await ctx.Wait(0.2f);
            ctx.Assert(_turret != null && _turret.Look != null && _turret.Look.PerksShown.Contains(RelicRun.MoltenRounds), "relics/molten_turret_wears_it",
                _turret?.Look == null ? "no turret" : string.Join(",", _turret.Look.PerksShown));
            if (_turret == null) return;
            var e = Spawn(_turret.GlobalPosition + new Vector3(2.5f, 0, 0));
            bool burning = await ctx.WaitUntil(() => e.IsBurning, 5f);
            await Shot(ctx, "molten", (_turret.GlobalPosition + e.GlobalPosition) * 0.5f + Vector3.Up * 0.6f, 5.5f, 0.5f);
            ctx.Assert(burning, "relics/molten_sets_enemies_burning");
            var next = Spawn(e.GlobalPosition + new Vector3(0, 0, 1.2f));
            await ctx.Wait(0.05f);
            VineEnemy.HitSource = "your towers";
            try { if (!e.IsBurning) await ctx.WaitUntil(() => e.IsBurning, 3f); e.TakeDamage(1e9f, DamageKind.Heavy); }
            finally { VineEnemy.HitSource = null; }
            await ctx.Wait(0.05f);
            ctx.Assert(next.IsBurning, "relics/molten_fire_spreads_on_death");
            ctx.Assert(Mathf.IsEqualApprox(RelicRun.TowerDamageMult(VineNodeType.TeslaCoil), 0.75f) && Mathf.IsEqualApprox(RelicRun.TowerDamageMult(VineNodeType.DamageTower), 1f),
                "relics/molten_tesla_and_flak_weaker");
            Clear();
        }

        private async Task LivingWalls(TestContext ctx)
        {
            ctx.StartTest();
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var wallData = VineNodeRegistry.Get(VineNodeType.BarrierWall);
            int plain = MetaRun.PlaceCost(wallData);
            EquipNow(RelicRun.LivingWalls);
            await ctx.Wait(0.2f);
            ctx.Assert(MetaRun.PlaceCost(wallData) == plain * 2 && _turret != null && !_turret.Look.PerksShown.Contains(RelicRun.MoltenRounds),
                "relics/living_walls_cost_double_and_molten_comes_off", $"wall {plain} -> {MetaRun.PlaceCost(wallData)}, turret {string.Join(",", _turret?.Look?.PerksShown ?? new List<string>())}");
            var wall = placer.TestPlaceAt(VineNodeType.BarrierWall, FieldCell(grid, 1));
            await ctx.Wait(0.1f);
            if (wall == null) { ctx.Assert(false, "relics/living_walls_wall_placed"); return; }
            var e = Spawn(wall.GlobalPosition + new Vector3(1.6f, 0, 0));
            await ctx.Wait(0.1f);
            float hp = e.CurrentHealth;
            await ctx.Wait(1.2f);
            await Shot(ctx, "living_walls", wall.GlobalPosition + new Vector3(0.8f, 0.6f, 0), 5f, 0.55f);
            ctx.Assert(e.CurrentHealth < hp && wall.ThornHits >= 1 && wall.Look.PerksShown.Contains(RelicRun.LivingWalls), "relics/thorns_cut",
                $"-{hp - e.CurrentHealth:F1}, thorn hits {wall.ThornHits}");
            Clear();
        }

        private async Task GravityRams(TestContext ctx)
        {
            ctx.StartTest();
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            EquipNow(RelicRun.GravityRams);
            var ram = placer.TestPlaceAt(VineNodeType.PushPull, FieldCell(grid, 2));
            await ctx.Wait(0.1f);
            if (ram == null) { ctx.Assert(false, "relics/gravity_ram_placed"); return; }
            var e = Spawn(ram.GlobalPosition + new Vector3(3.2f, 0, 0.3f));
            float d0 = Flat(e.GlobalPosition - ram.GlobalPosition).Length();
            bool crushed = await ctx.WaitUntil(() => e.IsCrushed, 4f);
            float d1 = Flat(e.GlobalPosition - ram.GlobalPosition).Length();
            await Shot(ctx, "gravity_rams", ram.GlobalPosition + Vector3.Up * 0.6f, 5f, 0.5f);
            ctx.Assert(crushed && d1 < d0 - 0.4f && ram.Look.PerksShown.Contains(RelicRun.GravityRams), "relics/gravity_rams_pull_in",
                $"{d0:F2} -> {d1:F2}, crushed {crushed}");
            float hp = e.CurrentHealth;
            e.TakeDamage(100f, DamageKind.Heavy);
            ctx.Assert(Mathf.Abs(hp - e.CurrentHealth - 100f * RelicRun.T(RelicRun.GravityRams, "crushTaken", 1.3f)) < 1f, "relics/crushed_takes_more",
                $"took {hp - e.CurrentHealth:F1} from 100");
            Clear();
        }

        // ── The Spire ──

        private async Task OverchargedDome(TestContext ctx)
        {
            ctx.StartTest();
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var dome = ServiceLocator.Get<ConversionDome>();
            float r0 = dome.CurrentRadius;
            EquipNow(RelicRun.OverchargedDome);
            await ctx.Wait(0.2f);
            await Shot(ctx, "dome_overcharged", grid.Harvester.GlobalPosition, 46f, 1.0f);
            ctx.Assert(Mathf.IsEqualApprox(dome.RadiusMult, 1.5f) && dome.CurrentRadius > r0 * 1.45f, "relics/dome_reaches_further", $"{r0:F1} -> {dome.CurrentRadius:F1}");
            // A tower inside the dome and one well outside it
            var inside = placer.TestPlaceAt(VineNodeType.DamageTower, NearSpireCell(grid));
            await ctx.Wait(1.2f);
            ctx.Assert(inside != null && inside.InDome && Mathf.IsEqualApprox(inside.DomeBonus, Constants.DOME_RATE_BONUS + 0.25f), "relics/dome_inside_faster",
                $"in dome {inside?.InDome}, bonus {inside?.DomeBonus:F2}");
            ctx.Assert(_turret != null && !_turret.InDome && Mathf.IsEqualApprox(_turret.OutsideDomePenalty, -0.25f), "relics/dome_outside_slower",
                $"in dome {_turret?.InDome}, penalty {_turret?.OutsideDomePenalty:F2}");
        }

        private async Task SiphonCore(TestContext ctx)
        {
            ctx.StartTest();
            var grid = ServiceLocator.Get<VineGrid>();
            var spire = grid.Harvester;
            var dome = ServiceLocator.Get<ConversionDome>();
            float max0 = spire.MaxHP;
            EquipNow(RelicRun.SiphonCore);
            await ctx.Wait(0.2f);
            ctx.Assert(Mathf.Abs(spire.MaxHP - max0 * 0.5f) < max0 * 0.02f && Mathf.IsEqualApprox(dome.RadiusMult, 1f), "relics/siphon_halves_the_spire",
                $"{max0:F0} -> {spire.MaxHP:F0}, dome x{dome.RadiusMult:F2}");
            spire.TakeDamage(spire.MaxHP * 0.3f);
            float hp = spire.CurrentHP;
            var fx = ServiceLocator.Get<RelicEffects>();
            int siphons = fx.Siphons;
            var e = Spawn(spire.GlobalPosition + new Vector3(3.5f, 0, 0));
            await ctx.Wait(0.05f);
            e.TakeDamage(1e9f, DamageKind.Heavy);
            await Shot(ctx, "siphon", (spire.GlobalPosition + e.GlobalPosition) * 0.5f + Vector3.Up * 3f, 14f, 0.45f);
            await ctx.Wait(0.05f);
            ctx.Assert(fx.Siphons == siphons + 1 && spire.CurrentHP > hp + spire.MaxHP * 0.01f, "relics/siphon_heals_from_deaths_in_the_dome",
                $"{hp:F0} -> {spire.CurrentHP:F0}");
            spire.Heal(spire.MaxHP);
            Clear();
        }

        private async Task OrbitalUplink(TestContext ctx)
        {
            ctx.StartTest();
            var gm = GameManager.Instance;
            var grid = ServiceLocator.Get<VineGrid>();
            var spire = grid.Harvester;
            // Without it the Spire's guns (Bruteforge's autocannons) hit what comes close in a wave
            RelicInventory.Unequip(RelicRun.SiphonCore);
            await ctx.Wait(0.1f);
            var e = Spawn(spire.GlobalPosition + new Vector3(5f, 0, 1f));
            gm.SetPhase(GamePhase.Wave);
            float BySpire() => e.DamageBySource.GetValueOrDefault("the Spire");
            float s0 = BySpire();
            await ctx.Wait(1.5f);
            float plainHit = BySpire() - s0;
            EquipNow(RelicRun.OrbitalUplink);
            spire.TestUplinkCharge(0f);
            // Shells already in the air still land
            await ctx.Wait(1.2f);
            spire.TestUplinkCharge(0f);
            s0 = BySpire();
            await ctx.Wait(1.5f);
            float quiet = BySpire() - s0;
            ctx.Assert(plainHit > 0f && quiet < 0.01f, "relics/uplink_silences_the_guns", $"the Spire's guns did {plainHit:F0}, then {quiet:F0}");
            float hp;
            int strikes = spire.UplinkStrikes;
            spire.TestUplinkCharge(1f);
            hp = e.CurrentHealth;
            await ctx.Wait(0.15f);
            await Shot(ctx, "uplink", e.GlobalPosition + Vector3.Up, 14f, 0.6f);
            await ctx.Wait(0.15f);
            ctx.Assert(spire.UplinkStrikes == strikes + 1 && e.CurrentHealth < hp, "relics/uplink_lances_the_knot", $"strikes {spire.UplinkStrikes - strikes}, -{hp - e.CurrentHealth:F0}");
            gm.SetPhase(GamePhase.Build);
            RelicInventory.Unequip(RelicRun.OrbitalUplink);
            Clear();
        }

        // ── From a crate ──

        private async Task FromACrate(TestContext ctx)
        {
            ctx.StartTest();
            RelicInventory.UseInMemoryForTests();
            RelicRun.Begin();
            var loot = ServiceLocator.Get<LootManager>();
            var bit = ServiceLocator.Get<VinePlayer>();
            loot.GiveForTests(LootTable.Tier("legendary"));
            loot.OpenAll();
            var c = loot.Ceremony;
            if (c == null) { ctx.Assert(false, "relics/crate_opens"); return; }
            c.Manual = true;
            c.Skip();
            string relic = c.Current.Rewards.FirstOrDefault(r => r.Kind == "relic")?.RelicId;
            var kind = relic == null ? null : RelicRegistry.Get(relic)?.Kind;
            ctx.Assert(relic != null && c.EquipOffers >= 1, "relics/crate_offers_equip", $"relic {relic}, offers {c.EquipOffers}");
            for (int i = 0; i < 90 && !c.AllRevealed; i++) c.Step(1f / 30f);
            for (int i = 0; i < 20; i++) c.Step(1f / 30f);
            await Shot(ctx, "crate_equip_offer", null);
            bool ok = c.EquipFirst();
            for (int i = 0; i < 6; i++) c.Step(1f / 30f);
            await Shot(ctx, "crate_equipped", null);
            string inSlot = kind == null ? null : kind == "bit" ? RelicRun.Bit : kind == "towers" ? RelicRun.Towers : RelicRun.Spire;
            ctx.Assert(ok && inSlot == relic && RelicInventory.IsEquipped(relic) && c.Equipped == 1 && (kind != "bit" || bit.RelicWeapon?.Id == relic),
                "relics/equipped_from_the_reveal", $"{relic} in the {kind} slot: {inSlot}");
            c.Collect();
            for (int i = 0; i < 20 && loot.Ceremony == c; i++) c.Step(1f / 30f);
            await ctx.Wait(0.1f);
            ctx.Assert(!ctx.Tree.Paused && RelicInventory.IsEquipped(relic), "relics/still_on_after_collect");
        }

        // ── The Relics screen (the built-in one: the web page needs godot-cef) ──

        private async Task Screen(TestContext ctx)
        {
            ctx.StartTest();
            var gm = GameManager.Instance;
            gm.SetPhase(GamePhase.Defeat);
            RelicInventory.UseInMemoryForTests();
            RelicInventory.ForceOwn(RelicRun.CryoFists);
            RelicInventory.ForceOwn(RelicRun.DroneBay);
            RelicInventory.Equip(RelicRun.CryoFists);
            gm.ShowRelicInventory();
            await ctx.Wait(1.2f);
            var screen = ctx.FindNode<RelicInventoryScreen>();
            ctx.Assert(screen != null && RelicRegistry.Kinds.All(k => screen.FallbackSlot(k) != null), "relics/screen_has_three_slots", screen == null ? "no screen" : "");
            if (screen == null) return;
            string SlotText(string k) => string.Join(" ", screen.FallbackSlot(k).FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text));
            ctx.Assert(SlotText("bit").Contains("CRYO FISTS") && SlotText("towers").Contains("EMPTY"), "relics/screen_slots_show_whats_on",
                $"{SlotText("bit")} | {SlotText("towers")}");
            var locked = screen.FallbackRelic(RelicRun.StormLance);
            ctx.Assert(locked != null && locked.Text.StartsWith("???") && screen.FallbackRelic(RelicRun.DroneBay)?.Text.StartsWith("Drone Bay") == true,
                "relics/screen_hides_relics_not_found", locked?.Text.Replace("\n", " ") ?? "none");
            screen.FallbackRelic(RelicRun.DroneBay)?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.1f);
            var eq = screen.FallbackEquip;
            ctx.Assert(eq != null && eq.Visible && eq.Text == "EQUIP", "relics/screen_offers_equip", eq?.Text ?? "none");
            eq?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.2f);
            screen = ctx.FindNode<RelicInventoryScreen>();
            await Shot(ctx, "relics_screen", null);
            ctx.Assert(RelicInventory.EquippedOfKind("bit") == RelicRun.DroneBay && screen != null && SlotText("bit").Contains("DRONE BAY"),
                "relics/screen_equip_swaps_the_slot", screen == null ? "" : SlotText("bit"));
        }

        // ── Helpers ──

        private static bool EquipNow(string id)
        {
            RelicInventory.ForceOwn(id);
            return ServiceLocator.TryGet<RelicManager>(out var rm) && rm.EquipNow(id);
        }

        private VineEnemy Spawn(Vector3 at)
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            var grid = ServiceLocator.Get<VineGrid>();
            var e = new VineEnemy();
            tree.CurrentScene.AddChild(e);
            e.Initialize("Relic test", VineEnemyFaction.Brute, 1e5f, 0f, 0, PlanetTheme.Current.EnemyScavenger, grid.WorldToGrid(at));
            e.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
            e.SkipMarch();
            _spawned.Add(e);
            return e;
        }

        private void Clear()
        {
            foreach (var e in _spawned) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            _spawned.Clear();
        }

        private static Vector3 Flat(Vector3 v) { v.Y = 0; return v; }

        /// <summary>Open cells far from BIT and the Spire, well apart.</summary>
        private static Vector2I FieldCell(VineGrid grid, int index)
        {
            var bit = ServiceLocator.Get<VinePlayer>().GlobalPosition;
            var spire = grid.Harvester.GlobalPosition;
            var pf = ServiceLocator.Get<VinePathfinder>();
            var found = new List<Vector2I>();
            for (int y = 3; y < grid.Height - 3; y += 3)
                for (int x = 3; x < grid.Width - 3; x += 3)
                {
                    var c = new Vector2I(x, y);
                    var w = grid.GridToWorld(c);
                    if (Flat(w - bit).Length() < 16f || Flat(w - spire).Length() < 26f) continue;
                    bool open = true;
                    for (int dx = -2; dx <= 2 && open; dx++)
                        for (int dy = -2; dy <= 2 && open; dy++)
                        {
                            var n = c + new Vector2I(dx, dy);
                            open = grid.InBounds(n) && grid.IsWalkable(n) && grid.GetNode(n) == null;
                        }
                    if (!open || !grid.CanPlace(c) || pf.WouldBlockAllPaths(c) || found.Any(o => Flat(grid.GridToWorld(o) - w).Length() < 12f)) continue;
                    found.Add(c);
                    if (found.Count > index) return found[index];
                }
            return found.Count > 0 ? found[^1] : new Vector2I(4, 4);
        }

        private static Vector2I NearSpireCell(VineGrid grid)
        {
            var spire = grid.WorldToGrid(grid.Harvester.GlobalPosition);
            var pf = ServiceLocator.Get<VinePathfinder>();
            for (int r = 3; r < 6; r++)
                for (int dx = -r; dx <= r; dx++)
                    foreach (int dy in new[] { -r, r })
                    {
                        var c = spire + new Vector2I(dx, dy);
                        if (grid.CanPlace(c) && !pf.WouldBlockAllPaths(c)) return c;
                    }
            return spire + new Vector2I(3, 3);
        }

        private static async Task<bool> StartRun(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "relics/battle_loads", "no site"); return false; }
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = 1;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = SpireData.Get("Bruteforge")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok && RelicRun.Bit == null && ServiceLocator.TryGet<RelicEffects>(out _), "relics/battle_loads", site.Id);
            if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = true;
            await ctx.Wait(0.5f);
            return ok;
        }
    }
}
