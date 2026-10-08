using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What each damage tower actually deals in a real battle: one tower beside an open row, a
    /// lone target and a tight group of five, both in range and kept alive, measured over at least
    /// fourteen of the tower's shots. Checks that damage, fire-rate and Overclock Relay boosts
    /// reach every tower, and the intended roles: the Junk Turret is the best single-target buy,
    /// the area towers beat it on groups without running away with it, and none is worthless on a
    /// lone target. Writes test-reports/balance.json and prints the table. Headless-safe.
    /// </summary>
    public class TowerBalanceSuite : ITestSuite
    {
        public string SuiteName => "balance";

        private static readonly VineNodeType[] DamageTowers =
            { VineNodeType.DamageTower, VineNodeType.ScatterCannon, VineNodeType.TeslaCoil, VineNodeType.FlakBattery };

        // Design bands, relative to the Junk Turret's damage a second per Resource
        private const float SingleFloor = 0.3f;     // every tower does at least this on a lone target
        private const float GroupCeiling = 2.6f;    // no area tower more than this on a group

        private const float TimeScale = 4f;

        private sealed class Row
        {
            public VineNodeType Type;
            public int Cost;
            public float Single, Group, DamageBoost, RateBoost, Relay;
            public float SinglePerCost => Single / Cost;
            public float GroupPerCost => Group / Cost;
        }

        public async Task Run(TestContext ctx)
        {
            var sites = FidelityTestSuite.SitesToCheck();
            var site = sites.FirstOrDefault(s => s.Planet == 1) ?? sites.FirstOrDefault();
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "balance/battle_loads");
            if (!ok) return;

            var grid = ServiceLocator.Get<VineGrid>();
            var scene = ctx.Tree.CurrentScene;
            // Nothing else may hurt the targets: no wave, and BIT stands still
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            var bit = All<VinePlayer>(scene).FirstOrDefault();
            if (bit != null) bit.ProcessMode = Node.ProcessModeEnum.Disabled;

            var row = TowerSheetSuite.FindRow(grid, 9);
            ctx.StartTest();
            ctx.Assert(row.Count > 0, "balance/row_found");
            if (row.Count == 0) return;

            var rows = new List<Row>();
            foreach (var type in DamageTowers)
                rows.Add(await Measure(ctx, grid, scene, row, type));
            await CheckPerks(ctx, grid, scene, row);

            Engine.TimeScale = 1.0;
            if (bit != null) bit.ProcessMode = Node.ProcessModeEnum.Inherit;
            if (waves != null) waves.PauseAutoStart = false;

            Report(rows);
            Check(ctx, rows);
            GameManager.Instance.CurrentPlanet = 1;
            GameManager.Instance.CurrentTerritorySectionId = null;
        }

        private static async Task<Row> Measure(TestContext ctx, VineGrid grid, Node scene, List<Vector2I> row, VineNodeType type)
        {
            var data = VineNodeRegistry.Get(type);
            var r = new Row { Type = type, Cost = data.ResourceCost };
            var node = new VineNode();
            node.Initialize(data);
            if (!grid.PlaceNode(node, row[1])) { node.QueueFree(); return r; }

            float interval = type switch
            {
                VineNodeType.ScatterCannon => Constants.SCATTER_CANNON_INTERVAL,
                VineNodeType.TeslaCoil => Constants.TESLA_COIL_INTERVAL,
                VineNodeType.FlakBattery => Constants.FLAK_BATTERY_INTERVAL,
                _ => Constants.TOWER_AUTO_FIRE_INTERVAL,
            };
            float window = Mathf.Max(6f, interval * 14f);
            // Targets three cells down the row: in range of every damage tower
            var centre = grid.GridToWorld(row[4]);

            r.Single = await Dps(ctx, scene, grid, node, centre, 1, window);
            r.Group = await Dps(ctx, scene, grid, node, centre, 5, window);

            // A damage perk (Overclocked Cores is +20%)
            SignalTuningEditor.DamageTowerDPS *= 1.2f;
            r.DamageBoost = await Dps(ctx, scene, grid, node, centre, 1, window) / Mathf.Max(r.Single, 0.001f);
            SignalTuningEditor.DamageTowerDPS /= 1.2f;

            // Fifty percent faster attacks
            node.AttackRateMultiplier = 1.5f;
            r.RateBoost = await Dps(ctx, scene, grid, node, centre, 1, window) / Mathf.Max(r.Single, 0.001f);
            node.AttackRateMultiplier = 1f;

            // An Overclock Relay next door
            var relay = new VineNode();
            relay.Initialize(VineNodeRegistry.Get(VineNodeType.BuffEmitter));
            if (grid.PlaceNode(relay, row[1] + new Vector2I(0, 1)) || grid.PlaceNode(relay, row[1] + new Vector2I(0, -1)))
            {
                await ctx.Wait(1f);
                r.Relay = await Dps(ctx, scene, grid, node, centre, 1, window) / Mathf.Max(r.Single, 0.001f);
                grid.RemoveNode(relay.GridPosition);
            }
            else relay.QueueFree();

            grid.RemoveNode(node.GridPosition);
            await Frames(ctx, 2);
            return r;
        }

        /// <summary>Total damage a second dealt to <paramref name="count"/> targets around <paramref name="centre"/>.
        /// Targets get 100,000 health: at a billion, single-precision health dropped hits of a few points.</summary>
        private static async Task<float> Dps(TestContext ctx, Node scene, VineGrid grid, VineNode tower, Vector3 centre, int count, float window,
            Vector3[] layout = null)
        {
            var offsets = layout ?? new[] { Vector3.Zero, new Vector3(1f, 0, 0), new Vector3(-1f, 0, 0), new Vector3(0, 0, 1f), new Vector3(0, 0, -1f),
                new Vector3(0.9f, 0, 0.9f), new Vector3(-0.9f, 0, 0.9f), new Vector3(0.9f, 0, -0.9f) };
            var targets = new List<VineEnemy>();
            for (int i = 0; i < count; i++)
            {
                var at = centre + offsets[i];
                var e = new VineEnemy();
                scene.AddChild(e);
                e.Initialize("Balance target", VineEnemyFaction.Scavenger, 1e5f, 0f, 0, new Color(0.8f, 0.4f, 0.3f), grid.WorldToGrid(at));
                e.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
                targets.Add(e);
            }
            Engine.TimeScale = 1.0;
            await Frames(ctx, 3);
            Engine.TimeScale = TimeScale;
            await ctx.Wait(0.5f); // let the tower pick them up
            float Hp() => targets.Where(GodotObject.IsInstanceValid).Sum(t => t.CurrentHealth);
            float start = Hp();
            float elapsed = 0f;
            var home = targets.Select(t => t.GlobalPosition).ToList();
            int steps = 0;
            while (elapsed < window)
            {
                // Still targets are despawned as stuck after 4 s: walk each round a small circle,
                // a quarter turn every 1.5 s, so it is never back where it was 4 s earlier
                float step = Mathf.Min(1.5f, window - elapsed);
                await ctx.Wait(step);
                elapsed += step;
                steps++;
                float a = steps * Mathf.Pi / 2f;
                // The targets shoot back: keep the tower standing
                if (GodotObject.IsInstanceValid(tower)) tower.NodeCurrentHealth = tower.NodeMaxHealth;
                for (int i = 0; i < targets.Count; i++)
                    if (GodotObject.IsInstanceValid(targets[i]))
                        targets[i].GlobalPosition = home[i] + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.3f;
            }
            float dealt = start - Hp();
            float spread = targets.Where(GodotObject.IsInstanceValid).Select(t => new Vector2(t.GlobalPosition.X - centre.X, t.GlobalPosition.Z - centre.Z).Length()).DefaultIfEmpty(0f).Max();
            GD.Print($"[Balance] {tower?.Data?.Type} x{count}: {dealt / window:F1}/s over {window:F1}s, targets within {spread:F2} of the centre, tower {(GodotObject.IsInstanceValid(tower) ? tower.NodeCurrentHealth : -1):F0} HP");
            Engine.TimeScale = 1.0;
            foreach (var t in targets.Where(GodotObject.IsInstanceValid)) t.QueueFree();
            await Frames(ctx, 2);
            return dealt / window;
        }

        // ── Tower perks ──

        private static PerkData Perk(string id) => VinePerkRegistry.GetAll().First(p => p.Id == id);

        /// <summary>Damage a second with and without a perk; the ratio.</summary>
        private static async Task<float> WithPerk(TestContext ctx, Node scene, VineGrid grid, VineNode tower, Vector3 centre,
            string perk, int count, float window, Vector3[] layout = null)
        {
            var gm = GameManager.Instance;
            float off = await Dps(ctx, scene, grid, tower, centre, count, window, layout);
            gm.ActivePerks.Add(Perk(perk));
            float on = await Dps(ctx, scene, grid, tower, centre, count, window, layout);
            gm.ActivePerks.RemoveAll(p => p.Id == perk);
            GD.Print($"[Balance] perk {perk}: {off:F1}/s without, {on:F1}/s with");
            return on / Mathf.Max(off, 0.001f);
        }

        private static VineNode Place(VineGrid grid, VineNodeType type, Vector2I cell)
        {
            var node = new VineNode();
            node.Initialize(VineNodeRegistry.Get(type));
            if (grid.PlaceNode(node, cell)) return node;
            node.QueueFree();
            return null;
        }

        private static async Task CheckPerks(TestContext ctx, VineGrid grid, Node scene, List<Vector2I> row)
        {
            var centre = grid.GridToWorld(row[4]);
            var gm = GameManager.Instance;

            // Arc Conductor: the Tesla Coil reaches more of a group, harder
            var tesla = Place(grid, VineNodeType.TeslaCoil, row[1]);
            ctx.StartTest();
            float arc = tesla != null ? await WithPerk(ctx, scene, grid, tesla, centre, "arc_conductor", 5, 11.2f) : 0f;
            ctx.Assert(arc >= 1.35f, "perks/arc_conductor/arcs_further", $"x{arc:F2} into a group of five");
            if (tesla != null) grid.RemoveNode(tesla.GridPosition);

            // Cluster Shells: the whole group takes full damage
            var scatter = Place(grid, VineNodeType.ScatterCannon, row[1]);
            ctx.StartTest();
            float cluster = scatter != null ? await WithPerk(ctx, scene, grid, scatter, centre, "cluster_shells", 5, 16.8f) : 0f;
            ctx.Assert(cluster >= 1.15f, "perks/cluster_shells/wider_full_blasts", $"x{cluster:F2} into a group of five");
            if (scatter != null) grid.RemoveNode(scatter.GridPosition);

            // Saturation Fire: eight targets instead of five
            var flak = Place(grid, VineNodeType.FlakBattery, row[1]);
            ctx.StartTest();
            float sat = flak != null ? await WithPerk(ctx, scene, grid, flak, centre, "saturation_fire", 8, 6f) : 0f;
            ctx.Assert(sat >= 1.4f, "perks/saturation_fire/more_targets", $"x{sat:F2} into a group of eight");
            if (flak != null) grid.RemoveNode(flak.GridPosition);

            // Piercing Rail: two more in a line behind the first take 60%
            var turret = Place(grid, VineNodeType.DamageTower, row[1]);
            var line = new[] { Vector3.Zero, new Vector3(1.5f, 0, 0), new Vector3(3f, 0, 0) };
            ctx.StartTest();
            float pierce = turret != null ? await WithPerk(ctx, scene, grid, turret, centre, "piercing_rail", 3, 7f, line) : 0f;
            ctx.Assert(pierce >= 1.8f, "perks/piercing_rail/punches_through", $"x{pierce:F2} into three in a line");

            // Relay Mesh: a relay two cells away boosts the turret only with the perk
            if (turret != null)
            {
                var relay = Place(grid, VineNodeType.BuffEmitter, row[1] + new Vector2I(0, 2)) ?? Place(grid, VineNodeType.BuffEmitter, row[1] + new Vector2I(0, -2));
                ctx.StartTest();
                float mesh = 0f, plain = 0f;
                if (relay != null)
                {
                    float none = await Dps(ctx, scene, grid, turret, centre, 1, 7f);
                    await ctx.Wait(3.5f); // any buff from before decays
                    plain = await Dps(ctx, scene, grid, turret, centre, 1, 7f) / Mathf.Max(none, 0.001f);
                    gm.ActivePerks.Add(Perk("relay_mesh"));
                    await ctx.Wait(1f);
                    mesh = await Dps(ctx, scene, grid, turret, centre, 1, 7f) / Mathf.Max(none, 0.001f);
                    gm.ActivePerks.RemoveAll(p => p.Id == "relay_mesh");
                    grid.RemoveNode(relay.GridPosition);
                }
                ctx.Assert(relay != null && plain < 1.1f && mesh >= 1.3f, "perks/relay_mesh/reaches_two_cells",
                    $"Relay two cells away: x{plain:F2} without the perk, x{mesh:F2} with");
                grid.RemoveNode(turret.GridPosition);
            }

            // Hydraulic Stun: a shoved enemy stops dead, only with the perk
            var ram = Place(grid, VineNodeType.PushPull, row[1]);
            ctx.StartTest();
            if (ram != null)
            {
                bool plainStun = await ShoveStuns(ctx, scene, grid, ram, row);
                gm.ActivePerks.Add(Perk("hydraulic_stun"));
                bool perkStun = await ShoveStuns(ctx, scene, grid, ram, row);
                gm.ActivePerks.RemoveAll(p => p.Id == "hydraulic_stun");
                ctx.Assert(!plainStun && perkStun, "perks/hydraulic_stun/shove_stops_enemies",
                    $"Stunned after a shove: {plainStun} without the perk, {perkStun} with");
                grid.RemoveNode(ram.GridPosition);
            }

            // Tar Pools: gobs leave pools on the ground that slow, and dry up
            var tar = Place(grid, VineNodeType.SlowField, row[1]);
            ctx.StartTest();
            if (tar != null)
            {
                gm.ActivePerks.Add(Perk("tar_pools"));
                var e = Target(scene, grid, centre);
                Engine.TimeScale = TimeScale;
                TarPool pool = null;
                for (int i = 0; i < 40 && pool == null; i++)
                {
                    await ctx.Wait(0.1f);
                    pool = All<TarPool>(scene).FirstOrDefault(p => p.Visible);
                }
                bool near = pool != null && new Vector2(pool.GlobalPosition.X - centre.X, pool.GlobalPosition.Z - centre.Z).Length() < 1.5f;
                float onGround = pool != null ? Mathf.Abs(pool.GlobalPosition.Y - grid.GetWorldHeight(pool.GlobalPosition.X, pool.GlobalPosition.Z)) : 9f;
                gm.ActivePerks.RemoveAll(p => p.Id == "tar_pools");
                if (GodotObject.IsInstanceValid(e)) e.QueueFree();
                await ctx.Wait(Constants.PERK_TAR_POOL_DURATION + 1f);
                bool dried = !All<TarPool>(scene).Any();
                Engine.TimeScale = 1.0;
                ctx.Assert(pool != null && near && onGround < 0.05f && dried, "perks/tar_pools/pool_lands_and_dries",
                    $"pool {(pool != null ? "laid" : "never laid")}, by the target {near}, {onGround:F2} off the ground, gone after {Constants.PERK_TAR_POOL_DURATION} s {dried}");
                grid.RemoveNode(tar.GridPosition);
            }
            await Frames(ctx, 2);
        }

        private static VineEnemy Target(Node scene, VineGrid grid, Vector3 at, float speed = 0f)
        {
            var e = new VineEnemy();
            scene.AddChild(e);
            e.Initialize("Balance target", VineEnemyFaction.Scavenger, 1e5f, speed, 0, new Color(0.8f, 0.4f, 0.3f), grid.WorldToGrid(at));
            e.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
            return e;
        }

        /// <summary>Put an enemy next to the ram and see whether a shove leaves it stunned.</summary>
        private static async Task<bool> ShoveStuns(TestContext ctx, Node scene, VineGrid grid, VineNode ram, List<Vector2I> row)
        {
            var e = Target(scene, grid, grid.GridToWorld(row[2]));
            bool stunned = false;
            Engine.TimeScale = TimeScale;
            for (int i = 0; i < 40 && !stunned; i++)
            {
                await ctx.Wait(0.1f);
                if (GodotObject.IsInstanceValid(e) && e.IsStunned) stunned = true;
                if (GodotObject.IsInstanceValid(ram)) ram.NodeCurrentHealth = ram.NodeMaxHealth;
            }
            Engine.TimeScale = 1.0;
            if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            await ctx.Wait(3f); // the ram recharges
            return stunned;
        }

        private static void Report(List<Row> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Balance] tower           cost  single  per-cost   group5  per-cost   +20%dmg  x1.5rate  relay");
            foreach (var r in rows)
                sb.AppendLine($"[Balance] {r.Type,-15} {r.Cost,4} {r.Single,7:F1} {r.SinglePerCost,9:F2} {r.Group,8:F1} {r.GroupPerCost,9:F2} {r.DamageBoost,9:F2} {r.RateBoost,9:F2} {r.Relay,6:F2}");
            GD.Print(sb.ToString());
            var json = "[" + string.Join(",", rows.Select(r =>
                $"{{\"type\":\"{r.Type}\",\"cost\":{r.Cost},\"single_dps\":{r.Single:F2},\"single_per_cost\":{r.SinglePerCost:F3}," +
                $"\"group_dps\":{r.Group:F2},\"group_per_cost\":{r.GroupPerCost:F3},\"damage_boost\":{r.DamageBoost:F3}," +
                $"\"rate_boost\":{r.RateBoost:F3},\"relay_boost\":{r.Relay:F3}}}")) + "]";
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://test-reports/balance.json"), json);
        }

        private static void Check(TestContext ctx, List<Row> rows)
        {
            foreach (var r in rows)
            {
                string p = $"balance/{r.Type}";
                ctx.StartTest();
                ctx.Assert(r.Single > 0f, $"{p}/deals_damage", "Dealt nothing to a target in range");
                ctx.Assert(r.DamageBoost is >= 1.12f and <= 1.3f, $"{p}/damage_bonus_applies",
                    $"+20% tower damage gave x{r.DamageBoost:F2}");
                ctx.Assert(r.RateBoost is >= 1.3f and <= 1.7f, $"{p}/fire_rate_applies",
                    $"Attacking 50% faster gave x{r.RateBoost:F2} damage a second");
                ctx.Assert(r.Relay >= 1.3f, $"{p}/relay_applies", $"An Overclock Relay next door gave x{r.Relay:F2}");
            }

            var jt = rows.FirstOrDefault(r => r.Type == VineNodeType.DamageTower);
            if (jt == null || jt.Single <= 0f) return;
            foreach (var r in rows.Where(r => r != jt))
            {
                string p = $"balance/{r.Type}";
                ctx.StartTest();
                ctx.Assert(r.SinglePerCost <= jt.SinglePerCost, $"{p}/turret_is_the_single_target_buy",
                    $"{r.SinglePerCost:F2} a second per Resource on one target beats the Junk Turret's {jt.SinglePerCost:F2}");
                ctx.Assert(r.SinglePerCost >= jt.SinglePerCost * SingleFloor, $"{p}/useful_on_one_target",
                    $"{r.SinglePerCost:F2} per Resource on one target, under {SingleFloor:P0} of the Junk Turret's {jt.SinglePerCost:F2}");
                ctx.Assert(r.GroupPerCost > jt.GroupPerCost, $"{p}/beats_turret_on_groups",
                    $"{r.GroupPerCost:F2} per Resource into five, Junk Turret {jt.GroupPerCost:F2}");
                ctx.Assert(r.GroupPerCost <= jt.GroupPerCost * GroupCeiling, $"{p}/group_damage_in_bounds",
                    $"{r.GroupPerCost:F2} per Resource into five is over {GroupCeiling}x the Junk Turret's {jt.GroupPerCost:F2}");
            }
        }

        private static IEnumerable<T> All<T>(Node root) where T : class
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            foreach (var child in root.GetChildren())
                foreach (var x in All<T>(child)) yield return x;
        }

        private static async Task Frames(TestContext ctx, int n)
        {
            for (int i = 0; i < n; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }
    }
}
