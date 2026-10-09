using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The perk tree's perks in a real battle (P1, first site, Bruteforge) with every perk bought:
    /// stat perks reach the run, Field Kit's free towers (and the build bar saying so), Masonry's
    /// cheaper and tougher walls, Veteran Crews' free level, Surplus Parts' cheaper upgrades,
    /// Free Rebuild's full refund between waves, Head Start's levels, cheaper abilities, Second
    /// Wind once a run, Bounty Contracts on a boss, a tougher Spire, Compound Interest when a
    /// wave ends and the fourth perk card. Then the same battle with an empty tree is plain.
    /// </summary>
    public class MetaPerksTestSuite : ITestSuite
    {
        public string SuiteName => "metaperks";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var oldSave = gm.MetaSave;
            var full = new MetaPerkSaveData();
            // Every fixed perk at its top rank (Mastery is endless: checked on its own below)
            foreach (var n in MetaPerkRegistry.GetAll()) if (!n.Repeatable) full.Ranks[n.Id] = n.MaxRank;
            gm.MetaSave = full;

            if (!await StartBattle(ctx)) { gm.MetaSave = oldSave; return; }
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var player = ServiceLocator.Get<VinePlayer>();
            var waves = ServiceLocator.Get<VineWaveManager>();
            float R(string id) { var n = MetaPerkRegistry.Get(id); return n.Value * n.MaxRank; }

            // ── Stats reach the run ──
            ctx.StartTest();
            ctx.Assert(Mathf.IsEqualApprox(SignalTuningEditor.DamageTowerDPS, Constants.DAMAGE_TOWER_DPS * (1f + R("calibrated_barrels") / 100f)),
                "metaperks/tower_damage", $"{SignalTuningEditor.DamageTowerDPS:F2}");
            ctx.Assert(Mathf.IsEqualApprox(SignalTuningEditor.DamageTowerRange, Constants.DAMAGE_TOWER_RANGE + R("long_sight")),
                "metaperks/tower_range", $"{SignalTuningEditor.DamageTowerRange:F2}");
            var spireData = SpireData.Get(gm.SelectedRole);
            ctx.Assert(spireData != null && Mathf.IsEqualApprox(grid.Harvester.MaxHP, spireData.MaxHP * (1f + R("reinforced_spire") / 100f)),
                "metaperks/spire_hp", $"{grid.Harvester.MaxHP:F0} vs base {spireData?.MaxHP:F0}");

            // ── Field Kit: two free towers, and the build bar says so ──
            var cells = FreeCells(grid, 8);
            gm.AddResources(500);
            var freeBtn = FindButton((Node)ctx.FindNode<VineHUD>() ?? ctx.Tree.CurrentScene, "Junk Turret");
            await ctx.Wait(0.1f);
            bool saysFree = freeBtn != null && freeBtn.Text.Contains("FREE");
            int before = gm.CurrentResources;
            var t1 = placer.TestPlaceAt(VineNodeType.DamageTower, cells[0]);
            var t2 = placer.TestPlaceAt(VineNodeType.DamageTower, cells[1]);
            int afterTwo = gm.CurrentResources;
            await ctx.Wait(0.1f);
            bool saysPrice = freeBtn != null && !freeBtn.Text.Contains("FREE");
            int beforeT3 = gm.CurrentResources;
            var t3 = placer.TestPlaceAt(VineNodeType.DamageTower, cells[2]);
            int cost = VineNodeRegistry.Get(VineNodeType.DamageTower).ResourceCost;
            ctx.Assert(t1 != null && t2 != null && t3 != null && afterTwo == before && gm.CurrentResources == beforeT3 - cost,
                "metaperks/field_kit_two_free", $"{before} -> {afterTwo}; third {beforeT3} -> {gm.CurrentResources} (turret {cost})");
            ctx.Assert(saysFree && saysPrice, "metaperks/build_bar_says_free", $"'{freeBtn?.Text.Replace("\n", " ")}'");

            // ── Veteran Crews: built one level up ──
            ctx.Assert(t3 != null && t3.Level == 2, "metaperks/veteran_level", $"level {t3?.Level}");

            // ── Surplus Parts: cheaper upgrades (on top of Bruteforge's own discount) ──
            var next = t3?.NextLevel;
            int price = VineNode.PriceOf(next);
            ctx.Assert(next != null && price == Mathf.Max(1, Mathf.RoundToInt(next.Cost * (1f - R("surplus_parts") / 100f) * RoleRun.UpgradeCostMult)) && price < next.Cost,
                "metaperks/upgrade_discount", next == null ? "no next level" : $"{next.Cost} -> {price}");

            // ── Free Rebuild: everything back between waves ──
            ctx.Assert(gm.CurrentPhase == GamePhase.Build && t3 != null && t3.SellValue == cost,
                "metaperks/free_rebuild_full_refund", $"sell {t3?.SellValue} of {cost} in {gm.CurrentPhase}");
            ctx.Assert(t1 != null && t1.SellValue == 0, "metaperks/free_tower_sells_for_nothing", $"sell {t1?.SellValue}");

            // ── Masonry: cheaper, tougher walls ──
            int wallBefore = gm.CurrentResources;
            var wall = placer.TestPlaceAt(VineNodeType.BarrierWall, cells[3]);
            int wallCost = Mathf.Max(1, Constants.BARRIER_WALL_COST - Mathf.RoundToInt(R("masonry")));
            ctx.Assert(wall != null && wallBefore - gm.CurrentResources == wallCost
                && wall.NodeMaxHealth >= Constants.BARRIER_WALL_HP * (1f + MetaPerkRegistry.Get("masonry").Value2 * 2 / 100f) - 0.01f,
                "metaperks/masonry", $"cost {wallBefore - gm.CurrentResources} (expected {wallCost}), hp {wall?.NodeMaxHealth:F0}");

            // ── BIT: Head Start, cheaper abilities ──
            ctx.Assert(player.Progression != null && player.Progression.Level == 1 + Mathf.RoundToInt(R("head_start")),
                "metaperks/head_start", $"level {player.Progression?.Level}");
            var shock = player.GetAbilities()[0];
            ctx.Assert(Mathf.IsEqualApprox(shock.MaterialsCost, 15f * (1f - R("efficient_casting") / 100f)),
                "metaperks/ability_cost", $"Shock Blast {shock.MaterialsCost:F2} Materials");

            // ── Second Wind: once a run ──
            player.TakeDamage(1e6f);
            await ctx.Wait(0.2f);
            bool stood = player.IsAlive && Mathf.IsEqualApprox(player.CurrentHP, player.MaxHP);
            player.TakeDamage(1e6f);
            await ctx.Wait(0.2f);
            ctx.Assert(stood && !player.IsAlive, "metaperks/second_wind_once", $"first hit: stood={stood}; second: alive={player.IsAlive}");

            // ── Bounty Contracts: a boss drops double ──
            var am = ServiceLocator.Get<AscendantManager>();
            am.TestAnnounce("iron_sovereign");
            var boss = am.TestSpawnNow(1, 0f, withFriendly: false);
            ctx.Assert(boss != null && boss.DropValue == boss.ResourceValue * 2 && boss.ResourceValue > 0,
                "metaperks/bounty_doubles_bosses", boss == null ? "no boss" : $"{boss.ResourceValue} -> {boss.DropValue}");
            if (boss != null) boss.TakeDamage(1e8f, DamageKind.Heavy);
            await ctx.Wait(0.5f);

            // ── Compound Interest when a wave ends ──
            gm.AddResources(2000);
            waves.RequestNextWave();
            Engine.TimeScale = 4.0;
            bool ended = await ctx.WaitUntil(() =>
            {
                foreach (var e in ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).OfType<VineEnemy>())
                    if (e.IsAlive) e.TakeDamage(1e8f, DamageKind.Heavy);
                return !waves.WaveActive && gm.CurrentPhase is GamePhase.WaveComplete or GamePhase.Build;
            }, 60f);
            Engine.TimeScale = 1.0;
            var ci = MetaPerkRegistry.Get("compound_interest");
            ctx.Assert(ended && waves.LastInterest == Mathf.RoundToInt(ci.Value2), "metaperks/compound_interest",
                $"paid {waves.LastInterest} (cap {ci.Value2}) with {gm.CurrentResources} banked");

            // ── The rows added after the first tree was bought out ──
            ctx.StartTest();
            ctx.Assert(t3 != null && Mathf.IsEqualApprox(MetaRun.CrewBonusAdd, R("crew_drills") / 100f), "metaperks/crew_drills",
                $"+{MetaRun.CrewBonusAdd:P0} a link");
            ctx.Assert(Mathf.IsEqualApprox(MetaRun.DomeBonusAdd, R("dome_tenders") / 100f), "metaperks/dome_tenders", $"+{MetaRun.DomeBonusAdd:P0}");
            ctx.Assert(t3 != null && t3.NodeMaxHealth >= Constants.VINE_NODE_BASE_HEALTH * (1f + R("hardened_plating") / 100f) - 0.5f,
                "metaperks/hardened_plating", $"tower health {t3?.NodeMaxHealth:F0}");
            if (t2 != null)
            {
                int k0 = t2.Kills;
                t2.CreditKill();
                ctx.Assert(t2.Kills - k0 == 2, "metaperks/battle_hardened_double_kills", $"+{t2.Kills - k0}");
            }
            ctx.Assert(Mathf.IsEqualApprox(player.MoveSpeed, SignalTuningEditor.PlayerMoveSpeed * (1f + R("fleet_foot") / 100f), 0.05f)
                || player.MoveSpeed > SignalTuningEditor.PlayerMoveSpeed * (1f + R("fleet_foot") / 100f) - 0.05f,
                "metaperks/fleet_foot", $"move {player.MoveSpeed:F2} vs {SignalTuningEditor.PlayerMoveSpeed:F2}");
            ctx.Assert(Mathf.IsEqualApprox(player.GetAbilities()[1].Cooldown, 8f * (1f - R("quick_cooldowns") / 100f)), "metaperks/quick_cooldowns",
                $"Repair Pulse {player.GetAbilities()[1].Cooldown:F1} s");
            ctx.Assert(Mathf.IsEqualApprox(MetaRun.BitRegenMult, 1f + R("field_medic") / 100f) && Mathf.IsEqualApprox(MetaRun.BitKillBounty, 1.5f),
                "metaperks/field_medic_and_scavenger", $"regen x{MetaRun.BitRegenMult:F1}, BIT kills x{MetaRun.BitKillBounty:F1}");
            var st = SpireStation.Current;
            await ctx.Wait(0.3f);
            ctx.Assert(st != null && st.Data.Strikes.All(sk => st.Charges(sk.Id) >= 1), "metaperks/stockpile_starts_charged",
                st == null ? "no Spire" : string.Join(", ", st.Data.Strikes.Select(sk => $"{sk.Id} {st.Charges(sk.Id)}")));
            ctx.Assert(grid.Harvester.ExtraGunCount == Mathf.RoundToInt(R("gun_foundry")) + RoleRun.StartGuns, "metaperks/gun_foundry", $"{grid.Harvester.ExtraGunCount} guns");
            var lance = st?.Strike("lance");
            ctx.Assert(lance != null && lance.CostAt(0) == Mathf.Max(1, Mathf.RoundToInt(lance.Cost * (1f - R("strike_logistics") / 100f) * RoleRun.StrikeCostMult)),
                "metaperks/strike_logistics", lance == null ? "" : $"lance {lance.CostAt(0)} of {lance.Cost}");
            ctx.Assert(Mathf.IsEqualApprox(MetaRun.DropMult, 1f + R("salvage_claims") / 100f), "metaperks/salvage_claims", $"x{MetaRun.DropMult:F2}");

            // ── Wider Choice: four cards ──
            var pick = new VinePerkScreen { InBattleOverlay = true, Wave = 5 };
            ctx.Tree.CurrentScene.AddChild(pick);
            await ctx.Wait(0.2f);
            ctx.AssertEqual(4, pick.ChoiceCount, "metaperks/wider_choice_four_cards");
            pick.QueueFree();
            await ctx.Wait(0.2f);
            ctx.Tree.Paused = false;

            // ── An empty tree changes nothing ──
            gm.MetaSave = new MetaPerkSaveData();
            if (await StartBattle(ctx))
            {
                ctx.StartTest();
                var p2 = ServiceLocator.Get<VinePlayer>();
                ctx.Assert(MetaRun.FreeTowersLeft == 0 && !MetaRun.FreeRebuild && !MetaRun.VeteranTowers && MetaRun.PerkChoices == 3
                    && Mathf.IsEqualApprox(SignalTuningEditor.DamageTowerDPS, Constants.DAMAGE_TOWER_DPS)
                    && (p2.Progression?.Level ?? 1) == 1 && Mathf.IsEqualApprox(p2.GetAbilities()[0].MaterialsCost, 15f),
                    "metaperks/empty_tree_is_plain", $"free {MetaRun.FreeTowersLeft}, level {p2.Progression?.Level}, dps {SignalTuningEditor.DamageTowerDPS}");
            }

            gm.MetaSave = oldSave;
            gm.AutoResolvePerks = false;
            Engine.TimeScale = 1.0;
        }

        private static async Task<bool> StartBattle(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "metaperks/battle_loads", "no site"); return false; }
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = site.Planet;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = SpireData.Get("Bruteforge")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, "metaperks/battle_loads", site.Id);
            await ctx.Wait(0.5f);
            return ok;
        }

        // Open cells near the middle that don't cut the route
        private static System.Collections.Generic.List<Vector2I> FreeCells(VineGrid grid, int count)
        {
            var list = new System.Collections.Generic.List<Vector2I>();
            int cx = grid.Width / 2, cy = grid.Height / 2;
            var pf = ServiceLocator.Get<VinePathfinder>();
            for (int r = 3; r < 14 && list.Count < count; r++)
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

        private static Button FindButton(Node root, string startsWith)
        {
            if (root is Button b && b.Text.StartsWith(startsWith)) return b;
            foreach (var c in root.GetChildren())
            {
                var f = FindButton(c, startsWith);
                if (f != null) return f;
            }
            return null;
        }
    }
}
