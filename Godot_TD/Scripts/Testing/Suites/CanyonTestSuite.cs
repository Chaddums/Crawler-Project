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

            // A route that opens draws its path lines at once, in the build phase and when its
            // enemies come (playtest: "the enemy path lines don't update when enemies come from
            // new locations")
            ctx.StartTest();
            {
                var preview = ctx.Tree.CurrentScene.FindChild("PathPreview", true, false) as PathPreview;
                var walls = ServiceLocator.TryGet<ShieldWallManager>(out var swm) ? swm : null;
                int before = preview?.RoutesDrawn ?? -1, open0 = grid.ActiveEntryRegions.Count;
                CardinalDirection? shut = walls != null && walls.StandingWalls.Count > 0 ? walls.StandingWalls[0] : null;
                if (preview != null && walls != null && shut != null)
                {
                    walls.BreakWall(shut.Value);
                    await ctx.Wait(0.3f);
                }
                int after = preview?.RoutesDrawn ?? -1;
                int fresh = preview?.FreshRoutes ?? 0;
                // And during a wave, with enemies on the field
                int mid0 = after, mid1 = -1;
                CardinalDirection? next = walls != null && walls.StandingWalls.Count > 0 ? walls.StandingWalls[0] : null;
                if (preview != null && walls != null && next != null && ServiceLocator.TryGet<VineWaveManager>(out var wmr))
                {
                    wmr.StartWave();
                    await ctx.Wait(1.5f);
                    walls.BreakWall(next.Value);
                    await ctx.Wait(0.3f);
                    mid1 = preview.RoutesDrawn;
                }
                ctx.Assert(preview != null && shut != null && grid.ActiveEntryRegions.Count >= open0 + 1 && after == before + 1 && fresh >= 1
                    && (next == null || mid1 == mid0 + 1), "canyon/new_route_draws_its_path",
                    $"routes drawn {before} -> {after} in the build, {mid0} -> {mid1} in a wave, {fresh} drawn as new, entries open {open0} -> {grid.ActiveEntryRegions.Count}, broke {shut?.ToString() ?? "nothing"} then {next?.ToString() ?? "nothing"}");
                // Back to the build phase for what follows
                foreach (var e in Roster.Enemies(ctx.Tree).ToList()) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
                await ctx.WaitForPhase(GamePhase.Build, 10f);
            }

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

            // ── Raiders and what stands in their way (2026-11: on Buffer Overflow a raider climbed to
            // a wall top with a tower between it and its drill and stood there for good, out of
            // every gun's sight, so the wave never ended) ──
            foreach (var e in Roster.Enemies(ctx.Tree).ToList()) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            if (wm.WaveActive) await ctx.WaitUntil(() => !wm.WaveActive, 20f);
            grid.RemoveNode(ore);
            await ctx.Wait(0.3f);
            // A wall top with wall top all round it, and a floor cell to start from
            Vector2I inner = new(-1, -1);
            foreach (var c in Ledges(grid).OrderBy(c => (c - grid.ExitPoint).LengthSquared()))
            {
                bool allLedge = true;
                for (int dx = -1; dx <= 1 && allLedge; dx++)
                    for (int dy = -1; dy <= 1 && allLedge; dy++)
                        if (grid.GetCell(c.X + dx, c.Y + dy) != VineCellType.Ledge || grid.GetNode(new Vector2I(c.X + dx, c.Y + dy)) != null) allLedge = false;
                if (allLedge && (c - grid.ExitPoint).Length() > 9f) { inner = c; break; }
            }
            ctx.StartTest();
            if (inner.X < 0) { ctx.Assert(false, "raid/gets_round_a_tower", "no wall top with wall top all round it"); return; }
            Vector2I floor = new(-1, -1);
            float nearest = float.MaxValue;
            for (int fx = 0; fx < grid.Width; fx++)
                for (int fy = 0; fy < grid.Height; fy++)
                {
                    var c = new Vector2I(fx, fy);
                    if (grid.GetCell(c) != VineCellType.Empty || !grid.IsWalkable(c)) continue;
                    float d = (c - inner).LengthSquared();
                    if (d < nearest) { nearest = d; floor = c; }
                }
            var drill2 = placer.TestPlaceAt(VineNodeType.SalvageDrill, inner);
            if (drill2 == null) { ctx.Assert(false, "raid/gets_round_a_tower", $"no drill at {inner}: {placer.WhyNotType(inner)}"); return; }
            drill2.NodeMaxHealth = 1e6f;
            drill2.NodeCurrentHealth = 1e6f;
            // A tower on the straight line from the floor to the drill
            var toFloor = new Vector2(floor.X - inner.X, floor.Y - inner.Y).Normalized();
            var block = inner + new Vector2I(Mathf.RoundToInt(toFloor.X), Mathf.RoundToInt(toFloor.Y));
            var blocker = placer.TestPlaceAt(VineNodeType.BuffEmitter, block);
            VineEnemy Raider()
            {
                var e = new VineEnemy();
                ServiceLocator.Get<VinePlayer>().GetParent().AddChild(e);
                e.Initialize("Strike Raider", VineEnemyFaction.Brute, 1e6f, 3.2f, 0, new Color(0.8f, 0.3f, 0.3f), floor);
                var w = grid.GridToWorld(floor);
                e.GlobalPosition = new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z), w.Z);
                e.AttackRange = 0f;
                raids.TestDraft(e);
                return e;
            }
            if (bitNode != null) bitNode.ProcessMode = Node.ProcessModeEnum.Disabled;
            Engine.TimeScale = 3.0;
            try
            {
                int gaveUp0 = raids.GaveUp;
                var r1 = Raider();
                float d0 = drill2.NodeCurrentHealth;
                bool hurt = await ctx.WaitUntil(() => !GodotObject.IsInstanceValid(r1) || drill2.NodeCurrentHealth < d0, 60f) && GodotObject.IsInstanceValid(r1);
                // (and it never gave up on the way: measured by distance to the drill, raiders on a
                // winding floor gave up seconds in, 50 units out)
                ctx.Assert(blocker != null && hurt && drill2.NodeCurrentHealth < d0 && raids.GaveUp == gaveUp0, "raid/gets_round_a_tower",
                    $"drill at {inner}, a tower in the way at {block}, from the floor at {floor}: {(GodotObject.IsInstanceValid(r1) ? raids.Describe(r1) : "the raider is gone")}");

                // With the drills gone it walks back down to the floor and on (it used to be let go
                // where it stood, up on the wall, and despawned as stuck four seconds later)
                ctx.StartTest();
                int stuck0 = VineEnemy.StuckDespawns;
                grid.RemoveNode(inner);
                bool down = GodotObject.IsInstanceValid(r1) && await ctx.WaitUntil(() => !GodotObject.IsInstanceValid(r1) || (!r1.IsRaider && grid.IsWalkable(grid.WorldToGrid(r1.GlobalPosition))), 60f);
                ctx.Assert(down && GodotObject.IsInstanceValid(r1) && r1.IsAlive && VineEnemy.StuckDespawns == stuck0, "raid/walks_back_down",
                    GodotObject.IsInstanceValid(r1) ? $"at {grid.WorldToGrid(r1.GlobalPosition)} ({grid.GetCell(grid.WorldToGrid(r1.GlobalPosition))}), raiding {r1.IsRaider}: {raids.Describe(r1)}" : $"gone; stuck despawns {VineEnemy.StuckDespawns - stuck0}");
                if (GodotObject.IsInstanceValid(r1)) r1.QueueFree();

                // A drill walled in by towers isn't safe: the raider wrecks at the towers round it
                ctx.StartTest();
                var drill3 = placer.TestPlaceAt(VineNodeType.SalvageDrill, inner);
                var guards = new List<VineNode>();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var c = inner + new Vector2I(dx, dy);
                        if (c == inner) continue;
                        var g = grid.GetNode(c) ?? placer.TestPlaceAt(VineNodeType.BuffEmitter, c);
                        if (g != null) { g.NodeMaxHealth = 1e6f; g.NodeCurrentHealth = 1e6f; guards.Add(g); }
                    }
                if (drill3 != null) { drill3.NodeMaxHealth = 1e6f; drill3.NodeCurrentHealth = 1e6f; }
                await ctx.Wait(0.2f);
                var r2 = Raider();
                bool guardHurt = drill3 != null && guards.Count == 8 && await ctx.WaitUntil(() => !GodotObject.IsInstanceValid(r2) || guards.Any(g => g.NodeCurrentHealth < 1e6f - 1f), 60f);
                ctx.Assert(guardHurt && guards.Any(g => g.NodeCurrentHealth < 1e6f - 1f) && drill3.NodeCurrentHealth >= 1e6f - 1f, "raid/walled_in_drill_costs_its_guards",
                    $"{guards.Count} towers round the drill at {inner}: {(GodotObject.IsInstanceValid(r2) ? raids.Describe(r2) : "the raider is gone")}");

                // And a raider that can't get anywhere gives up and comes down, so the wave can end
                ctx.StartTest();
                foreach (var g in guards) { g.NodeMaxHealth = 100f; g.NodeCurrentHealth = 0.01f; g.TakeDamage(5f, TowerLoss.Raid); }
                int gave0 = raids.GaveUp;
                bool left = GodotObject.IsInstanceValid(r2) && await ctx.WaitUntil(() => !GodotObject.IsInstanceValid(r2) || !r2.IsRaider, 90f);
                ctx.Assert(left && guards.All(g => g.IsDestroyed) && GodotObject.IsInstanceValid(r2) && !r2.IsRaider && grid.IsWalkable(grid.WorldToGrid(r2.GlobalPosition)),
                    "raid/nothing_left_to_hit_it_leaves", GodotObject.IsInstanceValid(r2) ? $"at {grid.WorldToGrid(r2.GlobalPosition)} ({grid.GetCell(grid.WorldToGrid(r2.GlobalPosition))}), raiding {r2.IsRaider}, gave up {raids.GaveUp - gave0}: {raids.Describe(r2)}" : "gone");
                if (GodotObject.IsInstanceValid(r2)) r2.QueueFree();
            }
            finally
            {
                Engine.TimeScale = 1.0;
                if (bitNode != null) bitNode.ProcessMode = Node.ProcessModeEnum.Inherit;
            }

            // The way a raider takes: floor first, then up; none where there is none
            ctx.StartTest();
            {
                // (the wrecks round the drill are cleared first: a wreck still stands in the way)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if ((dx != 0 || dy != 0) && grid.GetNode(inner + new Vector2I(dx, dy)) != null) grid.RemoveNode(inner + new Vector2I(dx, dy));
                var up = RaidManager.ClimbPath(grid, floor, c => c == inner + new Vector2I(1, 0) || c == inner + new Vector2I(-1, 0), null);
                bool joined = up != null && up.Count > 1;
                for (int i = 1; joined && i < up.Count; i++)
                    joined = (up[i] - up[i - 1]).LengthSquared() == 1 && (grid.IsWalkable(up[i]) || grid.IsClimbable(up[i]));
                var nowhere = RaidManager.ClimbPath(grid, floor, c => false, null);
                ctx.Assert(joined && nowhere == null, "raid/path_goes_over_the_wall", $"{up?.Count ?? 0} cells from {floor} to beside {inner}; a goal that isn't there gives {(nowhere == null ? "none" : "a path")}");
            }
        }

        private static IEnumerable<Vector2I> Ledges(VineGrid grid)
        {
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.GetCell(x, y) == VineCellType.Ledge) yield return new Vector2I(x, y);
        }
    }
}
