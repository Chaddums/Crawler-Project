using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The pooled effects in a real battle: every effect puts particles up and they all clear
    /// within their lifetime; effects make no engine objects (a projectile is one node and no
    /// trail nodes); a busy frame fits the pools; debris lands on the ground; each battle scene
    /// gets its own particle system. "vfx" is headless-safe. "vfx-sheets" also renders every
    /// effect at three moments to test-reports/vfx/ (needs a display).
    /// </summary>
    public class VfxTestSuite : ITestSuite
    {
        private readonly bool _sheets;
        public VfxTestSuite(bool sheets = false) { _sheets = sheets; }
        public string SuiteName => _sheets ? "vfx-sheets" : "vfx";

        private sealed record Effect(string Name, Action<SceneTree, Vector3> Spawn);

        private static readonly Effect[] Effects =
        {
            new("muzzle_flash", (t, p) => VfxFactory.SpawnMuzzleFlash(t, p, Vector3.Right, new Color(1f, 0.7f, 0.2f))),
            new("impact", (t, p) => VfxFactory.SpawnImpact(t, p, new Color(1f, 0.7f, 0.2f))),
            new("explosion", (t, p) => VfxFactory.SpawnExplosion(t, p, 3f, new Color(1f, 0.5f, 0.15f))),
            new("tesla_arc", (t, p) => VfxFactory.SpawnArc(t, p, p + new Vector3(5f, 0, 1f), new Color(0.3f, 0.7f, 1f))),
            new("flak_tracer", (t, p) => VfxFactory.SpawnTracer(t, p, p + new Vector3(6f, 0, 0), new Color(1f, 0.6f, 0.2f))),
            new("tar_splat", (t, p) => VfxFactory.SpawnTarSplat(t, p)),
            new("death_burst", (t, p) => VfxFactory.SpawnDeathBurst(t, p, new Color(0.8f, 0.3f, 0.2f))),
            new("energy_burst", (t, p) => VfxFactory.SpawnEnergyBurst(t, p, new Color(0.3f, 0.8f, 1f))),
            new("area_pulse", (t, p) => VfxFactory.SpawnAreaPulse(t, p, 4f, new Color(0.3f, 0.3f, 0.8f))),
            new("shove", (t, p) => VfxFactory.SpawnShove(t, p, Vector3.Right)),
            new("boss_death", (t, p) => VfxFactory.SpawnBossDeathBurst(t, p, new Color(0.8f, 0.3f, 0.2f))),
            new("wave_complete", (t, p) => VfxFactory.SpawnWaveCompleteBurst(t, p)),
        };

        public async Task Run(TestContext ctx)
        {
            var sites = FidelityTestSuite.SitesToCheck();
            var site = sites.FirstOrDefault(s => s.Planet == (_sheets ? 2 : 1)) ?? sites.FirstOrDefault();
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, $"{SuiteName}/battle_loads");
            if (!ok) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var waves)) waves.PauseAutoStart = true;
            var grid = ServiceLocator.Get<VineGrid>();
            var row = TowerSheetSuite.FindRow(grid, 7);
            var at = grid.GridToWorld(row.Count > 0 ? row[3] : grid.ExitPoint) + Vector3.Up * 0.8f;

            ConversionDome.AmbientParticles = false; // the dome's motes would never let the pools empty
            try
            {
                if (_sheets) await Sheets(ctx, grid, at);
                else await Checks(ctx, grid, at);
            }
            finally { ConversionDome.AmbientParticles = true; }

            if (waves != null) waves.PauseAutoStart = false;
            GameManager.Instance.CurrentPlanet = 1;
            GameManager.Instance.CurrentTerritorySectionId = null;
        }

        private static async Task Checks(TestContext ctx, VineGrid grid, Vector3 at)
        {
            var tree = ctx.Tree;
            var vfx = VfxParticles.Get(tree);
            ctx.StartTest();
            ctx.Assert(vfx != null && vfx.GetParent() == tree.CurrentScene && vfx.GetChildCount() == 6,
                "vfx/system_in_scene", "The battle scene should hold one particle system with six pools");
            if (vfx == null) return;
            await ctx.Wait(3f);

            foreach (var e in Effects)
            {
                ctx.StartTest();
                double obj0 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
                int before = vfx.TotalCount;
                e.Spawn(tree, at);
                int after = vfx.TotalCount;
                double objDelta = Performance.GetMonitor(Performance.Monitor.ObjectCount) - obj0;
                ctx.Assert(after > before, $"vfx/{e.Name}/emits", "Put up no particles");
                ctx.Assert(objDelta <= 0.5, $"vfx/{e.Name}/no_engine_objects", $"Made {objDelta} engine objects");
                await ctx.Wait(3.2f);
                ctx.Assert(vfx.TotalCount == 0, $"vfx/{e.Name}/clears", $"{vfx.TotalCount} particles still up 3 s later");
            }

            // A projectile is one node: its trail is particles
            ctx.StartTest();
            var known = new HashSet<ulong>(All<Node>(tree.Root).Select(n => n.GetInstanceId()));
            VfxFactory.SpawnProjectile(tree, at, at + new Vector3(8f, 0, 0), new Color(1f, 0.7f, 0.2f), 18f);
            var seen = new HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                await ctx.Wait(0.02f);
                foreach (var n in All<Node>(tree.Root))
                    if (!known.Contains(n.GetInstanceId()))
                    {
                        known.Add(n.GetInstanceId());
                        if (n is VineProjectile || n.GetParent() is VineProjectile) continue; // the shot itself
                        string what = n is MeshInstance3D mi ? $" ({mi.Mesh?.GetType().Name}, {mi.MaterialOverride?.GetType().Name}, at {mi.GlobalPosition:F1})" : "";
                        seen.Add($"{n.GetType().Name} {n.Name}{what} under {n.GetParent()?.Name}");
                    }
            }
            var trail = seen.ToList();
            GD.Print($"[Vfx] nodes that appeared while a projectile flew: {string.Join("; ", seen)}");
            ctx.Assert(trail.All(x => !x.Contains("AutoFade") && !x.Contains("MeshInstance3D")), "vfx/projectile_no_trail_nodes",
                $"Nodes besides the projectile: {string.Join("; ", trail)}");
            await ctx.Wait(1f);

            // A busy frame: forty turret hits, ten blasts, twenty arcs and sixty flak rounds
            ctx.StartTest();
            int dropped0 = vfx.Dropped;
            double objBusy = Performance.GetMonitor(Performance.Monitor.ObjectCount);
            for (int i = 0; i < 40; i++) VfxFactory.SpawnImpact(tree, at + new Vector3(i % 8, 0, i / 8), new Color(1f, 0.7f, 0.2f));
            for (int i = 0; i < 10; i++) VfxFactory.SpawnExplosion(tree, at + new Vector3(i * 2f, 0, 3f), 3f, new Color(1f, 0.5f, 0.15f));
            for (int i = 0; i < 20; i++) VfxFactory.SpawnArc(tree, at, at + new Vector3(4f, 0, i * 0.3f), new Color(0.3f, 0.7f, 1f));
            for (int i = 0; i < 60; i++) VfxFactory.SpawnTracer(tree, at, at + new Vector3(6f, 0, i * 0.1f), new Color(1f, 0.6f, 0.2f));
            double busyObj = Performance.GetMonitor(Performance.Monitor.ObjectCount) - objBusy; // same frame
            await ctx.Wait(0.5f);
            ctx.Assert(vfx.Dropped == dropped0, "vfx/pools_hold_a_busy_frame", $"{vfx.Dropped - dropped0} particles dropped for want of room");
            ctx.Assert(busyObj <= 2, "vfx/busy_frame_no_objects", $"{busyObj} engine objects from 130 effects");

            // Debris comes to rest on the ground
            await ctx.Wait(3.2f);
            ctx.StartTest();
            VfxFactory.SpawnDeathBurst(tree, at, new Color(0.8f, 0.3f, 0.2f), 6);
            await ctx.Wait(0.75f);
            float ground = grid.GetWorldHeight(at.X, at.Z);
            var lowest = vfx.LowestOf(VfxParticles.Kind.Debris);
            ctx.Assert(vfx.CountOf(VfxParticles.Kind.Debris) > 0 && lowest >= ground - 0.3f, "vfx/debris_lands_on_ground",
                $"Lowest debris {lowest - ground:F2} from the ground near the burst");
            await ctx.Wait(3f);

            // The dome's rising motes and wisps are pooled particles too
            ctx.StartTest();
            var knownDome = new HashSet<ulong>(All<Node>(tree.Root).Select(n => n.GetInstanceId()));
            ConversionDome.AmbientParticles = true;
            await ctx.Wait(2f);
            int motes = vfx.CountOf(VfxParticles.Kind.Glow) + vfx.CountOf(VfxParticles.Kind.Bolt);
            var domeNodes = All<Node>(tree.Root).Where(n => !knownDome.Contains(n.GetInstanceId()) && n is MeshInstance3D)
                .Select(n => $"{n.Name} under {n.GetParent()?.Name}").ToList();
            ConversionDome.AmbientParticles = false;
            ctx.Assert(motes > 0 && domeNodes.Count == 0, "vfx/dome_motes_are_particles",
                $"{motes} dome particles, {domeNodes.Count} new meshes: {string.Join("; ", domeNodes.Take(4))}");
            await ctx.Wait(3.2f);

            // A tower shooting in a real battle uses the pools (no stray effect nodes left behind)
            ctx.StartTest();
            // The numbers and pickup pools are bounded and kept: leave their nodes out
            int Pooled() => DamageNumbers.PooledNodes + (tree.CurrentScene.FindChildren("*", "CanvasLayer", true, false).OfType<VineHUD>().FirstOrDefault()?.Flyout?.GetChildCount() ?? 0);
            int pooledBefore = Pooled();
            double nodesBefore = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            var node = new VineNode();
            node.Initialize(VineNodeRegistry.Get(VineNodeType.FlakBattery));
            var row = TowerSheetSuite.FindRow(grid, 7);
            bool placed = row.Count > 0 && grid.PlaceNode(node, row[1]);
            var targets = new List<VineEnemy>();
            for (int i = 0; placed && i < 5; i++)
            {
                var e = new VineEnemy();
                tree.CurrentScene.AddChild(e);
                var p = grid.GridToWorld(row[4]) + new Vector3(i * 0.4f - 0.8f, 0, 0);
                e.Initialize("Vfx target", VineEnemyFaction.Scavenger, 1e5f, 0f, 0, new Color(0.8f, 0.4f, 0.3f), grid.WorldToGrid(p));
                e.GlobalPosition = new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
                targets.Add(e);
            }
            await ctx.Wait(2f);
            int flakParticles = vfx.CountOf(VfxParticles.Kind.Spark);
            foreach (var e in targets) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
            if (placed) grid.RemoveNode(node.GridPosition); else node.QueueFree();
            await ctx.Wait(3.5f);
            double nodesAfter = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount) - nodesBefore - (Pooled() - pooledBefore);
            ctx.Assert(placed && flakParticles > 0, "vfx/flak_fires_tracers", $"{flakParticles} spark particles while the Flak fired");
            ctx.Assert(nodesAfter <= 1, "vfx/no_nodes_left_behind", $"{nodesAfter} more nodes after the shooting stopped");
        }

        // ── Sheets ──

        private static async Task Sheets(TestContext ctx, VineGrid grid, Vector3 at)
        {
            var outDir = ProjectSettings.GlobalizePath("res://test-reports/vfx");
            System.IO.Directory.CreateDirectory(outDir);
            var tree = ctx.Tree;
            var vfx = VfxParticles.Get(tree);
            var cam = new Camera3D { Fov = 40f };
            tree.CurrentScene.AddChild(cam);
            cam.Current = true;
            foreach (var l in All<CanvasLayer>(tree.Root)) l.Visible = false;

            // Each effect frozen at three moments. A software-rendered frame takes longer than
            // most effects live, so the battle is paused and the particles are stepped by hand.
            var tiles = new List<(Image, string)>();
            var times = new[] { 0.03f, 0.12f, 0.4f };
            Engine.TimeScale = 0.0;
            foreach (var e in Effects)
            {
                for (int k = 0; k < times.Length; k++)
                {
                    vfx.Clear();
                    e.Spawn(tree, at);
                    vfx.Advance(times[k]);
                    float dist = e.Name is "boss_death" or "wave_complete" or "area_pulse" or "explosion" ? 12f : e.Name == "tesla_arc" || e.Name == "flak_tracer" ? 9f : 6f;
                    var focus = at + (e.Name is "tesla_arc" or "flak_tracer" ? new Vector3(3f, 0, 0) : Vector3.Zero);
                    tiles.Add((await Capture(ctx, cam, focus, dist, 300), $"{e.Name} {times[k]:F2}s"));
                    GD.Print($"[VfxSheets] {e.Name} {times[k]:F2}s ({tiles.Count}/{Effects.Length * times.Length})");
                }
            }
            vfx.Clear();
            SaveSheet(tiles, $"{outDir}/effects.png", 6, 300);
            System.IO.File.WriteAllText($"{outDir}/effects.txt", string.Join("\n", tiles.Select(t => t.Item2)));

            // A fight at play distance: every shooting tower against a line of targets. The
            // battle runs at about 1/60 s of game time per rendered frame.
            var row = TowerSheetSuite.FindRow(grid, 9);
            var types = new[] { VineNodeType.DamageTower, VineNodeType.ScatterCannon, VineNodeType.TeslaCoil,
                VineNodeType.FlakBattery, VineNodeType.SlowField, VineNodeType.PushPull, VineNodeType.BuffEmitter };
            var placed = new List<VineNode>();
            var targets = new List<VineEnemy>();
            var battle = new List<(Image, string)>();
            if (row.Count >= 9)
            {
                Engine.TimeScale = 1.0;
                for (int i = 0; i < types.Length; i++)
                {
                    var node = new VineNode();
                    node.Initialize(VineNodeRegistry.Get(types[i]));
                    if (grid.PlaceNode(node, row[1 + i])) placed.Add(node); else node.QueueFree();
                }
                var factions = new[] { VineEnemyFaction.Scavenger, VineEnemyFaction.Brute, VineEnemyFaction.Swarm };
                for (int i = 0; i < 9; i++)
                {
                    var e = new VineEnemy();
                    tree.CurrentScene.AddChild(e);
                    var cell = row[i] + new Vector2I(0, 3);
                    var p = grid.GridToWorld(cell) + new Vector3(0, 0, (i % 2) * 0.6f);
                    e.Initialize("Vfx target", factions[i % 3], 1e5f, 0f, 0, PlanetTheme.Current.EnemyScavenger, grid.WorldToGrid(p));
                    e.GlobalPosition = new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
                    targets.Add(e);
                }
                var center = (grid.GridToWorld(row[0]) + grid.GridToWorld(row[8])) * 0.5f + new Vector3(0, 0, 1.5f);
                cam.GlobalPosition = center + new Vector3(0, Mathf.Sin(Mathf.DegToRad(52f)), Mathf.Cos(Mathf.DegToRad(52f))) * 17f;
                cam.LookAt(center, Vector3.Up);
                // Steer the time scale so each rendered frame moves the game about 1/60 s
                // (frame times swing from under a second to tens of seconds here)
                double ts = 0.01, gameTime = 0;
                Engine.TimeScale = ts;
                for (int f = 0; f < 150; f++)
                {
                    await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    double dt = tree.Root.GetProcessDeltaTime();
                    gameTime += dt;
                    if (dt > 1e-6) ts = Math.Clamp(ts * Math.Clamp(1.0 / 60.0 / dt, 0.25, 4.0), 0.0002, 1.0);
                    Engine.TimeScale = ts;
                    if (f >= 90 && f % 12 == 0)
                    {
                        var img = tree.Root.GetViewport().GetTexture().GetImage();
                        img.Convert(Image.Format.Rgba8);
                        img.Resize(640, 360);
                        battle.Add((img, $"battle {gameTime:F2}s"));
                        GD.Print($"[VfxSheets] battle frame {f} at {gameTime:F2} s game time");
                    }
                }
                Engine.TimeScale = 1.0;
                foreach (var e in targets) if (GodotObject.IsInstanceValid(e)) e.QueueFree();
                foreach (var n in placed) grid.RemoveNode(n.GridPosition);
                if (battle.Count > 0) SaveSheet(battle, $"{outDir}/battle.png", 2, 640, 360);
            }
            Engine.TimeScale = 1.0;
            cam.QueueFree();
            ctx.StartTest();
            ctx.Assert(tiles.Count == Effects.Length * times.Length, "vfx-sheets/rendered");
            ctx.Assert(battle.Count > 0, "vfx-sheets/battle_rendered", $"{placed.Count} towers, {battle.Count} frames: {string.Join(", ", battle.Select(b => b.Item2))}");
        }

        private static async Task<Image> Capture(TestContext ctx, Camera3D cam, Vector3 focus, float dist, int size)
        {
            cam.GlobalPosition = focus + new Vector3(0, Mathf.Sin(0.5f), Mathf.Cos(0.5f)) * dist;
            cam.LookAt(focus, Vector3.Up);
            for (int i = 0; i < 2; i++)
                await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = ctx.Tree.Root.GetViewport().GetTexture().GetImage();
            img.Convert(Image.Format.Rgba8);
            int side = Mathf.Min(img.GetWidth(), img.GetHeight());
            var crop = img.GetRegion(new Rect2I((img.GetWidth() - side) / 2, (img.GetHeight() - side) / 2, side, side));
            crop.Resize(size, size);
            return crop;
        }

        private static void SaveSheet(List<(Image, string)> tiles, string path, int cols, int w, int h = 0)
        {
            if (h == 0) h = w;
            int rows = (tiles.Count + cols - 1) / cols;
            var sheet = Image.CreateEmpty(w * cols, h * rows, false, Image.Format.Rgba8);
            for (int i = 0; i < tiles.Count; i++)
                sheet.BlitRect(tiles[i].Item1, new Rect2I(0, 0, w, h), new Vector2I((i % cols) * w, (i / cols) * h));
            sheet.SavePng(path);
        }

        private static IEnumerable<T> All<T>(Node root) where T : class
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            foreach (var child in root.GetChildren())
                foreach (var x in All<T>(child)) yield return x;
        }
    }
}
