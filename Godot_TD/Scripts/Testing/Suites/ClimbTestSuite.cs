using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The balance pass (2026-10): the steady climb (every wave harder, no wall after the authored
    /// waves, leaks capped), the money that keeps pace (bounties, the wave bonus past W20,
    /// interest, the greed bet for sending waves early, Salvage Drills, resource nodes and their
    /// raiders), counters a single tower can't cover, power types that shape the run, and the
    /// threat readout on the wave card.
    /// </summary>
    public class ClimbTestSuite : ITestSuite
    {
        public string SuiteName => "climb";
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
                Curve(ctx);
                Money(ctx);
                Counters(ctx);
                Powers(ctx);
                await Battle(ctx);
            }
            finally
            {
                foreach (var e in _spawned) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
                PowerType.Reset();
                MetaRun.InterestPct = 0f;
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

        // ── The curve ──

        private static void Curve(TestContext ctx)
        {
            ctx.StartTest();
            bool rises = true;
            float maxStep = 0f;
            for (int w = 2; w <= 120; w++)
            {
                if (Climb.Hp(w) <= Climb.Hp(w - 1)) rises = false;
                maxStep = Mathf.Max(maxStep, Climb.StepAt(w));
            }
            ctx.Assert(rises && Mathf.IsEqualApprox(Climb.Hp(1), 1f), "climb/harder_every_wave", $"steepest step x{maxStep:F2}");
            ctx.Assert(maxStep < 1.4f, "climb/no_wall", $"steepest step x{maxStep:F2} (it was x1.18 a wave on top of the authored growth past W20)");
            // Past the authored waves it carries on as before: no jump
            ctx.Assert(Mathf.Abs(Climb.StepAt(21) - Climb.StepAt(20)) < 0.12f, "climb/smooth_past_authored",
                $"W20 x{Climb.StepAt(20):F3}, W21 x{Climb.StepAt(21):F3}");
            ctx.Assert(Climb.Damage(30) > Climb.Damage(1) && Mathf.IsEqualApprox(Climb.Damage(1), 1f), "climb/enemies_hit_harder",
                $"x{Climb.Damage(30):F2} at W30");

            // A leak costs a set amount for its wave, capped: one never ends a run
            float plain = Climb.LeakDamage(5, 200f, false, false), deep = Climb.LeakDamage(120, 200f, false, false);
            float boss = Climb.LeakDamage(120, 200f, false, true);
            ctx.Assert(plain > 0f && deep <= 200f * 0.12f + 0.01f && boss <= 200f * 0.25f + 0.01f && deep >= plain, "climb/leaks_capped",
                $"W5 {plain:F0}, W120 {deep:F0}, boss {boss:F0} of 200");

            // The same procedural wave whenever it's asked for (the card previews the one that comes)
            var authored = VineWaveLoader.LoadPlanetWaves(1);
            bool mixed = true;
            var why = new List<string>();
            for (int w = authored.Count + 1; w <= authored.Count + 30; w++)
            {
                var data = VineWaveLoader.GenerateWave(w, authored, new RandomNumberGenerator { Seed = (ulong)w });
                var have = EnemyTraits.None;
                foreach (var s in data.Surges) have |= s.Traits;
                int kinds = new[] { EnemyTraits.Armoured, EnemyTraits.Shielded, EnemyTraits.Flying }.Count(t => (have & t) != 0);
                if (kinds < 2) { mixed = false; why.Add($"W{w}"); }
                // Flyers stay plain: nothing that reaches the air hits armour hard, and shields on
                // flyers outlasted any amount of Tesla
                foreach (var s in data.Surges)
                    if ((s.Traits & EnemyTraits.Flying) != 0 && (s.Traits & (EnemyTraits.Armoured | EnemyTraits.Shielded)) != 0) { mixed = false; why.Add($"W{w} covered flyers"); }
            }
            ctx.Assert(mixed, "climb/deep_waves_need_two_answers", why.Count == 0 ? "" : "one answer covers " + string.Join(", ", why));

            // Procedural waves at one depth carry about the same health budget whichever template
            // they come from, and the same share of commanders (rolls gave 0 one wave and 4 the next)
            float lo = float.MaxValue, hi = 0f;
            var cmdShare = new List<float>();
            for (int seed = 1; seed <= 12; seed++)
            {
                var data = VineWaveLoader.GenerateWave(36, authored, new RandomNumberGenerator { Seed = (ulong)(seed * 7919) });
                float budget = data.Surges.Sum(sg => sg.Count * sg.Health);
                lo = Mathf.Min(lo, budget); hi = Mathf.Max(hi, budget);
                cmdShare.Add(data.Surges.Count(sg => sg.Commander != null) / (float)Mathf.Max(1, data.Surges.Count));
            }
            ctx.Assert(hi / Mathf.Max(1f, lo) < 1.6f && cmdShare.Max() - cmdShare.Min() < 0.35f, "climb/procedural_waves_even",
                $"W36 budgets {lo:F0} to {hi:F0} (x{hi / Mathf.Max(1f, lo):F2}), commanders per surge {cmdShare.Min():F2} to {cmdShare.Max():F2}");
        }

        // ── Money ──

        private static void Money(TestContext ctx)
        {
            ctx.StartTest();
            ctx.Assert(Climb.Bounty(30) > Climb.Bounty(1) && Climb.Bounty(1) > 0.3f && Climb.Bounty(1) <= 1f, "money/bounties_grow", $"x{Climb.Bounty(30):F2} at W30");
            var waves = VineWaveLoader.LoadPlanetWaves(1);
            int last = VineWaveManager.ComputeWaveBonus(waves.Count, waves[^1], waves.Count);
            int after = VineWaveManager.ComputeWaveBonus(waves.Count + 1, null, waves.Count);
            int later = VineWaveManager.ComputeWaveBonus(waves.Count + 10, null, waves.Count);
            ctx.Assert(after >= last * 0.9f && after <= last * 1.3f && later > after, "money/bonus_carries_on",
                $"W{waves.Count} {last}, W{waves.Count + 1} {after}, W{waves.Count + 10} {later} (it jumped from 50 to 96)");
            ctx.Assert(Climb.GreedBonus(0) == 1f && Climb.GreedBonus(2) > Climb.GreedBonus(1) && Climb.GreedBounty(2) > 1f, "money/greed_pays_more_the_further_ahead",
                $"bonus x{Climb.GreedBonus(1):F2} / x{Climb.GreedBonus(2):F2}, kills x{Climb.GreedBounty(2):F2}");

            // Interest on what's banked, capped by the wave; Compound Interest raises both
            MetaRun.InterestPct = 0f;
            int plain = VineWaveManager.InterestOn(1000, 10);
            int pct = Mathf.FloorToInt(1000 * Climb.InterestPct / 100f);
            ctx.Assert(plain == Mathf.Min(pct, Climb.InterestCap(10)) && VineWaveManager.InterestOn(100, 10) == Mathf.FloorToInt(100 * Climb.InterestPct / 100f),
                "money/interest_on_savings", $"{plain} on 1000 at W10 (cap {Climb.InterestCap(10)})");
            MetaRun.InterestPct = 5f;
            int keystone = VineWaveManager.InterestOn(1000, 10);
            MetaRun.InterestPct = 0f;
            ctx.Assert(keystone > plain && keystone == Mathf.Min(Mathf.FloorToInt(1000 * (Climb.InterestPct + 5f) / 100f), Climb.InterestCap(10) * 2),
                "money/compound_interest_raises_it", $"{keystone}");

            ctx.Assert(SalvageDrill.CostWith(0) == Constants.SALVAGE_DRILL_COST && SalvageDrill.CostWith(2) > SalvageDrill.CostWith(1)
                && SalvageDrill.BaseAt(20) > SalvageDrill.BaseAt(1), "money/drills_dearer_each_and_pay_more_later",
                $"{SalvageDrill.CostWith(0)}, {SalvageDrill.CostWith(1)}, {SalvageDrill.CostWith(2)}; pays {SalvageDrill.BaseAt(1):F0} then {SalvageDrill.BaseAt(20):F0}");
            ctx.Assert(VineHarvester.NodePayAt(30) > VineHarvester.NodePayAt(1), "money/nodes_pay_more_later",
                $"{VineHarvester.NodePayAt(1)} then {VineHarvester.NodePayAt(30)}");

            // Towers get dearer with the whole field, not just their type; chaos drops pay a
            // little more, not triple
            ctx.Assert(Climb.SupplyFactor(0, 60) > Climb.SupplyFactor(0, 10) && Climb.SupplyFactor(5, 0) > 1f
                && Climb.SupplyCost(12, 5, 60) > Climb.SupplyCost(12, 5, 0), "money/fleet_makes_towers_dearer",
                $"x{Climb.SupplyFactor(0, 10):F2} with 10 towers, x{Climb.SupplyFactor(0, 60):F2} with 60");
            ctx.Assert(Climb.ChaosDropMult >= 1f && Climb.ChaosDropMult < 3f, "money/chaos_drops_not_triple", $"x{Climb.ChaosDropMult:F2}");

            // AXIS answers the perk tree: none with an empty tree, more with a full one, capped
            ctx.Assert(Mathf.IsEqualApprox(Climb.ThreatFor(0), 1f) && Climb.ThreatFor(100) > 1.3f && Climb.ThreatFor(100) > Climb.ThreatFor(40)
                && Climb.ThreatFor(100000) <= 6f, "climb/threat_follows_the_perk_tree",
                $"x{Climb.ThreatFor(40):F2} at 40 points, x{Climb.ThreatFor(100):F2} at 100, cap x{Climb.ThreatFor(100000):F2}");
            int saved = MetaRun.PointsSpent;
            MetaRun.PointsSpent = 100;
            float hp = Climb.EnemyHp(10);
            MetaRun.PointsSpent = saved;
            ctx.Assert(Mathf.IsEqualApprox(hp, Climb.Hp(10) * Climb.ThreatFor(100)), "climb/threat_on_enemy_health", $"x{hp:F2} at W10 with 100 points");
        }

        // ── Counters ──

        private static void Counters(TestContext ctx)
        {
            ctx.StartTest();
            var mini = TowerUpgradePlan.For("damage_tower")?.Branches.FirstOrDefault(b => b.Id == "minigun");
            ctx.Assert(mini != null && mini.AntiAir && mini.Kind == "Light" && (1f + mini.Rate) * (1f + mini.Damage) <= 1.05f,
                "counters/minigun_is_a_specialist", mini == null ? "no minigun" : $"kind {mini.Kind}, damage a second x{(1f + mini.Rate) * (1f + mini.Damage):F2}");
            float lightOnShield = ThreatModel.HitShare(DamageKind.Light, EnemyTraits.Shielded);
            float electricOnShield = ThreatModel.HitShare(DamageKind.Electric, EnemyTraits.Shielded);
            float lightOnArmour = ThreatModel.HitShare(DamageKind.Light, EnemyTraits.Armoured);
            ctx.Assert(lightOnShield < 0.5f && electricOnShield > lightOnShield * 2f && lightOnArmour < 0.5f, "counters/light_rounds_need_help",
                $"light on shields {lightOnShield:F2}, Tesla {electricOnShield:F2}, light on armour {lightOnArmour:F2}");
        }

        // ── Power types ──

        private static void Powers(TestContext ctx)
        {
            ctx.StartTest();
            PowerType.Reset();
            ctx.Assert(PowerType.TowerRateMult == 1f && PowerType.WallCostMult == 1f && PowerType.CorrodeStacks == 0, "power/none_until_picked");
            PowerType.TestSet(MaterialType.Chaos, 0);
            int c0 = PowerType.CorrodeStacks;
            PowerType.TestSet(MaterialType.Chaos, 3);
            int c3 = PowerType.CorrodeStacks;
            ctx.Assert(c0 >= 3 && c3 > c0 && PowerType.ThrowCells > 0f, "power/chaos_corrodes_and_grows", $"{c0} stacks at tier 0, {c3} at tier 3");
            PowerType.TestSet(MaterialType.Power, 0);
            float r0 = PowerType.TowerRateMult;
            PowerType.TestSet(MaterialType.Power, 5);
            ctx.Assert(r0 > 1f && PowerType.TowerRateMult > r0 && PowerType.TowerRangeMult > 1f && PowerType.OverclockSecondsMult > 1f, "power/power_fires_faster_and_grows",
                $"x{r0:F2} at tier 0, x{PowerType.TowerRateMult:F2} at 5");
            PowerType.TestSet(MaterialType.Environment, 0);
            ctx.Assert(PowerType.WallCostMult < 1f && PowerType.WallHpMult > 1f && PowerType.SlowMult > 1f && PowerType.NodeMult > 1f && PowerType.FortifySeconds > 0f,
                "power/environment_builds_the_maze", PowerType.TierLine());
            ctx.Assert(PowerType.PickText(MaterialType.Chaos).Length > 20 && PowerType.PickText(MaterialType.Power).Length > 20 && PowerType.PickText(MaterialType.Environment).Length > 20,
                "power/picker_says_what_each_does");

            // Tiers come from Materials banked
            PowerType.Reset();
            PowerType.Set(MaterialType.Chaos);
            PowerType.AddEarned(PowerType.NextTierAt + 1);
            ctx.Assert(PowerType.Tier == 1, "power/tier_from_materials_banked", $"tier {PowerType.Tier} after {PowerType.Earned:F0}");
            PowerType.Reset();
        }

        // ── In a battle ──

        private async Task Battle(TestContext ctx)
        {
            var gm = GameManager.Instance;
            gm.MetaSave = new MetaPerkSaveData();
            if (!await StartBattle(ctx)) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var wm = ServiceLocator.Get<VineWaveManager>();
            var cm = ServiceLocator.Get<CorruptionManager>();
            gm.AddResources(50000);

            // The wave card previews the procedural wave that comes
            ctx.StartTest();
            var a = wm.WaveDataAt(wm.TotalWaves + 4);
            var b = wm.WaveDataAt(wm.TotalWaves + 4);
            ctx.Assert(a != null && ReferenceEquals(a, b), "climb/procedural_preview_is_the_wave");

            // Enemies come with the climb's health
            wm.TestSetWave(7);
            wm.RequestNextWave();
            var w8 = wm.WaveDataAt(8);
            VineEnemy first = null;
            await ctx.WaitUntil(() => (first = Roster.Enemies(ctx.Tree).FirstOrDefault(e => e.IsAlive && !e.IsCommander && e.Wave == 8)) != null, 20f);
            float authoredHp = w8.Surges.Where(s => s.EnemyName == first?.EnemyName).Select(s => s.Health).FirstOrDefault();
            ctx.Assert(first != null && Mathf.Abs(first.MaxHealth / Mathf.Max(1f, authoredHp) - Climb.EnemyHp(8)) < 0.05f, "climb/enemies_get_the_climb",
                $"{first?.MaxHealth:F0} hp vs authored {authoredHp:F0} x{Climb.Hp(8):F2}");
            ctx.Assert(first != null && Mathf.IsEqualApprox(first.BountyScale, Climb.Bounty(8)) && Climb.Bounty(8) > Climb.Bounty(1), "money/kills_pay_the_wave",
                $"bounty x{first?.BountyScale:F2} (W8 x{Climb.Bounty(8):F2})");

            // A stack: chaos stays armed, each wave pays its own clear, greed pays extra
            ctx.StartTest();
            await KillAll(ctx, wm);
            await ctx.WaitForPhase(GamePhase.Build, 10f);
            int cleared = 0;
            System.Action<int> count = _ => cleared++;
            GameEvents.OnWaveCleared += count;
            wm.RequestNextWave();               // W9: a chaos wave
            bool armed = cm.ChaosArmed;
            wm.RequestNextWave();               // W10 on top
            wm.RequestNextWave();               // W11 on top
            ctx.Assert(CorruptionManager.IsChaosWave(9) && armed && cm.ChaosArmed, "chaos/stacking_keeps_chaos", $"armed {armed} then {cm.ChaosArmed}");
            await KillAll(ctx, wm);
            GameEvents.OnWaveCleared -= count;
            ctx.Assert(cleared == 3 && wm.CurrentWave == 11, "money/each_stacked_wave_pays", $"{cleared} waves cleared to W{wm.CurrentWave}");
            ctx.Assert(wm.LastGreedBonus > 0, "money/greed_paid", $"+{wm.LastGreedBonus}");
            ctx.Assert(wm.LastInterest > 0, "money/interest_paid", $"+{wm.LastInterest}");

            // A Salvage Drill pays when the wave clears, and raiders go for it
            ctx.StartTest();
            await ctx.WaitForPhase(GamePhase.Build, 10f);

            // Waiting between waves earns nothing: the Spire mines while a wave is on
            if (grid.Harvester.CurrentMode != MiningMode.Resources) grid.Harvester.ToggleMode();
            int idle0 = GameManager.Instance.CurrentResources;
            await ctx.Wait(Constants.VINE_HARVESTER_INCOME_INTERVAL * 1.4f);
            ctx.Assert(GameManager.Instance.CurrentPhase == GamePhase.Build && GameManager.Instance.CurrentResources == idle0, "money/no_mining_between_waves",
                $"{idle0} -> {GameManager.Instance.CurrentResources} over {Constants.VINE_HARVESTER_INCOME_INTERVAL * 1.4f:0} s of build");
            // Drills stand out from the Spire, where raiders can get at them
            ctx.StartTest();
            var exit = grid.ExitPoint;
            Vector2I? close = null;
            for (int r = 2; r < 7 && close == null; r++)
                for (int dx = -r; dx <= r && close == null; dx++)
                    for (int dy = -r; dy <= r && close == null; dy++)
                    {
                        var d = new Vector2I(dx, dy);
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
                        if (grid.CanPlace(exit + d) && !ServiceLocator.Get<VinePathfinder>().WouldBlockAllPaths(exit + d)) close = exit + d;
                    }
            var refused = close.HasValue ? placer.TestPlaceAt(VineNodeType.SalvageDrill, close.Value) : null;
            ctx.Assert(close.HasValue && refused == null && SalvageDrill.WhyNotHere(grid, close.Value) != null, "drill/not_beside_the_spire",
                close.HasValue ? SalvageDrill.WhyNotHere(grid, close.Value) ?? "allowed" : "no free cell near the Spire");
            // A resource node takes a drill, on it: a gun can't go there, a gun beside it doesn't
            // hold it (it used to), the drill does, and taking the drill off frees the ore
            ctx.StartTest();
            var ore = grid.GetResourceNodes().Where(c => grid.GetNode(c) == null).ToList();
            if (ore.Count > 0)
            {
                var rn = ore[0];
                var gunOn = placer.TestPlaceAt(VineNodeType.DamageTower, rn);
                Vector2I? beside = null;
                foreach (var d in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
                    if (beside == null && grid.CanPlace(rn + d) && !ServiceLocator.Get<VinePathfinder>().WouldBlockAllPaths(rn + d)) beside = rn + d;
                var gunBeside = beside.HasValue ? placer.TestPlaceAt(VineNodeType.DamageTower, beside.Value) : null;
                await ctx.Wait(Constants.HAZARD_TICK_INTERVAL * 2.5f);
                bool heldByGun = grid.IsResourceNodeCaptured(rn);
                var drillOn = placer.TestPlaceAt(VineNodeType.SalvageDrill, rn);
                await ctx.Wait(Constants.HAZARD_TICK_INTERVAL * 2.5f);
                bool held = grid.IsResourceNodeCaptured(rn) && grid.IsResourceNode(rn) && grid.GetResourceNodes().Contains(rn);
                ctx.Assert(gunOn == null && !heldByGun && drillOn != null && held, "drill/on_resource_node",
                    $"gun on it {gunOn != null}, held by a gun beside it {heldByGun}, drill on it {drillOn != null}, held {held}");
                grid.RemoveNode(rn);
                if (beside.HasValue && gunBeside != null) grid.RemoveNode(beside.Value);
                await ctx.Wait(Constants.HAZARD_TICK_INTERVAL * 2.5f);
                ctx.Assert(grid.GetCell(rn) == VineCellType.ResourceNode && !grid.IsResourceNodeCaptured(rn), "drill/off_resource_node_frees_it",
                    $"cell {grid.GetCell(rn)}, held {grid.IsResourceNodeCaptured(rn)}");
            }
            else ctx.Assert(true, "drill/on_resource_node", "no free resource node on this map");

            ctx.StartTest();
            var cells = FreeCells(grid, 400).Where(c => SalvageDrill.WhyNotHere(grid, c) == null).ToList();
            if (cells.Count == 0) { ctx.Assert(false, "drill/built_and_next_dearer", "no cell far enough from the Spire"); return; }
            var drill = placer.TestPlaceAt(VineNodeType.SalvageDrill, cells[0]);
            int nextCost = SalvageDrill.NextCost();
            ctx.Assert(drill != null && nextCost > Constants.SALVAGE_DRILL_COST, "drill/built_and_next_dearer", $"next costs {nextCost}");
            var raids = ServiceLocator.Get<RaidManager>();
            int drafted = raids.Drafted;
            int drillWave = wm.CurrentWave + 1;
            wm.RequestNextWave();
            bool raided = await ctx.WaitUntil(() => raids.Drafted > drafted, 40f);
            var raider = Roster.Enemies(ctx.Tree).FirstOrDefault(e => e.IsRaider);
            ctx.Assert(raided && raider != null && raider.IsWandering, "drill/raiders_come_for_it", $"{raids.Drafted - drafted} drafted");
            int want = drill != null ? SalvageDrill.PayoutOf(drill, drillWave) : 0;
            await KillAll(ctx, wm);
            ctx.Assert(drill != null && GodotObject.IsInstanceValid(drill) && !drill.IsDestroyed && wm.LastDrillPay == want && want > 0, "drill/pays_each_wave",
                $"paid {wm.LastDrillPay} (want {want})");

            // A leak at a deep wave costs the capped amount, not a share of its health
            ctx.StartTest();
            await ctx.WaitForPhase(GamePhase.Build, 10f);
            var spire = grid.Harvester;
            spire.Heal(spire.MaxHP);
            var exitCell = grid.ExitPoint;
            var near = FreeCells(grid, 40).OrderBy(c => (c - exitCell).LengthSquared()).First();
            var leaker = Spawn(grid.GridToWorld(near), 1e7f, 5f);
            leaker.Wave = 90;
            float hp0 = spire.CurrentHP;
            bool leaked = false;
            System.Action<Node, Vector3> onLeak = (n, _) => { if (n == leaker) leaked = true; };
            GameEvents.OnEnemyLeaked += onLeak;
            await ctx.WaitUntil(() => leaked, 30f);
            await ctx.Wait(0.1f);
            GameEvents.OnEnemyLeaked -= onLeak;
            float took = hp0 - spire.CurrentHP;
            float cap = Climb.LeakDamage(90, spire.MaxHP, false, false);
            ctx.Assert(leaked && took > 0f && took <= cap + 0.5f && cap <= spire.MaxHP * 0.25f, "climb/one_leak_cant_end_it",
                $"took {took:F0} of {spire.MaxHP:F0} (cap {cap:F0}); a W90 enemy with 10 million hp used to take a million");

            // Corrode: tower hits stack it, each stack makes hits land harder
            ctx.StartTest();
            PowerType.TestSet(MaterialType.Chaos, 1);
            var dummy = Spawn(grid.GridToWorld(cells[5]), 1e6f, 0f);
            VineEnemy.HitSource = "your towers";
            for (int i = 0; i < 12; i++) dummy.TakeDamage(1f, DamageKind.Heavy);
            VineEnemy.HitSource = null;
            ctx.Assert(dummy.CorrodeStacks == PowerType.CorrodeStacks && dummy.CorrodeTakenMult > 1.2f, "power/corrode_stacks_to_the_cap",
                $"{dummy.CorrodeStacks} stacks, x{dummy.CorrodeTakenMult:F2} damage taken");
            dummy.ChaosTakenMult = CorruptionManager.CHAOS_TAKEN;
            ctx.Assert(dummy.StatusTakenMult < dummy.CorrodeTakenMult, "chaos/berserk_takes_less_not_flat_armour", $"x{dummy.StatusTakenMult:F2}");

            // Power: towers reach further; Environment: walls are cheaper
            PowerType.Reset();
            var turret = placer.TestPlaceAt(VineNodeType.DamageTower, cells[8]);
            float range0 = turret?.CurrentRange ?? 0f;
            PowerType.TestSet(MaterialType.Power, 0);
            float range1 = turret?.CurrentRange ?? 0f;
            PowerType.TestSet(MaterialType.Environment, 0);
            int wallCost = MetaRun.PlaceCost(VineNodeRegistry.Get(VineNodeType.BarrierWall));
            ctx.Assert(range1 > range0 * 1.02f && wallCost < Constants.BARRIER_WALL_COST && PowerType.HitSlow > 0f, "power/towers_and_walls_feel_it",
                $"range {range0:F1} -> {range1:F1}, wall {wallCost} (was {Constants.BARRIER_WALL_COST})");
            PowerType.Reset();

            // The readout: a bare defence breaks on flyers and says why; the card shows it
            ctx.StartTest();
            int flyWave = Enumerable.Range(9, 12).FirstOrDefault(w => wm.WaveDataAt(w)?.Surges.Any(s => (s.Traits & EnemyTraits.Flying) != 0) == true);
            var t = ThreatModel.Assess(flyWave, flyWave);
            ctx.Assert(flyWave > 0 && t.Health > 0f && t.Enemies > 0 && t.Bonus > 0, "threat/reads_the_wave", $"W{flyWave}: {t.Health:F0} hp, {t.Enemies} enemies, pays {t.Pay}");
            ctx.Assert(t.Verdict != ThreatModel.Verdict.Holding || t.GapTrait != EnemyTraits.None || t.Ratio < 99f, "threat/has_a_verdict", $"{t.VerdictWord} {t.Ratio:F2}x");
            var stack = ThreatModel.Assess(flyWave, flyWave + 2, sentEarly: true);
            ctx.Assert(stack.Health > t.Health && stack.Greed > 0, "threat/stack_is_one_fight", $"{stack.Health:F0} hp, greed +{stack.Greed}");
            var hud = ctx.FindNode<VineHUD>();
            await ctx.Wait(1.0f);
            ctx.Assert(hud?.LastThreat != null && hud.LastThreat.From == wm.CurrentWave + 1, "threat/on_the_wave_card", hud?.LastThreat?.VerdictWord ?? "none");

            // The card counts the Overclock cast last wave (a player who casts none gets the plain card)
            ctx.StartTest();
            OverclockHabit.Reset();
            // A gun beside the route, against an early ground wave (it can't reach flyers)
            Vector2I? byRoute = null;
            var pf0 = ServiceLocator.Get<VinePathfinder>();
            foreach (var reg in grid.ActiveEntryRegions)
            {
                var path = pf0.GetCachedPath(reg.Center);
                if (path == null) continue;
                // On the lip, where it sees the route (Sight: one set back sees little of it)
                var spots = TestCells.BesideRoute(grid, path, 1, path.Count / 2);
                if (spots.Count > 0) { byRoute = spots[0]; break; }
            }
            // (a gun on the lip sees enough of the route to deal three times wave 3's health, where
            // the card stops counting, so a heavier ground wave: flyers come on 6)
            const int groundWave = 5;
            // What the towers add: BIT and the Spire's guns count on the card too (and BIT in a
            // frame by now counts for more), so the gain is judged against the towers' part
            var noGun = ThreatModel.Assess(groundWave, groundWave);
            var ocGun = byRoute != null ? placer.TestPlaceAt(VineNodeType.DamageTower, byRoute.Value) : null;
            var plain = ThreatModel.Assess(groundWave, groundWave);
            OverclockHabit.WaveStarted();
            OverclockHabit.Tick(20f);
            foreach (var n in Roster.Nodes(ctx.Tree))
                if (n.Data?.Category == VineNodeCategory.Effect && !n.IsDestroyed) OverclockHabit.Boosted(n, 8f);
            OverclockHabit.WaveEnded();
            var counted = ThreatModel.Assess(groundWave, groundWave);
            float gain = OverclockHabit.Gain(ocGun);
            OverclockHabit.Reset();
            float towersPart = Mathf.Max(1f, plain.Damage - noGun.Damage);
            ctx.Assert(ocGun != null && !plain.Overclock && counted.Overclock && counted.Damage - plain.Damage > towersPart * (gain - 1f) * 0.6f && gain > 1.4f && gain < 1.7f,
                "threat/counts_the_overclock_cast", $"damage {plain.Damage:F0} -> {counted.Damage:F0} with 40% of the wave overclocked (x{gain:F2} a tower; the gun's part {towersPart:F0})");
            Clear();

            // Siege: a siege enemy with towers in reach beds down and shells the tightest knot,
            // hurting every tower in the splash (packing round the Spire won by a wide margin)
            ctx.StartTest();
            var pf = ServiceLocator.Get<VinePathfinder>();
            Vector2I? hub = null;
            var plus = new[] { Vector2I.Zero, Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
            for (int x = 6; x < grid.Width - 6 && hub == null; x++)
                for (int y = 6; y < grid.Height - 6 && hub == null; y++)
                {
                    var c = new Vector2I(x, y);
                    if ((c - grid.ExitPoint).Length() < 12f) continue;
                    if (plus.All(d => grid.CanPlace(c + d) && !pf.WouldBlockAllPaths(c + d)) && grid.CanPlace(c + new Vector2I(7, 0))) hub = c;
                }
            if (hub == null) { ctx.Assert(false, "siege/shells_the_knot", "no room for a knot of towers"); return; }
            var knot = new List<VineNode>();
            // Turrets (a canyon's Barricades only go on the floor)
            gm.AddResources(400);
            foreach (var d in plus) if (placer.TestPlaceAt(grid.IsCanyon ? VineNodeType.DamageTower : VineNodeType.BarrierWall, hub.Value + d) is { } kw) knot.Add(kw);
            var knotHp = knot.Select(kw => kw.NodeCurrentHealth).ToList();
            var at = grid.GridToWorld(hub.Value + new Vector2I(7, 0));
            var gun = new VineEnemy();
            ctx.Tree.CurrentScene.AddChild(gun);
            gun.Initialize("Siege test", VineEnemyFaction.Brute, 1e6f, 0f, 0, new Color(0.8f, 0.4f, 0.2f), grid.WorldToGrid(at));
            gun.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
            gun.SkipMarch();
            gun.SetTraits(EnemyTraits.Siege);
            _spawned.Add(gun);
            await ctx.Wait(Siege.Flight + 2.5f);
            int hurt = knot.Where((kw, i) => GodotObject.IsInstanceValid(kw) && (kw.IsDestroyed || kw.NodeCurrentHealth < knotHp[i])).Count();
            ctx.Assert(gun.IsSieging && gun.ShellsFired >= 1 && hurt >= 3, "siege/shells_the_knot",
                $"sieging {gun.IsSieging}, {gun.ShellsFired} shells, {hurt} of {knot.Count} packed walls hurt");
            Clear();
            foreach (var kw in knot) if (GodotObject.IsInstanceValid(kw) && !kw.IsDestroyed) grid.RemoveNode(kw.GridPosition);

            // Wrecks: a gun at zero health stands as a wreck (no fire, still blocking, still on
            // the grid) until repaired for a share of what went into it; a wall is gone
            ctx.StartTest();
            gm.AddResources(500); // placing goes through the placer's prices
            var free = FreeCells(grid, 6);
            if (free.Count < 2) { ctx.Assert(false, "wreck/stands_until_repaired", "no free cells"); return; }
            var gunCell = free[0];
            var tower = placer.TestPlaceAt(VineNodeType.DamageTower, gunCell);
            var wallCell = grid.IsCanyon ? TestCells.WallSpot(grid) ?? free[1] : free[1];
            var wall = placer.TestPlaceAt(VineNodeType.BarrierWall, wallCell);
            if (tower == null || wall == null) { ctx.Assert(false, "wreck/stands_until_repaired", "couldn't place the test towers"); return; }
            tower.PaidToPlace = 100;
            tower.TakeDamage(1e9f, TowerLoss.Siege);
            wall.TakeDamage(1e9f);
            await ctx.Wait(0.1f);
            bool wrecked = GodotObject.IsInstanceValid(tower) && tower.IsWrecked && tower.IsDestroyed && grid.GetNode(gunCell) == tower
                && tower.GetFirepower().Dps == 0f && VineNode.Wrecks(ctx.Tree).Contains(tower);
            bool wallGone = grid.GetNode(wallCell) == null;
            // It looks broken: smoke, sparks and a ring keep coming, the model is scorched, and a
            // label the same size at any zoom says it needs a repair
            int puffs0 = tower.WreckPuffs;
            await ctx.Wait(1.6f);
            var wlabel = tower.FindChild("WreckLabel", true, false) as Label3D;
            var meshes = tower.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => m.Mesh != null && m.Visible).ToList();
            int scorched = meshes.Count(m => m.MaterialOverlay is ShaderMaterial sm && sm.Shader != null && sm.Shader.Code.Contains("blend_mul"));
            bool looksWrecked = tower.WreckPuffs - puffs0 >= 6 && wlabel != null && wlabel.Visible && wlabel.FixedSize && wlabel.Text.Contains("repair") && scorched > 0;
            string wreckLook = $"{tower.WreckPuffs - puffs0} puffs in 1.6 s, label '{wlabel?.Text?.Replace("\n", " ")}' fixed {wlabel?.FixedSize}, {scorched} of {meshes.Count} meshes scorched";
            int bill = tower.RepairCost, sell = tower.SellValue;
            gm.AddResources(bill + 5);
            int before = gm.CurrentResources;
            bool fixedUp = tower.TryRepairWreck();
            ctx.Assert(wrecked && wallGone && bill == Mathf.RoundToInt(100 * Constants.WRECK_REPAIR_SHARE) && fixedUp && !tower.IsWrecked
                && tower.NodeCurrentHealth >= tower.NodeMaxHealth && before - gm.CurrentResources == bill && tower.GetFirepower().Dps > 0f
                && sell < tower.SellValue, "wreck/stands_until_repaired",
                $"wrecked {wrecked}, wall gone {wallGone}, repair {bill}r (paid {before - gm.CurrentResources}), back {fixedUp}, wreck sells {sell} vs {tower.SellValue}");
            int scorchedAfter = meshes.Count(m => GodotObject.IsInstanceValid(m) && m.MaterialOverlay is ShaderMaterial sm2 && sm2.Shader != null && sm2.Shader.Code.Contains("blend_mul"));
            ctx.Assert(looksWrecked && scorchedAfter == 0 && !(wlabel?.Visible ?? false), "wreck/looks_broken",
                $"{wreckLook}; after the repair {scorchedAfter} scorched, label shown {wlabel?.Visible}");
            grid.RemoveNode(gunCell);
        }

        // ── Helpers ──

        private static async Task KillAll(TestContext ctx, VineWaveManager wm)
        {
            Engine.TimeScale = 4.0;
            await ctx.WaitUntil(() =>
            {
                foreach (var e in Roster.Enemies(ctx.Tree))
                    if (e.IsAlive && e.OnGrid) e.TakeDamage(1e12f, DamageKind.Heavy);
                return !wm.WaveActive;
            }, 90f);
            Engine.TimeScale = 1.0;
            await ctx.Wait(0.3f);
        }

        private VineEnemy Spawn(Vector3 at, float hp, float speed)
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            var grid = ServiceLocator.Get<VineGrid>();
            var e = new VineEnemy();
            tree.CurrentScene.AddChild(e);
            e.Initialize("Climb test", VineEnemyFaction.Scavenger, hp, speed, 0, PlanetTheme.Current.EnemyScavenger, grid.WorldToGrid(at));
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

        private static async Task<bool> StartBattle(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "climb/battle_loads", "no site"); return false; }
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = 1;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = SpireData.Get("Obelisk")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, "climb/battle_loads", site.Id);
            if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = true;
            await ctx.Wait(0.5f);
            return ok;
        }
    }
}
