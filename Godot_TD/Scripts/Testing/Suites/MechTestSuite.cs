using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// BIT's in-run progression, built the way a battle builds it: XP from kills, BIT's own kills
    /// and cleared waves; the level curve, stats and growth each level gives; the parts each level
    /// adds; the gear every offered perk bolts on (on its socket, on the body, in BIT's look);
    /// shots leaving from the guns; and a fresh run starting bare.
    ///
    /// "mech" is headless-safe. "mech-sheets" also renders BIT at each tier, with each perk's gear,
    /// and fully built on both planets to test-reports/mech/ (needs a display).
    /// </summary>
    public class MechTestSuite : ITestSuite
    {
        private readonly bool _sheets;
        public MechTestSuite(bool sheets = false) { _sheets = sheets; }
        public string SuiteName => _sheets ? "mech-sheets" : "mech";

        // Gear may stand off the hull by this much and still read as attached
        private const float AttachTol = 0.2f;

        private string _outDir;

        public async Task Run(TestContext ctx)
        {
            _outDir = ProjectSettings.GlobalizePath("res://test-reports/mech");
            System.IO.Directory.CreateDirectory(_outDir);
            var sheet = MechSheet.Load(Constants.PLAYER_MECH_ID);
            CheckSheet(ctx, sheet);

            var sites = FidelityTestSuite.SitesToCheck();
            var p1 = sites.FirstOrDefault(s => s.Planet == 1);
            var p2 = sites.FirstOrDefault(s => s.Planet == 2);
            if (p1 == null) { ctx.Assert(false, "mech/site_found"); return; }

            var player = await Battle(ctx, p1, "mech/battle_loads");
            if (player == null) return;

            if (_sheets)
            {
                await Sheets(ctx, player, sheet, "P1");
                if (p2 != null && (player = await Battle(ctx, p2, "mech/battle_loads_p2")) != null)
                    await FullBuildShots(ctx, player, sheet, "P2");
            }
            else
            {
                await CheckStartsBare(ctx, player);
                await CheckXp(ctx, player, sheet);
                await CheckLevels(ctx, player, sheet);
                await CheckGrowthSurvivesAttacks(ctx, player);
                await CheckGear(ctx, player, sheet);
                await CheckGuns(ctx, player);
                // A new run starts from a bare level-1 mech
                player = await Battle(ctx, p1, "mech/second_run_loads");
                if (player != null) await CheckStartsBare(ctx, player, "mech/new_run");
            }

            Engine.TimeScale = 1.0;
            GameManager.Instance.CurrentPlanet = 1;
            GameManager.Instance.CurrentTerritorySectionId = null;
        }

        // ── Data ──

        private static void CheckSheet(TestContext ctx, MechSheet sheet)
        {
            ctx.StartTest();
            ctx.Assert(sheet.Id == Constants.PLAYER_MECH_ID && sheet.Levels.Max >= 2, "mech/sheet_loads",
                $"Data/Mechs/{Constants.PLAYER_MECH_ID}.json should load with at least two levels");
            ctx.Assert(Enumerable.Range(1, sheet.Levels.Max - 1).All(l => sheet.Levels.XpToNext(l) > 0),
                "mech/sheet/xp_curve", "Every level below the top needs a positive XP cost");

            // Every perk a player can be offered changes the mech
            var offered = VinePerkRegistry.GetAll().Where(p => p.Offered).Select(p => p.Id).ToList();
            var bare = offered.Where(id => !sheet.Gear.TryGetValue(id, out var g) || g.Count == 0).ToList();
            ctx.Assert(bare.Count == 0, "mech/sheet/every_perk_has_gear", $"No gear for: {string.Join(", ", bare)}");

            var parts = sheet.Gear.SelectMany(g => g.Value.Select(p => (g.Key, p)))
                .Concat(sheet.Levels.Tiers.SelectMany(t => t.Parts.Select(p => ($"level {t.Level}", p)))).ToList();
            var badSocket = parts.Where(x => !sheet.Sockets.ContainsKey(x.p.Socket)).Select(x => $"{x.Item1}:{x.p.Socket}").ToList();
            ctx.Assert(badSocket.Count == 0, "mech/sheet/sockets_exist", $"Unknown sockets: {string.Join(", ", badSocket)}");
            var badModel = parts.Where(x => !string.IsNullOrEmpty(x.p.Model) && !ResourceLoader.Exists(x.p.Model))
                .Select(x => $"{x.Item1}:{x.p.Model}").ToList();
            ctx.Assert(badModel.Count == 0, "mech/sheet/models_exist", $"Missing models: {string.Join(", ", badModel)}");
            var badShape = parts.Where(x => string.IsNullOrEmpty(x.p.Model)
                && !new[] { "box", "sphere", "cylinder", "capsule", "torus" }.Contains(x.p.Shape))
                .Select(x => $"{x.Item1}:{x.p.Shape}").ToList();
            ctx.Assert(badShape.Count == 0, "mech/sheet/shapes_known", $"Unknown shapes: {string.Join(", ", badShape)}");
        }

        // ── Runtime ──

        private static async Task<VinePlayer> Battle(TestContext ctx, FidelityTestSuite.Site site, string name)
        {
            bool ok = await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.StartTest();
            ctx.Assert(ok, name, $"Battle on {site.Id} should reach Build phase");
            if (!ok) return null;
            var player = ServiceLocator.TryGet<VinePlayer>(out var p) ? p : null;
            bool built = player != null && await ctx.WaitUntil(() => player.Look?.IsBuilt == true, 5f);
            ctx.Assert(built, $"{name}/mech_built", "BIT's mech look should finish building");
            return built ? player : null;
        }

        private static async Task CheckStartsBare(TestContext ctx, VinePlayer player, string prefix = "mech/start")
        {
            await Frames(ctx, 2);
            ctx.StartTest();
            ctx.Assert(player.Progression.Level == 1 && player.Progression.TotalXp == 0f, $"{prefix}/level_1",
                $"Level {player.Progression.Level}, {player.Progression.TotalXp} XP");
            var gear = player.Look.Parts.Where(p => !p.owner.StartsWith("level:")).ToList();
            ctx.Assert(gear.Count == 0 && player.Look.Weapons.Count == 0, $"{prefix}/no_gear",
                $"{gear.Count} gear parts, {player.Look.Weapons.Count} guns");
            ctx.Assert(GameManager.Instance.ActivePerks.Count == 0, $"{prefix}/no_perks");
        }

        private static async Task CheckXp(TestContext ctx, VinePlayer player, MechSheet sheet)
        {
            var prog = player.Progression;
            var x = sheet.Xp;
            ctx.StartTest();

            // Any kill, by anyone
            var e = SpawnEnemy(player, VineEnemyFaction.Scavenger, new Vector3(0, 0, 30));
            await Frames(ctx, 2);
            float before = prog.TotalXp;
            e.TakeDamage(1e9f);
            await Frames(ctx, 1);
            ctx.Assert(Mathf.IsEqualApprox(prog.TotalXp - before, x.Kill), "mech/xp/kill", $"+{prog.TotalXp - before}, sheet says {x.Kill}");

            // Stuck enemies are despawned through the kill event while alive: nothing
            var stuck = SpawnEnemy(player, VineEnemyFaction.Scavenger, new Vector3(0, 0, 30));
            await Frames(ctx, 2);
            before = prog.TotalXp;
            GameEvents.OnEnemyKilled?.Invoke(stuck);
            ctx.Assert(prog.TotalXp == before, "mech/xp/despawn_gives_nothing", $"+{prog.TotalXp - before}");
            stuck.QueueFree();

            // BIT's own kill earns the bonus on top
            before = prog.TotalXp;
            player.CreditKill();
            ctx.Assert(Mathf.IsEqualApprox(prog.TotalXp - before, x.PersonalKill), "mech/xp/personal_kill", $"+{prog.TotalXp - before}");

            before = prog.TotalXp;
            GameEvents.OnWaveCompleted?.Invoke(1);
            ctx.Assert(Mathf.IsEqualApprox(prog.TotalXp - before, x.WaveCleared), "mech/xp/wave_cleared", $"+{prog.TotalXp - before}");
            await Frames(ctx, 2);
        }

        private static async Task CheckLevels(TestContext ctx, VinePlayer player, MechSheet sheet)
        {
            var prog = player.Progression;
            var lv = sheet.Levels;
            ctx.StartTest();
            int startLevel = prog.Level;
            float hp = player.MaxHP, dmg = player.AttackDamage, speed = player.MoveSpeed;

            // One short of the next level, then the last point
            prog.AddXp(prog.XpToNext - prog.Xp - 0.5f);
            ctx.Assert(prog.Level == startLevel, "mech/level/not_early", $"Level {prog.Level} with 0.5 XP to go");
            prog.AddXp(0.5f);
            ctx.Assert(prog.Level == startLevel + 1, "mech/level/up_on_threshold", $"Level {prog.Level}");
            ctx.Assert(Mathf.IsEqualApprox(player.MaxHP - hp, lv.MaxHPPerLevel), "mech/level/max_hp",
                $"+{player.MaxHP - hp} max HP, sheet says {lv.MaxHPPerLevel}");
            ctx.Assert(player.AttackDamage > dmg, "mech/level/damage", $"{dmg} → {player.AttackDamage}");
            ctx.Assert(Mathf.IsEqualApprox(player.MoveSpeed, speed * (1f + lv.MoveSpeedPerLevel)), "mech/level/faster",
                $"move speed {speed:F2} -> {player.MoveSpeed:F2} (sheet +{lv.MoveSpeedPerLevel:P0})");

            // Bigger, once the growth has eased in
            await ctx.WaitUntil(() => Mathf.IsEqualApprox(player.Look.ShownGrowth, player.Look.Growth), 3f);
            float expected = 1f + lv.GrowthPerLevel * (prog.Level - 1);
            float scale = player.ModelRoot.Scale.X / (player._baseModelScale * SignalTuningEditor.PlayerModelScale);
            ctx.Assert(Mathf.Abs(scale - expected) < 0.01f, "mech/level/grows", $"Scale x{scale:F3}, expected x{expected:F3}");

            // To the top: capped, no XP banked, every tier's parts on
            prog.AddXp(1e6f);
            ctx.Assert(prog.Level == lv.Max && prog.Xp == 0f && prog.XpToNext == 0f, "mech/level/caps",
                $"Level {prog.Level}, {prog.Xp} XP, {prog.XpToNext} to next");
            ctx.Assert(Mathf.IsEqualApprox(player.MaxHP - hp, lv.MaxHPPerLevel * (lv.Max - startLevel)), "mech/level/max_hp_total",
                $"+{player.MaxHP - hp} max HP");
            await Frames(ctx, 2);
            foreach (var tier in lv.Tiers)
            {
                int n = player.Look.Parts.Count(p => p.owner == $"level:{tier.Level}");
                ctx.Assert(n == tier.Parts.Count, $"mech/level/tier_{tier.Level}_parts", $"{n} of {tier.Parts.Count} parts");
            }
            await ctx.WaitUntil(() => Mathf.IsEqualApprox(player.Look.ShownGrowth, player.Look.Growth), 5f);
        }

        /// <summary>The attack pulse used to set the scale to 1, which reset any growth on the first shot.</summary>
        private static async Task CheckGrowthSurvivesAttacks(TestContext ctx, VinePlayer player)
        {
            ctx.StartTest();
            var e = SpawnEnemy(player, VineEnemyFaction.Swarm, new Vector3(2.5f, 0, 0), hp: 1e9f, speed: 0f);
            float lo = float.MaxValue, hi = float.MinValue;
            float t = 0f;
            while (t < 3f)
            {
                await Frames(ctx, 1);
                t += (float)ctx.Tree.Root.GetProcessDeltaTime();
                player.Heal(1000f);
                float rel = player.ModelRoot.Scale.X / player.VisualScale;
                lo = Mathf.Min(lo, rel);
                hi = Mathf.Max(hi, rel);
            }
            ctx.Assert(lo > 0.9f && hi < 1.12f, "mech/growth_survives_attacks",
                $"Model scale ranged x{lo:F2} to x{hi:F2} of its level size while attacking");
            e.QueueFree();
            await Frames(ctx, 2);
        }

        private static async Task CheckGear(TestContext ctx, VinePlayer player, MechSheet sheet)
        {
            var offered = VinePerkRegistry.GetAll().Where(p => p.Offered).ToList();
            foreach (var perk in offered)
            {
                ctx.StartTest();
                string pp = $"mech/gear/{perk.Id}";
                GameManager.Instance.AddPerk(perk);
                await ctx.Wait(0.6f);
                var parts = player.Look.Parts.Where(p => p.owner == perk.Id).ToList();
                int want = sheet.Gear.TryGetValue(perk.Id, out var g) ? g.Count : 0;
                ctx.Assert(parts.Count == want && want > 0, $"{pp}/bolted_on", $"{parts.Count} of {want} parts");

                BodyBounds(player, out var body);
                // Attached: touching the hull, or a piece of the same gear that is (a beacon on a mast)
                var boxes = parts.Select(x => FidelityTestSuite.MeshBounds(x.pivot, out var b) ? b : (Aabb?)null).ToList();
                var attached = new bool[parts.Count];
                for (bool grew = true; grew;)
                {
                    grew = false;
                    for (int i = 0; i < parts.Count; i++)
                    {
                        if (attached[i] || boxes[i] == null) continue;
                        bool on = Gap(body, boxes[i].Value) <= AttachTol
                            || Enumerable.Range(0, parts.Count).Any(j => attached[j] && Gap(boxes[j].Value, boxes[i].Value) <= AttachTol);
                        if (on) { attached[i] = grew = true; }
                    }
                }
                for (int i = 0; i < parts.Count; i++)
                {
                    var (owner, pivot, part) = parts[i];
                    string where = part.Socket + (string.IsNullOrEmpty(part.Model) ? $" {part.Shape}" : $" {System.IO.Path.GetFileNameWithoutExtension(part.Model)}");
                    bool visible = pivot.IsVisibleInTree() && pivot.Scale.X > 0.9f;
                    ctx.Assert(visible, $"{pp}/{part.Socket}/shown", $"{where}: visible {pivot.IsVisibleInTree()}, scale {pivot.Scale.X:F2}");
                    var meshes = pivot.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
                    ctx.Assert(meshes.Count > 0 && (part.Surface == "kit" || meshes.All(player.WearsMechLook)), $"{pp}/{part.Socket}/wears_mech_look",
                        $"{where}: {meshes.Count(m => !player.WearsMechLook(m))} of {meshes.Count} meshes without BIT's materials");
                    if (boxes[i] is Aabb pb)
                    {
                        ctx.Assert(attached[i], $"{pp}/{part.Socket}/on_the_body",
                            $"{where}: {Gap(body, pb):F2} from the hull and not touching the rest of its gear");
                        float sink = player.GlobalPosition.Y - pb.Position.Y;
                        ctx.Assert(sink <= 0.05f, $"{pp}/{part.Socket}/above_ground", $"{where}: {sink:F2} below BIT's feet");
                    }
                }
            }
        }

        private static async Task CheckGuns(TestContext ctx, VinePlayer player)
        {
            ctx.StartTest();
            int guns = player.Look.Weapons.Count;
            ctx.Assert(guns > 0, "mech/guns/mounted", "Perk gear should include guns");
            if (guns == 0) return;

            VineEnemy e = null;
            var seen = new HashSet<VineProjectile>();
            var used = new HashSet<int>();
            int shots = 0, offMuzzle = 0;
            var offWhere = new List<string>();
            float t = 0f;
            // Where each gun has been over the last few physics ticks: BIT fires from inside
            // _PhysicsProcess mid-lunge, and under load several ticks run per rendered frame
            var recent = new Queue<Vector3[]>();
            void Sample()
            {
                if (!GodotObject.IsInstanceValid(player)) return;
                recent.Enqueue(player.Look.Weapons.Select(w => w.Muzzle.GlobalPosition).ToArray());
                while (recent.Count > 8) recent.Dequeue();
            }
            ctx.Tree.PhysicsFrame += Sample;
            while (t < 12f && used.Count < guns)
            {
                // A still target is despawned as stuck after 4 s: keep one in range
                if (e == null || !GodotObject.IsInstanceValid(e) || !e.IsAlive)
                    e = SpawnEnemy(player, VineEnemyFaction.Swarm, new Vector3(3f, 0, 0), hp: 1e9f, speed: 0f);
                player.Heal(1000f);
                await Frames(ctx, 1);
                Sample();
                t += (float)ctx.Tree.Root.GetProcessDeltaTime();
                foreach (var proj in All<VineProjectile>(ctx.Tree.CurrentScene))
                {
                    // Only BIT's shots: the target shoots back from 3 units away
                    if (!seen.Add(proj) || proj.Origin.DistanceTo(player.GlobalPosition) > 1.8f) continue;
                    shots++;
                    int best = -1;
                    float bestD = float.MaxValue;
                    for (int i = 0; i < guns; i++)
                    {
                        float d = recent.Min(at => proj.Origin.DistanceTo(at[i]));
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    if (bestD < 0.35f) used.Add(best);
                    else
                    {
                        offMuzzle++;
                        if (offWhere.Count < 3) offWhere.Add($"from {proj.Origin - player.GlobalPosition:F2} (BIT-relative), {bestD:F2} from the nearest gun");
                    }
                }
            }
            ctx.Tree.PhysicsFrame -= Sample;
            ctx.Assert(shots > 0 && offMuzzle == 0, "mech/guns/fire_from_muzzles", $"{offMuzzle} of {shots} shots left from away from every gun: {string.Join("; ", offWhere)}");
            ctx.Assert(used.Count == guns, "mech/guns/take_turns", $"{used.Count} of {guns} guns fired in {t:F1} s");
            if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            await Frames(ctx, 2);
        }

        // ── Sheets ──

        private async Task Sheets(TestContext ctx, VinePlayer player, MechSheet sheet, string tag)
        {
            var cam = new Camera3D { Fov = 40f };
            ctx.Tree.CurrentScene.AddChild(cam);
            cam.Current = true;
            HideHud(ctx);
            var look = player.Look;
            var prog = player.Progression;
            var tiles = new List<(Image img, string label)>();

            // Levels, no gear
            int[] levels = { 1, 3, 5, 7, 10 };
            foreach (int l in levels)
            {
                look.ResetAll();
                look.SetLevel(l, animate: false);
                tiles.Add((await Shot(ctx, cam, player, 35f, fixedFrame: true), $"Level {l}"));
            }
            SaveSheet(tiles, $"{_outDir}/{tag}_levels.png", 5);

            // Each perk's gear alone, front and back
            tiles.Clear();
            foreach (var perk in VinePerkRegistry.GetAll().Where(p => p.Offered))
            {
                look.ResetAll();
                look.AddPerkGear(perk.Id, animate: false);
                tiles.Add((await Shot(ctx, cam, player, 35f), perk.Name));
                tiles.Add((await Shot(ctx, cam, player, 215f), perk.Name + " (back)"));
            }
            SaveSheet(tiles, $"{_outDir}/{tag}_gear.png", 4);

            await FullBuildShots(ctx, player, sheet, tag, cam);
            cam.QueueFree();
        }

        private async Task FullBuildShots(TestContext ctx, VinePlayer player, MechSheet sheet, string tag, Camera3D cam = null)
        {
            bool own = cam == null;
            if (own)
            {
                cam = new Camera3D { Fov = 40f };
                ctx.Tree.CurrentScene.AddChild(cam);
                cam.Current = true;
                HideHud(ctx);
            }
            var look = player.Look;
            look.ResetAll();
            look.SetLevel(sheet.Levels.Max, animate: false);
            foreach (var perk in VinePerkRegistry.GetAll().Where(p => p.Offered))
                look.AddPerkGear(perk.Id, animate: false);
            var tiles = new List<(Image, string)>();
            foreach (var (yaw, label) in new[] { (35f, "front"), (125f, "side"), (215f, "back"), (305f, "other side") })
                tiles.Add((await Shot(ctx, cam, player, yaw), $"Full build, {label}"));
            player.ShowDomeLook(false);
            tiles.Add((await Shot(ctx, cam, player, 35f), "Full build, outside the dome"));
            tiles.Add((await Shot(ctx, cam, player, 35f, wide: true), "Full build, play distance"));
            player.ShowDomeLook(null);
            SaveSheet(tiles, $"{_outDir}/{tag}_full.png", 3);
            if (own) cam.QueueFree();
        }

        /// <summary>Freeze BIT facing +Z and shoot it from <paramref name="yaw"/> degrees round.</summary>
        /// <param name="fixedFrame">Same framing whatever the size, so growth shows.</param>
        private static async Task<Image> Shot(TestContext ctx, Camera3D cam, VinePlayer player, float yaw, bool wide = false, bool fixedFrame = false)
        {
            // Let pop-ins and growth settle at normal speed, then freeze the frame
            Engine.TimeScale = 1.0;
            await Frames(ctx, 3);
            Engine.TimeScale = 0.0;
            player.ModelRoot.Scale = Vector3.One * player.VisualScale;
            player.ModelRoot.Rotation = Vector3.Zero;
            player.ModelRoot.Position = Vector3.Zero;
            FidelityTestSuite.MeshBounds(player.ModelRoot, out var b);
            var focus = fixedFrame ? player.GlobalPosition + Vector3.Up * 1.0f : b.GetCenter();
            float dist = wide ? 14f : fixedFrame ? 5.2f : Mathf.Max(b.Size.Y, Mathf.Max(b.Size.X, b.Size.Z)) * 2.4f;
            float r = Mathf.DegToRad(yaw);
            cam.GlobalPosition = focus + new Vector3(Mathf.Sin(r), wide ? 0.9f : 0.32f, Mathf.Cos(r)) * dist;
            cam.LookAt(focus, Vector3.Up);
            var img = await Grab(ctx);
            Engine.TimeScale = 1.0;
            return Square(img, 360);
        }

        private static void SaveSheet(List<(Image img, string label)> tiles, string path, int cols)
        {
            if (tiles.Count == 0) return;
            const int cell = 360;
            int rows = (tiles.Count + cols - 1) / cols;
            var sheet = Image.CreateEmpty(cell * cols, cell * rows, false, Image.Format.Rgba8);
            sheet.Fill(new Color(0.1f, 0.1f, 0.12f));
            for (int i = 0; i < tiles.Count; i++)
                if (tiles[i].img != null)
                    sheet.BlitRect(tiles[i].img, new Rect2I(0, 0, cell, cell), new Vector2I((i % cols) * cell, (i / cols) * cell));
            sheet.SavePng(path);
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(path, ".txt"), string.Join("\n", tiles.Select(t => t.label)));
        }

        // ── Helpers ──

        private static VineEnemy SpawnEnemy(VinePlayer player, VineEnemyFaction faction, Vector3 offset, float hp = 50f, float speed = 0f)
        {
            var grid = ServiceLocator.Get<VineGrid>();
            var e = new VineEnemy();
            player.GetParent().AddChild(e);
            var cell = grid.WorldToGrid(player.GlobalPosition + offset);
            cell = new Vector2I(Mathf.Clamp(cell.X, 0, grid.Width - 1), Mathf.Clamp(cell.Y, 0, grid.Height - 1));
            e.Initialize($"Mech test {faction}", faction, hp, speed, 0, new Color(0.8f, 0.3f, 0.3f), cell, false);
            e.GlobalPosition = new Vector3(player.GlobalPosition.X + offset.X, player.GlobalPosition.Y, player.GlobalPosition.Z + offset.Z);
            return e;
        }

        /// <summary>Bounds of BIT's own meshes (the skinned body and eyes), without gear.</summary>
        private static bool BodyBounds(VinePlayer player, out Aabb box)
        {
            box = default;
            bool any = false;
            var skel = player.ModelRoot.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
            if (skel == null) return FidelityTestSuite.MeshBounds(player.ModelRoot, out box);
            foreach (var m in skel.GetChildren().OfType<MeshInstance3D>())
            {
                if (m.Mesh == null || !FidelityTestSuite.MeshBounds(m, out var b)) continue;
                box = any ? box.Merge(b) : b;
                any = true;
            }
            return any;
        }

        /// <summary>Distance between two boxes (0 when they touch or overlap).</summary>
        private static float Gap(Aabb a, Aabb b)
        {
            float dx = Mathf.Max(0, Mathf.Max(a.Position.X - b.End.X, b.Position.X - a.End.X));
            float dy = Mathf.Max(0, Mathf.Max(a.Position.Y - b.End.Y, b.Position.Y - a.End.Y));
            float dz = Mathf.Max(0, Mathf.Max(a.Position.Z - b.End.Z, b.Position.Z - a.End.Z));
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static void HideHud(TestContext ctx)
        {
            foreach (var l in All<CanvasLayer>(ctx.Tree.Root)) l.Visible = false;
        }

        private static async Task<Image> Grab(TestContext ctx)
        {
            for (int i = 0; i < 3; i++)
                await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = ctx.Tree.Root.GetViewport().GetTexture().GetImage();
            img.Convert(Image.Format.Rgba8);
            return img;
        }

        private static Image Square(Image img, int size)
        {
            int side = Mathf.Min(img.GetWidth(), img.GetHeight());
            var crop = img.GetRegion(new Rect2I((img.GetWidth() - side) / 2, (img.GetHeight() - side) / 2, side, side));
            crop.Resize(size, size);
            return crop;
        }

        private static IEnumerable<T> All<T>(Node root) where T : class
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            foreach (var child in root.GetChildren())
                foreach (var x in All<T>(child)) yield return x;
        }

        private static async Task Frames(TestContext ctx, int n)
        {
            for (int i = 0; i < n; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }
    }
}
