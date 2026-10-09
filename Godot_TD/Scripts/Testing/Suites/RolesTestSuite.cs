using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The three roles play differently (Data/Spires/*.json "role", <see cref="RoleRun"/>).
    /// The role screen: each card shows how the role plays, its Spire's weapon, bonuses and a
    /// signature, and picking a card starts the run as that role. Then a battle as each role
    /// with an empty perk tree. Bruteforge: an extra Spire gun at once and more every 10 waves,
    /// cheaper upgrades and strikes, harder Junk Turrets. Arcanist: crew links worth double,
    /// relays reaching two cells and buffing harder, harder Tesla Coils, a shield back in half
    /// the time that stuns what's close when it breaks. Obelisk: a stronger BIT with more health
    /// and cheaper abilities, and a beam that jumps on to more enemies. And none of it leaks
    /// into another role's run.
    /// </summary>
    public class RolesTestSuite : ITestSuite
    {
        public string SuiteName => "roles";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var oldSave = gm.MetaSave;
            gm.MetaSave = new MetaPerkSaveData();
            bool autoFire = GameSettings.BitAutoFire;
            GameSettings.BitAutoFire = false;
            try
            {
                await RoleScreen(ctx);
                await Bruteforge(ctx);
                await Arcanist(ctx);
                await Obelisk(ctx);
            }
            finally
            {
                GameSettings.BitAutoFire = autoFire;
                gm.MetaSave = oldSave;
                gm.AutoResolvePerks = false;
                Engine.TimeScale = 1.0;
                if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = false;
            }
        }

        // ── The role screen ──

        private static async Task RoleScreen(TestContext ctx)
        {
            var gm = GameManager.Instance;
            SetSite(ctx);
            ctx.Tree.ChangeSceneToFile(Constants.SCENE_VINE_DRAFT);
            await ctx.Wait(1.0f);
            var screen = ctx.FindNode<VineDraftScreen>();
            ctx.StartTest();
            ctx.Assert(screen != null && screen.Cards.All(c => c != null), "roles/screen_opens", screen == null ? "no screen" : "");
            if (screen == null) return;

            var texts = new List<string>();
            for (int i = 0; i < screen.Cards.Length; i++)
            {
                var card = screen.Cards[i];
                string role = VineDraftScreen.GetRoleName(i);
                var labels = card.FindChildren("*", "Label", true, false).OfType<Label>().ToList();
                var weapon = labels.FirstOrDefault(l => l.Name == "Weapon");
                var play = labels.FirstOrDefault(l => l.Name == "Playstyle");
                var sig = labels.FirstOrDefault(l => l.Name == "Signature");
                int bonuses = labels.Count(l => l.Name.ToString().StartsWith("Bonus"));
                bool listsTowers = labels.Any(l => l.Text.Contains("Junk Turret ("));
                ctx.Assert(weapon != null && play != null && sig != null && bonuses >= 3 && !listsTowers, $"roles/card_shows_how_{role}_plays",
                    $"weapon {weapon?.Text}, playstyle {play?.Text}, {bonuses} bonuses, signature {sig?.Text}, tower list {listsTowers}");
                texts.Add(string.Join("|", labels.Select(l => l.Text)));
            }
            ctx.Assert(texts.Distinct().Count() == texts.Count, "roles/cards_differ", $"{texts.Distinct().Count()} different cards of {texts.Count}");

            // Picking Bruteforge's card starts the run as Bruteforge
            int bf = Enumerable.Range(0, VineDraftScreen.RoleCount).First(i => VineDraftScreen.GetRoleName(i) == "Bruteforge");
            var select = screen.Cards[bf].FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "SELECT");
            gm.AutoResolvePerks = true;
            select?.EmitSignal(BaseButton.SignalName.Pressed);
            bool ok = select != null && await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok && gm.SelectedRole == "Bruteforge" && RoleRun.Role == "Bruteforge", "roles/picking_a_card_sets_the_role",
                $"selected {gm.SelectedRole}, run as {RoleRun.Role}");
        }

        // ── Bruteforge: guns ──

        private static async Task Bruteforge(TestContext ctx)
        {
            var gm = GameManager.Instance;
            if (!await StartBattle(ctx, "Bruteforge")) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var station = SpireStation.Current;
            var h = grid.Harvester;

            ctx.StartTest();
            ctx.Assert(h.ExtraGunCount == 1, "roles/bruteforge_starts_with_a_gun", $"{h.ExtraGunCount} extra guns");
            ctx.AssertEqual(75, MetaRun.UpgradeCost(100), "roles/bruteforge_upgrades_cheaper");
            var strike = station?.Data.Strikes.FirstOrDefault();
            ctx.Assert(strike != null && strike.CostAt(0) == Mathf.Max(1, Mathf.RoundToInt(strike.Cost * 0.75f)), "roles/bruteforge_strikes_cheaper",
                strike == null ? "no strike" : $"{strike.CostAt(0)} vs {strike.Cost}");

            // A lone Junk Turret hits 20% harder than with no role
            var cell = FreeCells(grid, 1).FirstOrDefault();
            gm.AddResources(500);
            var t = placer.TestPlaceAt(VineNodeType.DamageTower, cell);
            await ctx.Wait(0.2f);
            float with = t?.CurrentDps ?? 0f;
            RoleRun.Reset();
            float without = t?.CurrentDps ?? 0f;
            RoleRun.Apply("Bruteforge");
            ctx.Assert(t != null && without > 0f && Mathf.Abs(with / without - 1.2f) < 0.01f, "roles/bruteforge_turrets_hit_harder",
                $"{with:F1} vs {without:F1} dps");

            // Signature: another gun every 10 waves, 3 at most
            int wave = gm.CurrentWave;
            int tags = DamageNumbers.TagCount;
            gm.CurrentWave = 20;
            await ctx.Wait(0.2f);
            int at20 = h.ExtraGunCount;
            gm.CurrentWave = 90;
            await ctx.Wait(0.2f);
            int at90 = h.ExtraGunCount;
            ctx.Assert(at20 == 3 && at90 == 4 && DamageNumbers.TagCount > tags, "roles/bruteforge_guns_grow_with_waves",
                $"{at20} extra guns at wave 20, {at90} at wave 90, {DamageNumbers.TagCount - tags} tags");
            gm.CurrentWave = wave;
            await ctx.Wait(0.2f);
        }

        // ── Arcanist: the network ──

        private static async Task Arcanist(TestContext ctx)
        {
            var gm = GameManager.Instance;
            if (!await StartBattle(ctx, "Arcanist")) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var h = grid.Harvester;
            gm.AddResources(2000);

            ctx.StartTest();
            ctx.Assert(Mathf.IsEqualApprox(h.ShieldRechargeDelay, SpireData.Get("Arcanist").ShieldRechargeDelay / 2f), "roles/arcanist_shield_back_twice_as_fast",
                $"{h.ShieldRechargeDelay:F1} s");
            ctx.AssertEqual(100, MetaRun.UpgradeCost(100), "roles/arcanist_upgrades_full_price");
            ctx.Assert(h.ExtraGunCount == 0, "roles/arcanist_no_bruteforge_gun", $"{h.ExtraGunCount}");

            // Two turrets side by side: each link worth +10%
            var row = FreeRow(grid, 4);
            if (row.Count < 4) { ctx.Assert(false, "roles/arcanist_space", "no free row"); return; }
            var a = placer.TestPlaceAt(VineNodeType.DamageTower, row[0]);
            var b = placer.TestPlaceAt(VineNodeType.DamageTower, row[1]);
            await ctx.Wait(1.3f);
            a?.RecountCrew(); b?.RecountCrew();
            ctx.Assert(a != null && a.CrewLinks == 1 && Mathf.IsEqualApprox(a.CrewBonus, Constants.CREW_BONUS_PER_LINK * 2f), "roles/arcanist_crew_links_double",
                a == null ? "not placed" : $"links {a.CrewLinks}, bonus {a.CrewBonus:P0}");

            // A relay two cells from a turret reaches it, and buffs 50% harder
            var relay = placer.TestPlaceAt(VineNodeType.BuffEmitter, row[3]);
            await ctx.Wait(Constants.BUFF_EMITTER_PULSE_INTERVAL + 0.6f);
            ctx.Assert(relay != null && b.BuffStrength >= Constants.BUFF_EMITTER_STRENGTH * 1.5f - 0.01f, "roles/arcanist_relays_reach_and_hit_harder",
                $"buff {b.BuffStrength:F2} on the turret two cells off");

            // A Tesla Coil hits 25% harder than with no role
            var coilCell = FreeCells(grid, 1, avoid: row).FirstOrDefault();
            var coil = placer.TestPlaceAt(VineNodeType.TeslaCoil, coilCell);
            await ctx.Wait(0.2f);
            float with = coil?.CurrentDps ?? 0f;
            RoleRun.Reset();
            float without = coil?.CurrentDps ?? 0f;
            RoleRun.Apply("Arcanist");
            ctx.Assert(coil != null && without > 0f && Mathf.Abs(with / without - 1.25f) < 0.01f, "roles/arcanist_coils_hit_harder",
                $"{with:F1} vs {without:F1}");

            // Signature: the shield breaking stuns what's close
            var dummies = new List<VineEnemy>();
            for (int i = 0; i < 3; i++)
                dummies.Add(Dummy(ctx, grid, h.GlobalPosition + new Vector3(3f + i, 0f, 2f - i * 2f), 4000f));
            await ctx.Wait(0.1f);
            float hp = h.CurrentHP;
            if (h.ShieldUp) h.TakeDamage(10f);
            await ctx.Wait(0.05f);
            int stunned = dummies.Count(d => IsInstanceValid(d) && d.IsStunned);
            ctx.Assert(h.ShieldPulses >= 1 && stunned == 3 && h.CurrentHP >= hp - 0.01f, "roles/arcanist_shield_shocks_when_it_breaks",
                $"{h.ShieldPulses} shocks, {stunned}/3 stunned, HP {hp:F0} -> {h.CurrentHP:F0}");
            foreach (var d in dummies) if (IsInstanceValid(d)) d.QueueFree();
            await ctx.Wait(0.1f);
        }

        // ── Obelisk: BIT and the field ──

        private static async Task Obelisk(TestContext ctx)
        {
            var gm = GameManager.Instance;
            if (!await StartBattle(ctx, "Obelisk")) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var player = ServiceLocator.Get<VinePlayer>();
            var h = grid.Harvester;

            ctx.StartTest();
            float baseHp = SignalTuningEditor.PlayerMaxHP + SignalTuningEditor.PlayerMaxHPBonus;
            ctx.Assert(Mathf.IsEqualApprox(player.MaxHP, baseHp + 40f), "roles/obelisk_bit_tougher", $"{player.MaxHP:F0} vs {baseHp:F0}");
            float with = player.EffectiveDamage;
            RoleRun.Reset();
            float without = player.EffectiveDamage;
            RoleRun.Apply("Obelisk");
            ctx.Assert(without > 0f && Mathf.Abs(with / without - 1.3f) < 0.01f, "roles/obelisk_bit_hits_harder", $"{with:F1} vs {without:F1}");
            var shock = player.GetAbilities()?.FirstOrDefault();
            ctx.Assert(shock != null && Mathf.IsEqualApprox(shock.MaterialsCost, 15f * 0.7f), "roles/obelisk_abilities_cheaper", $"{shock?.MaterialsCost:F1} Materials");
            ctx.Assert(h.ExtraGunCount == 0 && !h.ShieldUp, "roles/obelisk_no_other_roles_kit", $"{h.ExtraGunCount} guns, shield {h.ShieldUp}");

            // Signature: the beam jumps on to two more enemies
            var dummies = new List<VineEnemy>();
            for (int i = 0; i < 3; i++)
                dummies.Add(Dummy(ctx, grid, h.GlobalPosition + new Vector3(8f + i * 2f, 0f, 1f), 50000f));
            // The beam fires in a wave
            var waves = ServiceLocator.Get<VineWaveManager>();
            waves.RequestNextWave();
            await ctx.WaitForPhase(GamePhase.Wave, 5f);
            int hits = h.BeamChainHits;
            float t0 = 0f;
            while (h.BeamChainHits < hits + 2 && t0 < 7f) { await ctx.Wait(0.1f); t0 += 0.1f; }
            int damaged = dummies.Count(d => IsInstanceValid(d) && d.CurrentHealth < d.MaxHealth);
            ctx.Assert(h.BeamChainHits >= hits + 2 && damaged == 3, "roles/obelisk_beam_chains",
                $"{h.BeamChainHits - hits} jumps in {t0:F1} s, {damaged}/3 hit");
            foreach (var d in dummies) if (IsInstanceValid(d)) d.QueueFree();
            await ctx.Wait(0.1f);
        }

        // ── Helpers ──

        private static void SetSite(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            Engine.TimeScale = 1.0;
            if (site == null) return;
            gm.CurrentPlanet = site.Planet;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
        }

        private static async Task<bool> StartBattle(TestContext ctx, string role)
        {
            var gm = GameManager.Instance;
            SetSite(ctx);
            gm.SelectedRole = role;
            gm.AvailableNodes = SpireData.Get(role)?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.StartTest();
            ctx.Assert(ok && RoleRun.Role == role, $"roles/{role.ToLower()}_battle_loads", $"run as {RoleRun.Role}");
            if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = true;
            await ctx.Wait(0.5f);
            return ok;
        }

        private static VineEnemy Dummy(TestContext ctx, VineGrid grid, Vector3 at, float hp)
        {
            var e = new VineEnemy();
            ctx.Tree.CurrentScene.AddChild(e);
            e.Initialize("Dummy", VineEnemyFaction.Scavenger, hp, 0f, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(at));
            e.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
            return e;
        }

        private static List<Vector2I> FreeCells(VineGrid grid, int count, List<Vector2I> avoid = null)
        {
            var list = new List<Vector2I>();
            int cx = grid.Width / 2, cy = grid.Height / 2;
            var pf = ServiceLocator.Get<VinePathfinder>();
            for (int r = 3; r < 14 && list.Count < count; r++)
                for (int dx = -r; dx <= r && list.Count < count; dx += 2)
                    foreach (int dy in new[] { -r, r })
                    {
                        var c = new Vector2I(cx + dx, cy + dy);
                        if (list.Count < count && grid.CanPlace(c) && !pf.WouldBlockAllPaths(c)
                            && !list.Concat(avoid ?? new List<Vector2I>()).Any(o => Mathf.Abs(o.X - c.X) + Mathf.Abs(o.Y - c.Y) < 3))
                            list.Add(c);
                    }
            return list;
        }

        /// <summary>A row of free cells side by side.</summary>
        private static List<Vector2I> FreeRow(VineGrid grid, int length)
        {
            var pf = ServiceLocator.Get<VinePathfinder>();
            for (int y = 3; y < grid.Height - 3; y++)
                for (int x = 3; x < grid.Width - length - 2; x++)
                {
                    var row = Enumerable.Range(0, length).Select(k => new Vector2I(x + k, y)).ToList();
                    if (row.All(c => grid.GetCell(c) == VineCellType.Empty && grid.CanPlace(c) && !pf.WouldBlockAllPaths(c))) return row;
                }
            return new List<Vector2I>();
        }

        private static bool IsInstanceValid(GodotObject o) => GodotObject.IsInstanceValid(o);
    }
}
