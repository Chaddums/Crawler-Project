using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Probe (not in "all"): on a canyon with every route open and no towers, sends tough walkers
    /// of each kind down every route and logs every stall (under 0.6 moved in 4 game seconds) and
    /// every walker the game despawned as stuck. STALL_SITE picks the site (default p1_ng_relay).
    /// </summary>
    public class StallProbeSuite : ITestSuite
    {
        public string SuiteName => "stallprobe";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            gm.SuppressAutoDebrief = true;
            gm.CurrentTerritorySectionId = OS.GetEnvironment("STALL_SITE") is { Length: > 0 } s ? s : "p1_ng_relay";
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            ctx.StartTest();
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 15f);
            ctx.Assert(inBuild, "stall/battle_loads");
            if (!inBuild) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var walls = ServiceLocator.Get<ShieldWallManager>();
            foreach (var d in walls.StandingWalls) walls.BreakWall(d);
            await ctx.Wait(0.5f);
            var parent = ServiceLocator.Get<VinePlayer>().GetParent();
            var exit = grid.GridToWorld(grid.ExitPoint);
            var kinds = new (string name, VineEnemyFaction f, float speed, bool boss)[]
            {
                ("Scrap Rat", VineEnemyFaction.Scavenger, 2.7f, false),
                ("Rust Hulk", VineEnemyFaction.Brute, 1.6f, false),
                ("Buzz Drone", VineEnemyFaction.Swarm, 4.5f, false),
                ("Forge Overseer", VineEnemyFaction.Brute, 1.1f, true),
            };
            var rng = new RandomNumberGenerator { Seed = 7 };
            var alive = new List<VineEnemy>();
            var ours = new HashSet<VineEnemy>();
            var lastMove = new Dictionary<VineEnemy, (Vector3 at, ulong t)>();
            var stalls = new List<string>();
            int reached = 0;
            Engine.TimeScale = 3.0;
            try
            {
                for (int round = 0; round < 3; round++)
                {
                    foreach (var region in grid.ActiveEntryRegions)
                        foreach (var k in kinds)
                        {
                            var cell = round == 0 ? region.Center : region.GetRandomSpawnCell(rng);
                            var e = new VineEnemy();
                            parent.AddChild(e);
                            e.Initialize(k.name, k.f, 1e6f, k.speed, 0, new Color(0.8f, 0.3f, 0.3f), cell, k.boss);
                            var w = grid.GridToWorld(cell);
                            var mid = new Vector3(grid.Width * Constants.VINE_CELL_SIZE / 2f, 0, grid.Height * Constants.VINE_CELL_SIZE / 2f);
                            var p = w - (mid - w).Normalized() * Constants.VINE_SPAWN_OFFSET;
                            e.GlobalPosition = new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
                            alive.Add(e);
                            ours.Add(e);
                        }
                    ulong t0 = Time.GetTicksMsec();
                    while (Time.GetTicksMsec() - t0 < 50000 && alive.Any(GodotObject.IsInstanceValid))
                    {
                        await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.PhysicsFrame);
                        ulong now = Time.GetTicksMsec();
                        foreach (var e in alive.ToList())
                        {
                            if (!GodotObject.IsInstanceValid(e)) continue;
                            var at = e.GlobalPosition;
                            if (new Vector2(at.X - exit.X, at.Z - exit.Z).Length() < 6f) { reached++; alive.Remove(e); e.QueueFree(); continue; }
                            if (!e.OnGrid) continue;
                            if (!lastMove.TryGetValue(e, out var lm) || new Vector2(lm.at.X - at.X, lm.at.Z - at.Z).Length() > 0.6f) { lastMove[e] = (at, now); continue; }
                            if (now - lm.t > 4000 / 3)
                            {
                                var c = grid.WorldToGrid(at);
                                stalls.Add($"{e.EnemyName} at {c} {grid.GetCell(c)} world ({at.X:0.0}, {at.Z:0.0})");
                                lastMove[e] = (at, now);
                            }
                        }
                    }
                }
            }
            finally { Engine.TimeScale = 1.0; }
            int despawned = alive.Count(e => !GodotObject.IsInstanceValid(e));
            int left = alive.Count(GodotObject.IsInstanceValid);
            foreach (var g in stalls.GroupBy(x => x).OrderByDescending(g => g.Count())) GD.Print($"[StallProbe] x{g.Count()} {g.Key}");
            ctx.StartTest();
            ctx.Assert(stalls.Count == 0 && despawned == 0 && left == 0, "stall/every_route_walks_clean",
                $"{ours.Count} walkers over {grid.ActiveEntryRegions.Count} routes: {reached} reached the Spire, {despawned} despawned as stuck, {left} still out, {stalls.Count} stalls");
            foreach (var e in alive) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            gm.SuppressAutoDebrief = false;
        }
    }
}
