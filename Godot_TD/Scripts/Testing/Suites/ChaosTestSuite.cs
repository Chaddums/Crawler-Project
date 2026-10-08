using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// AXIS chaos as a player meets it mid-wave: every enemy on the field goes berserk (tougher,
    /// faster), leaves the path and hunts BIT or the towers, AXIS drops reinforcements away from
    /// the Spire that hold the wave open, things get hurt, and when it ends the survivors head
    /// for the Spire again. Headless.
    /// </summary>
    public class ChaosTestSuite : ITestSuite
    {
        public string SuiteName => "chaos";

        public async Task Run(TestContext ctx)
        {
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "chaos/battle_loads");
            if (!ok) return;
            var tree = ctx.Tree;
            var grid = ServiceLocator.Get<VineGrid>();
            ServiceLocator.TryGet<VineWaveManager>(out var waves);
            ServiceLocator.TryGet<VinePlayer>(out var player);
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            var chaos = All<CorruptionManager>(tree.CurrentScene).FirstOrDefault();
            ctx.StartTest();
            ctx.Assert(waves != null && player != null && chaos != null && pf != null, "chaos/systems_exist");
            if (waves == null || player == null || chaos == null || pf == null) return;
            waves.PauseAutoStart = true;
            grid.Harvester?.IncreaseMaxHP(100000f);
            player.MaxHP = player.CurrentHP = 1e6f;

            // Towers along the path, BIT off to one side of it
            var region = grid.ActiveEntryRegions.First();
            var path = pf.GetCachedPath(region.Center);
            var towers = new List<VineNode>();
            for (int i = 4; i < path.Count - 4 && towers.Count < 4; i += 4)
                foreach (var off in new[] { new Vector2I(2, 0), new Vector2I(-2, 0), new Vector2I(0, 2), new Vector2I(0, -2) })
                {
                    var c = path[i] + off;
                    if (!grid.CanPlace(c) || pf.WouldBlockAllPaths(c)) continue;
                    var n = new VineNode();
                    n.Initialize(VineNodeRegistry.Get(VineNodeType.BarrierWall)); // takes hits, shoots nobody
                    if (grid.PlaceNode(n, c)) { towers.Add(n); break; }
                    n.QueueFree();
                }
            var bitCell = path[path.Count / 2] + new Vector2I(0, 4);
            if (!grid.IsWalkable(bitCell)) bitCell = path[path.Count / 2];
            var bp = grid.GridToWorld(bitCell);
            player.GlobalPosition = new Vector3(bp.X, grid.GetWorldHeight(bp.X, bp.Z), bp.Z);
            player.SetPhysicsProcess(false); // BIT stands still and doesn't shoot back
            player.SetProcess(false);

            // A wave, until enough enemies are on the grid
            waves.StartWave();
            bool enough = await ctx.WaitUntil(() => Enemies(tree).Count(e => e.OnGrid) >= 5, 40f);
            ctx.StartTest();
            ctx.Assert(enough, "chaos/enemies_on_field", $"{Enemies(tree).Count(e => e.OnGrid)} on the grid");
            if (!enough) return;
            await ctx.Wait(0.5f);

            var before = Enemies(tree).Where(e => e.OnGrid).ToDictionary(e => e, e => (hp: e.MaxHealth, speed: e.SpeedMultiplier));
            var pathCells = new HashSet<Vector2I>(path);
            float towerHp0 = towers.Sum(t => t.NodeCurrentHealth);
            int alive0 = Enemies(tree).Count;
            chaos.DebugTriggerChaos();
            await ctx.Wait(0.2f);

            // 1. Berserk
            ctx.StartTest();
            var buffed = before.Keys.Where(e => GodotObject.IsInstanceValid(e) && e.IsAlive).ToList();
            // Tougher all; faster unless it's a Spire rusher (they keep their pace on the path)
            int tougher = buffed.Count(e => e.MaxHealth >= before[e].hp * (CorruptionManager.CHAOS_HP_MULT - 0.02f)
                && (chaos.RoleOf(e) == CorruptionManager.Hunt.Spire || e.SpeedMultiplier >= before[e].speed * (CorruptionManager.CHAOS_SPEED_MULT - 0.02f)));
            ctx.Assert(buffed.Count > 0 && tougher == buffed.Count, "chaos/enemies_go_berserk",
                $"{tougher}/{buffed.Count} tougher and faster");

            // 2. Every role is in play
            ctx.StartTest();
            var roles = Enemies(tree).Select(e => chaos.RoleOf(e)).Where(r => r != null).ToList();
            ctx.Assert(roles.Contains(CorruptionManager.Hunt.Bit) && roles.Contains(CorruptionManager.Hunt.Tower) && roles.Contains(CorruptionManager.Hunt.Spire),
                "chaos/hunters_wreckers_rushers", $"roles: {string.Join(", ", roles.GroupBy(r => r).Select(g => $"{g.Key} {g.Count()}"))}");

            // 3. Reinforcements dropped at once, away from the Spire, and they keep the wave open
            ctx.StartTest();
            int dropped = chaos.ReinforcementsDropped;
            var spire = grid.GridToWorld(grid.ExitPoint);
            float half = Mathf.Max(grid.Width, grid.Height) * 0.35f * Constants.VINE_CELL_SIZE;
            var drops = Enemies(tree).Where(e => !before.ContainsKey(e) && e.OnGrid).ToList();
            ctx.Assert(dropped >= 2 && Enemies(tree).Count >= alive0 + 2, "chaos/reinforcements_drop",
                $"{dropped} dropped, {Enemies(tree).Count} enemies (was {alive0})");
            ctx.Assert(drops.Count > 0 && drops.All(e => Flat(e.GlobalPosition, spire) > half * 0.9f), "chaos/drops_land_away_from_spire",
                $"closest drop {(drops.Count > 0 ? drops.Min(e => Flat(e.GlobalPosition, spire)) : 0):F1} from the Spire (want > {half * 0.9f:F1})");

            // 4. Hunters close on BIT, wreckers on towers; enemies leave the path
            var hunters = Enemies(tree).Where(e => chaos.RoleOf(e) == CorruptionManager.Hunt.Bit && e.Faction != VineEnemyFaction.Ghost).ToList();
            var d0 = hunters.ToDictionary(e => e, e => Flat(e.GlobalPosition, player.GlobalPosition));
            int offPathMax = 0;
            for (int i = 0; i < 16; i++)
            {
                await ctx.Wait(0.5f);
                offPathMax = Mathf.Max(offPathMax, Enemies(tree).Count(e => e.OnGrid && OffPath(grid, pathCells, e.GlobalPosition) >= 3));
            }
            ctx.StartTest();
            var still = hunters.Where(e => GodotObject.IsInstanceValid(e) && e.IsAlive).ToList();
            int closer = still.Count(e => Flat(e.GlobalPosition, player.GlobalPosition) < d0[e] - 1.5f || Flat(e.GlobalPosition, player.GlobalPosition) < 3f);
            ctx.Assert(still.Count > 0 && closer >= (still.Count + 1) / 2, "chaos/hunters_close_on_bit",
                $"{closer}/{still.Count} BIT hunters got closer");
            ctx.Assert(offPathMax >= 2, "chaos/enemies_leave_the_path", $"at most {offPathMax} enemies 3+ cells off the path");
            float towerLost = towerHp0 - towers.Where(t => GodotObject.IsInstanceValid(t)).Sum(t => t.NodeCurrentHealth);
            float bitLost = player.MaxHP - player.CurrentHP;
            ctx.Assert(towerLost > 0f || bitLost > 0f, "chaos/things_get_hurt", $"towers lost {towerLost:F0} HP, BIT lost {bitLost:F0}");

            // 5. More drops come
            await ctx.Wait(CorruptionManager.CHAOS_DROP_INTERVAL - 7f);
            ctx.StartTest();
            ctx.Assert(chaos.ReinforcementsDropped > dropped, "chaos/more_drops", $"{chaos.ReinforcementsDropped} after the second drop (first {dropped})");
            ctx.Assert(waves.WaveActive, "chaos/wave_held_open");

            // 6. It ends: back to normal stats, and on to the Spire
            chaos.DebugEndChaos();
            await ctx.Wait(0.3f);
            var rest = Enemies(tree).Where(e => e.OnGrid).ToList();
            ctx.StartTest();
            ctx.Assert(rest.All(e => !e.IsWandering), "chaos/ends_back_to_path", $"{rest.Count(e => e.IsWandering)} still hunting");
            var exitD = rest.ToDictionary(e => e, e => Flat(e.GlobalPosition, spire));
            await ctx.Wait(3f);
            var moving = rest.Where(e => GodotObject.IsInstanceValid(e) && e.IsAlive).ToList();
            int towardSpire = moving.Count(e => Flat(e.GlobalPosition, spire) < exitD[e] - 0.5f);
            ctx.Assert(moving.Count == 0 || towardSpire >= moving.Count * 0.6f, "chaos/survivors_head_for_spire",
                $"{towardSpire}/{moving.Count} closer to the Spire after 3 s");

            player.SetPhysicsProcess(true);
            player.SetProcess(true);
            foreach (var e in Enemies(tree)) e.QueueFree();
            waves.PauseAutoStart = false;
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

        /// <summary>Cells from the nearest path cell (Chebyshev), capped at 6.</summary>
        private static int OffPath(VineGrid grid, HashSet<Vector2I> path, Vector3 pos)
        {
            var c = grid.WorldToGrid(pos);
            for (int r = 0; r <= 6; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == r && path.Contains(c + new Vector2I(dx, dy))) return r;
            return 6;
        }

        private static List<VineEnemy> Enemies(SceneTree tree) =>
            tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).OfType<VineEnemy>().Where(e => GodotObject.IsInstanceValid(e) && e.IsAlive).ToList();

        private static IEnumerable<T> All<T>(Node root) where T : class
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            foreach (var child in root.GetChildren())
                foreach (var x in All<T>(child)) yield return x;
        }
    }
}
