using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What 2026-11's design answers added: a site's difficulty dots scale enemy health; a boss
    /// fight has a boss with a trick (The Silencer silences towers near it, The Overloader burns
    /// them in bursts), it stays at the Spire and breaks it, and the fight is won when it dies;
    /// banked Resources buy looks at the Company Store; the last boss fight ends the game, and the
    /// ending begins a harder campaign with the perk tree, the bank and the store's items kept.
    /// </summary>
    public class EndgameTestSuite : ITestSuite
    {
        public string SuiteName => "endgame";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var oldSave = gm.MetaSave;
            bool autoFire = GameSettings.BitAutoFire;
            bool siteOff = Climb.SiteOff;
            string oldSite = gm.CurrentTerritorySectionId;
            SuitManager.UseInMemoryForTests();
            GameSettings.BitAutoFire = false;
            gm.SuppressAutoDebrief = true;
            try
            {
                Numbers(ctx);
                Store(ctx);
                Campaigns(ctx);
                await Screens(ctx);
                await SilencerFight(ctx);
                await OverloaderFight(ctx);
            }
            finally
            {
                gm.MetaSave = oldSave;
                gm.CurrentTerritorySectionId = oldSite;
                gm.SuppressAutoDebrief = false;
                gm.AutoResolvePerks = false;
                gm.EndingPending = false;
                GameSettings.BitAutoFire = autoFire;
                Climb.SiteOff = siteOff;
                Cosmetics.ClearPreview();
                SuitKit.Clear();
                SuitManager.RestoreFromDisk();
                Engine.TimeScale = 1.0;
                if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = false;
            }
        }

        private static IEnumerable<TerritorySite> AllSites()
        {
            foreach (int planet in new[] { 1, 2 })
                foreach (var region in TerritoryManager.GetPlanet(planet)?.Regions ?? new List<TerritoryRegion>())
                    foreach (var site in region.Sites) yield return site;
        }

        /// <summary>A save holding every site but those named.</summary>
        private static MetaPerkSaveData SaveWithout(params string[] notHeld)
        {
            var save = new MetaPerkSaveData();
            foreach (var site in AllSites())
                if (!notHeld.Contains(site.Id)) TerritoryManager.ClearSite(site.Id, save);
            return save;
        }

        // ── Dots, campaigns and bosses in numbers ──

        private static void Numbers(TestContext ctx)
        {
            var gm = GameManager.Instance;
            ctx.StartTest();
            var sites = AllSites().Where(s => !s.IsBossSite).ToList();
            int maxDots = TerritoryManager.MaxDots;
            bool rises = true;
            for (int d = 2; d <= maxDots; d++) if (Climb.DotsHp(d) < Climb.DotsHp(d - 1)) rises = false;
            ctx.Assert(Mathf.IsEqualApprox(Climb.DotsHp(1), 1f) && rises && Climb.DotsHp(maxDots) >= 1.2f, "site/dots_scale_enemy_health",
                string.Join(", ", Enumerable.Range(1, maxDots).Select(d => $"{d}: x{Climb.DotsHp(d):0.00}")));

            // Under the harness a site changes nothing (the other suites were written for the climb alone)
            var hard = sites.OrderByDescending(s => s.Difficulty).First();
            gm.CurrentTerritorySectionId = hard.Id;
            gm.CurrentPlanet = TerritoryManager.PlanetOfSite(hard.Id);
            float off = Climb.SiteHp;
            Climb.SiteOff = false;
            float on = Climb.SiteHp, hp = Climb.EnemyHp(10);
            ctx.Assert(Mathf.IsEqualApprox(off, 1f) && Mathf.IsEqualApprox(on, Climb.DotsHp(hard.Difficulty))
                && Mathf.Abs(hp - Climb.Hp(10) * Climb.Threat * Climb.PlanetHp * on * Climb.CampaignHp) < 0.001f,
                "site/enemies_carry_the_site", $"{hard.Name} ({hard.Difficulty} dots): x{on:0.00}; W10 x{hp:0.00}");
            string words = TerritoryManager.DifficultyWords(hard);
            ctx.Assert(words.Count(c => c == '●') == hard.Difficulty && words.Contains($"x{on:0.00}"), "site/map_says_what_dots_do", words);

            // A campaign played again is harder, up to a cap
            var old = gm.MetaSave;
            gm.MetaSave = new MetaPerkSaveData { CampaignsCompleted = 1 };
            float second = Climb.CampaignHp;
            gm.MetaSave = old;
            ctx.Assert(Mathf.IsEqualApprox(Climb.CampaignHpFor(0), 1f) && second > 1.05f && Mathf.IsEqualApprox(second, Climb.CampaignHpFor(1))
                && Climb.CampaignHpFor(2) > second && Climb.CampaignHpFor(1000) <= Climb.CampaignHpFor(999) + 0.001f,
                "campaign/again_is_harder", $"II x{second:0.00}, III x{Climb.CampaignHpFor(2):0.00}, cap x{Climb.CampaignHpFor(1000):0.00}");
            Climb.SiteOff = true;

            // Every boss fight has a boss, with words, a model and a rig
            var bossSites = AllSites().Where(s => s.IsBossSite).ToList();
            var problems = new List<string>();
            foreach (var site in bossSites)
            {
                var b = SiteBosses.ForSite(site);
                if (b == null) { problems.Add($"{site.Id}: no boss '{site.BossId}'"); continue; }
                if (string.IsNullOrWhiteSpace(b.Name) || b.WhatWords.Length < 10 || b.WhatWords.Contains('{') || b.AnswerWords.Contains('{')) problems.Add($"{b.Id}: words '{b.WhatWords}'");
                if (!b.Jams && !b.Overloads) problems.Add($"{b.Id}: trick '{b.Trick}'");
                if (string.IsNullOrEmpty(b.Model) || !ResourceLoader.Exists(b.Model)) problems.Add($"{b.Id}: model {b.Model}");
                if (TowerSheet.Load(b.Rig) == null) problems.Add($"{b.Id}: rig {b.Rig}");
                if (b.Hp <= 0 || b.Radius <= 0 || b.SpireHitShare <= 0) problems.Add($"{b.Id}: numbers");
                // A boss can't share a tower's name (the Arcanist builds a Signal Jammer)
                if (System.Enum.GetValues<VineNodeType>().Any(t => VineNodeRegistry.Get(t)?.Name is { } n && b.Name.Contains(n, System.StringComparison.OrdinalIgnoreCase)))
                    problems.Add($"{b.Id}: named like a tower ({b.Name})");
                if (!TerritoryManager.BossGoal(site).Contains(b.Name)) problems.Add($"{b.Id}: goal '{TerritoryManager.BossGoal(site)}'");
            }
            ctx.Assert(bossSites.Count >= 2 && problems.Count == 0, "boss/every_boss_fight_has_a_boss",
                problems.Count == 0 ? string.Join(", ", bossSites.Select(s => $"{s.Name}: {SiteBosses.ForSite(s)?.Name}")) : string.Join("; ", problems));
        }

        // ── The Company Store's rules ──

        private static void Store(TestContext ctx)
        {
            ctx.StartTest();
            var items = Cosmetics.All;
            var slots = Cosmetics.Slots.Select(s => s.Id).ToList();
            var bad = new List<string>();
            foreach (var i in items)
            {
                if (!slots.Contains(i.Slot)) bad.Add($"{i.Id}: slot {i.Slot}");
                if (i.Price <= 0 || string.IsNullOrWhiteSpace(i.Name) || string.IsNullOrWhiteSpace(i.Line)) bad.Add($"{i.Id}: price or words");
                if (i.Slot == Cosmetics.Paint && (i.HullDark == null || i.HullLight == null || i.Glow == null)) bad.Add($"{i.Id}: a paint needs hull_dark, hull_light and glow");
                if (i.Slot != Cosmetics.Paint && i.Rim == null) bad.Add($"{i.Id}: needs rim");
                if (i.Slot == Cosmetics.Spire && TowerSheet.Load(i.Crown) == null) bad.Add($"{i.Id}: crown {i.Crown}");
                // Red and magenta are the enemies' colours
                var c = i.Swatch;
                if (c.R > 0.75f && c.G < 0.4f) bad.Add($"{i.Id}: its light reads as an enemy's");
                if (i.Line.Contains('\u2014')) bad.Add($"{i.Id}: dash");
            }
            ctx.Assert(items.Count >= 12 && items.Select(i => i.Id).Distinct().Count() == items.Count && slots.Count == 3 && bad.Count == 0
                && slots.All(s => Cosmetics.In(s).Count() >= 3), "store/catalogue_is_sound",
                bad.Count == 0 ? $"{items.Count} items: " + string.Join(", ", slots.Select(s => $"{s} {Cosmetics.In(s).Count()}")) : string.Join("; ", bad));

            // Buying: the bank pays, the item is owned and worn; nothing is bought twice or without the price
            var cheap = Cosmetics.In(Cosmetics.Paint).OrderBy(i => i.Price).First();
            var gated = items.FirstOrDefault(i => i.RequiresCampaigns > 0);
            var save = new MetaPerkSaveData { MetaResources = cheap.Price - 1 };
            string shortBy = Cosmetics.WhyNot(save, cheap);
            bool refused = !Cosmetics.Buy(save, cheap) && save.OwnedCosmetics.Count == 0;
            save.MetaResources = cheap.Price + 500;
            bool bought = Cosmetics.Buy(save, cheap);
            bool twice = Cosmetics.Buy(save, cheap);
            ctx.Assert(shortBy != null && shortBy.Contains("short") && refused && bought && !twice && save.MetaResources == 500
                && Cosmetics.Owns(save, cheap.Id) && Cosmetics.Worn(save, Cosmetics.Paint) == cheap,
                "store/the_bank_buys_it", $"{cheap.Name} for {cheap.Price}: '{shortBy}', then bought, {save.MetaResources} left, worn {Cosmetics.Worn(save, Cosmetics.Paint)?.Name}");

            save.MetaResources = 1000000;
            string gate = gated != null ? Cosmetics.WhyNot(save, gated) : null;
            save.CampaignsCompleted = gated?.RequiresCampaigns ?? 0;
            ctx.Assert(gated != null && gate != null && gate.Contains("campaign") && Cosmetics.WhyNot(save, gated) == null, "store/some_ask_for_a_campaign",
                gated == null ? "none" : $"{gated.Name}: '{gate}', open after {gated.RequiresCampaigns}");

            // Wearing: an owned item goes on and comes off; one not owned doesn't go on
            var other = Cosmetics.In(Cosmetics.Paint).First(i => i != cheap);
            bool notOwned = !Cosmetics.Wear(save, Cosmetics.Paint, other);
            bool off = Cosmetics.Wear(save, Cosmetics.Paint, null) && Cosmetics.Worn(save, Cosmetics.Paint) == null;
            bool on = Cosmetics.Wear(save, Cosmetics.Paint, cheap) && Cosmetics.Worn(save, Cosmetics.Paint) == cheap;
            // A worn id that isn't owned (a save edited by hand) wears nothing
            save.WornCosmetics[Cosmetics.Trim] = Cosmetics.In(Cosmetics.Trim).First().Id;
            Cosmetics.Changed();
            ctx.Assert(notOwned && off && on && Cosmetics.Worn(save, Cosmetics.Trim) == null, "store/wear_what_you_own",
                $"not owned refused {notOwned}, off {off}, on {on}");

            // It is all in the save
            bool writes = MetaPerkSave.SuppressWrites;
            MetaPerkSave.SuppressWrites = false;
            save.WornCosmetics.Remove(Cosmetics.Trim);
            save.CampaignStartRun = 7;
            MetaPerkSave.Save(save);
            var back = MetaPerkSave.Load();
            MetaPerkSave.SuppressWrites = writes;
            ctx.Assert(back.OwnedCosmetics.SequenceEqual(save.OwnedCosmetics) && back.WornCosmetics.TryGetValue(Cosmetics.Paint, out var wornId) && wornId == cheap.Id
                && back.CampaignsCompleted == save.CampaignsCompleted && back.CampaignStartRun == 7 && back.MetaResources == save.MetaResources,
                "store/saved_and_loaded", $"owned {string.Join(",", back.OwnedCosmetics)}, worn {string.Join(",", back.WornCosmetics.Select(kv => kv.Key + "=" + kv.Value))}, campaigns {back.CampaignsCompleted}");
        }

        // ── The end, and the campaign after it ──

        private static void Campaigns(TestContext ctx)
        {
            ctx.StartTest();
            var all = SaveWithout();
            var notYet = SaveWithout(AllSites().Last(s => s.IsBossSite).Id);
            ctx.Assert(NewCampaign.EndingDue(all) && !NewCampaign.EndingDue(notYet) && !NewCampaign.EndingDue(new MetaPerkSaveData())
                && !NewCampaign.Begin(notYet) && notYet.CampaignsCompleted == 0, "ending/due_when_the_last_boss_is_down",
                $"all held {NewCampaign.EndingDue(all)}, last boss standing {NewCampaign.EndingDue(notYet)}");

            var view = TerritoryManager.Campaign(all, 2);
            ctx.Assert(view.Complete && view.Line.Contains("Campaign I ") && view.Line.Contains("Campaign II") && !view.Line.Contains("played again")
                && TerritoryScreen.NextStepText(all, 1) == "The ending  ▶", "ending/the_hub_and_the_map_point_at_it", $"{view.Line} | map: {TerritoryScreen.NextStepText(all, 1)}");

            // The next campaign: the sites are given back, everything else stays, enemies are tougher
            all.MetaResources = 4321;
            all.AvailablePoints = 3;
            all.RunCount = 25;
            all.OwnedCosmetics.Add("paint_ember");
            all.WornCosmetics[Cosmetics.Paint] = "paint_ember";
            all.ClearedMilestones["P1-W10"] = 1;
            var perk = MetaPerkRegistry.GetAll().First();
            all.Ranks[perk.Id] = 1;
            var stats = NewCampaign.Stats(all);
            bool began = NewCampaign.Begin(all);
            var first = TerritoryManager.Campaign(all, 1);
            ctx.Assert(began && all.CampaignsCompleted == 1 && all.ClearedSites.Count == 0 && all.ConqueredRegions.Count == 0 && all.CampaignStartRun == 25
                && all.MetaResources == 4321 && all.AvailablePoints == 3 && all.Ranks[perk.Id] == 1 && all.OwnedCosmetics.Contains("paint_ember")
                && all.ClearedMilestones.Count == 1 && first.Site != null && first.Site.Id == AllSites().First().Id && !first.Complete
                && NewCampaign.Name(all) == "Campaign II" && !TerritoryManager.IsPlanetAccessible(2, all),
                "ending/next_campaign_keeps_what_was_earned", $"{NewCampaign.Name(all)}: {all.ClearedSites.Count} sites held, bank {all.MetaResources}, next {first.Site?.Name}; stats {string.Join(", ", stats.Select(s => $"{s.label} {s.value}"))}");
            ctx.Assert(NewCampaign.Roman(1) == "I" && NewCampaign.Roman(4) == "IV" && NewCampaign.Roman(9) == "IX" && NewCampaign.Roman(14) == "XIV" && NewCampaign.Roman(40) == "XL",
                "ending/campaigns_are_numbered", $"{NewCampaign.Roman(2)}, {NewCampaign.Roman(4)}, {NewCampaign.Roman(14)}");

            // The last words: both voices, nothing left unfilled, different the second time
            var lines1 = EndingScreen.EndingLines(new MetaPerkSaveData { RunCount = 21 });
            var lines2 = EndingScreen.EndingLines(new MetaPerkSaveData { RunCount = 44, CampaignsCompleted = 1 });
            ctx.Assert(lines1.Count >= 4 && lines2.Count >= 3 && lines1.Concat(lines2).All(l => l.Length == 2 && !l[1].Contains('{') && !l[1].Contains('\u2014'))
                && lines1.Any(l => l[0] == Comms.Bit) && lines1.Any(l => l[0] == Comms.Axis) && lines1[0][1] != lines2[0][1]
                && lines1.Any(l => l[1].Contains("848")) && lines2.Any(l => l[1].Contains("849")),
                "ending/last_words", string.Join(" / ", lines1.Select(l => l[1])));
        }

        // ── The ending, the store and the map as screens ──

        private static async Task Screens(TestContext ctx)
        {
            var gm = GameManager.Instance;
            bool writes = MetaPerkSave.SuppressWrites;
            MetaPerkSave.SuppressWrites = true;
            try
            {
                // The campaign won in the run just played: the ending comes before the Command Center
                var all = SaveWithout();
                all.MetaResources = 50000;
                all.RunCount = 21;
                gm.MetaSave = all;
                gm.EndingPending = true;
                gm.ShowMetaHub();
                await ctx.Wait(0.8f);
                var ending = ctx.FindNode<EndingScreen>();
                ctx.StartTest();
                ctx.Assert(ending != null && ending.LineCount >= 4 && !ending.AllShown && ending.ButtonText.Contains("CAMPAIGN II") && !gm.EndingPending,
                    "ending/comes_after_the_last_boss", ending == null ? "no ending screen" : $"{ending.LineCount} lines, '{ending.ButtonText}'");
                if (ending != null)
                {
                    var button = ending.FindChild("BeginButton", true, false) as Button;
                    bool locked = button != null && button.Disabled;
                    ending.ShowAll();
                    var labels = ending.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList();
                    ctx.Assert(locked && ending.AllShown && button != null && !button.Disabled && labels.Any(t => t.Contains("CAMPAIGN I COMPLETE"))
                        && labels.Any(t => t.Contains("more health")) && labels.Any(t => t == "50,000"),
                        "ending/says_what_follows", string.Join(" | ", labels.Where(t => t.Length > 30).Take(3)));
                    ending.BeginNext();
                    await ctx.Wait(0.8f);
                    var hub = ctx.FindNode<MetaHubScreen>();
                    var hubText = hub?.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList() ?? new List<string>();
                    ctx.Assert(hub != null && all.CampaignsCompleted == 1 && all.ClearedSites.Count == 0 && hubText.Any(t => t.Contains("Campaign II") && t.Contains("50,000")),
                        "ending/begins_the_next_campaign", hub == null ? "no hub" : string.Join(" | ", hubText.Take(4)));
                }

                // A campaign that stands complete (an older save, or the game closed on the ending): Deploy is the ending
                gm.MetaSave = SaveWithout();
                gm.ShowMetaHub();
                await ctx.Wait(0.6f);
                MetaHubScreen.DeployNext();
                await ctx.Wait(0.8f);
                ctx.Assert(ctx.FindNode<EndingScreen>() != null, "ending/deploy_on_a_finished_campaign", "the Command Center's Deploy");

                // The store: an item is bought on the second press, and is on
                var save = new MetaPerkSaveData { MetaResources = 30000 };
                gm.MetaSave = save;
                gm.ShowCompanyStore();
                await ctx.Wait(1.0f);
                var store = ctx.FindNode<CompanyStoreScreen>();
                var cheap = Cosmetics.In(Cosmetics.Paint).OrderBy(i => i.Price).First();
                var gated = Cosmetics.All.First(i => i.RequiresCampaigns > 0);
                var buy = store?.ButtonFor(cheap.Id);
                ctx.Assert(store != null && buy != null && buy.Text == "BUY" && !buy.Disabled && store.BankText.Contains("30,000")
                    && store.ButtonFor(gated.Id) is { Disabled: true } g && g.Text.Contains("campaign")
                    && store.ButtonFor("standard:paint") is { Disabled: true, Text: "WORN" },
                    "store/screen_shows_the_shelves", store == null ? "no store" : $"'{buy?.Text}', bank '{store.BankText}', {gated.Name}: '{store.ButtonFor(gated.Id)?.Text}'");
                if (store != null && buy != null)
                {
                    buy.EmitSignal(BaseButton.SignalName.Pressed);
                    await ctx.Wait(0.2f);
                    var confirm = store.ButtonFor(cheap.Id);
                    bool asked = confirm != null && confirm.Text.StartsWith("CONFIRM") && save.OwnedCosmetics.Count == 0;
                    confirm?.EmitSignal(BaseButton.SignalName.Pressed);
                    await ctx.Wait(0.3f);
                    ctx.Assert(asked && Cosmetics.Owns(save, cheap.Id) && save.MetaResources == 30000 - cheap.Price && store.ButtonFor(cheap.Id)?.Text == "TAKE OFF"
                        && store.BankText.Contains((30000 - cheap.Price).ToString("N0")) && Cosmetics.BitPaint == cheap,
                        "store/buy_asks_once_more", $"asked {asked}, owned {Cosmetics.Owns(save, cheap.Id)}, bank {save.MetaResources}, button '{store.ButtonFor(cheap.Id)?.Text}'");
                    // The stage: BIT, a tower and the Spire, repainted when something changes
                    var stage = store.Preview;
                    int before = stage.Rebuilds;
                    var crown = Cosmetics.In(Cosmetics.Spire).First();
                    Cosmetics.SetPreview(Cosmetics.Spire, crown);
                    stage.Rebuild();
                    await ctx.Wait(0.2f);
                    bool crowned = stage.Spire?.FindChild("SpireCrown", true, false) != null;
                    Cosmetics.SetPreview(Cosmetics.Spire, null);
                    stage.Rebuild();
                    await ctx.Wait(0.2f);
                    ctx.Assert(stage.Bit != null && stage.Bit.FindChildren("*", "MeshInstance3D", true, false).Count > 0 && stage.Tower?.GetChildCount() > 0
                        && stage.Spire?.GetChildCount() > 0 && crowned && stage.Spire.FindChild("SpireCrown", true, false) == null && stage.Rebuilds >= before + 2,
                        "store/stage_wears_what_is_pointed_at", $"BIT {stage.Bit?.GetChildCount()} parts, crown shown {crowned}, {stage.Rebuilds} repaints");
                }

                // The map says what a site's dots do
                gm.MetaSave = new MetaPerkSaveData();
                gm.CurrentPlanet = 1;
                gm.ShowTerritory();
                await ctx.Wait(0.8f);
                var map = ctx.FindNode<TerritoryScreen>();
                var mapText = map?.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList() ?? new List<string>();
                ctx.Assert(mapText.Count(t => t.Contains("enemy health x")) >= 3 && !mapText.Any(t => t.Contains("[Diff")) && mapText.Any(t => t.Contains("More dots")),
                    "site/map_shows_enemy_health", string.Join(" | ", mapText.Where(t => t.Contains("enemy health")).Take(2)));
            }
            finally { MetaPerkSave.SuppressWrites = writes; }
        }

        // ── A boss fight ──

        private static async Task<bool> StartBoss(TestContext ctx, TerritorySite boss, MetaPerkSaveData save, string test)
        {
            var gm = GameManager.Instance;
            Engine.TimeScale = 1.0;
            gm.MetaSave = save;
            gm.SelectedRole = "Bruteforge";
            gm.AutoResolvePerks = true;
            gm.StartBossRun(TerritoryManager.PlanetOfSite(boss.Id), -1, boss.Id);
            await ctx.Wait(1.0f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.StartTest();
            var m = SiteBossManager.Current;
            ctx.Assert(ok && gm.IsBossRun && m?.Profile != null && m.BossWave == boss.BossWave && m.Boss == null && !m.Spawned, test,
                m?.Profile == null ? "no boss for this fight" : $"{m.Profile.Name} comes with wave {m.BossWave}");
            if (!ok || m?.Profile == null) return false;
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            await ctx.Wait(2.5f); // BIT climbs out of the Spire
            return true;
        }

        private static VineNode Tower(VineGrid grid, Vector2I cell, VineNodeType type = VineNodeType.DamageTower)
        {
            var node = new VineNode();
            node.Initialize(VineNodeRegistry.Get(type));
            grid.PlaceNode(node, cell);
            return node;
        }

        /// <summary>
        /// Three cells beside the route close enough together for one boss to reach them all from
        /// the route cell between them, the route cell, and a cell far outside that reach.
        /// </summary>
        private static (List<Vector2I> near, Vector2I at, Vector2I far)? PickCells(VineGrid grid, IList<Vector2I> route, float radius)
        {
            var pool = TestCells.BesideRoute(grid, route, 60, skip: Mathf.Max(2, route.Count / 4));
            foreach (var at in route.Skip(Mathf.Max(2, route.Count / 4)))
            {
                var here = grid.GridToWorld(at);
                var near = pool.Where(c => Flat(grid.GridToWorld(c), here) <= radius - 2.5f).OrderBy(c => Flat(grid.GridToWorld(c), here)).Take(3).ToList();
                if (near.Count < 3) continue;
                var far = pool.Where(c => Flat(grid.GridToWorld(c), here) >= radius + 5f).OrderByDescending(c => Flat(grid.GridToWorld(c), here)).ToList();
                if (far.Count == 0) continue;
                return (near, at, far[0]);
            }
            return null;
        }

        private static Vector3 Ground(VineGrid grid, Vector3 p) => new(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

        private static async Task SilencerFight(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var boss = TerritoryManager.GetPlanet(1).Regions.SelectMany(r => r.Sites).First(s => s.IsBossSite);
            var save = SaveWithout(AllSites().Where(s => TerritoryManager.PlanetOfSite(s.Id) == 2).Select(s => s.Id).Append(boss.Id).ToArray());
            if (!await StartBoss(ctx, boss, save, "boss/fight_has_a_boss_waiting")) return;
            var m = SiteBossManager.Current;
            var p = m.Profile;
            var grid = ServiceLocator.Get<VineGrid>();
            var pf = ServiceLocator.Get<VinePathfinder>();
            var spire = grid.Harvester;
            spire.IncreaseMaxHP(100000f);

            // The words before it comes: the objective and the wave card
            string objective = gm.ObjectiveLine(boss.BossWave - 2);
            string far = SiteBossManager.CardLine(boss.BossWave - p.WarnWaves - 2), near = SiteBossManager.CardLine(boss.BossWave - 1);
            ctx.Assert(objective.Contains(p.Name) && objective.Contains($"wave {boss.BossWave}") && far == null && near != null && near.Contains(p.Name) && near.Contains("cells"),
                "boss/announced_before_it_comes", $"{objective} | {near}");

            // Towers along the route, well out from the Spire, and one far from all of them
            var route = pf.GetCachedPath(grid.ActiveEntryRegions[0].Center) ?? new List<Vector2I>();
            var picked = PickCells(grid, route, p.Radius);
            ctx.Assert(picked != null, "boss/room_for_towers", $"a route of {route.Count} cells");
            if (picked == null) return;
            var towers = picked.Value.near.Select(c => Tower(grid, c)).ToList();
            var farTower = Tower(grid, picked.Value.far);
            await ctx.Wait(0.3f);

            // It comes: named, in its colour, its rig on its back, with the climb's health
            var e = m.Spawn();
            await ctx.Wait(0.4f);
            float wantHp = SiteBossManager.HealthFor(p, Mathf.Max(1, gm.CurrentWave));
            ctx.Assert(e != null && e.IsSiteBoss && e.IsBoss && e.IsNamedBoss && e.EnemyName == p.Name && e.BossRig != null && e.BossRig.GetChildCount() > 0
                && Mathf.Abs(e.MaxHealth - wantHp) < 1f && e.FindChild("AscendantName", true, false) is Label3D tag && tag.Text == p.Name.ToUpperInvariant()
                && m.Bar != null && m.Spawned && SiteBossManager.CardLine(gm.CurrentWave + 1)?.Contains("on the field") == true,
                "boss/arrives_named", e == null ? "not spawned" : $"{e.EnemyName}, {e.MaxHealth:F0} health (want {wantHp:F0}), rig {e.BossRig?.GetChildCount()}");
            if (e == null) return;

            // Beside the towers: they go dark and stop firing; the far one fires on
            var routeNear = picked.Value.at;
            e.BaseSpeed = 0f;
            e.GlobalPosition = Ground(grid, grid.GridToWorld(routeNear));
            await ctx.Wait(0.7f);
            bool dark = towers.All(t => t.IsSilenced), farFires = !farTower.IsSilenced && Flat(farTower.GlobalPosition, e.GlobalPosition) > p.Radius;
            float hpBefore = e.CurrentHealth;
            await ctx.Wait(2.0f);
            float lost = hpBefore - e.CurrentHealth;
            ctx.Assert(dark && farFires && m.SilencedNow >= 3 && m.BarState.Contains("silenced") && lost <= 0.01f
                && towers[0].FindChild("JamLabel", true, false) is Label3D { Visible: true },
                "boss/silences_towers_near_it", $"{m.SilencedNow} silenced ({string.Join(",", towers.Select(t => t.IsSilenced))}), far tower silenced {farTower.IsSilenced} at {Flat(farTower.GlobalPosition, e.GlobalPosition):F1}; boss lost {lost:F1} in 2 s; bar '{m.BarState}'");

            // A stun drops the field for as long as it holds
            e.ApplyStun(6f);
            await ctx.Wait(0.9f);
            bool lifted = towers.All(t => !t.IsSilenced) && m.BarState.Contains("stunned");
            await ctx.Wait(1.2f);
            float lostStunned = hpBefore - e.CurrentHealth;
            ctx.Assert(lifted && lostStunned > 0.5f, "boss/a_stun_drops_the_field", $"field down {lifted}, towers took {lostStunned:F0} off it");
            await ctx.Wait(1.6f); // the stun ends (bosses take a share of it)

            // Walked off: the towers come back and shoot it
            e.GlobalPosition = Ground(grid, grid.GridToWorld(route[Mathf.Min(route.Count - 1, 1)]));
            float away = towers.Min(t => Flat(t.GlobalPosition, e.GlobalPosition));
            await ctx.Wait(1.0f);
            ctx.Assert(away <= p.Radius || towers.All(t => !t.IsSilenced && t.FindChild("JamLabel", true, false) is Label3D { Visible: false }),
                "boss/towers_come_back", $"boss {away:F1} from the nearest of them");

            // At the Spire it stays and breaks it: not a leak, not gone
            int leaksBefore = RunRecord.Leaks;
            float spireBefore = spire.CurrentHP;
            foreach (var t in towers.Append(farTower)) t.TakeDamage(100000f); // nothing shoots it while this is measured
            e.BaseSpeed = p.Speed;
            e.GlobalPosition = Ground(grid, grid.GridToWorld(grid.ExitPoint) + new Vector3(0.9f, 0, 0));
            bool there = await ctx.WaitUntil(() => e.AtSpire, 8f);
            await ctx.Wait(p.SpireHitInterval * 2.6f);
            float took = spireBefore - spire.CurrentHP;
            float perHit = spire.MaxHP * p.SpireHitShare;
            ctx.Assert(there && GodotObject.IsInstanceValid(e) && e.IsAlive && RunRecord.Leaks == leaksBefore && took >= perHit * 1.9f && took <= perHit * 4.1f
                && e.SpireDamageDone > 0f && m.BarState.Contains("AT THE SPIRE") && gm.ObjectiveLine(gm.CurrentWave) == $"Kill {p.Name}",
                "boss/stays_at_the_spire_and_breaks_it", $"at the Spire {there}, Spire -{took:F0} ({perHit:F0} a hit), leaks {RunRecord.Leaks - leaksBefore}; '{m.BarState}'");

            // It dies: the fight is won there and then, and the planet is taken
            e.TakeDamage(e.MaxHealth * 3f);
            await ctx.Wait(0.5f);
            var words = TerritoryManager.BossWinWords(boss.Id, true, boss.RewardResources, save);
            ctx.Assert(m.Defeated && gm.CurrentPhase == GamePhase.Victory && TerritoryManager.IsSiteCleared(boss.Id, save) && !gm.EndingPending
                && words.Kicker == "Planet taken" && words.Detail.Contains($"{p.Name} is destroyed") && words.Detail.Contains("is open")
                && RunRecord.Headline(true, true, gm.CurrentWave).Contains(p.Name) && (m.Bar == null || !m.Bar.IsVisibleInTree()),
                "boss/killing_it_wins_the_fight", $"{gm.CurrentPhase}; {words.Kicker}: {words.Detail} | {RunRecord.Headline(true, true, gm.CurrentWave)}");
        }

        private static async Task OverloaderFight(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var boss = AllSites().Last(s => s.IsBossSite);
            var save = SaveWithout(boss.Id);
            if (!await StartBoss(ctx, boss, save, "boss/last_fight_has_its_boss")) return;
            var m = SiteBossManager.Current;
            var p = m.Profile;
            var grid = ServiceLocator.Get<VineGrid>();
            var pf = ServiceLocator.Get<VinePathfinder>();
            var bit = ServiceLocator.Get<VinePlayer>();
            grid.Harvester.IncreaseMaxHP(100000f);
            ctx.Assert(p.Overloads, "boss/the_last_one_overloads", p.WhatWords);
            if (!p.Overloads) return;

            var route = pf.GetCachedPath(grid.ActiveEntryRegions[0].Center) ?? new List<Vector2I>();
            var picked = PickCells(grid, route, p.Radius);
            if (picked == null) { ctx.Assert(false, "boss/overload_burns_towers", "no room for towers"); return; }
            var towers = picked.Value.near.Select(c => Tower(grid, c)).ToList();
            var farTower = Tower(grid, picked.Value.far);
            await ctx.Wait(0.3f);

            var e = m.Spawn();
            await ctx.Wait(0.3f);
            if (e == null) { ctx.Assert(false, "boss/overload_burns_towers", "not spawned"); return; }
            e.BaseSpeed = 0f;
            e.GlobalPosition = Ground(grid, grid.GridToWorld(picked.Value.at));
            // BIT stands in the ring for the first burst
            bit.GlobalPosition = Ground(grid, e.GlobalPosition + new Vector3(4.5f, 0, 0)); // in the ring, out of its touch
            await ctx.Wait(0.2f);

            // It charges by itself, says so, and the burst lands when the count is done
            Engine.TimeScale = 4.0;
            bool charging = await ctx.WaitUntil(() => m.ChargeLeft > 0f, p.Interval / 4f + 4f);
            string chargingBar = m.BarState;
            float bitBefore = bit.CurrentHP;
            var before = towers.Select(t => t.NodeCurrentHealth).ToList();
            bit.GlobalPosition = Ground(grid, e.GlobalPosition + new Vector3(4.5f, 0, 0)); // in the ring, out of its touch
            bool burst = await ctx.WaitUntil(() => m.Bursts >= 1, p.Charge / 4f + 4f);
            Engine.TimeScale = 1.0;
            await ctx.Wait(0.1f);
            bool burned = towers.Select((t, i) => Mathf.Abs((before[i] - t.NodeCurrentHealth) - t.NodeMaxHealth * p.TowerShare) < t.NodeMaxHealth * 0.08f).All(x => x);
            float bitLost = bitBefore - bit.CurrentHP;
            ctx.Assert(charging && chargingBar.Contains("OVERLOAD") && burst && burned && Mathf.IsEqualApprox(farTower.NodeCurrentHealth, farTower.NodeMaxHealth)
                && bitLost >= bit.MaxHP * p.BitShare * 0.6f && m.TowersBurned >= 3,
                "boss/overload_burns_towers", $"charged {charging} ('{chargingBar}'), burst {burst}: towers at {string.Join(", ", towers.Select(t => $"{t.NodeCurrentHealth:F0}/{t.NodeMaxHealth:F0}"))}, far {farTower.NodeCurrentHealth:F0}; BIT -{bitLost:F0} of {bit.MaxHP:F0}");

            // A second burst on the same towers wrecks them; out of the ring, BIT takes nothing
            bit.GlobalPosition = Ground(grid, e.GlobalPosition + new Vector3(p.Radius + 6f, 0, 0));
            await ctx.Wait(0.1f);
            float bitOut = bit.CurrentHP;
            m.Burst(e);
            await ctx.Wait(0.2f);
            ctx.Assert(towers.All(t => t.IsWrecked) && !farTower.IsWrecked && m.TowersWrecked >= 3 && Losses.Lost(TowerLoss.Boss) >= 3 && bit.CurrentHP >= bitOut - 0.01f,
                "boss/second_burst_wrecks", $"wrecked {towers.Count(t => t.IsWrecked)} of 3 (ledger {Losses.Lost(TowerLoss.Boss)}), BIT out of the ring -{bitOut - bit.CurrentHP:F0}");

            // The last boss down: the campaign is won, and the ending is next
            e.TakeDamage(e.MaxHealth * 3f);
            await ctx.Wait(0.5f);
            var words = TerritoryManager.BossWinWords(boss.Id, true, boss.RewardResources, save);
            ctx.Assert(m.Defeated && gm.CurrentPhase == GamePhase.Victory && NewCampaign.EndingDue(save) && gm.EndingPending && words.Kicker == "Campaign complete",
                "boss/the_last_one_ends_the_campaign", $"{gm.CurrentPhase}, ending pending {gm.EndingPending}; {words.Kicker}: {words.Detail}");
        }
    }
}
