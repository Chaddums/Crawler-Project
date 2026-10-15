using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>A strategy that reads its run's config (balance bots).</summary>
    public interface IConfigurableStrategy
    {
        void Configure(AutoPlayerConfig config);
    }

    /// <summary>
    /// The balance bots' shared hands: they pay for what they build (the older strategies place
    /// for free), build next to the enemies' route where a tower covers the most of it, and
    /// upgrade by the panel's rules. Used to measure the climb: SingleType is the player who
    /// builds one tower and levels it, Counters builds what the wave card asks for, Greedy is
    /// Counters sending waves early when the card says it holds.
    /// </summary>
    public abstract class BalanceBot : IAutoPlayerStrategy, IConfigurableStrategy
    {
        public abstract string Name { get; }
        protected AutoPlayerConfig Config = new();
        protected readonly RandomNumberGenerator Rng = new();
        private int _sentOnTop;
        private float _waveClock;
        /// <summary>The bot's build phase: a new wave (and its stack) is next. Wrecks are repaired first (a third of a new tower, upgrades kept).</summary>
        protected void NewCycle()
        {
            _sentOnTop = 0; _waveClock = 0f;
            if (Engine.GetMainLoop() is SceneTree tree) VineNode.RepairAll(tree);
        }

        public void Configure(AutoPlayerConfig config) { Config = config ?? new AutoPlayerConfig(); Rng.Seed = (ulong)(config?.GetHashCode() ?? 1); }

        public abstract void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber);
        public virtual void OnMilestone(int wave, string type) { }

        public virtual void OnWavePhase(float dt)
        {
            // "Send three waves over and over": Stack more on top of each wave, a few seconds in
            if (Config.Stack <= 0 || !ServiceLocator.TryGet<VineWaveManager>(out var wm) || !wm.WaveActive) return;
            _waveClock += dt;
            if (_sentOnTop < Config.Stack && _waveClock > 2f + _sentOnTop)
            {
                _sentOnTop++;
                wm.RequestNextWave();
            }
        }


        protected static int Resources => GameManager.Instance?.CurrentResources ?? 0;

        /// <summary>Cells the enemies walk now, from every open entry.</summary>
        protected static List<Vector2I> RouteCells(VineGrid grid)
        {
            var cells = new List<Vector2I>();
            if (!ServiceLocator.TryGet<VinePathfinder>(out var pf)) return cells;
            foreach (var reg in grid.ActiveEntryRegions)
            {
                var p = pf.GetCachedPath(reg.Center);
                if (p != null) foreach (var c in p) if (!cells.Contains(c)) cells.Add(c);
            }
            return cells;
        }

        /// <summary>The open cell, next to the route, where a tower of this reach covers the most of it.</summary>
        protected static Vector2I? BestSpot(VineGrid grid, float range, List<Vector2I> route, bool nearExit = false)
        {
            if (route.Count == 0) return null;
            var routeSet = new HashSet<Vector2I>(route);
            var exit = grid.ExitPoint;
            Vector2I? best = null;
            float bestScore = -1f;
            var seen = new HashSet<Vector2I>();
            foreach (var c in route)
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        var at = c + new Vector2I(dx, dy);
                        if (!seen.Add(at) || routeSet.Contains(at) || !grid.CanPlace(at)) continue;
                        if ((at - exit).LengthSquared() <= 2) continue; // not on the Spire's doorstep
                        var w = grid.GridToWorld(at);
                        float score = 0f;
                        foreach (var r in route)
                        {
                            var rw = grid.GridToWorld(r);
                            if (new Vector2(rw.X - w.X, rw.Z - w.Z).Length() <= range) score += 1f;
                        }
                        if (nearExit) score += 40f / (1f + (at - exit).Length());
                        score += (float)(at.X * 7 + at.Y * 13) % 5 * 0.01f; // stable tie-break
                        if (score > bestScore) { bestScore = score; best = at; }
                    }
            return best;
        }

        /// <summary>Build and pay, as the build bar does. False if it can't be afforded or placed.</summary>
        protected static bool Buy(VineGrid grid, Vector2I at, VineNodeType type)
        {
            var data = VineNodeRegistry.Get(type);
            if (data == null || !grid.CanPlace(at)) return false;
            if (type == VineNodeType.SalvageDrill && SalvageDrill.WhyNotHere(grid, at) != null) return false;
            if (Climb.SupplyRefusal(type) != null) return false;
            // One Big Gun: the placer refuses the gun past the cap, so the bots do too
            if (MetaRun.GunCap > 0 && MetaRun.CountsAsGun(type) && Towers(grid, n => MetaRun.CountsAsGun(n.Data.Type)).Count >= MetaRun.GunCap) return false;
            int cost = MetaRun.PlaceCost(data);
            if (Resources < cost) return false;
            if (ServiceLocator.TryGet<VinePathfinder>(out var pf) && pf.WouldBlockAllPaths(at)) return false;
            var node = new VineNode();
            node.Initialize(data);
            if (!grid.PlaceNode(node, at)) return false;
            GameManager.Instance?.SpendResources(cost);
            node.PaidToPlace = cost;
            MetaRun.OnPlaced(data);
            return true;
        }

        protected static List<VineNode> Towers(VineGrid grid, Func<VineNode, bool> where = null)
        {
            var list = new List<VineNode>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var n = grid.GetNode(x, y);
                    if (n == null || n.IsDestroyed || n.Data?.Category != VineNodeCategory.Effect) continue;
                    if (where == null || where(n)) list.Add(n);
                }
            return list;
        }

        protected static int CostOf(VineNodeType t) => MetaRun.PlaceCost(VineNodeRegistry.Get(t));

        /// <summary>Buy the cheapest next step on any of these towers (level, then the branch <paramref name="branch"/> picks).</summary>
        protected static bool UpgradeCheapest(List<VineNode> towers, Func<VineNode, string> branch)
        {
            VineNode pick = null;
            int price = int.MaxValue;
            string branchId = null;
            foreach (var n in towers)
            {
                if (n.UpgradePlan == null) continue;
                if (n.NextLevel != null)
                {
                    int p = VineNode.PriceOf(n.NextLevel);
                    if (p < price && n.CantUpgrade() == null) { price = p; pick = n; branchId = null; }
                }
                else if (n.BranchChoices.Count > 0)
                {
                    string id = branch(n) ?? n.BranchChoices[0].Id;
                    var b = n.BranchChoices.FirstOrDefault(x => x.Id == id) ?? n.BranchChoices[0];
                    int p = VineNode.PriceOf(b);
                    if (p < price && n.CantBranch(b.Id) == null) { price = p; pick = n; branchId = b.Id; }
                }
            }
            if (pick == null) return false;
            return branchId == null ? pick.TryUpgrade() : pick.TryBranch(branchId);
        }

        /// <summary>
        /// Where this bot puts a new tower: the route spot (BestSpot), or, to test whether
        /// placement matters, the free cell nearest the Spire ("blob") or a random free cell
        /// within reach of the Spire's surroundings ("random").
        /// </summary>
        protected Vector2I? SpotFor(VineGrid grid, float range, List<Vector2I> route, bool nearExit = false)
        {
            switch (Config.Placement)
            {
                case "blob": return Nearest(grid, grid.ExitPoint, 1, 22);
                case "random":
                {
                    var exit = grid.ExitPoint;
                    for (int tries = 0; tries < 200; tries++)
                    {
                        var at = exit + new Vector2I(Rng.RandiRange(-16, 16), Rng.RandiRange(-12, 12));
                        if ((at - exit).LengthSquared() <= 2 || !grid.InBounds(at) || !grid.CanPlace(at)) continue;
                        if (ServiceLocator.TryGet<VinePathfinder>(out var pf) && pf.WouldBlockAllPaths(at)) continue;
                        return at;
                    }
                    return Nearest(grid, exit, 1, 22);
                }
                case "spread": return SpreadSpot(grid, range, route);
                case "checker": return Nearest(grid, grid.ExitPoint, 1, 22, checker: true);
                default: return BestSpot(grid, range, route, nearExit);
            }
        }

        /// <summary>
        /// A player who has seen siege fire: the route spot that covers the most, among cells that
        /// don't make a knot siege shells (a tower with Siege.MinCrowd towers within its splash).
        /// </summary>
        protected static Vector2I? SpreadSpot(VineGrid grid, float range, List<Vector2I> route)
        {
            if (route.Count == 0) return null;
            var routeSet = new HashSet<Vector2I>(route);
            var exit = grid.ExitPoint;
            var towers = Towers(grid).Select(n => n.GridPosition).ToList();
            float splashCells = Siege.Splash / Constants.VINE_CELL_SIZE;
            int Crowd(Vector2I c) => towers.Count(t => (t - c).Length() <= splashCells + 0.01f);
            Vector2I? best = null;
            float bestScore = -1f;
            var seen = new HashSet<Vector2I>();
            foreach (var c in route)
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        var at = c + new Vector2I(dx, dy);
                        if (!seen.Add(at) || routeSet.Contains(at) || !grid.CanPlace(at)) continue;
                        if ((at - exit).LengthSquared() <= 2) continue;
                        // Would it make (or join) a knot? Count it and each neighbour with it added
                        if (Crowd(at) + 1 >= Siege.MinCrowd) continue;
                        bool knot = false;
                        foreach (var t in towers)
                            if ((t - at).Length() <= splashCells + 0.01f && Crowd(t) + 1 >= Siege.MinCrowd) { knot = true; break; }
                        if (knot) continue;
                        var w = grid.GridToWorld(at);
                        float score = 0f;
                        foreach (var r in route)
                        {
                            var rw = grid.GridToWorld(r);
                            if (new Vector2(rw.X - w.X, rw.Z - w.Z).Length() <= range) score += 1f;
                        }
                        score += (float)(at.X * 7 + at.Y * 13) % 5 * 0.01f;
                        if (score > bestScore) { bestScore = score; best = at; }
                    }
            return best ?? BestSpot(grid, range, route);
        }

        /// <summary>
        /// Where a drill goes: open ground far enough out from the Spire, covered by as many of
        /// the bot's guns as possible (raiders come for drills), nearer the Spire on a tie.
        /// </summary>
        protected static Vector2I? DrillSpot(VineGrid grid)
        {
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            var exit = grid.ExitPoint;
            int min = Constants.SALVAGE_DRILL_MIN_SPIRE_CELLS, max = min + 6;
            var guns = Towers(grid, n => n.Data.AutoFires).Select(n => n.GlobalPosition).ToList();
            Vector2I? best = null;
            float bestScore = float.MinValue;
            for (int dx = -max; dx <= max; dx++)
                for (int dy = -max; dy <= max; dy++)
                {
                    var at = exit + new Vector2I(dx, dy);
                    float d = (at - exit).Length();
                    if (d < min || d > max || !grid.InBounds(at) || !grid.CanPlace(at)) continue;
                    if (SalvageDrill.WhyNotHere(grid, at) != null || (pf != null && pf.WouldBlockAllPaths(at))) continue;
                    var w = grid.GridToWorld(at);
                    int cover = guns.Count(g => new Vector2(g.X - w.X, g.Z - w.Z).Length() <= 12f);
                    float score = cover - d * 0.05f;
                    if (score > bestScore) { bestScore = score; best = at; }
                }
            return best;
        }

        /// <summary>The free buildable cell nearest <paramref name="from"/> (ring by ring) that leaves a route open.</summary>
        protected static Vector2I? Nearest(VineGrid grid, Vector2I from, int minR, int maxR, bool checker = false)
        {
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            for (int r = minR; r <= maxR; r++)
            {
                Vector2I? best = null;
                float bd = float.MaxValue;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        // A checkerboard: no tower beside another, so no knot for siege to shell
                        if (checker && ((dx + dy) & 1) != 0) continue;
                        var at = from + new Vector2I(dx, dy);
                        if ((at - from).LengthSquared() <= 2 || !grid.InBounds(at) || !grid.CanPlace(at)) continue;
                        if (pf != null && pf.WouldBlockAllPaths(at)) continue;
                        float d = (at - from).LengthSquared();
                        if (d < bd) { bd = d; best = at; }
                    }
                if (best.HasValue) return best;
            }
            return null;
        }

        protected static float RangeOf(VineNodeType t)
        {
            var d = VineNodeRegistry.Get(t);
            return d != null && d.Range > 0 ? d.Range : Constants.VINE_CELL_SIZE * 3f;
        }
    }

    /// <summary>
    /// One tower type, levelled up, and (with "stack") waves sent on top of each other: the
    /// build that used to coast. It takes the branch that reaches flyers when there is one.
    /// </summary>
    public class SingleTypeStrategy : BalanceBot
    {
        public override string Name => "SingleType";

        public override void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            NewCycle();
            if (!Enum.TryParse<VineNodeType>(Config.Tower, true, out var type)) type = VineNodeType.DamageTower;
            float range = RangeOf(type);
            for (int guard = 0; guard < 40; guard++)
            {
                var mine = Towers(grid, n => n.Data.Type == type);
                // Alternate: a new one, then level one up
                bool wantNew = mine.Count < 3 || mine.Count <= mine.Sum(n => n.Level - 1 + (n.Branch != null ? 1 : 0)) / 2;
                bool did = false;
                if (wantNew)
                {
                    var spot = BestSpot(grid, range, RouteCells(grid));
                    did = spot.HasValue && Buy(grid, spot.Value, type);
                }
                if (!did) did = UpgradeCheapest(mine, n => n.BranchChoices.FirstOrDefault(b => b.AntiAir)?.Id);
                if (!did && !wantNew)
                {
                    var spot = BestSpot(grid, range, RouteCells(grid));
                    did = spot.HasValue && Buy(grid, spot.Value, type);
                }
                if (!did) break;
            }
        }
    }

    /// <summary>
    /// Builds what the wave card asks for. Each step it reads the next two waves (ThreatModel),
    /// finds the enemy group the defence is weakest against and buys whatever adds the most
    /// damage against that group per Resource: a new tower of the type that suits it (flyers
    /// need anti-air, armour heavy hits, shields Tesla) where it covers the most of the route, or
    /// the next upgrade on a tower that already reaches it. Tar goes in once there's something to
    /// slow into, a wall or two lengthens the maze early, and Salvage Drills go up by the Spire
    /// while the defence holds with room to spare.
    /// </summary>
    public class CountersStrategy : BalanceBot
    {
        public override string Name => "Counters";
        /// <summary>Resources kept back each build (Greedy banks some for interest).</summary>
        protected virtual float Reserve(int wave) => 0f;

        private static readonly VineNodeType[] Guns = { VineNodeType.DamageTower, VineNodeType.ScatterCannon, VineNodeType.TeslaCoil, VineNodeType.FlakBattery };
        private int _wallsThisRun;
        private int _aheadFor = -1, _aheadWave = -1;

        public override void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            NewCycle();
            int next = waveNumber + 1;
            if (Config.Walls > 8) { for (int i = 0; i < Config.WallsPerBuild; i++) if (!PlaceWall(grid)) break; }
            else if (next <= 10 && next % 2 == 1) PlaceWall(grid);

            for (int guard = 0; guard < 160; guard++)
            {
                if (Resources <= Reserve(next)) break;
                var threat = ThreatModel.Assess(next, next + 1 + Math.Max(0, Config.Stack));
                var weakest = threat.Groups.Where(g => g.Health > 0.04f * threat.Health).OrderBy(g => g.Ratio).FirstOrDefault();
                // "lookAhead": a player reading the card's forecast builds for the wave it says breaks
                // ("Breaks at W33"), not only for the next one
                if (Config.LookAhead > 0)
                {
                    if (_aheadFor != next) { _aheadFor = next; _aheadWave = ThreatModel.BreaksAt(next + 2, Config.LookAhead); }
                    if (_aheadWave > 0)
                    {
                        var ahead = ThreatModel.Assess(_aheadWave, _aheadWave);
                        var big = ahead.Groups.Where(g => g.Health > 0.04f * ahead.Health).OrderBy(g => g.Ratio).ToList();
                        if (big.Count > 0 && (big[0].Ratio < weakest.Ratio || threat.Verdict == ThreatModel.Verdict.Holding)) weakest = big[0];
                    }
                }
                var route = RouteCells(grid);
                var towers = Towers(grid, n => n.Data.AutoFires);

                // Room to spare: a drill (by the Spire, behind the guns)
                bool roomForDrill = Config.EagerDrills ? threat.Verdict != ThreatModel.Verdict.Breaking : threat.Verdict == ThreatModel.Verdict.Holding && weakest.Ratio > 2.5f;
                if (Config.Drills && next >= 3 && roomForDrill
                    && SalvageDrill.All().Count < 1 + next / Math.Max(1, Config.DrillEvery) && Resources - SalvageDrill.NextCost() > Reserve(next) + 20)
                {
                    var spot = DrillSpot(grid);
                    if (spot.HasValue && Buy(grid, spot.Value, VineNodeType.SalvageDrill)) continue;
                }
                // Tar multiplies every gun around it
                int tar = Towers(grid, n => n.Data.Type == VineNodeType.SlowField).Count;
                if (towers.Count >= 6 && tar < towers.Count / Math.Max(1, Config.TarEvery) && !weakest.Flying && Resources >= CostOf(VineNodeType.SlowField)
                    && Climb.SupplyRefusal(VineNodeType.SlowField) == null)
                {
                    var spot = SpotFor(grid, RangeOf(VineNodeType.SlowField), route);
                    if (spot.HasValue && Buy(grid, spot.Value, VineNodeType.SlowField)) continue;
                }

                // The best damage per Resource against the weakest group
                float bestScore = 0f;
                Action best = null;
                foreach (var type in Guns)
                {
                    bool air = type is VineNodeType.FlakBattery or VineNodeType.TeslaCoil;
                    if (weakest.Flying && !air) continue;
                    if (Climb.SupplyRefusal(type) != null) continue;
                    int cost = CostOf(type);
                    if (cost > Resources) continue;
                    var spot = SpotFor(grid, RangeOf(type), route);
                    if (!spot.HasValue) continue;
                    float score = BaseDps(type, weakest.Count) * ThreatModel.HitShare(KindOf(type), weakest.Traits) * Coverage(grid, spot.Value, RangeOf(type), route) / cost;
                    if (score > bestScore) { bestScore = score; var at = spot.Value; var t = type; best = () => Buy(grid, at, t); }
                }
                foreach (var n in towers)
                {
                    var f = n.GetFirepower();
                    if (weakest.Flying ? !f.Air : !f.Ground) continue;
                    float cover = Coverage(grid, n.GridPosition, f.Range, route);
                    if (n.NextLevel != null)
                    {
                        int price = VineNode.PriceOf(n.NextLevel);
                        if (price > Resources) continue;
                        float gain = f.Dps * Mathf.Max(0.05f, (1f + n.NextLevel.Damage) * (1f + n.NextLevel.Rate) * (1f + n.NextLevel.Range * 0.5f) - 1f);
                        float score = gain * Mathf.Min(f.Targets, Mathf.Max(1f, weakest.Count / 4f)) * ThreatModel.HitShare(f.Kind, weakest.Traits) * cover / price;
                        if (score > bestScore) { bestScore = score; var node = n; best = () => node.TryUpgrade(); }
                    }
                    else if (n.BranchChoices.Count > 0)
                    {
                        var b = PickBranch(n, weakest);
                        int price = VineNode.PriceOf(b);
                        if (b == null || price > Resources) continue;
                        float gain = f.Dps * Mathf.Max(0.1f, (1f + b.Damage) * (1f + b.Rate) * (1f + b.Range * 0.5f) - 1f + (b.Grants?.Length ?? 0) * 0.3f);
                        float score = gain * ThreatModel.HitShare(f.Kind, weakest.Traits) * cover / price;
                        if (score > bestScore) { bestScore = score; var node = n; var id = b.Id; best = () => node.TryBranch(id); }
                    }
                }
                // One Big Gun: with the six guns built, banking is waste; the card's weakest group may
                // be one none of them can answer, so level whichever is cheapest rather than sit on it
                if (best == null && MetaRun.GunCap > 0)
                {
                    var cheapest = towers.Where(n => n.NextLevel != null && VineNode.PriceOf(n.NextLevel) <= Resources)
                        .OrderBy(n => VineNode.PriceOf(n.NextLevel)).FirstOrDefault();
                    if (cheapest != null) best = () => cheapest.TryUpgrade();
                }
                if (best == null) break;
                int before = Resources;
                best();
                if (Resources >= before) break; // nothing bought
            }
        }

        private static float BaseDps(VineNodeType t, int crowd)
        {
            var d = VineNodeRegistry.Get(t);
            float dps = d?.Damage ?? 10f;
            float targets = t switch
            {
                VineNodeType.ScatterCannon => 2.4f,
                VineNodeType.TeslaCoil => 1f + Constants.TESLA_COIL_CHAIN_COUNT * Constants.TESLA_COIL_CHAIN_FACTOR * 0.7f,
                VineNodeType.FlakBattery => Constants.FLAK_BATTERY_MAX_TARGETS,
                _ => 1f,
            };
            // A tower relic, as a player weighs it: Molten Rounds weakens Tesla and Flak and sets
            // turrets and scatters burning (the bot used to keep buying Tesla and Flak with it)
            float kit = RelicRun.TowerDamageMult(t)
                * (RelicRun.Ignites(t) ? 1f + 0.8f * RelicRun.T(RelicRun.MoltenRounds, "burnShare", 0.45f) : 1f);
            return dps * kit * Mathf.Min(targets, Mathf.Max(1f, crowd / 4f));
        }

        private static DamageKind KindOf(VineNodeType t) => t switch
        {
            VineNodeType.DamageTower or VineNodeType.ScatterCannon => DamageKind.Heavy,
            VineNodeType.FlakBattery => DamageKind.Light,
            VineNodeType.TeslaCoil => DamageKind.Electric,
            _ => DamageKind.Normal,
        };

        /// <summary>Path cells within reach of a spot, as a share of the route.</summary>
        private static float Coverage(VineGrid grid, Vector2I at, float range, List<Vector2I> route)
        {
            if (route.Count == 0 || range <= 0f) return 0.1f;
            var w = grid.GridToWorld(at);
            int n = 0;
            foreach (var c in route)
            {
                var p = grid.GridToWorld(c);
                if (new Vector2(p.X - w.X, p.Z - w.Z).Length() <= range) n++;
            }
            return Mathf.Max(0.05f, n / 10f);
        }

        /// <summary>The branch that answers the weakest group.</summary>
        private static TowerUpgradeStep PickBranch(VineNode n, ThreatModel.GroupResult weakest)
        {
            string id = n.Data.Type switch
            {
                VineNodeType.DamageTower => weakest.Flying ? "minigun" : "railgun",
                VineNodeType.ScatterCannon => (weakest.Traits & EnemyTraits.Armoured) != 0 ? "shredder" : "mortar",
                VineNodeType.TeslaCoil => (weakest.Traits & EnemyTraits.Shielded) != 0 ? "overload" : "storm",
                VineNodeType.FlakBattery => weakest.Flying ? "skyguard" : "shrapnel",
                _ => null,
            };
            return n.BranchChoices.FirstOrDefault(b => b.Id == id) ?? n.BranchChoices.FirstOrDefault();
        }

        /// <summary>A wall where it makes the route longest (it can't close the route).</summary>
        private bool PlaceWall(VineGrid grid)
        {
            if (_wallsThisRun >= Config.Walls || Resources < CostOf(VineNodeType.BarrierWall) + 30) return false;
            if (!ServiceLocator.TryGet<VinePathfinder>(out var pf) || grid.ActiveEntryRegions.Count == 0) return false;
            var from = grid.ActiveEntryRegions[0].Center;
            var now = pf.GetCachedPath(from);
            if (now == null || now.Count < 4) return false;
            Vector2I? best = null;
            int bestLen = now.Count + 3;
            for (int i = 3; i < now.Count - 3; i += 2)
            {
                var c = now[i];
                if (!grid.CanPlace(c) || pf.WouldBlockAllPaths(c)) continue;
                var p = pf.PreviewPath(from, c);
                if (p != null && p.Count > bestLen) { bestLen = p.Count; best = c; }
            }
            if (!best.HasValue || !Buy(grid, best.Value, VineNodeType.BarrierWall)) return false;
            _wallsThisRun++;
            return true;
        }
    }

    /// <summary>
    /// Builds without reading anything: every build it spends everything on a random gun (or,
    /// one time in three, a random tower's next level) where its placement puts it, with a drill
    /// every DrillEvery waves. The yardstick for "you just build continuously": it should die
    /// well before a bot that reads the wave card and covers the route.
    /// </summary>
    public class MindlessStrategy : BalanceBot
    {
        public override string Name => "Mindless";
        private static readonly VineNodeType[] Pick = { VineNodeType.DamageTower, VineNodeType.ScatterCannon, VineNodeType.TeslaCoil, VineNodeType.FlakBattery, VineNodeType.SlowField };

        public override void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            NewCycle();
            int next = waveNumber + 1;
            if (Config.Drills && next >= 3 && SalvageDrill.All().Count < next / Math.Max(1, Config.DrillEvery))
            {
                var spot = DrillSpot(grid);
                if (spot.HasValue) Buy(grid, spot.Value, VineNodeType.SalvageDrill);
            }
            int misses = 0;
            for (int guard = 0; guard < 160 && misses < 12; guard++)
            {
                var towers = Towers(grid, n => n.Data.AutoFires);
                bool did;
                if (towers.Count > 4 && Rng.Randf() < 0.33f)
                {
                    var n = towers[Rng.RandiRange(0, towers.Count - 1)];
                    did = n.NextLevel != null ? n.TryUpgrade()
                        : n.BranchChoices.Count > 0 && n.TryBranch(n.BranchChoices[Rng.RandiRange(0, n.BranchChoices.Count - 1)].Id);
                }
                else
                {
                    var type = Pick[Rng.RandiRange(0, Pick.Length - 1)];
                    var spot = SpotFor(grid, RangeOf(type), RouteCells(grid));
                    did = spot.HasValue && Buy(grid, spot.Value, type);
                }
                misses = did ? 0 : misses + 1;
            }
        }
    }

    /// <summary>
    /// Counters, playing the greed bet: banks a share for interest, and sends the next wave
    /// early whenever the card says the defence holds it sent now.
    /// </summary>
    public class GreedyStrategy : CountersStrategy
    {
        public override string Name => "Greedy";
        private float _look;

        protected override float Reserve(int wave) => 10f + 3f * wave;

        public override void OnWavePhase(float dt)
        {
            if ((_look -= dt) > 0f) return;
            _look = 2f;
            if (!ServiceLocator.TryGet<VineWaveManager>(out var wm) || !wm.WaveActive) return;
            if (wm.PendingStackedWaves >= 1) return;
            // Not into AXIS's chaos: the card says it's coming
            if (CorruptionManager.IsChaosWave(wm.CurrentWave + 1) || CorruptionManager.IsChaosWave(wm.CurrentWave)) return;
            var t = ThreatModel.Assess(wm.CurrentWave + 1, wm.CurrentWave + 1, sentEarly: true);
            if (t.Verdict == ThreatModel.Verdict.Holding && t.Ratio >= 2.5f)
            {
                GD.Print($"[Greedy] Sending W{wm.CurrentWave + 1} early ({t.Ratio:0.0}x, +{t.Greed} greed)");
                wm.RequestNextWave();
            }
        }
    }
}
