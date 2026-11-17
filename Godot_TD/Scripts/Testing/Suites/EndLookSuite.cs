using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Probe (not in "all"; needs a display): pictures of what 2026-11's design answers added, to
    /// END_LOOK (default test-reports/end_look): the Company Store as issued and with a paint, a
    /// trim and a crown pointed at, the ending, the Command Center of a second campaign, the
    /// territory map's dots, the boss screen, and each boss on the field beside towers (The
    /// Silencer's dark towers, The Overloader's ring closing).
    /// </summary>
    public class EndLookSuite : ITestSuite
    {
        public string SuiteName => "endlook";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var oldSave = gm.MetaSave;
            string dir = OS.GetEnvironment("END_LOOK") is { Length: > 0 } d ? d : ProjectSettings.GlobalizePath("res://test-reports/end_look");
            bool shoot = HubLook.CanShoot;
            string only = OS.GetEnvironment("END_LOOK_ONLY");
            bool Want(string part) => string.IsNullOrEmpty(only) || only.Split(',').Contains(part);
            ctx.StartTest();
            ctx.Assert(true, "endlook/started", shoot ? dir : "no display: nothing is saved");
            bool writes = MetaPerkSave.SuppressWrites;
            MetaPerkSave.SuppressWrites = true;
            SuitManager.UseInMemoryForTests();
            gm.SuppressAutoDebrief = true;
            GameSettings.BitAutoFire = false;
            async Task Shot(string label, float wait = 2.5f)
            {
                await ctx.Wait(wait);
                if (!shoot) return;
                await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                HubLook.Save(ctx.Tree, dir, label);
            }
            try
            {
                var all = new MetaPerkSaveData { MetaResources = 61250, RunCount = 23 };
                foreach (int planet in new[] { 1, 2 })
                    foreach (var region in TerritoryManager.GetPlanet(planet).Regions)
                        foreach (var site in region.Sites) TerritoryManager.ClearSite(site.Id, all);

                if (Want("store"))
                {
                    gm.MetaSave = new MetaPerkSaveData { MetaResources = 61250 };
                    gm.MetaSave.OwnedCosmetics.Add("paint_ember");
                    gm.ShowCompanyStore();
                    await Shot("store_1_standard", 4f);
                    var store = ctx.FindNode<CompanyStoreScreen>();
                    // What the Spire on the stage is made of, top to bottom (a crown sits on its top)
                    if (store?.Preview.Spire is { } spireNode)
                        foreach (var mi in spireNode.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().OrderByDescending(m => (m.GlobalTransform * m.GetAabb()).End.Y).Take(8))
                        {
                            var box = mi.GlobalTransform * mi.GetAabb();
                            GD.Print($"[EndLook] spire mesh {mi.Name}: y {box.Position.Y:F2} to {box.End.Y:F2}, {box.Size.X:F2} x {box.Size.Z:F2}, visible {mi.IsVisibleInTree()}, {mi.Mesh?.GetSurfaceCount()} surfaces");
                        }
                    foreach (var (label, ids) in new[] { ("store_2_ember_gold_halo", new[] { "paint_ember", "trim_gold", "spire_halo" }),
                        ("store_3_bone_ultraviolet_orbit", new[] { "paint_bone", "trim_ultraviolet", "spire_orbit" }),
                        ("store_4_goldleaf_whitehot_crown", new[] { "paint_goldleaf", "trim_whitehot", "spire_crown" }),
                        ("store_5_void_ice_beacon", new[] { "paint_void", "trim_ice", "spire_beacon" }),
                        ("store_6_royal_signal", new[] { "paint_royal", "trim_signal" }),
                        ("store_7_verdigris_reactor", new[] { "paint_verdigris" }),
                        ("store_8_reactor", new[] { "paint_reactor" }) })
                    {
                        Cosmetics.ClearPreview();
                        foreach (var id in ids) { var item = Cosmetics.Get(id); if (item != null) Cosmetics.SetPreview(item.Slot, item); }
                        store?.Preview.Rebuild();
                        await Shot(label, 1.5f);
                    }
                    Cosmetics.ClearPreview();
                }

                if (Want("ending"))
                {
                    gm.MetaSave = all;
                    gm.ShowEnding();
                    await ctx.Wait(1.0f);
                    ctx.FindNode<EndingScreen>()?.ShowAll();
                    await Shot("ending", 1.5f);
                    ctx.FindNode<EndingScreen>()?.BeginNext();
                    await Shot("hub_campaign_2", 2f);
                    gm.CurrentPlanet = 1;
                    gm.ShowTerritory();
                    await Shot("map_campaign_2", 2f);
                }

                if (Want("silencer") || Want("overloader"))
                {
                    foreach (int planet in new[] { 1, 2 })
                    {
                        var boss = TerritoryManager.GetPlanet(planet).Regions.SelectMany(r => r.Sites).First(s => s.IsBossSite);
                        var profile = SiteBosses.ForSite(boss);
                        if (profile == null || !Want(profile.Jams ? "silencer" : "overloader")) continue;
                        var save = new MetaPerkSaveData();
                        foreach (int pl in new[] { 1, 2 })
                            foreach (var region in TerritoryManager.GetPlanet(pl).Regions)
                                foreach (var site in region.Sites)
                                    if (site.Id != boss.Id && (pl < planet || pl == planet)) TerritoryManager.ClearSite(site.Id, save);
                        gm.MetaSave = save;
                        gm.ShowBossConfirmation(planet, -1, boss.Id);
                        await Shot($"boss_{planet}_confirm", 2f);
                        gm.SelectedRole = "Bruteforge";
                        gm.AutoResolvePerks = true;
                        gm.StartBossRun(planet, -1, boss.Id);
                        await ctx.Wait(1.0f);
                        if (!await ctx.WaitForPhase(GamePhase.Build, 60f)) continue;
                        if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
                        await ctx.Wait(3f);
                        var grid = ServiceLocator.Get<VineGrid>();
                        var pf = ServiceLocator.Get<VinePathfinder>();
                        var bit = ServiceLocator.Get<VinePlayer>();
                        grid.Harvester.IncreaseMaxHP(100000f);
                        var m = SiteBossManager.Current;
                        var route = pf.GetCachedPath(grid.ActiveEntryRegions[0].Center) ?? new List<Vector2I>();
                        var at = route[Mathf.Min(route.Count - 1, route.Count / 2)];
                        var here = grid.GridToWorld(at);
                        var types = new[] { VineNodeType.DamageTower, VineNodeType.ScatterCannon, VineNodeType.TeslaCoil, VineNodeType.FlakBattery, VineNodeType.MortarPit, VineNodeType.RailLance };
                        int k = 0;
                        foreach (var c in TestCells.BesideRoute(grid, route, 80, skip: Mathf.Max(0, route.Count / 2 - 14))
                            .OrderBy(c => new Vector2(grid.GridToWorld(c).X - here.X, grid.GridToWorld(c).Z - here.Z).Length()).Take(16))
                        {
                            var node = new VineNode();
                            node.Initialize(VineNodeRegistry.Get(types[k++ % types.Length]));
                            grid.PlaceNode(node, c);
                        }
                        var e = m.Spawn();
                        if (e == null) continue;
                        e.BaseSpeed = 0f;
                        e.GlobalPosition = new Vector3(here.X, grid.GetWorldHeight(here.X, here.Z), here.Z);
                        var beside = grid.GridToWorld(route[Mathf.Min(route.Count - 1, route.Count / 2 + 3)]);
                        bit.GlobalPosition = new Vector3(beside.X, grid.GetWorldHeight(beside.X, beside.Z), beside.Z);
                        Celebration.Dismiss("Boss");
                        await Shot($"boss_{planet}_{profile.Id}_1", 6.5f);
                        if (profile.Overloads)
                        {
                            Engine.TimeScale = 3.0;
                            await ctx.WaitUntil(() => m.ChargeLeft > 0f && m.ChargeLeft < profile.Charge * 0.5f, 20f);
                            Engine.TimeScale = 0.05;
                            await Shot($"boss_{planet}_{profile.Id}_2_charging", 0.3f);
                            Engine.TimeScale = 3.0;
                            await ctx.WaitUntil(() => m.Bursts >= 2, 30f);
                            Engine.TimeScale = 1.0;
                            await Shot($"boss_{planet}_{profile.Id}_3_after_two_bursts", 1.0f);
                        }
                        gm.SetPhase(GamePhase.Defeat);
                        await ctx.Wait(0.3f);
                    }
                }
            }
            finally
            {
                gm.MetaSave = oldSave;
                gm.SuppressAutoDebrief = false;
                gm.AutoResolvePerks = false;
                gm.EndingPending = false;
                MetaPerkSave.SuppressWrites = writes;
                Cosmetics.ClearPreview();
                SuitManager.RestoreFromDisk();
                Engine.TimeScale = 1.0;
            }
        }
    }
}
