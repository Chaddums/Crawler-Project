using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Loot crates (Data/loot_boxes.json). The file: five tiers in order with sheets, sequences and
    /// lines, drop tables naming real tiers. Rolls: amounts inside their range and growing with the
    /// wave, guaranteed lines always there, each source's tiers by wave, copies salvaged. In a real
    /// battle: commanders, bosses and Ascendants drop crates where they die (plain enemies, stuck
    /// ones and a failed roll don't, and no relic drops straight from a commander any more); a crate
    /// lands on open ground, floats with its name, Gold and up under a light pillar; a supply drop
    /// falls out of the sky far from the Spire; BIT walking over a crate takes it; the HUD card
    /// counts them. Opening: not during a wave; O opens Scrap and Supply crates together and each
    /// bigger cache on its own, with the battle paused; each sequence bursts on time (pop, rays,
    /// shatter with its pieces, ascend with three rings), the contents slide in, skip shows them at
    /// once, collecting pays exactly what was shown, and the last one unpauses. Space, Escape and
    /// the HUD's button work (Escape doesn't open the pause menu). The run ending opens what BIT
    /// carries, a ceremony still up included. Relics, suits and perk points stay in memory.
    /// "loot-sheets" also renders the five crates in the field and each sequence at its key moments
    /// to test-reports/loot/ (needs a display).
    /// </summary>
    public class LootTestSuite : ITestSuite
    {
        private readonly bool _shots;
        public LootTestSuite(bool shots = false) { _shots = shots; }
        public string SuiteName => _shots ? "loot-sheets" : "loot";

        private string _announced;

        public async Task Run(TestContext ctx)
        {
            if (_shots && DisplayServer.GetName() == "headless")
            {
                ctx.StartTest();
                ctx.Assert(false, "loot-sheets/needs_display", "Run with a display (Xvfb)");
                return;
            }
            var gm = GameManager.Instance;
            var oldSave = gm.MetaSave;
            bool autoFire = GameSettings.BitAutoFire;
            System.Action<string> listen = t => _announced = t;
            RelicInventory.UseInMemoryForTests();
            SuitManager.UseInMemoryForTests();
            MetaPerkSave.SuppressWrites = true;
            GameSettings.BitAutoFire = false;
            gm.SuppressAutoDebrief = true;
            gm.MetaSave = new MetaPerkSaveData();
            try
            {
                FileChecks(ctx);
                if (!await StartRun(ctx)) return;
                GameEvents.OnAnnouncement += listen;
                Rolls(ctx);
                var crates = await Drops(ctx);
                await Field(ctx, crates);
                if (_shots) await FieldShots(ctx);
                await Pickup(ctx);
                await NotInAWave(ctx);
                await Opening(ctx);
                await EveryTier(ctx);
                if (_shots) await SequenceShots(ctx);
                await RealInput(ctx);
                await RunEnd(ctx);
            }
            finally
            {
                GameEvents.OnAnnouncement -= listen;
                LootTable.ForceDropChance = null;
                gm.MetaSave = oldSave;
                gm.SuppressAutoDebrief = false;
                gm.AutoResolvePerks = false;
                GameSettings.BitAutoFire = autoFire;
                MetaPerkSave.SuppressWrites = false;
                RelicInventory.RestoreFromDisk();
                SuitManager.RestoreFromDisk();
                Engine.TimeScale = 1.0;
                if (ctx.Tree.Paused && !LootCeremony.IsOpen) ctx.Tree.Paused = false;
                if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = false;
            }
        }

        // ── The file ──

        private static void FileChecks(TestContext ctx)
        {
            ctx.StartTest();
            LootTable.Reload();
            var tiers = LootTable.Tiers;
            ctx.Assert(LootTable.Loaded && tiers.Select(t => t.Id).SequenceEqual(new[] { "bronze", "silver", "gold", "diamond", "legendary" }),
                "loot/tiers_in_order", string.Join(", ", tiers.Select(t => $"{t.Id} {t.Name}")));
            ctx.Assert(tiers.All(t => TowerSheet.Load(t.Sheet) != null), "loot/every_tier_has_a_sheet",
                string.Join(", ", tiers.Where(t => TowerSheet.Load(t.Sheet) == null).Select(t => t.Sheet)));
            var seqs = new[] { "pop", "rays", "shatter", "ascend" };
            ctx.Assert(tiers.All(t => seqs.Contains(t.Sequence)) && seqs.All(s => tiers.Any(t => t.Sequence == s)) && seqs.All(s => LootTable.File.Lines.TryGetValue(s, out var l) && l.Length > 0),
                "loot/sequences_and_lines", string.Join(", ", tiers.Select(t => $"{t.Id}:{t.Sequence}")));
            ctx.Assert(tiers.Where(t => t.Rank >= 2).All(t => t.Pillar) && tiers.Where(t => t.Rank < 2).All(t => !t.Pillar), "loot/pillars_from_gold_up");
            var bad = LootTable.File.Drops.SelectMany(d => d.Value.Table.SelectMany(r => r.Weights.Keys.Where(k => LootTable.Tier(k) == null).Select(k => $"{d.Key}:{k}"))).ToList();
            ctx.Assert(new[] { "commander", "boss", "ascendant", "supply" }.All(LootTable.File.Drops.ContainsKey) && bad.Count == 0,
                "loot/drop_tables_name_real_tiers", string.Join(", ", bad));
        }

        // ── Rolls (in a battle, so strikes and relics are there to roll) ──

        private static void Rolls(TestContext ctx)
        {
            ctx.StartTest();
            var rng = new RandomNumberGenerator { Seed = 1234 };
            var problems = new List<string>();
            foreach (var t in LootTable.Tiers)
                foreach (int wave in new[] { 0, 12 })
                {
                    float scale0 = 1f + LootTable.File.WaveScale * wave;
                    float money = 1f + LootTable.File.ResourceWaveScale * wave;
                    for (int i = 0; i < 150; i++)
                    {
                        var got = LootTable.RollContents(t, wave, rng);
                        foreach (var line in t.Rewards.Where(l => l.Kind is "resources" or "materials" or "xp"))
                        {
                            var r = got.FirstOrDefault(x => x.Kind == line.Kind);
                            if (r == null) { if (line.Chance >= 1f) problems.Add($"{t.Id} W{wave} no {line.Kind}"); continue; }
                            float scale = line.Kind == "resources" ? money : scale0;
                            if (r.Amount < Mathf.FloorToInt(line.Min * scale) || r.Amount > Mathf.CeilToInt(line.Max * scale)) problems.Add($"{t.Id} W{wave} {line.Kind} {r.Amount}");
                        }
                        foreach (var line in t.Rewards.Where(l => l.Chance >= 1f && l.Kind is "strike" or "perk_point"))
                            if (got.Count(x => x.Kind == line.Kind) < t.Rewards.Count(l => l.Kind == line.Kind && l.Chance >= 1f)) problems.Add($"{t.Id} no {line.Kind}");
                        if (t.Rewards.Any(l => l.Kind == "relic" && l.Chance >= 1f) && !got.Any(x => x.Kind is "relic" or "salvage")) problems.Add($"{t.Id} no relic");
                        if (got.Select(x => x.RarityRank).Zip(got.Select(x => x.RarityRank).Skip(1)).Any(p => p.First > p.Second)) problems.Add($"{t.Id} not best last");
                    }
                }
            ctx.Assert(problems.Count == 0, "loot/contents_in_range_and_guaranteed", string.Join("; ", problems.Distinct().Take(8)));

            var gold = LootTable.Tier("gold");
            float Avg(int wave) => (float)Enumerable.Range(0, 200).Average(_ => LootTable.RollContents(gold, wave, rng).Where(r => r.Kind == "resources").Sum(r => r.Amount));
            float a0 = Avg(0), a20 = Avg(20);
            // Resources grow like kill bounties (resourceWaveScale), not as fast as Materials and XP
            float want = 1f + LootTable.File.ResourceWaveScale * 20f;
            ctx.Assert(a20 > a0 * (want - 0.12f) && a20 < a0 * (want + 0.12f) && a20 > a0, "loot/contents_grow_with_the_wave",
                $"gold cache Resources W0 {a0:F0}, W20 {a20:F0} (x{want:F2} wanted)");

            // Which tier each source drops
            LootTable.ForceDropChance = 1f;
            var c0 = Enumerable.Range(0, 300).Select(_ => LootTable.RollDrop("commander", 2, rng)?.Id).ToList();
            var c25 = Enumerable.Range(0, 300).Select(_ => LootTable.RollDrop("commander", 25, rng)?.Id).ToList();
            ctx.Assert(c0.All(id => id is "bronze" or "silver" or "gold") && c0.Count(id => id == "bronze") > c0.Count(id => id == "gold") * 3
                && c25.All(id => id is "silver" or "gold" or "diamond") && c25.Contains("diamond"), "loot/commander_tiers_by_wave",
                $"W2 {string.Join(" ", c0.GroupBy(x => x).Select(g => $"{g.Key}:{g.Count()}"))}; W25 {string.Join(" ", c25.GroupBy(x => x).Select(g => $"{g.Key}:{g.Count()}"))}");
            var b5 = Enumerable.Range(0, 100).Select(_ => LootTable.RollDrop("boss", 5, rng)?.Id).Distinct().ToList();
            var b25 = Enumerable.Range(0, 200).Select(_ => LootTable.RollDrop("boss", 25, rng)?.Id).Distinct().OrderBy(x => x).ToList();
            var asc = Enumerable.Range(0, 50).Select(_ => LootTable.RollDrop("ascendant", 8, rng)?.Id).Distinct().ToList();
            ctx.Assert(b5.SequenceEqual(new[] { "gold" }) && b25.SequenceEqual(new[] { "diamond", "gold" }) && asc.SequenceEqual(new[] { "legendary" }),
                "loot/boss_and_ascendant_tiers", $"boss W5 {string.Join(",", b5)}, W25 {string.Join(",", b25)}; Ascendant {string.Join(",", asc)}");
            ctx.Assert(LootTable.RollDrop("supply", 2, rng) == null && LootTable.RollDrop("supply", 3, rng) != null, "loot/supply_from_wave_3");
            LootTable.ForceDropChance = null;
            int natural = Enumerable.Range(0, 2000).Count(_ => LootTable.RollDrop("commander", 2, rng) != null);
            ctx.Assert(natural > 1080 && natural < 1320, "loot/commander_drop_chance", $"{natural} of 2000 (0.6 expected)");
            LootTable.ForceDropChance = 0f;
            ctx.Assert(LootTable.RollDrop("boss", 5, rng) == null, "loot/force_no_drop");

            // A relic BIT already has comes back as Resources
            var pool = RelicRegistry.All.Where(r => r.Rarity is "rare" or "legendary").ToList();
            foreach (var r in pool) RelicInventory.ForceOwn(r.Id);
            var leg = LootTable.RollContents(LootTable.Tier("legendary"), 5, rng);
            var salvage = leg.FirstOrDefault(r => r.Kind == "salvage");
            ctx.Assert(pool.Count > 0 && salvage != null && salvage.Amount > 0 && !leg.Any(r => r.Kind == "relic"), "loot/copies_are_salvaged",
                salvage == null ? string.Join(", ", leg.Select(r => r.Kind)) : $"{salvage.Name}: {salvage.Detail}");
            RelicInventory.UseInMemoryForTests();

            // Crates opened together never hand out the same relic twice
            var shared = new HashSet<string>();
            var batch = Enumerable.Range(0, 8).SelectMany(_ => LootTable.RollContents(LootTable.Tier("legendary"), 5, rng, shared)).ToList();
            var ids = batch.Where(r => r.Kind == "relic").Select(r => r.RelicId).ToList();
            ctx.Assert(ids.Count > 0 && ids.Count == ids.Distinct().Count() && batch.Count(r => r.Kind is "relic" or "salvage") == 8, "loot/one_relic_per_batch",
                $"{ids.Count} relics, {ids.Distinct().Count()} different, {batch.Count(r => r.Kind == "salvage")} salvaged");
            LootTable.ForceDropChance = null;
        }

        // ── Drops ──

        private async Task<Dictionary<string, LootCrate>> Drops(TestContext ctx)
        {
            ctx.StartTest();
            var tree = ctx.Tree;
            var grid = ServiceLocator.Get<VineGrid>();
            var loot = ServiceLocator.Get<LootManager>();
            var bit = ServiceLocator.Get<VinePlayer>();
            var cells = FieldCells(grid, bit.GlobalPosition, 8);
            var found = new Dictionary<string, LootCrate>();
            ctx.Assert(cells.Count == 8, "loot/field_cells", $"{cells.Count}");
            if (cells.Count < 8) return found;
            int relicsBefore = RelicInventory.OwnedCount;
            LootTable.ForceDropChance = 1f;

            async Task<List<LootCrate>> Kill(Vector2I cell, bool boss, bool commander, bool ascendant, bool despawn = false)
            {
                var before = Crates(tree);
                var e = new VineEnemy { IsAscendant = ascendant };
                tree.CurrentScene.AddChild(e);
                e.Initialize("Loot test", VineEnemyFaction.Brute, 1e5f, 0f, 0, PlanetTheme.Current.EnemyScavenger, cell, boss);
                e.IsCommander = commander;
                var p = grid.GridToWorld(cell);
                e.GlobalPosition = new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
                e.SkipMarch();
                await ctx.Wait(0.1f);
                if (despawn) { GameEvents.OnEnemyKilled?.Invoke(e); e.QueueFree(); }
                else e.TakeDamage(1e8f, DamageKind.Heavy);
                await ctx.Wait(0.1f);
                return Crates(tree).Except(before).ToList();
            }

            var cmd = await Kill(cells[0], true, true, false);
            var commanderTiers = LootTable.File.Drops["commander"].Table[0].Weights.Keys.ToList();
            ctx.Assert(cmd.Count == 1 && commanderTiers.Contains(cmd[0].Tier.Id), "loot/commander_drops_a_crate", string.Join(", ", cmd.Select(c => c.Tier.Name)));
            if (cmd.Count == 1) found["commander"] = cmd[0];
            var boss = await Kill(cells[1], true, false, false);
            ctx.Assert(boss.Count == 1 && boss[0].Tier.Id == "gold", "loot/boss_drops_a_gold_cache", string.Join(", ", boss.Select(c => c.Tier.Name)));
            if (boss.Count == 1) found["boss"] = boss[0];
            var asc = await Kill(cells[2], true, false, true);
            ctx.Assert(asc.Count == 1 && asc[0].Tier.Id == "legendary", "loot/ascendant_drops_its_cache", string.Join(", ", asc.Select(c => c.Tier.Name)));
            if (asc.Count == 1) found["ascendant"] = asc[0];
            var plain = await Kill(cells[3], false, false, false);
            var stuck = await Kill(cells[4], true, true, false, despawn: true);
            LootTable.ForceDropChance = 0f;
            var unlucky = await Kill(cells[5], true, true, false);
            LootTable.ForceDropChance = 1f;
            ctx.Assert(plain.Count == 0 && stuck.Count == 0 && unlucky.Count == 0, "loot/no_crate_otherwise",
                $"plain {plain.Count}, despawned {stuck.Count}, failed roll {unlucky.Count}");
            ctx.Assert(RelicInventory.OwnedCount == relicsBefore, "loot/no_relic_straight_from_a_commander", $"{RelicInventory.OwnedCount - relicsBefore} relics");
            ctx.Assert(loot.Dropped == 3, "loot/drops_counted", $"{loot.Dropped}");
            // Where they died, for the landing check
            foreach (var (key, cell) in new[] { ("commander", cells[0]), ("boss", cells[1]), ("ascendant", cells[2]) })
                if (found.TryGetValue(key, out var c)) c.SetMeta("died_at", grid.GridToWorld(cell));
            _fieldCells = cells;
            return found;
        }

        private List<Vector2I> _fieldCells;

        private async Task Field(TestContext ctx, Dictionary<string, LootCrate> crates)
        {
            ctx.StartTest();
            await ctx.Wait(1.0f);
            var grid = ServiceLocator.Get<VineGrid>();
            var bad = new List<string>();
            foreach (var (key, c) in crates)
            {
                if (!GodotObject.IsInstanceValid(c) || !c.Waiting) { bad.Add($"{key} not waiting"); continue; }
                var p = c.GlobalPosition;
                var cell = grid.WorldToGrid(p);
                float ground = grid.GetWorldHeight(p.X, p.Z);
                var died = c.GetMeta("died_at").AsVector3();
                if (!grid.IsWalkable(cell) || grid.GetNode(cell) != null) bad.Add($"{key} on a blocked cell");
                if (Mathf.Abs(p.Y - ground) > 0.05f) bad.Add($"{key} at {p.Y:F2}, ground {ground:F2}");
                if (new Vector2(p.X - died.X, p.Z - died.Z).Length() > 3.5f * Constants.VINE_CELL_SIZE) bad.Add($"{key} far from where it died");
            }
            ctx.Assert(bad.Count == 0 && crates.Count == 3, "loot/crates_land_on_open_ground", string.Join("; ", bad));

            var labels = crates.Values.Where(GodotObject.IsInstanceValid).Select(c => (c, l: c.GetNodeOrNull<Label3D>("TierLabel"))).ToList();
            ctx.Assert(labels.All(x => x.l != null && x.l.Visible && x.l.Text == x.c.Tier.Name.ToUpper()), "loot/crate_shows_its_name",
                string.Join(", ", labels.Select(x => x.l?.Text ?? "none")));
            ctx.Assert(crates.Values.Where(GodotObject.IsInstanceValid).All(c => c.Tier.Pillar ? c.Pillar != null && c.Pillar.Visible : c.Pillar == null),
                "loot/light_pillar_from_gold_up", string.Join(", ", crates.Values.Select(c => $"{c.Tier.Id}:{(c.Pillar?.Visible == true ? "pillar" : "none")}")));
            // It floats and turns
            var gold = crates.GetValueOrDefault("boss");
            var hover = gold?.GetNodeOrNull<Node3D>("Hover");
            float lo = float.MaxValue, hi = float.MinValue, r0 = hover?.Rotation.Y ?? 0f;
            // Over half its bob (3.4 s a cycle for a Gold Cache)
            for (int i = 0; i < 18 && hover != null; i++)
            {
                lo = Mathf.Min(lo, hover.Position.Y); hi = Mathf.Max(hi, hover.Position.Y);
                await ctx.Wait(0.1f);
            }
            ctx.Assert(hover != null && hi - lo > 0.05f && Mathf.Abs(hover.Rotation.Y - r0) > 0.1f, "loot/crate_floats_and_turns",
                hover == null ? "no hover" : $"bob {hi - lo:F3}, turn {hover.Rotation.Y - r0:F2}");

            var hud = ctx.FindNode<VineHUD>();
            await ctx.Wait(0.6f);
            ctx.Assert(hud?.LootCard != null && hud.LootCard.Visible && hud.LootFieldText.StartsWith("3 in the field") && !hud.LootOpenButton.Visible,
                "loot/hud_counts_the_field", $"card {hud?.LootCard?.Visible}, \"{hud?.LootFieldText}\"");

            // A supply drop falls from the sky somewhere out in the field
            var loot = ServiceLocator.Get<LootManager>();
            var none = loot.SupplyDrop(2);
            _announced = null;
            var supply = loot.SupplyDrop(5);
            string said = _announced;
            float startY = supply != null ? supply.GlobalPosition.Y - grid.GetWorldHeight(supply.GlobalPosition.X, supply.GlobalPosition.Z) : 0f;
            await ctx.Wait(1.5f);
            var spire = grid.Harvester.GlobalPosition;
            float dist = supply != null ? new Vector2(supply.GlobalPosition.X - spire.X, supply.GlobalPosition.Z - spire.Z).Length() : 0f;
            ctx.Assert(none == null && supply != null && startY > 15f && supply.Waiting && dist >= 8f, "loot/supply_drop_from_the_sky",
                supply == null ? "none" : $"{supply.Tier.Name} from {startY:F0} up, {dist:F1} from the Spire, waiting {supply.Waiting}");
            ctx.Assert(said != null && said.StartsWith("Supply drop") && supply != null && said.Contains(supply.Tier.Name), "loot/supply_drop_announced", said ?? "nothing");
        }

        // ── BIT takes them ──

        private async Task Pickup(TestContext ctx)
        {
            ctx.StartTest();
            var tree = ctx.Tree;
            var loot = ServiceLocator.Get<LootManager>();
            var bit = ServiceLocator.Get<VinePlayer>();
            var field = Crates(tree).Where(c => !c.Taken).ToList();
            var home = bit.GlobalPosition;
            int taken = 0;
            foreach (var c in field)
            {
                bit.GlobalPosition = c.GlobalPosition + new Vector3(0.6f, 0, 0.3f);
                await ctx.Wait(0.15f);
                if (c.Taken) taken++;
            }
            bit.GlobalPosition = home;
            await ctx.Wait(0.6f);
            ctx.Assert(field.Count == 4 && taken == 4 && loot.Carried.Count == 4, "loot/bit_takes_crates", $"{taken} of {field.Count} taken, {loot.Carried.Count} carried");
            ctx.Assert(field.All(c => !GodotObject.IsInstanceValid(c)), "loot/taken_crates_fly_in_and_go", $"{field.Count(GodotObject.IsInstanceValid)} still there");
            var hud = ctx.FindNode<VineHUD>();
            await ctx.Wait(0.6f);
            var rows = hud?.LootCard?.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList() ?? new List<string>();
            ctx.Assert(hud != null && hud.LootCard.Visible && hud.LootOpenButton.Visible && !hud.LootOpenButton.Disabled && rows.Any(t => t.StartsWith("LOOT CRATES  4"))
                && rows.Any(t => t.Contains("Ascendant Cache")), "loot/hud_lists_what_bit_carries", string.Join(" | ", rows));
        }

        private async Task NotInAWave(TestContext ctx)
        {
            ctx.StartTest();
            var gm = GameManager.Instance;
            var loot = ServiceLocator.Get<LootManager>();
            var hud = ctx.FindNode<VineHUD>();
            gm.SetPhase(GamePhase.Wave);
            await ctx.Wait(0.6f);
            _announced = null;
            Press(Key.O);
            await ctx.Wait(0.2f);
            ctx.Assert(loot.CantOpen() == "Open between waves" && loot.Ceremony == null && !ctx.Tree.Paused && _announced == "Crates open between waves"
                && hud.LootOpenButton.Disabled && hud.LootOpenButton.Text == "OPEN BETWEEN WAVES", "loot/not_during_a_wave",
                $"{loot.CantOpen()}, \"{_announced}\", button {hud.LootOpenButton.Text} {hud.LootOpenButton.Disabled}");
            gm.SetPhase(GamePhase.Build);
            await ctx.Wait(0.6f);
        }

        // ── Opening ──

        private async Task Opening(TestContext ctx)
        {
            ctx.StartTest();
            var tree = ctx.Tree;
            var gm = GameManager.Instance;
            var loot = ServiceLocator.Get<LootManager>();
            loot.GiveForTests(LootTable.Tier("diamond"));
            loot.GiveForTests(LootTable.Tier("bronze"));
            if (!loot.Carried.Any(t => t.Id == "gold")) loot.GiveForTests(LootTable.Tier("gold"));
            var carried = loot.Carried.ToList();
            int small = carried.Count(t => t.Sequence == "pop"), big = carried.Count - small;
            Press(Key.O);
            await ctx.Wait(0.15f);
            var c = loot.Ceremony;
            ctx.Assert(c != null && tree.Paused && loot.Carried.Count == 0 && c.Remaining == big && c.Sequence == "pop" && c.Current.Tiers.Count == small,
                "loot/o_opens_small_ones_together", c == null ? "no ceremony" : $"paused {tree.Paused}, first {c.Sequence} x{c.Current.Tiers.Count}, {c.Remaining} to come");
            if (c == null) return;
            c.Manual = true;
            var seen = new List<string>();
            var bad = new List<string>();
            int opened = 0;
            int guard = 0;
            while (GodotObject.IsInstanceValid(c) && loot.Ceremony == c && guard++ < 8)
            {
                string seq = c.Sequence;
                seen.Add(seq);
                var opening = c.Current;
                // Before the burst, then the burst on time
                StepTo(c, c.BurstAt - 0.05f);
                if (c.Burst) bad.Add($"{seq} burst early");
                var back = c.FindChild("FxBack", true, false) as LootFx;
                if (seq == "rays" && !(back?.RaysAlpha > 0.2f)) bad.Add($"rays dim {back?.RaysAlpha:F2}");
                if (seq == "ascend" && c.FindChildren("Lock*", "MeshInstance3D", true, false).Count != 3) bad.Add($"ascend {c.FindChildren("Lock*", "MeshInstance3D", true, false).Count} rings");
                if (seq == "shatter" && !((c.FindChild("FxFront", true, false) as LootFx)?.CrackAlpha > 0.5f)) bad.Add("no cracks");
                StepTo(c, c.BurstAt + 0.1f);
                if (!c.Burst) bad.Add($"{seq} no burst");
                if (seq == "shatter" && (c.FindChild("Debris", true, false)?.GetChildCount() ?? 0) < 10) bad.Add("shatter no pieces");
                var front = c.FindChild("FxFront", true, false) as LootFx;
                if (front == null || front.Particles == 0) bad.Add($"{seq} no sparks");

                var before = Snapshot();
                if (seen.Count == 2)
                {
                    // Skip shows everything at once
                    c.Advance();
                    if (!c.AllRevealed) bad.Add("skip didn't reveal");
                }
                else
                {
                    for (int i = 0; i < 400 && !c.AllRevealed; i++) c.Step(1f / 30f);
                    if (!c.AllRevealed || c.Revealed != c.Lines.Count || c.Lines.Count == 0) bad.Add($"{seq} revealed {c.Revealed}/{c.Lines.Count}");
                }
                c.Advance(); // collect
                var after = Snapshot();
                string diff = Paid(opening.Rewards, before, after);
                if (diff != null) bad.Add($"{seq}: {diff}");
                if (seq == "ascend" && !(opening.Rewards.Any(r => r.Kind == "perk_point") && opening.Rewards.Any(r => r.Kind == "strike") && opening.Rewards.Any(r => r.Kind is "relic" or "salvage")))
                    bad.Add("ascendant cache short: " + string.Join(",", opening.Rewards.Select(r => r.Kind)));
                opened += opening.Tiers.Count;
                for (int i = 0; i < 15 && GodotObject.IsInstanceValid(c) && c.Current == opening; i++) c.Step(1f / 30f);
            }
            ctx.Assert(seen.SequenceEqual(new[] { "pop" }.Concat(carried.Where(t => t.Sequence != "pop").OrderBy(t => t.Rank).Select(t => t.Sequence))),
                "loot/each_cache_on_its_own", string.Join(" > ", seen));
            ctx.Assert(bad.Count == 0, "loot/sequences_burst_reveal_and_pay", string.Join("; ", bad));
            await ctx.Wait(0.1f);
            ctx.Assert(loot.Ceremony == null && !tree.Paused && !LootCeremony.IsOpen && tree.Root.FindChildren("LootCeremony*", "", false, false).Count == 0 && loot.Opened == carried.Count,
                "loot/last_one_unpauses_and_goes", $"paused {tree.Paused}, opened {loot.Opened} of {carried.Count}");
            await ctx.Wait(0.6f);
            var hud = ctx.FindNode<VineHUD>();
            ctx.Assert(hud?.LootCard != null && !hud.LootCard.Visible, "loot/hud_card_hides_when_empty");
        }

        // ── Every tier on its own, the whole way: dropped, landed, taken, opened, paid ──

        private static async Task EveryTier(TestContext ctx)
        {
            var tree = ctx.Tree;
            var grid = ServiceLocator.Get<VineGrid>();
            var loot = ServiceLocator.Get<LootManager>();
            var bit = ServiceLocator.Get<VinePlayer>();
            foreach (var tier in LootTable.Tiers)
            {
                ctx.StartTest();
                var bad = new List<string>();
                var home = bit.GlobalPosition;
                var cell = FieldCells(grid, home, 1).FirstOrDefault();
                var crate = loot.DropAt(tier, grid.GridToWorld(cell), fromSky: false);
                await ctx.Wait(1.0f);
                if (crate == null || !GodotObject.IsInstanceValid(crate) || !crate.Waiting) { ctx.Assert(false, $"loot/tier_{tier.Id}_works", "didn't land"); continue; }
                int meshes = crate.Look?.FindChildren("*", "MeshInstance3D", true, false).Count ?? 0;
                if (meshes < 3) bad.Add($"field model has {meshes} meshes");
                if (crate.GetNodeOrNull<Label3D>("TierLabel")?.Text != tier.Name.ToUpper()) bad.Add("no name tag");
                if (tier.Pillar != (crate.Pillar?.Visible == true)) bad.Add($"pillar {crate.Pillar?.Visible == true}, wanted {tier.Pillar}");

                bit.GlobalPosition = crate.GlobalPosition + new Vector3(0.5f, 0, 0.2f);
                await ctx.Wait(0.2f);
                bool taken = !GodotObject.IsInstanceValid(crate) || crate.Taken;
                bit.GlobalPosition = home;
                await ctx.Wait(0.5f);
                if (!taken) bad.Add("BIT didn't take it");
                if (GodotObject.IsInstanceValid(crate)) bad.Add("taken crate still in the field");
                if (loot.Carried.Count != 1 || loot.Carried[0] != tier) bad.Add($"carrying {string.Join(",", loot.Carried.Select(t => t.Id))}");

                bool opened = loot.OpenAll();
                var c = loot.Ceremony;
                if (!opened || c == null) { ctx.Assert(false, $"loot/tier_{tier.Id}_works", string.Join("; ", bad.Append($"didn't open: {loot.CantOpen()}"))); continue; }
                c.Manual = true;
                if (c.Sequence != tier.Sequence || c.Current.Tiers.Count != 1 || c.Current.Tiers[0] != tier) bad.Add($"opened as {c.Sequence} x{c.Current.Tiers.Count}");
                int crateMeshes = c.FindChild("Crate", true, false)?.FindChildren("*", "MeshInstance3D", true, false).Count ?? 0;
                if (crateMeshes < 3) bad.Add($"opening model has {crateMeshes} meshes");
                if (!tree.Paused) bad.Add("battle not paused");
                StepTo(c, c.BurstAt + 0.05f);
                if (!c.Burst) bad.Add("no burst");
                for (int i = 0; i < 600 && !c.AllRevealed; i++) c.Step(1f / 30f);
                if (!c.AllRevealed || c.Lines.Count == 0) bad.Add($"revealed {c.Revealed}/{c.Lines.Count}");
                var rewards = c.Current.Rewards;
                // Every line the tier always gives is in it
                foreach (var line in tier.Rewards.Where(l => l.Chance >= 1f))
                {
                    bool has = line.Kind == "relic" ? rewards.Any(r => r.Kind is "relic" or "salvage") : rewards.Any(r => r.Kind == line.Kind);
                    if (!has) bad.Add($"no {line.Kind}");
                }
                var shown = c.Lines.Select(l => l.name).ToList();
                var before = Snapshot();
                c.Advance();
                string diff = Paid(rewards, before, Snapshot());
                if (diff != null) bad.Add(diff);
                for (int i = 0; i < 20 && loot.Ceremony == c; i++) c.Step(1f / 30f);
                await ctx.Wait(0.1f);
                if (loot.Ceremony != null || tree.Paused || LootCeremony.IsOpen) bad.Add("didn't close");
                ctx.Assert(bad.Count == 0, $"loot/tier_{tier.Id}_works",
                    bad.Count > 0 ? string.Join("; ", bad) : $"{tier.Name}: {tier.Sequence}, {meshes} meshes, {string.Join(", ", shown)}");
            }
        }

        /// <summary>What a collect should have paid, against what changed (null when they match).</summary>
        private static string Paid(List<LootReward> rewards, (int res, float mat, float xp, int charges, int relics, int points) a, (int res, float mat, float xp, int charges, int relics, int points) b)
        {
            int res = rewards.Where(r => r.Kind is "resources" or "salvage").Sum(r => r.Amount);
            int mat = rewards.Where(r => r.Kind == "materials").Sum(r => r.Amount);
            int xp = rewards.Where(r => r.Kind == "xp").Sum(r => r.Amount);
            int strikes = rewards.Count(r => r.Kind == "strike");
            int relics = rewards.Where(r => r.Kind == "relic").Select(r => r.RelicId).Distinct().Count();
            int points = rewards.Where(r => r.Kind == "perk_point").Sum(r => r.Amount);
            var got = (b.res - a.res, b.mat - a.mat, b.xp - a.xp, b.charges - a.charges, b.relics - a.relics, b.points - a.points);
            bool ok = got.Item1 == res && Mathf.Abs(got.Item2 - mat) < 0.01f && Mathf.Abs(got.Item3 - xp) < 0.01f && got.Item4 == strikes && got.Item5 == relics && got.Item6 == points;
            return ok ? null : $"paid {got} for res {res} mat {mat} xp {xp} strikes {strikes} relics {relics} points {points}";
        }

        private static (int, float, float, int, int, int) Snapshot()
        {
            var gm = GameManager.Instance;
            var grid = ServiceLocator.Get<VineGrid>();
            var bit = ServiceLocator.Get<VinePlayer>();
            return (gm.CurrentResources, grid.Harvester?.MaterialsAccumulated ?? 0f, bit.Progression?.TotalXp ?? 0f,
                SpireStation.Current?.TotalCharges ?? 0, RelicInventory.OwnedCount, gm.MetaSave?.AvailablePoints ?? 0);
        }

        private static void StepTo(LootCeremony c, float t)
        {
            for (int i = 0; i < 600 && c.Clock < t - 0.0001f; i++) c.Step(Mathf.Min(1f / 60f, t - c.Clock));
        }

        // ── Real input: Space, Escape and the HUD's button ──

        private async Task RealInput(TestContext ctx)
        {
            ctx.StartTest();
            var tree = ctx.Tree;
            var loot = ServiceLocator.Get<LootManager>();
            var hud = ctx.FindNode<VineHUD>();
            var pause = ctx.FindNode<PauseMenu>();
            loot.GiveForTests(LootTable.Tier("silver"));
            await ctx.Wait(0.6f);
            hud.LootOpenButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ctx.Wait(0.2f);
            var c = loot.Ceremony;
            ctx.Assert(c != null && tree.Paused, "loot/hud_button_opens", c == null ? "no ceremony" : "");
            ctx.Assert(c != null && !hud.Visible && c.HiddenLayers > 0 && Celebration.Held, "loot/battle_screens_step_aside", $"HUD visible {hud.Visible}, {c?.HiddenLayers} hidden");
            if (c == null) return;
            Press(Key.Escape);
            await ctx.Wait(0.15f);
            bool skipped = GodotObject.IsInstanceValid(c) && c.AllRevealed && c.Burst;
            ctx.Assert(skipped && pause != null && !pause.Visible, "loot/escape_skips_without_the_pause_menu", $"revealed {skipped}, pause menu {pause?.Visible}");
            Press(Key.Space);
            await ctx.Wait(0.7f);
            ctx.Assert(hud.Visible && !Celebration.Held, "loot/battle_screens_come_back", $"HUD visible {hud.Visible}");
            ctx.Assert(loot.Ceremony == null && !tree.Paused && GameManager.Instance.CurrentPhase == GamePhase.Build && pause != null && !pause.Visible,
                "loot/space_collects_and_the_wave_waits", $"ceremony {loot.Ceremony != null}, paused {tree.Paused}, phase {GameManager.Instance.CurrentPhase}");
            // Opened at 3x speed, the sequence still runs in real time
            Engine.TimeScale = 3.0;
            loot.GiveForTests(LootTable.Tier("bronze"));
            loot.OpenAll();
            c = loot.Ceremony;
            ulong t0 = Time.GetTicksUsec();
            await ctx.Wait(1.5f); // game seconds: 0.5 s of real time at 3x
            float real = (Time.GetTicksUsec() - t0) / 1e6f;
            float clock = c != null && GodotObject.IsInstanceValid(c) ? c.Clock : 0f;
            // At game speed it would have run about 1.5 s (slow frames clamp both to 0.1 s a frame)
            ctx.Assert(c != null && clock > 0.05f && clock < Mathf.Min(0.9f, real + 0.15f), "loot/sequence_ignores_game_speed", $"clock {clock:F2} after {real:F2} s real (1.5 s of game time) at 3x");
            Engine.TimeScale = 1.0;
            if (c != null && GodotObject.IsInstanceValid(c)) { c.Collect(); await ctx.Wait(0.5f); }
        }

        // ── The run ends ──

        private static async Task RunEnd(TestContext ctx)
        {
            ctx.StartTest();
            var tree = ctx.Tree;
            var gm = GameManager.Instance;
            var loot = ServiceLocator.Get<LootManager>();
            int before = gm.CurrentResources, opened = loot.Opened;
            loot.GiveForTests(LootTable.Tier("bronze"));
            loot.GiveForTests(LootTable.Tier("gold"));
            int n = loot.SettleAtRunEnd();
            ctx.Assert(n == 2 && loot.Carried.Count == 0 && gm.CurrentResources > before && loot.Opened == opened + 2, "loot/run_end_opens_what_bit_carries",
                $"{n} opened, +{gm.CurrentResources - before} Resources");

            // With a ceremony up: it pays out what it showed and closes
            loot.GiveForTests(LootTable.Tier("silver"));
            loot.GiveForTests(LootTable.Tier("diamond"));
            loot.OpenAll();
            var c = loot.Ceremony;
            await ctx.Wait(0.2f);
            before = gm.CurrentResources;
            n = loot.SettleAtRunEnd();
            await ctx.Wait(0.1f);
            ctx.Assert(n == 2 && loot.Ceremony == null && !GodotObject.IsInstanceValid(c) && !tree.Paused && gm.CurrentResources > before,
                "loot/run_end_closes_an_open_ceremony", $"{n} opened, paused {tree.Paused}, +{gm.CurrentResources - before}");

            // A crate left in the field stays there (lost with the battle)
            var grid = ServiceLocator.Get<VineGrid>();
            var bit = ServiceLocator.Get<VinePlayer>();
            var far = FieldCells(grid, bit.GlobalPosition, 1);
            var left = far.Count > 0 ? loot.DropAt(LootTable.Tier("bronze"), grid.GridToWorld(far[0]), false) : null;
            loot.SettleAtRunEnd();
            ctx.Assert(left != null && GodotObject.IsInstanceValid(left) && !left.Taken && loot.Carried.Count == 0, "loot/field_crates_stay_in_the_field");
            left?.QueueFree();
        }

        // ── Pictures (loot-sheets) ──

        private static async Task FieldShots(TestContext ctx)
        {
            var tree = ctx.Tree;
            var grid = ServiceLocator.Get<VineGrid>();
            var loot = ServiceLocator.Get<LootManager>();
            var bit = ServiceLocator.Get<VinePlayer>();
            string outDir = ProjectSettings.GlobalizePath("res://test-reports/loot");
            System.IO.Directory.CreateDirectory(outDir);
            // The five tiers side by side on open ground
            var start = FieldCells(grid, bit.GlobalPosition, 1).FirstOrDefault();
            var shown = new List<LootCrate>();
            Vector3 centre;
            var spots = new List<Vector3>();
            for (int i = 0; i < 5; i++)
            {
                var w = grid.GridToWorld(start) + new Vector3(i * 2.6f - 5.2f, 0, 0);
                spots.Add(new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z), w.Z));
            }
            for (int i = 0; i < 5; i++) shown.Add(LootCrate.Spawn(tree.CurrentScene, LootTable.Tiers[i], spots[i], spots[i], false));
            centre = spots[2];
            await ctx.Wait(1.2f);
            var cam = new Camera3D { Fov = 50f };
            tree.CurrentScene.AddChild(cam);
            cam.Current = true;
            foreach (var (name, offset, look) in new[]
            {
                ("field_lineup", new Vector3(0, 4.5f, 9.5f), new Vector3(0, 0.8f, 0)),
                ("field_pillars", new Vector3(0, 9f, 22f), new Vector3(0, 4f, 0)),
                ("field_play", new Vector3(0, 26f, 20f), Vector3.Zero),
            })
            {
                cam.GlobalPosition = centre + offset;
                cam.LookAt(centre + look, Vector3.Up);
                for (int i = 0; i < 3; i++) await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                tree.Root.GetTexture().GetImage().SavePng($"{outDir}/{name}.png");
            }
            cam.QueueFree();
            if (ServiceLocator.TryGet<TDCamera>(out var td)) td.Current = true;
            foreach (var c in shown) c.QueueFree();
            await ctx.Wait(0.2f);
        }

        private static async Task SequenceShots(TestContext ctx)
        {
            var tree = ctx.Tree;
            var loot = ServiceLocator.Get<LootManager>();
            string outDir = ProjectSettings.GlobalizePath("res://test-reports/loot");
            System.IO.Directory.CreateDirectory(outDir);
            var moments = new Dictionary<string, float[]>
            {
                ["pop"] = new[] { 0.5f, 1.04f, 1.9f },
                ["rays"] = new[] { 1.4f, 2.1f, 2.28f, 4.2f },
                ["shatter"] = new[] { 1.2f, 2.3f, 2.5f, 5.2f },
                ["ascend"] = new[] { 0.9f, 2.5f, 3.3f, 3.5f, 7.0f },
            };
            var rng = new RandomNumberGenerator { Seed = 7 };
            foreach (var id in new[] { "bronze", "gold", "diamond", "legendary" })
            {
                var tier = LootTable.Tier(id);
                var tiers = id == "bronze" ? new List<LootTier> { tier, tier, LootTable.Tier("silver") } : new List<LootTier> { tier };
                var rewards = tiers.SelectMany(t => LootTable.RollContents(t, 12, rng)).OrderBy(r => r.RarityRank).ToList();
                var c = new LootCeremony { Name = "LootCeremony" };
                tree.Root.AddChild(c);
                c.Manual = true;
                c.Play(new List<LootCeremony.Opening> { new(tiers, rewards, LootTable.Line(tier.Sequence, rng)) });
                foreach (float t in moments[tier.Sequence])
                {
                    StepTo(c, t);
                    for (int i = 0; i < 3; i++) await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    tree.Root.GetTexture().GetImage().SavePng($"{outDir}/{tier.Sequence}_{t:F2}.png");
                }
                // Collect without paying out (these are only pictures)
                c.TakeRest();
                await ctx.Wait(0.1f);
            }
            ctx.StartTest();
            ctx.Assert(!LootCeremony.IsOpen && !tree.Paused, "loot-sheets/sequences_rendered");
        }

        // ── Helpers ──

        private static async Task<bool> StartRun(TestContext ctx)
        {
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "loot/battle_loads", "no site"); return false; }
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
            ctx.Assert(ok && ServiceLocator.TryGet<LootManager>(out _), "loot/battle_loads", site.Id);
            if (ServiceLocator.TryGet<VineWaveManager>(out var w)) w.PauseAutoStart = true;
            await ctx.Wait(0.5f);
            var hud = ctx.FindNode<VineHUD>();
            ctx.Assert(hud?.LootCard != null && !hud.LootCard.Visible, "loot/hud_card_hidden_without_crates");
            return ok;
        }

        private static List<LootCrate> Crates(SceneTree tree) => tree.GetNodesInGroup(LootCrate.GROUP).OfType<LootCrate>().Where(GodotObject.IsInstanceValid).ToList();

        /// <summary>Open cells well away from BIT (and each other), so nothing is picked up by accident.</summary>
        private static List<Vector2I> FieldCells(VineGrid grid, Vector3 bit, int count)
        {
            var list = new List<Vector2I>();
            var spire = grid.Harvester?.GlobalPosition ?? bit;
            // Canyon floors are two cells wide: every cell, and open ground is the cell and two of its sides
            int step = grid.IsCanyon ? 1 : 2;
            for (int y = 2; y < grid.Height - 2 && list.Count < count; y += step)
                for (int x = 2; x < grid.Width - 2 && list.Count < count; x += step)
                {
                    var c = new Vector2I(x, y);
                    var w = grid.GridToWorld(c);
                    float db = new Vector2(w.X - bit.X, w.Z - bit.Z).Length(), ds = new Vector2(w.X - spire.X, w.Z - spire.Z).Length();
                    if (db < 12f || ds < 10f || db > 40f) continue;
                    bool open = true;
                    if (grid.IsCanyon)
                    {
                        int sides = 0;
                        foreach (var d in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
                            if (grid.InBounds(c + d) && grid.IsWalkable(c + d) && grid.GetNode(c + d) == null) sides++;
                        open = grid.IsWalkable(c) && grid.GetNode(c) == null && sides >= 2;
                    }
                    else
                    for (int dx = -1; dx <= 1 && open; dx++)
                        for (int dy = -1; dy <= 1 && open; dy++)
                        {
                            var n = c + new Vector2I(dx, dy);
                            open = grid.InBounds(n) && grid.IsWalkable(n) && grid.GetNode(n) == null;
                        }
                    if (!open || list.Any(o => (grid.GridToWorld(o) - w).Length() < 9f)) continue;
                    list.Add(c);
                }
            return list;
        }

        private static void Press(Key key)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        }
    }
}
