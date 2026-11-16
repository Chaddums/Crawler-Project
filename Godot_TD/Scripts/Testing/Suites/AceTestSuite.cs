using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The skilled bot pilot (<see cref="BitAce"/>, "bitSkill": "ace"): BIT walks instead of
    /// being put on a spot. On a real canyon map with the waves paused: its way round rock is
    /// walkable and cuts no corner; it walks to a crate and takes it (no step longer than a
    /// stride: it is never placed); it holds aimed fire on an Empowerer before the enemy nearer
    /// the Spire; it backs away from an enemy walking at it; badly hurt it goes to the Spire,
    /// climbs in, and comes out again patched up.
    /// </summary>
    public class AceTestSuite : ITestSuite
    {
        public string SuiteName => "ace";

        public async Task Run(TestContext ctx)
        {
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "ace/battle_loads");
            if (!ok) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            var grid = ServiceLocator.Get<VineGrid>();
            var bit = ServiceLocator.Get<VinePlayer>();
            grid.Harvester?.IncreaseMaxHP(100000f);
            bool autoFire = GameSettings.BitAutoFire;
            await ctx.Wait(2.5f); // BIT climbs out of the Spire
            try
            {
                BitAce.Reset();
                Paths(ctx, grid, bit);
                await Crate(ctx, grid, bit);
                await Targets(ctx, grid, bit);
                await Distance(ctx, grid, bit);
                await Hurt(ctx, grid, bit);
            }
            finally
            {
                bit.BotMove = Vector3.Zero;
                bit.BotAim(null);
                GameSettings.BitAutoFire = autoFire;
                if (waves != null) waves.PauseAutoStart = false;
            }
        }

        /// <summary>Runs the pilot as the bot does, every frame, for some game seconds; returns the longest single step BIT took.</summary>
        private static async Task<float> Fly(TestContext ctx, VineGrid grid, VinePlayer bit, float seconds, System.Func<bool> until = null)
        {
            float longest = 0f, t = 0f;
            var last = bit.GlobalPosition;
            bool docked = bit.IsDocked;
            while (t < seconds)
            {
                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
                float dt = (float)ctx.Tree.Root.GetProcessDeltaTime();
                t += dt;
                BitAce.Tick(grid, dt);
                // (stepping into or out of the Spire is a move the game makes, not a walk)
                if (bit.IsDocked == docked && !bit.IsDocked)
                    longest = Mathf.Max(longest, new Vector2(bit.GlobalPosition.X - last.X, bit.GlobalPosition.Z - last.Z).Length());
                docked = bit.IsDocked;
                last = bit.GlobalPosition;
                if (until != null && until()) break;
            }
            return longest;
        }

        private static VineEnemy Dummy(TestContext ctx, VineGrid grid, Vector3 at, float hp, EnemyTraits traits, float speed = 0f,
            VineEnemyFaction faction = VineEnemyFaction.Scavenger)
        {
            var e = new VineEnemy();
            ctx.Tree.CurrentScene.AddChild(e);
            e.Initialize("Dummy", faction, hp, speed, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(at));
            e.GlobalPosition = new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
            if (traits != EnemyTraits.None) e.SetTraits(traits);
            return e;
        }

        private static Vector3 World(VineGrid grid, Vector2I c)
        {
            var p = grid.GridToWorld(c);
            return new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
        }

        /// <summary>Cells BIT can walk to from where it is, nearest first, at least <paramref name="min"/> steps away.</summary>
        private static List<Vector2I> Reachable(VineGrid grid, VinePlayer bit, int min, int max)
            => BitAce.Flood(grid, grid.WorldToGrid(bit.GlobalPosition), max).Where(kv => kv.Value >= min).OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();

        // ── The way round ──

        private static void Paths(TestContext ctx, VineGrid grid, VinePlayer bit)
        {
            ctx.StartTest();
            var from = grid.WorldToGrid(bit.GlobalPosition);
            var steps = BitAce.Flood(grid, from, 400);
            // The furthest cell BIT can reach: the way to it has corners in it on any canyon map
            var far = steps.OrderByDescending(kv => kv.Value).First().Key;
            var path = BitAce.PathTo(grid, from, far);
            bool walkable = path.All(c => BitAce.Walkable(grid, c));
            bool joined = true, noCorner = true;
            var prev = from;
            foreach (var c in path)
            {
                var d = c - prev;
                if (Mathf.Abs(d.X) > 1 || Mathf.Abs(d.Y) > 1 || d == Vector2I.Zero) joined = false;
                if (d.X != 0 && d.Y != 0 && (!BitAce.Walkable(grid, new Vector2I(prev.X + d.X, prev.Y)) || !BitAce.Walkable(grid, new Vector2I(prev.X, prev.Y + d.Y)))) noCorner = false;
                prev = c;
            }
            ctx.Assert(path.Count >= 10 && path[^1] == far && walkable && joined && noCorner, "ace/way_round_is_walkable",
                $"{path.Count} cells to ({far.X},{far.Y}): walkable {walkable}, joined {joined}, no corner cut {noCorner}");
            // Somewhere it can't stand: as near as it can get, not nothing
            Vector2I? rock = null;
            for (int x = 0; x < grid.Width && rock == null; x++)
                for (int y = 0; y < grid.Height && rock == null; y++)
                    if (!BitAce.Walkable(grid, new Vector2I(x, y)) && (new Vector2I(x, y) - from).Length() > 6) rock = new Vector2I(x, y);
            var toRock = rock != null ? BitAce.PathTo(grid, from, rock.Value) : new List<Vector2I>();
            ctx.Assert(rock != null && toRock.Count > 0 && toRock.All(c => BitAce.Walkable(grid, c)), "ace/rock_gets_the_nearest_cell",
                rock == null ? "no rock on this map" : $"{toRock.Count} cells toward ({rock.Value.X},{rock.Value.Y})");
        }

        // ── Walking to a crate ──

        private static async Task Crate(TestContext ctx, VineGrid grid, VinePlayer bit)
        {
            ctx.StartTest();
            var loot = ServiceLocator.Get<LootManager>();
            var cells = Reachable(grid, bit, 9, 14);
            if (cells.Count == 0) { ctx.Assert(false, "ace/walks_to_a_crate", "no cell 9 steps away"); return; }
            var at = World(grid, cells[0]);
            int walked0 = LootCrate.WalkedOver;
            var tier = LootTable.File.Tiers.First();
            var crate = loot.DropAt(tier, at, fromSky: false);
            await ctx.Wait(1.2f); // it lands
            var start = bit.GlobalPosition;
            float longest = await Fly(ctx, grid, bit, 20f, () => crate == null || !GodotObject.IsInstanceValid(crate) || crate.Taken);
            bool taken = crate == null || !GodotObject.IsInstanceValid(crate) || crate.Taken;
            float went = new Vector2(bit.GlobalPosition.X - start.X, bit.GlobalPosition.Z - start.Z).Length();
            ctx.Assert(taken && LootCrate.WalkedOver > walked0 && BitAce.WalkedTotal > 6f && longest < 1.2f, "ace/walks_to_a_crate",
                $"taken {taken}, walked {BitAce.WalkedTotal:0.0} m ({went:0.0} m as the crow flies), longest step {longest:0.00} m");
            // Nothing to do: back to its post, on foot
            float before = BitAce.WalkedTotal;
            await Fly(ctx, grid, bit, 4f);
            ctx.Assert(BitAce.Doing == BitAce.Job.Post && BitAce.WalkedTotal > before + 1f, "ace/walks_back_to_its_post",
                $"{BitAce.Doing}, {BitAce.WalkedTotal - before:0.0} m in 4 s");
        }

        // ── Who it shoots ──

        private static async Task Targets(TestContext ctx, VineGrid grid, VinePlayer bit)
        {
            ctx.StartTest();
            GameSettings.BitAutoFire = false;
            var spire = grid.Harvester;
            var here = bit.GlobalPosition;
            // Three in plain sight: a tough one with an Empowerer on it, and an ordinary one nearer the Spire
            var seen = Reachable(grid, bit, 4, 12).Where(c => Sight.Clear(bit.ChestPoint, World(grid, c) + Vector3.Up * 0.8f)).ToList();
            if (seen.Count < 3) { ctx.Assert(false, "ace/empowerer_first", $"only {seen.Count} cells in sight"); return; }
            var byDoor = seen.OrderBy(c => (World(grid, c) - spire.GlobalPosition).Length()).ToList();
            var plain = Dummy(ctx, grid, World(grid, byDoor[0]), 5000f, EnemyTraits.None);
            var big = Dummy(ctx, grid, World(grid, byDoor[^1]), 20000f, EnemyTraits.None, faction: VineEnemyFaction.Brute);
            // (the Empowerer on a cell of its own that BIT can see, the nearest to the tough one)
            var empCell = byDoor.Skip(1).Take(byDoor.Count - 2).OrderBy(c => (c - byDoor[^1]).LengthSquared()).First();
            var emp = Dummy(ctx, grid, World(grid, empCell), 5000f, EnemyTraits.Empowerer);
            await ctx.Wait(1.3f); // the tether forms
            var foes = new List<VineEnemy> { plain, big, emp };
            var pick = BitAce.PickTarget(bit, foes, spire);
            float wEmp = BitAce.Worth(emp, spire.GlobalPosition, bit.EffectiveDamage), wPlain = BitAce.Worth(plain, spire.GlobalPosition, bit.EffectiveDamage),
                  wBig = BitAce.Worth(big, spire.GlobalPosition, bit.EffectiveDamage);
            ctx.Assert(emp.IsTethering && pick == emp && wEmp > wPlain && wPlain > wBig, "ace/empowerer_first",
                $"tethering {emp.IsTethering}, picked {(pick == emp ? "the Empowerer" : pick == plain ? "the one by the Spire" : pick == big ? "the tethered one" : "nobody")}; worth {wEmp:0.0} / {wPlain:0.0} / {wBig:0.0}");

            // And it shoots it: aimed fire, the Empowerer hurt first
            int aimed0 = bit.AimedShots;
            float emp0 = emp.CurrentHealth, plain0 = plain.CurrentHealth;
            await Fly(ctx, grid, bit, 3f);
            ctx.Assert(bit.AimedShots > aimed0 && emp.CurrentHealth < emp0 && plain.CurrentHealth >= plain0 - 0.01f, "ace/holds_fire_on_it",
                $"{bit.AimedShots - aimed0} aimed shots; Empowerer took {emp0 - emp.CurrentHealth:0}, the other {plain0 - plain.CurrentHealth:0}");
            // With it dead the next is the one nearest the Spire
            emp.TakeDamage(1e7f, DamageKind.Heavy);
            await ctx.Wait(0.2f);
            foes.Remove(emp);
            var next = BitAce.PickTarget(bit, foes.Where(e => GodotObject.IsInstanceValid(e) && e.IsAlive).ToList(), spire);
            ctx.Assert(next == plain, "ace/then_the_one_nearest_the_spire", next == big ? "the far one" : next == null ? "nobody" : "ok");
            foreach (var e in new[] { plain, big }) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            bit.BotAim(null);
            await ctx.Wait(0.2f);
            bit.GlobalPosition = here;
        }

        // ── Keeping its distance ──

        private static async Task Distance(TestContext ctx, VineGrid grid, VinePlayer bit)
        {
            ctx.StartTest();
            GameSettings.BitAutoFire = false;
            BitAce.Reset();
            // An enemy that can't be killed in time, set down close: BIT should open the distance, on foot
            var close = Reachable(grid, bit, 2, 3).FirstOrDefault(c => Sight.Clear(bit.ChestPoint, World(grid, c) + Vector3.Up * 0.8f));
            var foe = Dummy(ctx, grid, World(grid, close), 1e7f, EnemyTraits.None);
            await ctx.Wait(0.2f);
            float d0 = new Vector2(foe.GlobalPosition.X - bit.GlobalPosition.X, foe.GlobalPosition.Z - bit.GlobalPosition.Z).Length();
            int aimed0 = bit.AimedShots;
            float longest = await Fly(ctx, grid, bit, 6f);
            float d1 = new Vector2(foe.GlobalPosition.X - bit.GlobalPosition.X, foe.GlobalPosition.Z - bit.GlobalPosition.Z).Length();
            ctx.Assert(d0 < 5.5f && d1 >= 6.5f && longest < 1.2f && bit.AimedShots > aimed0, "ace/backs_away_and_keeps_shooting",
                $"{d0:0.0} m -> {d1:0.0} m, longest step {longest:0.00} m, {bit.AimedShots - aimed0} aimed shots, {BitAce.Doing}");
            foe.QueueFree();
            bit.BotAim(null);
            await ctx.Wait(0.2f);
        }

        // ── Hurt ──

        private static async Task Hurt(TestContext ctx, VineGrid grid, VinePlayer bit)
        {
            ctx.StartTest();
            GameSettings.BitAutoFire = false;
            BitAce.Reset();
            var station = SpireStation.Current;
            if (station == null) { ctx.Assert(false, "ace/climbs_in_when_hurt", "no Spire station"); return; }
            // Out in the field, badly hurt, with something alive far off
            var far = Reachable(grid, bit, 10, 16);
            var foeAt = BitAce.Flood(grid, grid.WorldToGrid(bit.GlobalPosition), 400).OrderByDescending(kv => kv.Value).First().Key;
            var foe = Dummy(ctx, grid, World(grid, foeAt), 1e7f, EnemyTraits.None);
            if (far.Count > 0) bit.GlobalPosition = World(grid, far[0]);
            await ctx.Wait(0.3f);
            bit.TakeDamage(bit.CurrentHP - bit.MaxHP * 0.2f);
            float hurt = bit.CurrentHP / bit.MaxHP;
            float longest = await Fly(ctx, grid, bit, 25f, () => bit.IsDocked);
            ctx.Assert(hurt < 0.35f && bit.IsDocked && longest < 1.2f, "ace/climbs_in_when_hurt",
                $"at {hurt * 100:0}% health: docked {bit.IsDocked}, {BitAce.Doing}, longest step {longest:0.00} m");
            // Patched up: out again
            await Fly(ctx, grid, bit, 12f, () => !bit.IsDocked);
            ctx.Assert(!bit.IsDocked && bit.CurrentHP >= bit.MaxHP * 0.85f, "ace/comes_out_patched_up", $"docked {bit.IsDocked}, {bit.CurrentHP / bit.MaxHP * 100:0}% health");
            foe.QueueFree();
            await ctx.Wait(0.2f);
        }
    }
}
