using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Canyon maps (CanyonMaps, Data/Canyons): every campaign map is one; the floor is the
    /// enemies' road and takes no tower (and says why), towers stand on the wall tops at the
    /// walls' height and give the ledge back when sold, the cursor finds the wall top it points
    /// at (cutting the ray at y = 0 picked the cell under the wall's foot), walkers stay on the
    /// floor, BIT can't climb the faces, a raider climbs the wall to a drill, and the comms panel
    /// carries the run's briefing.
    /// </summary>
    public class CanyonTestSuite : ITestSuite
    {
        public string SuiteName => "canyon";

        public async Task Run(TestContext ctx)
        {
            // Every site's map is a canyon
            ctx.StartTest();
            var layouts = new HashSet<string>();
            foreach (int planet in new[] { 1, 2 })
                foreach (var region in TerritoryManager.GetPlanet(planet)?.Regions ?? new List<TerritoryRegion>())
                    foreach (var site in region.Sites)
                        if (!string.IsNullOrEmpty(site.MapLayout)) layouts.Add(site.MapLayout);
            var missing = layouts.Where(l => !CanyonMaps.Exists(l)).ToList();
            ctx.Assert(layouts.Count >= 10 && missing.Count == 0, "canyon/every_site_map",
                $"{layouts.Count} site maps, without a canyon: {string.Join(", ", missing)}");

            var gm = GameManager.Instance;
            gm.CurrentTerritorySectionId = "p1_ng_relay";
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 15f);
            ctx.Assert(inBuild, "canyon/battle_loads", "Battle should reach Build phase");
            if (!inBuild) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            ctx.Assert(grid.IsCanyon && grid.CanyonWallHeight >= 2f, "canyon/gateway_is_a_canyon", $"canyon {grid.IsCanyon}, walls {grid.CanyonWallHeight:0.0}");
            if (!grid.IsCanyon) return;
            gm.AddResources(2000);

            // The floor takes nothing, and says why
            ctx.StartTest();
            var route = ServiceLocator.Get<VinePathfinder>().GetCachedPath(grid.ActiveEntryRegions[0].Center) ?? new List<Vector2I>();
            var floorCell = route.Count > 6 ? route[route.Count / 2] : grid.ActiveEntryRegions[0].Center;
            string why = placer.WhyNotType(floorCell);
            var onFloor = placer.TestPlaceAt(VineNodeType.DamageTower, floorCell);
            ctx.Assert(!grid.CanPlace(floorCell) && onFloor == null && why != null && why.Contains("floor"), "canyon/floor_takes_no_tower",
                $"cell {grid.GetCell(floorCell)} at {floorCell}: {why}");

            // A wall top beside the route takes a tower at the wall's height; selling gives the ledge back
            ctx.StartTest();
            Vector2I? lip = null;
            foreach (var c in route)
            {
                for (int dx = -2; dx <= 2 && lip == null; dx++)
                    for (int dy = -2; dy <= 2 && lip == null; dy++)
                    {
                        var n = c + new Vector2I(dx, dy);
                        if (grid.InBounds(n) && grid.GetCell(n) == VineCellType.Ledge && placer.WhyNotType(n) == null) lip = n;
                    }
                if (lip != null) break;
            }
            VineNode tower = lip.HasValue ? placer.TestPlaceAt(VineNodeType.DamageTower, lip.Value) : null;
            float y = tower != null ? tower.GlobalPosition.Y : 0f;
            ctx.Assert(tower != null && y > grid.CanyonWallHeight * 0.8f, "canyon/tower_on_the_wall",
                lip.HasValue ? $"tower at {lip} stands at y {y:0.00} (walls {grid.CanyonWallHeight:0.0})" : "no ledge by the route");
            if (tower != null)
            {
                grid.RemoveNode(lip.Value);
                await ctx.Wait(0.1f);
                ctx.Assert(grid.GetCell(lip.Value) == VineCellType.Ledge && grid.CanPlace(lip.Value), "canyon/sold_gives_ledge_back", $"cell {grid.GetCell(lip.Value)}");
            }

            // The cursor finds the wall top it points at
            ctx.StartTest();
            var cam = ctx.Tree.Root.GetViewport().GetCamera3D();
            int hits = 0, tried = 0;
            string missNote = "";
            if (cam != null)
            {
                foreach (var c in Ledges(grid).Take(400).Where((_, i) => i % 23 == 0))
                {
                    var top = grid.GridToWorld(c);
                    top.Y = grid.GetWorldHeight(top.X, top.Z);
                    if (cam.IsPositionBehind(top)) continue;
                    var screen = cam.UnprojectPosition(top);
                    var picked = grid.PickGround(cam.ProjectRayOrigin(screen), cam.ProjectRayNormal(screen));
                    tried++;
                    if (picked.HasValue && grid.WorldToGrid(picked.Value) == c) hits++;
                    else if (missNote.Length == 0) missNote = $" first miss {c} -> {(picked.HasValue ? grid.WorldToGrid(picked.Value).ToString() : "none")}";
                }
            }
            ctx.Assert(tried >= 5 && hits == tried, "canyon/cursor_finds_wall_top", $"{hits}/{tried} wall tops picked{missNote}");

            // BIT stays in the canyon: the faces and tops are no place for it
            ctx.StartTest();
            ctx.Assert(VinePlayer.BlocksBit(VineCellType.Cliff) && VinePlayer.BlocksBit(VineCellType.Ledge) && !VinePlayer.BlocksBit(VineCellType.Empty),
                "canyon/bit_stays_on_the_floor", "faces and wall tops block BIT, the floor doesn't");

            // The run's briefing came over the comms panel
            ctx.StartTest();
            bool briefed = await ctx.WaitUntil(() => Comms.Shown > 0, 12f);
            ctx.Assert(briefed && !string.IsNullOrEmpty(Comms.LastText) && !Comms.LastText.Contains('{'), "canyon/briefing_on_comms",
                $"{Comms.Shown} lines, last [{Comms.LastSpeaker}] {Comms.LastText}");

            // Walkers keep to the floor
            ctx.StartTest();
            gm.SetCoreLives(999);
            grid.Harvester?.IncreaseMaxHP(100000f);
            var wm = ServiceLocator.Get<VineWaveManager>();
            Engine.TimeScale = 4.0;
            int samples = 0, off = 0;
            string offNote = "";
            ulong t0 = Time.GetTicksMsec();
            try
            {
                wm.StartWave();
                while (Time.GetTicksMsec() - t0 < 30000)
                {
                    await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.PhysicsFrame);
                    foreach (var e in Roster.Enemies(ctx.Tree))
                    {
                        if (!GodotObject.IsInstanceValid(e) || !e.IsAlive || e.IsFlying || e.IsRaider || !e.OnGrid) continue;
                        var c = grid.WorldToGrid(e.GlobalPosition);
                        if (!grid.InBounds(c)) continue;
                        samples++;
                        if (!grid.IsWalkable(c) || e.GlobalPosition.Y > grid.CanyonWallHeight * 0.4f)
                        {
                            off++;
                            if (offNote.Length == 0) offNote = $" first {e.Name} in {grid.GetCell(c)} {c} at y {e.GlobalPosition.Y:0.0}";
                        }
                    }
                    if (!wm.WaveActive && Time.GetTicksMsec() - t0 > 2000) break;
                }
            }
            finally { Engine.TimeScale = 1.0; }
            ctx.Assert(samples > 50 && off <= samples / 200, "canyon/walkers_keep_to_the_floor", $"{off} of {samples} samples off the floor{offNote}");
            foreach (var e in Roster.Enemies(ctx.Tree)) e.QueueFree();
            await ctx.Wait(0.3f);

            // A raider climbs the wall to a drill on the ore
            ctx.StartTest();
            await ctx.WaitForPhase(GamePhase.Build, 15f);
            var ore = grid.GetResourceNodes().FirstOrDefault(c => grid.GetNode(c) == null);
            var drill = grid.InBounds(ore) ? placer.TestPlaceAt(VineNodeType.SalvageDrill, ore) : null;
            if (drill == null) { ctx.Assert(false, "canyon/raider_climbs_to_drill", $"no drill on the ore at {ore}: {placer.WhyNotType(ore)}"); return; }
            // The walkers passing below shoot it too: it has to outlast them to be raided
            drill.NodeMaxHealth = 1e6f;
            drill.NodeCurrentHealth = 1e6f;
            float hp0 = drill.NodeCurrentHealth;
            var raids = ServiceLocator.Get<RaidManager>();
            int drafted = raids.Drafted;
            for (int i = 0; i < 4 && raids.Drafted == drafted; i++)
            {
                wm.RequestNextWave();
                await ctx.WaitUntil(() => raids.Drafted > drafted || !wm.WaveActive, 30f);
            }
            // A raider with a gun shoots the drill from the floor; one without climbs to it: take
            // its gun, make it last, and keep BIT out of it
            var raider = Roster.Enemies(ctx.Tree).FirstOrDefault(e => GodotObject.IsInstanceValid(e) && e.IsRaider);
            if (raider == null) { ctx.Assert(false, "canyon/raider_climbs_to_drill", $"{raids.Drafted - drafted} drafted, none on the field"); return; }
            raider.AttackRange = 0f;
            raider.SetChaosHP(1e6f);
            var bitNode = ServiceLocator.Get<VinePlayer>();
            if (bitNode != null) bitNode.ProcessMode = Node.ProcessModeEnum.Disabled;
            Engine.TimeScale = 3.0;
            bool climbed = false, hit = false;
            float minFlat = 999f, hpAtReach = -1f;
            try
            {
                await ctx.WaitUntil(() =>
                {
                    if (!GodotObject.IsInstanceValid(raider) || !raider.IsAlive || !GodotObject.IsInstanceValid(drill)) return true;
                    var at = raider.GlobalPosition;
                    if (grid.IsClimbable(grid.WorldToGrid(at))) climbed = true;
                    float flat = new Vector2(at.X - drill.GlobalPosition.X, at.Z - drill.GlobalPosition.Z).Length();
                    minFlat = Mathf.Min(minFlat, flat);
                    // Its own blows: the drill loses health once the raider is up beside it
                    if (flat < 2.3f && hpAtReach < 0f) hpAtReach = drill.NodeCurrentHealth;
                    hit = hpAtReach >= 0f && (drill.IsDestroyed || drill.NodeCurrentHealth < hpAtReach);
                    return climbed && hit;
                }, 60f);
            }
            finally
            {
                Engine.TimeScale = 1.0;
                if (bitNode != null) bitNode.ProcessMode = Node.ProcessModeEnum.Inherit;
            }
            string where = !GodotObject.IsInstanceValid(raider) ? "the raider is gone"
                : $"{raider.Faction} {(raider.IsAlive ? "alive" : "dead")}, raiding {raider.IsRaider}, at {grid.WorldToGrid(raider.GlobalPosition)} ({grid.GetCell(grid.WorldToGrid(raider.GlobalPosition))}), "
                  + $"closest {minFlat:0.0} flat to the drill at {ore}";
            ctx.Assert(climbed && hit, "canyon/raider_climbs_to_drill", $"climbed {climbed}, hit it {hit}; {where}");
        }

        private static IEnumerable<Vector2I> Ledges(VineGrid grid)
        {
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.GetCell(x, y) == VineCellType.Ledge) yield return new Vector2I(x, y);
        }
    }
}
