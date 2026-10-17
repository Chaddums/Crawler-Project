using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Screenshots of the code-built screens between runs, the way the game opens them:
    /// Territory for each planet (and for a planet with no territory), the Perk Tree and the
    /// draft. Checks each screen draws something and has a way out. Needs a display;
    /// screenshots to test-reports/menus/.
    /// </summary>
    public class MenusTestSuite : ITestSuite
    {
        public string SuiteName => "menus";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            string outDir = ProjectSettings.GlobalizePath("res://test-reports/menus");
            System.IO.Directory.CreateDirectory(outDir);
            async Task Shot(string name)
            {
                for (int i = 0; i < 4; i++) await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var img = ctx.Tree.Root.GetTexture().GetImage();
                img.SavePng($"{outDir}/{name}.png");
                // Mostly one colour = a blank screen
                int same = 0, n = 0;
                var c0 = img.GetPixel(img.GetWidth() / 2, img.GetHeight() / 2);
                for (int x = 0; x < img.GetWidth(); x += 40)
                    for (int y = 0; y < img.GetHeight(); y += 40, n++)
                        if (img.GetPixel(x, y).IsEqualApprox(c0)) same++;
                var buttons = All<Button>(ctx.Tree.Root).Count(b => b.IsVisibleInTree() && !b.Disabled);
                ctx.StartTest();
                ctx.Assert(same < n * 0.97f && buttons > 0, $"menus/{name}/draws_and_has_a_way_out",
                    $"{100f * same / n:F0}% one colour, {buttons} usable buttons");
            }

            foreach (int planet in new[] { 1, 2, 3 })
            {
                gm.CurrentPlanet = planet;
                gm.CurrentRunMode = RunMode.Harvest;
                gm.ShowTerritory();
                await ctx.Wait(1.5f);
                await Shot($"territory_P{planet}");
            }
            gm.ShowMetaHub();
            await ctx.Wait(1.5f);
            await Shot("hub");
            // Settings over the hub, the new-account button pressed once (it asks again first)
            var settings = SettingsScreen.Open(ctx.Tree.CurrentScene);
            await ctx.Wait(0.3f);
            settings.FindChildren("NewAccount", "Button", true, false).OfType<Button>().FirstOrDefault()?.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.2f);
            await Shot("settings");
            settings.Close();
            await ctx.Wait(0.3f);
            // Deploy from the hub goes to the Spire pick for the campaign's next site
            MetaHubScreen.DeployNext();
            await ctx.Wait(1.5f);
            await Shot("spire_pick");
            ctx.StartTest();
            ctx.Assert(!string.IsNullOrEmpty(gm.CurrentTerritorySectionId), "menus/hub_deploy_picks_a_site", gm.CurrentTerritorySectionId ?? "none");
            // Suits: a kit on the Suits screen and the boss fight screen (kept in memory)
            {
                SuitManager.UseInMemoryForTests();
                var keep = gm.MetaSave;
                var open = new MetaPerkSaveData();
                foreach (var region in TerritoryManager.GetPlanet(1)?.Regions ?? new System.Collections.Generic.List<TerritoryRegion>())
                    foreach (var site in region.Sites)
                        if (!site.IsBossSite) TerritoryManager.ClearSite(site.Id, open);
                gm.MetaSave = open;
                var kit = new SuitSaveData { Name = "Railgun Wall", Role = "Bruteforge", Planet = 1, Site = "Relay Station Alpha", Wave = 23 };
                foreach (var (t, lv) in new[] { (VineNodeType.DamageTower, 3), (VineNodeType.DamageTower, 3), (VineNodeType.TeslaCoil, 3),
                    (VineNodeType.FlakBattery, 2), (VineNodeType.ScatterCannon, 2), (VineNodeType.BuffEmitter, 2), (VineNodeType.SlowField, 1), (VineNodeType.PushPull, 1) })
                {
                    var plan = TowerUpgradePlan.For(VineNodeRegistry.Get(t).Id);
                    kit.Nodes.Add(new SuitNodeEntry { NodeType = t, Level = lv, Branch = lv == 3 ? plan?.Branches.FirstOrDefault()?.Id : null });
                }
                SuitManager.GetAll()[0] = kit;
                SuitManager.GetAll()[1] = new SuitSaveData { Name = "Glass Cannon", Role = "Arcanist", Planet = 1, Site = "Data Vault", Wave = 14, Consumed = true,
                    Nodes = new System.Collections.Generic.List<SuitNodeEntry> { new() { NodeType = VineNodeType.TeslaCoil } } };
                gm.ShowSuitInventory();
                await ctx.Wait(1.5f);
                await Shot("suits");
                var boss = TerritoryManager.NextBossSite(open, 1);
                if (boss != null)
                {
                    gm.ShowBossConfirmation(1, 0, boss.Id);
                    await ctx.Wait(1.5f);
                    await Shot("boss_fight");
                }
                gm.MetaSave = keep;
                SuitManager.RestoreFromDisk();
            }

            // The perk tree as the reported save would open it: an old tree's 8 perks refunded
            // on top of 3 unspent points, then a few ranks taken
            var oldSave = gm.MetaSave;
            SafeFile.WriteAllText("user://vine_meta.json",
                "{\"allocated\": [0, 1, 2, 4, 7, 10, 13, 16, 19], \"points\": 3, \"run_count\": 10}");
            gm.MetaSave = MetaPerkSave.Load();
            ctx.Tree.ChangeSceneToFile("res://Scenes/MetaPerkTree.tscn");
            await ctx.Wait(1.5f);
            await Shot("perk_tree_migrated");
            if (ctx.Tree.CurrentScene is MetaPerkTreeScreen tree)
            {
                foreach (var id in new[] { "calibrated_barrels", "calibrated_barrels", "masonry", "calibrated_barrels",
                    "field_kit", "hull_plating", "salvage_rig", "wave_processor" })
                    tree.OnBuy(id);
                await ctx.Wait(0.3f);
                await Shot("perk_tree");
            }
            // The reported save: the first tree (rows up to 10) bought out with 2 points left; the
            // screen opens scrolled to the new rows
            var bought = new MetaPerkSaveData { AvailablePoints = 2 };
            foreach (var n in MetaPerkRegistry.GetAll()) if (n.Needs <= 10) bought.Ranks[n.Id] = n.MaxRank;
            gm.MetaSave = bought;
            MetaPerkSave.Save(bought);
            ctx.Tree.ChangeSceneToFile("res://Scenes/MetaPerkTree.tscn");
            await ctx.Wait(1.5f);
            await Shot("perk_tree_full");
            if (ctx.Tree.CurrentScene is MetaPerkTreeScreen fullTree)
            {
                ctx.StartTest();
                ctx.Assert(fullTree.ScrolledTo > 0, "menus/perk_tree_opens_at_new_rows", $"scrolled {fullTree.ScrolledTo}");
            }
            gm.MetaSave = oldSave;
            MetaPerkSave.Save(oldSave ?? new MetaPerkSaveData());
            gm.CurrentPlanet = 1;
        }

        private static System.Collections.Generic.IEnumerable<T> All<T>(Node n) where T : Node
        {
            if (n is T t) yield return t;
            foreach (var c in n.GetChildren())
                foreach (var x in All<T>(c)) yield return x;
        }
    }
}
