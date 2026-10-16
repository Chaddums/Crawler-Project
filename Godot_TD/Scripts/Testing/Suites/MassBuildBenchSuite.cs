using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A mass build (a Swarm Doctrine run placed 473 towers; full perk trees field 250 to 300): a
    /// checkerboard of every kind of tower round the Spire, all at the top level with a branch, and
    /// a field kept full of tough enemies, held at 1x and then at 3x game speed. Measures the
    /// script time a frame (game logic: headless has no rendering), its biggest parts
    /// (FrameProfiler), objects and nodes, and asserts a budget. MASS_TOWERS (default 300) and
    /// MASS_ENEMIES (default 150) set the size. "massbuild" (not in "all").
    /// </summary>
    public class MassBuildBenchSuite : ITestSuite
    {
        public string SuiteName => "massbuild";

        private static readonly (VineNodeType type, string branch)[] Mix =
        {
            (VineNodeType.DamageTower, "railgun"), (VineNodeType.TeslaCoil, "storm"), (VineNodeType.FlakBattery, "shrapnel"),
            (VineNodeType.DamageTower, "minigun"), (VineNodeType.ScatterCannon, "shredder"), (VineNodeType.TeslaCoil, "storm"),
            (VineNodeType.FlakBattery, "skyguard"), (VineNodeType.SlowField, "glue"),
        };

        private static int Env(string name, int fallback)
            => int.TryParse(OS.GetEnvironment(name), out var v) && v > 0 ? v : fallback;

        private static readonly Dictionary<ulong, int> _trisOf = new();

        private static int Tris(Mesh mesh)
        {
            if (mesh == null) return 0;
            ulong id = mesh.GetInstanceId();
            if (_trisOf.TryGetValue(id, out var t)) return t;
            t = 0;
            if (mesh is ArrayMesh am)
                for (int i = 0; i < am.GetSurfaceCount(); i++)
                {
                    int idx = am.SurfaceGetArrayIndexLen(i);
                    t += (idx > 0 ? idx : am.SurfaceGetArrayLen(i)) / 3;
                }
            else t = mesh.GetFaces().Length / 3;
            _trisOf[id] = t;
            return t;
        }

        /// <summary>
        /// What the scene asks the GPU for: mesh instances and their triangles by what owns them
        /// (a tower type, enemies, the rest of the scene), and with a display the renderer's own
        /// draw calls. A headless run has no renderer, so its counts are the scene's only.
        /// </summary>
        private static async Task RenderProbe(TestContext ctx, string label)
        {
            for (int i = 0; i < 3; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
            var count = new Dictionary<string, (int meshes, long tris, long shadow)>();
            var stack = new Stack<Node>();
            stack.Push(ctx.Tree.Root);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n is MeshInstance3D mi && mi.IsVisibleInTree() && mi.Mesh != null)
                {
                    string k = "scene";
                    for (Node a = mi; a != null; a = a.GetParent())
                    {
                        if (a is VineNode vn) { k = $"tower {vn.Data?.Type}"; break; }
                        if (a is VineEnemy) { k = "enemies"; break; }
                        if (a is VineProjectile) { k = "projectiles"; break; }
                        if (a.GetParent() is VineBattleScene) { k = $"scene/{a.Name}"; break; }
                    }
                    var c = count.GetValueOrDefault(k);
                    bool seen = mi.CastShadow != GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
                    count[k] = (c.meshes + (seen ? 1 : 0), c.tris + (seen ? Tris(mi.Mesh) : 0), c.shadow + (mi.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off ? Tris(mi.Mesh) : 0));
                }
                for (int i = 0; i < n.GetChildCount(); i++) stack.Push(n.GetChild(i));
            }
            if (OS.GetEnvironment("MASS_PROBE_MESHES") == "1")
            {
                // The heaviest meshes by what they add up to, with where they come from
                var byMesh = new Dictionary<ulong, (string name, int uses, int tris)>();
                stack.Push(ctx.Tree.Root);
                while (stack.Count > 0)
                {
                    var n = stack.Pop();
                    if (n is MeshInstance3D mi && mi.IsVisibleInTree() && mi.Mesh != null)
                    {
                        ulong id = mi.Mesh.GetInstanceId();
                        var b = byMesh.GetValueOrDefault(id);
                        string owner = "";
                        for (Node a = mi; a != null; a = a.GetParent()) if (a is VineNode vn) { owner = vn.Data?.Type.ToString(); break; } else if (a is VineEnemy ve) { owner = "enemy"; break; }
                        string name = b.name ?? $"{owner} {mi.Name} <{mi.Mesh.GetType().Name} {(string.IsNullOrEmpty(mi.Mesh.ResourcePath) ? mi.Mesh.ResourceName : mi.Mesh.ResourcePath)}> under {mi.GetParent()?.Name}/{mi.GetParent()?.GetParent()?.Name}";
                        byMesh[id] = (name, b.uses + 1, Tris(mi.Mesh));
                    }
                    for (int i = 0; i < n.GetChildCount(); i++) stack.Push(n.GetChild(i));
                }
                foreach (var (name, uses, t) in byMesh.Values.OrderByDescending(v => (long)v.uses * v.tris).Take(25))
                    GD.Print($"[MassBuild]   mesh {name}: {t} triangles x {uses} = {(long)t * uses / 1000}k");
            }
            int meshes = count.Values.Sum(v => v.meshes);
            long tris = count.Values.Sum(v => v.tris), shadowTris = count.Values.Sum(v => v.shadow);
            GD.Print($"[MassBuild] render {label}: {meshes} visible meshes, {tris / 1000}k triangles, {shadowTris / 1000}k casting shadows"
                + $"; renderer: {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} draw calls, {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} objects, {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000}k primitives");
            foreach (var (k, v) in count.OrderByDescending(kv => kv.Value.tris).Take(12))
                GD.Print($"[MassBuild]   {k}: {v.meshes} meshes, {v.tris / 1000}k triangles ({v.tris / Math.Max(1, v.meshes)} each), {v.shadow / 1000}k casting shadows");

            // With a display: what the renderer draws as things stand, and with the towers'
            // meshes drawn simpler sooner, without their shadows, or hidden
            if (DisplayServer.GetName() == "headless" || OS.GetEnvironment("MASS_PROBE_RENDER") != "1") return;
            var towerMeshes = new List<MeshInstance3D>();
            foreach (var vn in Roster.Nodes(ctx.Tree))
                if (GodotObject.IsInstanceValid(vn)) towerMeshes.AddRange(vn.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>());
            async Task<string> Drawn(string what)
            {
                for (int i = 0; i < 3; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
                return $"{what} {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} draws / {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} objects / {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000}k prims";
            }
            var cam = ctx.Tree.Root.GetCamera3D();
            GD.Print($"[MassBuild]   camera at {cam?.GlobalPosition}, looking {(cam != null ? -cam.GlobalBasis.Z : Vector3.Zero)}, fov {cam?.Fov}, lod threshold {ctx.Tree.Root.MeshLodThreshold}, quality {GameSettings.Quality}");
            string shots = OS.GetEnvironment("MASS_SHOTS");
            async Task Shot(string name)
            {
                if (string.IsNullOrEmpty(shots)) return;
                await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                ctx.Tree.Root.GetTexture().GetImage().SavePng($"{shots}/{label.Replace(' ', '_')}_{name}.png");
            }
            var lines = new List<string> { await Drawn("as set") };
            await Shot("as_set");
            TowerLook.SetShadowProxies(!TowerLook.ShadowProxies, ctx.Tree);
            lines.Add(await Drawn(TowerLook.ShadowProxies ? "heavy meshes shadowed by boxes" : "heavy meshes casting their own shadows"));
            await Shot("proxies_switched");
            TowerLook.SetShadowProxies(!TowerLook.ShadowProxies, ctx.Tree);
            var cast = towerMeshes.Select(m => m.CastShadow).ToList();
            var over = towerMeshes.Select(m => m.MaterialOverlay).ToList();
            var bias = towerMeshes.Select(m => m.LodBias).ToList();
            void Restore() { for (int i = 0; i < towerMeshes.Count; i++) { towerMeshes[i].CastShadow = cast[i]; towerMeshes[i].MaterialOverlay = over[i]; towerMeshes[i].LodBias = bias[i]; } }
            foreach (var m in towerMeshes) m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            lines.Add(await Drawn("towers cast no shadow"));
            await Shot("no_shadow");
            foreach (var m in towerMeshes) m.MaterialOverlay = null;
            lines.Add(await Drawn("no tower shadows or rims"));
            foreach (var m in towerMeshes) m.LodBias = 0.5f;
            lines.Add(await Drawn("no tower shadows or rims, lod bias 0.5"));
            await Shot("no_shadow_rim_lod05");
            foreach (var m in towerMeshes) m.LodBias = 0.25f;
            lines.Add(await Drawn("no tower shadows or rims, lod bias 0.25"));
            Restore();
            foreach (var m in towerMeshes) m.MaterialOverlay = null;
            lines.Add(await Drawn("no tower rims"));
            Restore();
            foreach (var vn in Roster.Nodes(ctx.Tree))
                if (GodotObject.IsInstanceValid(vn) && vn.Data?.Type is VineNodeType.FlakBattery or VineNodeType.ScatterCannon) vn.Visible = false;
            lines.Add(await Drawn("flak and scatter hidden"));
            foreach (var vn in Roster.Nodes(ctx.Tree)) if (GodotObject.IsInstanceValid(vn)) vn.Visible = false;
            lines.Add(await Drawn("towers hidden"));
            foreach (var vn in Roster.Nodes(ctx.Tree)) if (GodotObject.IsInstanceValid(vn)) vn.Visible = true;
            foreach (var l in lines) GD.Print($"[MassBuild]   drawn {l}");
        }

        public async Task Run(TestContext ctx)
        {
            int towers = Env("MASS_TOWERS", 300), enemies = Env("MASS_ENEMIES", 150);
            var gm = GameManager.Instance;
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            if (site == null) { ctx.Assert(false, "massbuild/battle_loads", "no site"); return; }
            gm.CurrentPlanet = site.Planet;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = SpireData.Get("Bruteforge")?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            gm.AutoResolvePerks = true;
            gm.StartVineRun();
            await ctx.Wait(1f);
            bool ok = await ctx.WaitForPhase(GamePhase.Build, 30f);
            ctx.Assert(ok, "massbuild/battle_loads", site.Id);
            if (!ok) return;

            var grid = ServiceLocator.Get<VineGrid>();
            var waves = ServiceLocator.Get<VineWaveManager>();
            var pf = ServiceLocator.Get<VinePathfinder>();
            grid.Harvester?.IncreaseMaxHP(1e7f);
            gm.AddResources(1000000);
            waves.PauseAutoStart = true;

            bool probe = OS.GetEnvironment("MASS_PROBE") == "1";
            if (probe) await RenderProbe(ctx, "empty field");
            // A checkerboard of the mix outward from the Spire, every kind at the top with a branch
            var exit = grid.ExitPoint;
            int placed = 0;
            var buildSw = Stopwatch.StartNew();
            for (int r = 2; r < 40 && placed < towers; r++)
                for (int dx = -r; dx <= r && placed < towers; dx++)
                    for (int dy = -r; dy <= r && placed < towers; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r || ((dx + dy) & 1) != 0) continue;
                        var c = exit + new Vector2I(dx, dy);
                        if (!grid.CanPlace(c) || pf.WouldBlockAllPaths(c)) continue;
                        var (type, branch) = Mix[placed % Mix.Length];
                        if (AutoPlaceHelper.PlaceAt(grid, c.X, c.Y, type))
                        {
                            grid.GetNode(c)?.ForceUpgrade(3, branch);
                            placed++;
                        }
                    }
            double buildMs = buildSw.Elapsed.TotalMilliseconds;
            await ctx.Wait(0.5f);
            GD.Print($"[MassBuild] {placed} towers placed in {buildMs:F0} ms");

            waves.RequestNextWave();
            await ctx.Wait(0.5f);
            var rng = new RandomNumberGenerator { Seed = 11 };
            int Fill()
            {
                int alive = Roster.Enemies(ctx.Tree).Count(e => e != null && e.IsAlive);
                int added = 0;
                for (int i = 0; i < 400 && alive + added < enemies; i++)
                {
                    var c = new Vector2I(rng.RandiRange(1, grid.Width - 2), rng.RandiRange(1, grid.Height - 2));
                    if ((c - exit).Length() < 18f || !grid.IsWalkable(c)) continue;
                    if (waves.SpawnReinforcement(c, 20000f) != null) added++; // tough: the fight stays on
                }
                return added;
            }
            Fill();
            await ctx.Wait(1f);
            if (probe)
            {
                await RenderProbe(ctx, $"{placed} towers and enemies");
                if (OS.GetEnvironment("MASS_PROBE_ONLY") == "1")
                {
                    ctx.StartTest();
                    ctx.Assert(true, "massbuild/probe", "probe only");
                    foreach (var e in Roster.Enemies(ctx.Tree).ToList()) if (e != null && e.IsAlive) e.TakeDamage(1e9f);
                    gm.AutoResolvePerks = false; waves.PauseAutoStart = false;
                    return;
                }
            }

            // The wave card with this field: what one refresh costs (the HUD asks every 0.75 s)
            var tsw = Stopwatch.StartNew();
            int next = waves.CurrentWave + 1;
            ThreatModel.Assess(next, next);
            double assessMs = tsw.Elapsed.TotalMilliseconds;
            tsw.Restart();
            ThreatModel.BreaksAt(next + 1, 7);
            double breaksMs = tsw.Elapsed.TotalMilliseconds;
            GD.Print($"[MassBuild] wave card: Assess {assessMs:F1} ms, BreaksAt(7) {breaksMs:F1} ms");
            GD.Print($"[MassBuild] GC: server {System.Runtime.GCSettings.IsServerGC}, latency {System.Runtime.GCSettings.LatencyMode}, concurrent {System.AppContext.GetData("System.GC.Concurrent") ?? "default"}, heap {GC.GetTotalMemory(false) / 1048576} MB");

            // Experiment: towers that never turn (what the engine spends moving turrets)
            if (OS.GetEnvironment("MASS_FREEZE_LOOKS") == "1")
                foreach (var n in Roster.Nodes(ctx.Tree))
                    if (GodotObject.IsInstanceValid(n) && n.Look != null) n.Look.SetProcess(false);
            // What runs every frame: nodes with processing on, by class (each is a call into the
            // script per frame or per step)
            {
                var proc = new Dictionary<string, int>();
                var stack = new Stack<Node>();
                stack.Push(ctx.Tree.Root);
                int total = 0;
                while (stack.Count > 0)
                {
                    var n = stack.Pop();
                    total++;
                    string k = null;
                    if (n.IsProcessing()) k = "process ";
                    if (n.IsPhysicsProcessing()) k = (k ?? "") + "physics ";
                    if (n.IsProcessingInternal() || n.IsPhysicsProcessingInternal()) k = (k ?? "") + "internal ";
                    if (k != null) { k += n.GetClass() == n.GetType().Name ? n.GetClass() : $"{n.GetType().Name}({n.GetClass()})"; proc[k] = proc.GetValueOrDefault(k) + 1; }
                    for (int i = 0; i < n.GetChildCount(); i++) stack.Push(n.GetChild(i));
                }
                GD.Print($"[MassBuild] {total} nodes; processing: {string.Join(", ", proc.OrderByDescending(kv => kv.Value).Take(24).Select(kv => $"{kv.Key} {kv.Value}"))}");
            }
            if (OS.GetEnvironment("MASS_GC") == "low") System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
            using var sampler = new AllocationSampler();
            var results = new List<string>();
            double p95At1 = 0;
            foreach (double speed in new[] { 1.0, 3.0 })
            {
                Engine.TimeScale = speed;
                await ctx.Wait(1f);
                var sw = Stopwatch.StartNew();
                var frameSw = Stopwatch.StartNew();
                var times = new List<double>();
                int frames = 0, peakObjects = 0, peakNodes = 0, minEnemies = int.MaxValue;
                int peakDraws = 0, peakDrawn = 0, peakPrims = 0;
                double procMs = 0, physMs = 0;
                long alloc0 = GC.GetTotalAllocatedBytes(false);
                int gcStart0 = GC.CollectionCount(0), gcStart2 = GC.CollectionCount(2);
                var lastSnap = new Dictionary<string, long>();
                var snap = new Dictionary<string, long>();
                int spikes = 0, lastGc0 = GC.CollectionCount(0), lastGc2 = GC.CollectionCount(2);
                var lastPause = GC.GetTotalPauseDuration();
                float topUp = 0f;
                FrameProfiler.Reset();
                FrameProfiler.Enabled = true;
                sampler.On = speed == (OS.GetEnvironment("MASS_SAMPLE") == "3" ? 3.0 : 1.0);
                while (sw.Elapsed.TotalSeconds < 10)
                {
                    if ((topUp -= (float)frameSw.Elapsed.TotalSeconds) <= 0f) { topUp = 0.25f; Fill(); }
                    frameSw.Restart();
                    await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
                    frames++;
                    times.Add(frameSw.Elapsed.TotalSeconds);
                    // A slow frame: what it spent, and whether the collector ran
                    FrameProfiler.Snapshot(snap);
                    int gc0 = GC.CollectionCount(0), gc2 = GC.CollectionCount(2);
                    var pause = GC.GetTotalPauseDuration();
                    if (frameSw.Elapsed.TotalMilliseconds > 50 && spikes < 8)
                    {
                        spikes++;
                        var top = snap.Select(kv => (kv.Key, FrameProfiler.Ms(kv.Value - (lastSnap.TryGetValue(kv.Key, out var b) ? b : 0))))
                            .OrderByDescending(x => x.Item2).Take(4).Select(x => $"{x.Key} {x.Item2:F1}");
                        GD.Print($"[MassBuild] x{speed:0} slow frame {frameSw.Elapsed.TotalMilliseconds:F0} ms: {string.Join(", ", top)}; gc0 +{gc0 - lastGc0}, gc2 +{gc2 - lastGc2}, GC paused {(pause - lastPause).TotalMilliseconds:F0} ms");
                        if (gc0 != lastGc0)
                        {
                            var gi = GC.GetGCMemoryInfo(GCKind.Any);
                            GD.Print($"[MassBuild]   that GC: gen{gi.Generation}, pause {gi.PauseDurations[0].TotalMilliseconds:F1}+{gi.PauseDurations[1].TotalMilliseconds:F1} ms, promoted {gi.PromotedBytes / 1024} KB, heap {gi.HeapSizeBytes / 1048576} MB, pinned {gi.PinnedObjectsCount}, finalizable pending {gi.FinalizationPendingCount}, compacted {gi.Compacted}, concurrent {gi.Concurrent}, gen0 budget? {gi.GenerationInfo[0].SizeBeforeBytes / 1048576} MB before");
                        }
                    }
                    (lastSnap, snap) = (snap, lastSnap); lastGc0 = gc0; lastGc2 = gc2; lastPause = pause;
                    procMs += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
                    physMs += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
                    peakObjects = Math.Max(peakObjects, (int)Performance.GetMonitor(Performance.Monitor.ObjectCount));
                    peakDraws = Math.Max(peakDraws, (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
                    peakDrawn = Math.Max(peakDrawn, (int)Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame));
                    peakPrims = Math.Max(peakPrims, (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
                    peakNodes = Math.Max(peakNodes, (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
                    minEnemies = Math.Min(minEnemies, Roster.Enemies(ctx.Tree).Count(e => e != null && e.IsAlive));
                }
                FrameProfiler.Enabled = false;
                if (sampler.On)
                {
                    sampler.On = false;
                    foreach (var (type, bytes) in sampler.Top(16)) GD.Print($"[MassBuild] allocated: {type} ~{bytes / 1024} KB in 10 s");
                    if (OS.GetEnvironment("MASS_CLOSURES") == "1")
                        foreach (var t in typeof(VineNode).Assembly.GetTypes().Where(t => t.Name.Contains("DisplayClass8_0")))
                            GD.Print($"[MassBuild] closure {t.DeclaringType?.FullName}.{t.Name}: {string.Join(", ", t.GetFields().Select(f => f.Name))}");
                }
                foreach (var (key, ms, calls) in FrameProfiler.Report(frames).Take(14))
                    GD.Print($"[MassBuild] x{speed:0} {key}: {ms:F2} ms a frame ({calls} calls)");
                foreach (var (key, bytes) in FrameProfiler.Allocated().Take(10))
                    GD.Print($"[MassBuild] x{speed:0} {key} allocated {bytes / 1024.0 / Math.Max(1, frames):F1} KB a frame");
                times.Sort();
                double avg = times.Average() * 1000.0, p95 = times[(int)(times.Count * 0.95)] * 1000.0, worst = times[^1] * 1000.0;
                double fps = frames / sw.Elapsed.TotalSeconds;
                double kbPerFrame = (GC.GetTotalAllocatedBytes(false) - alloc0) / 1024.0 / Math.Max(1, frames);
                int gcs0 = GC.CollectionCount(0) - gcStart0, gcs2 = GC.CollectionCount(2) - gcStart2;
                if (speed == 1.0) p95At1 = p95;
                GD.Print($"[MassBuild] x{speed:0} Godot's own count: process {procMs / Math.Max(1, frames):F2} ms, physics {physMs / Math.Max(1, frames):F2} ms a frame (physics is per step), {Engine.PhysicsTicksPerSecond} ticks a second, {Engine.MaxPhysicsStepsPerFrame} steps a frame at most");
                string line = $"x{speed:0}: frame {avg:F2} ms (p95 {p95:F1}, worst {worst:F1}), {fps:F0} fps, {kbPerFrame:F0} KB allocated a frame ({gcs0} gen0 and {gcs2} gen2 collections in 10 s), {minEnemies}+ enemies, peak {peakObjects} objects, {peakNodes} nodes" + (peakDraws > 0 ? $", up to {peakDraws} draw calls, {peakDrawn} objects drawn, {peakPrims / 1000}k primitives a frame" : "");
                results.Add(line);
                GD.Print($"[MassBuild] {placed} towers, {line}");
            }
            Engine.TimeScale = 1.0;

            ctx.StartTest();
            ctx.Assert(placed >= towers * 0.9f, "massbuild/scene_built", $"{placed} of {towers} towers");
            bool headless = DisplayServer.GetName() == "headless";
            ctx.Assert(!headless || p95At1 < 16.7, "massbuild/frame_time_budget",
                (headless ? "" : "(not checked with a display: software rendering) ") + $"{placed} towers: {string.Join("; ", results)} (headless: game logic only)");

            foreach (var e in Roster.Enemies(ctx.Tree).ToList())
                if (e != null && e.IsAlive) e.TakeDamage(1e9f);
            gm.AutoResolvePerks = false;
            waves.PauseAutoStart = false;
        }
    }
}

namespace JunkyardTD
{
    /// <summary>
    /// Samples what the game allocates: the runtime's allocation ticks (one about every 100 KB,
    /// naming the type that crossed it), tallied by type. In process, as no trace tool reaches here.
    /// </summary>
    internal sealed class AllocationSampler : System.Diagnostics.Tracing.EventListener
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _bytes = new();
        public bool On;

        protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Verbose, (System.Diagnostics.Tracing.EventKeywords)0x1);
        }

        protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
        {
            if (!On || e.EventName == null || !e.EventName.StartsWith("GCAllocationTick") || e.Payload == null) return;
            string type = null; long amount = 100 * 1024;
            for (int i = 0; i < e.PayloadNames.Count; i++)
            {
                if (e.PayloadNames[i] == "TypeName") type = e.Payload[i] as string;
                else if (e.PayloadNames[i] == "AllocationAmount64" && e.Payload[i] is ulong a) amount = (long)a;
            }
            if (type != null) _bytes.AddOrUpdate(type, amount, (_, v) => v + amount);
        }

        public System.Collections.Generic.List<(string type, long bytes)> Top(int n)
            => _bytes.Select(kv => (kv.Key, kv.Value)).OrderByDescending(x => x.Value).Take(n).ToList();
    }
}
