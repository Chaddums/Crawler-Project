using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Regression tests for run-flow and meta-loop fixes (review 2026-09):
    /// perk overlay keeps the run, enemy parenting/knockback, territory access and
    /// site clearing, relic equip persistence, suit snapshot/apply, crash-safe saves,
    /// procedural wave escalation and repeating milestones.
    /// Saves are sandboxed (SafeFile redirects user:// under --test-harness).
    /// </summary>
    public class RunFlowTestSuite : ITestSuite
    {
        public string SuiteName => "flow";

        public async Task Run(TestContext ctx)
        {
            GD.Print("[RunFlowTestSuite] Starting run-flow tests...");

            ctx.Assert(SafeFile.IsSandboxed, "flow/saves_sandboxed",
                "Test runs must redirect user:// saves to user://sandbox/");

            TestSafeFileRecovery(ctx);
            TestPerkPool(ctx);
            TestMilestoneRepeat(ctx);
            TestProceduralWaveEscalation(ctx);
            TestTerritoryAccess(ctx);
            TestRelicEquipPersists(ctx);
            TestMetaPerkPoints(ctx);
            await TestMetaPerkTreeScreen(ctx);

            await LoadBattle(ctx);
            await TestPerkOverlayKeepsRun(ctx);
            await TestEnemyParentAndKnockback(ctx);
            TestSiteSecured(ctx);
            TestSuitSnapshotRoundTrip(ctx);
            await TestWaveBonusPerk(ctx);

            GD.Print("[RunFlowTestSuite] Complete.");
        }

        // ── Pure / data tests ──

        private void TestSafeFileRecovery(TestContext ctx)
        {
            const string path = "user://flow_safefile_test.json";
            bool IsJson(string t) => new Json().Parse(t) == Error.Ok;

            ctx.Assert(SafeFile.WriteAllText(path, "{\"v\":1}"), "safefile/write_1");
            ctx.Assert(SafeFile.WriteAllText(path, "{\"v\":2}"), "safefile/write_2");
            ctx.AssertEqual("{\"v\":2}", SafeFile.ReadAllText(path, IsJson), "safefile/read_latest");

            // Corrupt the main file as a crash mid-write would — backup (v1) must be used
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("user://sandbox/flow_safefile_test.json"), "{\"v\":");
            ctx.AssertEqual("{\"v\":1}", SafeFile.ReadAllText(path, IsJson), "safefile/recovers_from_backup");
        }

        private void TestPerkPool(TestContext ctx)
        {
            var offered = VinePerkRegistry.GetAll().Where(p => p.Offered).ToList();
            ctx.Assert(offered.Count >= 3, "perks/enough_offered", $"Only {offered.Count} offerable perks");

            bool anyDead = false;
            for (int i = 0; i < 50; i++)
                if (VinePerkRegistry.PickRandom(3, null).Any(p => !p.Offered)) anyDead = true;
            ctx.Assert(!anyDead, "perks/never_offers_disabled", "PickRandom returned a perk with Offered=false");
        }

        private void TestMilestoneRepeat(TestContext ctx)
        {
            var ms = VineWaveLoader.LoadMilestones(1);
            ctx.Assert(ms.Any(m => m.Wave == 5 && m.Type == "perk_select"), "milestones/authored_w5");
            ctx.Assert(ms.Any(m => m.Wave == 25 && m.Type == "perk_select"), "milestones/repeat_w25",
                "repeatAfter/repeatInterval in milestones.json should add W25");

            // Scrapyard has no milestones of its own; it must still get perk picks
            var p2 = VineWaveLoader.LoadMilestones(2);
            ctx.Assert(p2.Any(m => m.Type == "perk_select"), "milestones/p2_has_perks",
                "Planet 2 loaded no milestones, so Scrapyard runs never offered a perk");
            ctx.Assert(ms.Any(m => m.MetaPoints > 0), "milestones/meta_points_authored",
                "Some milestone must pay meta perk points, or the Perk Tree can never be used");
        }

        /// <summary>Meta perk points: paid once per planet per milestone, saved immediately.</summary>
        private void TestMetaPerkPoints(TestContext ctx)
        {
            var gm = GameManager.Instance;
            if (gm == null) { ctx.Assert(false, "meta_points/gm"); return; }
            var oldSave = gm.MetaSave;
            int oldPlanet = gm.CurrentPlanet;
            try
            {
                gm.MetaSave = new MetaPerkSaveData();
                gm.MetaSave.AllocatedIds.Add(0);
                gm.CurrentPlanet = 1;
                gm.AwardMetaPoints(5, 1);
                ctx.AssertEqual(1, gm.MetaSave.AvailablePoints, "meta_points/awarded");
                gm.AwardMetaPoints(5, 1);
                ctx.AssertEqual(1, gm.MetaSave.AvailablePoints, "meta_points/once_per_milestone");
                gm.CurrentPlanet = 2;
                gm.AwardMetaPoints(5, 1);
                ctx.AssertEqual(2, gm.MetaSave.AvailablePoints, "meta_points/per_planet");
                ctx.AssertEqual(2, MetaPerkSave.Load().AvailablePoints, "meta_points/saved");
            }
            finally
            {
                gm.MetaSave = oldSave;
                gm.CurrentPlanet = oldPlanet;
                MetaPerkSave.Save(oldSave ?? new MetaPerkSaveData()); // don't leave the throwaway save on disk
            }
        }

        /// <summary>The tree screen allocates with a point, enforces one-per-tier, and refunds on reset.</summary>
        private async Task TestMetaPerkTreeScreen(TestContext ctx)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var oldSave = gm.MetaSave;
            gm.MetaSave = new MetaPerkSaveData { AvailablePoints = 2 };
            gm.MetaSave.AllocatedIds.Add(0);

            ctx.Tree.ChangeSceneToFile(Constants.SCENE_META_PERK);
            await ctx.Wait(0.5f);
            var screen = ctx.Tree.CurrentScene as MetaPerkTreeScreen;
            ctx.AssertNotNull(screen, "meta_tree/loads");
            if (screen == null) { gm.MetaSave = oldSave; MetaPerkSave.Save(oldSave ?? new MetaPerkSaveData()); return; }

            var buttons = screen.FindChildren("*", "Button", true, false).OfType<Button>().ToList();
            var open = buttons.Where(b => !b.Disabled && b.TooltipText == "Click to take this perk").ToList();
            ctx.AssertEqual(3, open.Count, "meta_tree/tier1_open", "Exactly the three tier 1 perks start open");

            open.FirstOrDefault()?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.1f);
            ctx.AssertEqual(1, gm.MetaSave.AvailablePoints, "meta_tree/spends_point");
            ctx.AssertEqual(2, gm.MetaSave.AllocatedIds.Count, "meta_tree/allocates");
            ctx.Assert(open.Skip(1).All(b => b.Disabled), "meta_tree/one_per_tier",
                "The other tier 1 perks lock once one is taken");

            var reset = buttons.FirstOrDefault(b => b.Text.StartsWith("RESET TREE"));
            reset?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.1f);
            ctx.AssertEqual(2, gm.MetaSave.AvailablePoints, "meta_tree/reset_refunds");
            ctx.AssertEqual(1, gm.MetaSave.AllocatedIds.Count, "meta_tree/reset_keeps_root");

            gm.MetaSave = oldSave;
            MetaPerkSave.Save(oldSave ?? new MetaPerkSaveData());
        }

        private void TestProceduralWaveEscalation(TestContext ctx)
        {
            var authored = VineWaveLoader.LoadPlanetWaves(1);
            if (authored == null || authored.Count == 0)
            {
                ctx.Assert(false, "waves/authored_loaded", "No P1 waves");
                return;
            }

            float TotalHp(VineWaveData w) => w.Surges.Sum(s => s.Health * s.Count);

            // Same seed → same template, so only the wave number differs
            var early = VineWaveLoader.GenerateWave(authored.Count + 1, authored, new RandomNumberGenerator { Seed = 42 });
            var late = VineWaveLoader.GenerateWave(authored.Count + 20, authored, new RandomNumberGenerator { Seed = 42 });
            ctx.AssertNotNull(early, "waves/procedural_generated");
            if (early == null || late == null) return;

            ctx.Assert(TotalHp(late) > TotalHp(early), "waves/procedural_escalates",
                $"W{authored.Count + 20} total HP {TotalHp(late):F0} should exceed W{authored.Count + 1} {TotalHp(early):F0}");

            // Never easier than the weakest of the last authored waves it templates from
            float minRecentAuthored = authored.Skip(System.Math.Max(0, authored.Count - 5))
                .Where(w => !w.IsBossWave).Select(TotalHp).DefaultIfEmpty(0).Min();
            ctx.Assert(TotalHp(early) >= minRecentAuthored * 0.99f, "waves/procedural_not_easier",
                $"W{authored.Count + 1} HP {TotalHp(early):F0} < recent authored min {minRecentAuthored:F0}");
        }

        private void TestTerritoryAccess(TestContext ctx)
        {
            TerritoryManager.Load();
            var planet = TerritoryManager.GetPlanet(1);
            if (planet == null || planet.Regions.Count < 2)
            {
                ctx.Assert(false, "territory/p1_loaded", "Planet 1 territory missing");
                return;
            }

            var save = new MetaPerkSaveData();
            var first = planet.Regions[0];
            var bossRegion = planet.Regions.FirstOrDefault(r => r.IsBossRegion);
            var bossSite = bossRegion?.Sites.FirstOrDefault(s => s.IsBossSite);

            ctx.Assert(TerritoryManager.IsSiteAccessible(first.Sites[0].Id, save), "territory/first_site_accessible");
            if (bossSite != null)
                ctx.Assert(!TerritoryManager.IsSiteAccessible(bossSite.Id, save), "territory/boss_locked_initially");

            ctx.Assert(first.Sites.All(s => s.ClearWave > 0), "territory/clear_wave_set",
                "Every farming site needs a clear_wave");

            // Clear every non-boss region → region conquered, buff active, boss reachable
            int reward = TerritoryManager.ClearSite(first.Sites[0].Id, save);
            ctx.Assert(reward > 0, "territory/clear_awards", $"reward={reward}");
            ctx.AssertEqual(0, TerritoryManager.ClearSite(first.Sites[0].Id, save), "territory/clear_once");

            foreach (var region in planet.Regions.Where(r => !r.IsBossRegion))
                foreach (var site in region.Sites)
                    TerritoryManager.ClearSite(site.Id, save);

            ctx.Assert(TerritoryManager.IsRegionConquered(first.Id, save), "territory/region_conquered");
            if (first.Buff != null)
                ctx.Assert(TerritoryManager.GetBuffMultiplier(1, first.Buff.Type, save) > 1f,
                    "territory/conquest_buff_active");
            if (bossSite != null)
                ctx.Assert(TerritoryManager.IsSiteAccessible(bossSite.Id, save), "territory/boss_accessible_after_regions",
                    "Boss site must be playable once prior regions are conquered (used to require it already cleared)");
        }

        private void TestRelicEquipPersists(TestContext ctx)
        {
            RelicInventory.RestoreFromDisk(); // sandboxed file
            string id = RelicRegistry.All[0].Id;
            RelicInventory.Acquire(id);
            ctx.Assert(RelicInventory.Equip(id), "relic_persist/equip");

            RelicInventory.RestoreFromDisk(); // simulate a new session / scene
            ctx.Assert(RelicInventory.OwnsRelic(id), "relic_persist/owned_after_reload");
            ctx.Assert(RelicInventory.IsEquipped(id), "relic_persist/equipped_after_reload",
                "Equipped relics must survive leaving the meta screen");
            RelicInventory.Unequip(id);
        }

        // ── Battle-scene tests ──

        private VineGrid _grid;
        private VineWaveManager _wm;

        private async Task LoadBattle(TestContext ctx)
        {
            var gm = GameManager.Instance;
            gm.CurrentTerritorySectionId = null;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            await ctx.WaitForPhase(GamePhase.Build, 10f);
            _grid = ServiceLocator.Get<VineGrid>();
            _wm = ServiceLocator.Get<VineWaveManager>();
        }

        /// <summary>The "Wave Processor" meta perk (+5 WaveBonus) used to have no effect.</summary>
        private Task TestWaveBonusPerk(TestContext ctx)
        {
            var waves = VineWaveLoader.LoadPlanetWaves(1);
            if (waves == null || waves.Count == 0) { ctx.Assert(false, "flow/wave_bonus_perk", "No P1 waves"); return Task.CompletedTask; }
            int saved = SignalTuningEditor.WaveBonus;
            try
            {
                SignalTuningEditor.WaveBonus = Constants.VINE_WAVE_BONUS;
                int authoredBase = VineWaveManager.ComputeWaveBonus(1, waves[0], waves.Count);
                int proceduralBase = VineWaveManager.ComputeWaveBonus(waves.Count + 3, null, waves.Count);
                SignalTuningEditor.WaveBonus = Constants.VINE_WAVE_BONUS + 5;
                int authoredPerk = VineWaveManager.ComputeWaveBonus(1, waves[0], waves.Count);
                int proceduralPerk = VineWaveManager.ComputeWaveBonus(waves.Count + 3, null, waves.Count);

                ctx.AssertEqual(waves[0].BonusResources, authoredBase, "flow/wave_bonus_base_unchanged",
                    "Without the perk an authored wave pays exactly its own BonusResources");
                ctx.AssertEqual(5, authoredPerk - authoredBase, "flow/wave_bonus_perk_authored");
                ctx.AssertEqual(5, proceduralPerk - proceduralBase, "flow/wave_bonus_perk_procedural");
            }
            finally
            {
                SignalTuningEditor.WaveBonus = saved;
            }
            return Task.CompletedTask;
        }

        private Vector2I FindBuildableCell(int startX = 3, int startY = 3)
        {
            var pf = ServiceLocator.Get<VinePathfinder>();
            for (int x = startX; x < _grid.Width - 2; x++)
                for (int y = startY; y < _grid.Height - 2; y++)
                {
                    var c = new Vector2I(x, y);
                    if (_grid.CanPlace(c) && !pf.WouldBlockAllPaths(c)) return c;
                }
            return new Vector2I(-1, -1);
        }

        private async Task TestPerkOverlayKeepsRun(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var cell = FindBuildableCell();
            var data = VineNodeRegistry.Get(VineNodeType.DamageTower);
            var node = new VineNode();
            node.Initialize(data);
            bool placed = _grid.PlaceNode(node, cell);
            ctx.Assert(placed, "perk_overlay/setup_place");

            int resourcesBefore = gm.CurrentResources;
            int waveBefore = _wm.CurrentWave;
            int perksBefore = gm.ActivePerks.Count;

            GameEvents.OnWaveMilestone?.Invoke(5, "perk_select");
            await ctx.Wait(0.2f);

            ctx.Assert(VinePerkScreen.IsOverlayOpen, "perk_overlay/opens");
            ctx.Assert(ctx.Tree.Paused, "perk_overlay/pauses_run");

            // Pick the first perk by pressing its button
            var overlay = ctx.Tree.CurrentScene.GetChildren().OfType<VinePerkScreen>().FirstOrDefault();
            var button = overlay?.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault();
            ctx.AssertNotNull(button, "perk_overlay/has_choice");
            button?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.3f);

            ctx.Assert(!VinePerkScreen.IsOverlayOpen, "perk_overlay/closes");
            ctx.Assert(!ctx.Tree.Paused, "perk_overlay/unpauses");
            ctx.AssertEqual(perksBefore + 1, gm.ActivePerks.Count, "perk_overlay/perk_applied");
            ctx.Assert(GodotObject.IsInstanceValid(node) && _grid.GetNode(cell) == node, "perk_overlay/tower_survives",
                "Towers must survive the perk pick (scene used to reload)");
            ctx.AssertEqual(waveBefore, _wm.CurrentWave, "perk_overlay/wave_counter_kept");
            ctx.Assert(gm.CurrentResources >= resourcesBefore - 1, "perk_overlay/resources_kept",
                $"before={resourcesBefore} after={gm.CurrentResources}");

            _grid.RemoveNode(cell);
        }

        private async Task TestEnemyParentAndKnockback(TestContext ctx)
        {
            _wm.RequestNextWave();
            bool spawned = await ctx.WaitUntil(
                () => ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).Count > 0, 15f);
            ctx.Assert(spawned, "enemies/spawned");
            var enemy = ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).OfType<VineEnemy>().FirstOrDefault();
            if (enemy == null) return;

            ctx.Assert(enemy.GetParent() != ctx.Tree.Root, "enemies/not_root_parented",
                "Enemies under the tree root outlived the battle scene");

            // Knockback into a wall must stop at the wall. Need three open cells in a row:
            // start (x-2), landing (x-1), and the cell that becomes the wall (x).
            var pf = ServiceLocator.Get<VinePathfinder>();
            var wallCell = new Vector2I(-1, -1);
            for (int y = 3; y < _grid.Height - 3 && wallCell.X < 0; y++)
                for (int x = 5; x < _grid.Width - 3 && wallCell.X < 0; x++)
                    // Open ground, not just placeable: an elevated cell takes a tower but no walker
                    if (_grid.GetCell(x - 2, y) == VineCellType.Empty && _grid.GetCell(x - 1, y) == VineCellType.Empty
                        && _grid.GetCell(x, y) == VineCellType.Empty && !pf.WouldBlockAllPaths(new Vector2I(x, y)))
                        wallCell = new Vector2I(x, y);
            ctx.Assert(wallCell.X >= 0, "knockback/setup_cells");
            if (wallCell.X < 0) return;
            _grid.MutateCell(wallCell, VineCellType.Wall);
            var start = _grid.GridToWorld(wallCell + new Vector2I(-2, 0));
            enemy.GlobalPosition = new Vector3(start.X, enemy.GlobalPosition.Y, start.Z);
            enemy.ApplyKnockback(new Vector3(Constants.VINE_CELL_SIZE * 4f, 0, 0));
            var landed = _grid.WorldToGrid(enemy.GlobalPosition);
            ctx.Assert(landed.X < wallCell.X, "knockback/stops_before_wall",
                $"Enemy pushed to {landed} through wall at {wallCell}");
            ctx.Assert(enemy.GlobalPosition.X > start.X, "knockback/moves_enemy",
                $"{enemy.Faction} (body {enemy.BodyRadius:F2}) stayed at x={enemy.GlobalPosition.X:F2} from {start.X:F2}, wall cell {wallCell}, start cell {_grid.GetCell(wallCell + new Vector2I(-2, 0))}, rows beside it {_grid.GetCell(wallCell + new Vector2I(-2, -1))}/{_grid.GetCell(wallCell + new Vector2I(-2, 1))}");
            _grid.MutateCell(wallCell, VineCellType.Empty);
        }

        private void TestSiteSecured(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = TerritoryManager.GetPlanet(1)?.Regions[0].Sites[0];
            if (site == null) return;

            gm.CurrentTerritorySectionId = site.Id;
            gm.CheckSiteSecured(site.ClearWave - 1);
            ctx.Assert(!gm.SiteSecuredThisRun, "site_secured/not_before_clear_wave");
            gm.CheckSiteSecured(site.ClearWave);
            ctx.Assert(gm.SiteSecuredThisRun, "site_secured/at_clear_wave");
            ctx.Assert(TerritoryManager.IsSiteCleared(site.Id, gm.MetaSave), "site_secured/persisted");
            gm.CurrentTerritorySectionId = null;
        }

        private void TestSuitSnapshotRoundTrip(TestContext ctx)
        {
            var cell = FindBuildableCell(4, 4);
            var node = new VineNode();
            node.Initialize(VineNodeRegistry.Get(VineNodeType.DamageTower));
            _grid.PlaceNode(node, cell);

            var snap = SuitManager.CreateSnapshot(_grid, "Obelisk", 1, MaterialType.None);
            ctx.Assert(snap.Nodes.Any(n => n.GridX == cell.X && n.GridY == cell.Y), "suit/snapshot_has_tower");

            int slot = SuitManager.SaveSnapshot(snap, "Flow Test Suit");
            ctx.Assert(slot >= 0, "suit/saved");

            _grid.RemoveNode(cell);
            var pf = ServiceLocator.Get<VinePathfinder>();
            SuitManager.ApplySuit(SuitManager.GetAll()[slot], _grid, pf);
            ctx.Assert(_grid.GetNode(cell) != null, "suit/applied_to_grid");
            _grid.RemoveNode(cell);
        }
    }
}
