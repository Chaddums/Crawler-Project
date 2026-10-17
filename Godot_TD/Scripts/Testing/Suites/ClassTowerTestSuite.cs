using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The class towers (ClassTowers.cs): every class builds the shared four and five of its own;
    /// each class tower has a sheet, an upgrade plan and build-bar text, and does what it says:
    /// the Mortar Pit's shells land on what's far and never on what's close, the Rail Lance's
    /// slug goes through a line, the Autocannon Nest takes two targets, a Prism passes a Tesla
    /// Coil's arc on, the Signal Jammer drains shields and slows, the Storm Capacitor charges and
    /// dumps it, the Lens ramps on a held target, the Gravity Well pulls a pack together, the
    /// Beacon's mark makes hits land harder; on a canyon the Slag Chute needs a lip and pours a
    /// pool that burns, and the Barricade stands on the floor, keeps the route and stops walkers.
    /// </summary>
    public class ClassTowerTestSuite : ITestSuite
    {
        public string SuiteName => "classtowers";

        private static readonly VineNodeType[] Shared = { VineNodeType.DamageTower, VineNodeType.FlakBattery, VineNodeType.TeslaCoil, VineNodeType.SalvageDrill };
        internal static readonly VineNodeType[] Class =
        {
            VineNodeType.MortarPit, VineNodeType.RailLance, VineNodeType.SlagChute, VineNodeType.AutocannonNest,
            VineNodeType.ArcPrism, VineNodeType.SignalJammer, VineNodeType.StormCapacitor, VineNodeType.Lens,
            VineNodeType.GravityWell, VineNodeType.Beacon,
        };

        private Node _scene;
        private VineGrid _grid;
        private readonly List<VineEnemy> _dummies = new();

        public async Task Run(TestContext ctx)
        {
            Rosters(ctx);
            Content(ctx);

            // The mechanics on an open map (targets on the towers' own ground)
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            VineBattleScene.MapOverride = "crossroads";
            bool ok;
            try { ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site); }
            finally { VineBattleScene.MapOverride = null; }
            ctx.Assert(ok, "classtowers/open_battle_loads");
            if (!ok) return;
            _scene = ctx.Tree.CurrentScene;
            _grid = ServiceLocator.Get<VineGrid>();
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            // Nothing else may hurt the targets: BIT and the Spire's guns stand still
            var bit = ServiceLocator.Get<VinePlayer>();
            if (bit != null) bit.ProcessMode = Node.ProcessModeEnum.Disabled;
            var spire = _grid.Harvester;
            if (spire != null) spire.ProcessMode = Node.ProcessModeEnum.Disabled;
            var row = TowerSheetSuite.FindRow(_grid, 13);
            ctx.Assert(row.Count >= 13, "classtowers/row_found", $"{row.Count} cells");
            if (row.Count < 13) return;
            Engine.TimeScale = 3.0;
            try
            {
                await Mortar(ctx, row);
                await Rail(ctx, row);
                await Nest(ctx, row);
                await Prism(ctx, row);
                await Jammer(ctx, row);
                await Capacitor(ctx, row);
                await Lens(ctx, row);
                await Well(ctx, row);
                await Beacon(ctx, row);
            }
            finally
            {
                Engine.TimeScale = 1.0;
                ClearDummies();
                if (bit != null && GodotObject.IsInstanceValid(bit)) bit.ProcessMode = Node.ProcessModeEnum.Inherit;
                if (spire != null && GodotObject.IsInstanceValid(spire)) spire.ProcessMode = Node.ProcessModeEnum.Inherit;
            }

            // The canyon's own: the Slag Chute's lip and the Barricade on the floor
            await Canyon(ctx);
        }

        private static void Rosters(TestContext ctx)
        {
            ctx.StartTest();
            var notes = new List<string>();
            var owners = new Dictionary<VineNodeType, string>();
            foreach (var role in new[] { "Bruteforge", "Arcanist", "Obelisk" })
            {
                var nodes = SpireData.Get(role)?.Nodes ?? System.Array.Empty<VineNodeType>();
                foreach (var s in Shared) if (!nodes.Contains(s)) notes.Add($"{role} lacks {s}");
                var own = nodes.Where(n => !Shared.Contains(n)).ToList();
                if (own.Count != 5) notes.Add($"{role} has {own.Count} of its own");
                foreach (var n in own)
                {
                    if (owners.TryGetValue(n, out var other)) notes.Add($"{n} in both {other} and {role}");
                    owners[n] = role;
                }
            }
            foreach (var t in Class) if (!owners.ContainsKey(t)) notes.Add($"{t} on no build bar");
            ctx.Assert(notes.Count == 0, "classtowers/rosters", notes.Count == 0 ? "shared four and five each" : string.Join("; ", notes));
        }

        private static void Content(TestContext ctx)
        {
            foreach (var t in Class)
            {
                ctx.StartTest();
                var data = VineNodeRegistry.Get(t);
                var plan = data != null ? TowerUpgradePlan.For(data.Id) : null;
                var info = TowerInfo.Get(t);
                bool ok = data != null && data.Category == VineNodeCategory.Effect && data.AutoFires && data.ResourceCost > 0
                    && TowerSheet.Load(data.Id) != null && plan != null && plan.Levels.Count == 2 && plan.Branches.Count == 2
                    && !string.IsNullOrEmpty(info.Role) && !string.IsNullOrEmpty(info.Stats);
                ctx.Assert(ok, $"classtowers/{t}/content",
                    data == null ? "not registered" : $"sheet {TowerSheet.Load(data.Id) != null}, plan {plan?.Levels.Count}/{plan?.Branches.Count}, info '{info.Role}'");
            }
        }

        // ── Helpers ──

        private VineNode Place(VineNodeType type, Vector2I cell)
        {
            var n = new VineNode();
            n.Initialize(VineNodeRegistry.Get(type));
            if (_grid.PlaceNode(n, cell)) return n;
            n.QueueFree();
            return null;
        }

        private VineEnemy Dummy(Vector3 at, float hp = 1e5f, EnemyTraits traits = EnemyTraits.None)
        {
            var e = new VineEnemy();
            _scene.AddChild(e);
            e.Initialize("Class tower target", VineEnemyFaction.Scavenger, hp, 0f, 0, new Color(0.8f, 0.4f, 0.3f), _grid.WorldToGrid(at));
            e.GlobalPosition = new Vector3(at.X, _grid.GetWorldHeight(at.X, at.Z), at.Z);
            if (traits != EnemyTraits.None) e.SetTraits(traits);
            _dummies.Add(e);
            return e;
        }

        private void ClearDummies()
        {
            foreach (var e in _dummies) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            _dummies.Clear();
        }

        private Vector3 W(Vector2I c) => _grid.GridToWorld(c);
        private static Vector3 Along(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

        private async Task Done(TestContext ctx, VineNode tower)
        {
            ClearDummies();
            if (tower != null && GodotObject.IsInstanceValid(tower) && !tower.IsDestroyed) _grid.RemoveNode(tower.GridPosition);
            await ctx.Wait(0.2f);
        }

        // ── Bruteforge ──

        private async Task Mortar(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            var m = Place(VineNodeType.MortarPit, row[0]);
            if (m == null) { ctx.Assert(false, "classtowers/mortar_placed"); return; }
            var close = Dummy(W(row[1]));                 // 2 units out: inside its minimum
            var far = Dummy(W(row[8]));                   // 16 out
            await ctx.Wait(5f);
            float hitFar = 1e5f - far.CurrentHealth, hitClose = 1e5f - close.CurrentHealth;
            ctx.Assert(hitFar > 0f && hitClose <= 0f, "classtowers/mortar_long_not_close", $"far took {hitFar:0}, close took {hitClose:0}");
            await Done(ctx, m);
        }

        private async Task Rail(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            var r = Place(VineNodeType.RailLance, row[0]);
            if (r == null) { ctx.Assert(false, "classtowers/rail_placed"); return; }
            var line = new[] { Dummy(W(row[3])), Dummy(W(row[5])), Dummy(W(row[7])) };
            var off = Dummy(W(row[5]) + new Vector3(0, 0, 4f));
            await ctx.Wait(1.5f);
            int hit = line.Count(e => e.CurrentHealth < 1e5f);
            // The nearest of the line is the target; the two behind it take the same slug
            ctx.Assert(hit == 3 && off.CurrentHealth >= 1e5f - 0.01f, "classtowers/rail_through_the_line",
                $"{hit} of 3 on the line hit, off the line took {1e5f - off.CurrentHealth:0}");
            await Done(ctx, r);
        }

        private async Task Nest(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            var n = Place(VineNodeType.AutocannonNest, row[0]);
            if (n == null) { ctx.Assert(false, "classtowers/nest_placed"); return; }
            var a = Dummy(W(row[3]));
            var b = Dummy(W(row[3]) + new Vector3(0, 0, 2.5f));
            await ctx.Wait(2f);
            ctx.Assert(a.CurrentHealth < 1e5f && b.CurrentHealth < 1e5f, "classtowers/nest_two_targets",
                $"first took {1e5f - a.CurrentHealth:0}, second {1e5f - b.CurrentHealth:0}");
            await Done(ctx, n);
        }

        // ── Arcanist ──

        private async Task Prism(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            // Out of the coil's own reach (and its arcs'), in the Prism's
            var coil = Place(VineNodeType.TeslaCoil, row[0]);
            var prism = Place(VineNodeType.ArcPrism, row[3]);
            if (coil == null || prism == null) { ctx.Assert(false, "classtowers/prism_placed"); await Done(ctx, coil); await Done(ctx, prism); return; }
            var nearCoil = Dummy(W(row[2]) + new Vector3(0, 0, 1f));
            var pastIt = Dummy(W(row[8]));
            await ctx.Wait(2f);
            ctx.Assert(nearCoil.CurrentHealth < 1e5f && pastIt.CurrentHealth < 1e5f, "classtowers/prism_passes_the_arc",
                $"by the coil took {1e5f - nearCoil.CurrentHealth:0}, past it (only the Prism reaches) took {1e5f - pastIt.CurrentHealth:0}");
            await Done(ctx, prism);
            await Done(ctx, coil);
        }

        private async Task Jammer(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            var j = Place(VineNodeType.SignalJammer, row[0]);
            if (j == null) { ctx.Assert(false, "classtowers/jammer_placed"); return; }
            var e = Dummy(W(row[2]), 1e5f, EnemyTraits.Shielded);
            await ctx.Wait(0.3f);
            float shield0 = e.ShieldMax;
            await ctx.Wait(1.5f);
            ctx.Assert(shield0 > 0f && e.ShieldHP < shield0 - 5f && e.SlowNow > 0f, "classtowers/jammer_drains_and_slows",
                $"shield {e.ShieldHP:0}/{shield0:0}, slowed {e.SlowNow:P0}");
            await Done(ctx, j);
        }

        private async Task Capacitor(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            var c = Place(VineNodeType.StormCapacitor, row[0]);
            if (c == null) { ctx.Assert(false, "classtowers/capacitor_placed"); return; }
            await ctx.Wait(5f);
            float charged = c.Charge;
            var e = Dummy(W(row[3]));
            await ctx.Wait(0.6f);
            float took = 1e5f - e.CurrentHealth;
            ctx.Assert(charged >= ClassTowers.CAP_MIN && took >= ClassTowers.CAP_DAMAGE * ClassTowers.CAP_MIN * 0.9f && c.Charge < charged,
                "classtowers/capacitor_charges_and_dumps", $"charged {charged:0.0}, the blast took {took:0}, charge after {c.Charge:0.0}");
            await Done(ctx, c);
        }

        private async Task Lens(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            var l = Place(VineNodeType.Lens, row[0]);
            if (l == null) { ctx.Assert(false, "classtowers/lens_placed"); return; }
            var e = Dummy(W(row[4]));
            await ctx.Wait(0.4f);
            float h0 = e.CurrentHealth;
            await ctx.Wait(1f);
            float first = h0 - e.CurrentHealth;
            await ctx.Wait(2f);
            float h1 = e.CurrentHealth;
            await ctx.Wait(1f);
            float later = h1 - e.CurrentHealth;
            ctx.Assert(first > 0f && later > first * 1.4f, "classtowers/lens_ramps", $"a second at first {first:0}, a second later {later:0}");
            await Done(ctx, l);
        }

        // ── Obelisk ──

        private async Task Well(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            var w = Place(VineNodeType.GravityWell, row[6]);
            if (w == null) { ctx.Assert(false, "classtowers/well_placed"); return; }
            var a = Dummy(W(row[3]));
            var b = Dummy(W(row[9]));
            float d0 = new Vector2(a.GlobalPosition.X - b.GlobalPosition.X, a.GlobalPosition.Z - b.GlobalPosition.Z).Length();
            await ctx.Wait(1.5f);
            float d1 = new Vector2(a.GlobalPosition.X - b.GlobalPosition.X, a.GlobalPosition.Z - b.GlobalPosition.Z).Length();
            ctx.Assert(d1 < d0 - 2f, "classtowers/well_pulls_together", $"{d0:0.0} apart, then {d1:0.0}");
            await Done(ctx, w);
        }

        private async Task Beacon(TestContext ctx, List<Vector2I> row)
        {
            ctx.StartTest();
            var b = Place(VineNodeType.Beacon, row[0]);
            if (b == null) { ctx.Assert(false, "classtowers/beacon_placed"); return; }
            var e = Dummy(W(row[3]));
            bool marked = await ctx.WaitUntil(() => e.IsMarked, 4f);
            float h = e.CurrentHealth;
            e.TakeDamage(100f, DamageKind.Heavy);
            float took = h - e.CurrentHealth;
            ctx.Assert(marked && Mathf.Abs(took - 100f * (1f + ClassTowers.BEACON_TAKEN)) < 1f, "classtowers/beacon_marks",
                $"marked {marked}, 100 landed as {took:0.0}");
            await Done(ctx, b);
        }

        // ── On a canyon ──

        private async Task Canyon(TestContext ctx)
        {
            var gm = GameManager.Instance;
            ctx.StartTest();
            gm.CurrentPlanet = 1;
            gm.CurrentTerritorySectionId = "p1_ng_relay";
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = SpireData.Get("Obelisk")?.Nodes;
            gm.StartVineRun();
            await ctx.Wait(1f);
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 15f);
            var grid = ServiceLocator.Get<VineGrid>();
            ctx.Assert(inBuild && grid.IsCanyon, "classtowers/canyon_loads");
            if (!inBuild || !grid.IsCanyon) return;
            gm.AddResources(3000);
            var placer = ServiceLocator.Get<VinePlacer>();
            var pf = ServiceLocator.Get<VinePathfinder>();
            var entry = grid.ActiveEntryRegions[0].Center;
            var route = pf.GetCachedPath(entry) ?? new List<Vector2I>();

            // Slag Chute: refused back from the edge, built on the lip, and its slag burns on the floor
            ctx.StartTest();
            Vector2I? back = null, lip = null;
            for (int x = 0; x < grid.Width && (back == null || lip == null); x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var c = new Vector2I(x, y);
                    if (!grid.CanPlace(c)) continue;
                    if (back == null && VineNode.SlagWhyNot(grid, c) != null) back = c;
                    if (lip == null && VineNode.SlagWhyNot(grid, c) == null && route.Any(r => (r - c).LengthSquared() <= 9)) lip = c;
                }
            var refused = back.HasValue ? placer.TestPlaceAt(VineNodeType.SlagChute, back.Value) : null;
            var chute = lip.HasValue ? placer.TestPlaceAt(VineNodeType.SlagChute, lip.Value) : null;
            ctx.Assert(back.HasValue && refused == null && chute != null, "classtowers/slag_needs_a_lip",
                $"back from the edge at {back} {(refused == null ? "refused" : "built")}, on the lip at {lip} {(chute != null ? "built" : "refused")}");
            if (refused != null) grid.RemoveNode(refused.GridPosition);
            if (chute != null)
            {
                ctx.StartTest();
                // A target on the floor below it, in its reach
                Vector2I? below = null;
                float nearest = ClassTowers.SLAG_RANGE * 0.9f;
                for (int dx = -4; dx <= 4; dx++)
                    for (int dy = -4; dy <= 4; dy++)
                    {
                        var r = chute.GridPosition + new Vector2I(dx, dy);
                        if (!grid.InBounds(r) || !grid.IsWalkable(r) || grid.GetNode(r) != null) continue;
                        var w = grid.GridToWorld(r);
                        float d = new Vector2(w.X - chute.GlobalPosition.X, w.Z - chute.GlobalPosition.Z).Length();
                        if (d <= nearest) { nearest = d; below = r; }
                    }
                _scene = ctx.Tree.CurrentScene;
                _grid = grid;
                var target = below.HasValue ? Dummy(W(below.Value)) : null;
                var bitNow = ServiceLocator.Get<VinePlayer>();
                if (bitNow != null) bitNow.ProcessMode = Node.ProcessModeEnum.Disabled;
                if (grid.Harvester != null) grid.Harvester.ProcessMode = Node.ProcessModeEnum.Disabled;
                Engine.TimeScale = 3.0;
                try { if (target != null) await ctx.Wait(4f); }
                finally
                {
                    Engine.TimeScale = 1.0;
                    if (bitNow != null) bitNow.ProcessMode = Node.ProcessModeEnum.Inherit;
                    if (grid.Harvester != null) grid.Harvester.ProcessMode = Node.ProcessModeEnum.Inherit;
                }
                bool pool = SlagPool.Live > 0;
                float took = target != null && GodotObject.IsInstanceValid(target) ? 1e5f - target.CurrentHealth : 0f;
                ctx.Assert(target != null && took > 0f && pool, "classtowers/slag_burns_the_floor",
                    target == null ? "no floor cell in reach of the chute" : $"the walker below took {took:0}, a pool {(pool ? "lies" : "doesn't lie")} on the floor");
                ClearDummies();
                grid.RemoveNode(chute.GridPosition);
                await ctx.Wait(0.2f);
            }

            // Barricade: on the floor only, the route stays as it was, and a walker stops at it
            ctx.StartTest();
            var spot = route.Skip(4).FirstOrDefault(c => grid.BarricadeSpot(c));
            var onLedge = placer.TestPlaceAt(VineNodeType.BarrierWall, back ?? lip ?? spot);
            if (onLedge != null) grid.RemoveNode(onLedge.GridPosition);
            int before = pf.FlowPath(entry)?.Count ?? 0;
            var bar = grid.BarricadeSpot(spot) ? placer.TestPlaceAt(VineNodeType.BarrierWall, spot) : null;
            await ctx.Wait(0.3f);
            int after = pf.FlowPath(entry)?.Count ?? 0;
            ctx.Assert(onLedge == null && bar != null && bar.OnCanyonFloor && after == before, "classtowers/barricade_on_the_floor",
                $"on a wall top {(onLedge != null ? "built" : "refused")}, on the floor at {spot} {(bar != null ? "built" : "refused")}, route {before} -> {after} cells");
            if (bar == null) return;

            ctx.StartTest();
            // Across the floor: walkers take every lane of a canyon, so one barricade blocks one
            var line = new List<VineNode> { bar };
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var c = spot + new Vector2I(dx, dy);
                    if (c == spot || !grid.BarricadeSpot(c) || grid.GetNode(c) != null) continue;
                    var more = placer.TestPlaceAt(VineNodeType.BarrierWall, c);
                    if (more != null) line.Add(more);
                }
            foreach (var b in line) { b.NodeMaxHealth = 1e6f; b.NodeCurrentHealth = 1e6f; }
            gm.SetCoreLives(999);
            grid.Harvester?.IncreaseMaxHP(100000f);
            var wm = ServiceLocator.Get<VineWaveManager>();
            ServiceLocator.Get<VinePlayer>().ProcessMode = Node.ProcessModeEnum.Disabled;
            Engine.TimeScale = 3.0;
            bool held = false;
            try
            {
                wm.StartWave();
                held = await ctx.WaitUntil(() => Roster.Enemies(ctx.Tree).Any(e => GodotObject.IsInstanceValid(e) && e.AtBarricade), 40f);
                await ctx.Wait(2f);
            }
            finally
            {
                Engine.TimeScale = 1.0;
                ServiceLocator.Get<VinePlayer>().ProcessMode = Node.ProcessModeEnum.Inherit;
            }
            float lost = line.Where(GodotObject.IsInstanceValid).Sum(b => 1e6f - b.NodeCurrentHealth);
            ctx.Assert(held && lost > 0f, "classtowers/barricade_stops_walkers",
                $"a walker held at the {line.Count} barricades {held}, they took {lost:0}");
        }
    }
}
