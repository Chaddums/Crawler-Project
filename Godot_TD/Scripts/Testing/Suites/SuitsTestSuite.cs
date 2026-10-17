using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Suits as kits of towers. In a real battle: a snapshot keeps the best
    /// <see cref="Constants.SUIT_KIT_SIZE"/> towers by level and branch and no walls; it survives a
    /// save and load; an older save's layout loads as a kit; the debrief says what it would keep.
    /// A boss fight with a suit: its role, KIT on the build bar, kit towers placed free at their
    /// level and branch, the normal price once a type runs out, nothing back for selling one, and
    /// the suit lost with the fight. The boss fight screen offers "No suit" (FIGHT, nothing lost).
    /// The Suits screen lists each suit's towers by name and level (no map), takes a suit to the
    /// next boss fight, renames and salvages. Suits are kept in memory, never written to disk.
    /// </summary>
    public class SuitsTestSuite : ITestSuite
    {
        public string SuiteName => "suits";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var oldSave = gm.MetaSave;
            bool autoFire = GameSettings.BitAutoFire;
            SuitManager.UseInMemoryForTests();
            GameSettings.BitAutoFire = false;
            gm.SuppressAutoDebrief = true;
            try
            {
                // A save with every ordinary site on planet 1 secured, so its boss fight is open
                var save = new MetaPerkSaveData();
                foreach (var region in TerritoryManager.GetPlanet(1)?.Regions ?? new List<TerritoryRegion>())
                    foreach (var site in region.Sites)
                        if (!site.IsBossSite) TerritoryManager.ClearSite(site.Id, save);
                gm.MetaSave = save;
                var boss = TerritoryManager.NextBossSite(save, 1);
                ctx.StartTest();
                ctx.Assert(boss != null, "suits/boss_fight_open", boss?.Name ?? "none");

                int slot = await Capture(ctx);
                if (slot < 0 || boss == null) return;
                await BossWithSuit(ctx, slot, boss);
                await BossWithoutSuit(ctx, boss);
                await Screen(ctx, boss);
            }
            finally
            {
                gm.MetaSave = oldSave;
                gm.SuppressAutoDebrief = false;
                gm.AutoResolvePerks = false;
                GameSettings.BitAutoFire = autoFire;
                SuitKit.Clear();
                SuitManager.RestoreFromDisk();
                Engine.TimeScale = 1.0;
                if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = false;
            }
        }

        // ── A snapshot keeps the best towers ──

        private static async Task<int> Capture(TestContext ctx)
        {
            var gm = GameManager.Instance;
            if (!await StartRun(ctx, "Bruteforge")) return -1;
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            gm.AddResources(20000);
            var turretPlan = TowerUpgradePlan.For(VineNodeRegistry.Get(VineNodeType.DamageTower).Id);
            var coilPlan = TowerUpgradePlan.For(VineNodeRegistry.Get(VineNodeType.TeslaCoil).Id);
            string railgun = turretPlan?.Branches.FirstOrDefault()?.Id, storm = coilPlan?.Branches.FirstOrDefault()?.Id;
            // 12 towers and 2 walls: four at the top with a branch, three at level 2, five as built
            var build = new (VineNodeType type, int level, string branch)[]
            {
                (VineNodeType.DamageTower, 3, railgun), (VineNodeType.DamageTower, 3, railgun), (VineNodeType.TeslaCoil, 3, storm),
                (VineNodeType.DamageTower, 3, railgun), (VineNodeType.FlakBattery, 2, null), (VineNodeType.ScatterCannon, 2, null),
                (VineNodeType.BuffEmitter, 2, null), (VineNodeType.SlowField, 1, null), (VineNodeType.PushPull, 1, null),
                (VineNodeType.DamageTower, 1, null), (VineNodeType.FlakBattery, 1, null), (VineNodeType.TeslaCoil, 1, null),
                (VineNodeType.BarrierWall, 1, null), (VineNodeType.BarrierWall, 1, null),
            };
            var cells = FreeCells(grid, build.Length);
            int placed = 0;
            for (int i = 0; i < build.Length && i < cells.Count; i++)
            {
                // On a canyon a wall is a Barricade: it goes on the floor
                var at = build[i].type == VineNodeType.BarrierWall && grid.IsCanyon ? TestCells.WallSpot(grid) ?? cells[i] : cells[i];
                var n = placer.TestPlaceAt(build[i].type, at);
                if (n == null) continue;
                n.ForceUpgrade(build[i].level, build[i].branch);
                placed++;
            }
            ctx.StartTest();
            ctx.Assert(placed == build.Length, "suits/build_placed", $"{placed} of {build.Length}");

            var snap = SuitManager.CreateSnapshot(grid, gm.SelectedRole, 1, MaterialType.None, "Test Site", 23);
            var levels = snap.Nodes.Select(n => n.Level).ToList();
            ctx.Assert(snap.Nodes.Count == Constants.SUIT_KIT_SIZE && snap.Nodes.All(n => n.NodeType != VineNodeType.BarrierWall),
                "suits/kit_keeps_8_and_no_walls", $"{snap.Nodes.Count} towers: {string.Join(", ", snap.Nodes.Select(n => n.NodeType))}");
            ctx.Assert(levels.SequenceEqual(levels.OrderByDescending(l => l)) && snap.Nodes.Count(n => n.Level == 3) == 4
                && snap.Nodes.Count(n => n.Level == 2) == 3, "suits/kit_takes_the_best", string.Join(" ", levels));
            ctx.Assert(snap.Nodes.Count(n => n.Branch == railgun && n.NodeType == VineNodeType.DamageTower) == 3
                && snap.Nodes.Any(n => n.NodeType == VineNodeType.TeslaCoil && n.Branch == storm), "suits/kit_keeps_branches",
                string.Join(", ", snap.Nodes.Select(n => $"{n.NodeType} {n.Branch}")));

            string kit = DebriefScreen.SuitPromptKit(snap);
            ctx.Assert(kit.StartsWith("8 towers") && kit.Contains("3 Junk Turret L3") && kit.Contains("Tesla Coil L3"), "suits/debrief_says_what_it_keeps", kit);

            int slot = SuitManager.SaveSnapshot(snap, "  Test Kit  ");
            var back = SuitManager.Parse(SuitManager.Serialize(SuitManager.GetAll()));
            var s = slot >= 0 ? back[slot] : null;
            ctx.Assert(s != null && s.Name == "Test Kit" && s.Site == "Test Site" && s.Wave == 23 && s.Role == gm.SelectedRole
                && s.Nodes.Select(n => (n.NodeType, n.Level, n.Branch)).SequenceEqual(snap.Nodes.Select(n => (n.NodeType, n.Level, n.Branch))),
                "suits/save_and_load", s == null ? "not saved" : $"{s.Name}, {SuitManager.Origin(s)}, {s.Nodes.Count} towers");

            // An older save: the layout with sensors, routing and no levels becomes a kit of towers
            string old = "{\"suits\":[{\"name\":\"Old\",\"role\":\"Arcanist\",\"planet\":1,\"nodes\":[" +
                string.Join(",", Enumerable.Range(0, 14).Select(i => $"{{\"x\":{i},\"y\":2,\"type\":{(int)(i % 3 == 0 ? VineNodeType.ProximitySensor : i % 3 == 1 ? VineNodeType.Extender : VineNodeType.DamageTower)}}}")) +
                $",{{\"x\":1,\"y\":5,\"type\":{(int)VineNodeType.TeslaCoil}}}]}}]}}";
            var oldSuit = SuitManager.Parse(old)[0];
            ctx.Assert(oldSuit != null && oldSuit.Nodes.Count == 5 && oldSuit.Nodes.All(n => SuitManager.IsKitTower(n.NodeType) && n.Level == 1),
                "suits/old_save_becomes_a_kit", oldSuit == null ? "none" : string.Join(", ", oldSuit.Nodes.Select(n => n.NodeType)));
            return slot;
        }

        // ── A boss fight with the suit ──

        private static async Task BossWithSuit(TestContext ctx, int slot, TerritorySite boss)
        {
            var gm = GameManager.Instance;
            var suit = SuitManager.GetAll()[slot];
            var turret = VineNodeRegistry.Get(VineNodeType.DamageTower);
            gm.SelectedRole = "Obelisk"; // the suit's role should win
            gm.AutoResolvePerks = true;
            gm.StartBossRun(1, slot, boss.Id);
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.StartTest();
            ctx.Assert(ok && gm.IsBossRun && RoleRun.Role == suit.Role && SuitKit.Total == suit.Nodes.Count, "suits/boss_fight_takes_the_suit",
                $"boss run {gm.IsBossRun}, role {RoleRun.Role} (suit {suit.Role}), kit {SuitKit.Total}");
            if (!ok) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            await ctx.Wait(0.3f);
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();

            var kitButton = FindButton(ctx.FindNode<VineHUD>(), turret.Name);
            ctx.Assert(kitButton != null && kitButton.Text.Contains("KIT x3"), "suits/build_bar_says_kit", kitButton?.Text.Replace("\n", " ") ?? "no button");

            gm.AddResources(2000);
            var cells = FreeCells(grid, 6);
            int before = gm.CurrentResources;
            var placed = new List<VineNode>();
            for (int i = 0; i < 3; i++) placed.Add(placer.TestPlaceAt(VineNodeType.DamageTower, cells[i]));
            string railgun = suit.Nodes.First(n => n.NodeType == VineNodeType.DamageTower).Branch;
            ctx.Assert(placed.All(n => n != null && n.Level == 3 && n.Branch?.Id == railgun) && gm.CurrentResources == before,
                "suits/kit_towers_free_at_their_level", $"{string.Join(", ", placed.Select(n => $"L{n?.Level} {n?.Branch?.Id}"))}; spent {before - gm.CurrentResources}");
            await ctx.Wait(0.2f);
            ctx.Assert(kitButton != null && !kitButton.Text.Contains("KIT") && kitButton.Text.Contains($"{MetaRun.PlaceCost(turret)}r"), "suits/price_back_when_type_used_up",
                kitButton?.Text.Replace("\n", " ") ?? "");
            int beforeFourth = gm.CurrentResources;
            int price = MetaRun.PlaceCost(turret);
            var fourth = placer.TestPlaceAt(VineNodeType.DamageTower, cells[3]);
            ctx.Assert(fourth != null && fourth.Level == 1 && beforeFourth - gm.CurrentResources == price && price > 0,
                "suits/fourth_turret_costs", $"level {fourth?.Level}, paid {beforeFourth - gm.CurrentResources}");
            ctx.Assert(placed[0] != null && placed[0].SellValue == 0, "suits/kit_tower_sells_for_nothing", $"{placed[0]?.SellValue}");
            ctx.Assert(SuitKit.Left == suit.Nodes.Count - 3, "suits/kit_counts_down", $"{SuitKit.Left} left");

            // Losing the fight loses the suit
            gm.SetPhase(GamePhase.Defeat);
            await ctx.Wait(0.3f);
            ctx.Assert(SuitManager.GetAll()[slot].Consumed && SuitManager.GetAvailableSuits().Count == 0, "suits/lost_with_the_fight",
                $"consumed {SuitManager.GetAll()[slot].Consumed}");
            // Put it back for the screen checks
            SuitManager.GetAll()[slot].Consumed = false;
        }

        // ── Without a suit ──

        private static async Task BossWithoutSuit(TestContext ctx, TerritorySite boss)
        {
            var gm = GameManager.Instance;
            gm.ShowBossConfirmation(1, 0, boss.Id);
            await ctx.Wait(1.0f);
            var screen = ctx.FindNode<BossConfirmScreen>();
            ctx.StartTest();
            ctx.Assert(screen != null && screen.SuitIndex >= 0 && screen.ConfirmButton?.Text == "RISK IT", "suits/boss_screen_takes_a_suit",
                screen == null ? "no screen" : $"suit {screen.SuitIndex}, {screen.ConfirmButton?.Text}");
            if (screen == null) return;
            var lines = screen.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList();
            ctx.Assert(lines.Any(t => t.Contains("Junk Turret L3")) && lines.Any(t => t.Contains("suit is lost")), "suits/boss_screen_lists_the_kit",
                string.Join(" | ", lines.Take(8)));
            screen.SwitchSuit(-1);
            await ctx.Wait(0.2f);
            screen = ctx.FindNode<BossConfirmScreen>();
            ctx.Assert(screen != null && screen.SuitIndex < 0 && screen.ConfirmButton?.Text == "FIGHT" && !screen.ConfirmButton.Disabled,
                "suits/no_suit_option", screen == null ? "no screen" : $"{screen.SuitIndex} {screen.ConfirmButton?.Text}");
            gm.AutoResolvePerks = true;
            screen?.ConfirmButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok && gm.IsBossRun && gm.EquippedSuitIndex == null && SuitKit.Total == 0, "suits/fight_without_a_suit",
                $"boss run {gm.IsBossRun}, suit {gm.EquippedSuitIndex}, kit {SuitKit.Total}");
            gm.SetPhase(GamePhase.Defeat);
            await ctx.Wait(0.3f);
            ctx.Assert(SuitManager.GetAvailableSuits().Count == 1, "suits/nothing_lost_without_a_suit", $"{SuitManager.GetAvailableSuits().Count} usable");
        }

        // ── The Suits screen ──

        private static async Task Screen(TestContext ctx, TerritorySite boss)
        {
            var gm = GameManager.Instance;
            gm.ShowSuitInventory();
            await ctx.Wait(1.0f);
            var screen = ctx.FindNode<SuitArmoryScreen>();
            ctx.StartTest();
            ctx.Assert(screen != null && screen.SlotCards.Count == Constants.MAX_SUIT_SLOTS, "suits/screen_opens", screen == null ? "no screen" : $"{screen.SlotCards.Count} slots");
            if (screen == null) return;
            int slot = System.Array.FindIndex(SuitManager.GetAll(), s => s != null && s.Usable);
            var card = screen.SlotCards[slot];
            var towers = card.FindChildren("Tower*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList();
            var all = screen.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList();
            ctx.Assert(towers.Count == Constants.SUIT_KIT_SIZE && towers.Count(t => t.StartsWith("Junk Turret") && t.Contains("L3/3")) == 3,
                "suits/screen_lists_towers_and_levels", string.Join(" | ", towers));
            ctx.Assert(!all.Any(t => t.Contains("NETWORK LAYOUT") || t.Contains("NODES")) && all.Any(t => t.Contains("boss fight")),
                "suits/screen_explains_no_map", all.FirstOrDefault(t => t.Contains("boss fight")) ?? "");
            var data = SuitArmoryScreen.SuitsData();
            var d0 = data[slot].AsGodotDictionary();
            ctx.Assert(d0["towers"].AsGodotArray().Count == Constants.SUIT_KIT_SIZE && d0["origin"].AsString().Contains("wave 23")
                && SuitArmoryScreen.BossData()["open"].AsBool(), "suits/page_data", Json.Stringify(d0)[..System.Math.Min(160, Json.Stringify(d0).Length)]);

            // Rename
            var edit = card.FindChild("NameEdit", true, false) as LineEdit;
            var rename = card.FindChild("Rename", true, false) as Button;
            if (edit != null) edit.Text = "Railgun Wall";
            rename?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.2f);
            ctx.Assert(SuitManager.GetAll()[slot].Name == "Railgun Wall"
                && screen.SlotCards[slot].FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text == "Railgun Wall"),
                "suits/rename", SuitManager.GetAll()[slot].Name);

            // Take it into the boss fight
            var deploy = screen.SlotCards[slot].FindChild("Deploy", true, false) as Button;
            ctx.Assert(deploy != null && !deploy.Disabled && deploy.Text.Contains(boss.Name.ToUpper()), "suits/deploy_names_the_boss", deploy?.Text ?? "none");
            deploy?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(1.0f);
            var confirm = ctx.FindNode<BossConfirmScreen>();
            ctx.Assert(confirm != null && confirm.SuitIndex == slot && gm.BossSectionId == boss.Id, "suits/deploy_goes_to_the_boss_fight",
                confirm == null ? "no boss screen" : $"suit {confirm.SuitIndex}, site {gm.BossSectionId}");

            // Salvage takes two clicks
            gm.ShowSuitInventory();
            await ctx.Wait(1.0f);
            screen = ctx.FindNode<SuitArmoryScreen>();
            var salvage = screen?.SlotCards[slot].FindChild("Salvage", true, false) as Button;
            salvage?.EmitSignal(BaseButton.SignalName.Pressed);
            bool stillThere = SuitManager.GetAll()[slot] != null;
            salvage?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.2f);
            ctx.Assert(stillThere && SuitManager.GetAll()[slot] == null, "suits/salvage_asks_twice", $"after one click {stillThere}");
        }

        // ── Helpers ──

        private static async Task<bool> StartRun(TestContext ctx, string role)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "suits/battle_loads", "no site"); return false; }
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = 1;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = role;
            gm.AvailableNodes = SpireData.Get(role)?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, "suits/battle_loads", site.Id);
            if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = true;
            await ctx.Wait(0.5f);
            return ok;
        }

        private static List<Vector2I> FreeCells(VineGrid grid, int count)
        {
            var list = new List<Vector2I>();
            int cx = grid.Width / 2, cy = grid.Height / 2;
            var pf = ServiceLocator.Get<VinePathfinder>();
            for (int r = 3; r < 20 && list.Count < count; r++)
                for (int dx = -r; dx <= r && list.Count < count; dx += 2)
                    foreach (int dy in new[] { -r, r })
                    {
                        var c = new Vector2I(cx + dx, cy + dy);
                        if (list.Count < count && grid.CanPlace(c) && !pf.WouldBlockAllPaths(c)
                            && !list.Any(o => Mathf.Abs(o.X - c.X) + Mathf.Abs(o.Y - c.Y) < 2))
                            list.Add(c);
                    }
            return list;
        }

        private static Button FindButton(Node root, string startsWith)
        {
            if (root == null) return null;
            if (root is Button b && b.Text.StartsWith(startsWith)) return b;
            foreach (var c in root.GetChildren())
            {
                var f = FindButton(c, startsWith);
                if (f != null) return f;
            }
            return null;
        }
    }
}
