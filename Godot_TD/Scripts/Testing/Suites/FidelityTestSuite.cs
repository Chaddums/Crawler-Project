using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Loads a real battle for every map the territory uses (each layout once per planet) and
    /// measures what sits on the terrain against the surface the player actually sees: field
    /// decor, the Spire, BIT, every buildable tower and every enemy faction (normal and boss)
    /// walking the path. Flags anything floating, buried, overhanging its cell, sitting inside
    /// other geometry, or walking through towers and walls.
    ///
    /// "fidelity" is headless-safe. "fidelity-sheets" also renders an overview, a play view, a
    /// tower sheet and a combatant sheet per map to test-reports/fidelity/ (needs a display).
    /// FIDELITY_SITES=P1:gateway,P2:foundry limits the run to those maps.
    /// </summary>
    public class FidelityTestSuite : ITestSuite
    {
        private readonly bool _sheets;
        public FidelityTestSuite(bool sheets = false) { _sheets = sheets; }
        public string SuiteName => _sheets ? "fidelity-sheets" : "fidelity";

        // Visible gap under the low side of a solid, in world units
        internal const float FloatTol = 0.15f;
        // A walker's feet may sit this far off the surface before it reads as floating or wading
        internal const float WalkTol = 0.2f;
        // How far a tower may reach past its cell edge at rest (walkers in the next lane
        // brush anything further out)
        internal const float OverhangTol = 0.05f;
        // How deep a character may cut into a tower or wall before it reads as clipping
        internal const float ClipTol = 0.3f;
        // Bosses are drawn at BOSS_SCALE in the same one-cell lanes, so they may brush a little more
        internal const float BossClipTol = 0.45f;

        internal static readonly VineNodeType[] Towers =
        {
            VineNodeType.DamageTower, VineNodeType.SlowField, VineNodeType.ScatterCannon, VineNodeType.TeslaCoil,
            VineNodeType.FlakBattery, VineNodeType.BarrierWall, VineNodeType.PushPull, VineNodeType.BuffEmitter,
        };

        internal static readonly (VineEnemyFaction faction, bool boss)[] Combatants =
        {
            (VineEnemyFaction.Scavenger, false), (VineEnemyFaction.Brute, false),
            (VineEnemyFaction.Ghost, false), (VineEnemyFaction.Swarm, false),
            (VineEnemyFaction.Scavenger, true), (VineEnemyFaction.Brute, true),
            (VineEnemyFaction.Ghost, true), (VineEnemyFaction.Swarm, true),
        };

        internal record Site(int Planet, string Id, string Layout);

        private string _outDir;

        public async Task Run(TestContext ctx)
        {
            _outDir = ProjectSettings.GlobalizePath("res://test-reports/fidelity");
            System.IO.Directory.CreateDirectory(_outDir);
            var sites = SitesToCheck();
            ctx.StartTest();
            ctx.Assert(sites.Count > 0, "fidelity/sites_found", "No territory sites with a map layout");
            await CheckSpireModels(ctx);

            foreach (var site in sites)
            {
                string p = $"fidelity/P{site.Planet}/{site.Layout}";
                var rep = new StringBuilder();
                rep.Append($"{{\"planet\":{site.Planet},\"site\":\"{site.Id}\",\"layout\":\"{site.Layout}\"");
                bool inBuild = await LoadBattle(ctx, site);
                ctx.StartTest();
                ctx.Assert(inBuild, $"{p}/battle_loads", $"Battle on {site.Id} should reach Build phase");
                if (!inBuild) continue;
                await Frames(ctx, 20);

                var scene = ctx.Tree.CurrentScene;
                var grid = FindFirst<VineGrid>(scene);
                if (grid == null) { ctx.Assert(false, $"{p}/grid_exists"); continue; }
                var surface = new Surface(grid);
                var cam = scene.GetViewport().GetCamera3D();

                if (_sheets) await MapShots(ctx, site, grid, cam);
                CheckFieldDecor(ctx, p, grid, surface, rep);
                CheckSpireAndBit(ctx, p, scene, grid, surface, rep);
                var towers = PlaceTowers(grid);
                await Frames(ctx, 10);
                CheckTowers(ctx, p, grid, surface, towers, rep);
                if (_sheets) await TowerSheet(ctx, site, towers, cam);
                await CheckCombatants(ctx, p, site, scene, grid, surface, towers, cam, rep);

                rep.Append('}');
                System.IO.File.WriteAllText($"{_outDir}/P{site.Planet}_{site.Layout}.json", rep.ToString());
            }

            Engine.TimeScale = 1.0;
            GameManager.Instance.CurrentPlanet = 1;
            GameManager.Instance.CurrentTerritorySectionId = null;
        }

        /// <summary>Each role's Spire model stands on the point it's placed at.</summary>
        private static async Task CheckSpireModels(TestContext ctx)
        {
            var gm = GameManager.Instance;
            string saved = gm.SelectedRole;
            var stage = new Node3D { Name = "SpireStage" };
            ctx.Tree.Root.AddChild(stage);
            foreach (var role in new[] { "Obelisk", "Arcanist", "Bruteforge" })
            {
                ctx.StartTest();
                gm.SelectedRole = role;
                var h = new VineHarvester();
                stage.AddChild(h);
                await Frames(ctx, 2);
                if (h.VisualRoot != null && MeshBounds(h.VisualRoot, out var b, true))
                {
                    float bottom = b.Position.Y - h.GlobalPosition.Y;
                    ctx.Assert(bottom <= 0.05f && bottom >= -0.4f, $"fidelity/spire/{role}/stands_on_ground",
                        $"Model base is {F(bottom)} from the Spire's ground point");
                }
                h.QueueFree();
                await Frames(ctx, 1);
            }
            gm.SelectedRole = saved;
            stage.QueueFree();
        }

        internal static List<Site> SitesToCheck()
        {
            var only = System.Environment.GetEnvironmentVariable("FIDELITY_SITES")?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var list = new List<Site>();
            foreach (int planet in new[] { 1, 2 })
            {
                var seen = new HashSet<string>();
                var terr = TerritoryManager.GetPlanet(planet);
                if (terr == null) continue;
                foreach (var region in terr.Regions)
                foreach (var s in region.Sites)
                {
                    if (string.IsNullOrEmpty(s.MapLayout) || s.IsBossSite || !seen.Add(s.MapLayout)) continue;
                    if (only != null && only.Length > 0 && !only.Contains($"P{planet}:{s.MapLayout}")) continue;
                    list.Add(new Site(planet, s.Id, s.MapLayout));
                }
            }
            return list;
        }

        private static async Task<bool> LoadBattle(TestContext ctx, Site site)
        {
            var gm = GameManager.Instance;
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = site.Planet;
            gm.CurrentTerritorySectionId = site.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            return await ctx.WaitForPhase(GamePhase.Build, 30f);
        }

        // ── The surface the player sees ──

        /// <summary>
        /// Height of the rendered terrain: two triangles per cell split from (x+1,z) to (x,z+1),
        /// the same way VineGrid.RebuildTerrainMesh builds it.
        /// </summary>
        internal sealed class Surface
        {
            private readonly VineGrid _g;
            private readonly float _cs = Constants.VINE_CELL_SIZE;
            public Surface(VineGrid g) { _g = g; }
            public float Width => _g.Width * _cs;
            public float Depth => _g.Height * _cs;

            private float Corner(int ix, int iz) => _g.GetWorldHeight(ix * _cs, iz * _cs);

            public float At(float x, float z)
            {
                float fx = Mathf.Clamp(x / _cs, 0f, _g.Width - 0.0001f), fz = Mathf.Clamp(z / _cs, 0f, _g.Height - 0.0001f);
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                float h00 = Corner(ix, iz), h10 = Corner(ix + 1, iz), h01 = Corner(ix, iz + 1), h11 = Corner(ix + 1, iz + 1);
                if (tx + tz <= 1f) return h00 + (h10 - h00) * tx + (h01 - h00) * tz;
                return h11 + (h01 - h11) * (1f - tx) + (h10 - h11) * (1f - tz);
            }

            /// <summary>Lowest, highest and centre surface height under a footprint.</summary>
            public (float min, float max, float center) Under(Aabb box, float inset = 0.05f)
            {
                float x0 = Mathf.Clamp(box.Position.X + inset, 0, Width), x1 = Mathf.Clamp(box.End.X - inset, 0, Width);
                float z0 = Mathf.Clamp(box.Position.Z + inset, 0, Depth), z1 = Mathf.Clamp(box.End.Z - inset, 0, Depth);
                if (x1 < x0) x1 = x0;
                if (z1 < z0) z1 = z0;
                float mn = float.MaxValue, mx = float.MinValue;
                for (int i = 0; i <= 4; i++)
                for (int j = 0; j <= 4; j++)
                {
                    float h = At(Mathf.Lerp(x0, x1, i / 4f), Mathf.Lerp(z0, z1, j / 4f));
                    mn = Mathf.Min(mn, h);
                    mx = Mathf.Max(mx, h);
                }
                var c = box.GetCenter();
                return (mn, mx, At(c.X, c.Z));
            }

            public bool OnField(Vector3 p, float margin = 0f) =>
                p.X >= -margin && p.Z >= -margin && p.X <= Width + margin && p.Z <= Depth + margin;
        }

        /// <summary>
        /// World bounds of every visible mesh under root. precise uses the triangles (static
        /// meshes only; skinned meshes keep their bind-pose box).
        /// </summary>
        internal static bool MeshBounds(Node root, out Aabb box, bool precise = false)
        {
            box = default;
            bool any = false;
            foreach (var m in All<MeshInstance3D>(root))
            {
                if (m.Mesh == null || !m.IsVisibleInTree()) continue;
                if (precise && m.Skin == null && m.Skeleton.IsEmpty)
                {
                    var xf = m.GlobalTransform;
                    foreach (var v in m.Mesh.GetFaces())
                    {
                        var w = xf * v;
                        if (!any) { box = new Aabb(w, Vector3.Zero); any = true; }
                        else box = box.Expand(w);
                    }
                }
                else
                {
                    var b = m.GlobalTransform * m.GetAabb();
                    if (!any) { box = b; any = true; }
                    else box = box.Merge(b);
                }
            }
            return any;
        }

        /// <summary>Farthest a static mesh reaches from a vertical axis, in XZ.</summary>
        internal static float Reach(Node root, Vector3 axis)
        {
            float r = 0f;
            foreach (var m in All<MeshInstance3D>(root))
            {
                if (m.Mesh == null || !m.IsVisibleInTree()) continue;
                var xf = m.GlobalTransform;
                foreach (var v in m.Mesh.GetFaces())
                {
                    var w = xf * v;
                    r = Mathf.Max(r, new Vector2(w.X - axis.X, w.Z - axis.Z).Length());
                }
            }
            return r;
        }

        private static string F(float v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        // ── Field decor: walls, elevated blocks, props, hazards, markers ──

        internal record Decor(Node3D Node, Aabb Box, Vector2I Cell, bool Flat);

        internal static List<Decor> FieldDecor(VineGrid grid)
        {
            var list = new List<Decor>();
            foreach (var child in grid.GetChildren())
            {
                // Map builders add plain Node3D holders per cell; towers, links, the Spire and
                // the terrain are their own types
                if (child is not Node3D n || child.GetType() != typeof(Node3D) || !n.IsVisibleInTree()) continue;
                if (!MeshBounds(n, out var box)) continue;
                if (box.Size.X > 6f || box.Size.Z > 6f) continue;
                list.Add(new Decor(n, box, grid.WorldToGrid(box.GetCenter()), box.Size.Y < 0.2f));
            }
            return list;
        }

        private static void CheckFieldDecor(TestContext ctx, string p, VineGrid grid, Surface surf, StringBuilder rep)
        {
            ctx.StartTest();
            var decor = FieldDecor(grid);
            var floating = new List<string>();
            var buriedOverlay = new List<string>();
            var hoveringOverlay = new List<string>();
            foreach (var d in decor)
            {
                if (!surf.OnField(d.Box.GetCenter())) continue;
                var g = surf.Under(d.Box);
                string id = $"{grid.GetCell(d.Cell)}@{d.Cell.X},{d.Cell.Y}";
                if (!d.Flat)
                {
                    float gap = d.Box.Position.Y - g.min;
                    if (gap > FloatTol) floating.Add($"{id} gap {F(gap)}");
                }
                else
                {
                    float poke = g.max - d.Box.End.Y;
                    if (poke > 0.02f) buriedOverlay.Add($"{id} terrain {F(poke)} above it");
                    float hover = d.Box.Position.Y - g.min;
                    if (hover > 0.3f) hoveringOverlay.Add($"{id} {F(hover)} over the low side");
                }
            }
            rep.Append($",\"decor\":{decor.Count},\"decor_floating\":{Json(floating)},\"overlay_buried\":{Json(buriedOverlay)},\"overlay_hovering\":{Json(hoveringOverlay)}");
            ctx.Assert(floating.Count == 0, $"{p}/decor_grounded",
                $"{floating.Count} of {decor.Count} decor pieces float over the terrain: {string.Join("; ", floating.Take(6))}");
            ctx.Assert(buriedOverlay.Count == 0, $"{p}/overlays_visible",
                $"{buriedOverlay.Count} flat overlays are partly under the terrain: {string.Join("; ", buriedOverlay.Take(6))}");
            ctx.Assert(hoveringOverlay.Count == 0, $"{p}/overlays_on_ground",
                $"{hoveringOverlay.Count} flat overlays hover over a dip: {string.Join("; ", hoveringOverlay.Take(6))}");
        }

        private static void CheckSpireAndBit(TestContext ctx, string p, Node scene, VineGrid grid, Surface surf, StringBuilder rep)
        {
            ctx.StartTest();
            var spire = grid.Harvester?.VisualRoot;
            if (spire != null && MeshBounds(spire, out var sb, true))
            {
                var g = surf.Under(sb, 0.3f);
                float gap = sb.Position.Y - g.min;
                rep.Append($",\"spire_gap\":{F(gap)},\"spire_slope\":{F(g.max - g.min)}");
                ctx.Assert(gap <= FloatTol, $"{p}/spire_grounded", $"Spire base {F(gap)} over the lowest ground under it");
            }
            var bit = FindFirst<VinePlayer>(scene);
            if (bit?.VisualRoot != null && MeshBounds(bit.VisualRoot, out var bb))
            {
                float off = bb.Position.Y - surf.At(bb.GetCenter().X, bb.GetCenter().Z);
                var bp = bit.GlobalPosition;
                rep.Append($",\"bit_offset\":{F(off)},\"bit_at\":[{F(bp.X)},{F(bp.Y)},{F(bp.Z)}],\"bit_ground\":{F(surf.At(bp.X, bp.Z))}");
                ctx.Assert(Mathf.Abs(off) <= WalkTol, $"{p}/bit_grounded", $"BIT's feet are {F(off)} off the ground");
            }
        }

        // ── Towers ──

        internal record PlacedTower(VineNodeType Type, VineNode Node, Vector2I Cell, bool Elevated);

        /// <summary>
        /// One of each buildable tower beside the main path, on the open cells nearest the Spire,
        /// plus one on an elevated cell when the map has them.
        /// </summary>
        private static List<PlacedTower> PlaceTowers(VineGrid grid)
        {
            var placed = new List<PlacedTower>();
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            var region = grid.ActiveEntryRegions.FirstOrDefault() ?? grid.EntryRegions.FirstOrDefault();
            var path = region != null ? pf?.GetCachedPath(region.Center) : null;
            var pathSet = new HashSet<Vector2I>(path ?? new List<Vector2I>());
            var exit = grid.ExitPoint;

            var candidates = new List<Vector2I>();
            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                var c = new Vector2I(x, y);
                if (grid.GetCell(c) != VineCellType.Empty || pathSet.Contains(c)) continue;
                // next to the path so enemies walk past them, and not right on the Spire
                bool besidePath = pathSet.Count == 0 || pathSet.Contains(c + Vector2I.Left) || pathSet.Contains(c + Vector2I.Right)
                    || pathSet.Contains(c + Vector2I.Up) || pathSet.Contains(c + Vector2I.Down);
                if (besidePath && (c - exit).LengthSquared() > 4) candidates.Add(c);
            }
            // closest to the Spire first, two cells apart so towers don't touch
            candidates.Sort((a, b) => (a - exit).LengthSquared().CompareTo((b - exit).LengthSquared()));
            var used = new List<Vector2I>();
            int t = 0;
            foreach (var c in candidates)
            {
                if (t >= Towers.Length) break;
                if (used.Any(u => Mathf.Abs(u.X - c.X) <= 1 && Mathf.Abs(u.Y - c.Y) <= 1)) continue;
                var node = Place(grid, Towers[t], c);
                if (node == null) continue;
                placed.Add(new PlacedTower(Towers[t], node, c, false));
                used.Add(c);
                t++;
            }

            // One tower on an elevated cell, if the map has one
            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                var c = new Vector2I(x, y);
                if (!grid.IsElevated(c)) continue;
                var node = Place(grid, VineNodeType.DamageTower, c);
                if (node != null) { placed.Add(new PlacedTower(VineNodeType.DamageTower, node, c, true)); return placed; }
            }
            return placed;
        }

        private static VineNode Place(VineGrid grid, VineNodeType type, Vector2I cell)
        {
            var data = VineNodeRegistry.Get(type);
            if (data == null || !grid.CanPlace(cell)) return null;
            var node = new VineNode();
            node.Initialize(data);
            if (grid.PlaceNode(node, cell)) return node;
            node.QueueFree();
            return null;
        }

        private static void CheckTowers(TestContext ctx, string p, VineGrid grid, Surface surf, List<PlacedTower> towers, StringBuilder rep)
        {
            float half = Constants.VINE_CELL_SIZE / 2f;
            var decor = FieldDecor(grid);
            var rows = new List<string>();
            foreach (var t in towers)
            {
                ctx.StartTest();
                string tp = $"{p}/{t.Type}{(t.Elevated ? "_elevated" : "")}";
                var vis = t.Node.VisualRoot;
                if (vis == null || !MeshBounds(vis, out var box, true))
                {
                    ctx.Assert(false, $"{tp}/has_visual", "Tower has nothing visible");
                    continue;
                }
                var center = grid.GridToWorld(t.Cell);
                var g = surf.Under(box);
                // What the tower stands on: its own base, or the footing under it
                float bottom = box.Position.Y;
                if (t.Node.Footing != null && MeshBounds(t.Node.Footing, out var fb)) bottom = Mathf.Min(bottom, fb.Position.Y);
                float gap = bottom - g.min;                      // air under the low side
                float buried = g.max - box.Position.Y;           // model base under the high side
                float overhang = Mathf.Max(Mathf.Max(center.X - half - box.Position.X, box.End.X - (center.X + half)),
                                           Mathf.Max(center.Z - half - box.Position.Z, box.End.Z - (center.Z + half)));
                float reach = Reach(vis, center);
                var inside = decor.Where(d => d.Cell == t.Cell && !d.Flat && d.Box.Intersects(box.Grow(-0.05f)))
                                  .Select(d => $"decor top {F(d.Box.End.Y - box.Position.Y)} above the tower's base").ToList();

                rows.Add($"{{\"type\":\"{t.Type}\",\"cell\":[{t.Cell.X},{t.Cell.Y}],\"elevated\":{(t.Elevated ? "true" : "false")},\"gap\":{F(gap)},\"buried\":{F(buried)},\"slope\":{F(g.max - g.min)},\"overhang\":{F(overhang)},\"reach\":{F(reach)},\"height\":{F(box.Size.Y)},\"inside_decor\":{Json(inside)}}}");
                ctx.Assert(gap <= FloatTol, $"{tp}/grounded", $"{F(gap)} of air under the downhill side (slope {F(g.max - g.min)})");
                ctx.Assert(overhang <= OverhangTol, $"{tp}/within_cell", $"Reaches {F(overhang)} past its cell edge");
                ctx.Assert(inside.Count == 0, $"{tp}/clear_of_decor", $"Sits inside the cell's decor: {string.Join("; ", inside)}");
            }
            rep.Append($",\"towers\":[{string.Join(",", rows)}]");
        }

        // ── Combatants walking the path ──

        private sealed class Track
        {
            public string Name;
            public VineEnemy Enemy;
            public float Expected;            // where the model's bottom should sit over the ground
            public int Samples, Off;
            public float MinOff = float.MaxValue, MaxOff = float.MinValue;
            public float WorstClip;
            public string WorstClipWith = "";
            public float Height, Width;
            public float LastOff;
            public Vector3 LastCenter, Velocity;
            public Image Shot, ClipShot;
            public float ShotOff, ClipShotDepth;
            public Vector3 ClipAt;
            public float WorstPen;            // deepest the movement body (circle) got into a solid cell
            public float MinOut, MaxOut;      // bottom below / above all the ground under the footprint
            public string WorstPenAt = "";
        }

        /// <summary>How far a walker's body circle reaches into the nearest solid cell.</summary>
        private static float CellPenetration(VineGrid grid, Vector3 p, float r, out Vector2I hit)
        {
            float cs = Constants.VINE_CELL_SIZE, worst = 0f;
            hit = new Vector2I(-1, -1);
            for (int x = Mathf.FloorToInt((p.X - r) / cs); x <= Mathf.FloorToInt((p.X + r) / cs); x++)
            for (int z = Mathf.FloorToInt((p.Z - r) / cs); z <= Mathf.FloorToInt((p.Z + r) / cs); z++)
            {
                if (!grid.InBounds(new Vector2I(x, z)) || grid.IsWalkable(x, z)) continue;
                float dx = p.X - Mathf.Clamp(p.X, x * cs, x * cs + cs), dz = p.Z - Mathf.Clamp(p.Z, z * cs, z * cs + cs);
                float pen = r - Mathf.Sqrt(dx * dx + dz * dz);
                if (pen > worst) { worst = pen; hit = new Vector2I(x, z); }
            }
            return worst;
        }

        private async Task CheckCombatants(TestContext ctx, string p, Site site, Node scene, VineGrid grid, Surface surf,
            List<PlacedTower> towers, Camera3D cam, StringBuilder rep)
        {
            var region = grid.ActiveEntryRegions.FirstOrDefault() ?? grid.EntryRegions.FirstOrDefault();
            if (region == null) { ctx.Assert(false, $"{p}/entry_exists"); return; }

            // Solids a walker must not cut through: tower models and wall-cell decor
            // (with their cell: enemy fire can destroy a tower mid-run, and then its cell is open)
            var solids = new List<(string name, Aabb box, Vector2I cell)>();
            foreach (var t in towers)
                if (t.Node.VisualRoot != null && MeshBounds(t.Node.VisualRoot, out var tb, true))
                {
                    if (t.Node.Footing != null && MeshBounds(t.Node.Footing, out var fb)) tb = tb.Merge(fb);
                    solids.Add(($"{t.Type}", tb, t.Cell));
                }
            foreach (var d in FieldDecor(grid))
                if (!d.Flat && grid.GetCell(d.Cell) == VineCellType.Wall && grid.Harvester != null && d.Cell != grid.ExitPoint)
                    solids.Add(($"wall@{d.Cell.X},{d.Cell.Y}", d.Box, d.Cell));

            var tracks = new List<Track>();
            var parent = scene;
            Engine.TimeScale = 2.0;
            float elapsed = 0f, nextSpawn = 0f, nextShot = 9f;
            int spawned = 0;
            while (elapsed < 20f)
            {
                if (spawned < Combatants.Length && elapsed >= nextSpawn)
                {
                    var (faction, boss) = Combatants[spawned];
                    var e = new VineEnemy();
                    parent.AddChild(e);
                    // Spread along the entry so they don't walk inside each other
                    var spawn = region.Cells.Count > 0 ? region.Cells[(spawned * 5 + region.Cells.Count / 2) % region.Cells.Count] : region.Center;
                    e.Initialize($"Fidelity {faction}{(boss ? " boss" : "")}", faction, 1_000_000f, 2.2f, 0,
                        new Color(0.8f, 0.4f, 0.3f), spawn, boss);
                    var tr = new Track { Name = $"{faction}{(boss ? "_boss" : "")}", Enemy = e,
                        Expected = faction == VineEnemyFaction.Swarm ? Constants.SWARM_HOVER_HEIGHT : 0f };
                    tracks.Add(tr);
                    spawned++;
                    nextSpawn += 1.25f;
                }

                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
                elapsed += (float)ctx.Tree.Root.GetProcessDeltaTime();

                foreach (var tr in tracks)
                {
                    if (!GodotObject.IsInstanceValid(tr.Enemy) || tr.Enemy.VisualRoot == null) continue;
                    if (!MeshBounds(tr.Enemy.VisualRoot, out var b)) continue;
                    var c = b.GetCenter();
                    if (!surf.OnField(c, -0.5f)) continue; // still on the approach march outside the field
                    float off = b.Position.Y - surf.At(c.X, c.Z) - tr.Expected;
                    if (tr.Samples > 0) tr.Velocity = (c - tr.LastCenter) / Mathf.Max(0.001f, (float)ctx.Tree.Root.GetProcessDeltaTime());
                    tr.LastCenter = c;
                    tr.Samples++;
                    tr.MinOff = Mathf.Min(tr.MinOff, off);
                    tr.MaxOff = Mathf.Max(tr.MaxOff, off);
                    tr.Height = Mathf.Max(tr.Height, b.Size.Y);
                    tr.Width = Mathf.Max(tr.Width, Mathf.Max(b.Size.X, b.Size.Z));
                    tr.LastOff = off;
                    // Off the ground = the model's bottom is below all of the ground under its
                    // footprint (wading) or above all of it (floating). Measuring against the
                    // ground at the box centre alone failed a level drone on a 1.5 ramp whose
                    // mesh sits 0.3 behind its origin.
                    var under = surf.Under(b, Mathf.Min(b.Size.X, b.Size.Z) * 0.15f);
                    float feet = b.Position.Y - tr.Expected;
                    float outside = feet > under.max ? feet - under.max : feet < under.min ? feet - under.min : 0f;
                    if (Mathf.Abs(outside) > WalkTol + 0.1f * tr.Height) tr.Off++;
                    tr.MinOut = Mathf.Min(tr.MinOut, outside);
                    tr.MaxOut = Mathf.Max(tr.MaxOut, outside);
                    // The movement body itself: never inside a tower, wall or pit cell
                    float pen = CellPenetration(grid, tr.Enemy.GlobalPosition, tr.Enemy.BodyRadius, out var penCell);
                    if (pen > tr.WorstPen)
                    {
                        tr.WorstPen = pen;
                        var gp = tr.Enemy.GlobalPosition;
                        tr.WorstPenAt = $"{grid.GetCell(penCell)}@{penCell.X},{penCell.Y} (r {F(tr.Enemy.BodyRadius)}, t {F(elapsed)}, at {F(gp.X)},{F(gp.Z)})";
                    }
                    // XZ overlap depth with each solid it shares height with (shrunk: silhouettes aren't boxes)
                    var core = b.Grow(-Mathf.Min(b.Size.X, b.Size.Z) * 0.15f);
                    foreach (var (name, sbox, scell) in solids)
                    {
                        if (grid.IsWalkable(scell)) continue; // destroyed since
                        if (core.Position.Y > sbox.End.Y || core.End.Y < sbox.Position.Y) continue;
                        float dx = Mathf.Min(core.End.X, sbox.End.X) - Mathf.Max(core.Position.X, sbox.Position.X);
                        float dz = Mathf.Min(core.End.Z, sbox.End.Z) - Mathf.Max(core.Position.Z, sbox.Position.Z);
                        if (dx <= 0 || dz <= 0) continue;
                        float depth = Mathf.Min(dx, dz);
                        if (depth > tr.WorstClip)
                        {
                            tr.WorstClip = depth;
                            var ep = tr.Enemy.GlobalPosition;
                            var ec = grid.WorldToGrid(ep);
                            tr.WorstClipWith = $"{name} (body {F(b.Position.X)}..{F(b.End.X)},{F(b.Position.Z)}..{F(b.End.Z)} at {F(ep.X)},{F(ep.Z)} r {F(tr.Enemy.BodyRadius)} cell {ec.X},{ec.Y} {grid.GetCell(ec)}{(grid.IsWalkable(ec) ? " walkable" : "")}{(tr.Enemy.IsAlive ? "" : " dead")} t {F(elapsed)} vs {F(sbox.Position.X)}..{F(sbox.End.X)},{F(sbox.Position.Z)}..{F(sbox.End.Z)})";
                            tr.ClipAt = new Vector3((Mathf.Max(core.Position.X, sbox.Position.X) + Mathf.Min(core.End.X, sbox.End.X)) / 2f,
                                Mathf.Max(core.Position.Y, sbox.Position.Y), (Mathf.Max(core.Position.Z, sbox.Position.Z) + Mathf.Min(core.End.Z, sbox.End.Z)) / 2f);
                        }
                    }
                }

                if (_sheets)
                {
                    // A clearly bad moment, side-on; anything that never gets that bad gets a shot at 9 s
                    foreach (var tr in tracks)
                    {
                        if (tr.Shot != null || !GodotObject.IsInstanceValid(tr.Enemy) || tr.Samples < 3) continue;
                        // Clearly off the ground right now (wading or floating by most of a foot)
                        bool bad = Mathf.Abs(tr.LastOff) > 0.8f;
                        if (bad || elapsed >= nextShot + tracks.IndexOf(tr) * 0.4f)
                            tr.Shot = await SideShot(ctx, cam, tr);
                    }
                    // The deepest overlap with a tower or wall so far, looked at from above and aside
                    foreach (var tr in tracks)
                    {
                        if (!GodotObject.IsInstanceValid(tr.Enemy) || tr.WorstClip < 0.45f || tr.WorstClip < tr.ClipShotDepth + 0.25f) continue;
                        tr.ClipShot = await ClipShot(ctx, cam, tr);
                        tr.ClipShotDepth = tr.WorstClip;
                    }
                }
            }
            Engine.TimeScale = 1.0;
            if (_sheets) SaveCombatantSheet(site, tracks);

            var rows = new List<string>();
            foreach (var tr in tracks)
            {
                ctx.StartTest();
                string cp = $"{p}/{tr.Name}";
                float share = tr.Samples > 0 ? (float)tr.Off / tr.Samples : 0f;
                rows.Add($"{{\"name\":\"{tr.Name}\",\"samples\":{tr.Samples},\"off_share\":{F(share)},\"min_off\":{F(tr.Samples > 0 ? tr.MinOff : 0)},\"max_off\":{F(tr.Samples > 0 ? tr.MaxOff : 0)},\"below_ground\":{F(tr.MinOut)},\"above_ground\":{F(tr.MaxOut)},\"clip\":{F(tr.WorstClip)},\"clip_with\":\"{tr.WorstClipWith}\",\"body_in_solid\":{F(tr.WorstPen)},\"height\":{F(tr.Height)},\"width\":{F(tr.Width)}}}");
                ctx.Assert(tr.Samples > 0, $"{cp}/walked_on_field", "Never reached the field");
                ctx.Assert(share <= 0.05f, $"{cp}/follows_ground",
                    $"Off the ground in {share:P0} of frames (feet up to {F(tr.MinOut)} below and {F(tr.MaxOut)} above the ground under them)");
                // Ghosts phase through walls and towers by design (and fade while they do)
                if (!tr.Name.StartsWith("Ghost"))
                {
                    ctx.Assert(tr.WorstPen <= 0.05f, $"{cp}/stays_out_of_solids",
                        $"Body {F(tr.WorstPen)} inside {tr.WorstPenAt}");
                    float tol = tr.Name.EndsWith("_boss") ? BossClipTol : ClipTol;
                    ctx.Assert(tr.WorstClip <= tol, $"{cp}/no_clipping", $"Cut {F(tr.WorstClip)} into {tr.WorstClipWith}");
                }
                if (GodotObject.IsInstanceValid(tr.Enemy)) tr.Enemy.QueueFree();
            }
            rep.Append($",\"combatants\":[{string.Join(",", rows)}]");
            await Frames(ctx, 2);
        }

        // ── Sheets ──

        private async Task MapShots(TestContext ctx, Site site, VineGrid grid, Camera3D cam)
        {
            if (cam == null) return;
            HideHud(ctx);
            cam.SetProcess(false);
            cam.SetPhysicsProcess(false);
            float cs = Constants.VINE_CELL_SIZE;
            var mid = new Vector3(grid.Width * cs / 2f, 0, grid.Height * cs / 2f);
            mid.Y = new Surface(grid).At(mid.X, mid.Z);
            Aim(cam, mid, Constants.CAMERA_MAX_ZOOM * 0.45f);
            (await Grab(ctx)).SavePng($"{_outDir}/P{site.Planet}_{site.Layout}_overview.png");
            var exit = grid.GridToWorld(grid.ExitPoint);
            Aim(cam, exit, Constants.CAMERA_HEIGHT);
            (await Grab(ctx)).SavePng($"{_outDir}/P{site.Planet}_{site.Layout}_play.png");
            // The Spire's foot, low and close
            cam.GlobalPosition = exit + new Vector3(3.5f, 1.4f, 3.5f);
            cam.LookAt(exit + new Vector3(0, 0.5f, 0), Vector3.Up);
            Square(await Grab(ctx), 480).SavePng($"{_outDir}/P{site.Planet}_{site.Layout}_spire.png");
        }

        private async Task TowerSheet(TestContext ctx, Site site, List<PlacedTower> towers, Camera3D cam)
        {
            if (cam == null || towers.Count == 0) return;
            const int cell = 400, cols = 3;
            int rowsN = (towers.Count + cols - 1) / cols;
            var sheet = Image.CreateEmpty(cell * cols, cell * rowsN, false, Image.Format.Rgba8);
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                // From the downhill side, low and close, where a gap under the base shows
                var grid = t.Node.GetParent() as VineGrid;
                var c = t.Node.GlobalPosition;
                var down = Vector3.Back;
                if (grid != null)
                {
                    var surf = new Surface(grid);
                    float best = float.MaxValue;
                    foreach (var d in new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) })
                    {
                        float h = surf.At(c.X + d.X * 0.95f, c.Z + d.Z * 0.95f);
                        if (h < best) { best = h; down = d.Normalized(); }
                    }
                }
                var look = c - new Vector3(0, Constants.NODE_ORIGIN_HEIGHT * 0.6f, 0);
                cam.GlobalPosition = look + down * 4.2f + Vector3.Up * 1.7f;
                cam.LookAt(look, Vector3.Up);
                var img = Square(await Grab(ctx), cell);
                sheet.BlitRect(img, new Rect2I(0, 0, cell, cell), new Vector2I(i % cols * cell, i / cols * cell));
            }
            sheet.SavePng($"{_outDir}/P{site.Planet}_{site.Layout}_towers.png");
            System.IO.File.WriteAllText($"{_outDir}/P{site.Planet}_{site.Layout}_towers.txt",
                string.Join("\n", towers.Select(t => $"{t.Type}{(t.Elevated ? " (elevated)" : "")} {t.Cell}")));
        }

        /// <summary>Freeze, look at the walker side-on at foot level, unfreeze.</summary>
        private static async Task<Image> SideShot(TestContext ctx, Camera3D cam, Track tr)
        {
            if (cam == null || !MeshBounds(tr.Enemy.VisualRoot, out var b)) return null;
            double saved = Engine.TimeScale;
            Engine.TimeScale = 0.0;
            var heading = new Vector3(tr.Velocity.X, 0, tr.Velocity.Z);
            if (heading.LengthSquared() < 0.0001f) heading = Vector3.Forward;
            var side = heading.Normalized().Cross(Vector3.Up);
            float dist = Mathf.Max(3.2f, b.Size.Y * 2.2f);
            var feet = new Vector3(b.GetCenter().X, b.Position.Y + b.Size.Y * 0.35f, b.GetCenter().Z);
            // Whichever side has open ground under the camera, so it isn't inside a wall
            var grid = tr.Enemy.GetParent()?.GetChildren().OfType<VineGrid>().FirstOrDefault();
            if (grid != null && !grid.IsWalkable(grid.WorldToGrid(feet + side * dist))
                && grid.IsWalkable(grid.WorldToGrid(feet - side * dist)))
                side = -side;
            cam.GlobalPosition = feet + side * dist + Vector3.Up * dist * 0.28f;
            cam.LookAt(feet, Vector3.Up);
            var img = Square(await Grab(ctx), 400);
            Engine.TimeScale = saved;
            tr.ShotOff = tr.LastOff;
            return img;
        }

        private static async Task<Image> ClipShot(TestContext ctx, Camera3D cam, Track tr)
        {
            if (cam == null) return null;
            double saved = Engine.TimeScale;
            Engine.TimeScale = 0.0;
            var at = tr.ClipAt;
            float dist = Mathf.Max(3.5f, tr.Height * 2f);
            cam.GlobalPosition = at + new Vector3(dist * 0.55f, dist * 0.8f, dist * 0.55f);
            cam.LookAt(at, Vector3.Up);
            var img = Square(await Grab(ctx), 400);
            Engine.TimeScale = saved;
            return img;
        }

        private void SaveCombatantSheet(Site site, List<Track> tracks)
        {
            var clips = tracks.Where(t => t.ClipShot != null).ToList();
            if (clips.Count > 0)
            {
                const int c = 400, n = 4;
                var cs = Image.CreateEmpty(c * n, c * ((clips.Count + n - 1) / n), false, Image.Format.Rgba8);
                for (int i = 0; i < clips.Count; i++)
                    cs.BlitRect(clips[i].ClipShot, new Rect2I(0, 0, c, c), new Vector2I(i % n * c, i / n * c));
                cs.SavePng($"{_outDir}/P{site.Planet}_{site.Layout}_clips.png");
                System.IO.File.WriteAllText($"{_outDir}/P{site.Planet}_{site.Layout}_clips.txt",
                    string.Join("\n", clips.Select(t => $"{t.Name}\t{F(t.ClipShotDepth)} into {t.WorstClipWith}")));
            }
            var shots = tracks.Where(t => t.Shot != null).ToList();
            if (shots.Count == 0) return;
            const int cell = 400, cols = 4;
            int rowsN = (shots.Count + cols - 1) / cols;
            var sheet = Image.CreateEmpty(cell * cols, cell * rowsN, false, Image.Format.Rgba8);
            for (int i = 0; i < shots.Count; i++)
                sheet.BlitRect(shots[i].Shot, new Rect2I(0, 0, cell, cell), new Vector2I(i % cols * cell, i / cols * cell));
            sheet.SavePng($"{_outDir}/P{site.Planet}_{site.Layout}_combatants.png");
            System.IO.File.WriteAllText($"{_outDir}/P{site.Planet}_{site.Layout}_combatants.txt",
                string.Join("\n", shots.Select(t => $"{t.Name}\toff {F(t.ShotOff)}")));
        }

        /// <summary>Play-camera framing: 55 degree pitch, zoom = distance to the focus.</summary>
        private static void Aim(Camera3D cam, Vector3 focus, float zoom)
        {
            float a = Mathf.DegToRad(Constants.CAMERA_ANGLE);
            cam.GlobalPosition = focus + new Vector3(0, zoom * Mathf.Sin(a), zoom * Mathf.Cos(a));
            cam.LookAt(focus, Vector3.Up);
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

        // ── Helpers ──

        private static string Json(List<string> items) =>
            "[" + string.Join(",", items.Select(s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")) + "]";

        private static T FindFirst<T>(Node root) where T : class => All<T>(root).FirstOrDefault();

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
