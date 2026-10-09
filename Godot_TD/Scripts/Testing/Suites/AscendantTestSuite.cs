using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Ascendants in a real battle (P1, first site, Bruteforge): one announced on a cleared wave
    /// shows on the wave card and comes with the next wave as a boss in its own model; BIT and
    /// towers damage it; its friendly rival arrives, walks to it and fights it; killing it pays
    /// its reward and sends the friendly away; one that reaches the Spire takes its share of the
    /// Spire's health without ending the run; a fallen friendly doesn't hurt the Spire, and the
    /// enemy keeps walking. With a display it also saves shots to test-reports/ascendant/.
    /// </summary>
    public class AscendantTestSuite : ITestSuite
    {
        public string SuiteName => "ascendant";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            bool shots = DisplayServer.GetName() != "headless";
            string outDir = ProjectSettings.GlobalizePath("res://test-reports/ascendant");
            if (shots) System.IO.Directory.CreateDirectory(outDir);

            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "ascendant/battle_loads", "no site"); return; }
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
            ctx.Assert(ok, "ascendant/battle_loads", site.Id);
            if (!ok) return;

            var grid = ServiceLocator.Get<VineGrid>();
            var waves = ServiceLocator.Get<VineWaveManager>();
            var am = ServiceLocator.Get<AscendantManager>();
            var player = ServiceLocator.Get<VinePlayer>();
            var spire = grid.Harvester;
            spire?.IncreaseMaxHP(2000f);
            gm.AddResources(20000);
            int oldRuns = gm.MetaSave?.RunCount ?? 0;

            // ── 1. Announced: the wave card names it ──
            ctx.StartTest();
            bool announced = am.TestAnnounce("iron_sovereign");
            await ctx.Wait(0.3f);
            var card = FindNamed<Label>(ctx.Tree.Root, "AscendantWarning");
            ctx.Assert(announced && am.PendingName != null && card != null && card.IsVisibleInTree()
                && card.Text.Contains(am.PendingName), "ascendant/announced_on_wave_card",
                $"pending {am.PendingName ?? "none"}, card {(card == null ? "missing" : $"'{card.Text.Replace("\n", " / ")}' visible={card.IsVisibleInTree()}")}");
            if (shots) await Shot(ctx, outDir, "wave_card", null);

            // ── 2. Comes with the next wave as a boss in its own model ──
            ctx.StartTest();
            waves.RequestNextWave();
            await ctx.Wait(0.5f);
            var enemy = am.Enemy;
            bool hasModel = enemy != null && enemy.GetChildren().OfType<Node3D>().Any(n => n.GetChildCount() > 0
                && AssetLibrary.GetCombinedAABB(n).Size.Y * n.Scale.Y > 2.5f);
            ctx.Assert(enemy != null && enemy.IsAscendant && enemy.IsBoss && hasModel && am.PendingName == null
                && waves.EnemiesRemaining >= 1, "ascendant/comes_with_next_wave",
                enemy == null ? "no enemy Ascendant" : $"{enemy.EnemyName} {enemy.MaxHealth:F0} hp boss={enemy.IsBoss} model={hasModel} wave holds {waves.EnemiesRemaining}");
            if (enemy == null) { Cleanup(gm, oldRuns); return; }

            // ── 3. The friendly answers and fights it ──
            ctx.StartTest();
            bool came = await ctx.WaitUntil(() => am.Friendly != null, 6f);
            var friendly = am.Friendly;
            ctx.Assert(came && friendly.RivalEnemy == enemy && enemy.AscendantFoe == friendly,
                "ascendant/friendly_answers", friendly == null ? "no friendly" : $"{friendly.AscendantName} rival={friendly.RivalEnemy?.EnemyName}");
            if (friendly != null)
            {
                Engine.TimeScale = 3.0;
                float hp0 = enemy.CurrentHealth;
                bool fought = await ctx.WaitUntil(() => friendly.DamageDealt > 0 || !IsInstanceValid(enemy), 40f);
                Engine.TimeScale = 1.0;
                ctx.StartTest();
                ctx.Assert(fought && friendly.DamageDealt > 0, "ascendant/friendly_fights_it",
                    $"dealt {friendly.DamageDealt:F0}, enemy {hp0:F0} -> {(IsInstanceValid(enemy) ? enemy.CurrentHealth : 0):F0}, friendly {friendly.CurrentHP:F0}/{friendly.MaxHP:F0}");
                if (shots && IsInstanceValid(enemy)) await Shot(ctx, outDir, "clash", (enemy.GlobalPosition + friendly.GlobalPosition) * 0.5f);
            }

            // ── 4. Killing it pays and sends the friendly away; the body stays after enough runs ──
            ctx.StartTest();
            if (gm.MetaSave != null) gm.MetaSave.RunCount = 10;
            int reward = am.Profiles.First(p => p.Id == "iron_sovereign").Reward;
            int dropped = 0;
            System.Action<Vector3, int> onDrop = (_, v) => dropped += v;
            GameEvents.OnResourcesDropped += onDrop;
            var inhabit = ServiceLocator.Get<AscendantInhabit>();
            string killNote = null;
            System.Action<string> onKillNote = t => killNote ??= t;
            GameEvents.OnAnnouncement += onKillNote;
            VineEnemy.HitSource = "BIT"; // the killing blow, as BIT's tick marks it
            if (IsInstanceValid(enemy) && enemy.IsAlive) enemy.TakeDamage(1e7f, DamageKind.Heavy);
            VineEnemy.HitSource = null;
            await ctx.Wait(0.5f);
            GameEvents.OnResourcesDropped -= onDrop;
            GameEvents.OnAnnouncement -= onKillNote;
            ctx.Assert(am.KilledBy == "BIT" && killNote != null && killNote.Contains("BIT"), "ascendant/kill_names_the_killer",
                $"killed by {am.KilledBy ?? "?"}: \"{killNote}\"");
            ctx.Assert(am.Enemy == null || !am.Enemy.IsAlive, "ascendant/dies_to_damage", "");
            ctx.Assert(dropped >= reward, "ascendant/kill_pays_reward", $"dropped {dropped}, reward {reward}");
            ctx.Assert(friendly == null || !IsInstanceValid(friendly) || friendly.IsLingering || friendly.IsDeparting,
                "ascendant/friendly_stays_then_leaves", friendly != null && IsInstanceValid(friendly) ? $"lingering={friendly.IsLingering} departing={friendly.IsDeparting}" : "gone");
            ctx.Assert(inhabit.AvailableCorpse != null, "ascendant/body_left_to_inhabit",
                inhabit.AvailableCorpse != null ? inhabit.AvailableCorpse.AscendantName : "no corpse");
            if (shots && inhabit.AvailableCorpse != null) await Shot(ctx, outDir, "corpse", inhabit.AvailableCorpse.GlobalPosition);
            // The corpse goes by itself once the window passes, without errors
            Engine.TimeScale = 4.0;
            bool gone = await ctx.WaitUntil(() => inhabit.AvailableCorpse == null, 20f);
            Engine.TimeScale = 1.0;
            ctx.Assert(gone, "ascendant/body_clears_after_window", "");
            // While it lingers it fights what's left of the wave
            if (friendly != null && IsInstanceValid(friendly) && friendly.IsLingering)
            {
                friendly.Linger(12f); // a fresh window: the corpse wait above can use most of it up
                float dealt0 = friendly.DamageDealt;
                var near = new VineEnemy();
                ctx.Tree.CurrentScene.AddChild(near);
                var np = friendly.GlobalPosition + new Vector3(3f, 0, 0);
                near.Initialize("Straggler", VineEnemyFaction.Brute, 99999f, 0f, 0, new Color(0.8f, 0.3f, 0.3f), grid.WorldToGrid(np));
                near.GlobalPosition = new Vector3(np.X, grid.GetWorldHeight(np.X, np.Z), np.Z);
                Engine.TimeScale = 3.0;
                bool swatted = await ctx.WaitUntil(() => friendly.DamageDealt > dealt0, 15f);
                Engine.TimeScale = 1.0;
                ctx.Assert(swatted, "ascendant/friendly_fights_stragglers", $"dealt {friendly.DamageDealt - dealt0:F0} after its rival fell");
                if (IsInstanceValid(near)) near.QueueFree();
            }

            // ── 5. BIT and towers hurt it (no friendly) ──
            am.TestReset();
            ctx.StartTest();
            am.TestAnnounce("iron_sovereign");
            enemy = am.TestSpawnNow(waves.CurrentWave, 0f, withFriendly: false);
            ok = enemy != null && await ctx.WaitUntil(() => !IsInstanceValid(enemy) || grid.InBounds(grid.WorldToGrid(enemy.GlobalPosition))
                && grid.IsWalkable(grid.WorldToGrid(enemy.GlobalPosition)), 20f);
            if (ok && IsInstanceValid(enemy))
            {
                float before = enemy.CurrentHealth;
                for (int i = 0; i < 16 && IsInstanceValid(enemy); i++)
                {
                    var side = new Vector3(enemy.GlobalPosition.Z - spire.GlobalPosition.Z, 0, spire.GlobalPosition.X - enemy.GlobalPosition.X).Normalized();
                    player.GlobalPosition = enemy.GlobalPosition + side * 4f;
                    await ctx.Wait(0.25f);
                }
                float afterBit = IsInstanceValid(enemy) ? enemy.CurrentHealth : 0f;
                ctx.Assert(afterBit < before, "ascendant/bit_hurts_it", $"{before:F0} -> {afterBit:F0} in 4 s");

                // A ring of turrets on its route ahead
                player.GlobalPosition = spire.GlobalPosition + new Vector3(3, 0, 3);
                var route = ServiceLocator.Get<VinePathfinder>().FlowPath(grid.WorldToGrid(enemy.GlobalPosition));
                int placed = 0;
                if (route != null)
                    foreach (var c in route.Skip(2).Take(10))
                        foreach (var d in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
                            if (placed < 6 && !route.Contains(c + d) && AutoPlaceHelper.PlaceAt(grid, c.X + d.X, c.Y + d.Y, VineNodeType.DamageTower))
                                placed++;
                float before2 = IsInstanceValid(enemy) ? enemy.CurrentHealth : 0f;
                await ctx.Wait(8f);
                float after2 = IsInstanceValid(enemy) ? enemy.CurrentHealth : 0f;
                ctx.StartTest();
                ctx.Assert(placed > 0 && after2 < before2, "ascendant/towers_hurt_it", $"{placed} turrets, {before2:F0} -> {after2:F0} in 8 s");
                if (shots && IsInstanceValid(enemy)) await Shot(ctx, outDir, "towers", enemy.GlobalPosition);

                // ── 6. Getting through costs its share of the Spire, not the run ──
                ctx.StartTest();
                if (IsInstanceValid(enemy) && enemy.IsAlive)
                {
                    foreach (var n in ctx.Tree.GetNodesInGroup(Constants.GROUP_VINE_NODE).OfType<VineNode>().ToList())
                        if (n.Data?.Type == VineNodeType.DamageTower) grid.RemoveNode(grid.WorldToGrid(n.GlobalPosition));
                    spire.Heal(spire.MaxHP);
                    float spireBefore = spire.CurrentHP;
                    var exit = grid.ExitPoint;
                    var near = route?.Count > 3 ? route[route.Count - 3] : exit;
                    var w = grid.GridToWorld(near);
                    enemy.GlobalPosition = new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z), w.Z);
                    enemy.RepathToSpire();
                    bool leaked = await ctx.WaitUntil(() => !IsInstanceValid(enemy), 20f);
                    float lost = spireBefore - spire.CurrentHP;
                    float share = lost / spire.MaxHP;
                    ctx.Assert(leaked && share >= 0.3f && share <= 0.45f && gm.CurrentPhase != GamePhase.Defeat,
                        "ascendant/leak_takes_a_share_not_the_run",
                        $"leaked={leaked}, Spire lost {lost:F0} of {spire.MaxHP:F0} ({100f * share:F0}%), phase {gm.CurrentPhase}");
                }
                else ctx.Assert(false, "ascendant/leak_takes_a_share_not_the_run", "died before it could leak");
            }
            else ctx.Assert(false, "ascendant/bit_hurts_it", "it never reached the field");

            // ── 7. The friendly falling is no loss in itself ──
            am.TestReset();
            ctx.StartTest();
            am.TestAnnounce("void_architect");
            enemy = am.TestSpawnNow(waves.CurrentWave, 0f);
            friendly = am.Friendly;
            if (enemy != null && friendly != null)
            {
                spire.Heal(spire.MaxHP);
                float spireBefore = spire.CurrentHP;
                friendly.TakeDamage(1e7f);
                await ctx.Wait(6f);
                ctx.Assert(IsInstanceValid(enemy) && enemy.IsAlive && enemy.AscendantFoe == null
                    && spire.CurrentHP >= spireBefore - 1f && gm.CurrentPhase != GamePhase.Defeat,
                    "ascendant/friendly_falls_no_spire_blast",
                    $"enemy alive={IsInstanceValid(enemy) && enemy.IsAlive}, foe cleared={(IsInstanceValid(enemy) ? enemy.AscendantFoe == null : true)}, Spire {spireBefore:F0} -> {spire.CurrentHP:F0}, phase {gm.CurrentPhase}");
                if (IsInstanceValid(enemy) && enemy.IsAlive) enemy.TakeDamage(1e7f, DamageKind.Heavy);
            }
            else ctx.Assert(false, "ascendant/friendly_falls_no_spire_blast", "pair did not spawn");

            Cleanup(gm, oldRuns);
        }

        private static void Cleanup(GameManager gm, int oldRuns)
        {
            Engine.TimeScale = 1.0;
            gm.AutoResolvePerks = false;
            if (gm.MetaSave != null) gm.MetaSave.RunCount = oldRuns;
        }

        private static bool IsInstanceValid(GodotObject o) => o != null && GodotObject.IsInstanceValid(o);

        private static T FindNamed<T>(Node root, string name) where T : Node
        {
            if (root is T t && root.Name == name) return t;
            foreach (var c in root.GetChildren())
            {
                var f = FindNamed<T>(c, name);
                if (f != null) return f;
            }
            return null;
        }

        // A shot from a temporary camera looking at `at` (null: the game's own camera)
        private static async Task Shot(TestContext ctx, string dir, string name, Vector3? at)
        {
            Camera3D cam = null, old = null;
            if (at.HasValue)
            {
                var scene = ctx.Tree.CurrentScene;
                old = scene.GetViewport().GetCamera3D();
                cam = new Camera3D { Fov = 45f };
                scene.AddChild(cam);
                cam.GlobalPosition = at.Value + new Vector3(0, 9f, 11f);
                cam.LookAt(at.Value + Vector3.Up * 1.5f, Vector3.Up);
                cam.Current = true;
            }
            for (int i = 0; i < 6; i++)
                await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            ctx.Tree.Root.GetTexture().GetImage().SavePng($"{dir}/{name}.png");
            if (cam != null)
            {
                if (old != null && GodotObject.IsInstanceValid(old)) old.Current = true;
                cam.QueueFree();
            }
        }
    }
}
