using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The perk tree's pick-one keystones (rows at 18 and 30 in each lane), saves in memory. The
    /// tree: two to a group, one group per lane per row, taking one closes the other (and a swap is
    /// a refund then a buy). Then two real battles, one keystone from each group in each:
    /// Fortress Doctrine (triple tower health, no repairs), One Big Gun (six towers, then no more;
    /// walls still build), Blood Rites (abilities paid in health), Juggernaut (takes less, no sprint,
    /// slower), Austerity (the Spire mines nothing), Last Stand (the Spire's fall below a quarter
    /// stuns the field and doubles tower fire for a while); Swarm Doctrine (half-price, half-health
    /// towers), Chain Reaction (tower kills blow up their neighbours), Glass Core (less health, more
    /// damage), Echo Protocol (abilities fire twice, slower cooldowns), Fortified Core (double Spire,
    /// smaller dome), Requisition (strikes can't be bought, a charge comes with each wave cleared).
    /// </summary>
    public class KeystoneTestSuite : ITestSuite
    {
        public string SuiteName => "keystones";
        private readonly List<VineEnemy> _spawned = new();

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var oldSave = gm.MetaSave;
            bool autoFire = GameSettings.BitAutoFire;
            RelicInventory.UseInMemoryForTests();
            SuitManager.UseInMemoryForTests();
            MetaPerkSave.SuppressWrites = true;
            GameSettings.BitAutoFire = false;
            gm.SuppressAutoDebrief = true;
            try
            {
                TreeRules(ctx);
                await BattleA(ctx);
                await BattleB(ctx);
            }
            finally
            {
                foreach (var e in _spawned) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
                gm.MetaSave = oldSave;
                gm.SuppressAutoDebrief = false;
                gm.AutoResolvePerks = false;
                GameSettings.BitAutoFire = autoFire;
                MetaPerkSave.SuppressWrites = false;
                RelicInventory.RestoreFromDisk();
                SuitManager.RestoreFromDisk();
                Engine.TimeScale = 1.0;
                if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = false;
            }
        }

        // ── The tree ──

        private static void TreeRules(TestContext ctx)
        {
            ctx.StartTest();
            var all = MetaPerkRegistry.GetAll();
            var groups = all.Where(n => n.Choice != null).GroupBy(n => n.Choice).ToList();
            ctx.Assert(groups.Count == 6 && groups.All(g => g.Count() == 2 && g.All(n => n.IsKeystone) && g.Select(n => n.Needs).Distinct().Count() == 1)
                && groups.Select(g => g.First().Needs).Distinct().OrderBy(x => x).SequenceEqual(new[] { 18, 30 }), "keystones/six_pick_one_groups",
                string.Join(", ", groups.Select(g => $"{g.Key}@{g.First().Needs}")));
            // Nothing but stat ranks in the rows they replaced: each choice changes a rule
            var known = new[] { "fortress", "swarm", "big_guns", "chain_reaction", "blood_rites", "glass_core", "juggernaut", "echo", "austerity", "fortified", "last_stand", "requisition" };
            ctx.Assert(all.Where(n => n.Choice != null).All(n => known.Contains(n.Effect)), "keystones/effects_known");

            var save = new MetaPerkSaveData { AvailablePoints = 200 };
            foreach (var n in all.Where(n => n.Lane == MetaPerkLane.Network && n.Needs < 18)) save.Ranks[n.Id] = n.MaxRank;
            bool a = MetaPerkRegistry.TryBuy("fortress_doctrine", save);
            string why = MetaPerkRegistry.WhyNot(MetaPerkRegistry.Get("swarm_doctrine"), save);
            bool b = MetaPerkRegistry.TryBuy("swarm_doctrine", save);
            ctx.Assert(a && !b && why != null && why.Contains("Fortress Doctrine"), "keystones/one_of_two", why ?? "allowed");
            bool refunded = MetaPerkRegistry.TryRefund("fortress_doctrine", save);
            bool swapped = MetaPerkRegistry.TryBuy("swarm_doctrine", save);
            ctx.Assert(refunded && swapped && MetaPerkRegistry.RankOf(save, "swarm_doctrine") == 1, "keystones/swap_by_refund");

            // A save that owned both of a group (or a row that moved) gets the points back on load
            var odd = new MetaPerkSaveData();
            foreach (var n in all.Where(n => n.Lane == MetaPerkLane.Network && n.Needs < 18)) odd.Ranks[n.Id] = n.MaxRank;
            odd.Ranks["fortress_doctrine"] = 1;
            odd.Ranks["swarm_doctrine"] = 1;
            int back = MetaPerkRegistry.RefundStranded(odd);
            ctx.Assert(back == MetaPerkRegistry.Get("swarm_doctrine").Cost && MetaPerkRegistry.RankOf(odd, "fortress_doctrine") == 1 && MetaPerkRegistry.RankOf(odd, "swarm_doctrine") == 0,
                "keystones/doubled_group_refunded", $"{back} back");
        }

        // ── Battle A ──

        private async Task BattleA(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var save = new MetaPerkSaveData();
            foreach (var id in new[] { "fortress_doctrine", "one_big_gun", "blood_rites", "juggernaut", "austerity", "last_stand" }) save.Ranks[id] = 1;
            gm.MetaSave = save;
            if (!await StartBattle(ctx, "A")) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var bit = ServiceLocator.Get<VinePlayer>();
            var spire = grid.Harvester;
            gm.AddResources(20000);

            // Fortress Doctrine
            ctx.StartTest();
            var cells = FreeCells(grid, 9);
            var t = placer.TestPlaceAt(VineNodeType.DamageTower, cells[0]);
            ctx.Assert(t != null && Mathf.IsEqualApprox(t.NodeMaxHealth, Constants.VINE_NODE_BASE_HEALTH * 3f), "keystones/fortress_triple_health", $"{t?.NodeMaxHealth}");
            if (t != null)
            {
                t.TakeDamage(t.NodeMaxHealth * 0.5f);
                float hp = t.NodeCurrentHealth;
                t.Repair(1000f);
                await ctx.Wait(1.2f);
                ctx.Assert(Mathf.IsEqualApprox(t.NodeCurrentHealth, hp) && t.WrecksAtZero, "keystones/fortress_no_repairs", $"{hp:F0} -> {t.NodeCurrentHealth:F0}, wrecks at zero {t.WrecksAtZero}");
            }

            // One Big Gun
            int built = placer.GunsBuilt();
            for (int i = 1; i < cells.Count && placer.GunsBuilt() < MetaRun.GunCap; i++) placer.TestPlaceAt(VineNodeType.DamageTower, cells[i]);
            var spare = cells.Skip(1).FirstOrDefault(c => grid.GetNode(c) == null);
            var seventh = placer.TestPlaceAt(VineNodeType.DamageTower, spare);
            var wall = placer.TestPlaceAt(VineNodeType.BarrierWall, spare);
            ctx.Assert(MetaRun.GunCap == 6 && placer.GunsBuilt() == 6 && seventh == null && wall != null && Mathf.IsEqualApprox(MetaRun.TowerDamageFactor, 3f)
                && Mathf.IsEqualApprox(MetaRun.TowerRangeFactor, 1.4f), "keystones/one_big_gun", $"{placer.GunsBuilt()} towers, 7th {(seventh == null ? "refused" : "built")}, wall {(wall != null ? "built" : "refused")}");

            // ...and a gun with its branch keeps levelling: Overcharge, each step dearer and harder-hitting
            ctx.StartTest();
            var big = cells.Select(c => grid.GetNode(c) as VineNode).FirstOrDefault(n => n != null && n.Data?.Type == VineNodeType.DamageTower);
            string oc = "no gun";
            bool overcharges = false;
            if (big != null)
            {
                GameManager.Instance.AddResources(100000);
                while (big.NextLevel != null && big.NextLevel.Id != VineNode.OverchargeId && big.TryUpgrade()) { }
                if (big.BranchChoices.Count > 0) big.TryBranch(big.BranchChoices[0].Id);
                var s1 = big.NextLevel;
                float d0 = big.UpgradeDamageMult;
                int p1 = VineNode.PriceOf(s1);
                bool b1 = big.TryUpgrade();
                int p2 = VineNode.PriceOf(big.NextLevel);
                bool b2 = big.TryUpgrade();
                overcharges = s1?.Id == VineNode.OverchargeId && b1 && b2 && big.Overcharge == 2 && p2 > p1
                    && Mathf.IsEqualApprox(big.UpgradeDamageMult, d0 + 2f * Constants.OVERCHARGE_DAMAGE);
                oc = $"next {s1?.Name ?? "none"} for {p1}, then {p2}; overcharge {big.Overcharge}, damage x{d0:F2} -> x{big.UpgradeDamageMult:F2}";
            }
            ctx.Assert(overcharges, "keystones/one_big_gun_overcharges", oc);

            // Blood Rites
            ctx.StartTest();
            bit.TestDrain();
            float hp0 = bit.CurrentHP;
            int used = bit.AbilitiesUsed;
            bit.UseAbility(0);
            var hud = ctx.FindNode<VineHUD>();
            await ctx.Wait(0.6f);
            var hudText = hud?.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).FirstOrDefault(x => x.EndsWith("HP  ready") || x.Contains(" HP")) ?? "";
            ctx.Assert(bit.AbilitiesUsed == used + 1 && bit.CurrentHP < hp0 - 5f && hudText.Contains("HP"), "keystones/blood_rites_costs_health",
                $"used {bit.AbilitiesUsed - used}, HP {hp0:F0} -> {bit.CurrentHP:F0}, HUD '{hudText}'");
            bit.Heal(1000f);

            // Juggernaut
            float before = bit.CurrentHP;
            bit.TakeDamage(10f);
            float lost = before - bit.CurrentHP;
            bit.Heal(100f);
            ctx.Assert(Mathf.Abs(lost - 4f) < 0.01f && MetaRun.NoSprint && Mathf.IsEqualApprox(MetaRun.BitMoveMult, 0.8f), "keystones/juggernaut",
                $"lost {lost:F1} of 10, move x{MetaRun.BitMoveMult:F2}");

            // Austerity: the Spire mines nothing
            int r0 = gm.CurrentResources;
            await ctx.Wait(Constants.VINE_HARVESTER_INCOME_INTERVAL + 1f);
            ctx.Assert(gm.CurrentResources == r0 && MetaRun.NoSpireIncome, "keystones/austerity_no_mining", $"{r0} -> {gm.CurrentResources}");

            // Last Stand
            ctx.StartTest();
            var rules = ServiceLocator.Get<KeystoneRules>();
            // Far from the Spire, out of reach of the Arcanist's shield shock
            var e1 = Spawn(spire.GlobalPosition + new Vector3(20f, 0, 0));
            var e2 = Spawn(spire.GlobalPosition + new Vector3(-20f, 0, 4f));
            await ctx.Wait(0.05f);
            spire.TakeDamage(1f); // the shield takes the first hit whole
            await ctx.Wait(0.05f);
            bool stunnedBefore = e1.IsStunned || e2.IsStunned;
            spire.TakeDamage(spire.CurrentHP - spire.MaxHP * 0.2f);
            await ctx.Wait(0.05f);
            ctx.Assert(!stunnedBefore && rules.LastStandFired && e1.IsStunned && e2.IsStunned && MetaRun.LastStandLeft > 0f, "keystones/last_stand",
                $"fired {rules.LastStandFired}, stunned {e1.IsStunned}/{e2.IsStunned}, {MetaRun.LastStandLeft:F1} s of double fire");
            spire.Heal(spire.MaxHP);
            Clear();
        }

        // ── Battle B ──

        private async Task BattleB(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var save = new MetaPerkSaveData();
            foreach (var id in new[] { "swarm_doctrine", "chain_reaction", "glass_core", "echo_protocol", "fortified_core", "requisition" }) save.Ranks[id] = 1;
            gm.MetaSave = save;
            if (!await StartBattle(ctx, "B")) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var bit = ServiceLocator.Get<VinePlayer>();
            var spire = grid.Harvester;
            gm.AddResources(5000);

            // Swarm Doctrine
            ctx.StartTest();
            var turret = VineNodeRegistry.Get(VineNodeType.DamageTower);
            var t = placer.TestPlaceAt(VineNodeType.DamageTower, FreeCells(grid, 1)[0]);
            ctx.Assert(MetaRun.PlaceCost(turret) == Climb.SupplyCost(Mathf.Max(1, Mathf.RoundToInt(turret.ResourceCost * 0.5f)), Climb.Standing(VineNodeType.DamageTower), Climb.Fleet()) && t != null
                && Mathf.IsEqualApprox(t.NodeMaxHealth, Constants.VINE_NODE_BASE_HEALTH * 0.5f) && MetaRun.SupplyStepMult < 0.99f, "keystones/swarm_half_price_half_health",
                $"cost {MetaRun.PlaceCost(turret)} of {turret.ResourceCost}, hp {t?.NodeMaxHealth}");

            // Chain Reaction: a tower kill blows up its neighbours
            var rules = ServiceLocator.Get<KeystoneRules>();
            var at = grid.GridToWorld(FreeCells(grid, 3)[2]);
            var a = Spawn(at);
            var b = Spawn(at + new Vector3(1.5f, 0, 0));
            await ctx.Wait(0.05f);
            float bHp = b.CurrentHealth;
            int blasts = rules.ChainBlasts;
            VineEnemy.HitSource = "your towers";
            try { a.TakeDamage(1e9f, DamageKind.Heavy); }
            finally { VineEnemy.HitSource = null; }
            await ctx.Wait(0.3f);
            ctx.Assert(rules.ChainBlasts > blasts && b.CurrentHealth < bHp - a.MaxHealth * 0.15f, "keystones/chain_reaction",
                $"blasts +{rules.ChainBlasts - blasts}, neighbour -{bHp - b.CurrentHealth:F0}");
            Clear();

            // Glass Core
            ctx.StartTest();
            float baseHp = SignalTuningEditor.PlayerMaxHP + SignalTuningEditor.PlayerMaxHPBonus + RoleRun.BitHpBonus;
            ctx.Assert(Mathf.Abs(bit.MaxHP - baseHp * 0.4f) < 0.5f && Mathf.IsEqualApprox(MetaRun.BitDamageFactor, 2f), "keystones/glass_core",
                $"BIT {bit.MaxHP:F0} of {baseHp:F0}, damage x{MetaRun.BitDamageFactor:F1}");

            // Echo Protocol
            bit.TestRefill();
            int echoes = bit.EchoesFired;
            bit.UseAbility(0);
            await ctx.Wait(1.0f);
            var cd = bit.GetAbilities()[0].Cooldown;
            ctx.Assert(bit.EchoesFired == echoes + 1 && Mathf.IsEqualApprox(cd, 4f * 1.4f), "keystones/echo_fires_twice", $"echoes +{bit.EchoesFired - echoes}, cooldown {cd:F1}");

            // Fortified Core
            var data = SpireData.Get(gm.SelectedRole);
            var dome = ServiceLocator.Get<ConversionDome>();
            ctx.Assert(data != null && Mathf.IsEqualApprox(spire.MaxHP, data.MaxHP * 2f) && Mathf.IsEqualApprox(dome.RadiusMult, 1f - MetaPerkRegistry.Get("fortified_core").Value2 / 100f) && dome.RadiusMult < 1f, "keystones/fortified_core",
                $"Spire {spire.MaxHP:F0} of {data?.MaxHP:F0}, dome x{dome.RadiusMult:F2}");

            // Requisition
            var st = SpireStation.Current;
            int charges = st.TotalCharges;
            bool cantBuy = st.CantBuyStrike("lance") != null && !st.BuyStrike("lance");
            GameEvents.OnWaveCleared?.Invoke(1);
            await ctx.Wait(0.1f);
            ctx.Assert(cantBuy && st.TotalCharges == charges + 1 && st.Charges("lance") >= 1, "keystones/requisition", $"charges {charges} -> {st.TotalCharges}");
        }

        // ── Helpers ──

        private VineEnemy Spawn(Vector3 at)
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            var grid = ServiceLocator.Get<VineGrid>();
            var e = new VineEnemy();
            tree.CurrentScene.AddChild(e);
            e.Initialize("Keystone test", VineEnemyFaction.Brute, 1e5f, 0f, 0, PlanetTheme.Current.EnemyScavenger, grid.WorldToGrid(at));
            e.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
            e.SkipMarch();
            _spawned.Add(e);
            return e;
        }

        private void Clear()
        {
            foreach (var e in _spawned) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            _spawned.Clear();
        }

        private static List<Vector2I> FreeCells(VineGrid grid, int count)
        {
            var list = new List<Vector2I>();
            int cx = grid.Width / 2, cy = grid.Height / 2;
            var pf = ServiceLocator.Get<VinePathfinder>();
            for (int r = 3; r < 22 && list.Count < count; r++)
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

        private static async Task<bool> StartBattle(TestContext ctx, string tag)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, $"keystones/battle_{tag}_loads", "no site"); return false; }
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = 1;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Arcanist";
            gm.AvailableNodes = SpireData.Get("Arcanist")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, $"keystones/battle_{tag}_loads", site.Id);
            if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = true;
            await ctx.Wait(0.5f);
            return ok;
        }
    }
}
