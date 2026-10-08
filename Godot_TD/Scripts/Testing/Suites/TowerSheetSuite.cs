using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Renders every buildable tower and every Spire the way a battle builds them, on both
    /// planets: a close-up of each, the towers side by side from the play camera, and each
    /// role's Spire. Writes test-reports/towers/ (TOWER_TAG names the set). Needs a display.
    /// </summary>
    public class TowerSheetSuite : ITestSuite
    {
        public string SuiteName => "tower-sheets";

        private string _outDir;
        private string _tag;

        public async Task Run(TestContext ctx)
        {
            _outDir = ProjectSettings.GlobalizePath("res://test-reports/towers");
            System.IO.Directory.CreateDirectory(_outDir);
            _tag = System.Environment.GetEnvironmentVariable("TOWER_TAG") ?? "now";

            var sites = FidelityTestSuite.SitesToCheck();
            foreach (int planet in new[] { 1, 2 })
            {
                var site = sites.FirstOrDefault(s => s.Planet == planet);
                if (site == null) continue;
                bool ok = await FidelityTestSuite.LoadBattle(ctx, site);
                ctx.StartTest();
                ctx.Assert(ok, $"towers/P{planet}/battle_loads");
                if (!ok) continue;
                await Frames(ctx, 20);
                var grid = ServiceLocator.Get<VineGrid>();
                var cam = new Camera3D { Fov = 40f };
                ctx.Tree.CurrentScene.AddChild(cam);
                cam.Current = true;
                foreach (var l in All<CanvasLayer>(ctx.Tree.Root)) l.Visible = false;

                if (System.Environment.GetEnvironmentVariable("TOWER_PERKS") == "1")
                {
                    await PerkShots(ctx, grid, cam, planet);
                }
                else if (System.Environment.GetEnvironmentVariable("TOWER_SPIRES") == "1")
                {
                    if (planet == 2) await SpireShots(ctx, grid, cam, planet);
                }
                else if (System.Environment.GetEnvironmentVariable("TOWER_KIT") == "1")
                {
                    if (planet == 2) await KitShots(ctx, grid, cam);
                }
                else
                {
                    await TowerShots(ctx, grid, cam, planet);
                    await SpireShots(ctx, grid, cam, planet);
                }
                cam.QueueFree();
            }
            Engine.TimeScale = 1.0;
            GameManager.Instance.CurrentPlanet = 1;
            GameManager.Instance.CurrentTerritorySectionId = null;
        }

        private async Task TowerShots(TestContext ctx, VineGrid grid, Camera3D cam, int planet)
        {
            var types = FidelityTestSuite.Towers;
            var row = FindRow(grid, types.Length * 2 + 1);
            ctx.StartTest();
            ctx.Assert(row.Count > 0, $"towers/P{planet}/row_found", "No flat open row for the lineup");
            if (row.Count == 0) return;

            var placed = new List<(VineNodeType type, VineNode node)>();
            for (int i = 0; i < types.Length; i++)
            {
                var data = VineNodeRegistry.Get(types[i]);
                var node = new VineNode();
                node.Initialize(data);
                if (grid.PlaceNode(node, row[1 + i * 2])) placed.Add((types[i], node));
                else node.QueueFree();
            }
            await Frames(ctx, 10);

            var tiles = new List<(Image, string)>();
            foreach (var (type, node) in placed)
            {
                // The model only: range rings and health bars would blow the bounds up
                var vis = (Node)node.VisualRoot ?? node;
                Vector3 focus;
                float dist;
                if (FidelityTestSuite.MeshBounds(vis, out var b, true) && b.Size.Length() < 6f)
                {
                    focus = b.GetCenter();
                    dist = Mathf.Max(2.6f, Mathf.Max(b.Size.X, Mathf.Max(b.Size.Y, b.Size.Z)) * 2.2f);
                }
                else { focus = node.GlobalPosition + Vector3.Up * 0.6f; dist = 3.4f; }
                tiles.Add((await Shot(ctx, cam, focus, 30f, 24f, dist), type.ToString()));
            }
            Save(tiles, $"{_outDir}/{_tag}_P{planet}_towers.png", 4);

            // From straight above with each barrel turned toward a marker due +X: the red dot is
            // the muzzle and should sit on the barrel's end, on the marker's side
            var marker = new Node3D();
            grid.AddChild(marker);
            var aimTiles = new List<(Image, string)>();
            foreach (var (type, node) in placed.Where(t => t.node.Look?.CanAim == true))
            {
                marker.GlobalPosition = node.GlobalPosition + new Vector3(6f, 0, 0);
                node.Look.Track(marker);
                await ctx.Tree.ToSignal(ctx.Tree.CreateTimer(1.6f), SceneTreeTimer.SignalName.Timeout);
                node.Look.Track(marker);
                var dot = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Red, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true } };
                var line = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.02f, 0.02f) },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Lime, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true } };
                grid.AddChild(dot);
                grid.AddChild(line);
                dot.GlobalPosition = node.Look.MuzzleGlobal;
                line.GlobalPosition = node.GlobalPosition + new Vector3(0.8f, 1.5f, 0);
                aimTiles.Add((await Shot(ctx, cam, node.GlobalPosition, 0f, 89f, 4.2f), $"{type} aimed at +X"));
                dot.QueueFree();
                line.QueueFree();
            }
            marker.QueueFree();
            Save(aimTiles, $"{_outDir}/{_tag}_P{planet}_aim.png", 4);

            // The whole row from the play camera's angle
            var a = grid.GridToWorld(row[0]);
            var z = grid.GridToWorld(row[^1]);
            var mid = (a + z) / 2f + Vector3.Up * 0.6f;
            var wide = await Shot(ctx, cam, mid, 0f, Constants.CAMERA_ANGLE, 20f, square: false);
            Save(new List<(Image, string)> { (wide, "row") }, $"{_outDir}/{_tag}_P{planet}_row.png", 1, cell: 0);

            foreach (var (_, node) in placed) grid.RemoveNode(node.GridPosition);
            await Frames(ctx, 2);

            // Walls join up: a line of three with a corner, and one on its own
            var wallCells = new List<Vector2I> { row[2], row[3], row[4], row[4] + Vector2I.Down, row[7] };
            var walls = new List<VineNode>();
            foreach (var c in wallCells)
            {
                var w = new VineNode();
                w.Initialize(VineNodeRegistry.Get(VineNodeType.BarrierWall));
                if (grid.PlaceNode(w, c)) walls.Add(w); else w.QueueFree();
            }
            await Frames(ctx, 40); // wall faces refresh a few times a second
            var wm = (grid.GridToWorld(row[2]) + grid.GridToWorld(row[7])) / 2f + Vector3.Up * 0.5f;
            var wallShot = await Shot(ctx, cam, wm, 25f, 40f, 11f, square: false);
            Save(new List<(Image, string)> { (wallShot, "walls") }, $"{_outDir}/{_tag}_P{planet}_walls.png", 1, cell: 0);
            foreach (var w in walls) grid.RemoveNode(w.GridPosition);
            await Frames(ctx, 2);
        }

        private async Task SpireShots(TestContext ctx, VineGrid grid, Camera3D cam, int planet)
        {
            var gm = GameManager.Instance;
            string saved = gm.SelectedRole;
            var tiles = new List<(Image, string)>();
            var spot = FindRow(grid, 5);
            foreach (var role in new[] { "Obelisk", "Arcanist", "Bruteforge" })
            {
                gm.SelectedRole = role;
                var h = new VineHarvester();
                grid.AddChild(h);
                var at = spot.Count > 0 ? grid.GridToWorld(spot[2]) : grid.GridToWorld(grid.ExitPoint) + new Vector3(8, 0, 0);
                h.SeatOn(grid, at);
                await Frames(ctx, 3);
                FidelityTestSuite.MeshBounds((Node)h.VisualRoot ?? h, out var b, true);
                float dist = Mathf.Max(b.Size.Y, Mathf.Max(b.Size.X, b.Size.Z)) * 1.6f;
                tiles.Add((await Shot(ctx, cam, b.GetCenter(), 30f, 18f, dist), role));
                // And from the play camera's angle, where the base and its guns matter most
                tiles.Add((await Shot(ctx, cam, b.GetCenter() - Vector3.Up * b.Size.Y * 0.25f, 30f, Constants.CAMERA_ANGLE, dist * 0.9f), role + " (play angle)"));
                var dome = h.GetNodeOrNull<Node3D>("ShieldDome");
                if (dome != null && System.Environment.GetEnvironmentVariable("TOWER_SPIRES") == "1")
                {
                    dome.Visible = false;
                    h.SetProcess(false);
                    tiles.Add((await Shot(ctx, cam, b.GetCenter(), 30f, 18f, dist), role + " (no dome)"));
                    h.SetProcess(true);
                }
                h.QueueFree();
                await Frames(ctx, 2);
            }
            gm.SelectedRole = saved;
            Save(tiles, $"{_outDir}/{_tag}_P{planet}_spires.png", 2);
        }

        /// <summary>
        /// Every kit model a tower could be built from, at its normalized scale, one at a time on
        /// the same flat cell, labelled with its size against the 2-unit cell (TOWER_KIT=1).
        /// </summary>
        private async Task KitShots(TestContext ctx, VineGrid grid, Camera3D cam)
        {
            var paths = new[] {
                AssetLibrary.TURRET_A, AssetLibrary.TURRET_B, AssetLibrary.TURRET_C, AssetLibrary.WEAPON_A,
                AssetLibrary.WEAPON_B, AssetLibrary.ROCKET_LAUNCHER, AssetLibrary.PLASMA_GUN,
                AssetLibrary.PROP_ANTENNA_A, AssetLibrary.PROP_ANTENNA_B, AssetLibrary.PROP_RADAR,
                AssetLibrary.PROP_SATELLITE, AssetLibrary.PROP_GENERATOR_A, AssetLibrary.PROP_GENERATOR_B,
                AssetLibrary.PROP_BARRIER_A, AssetLibrary.PROP_BARRIER_B, AssetLibrary.PROP_SANDBAGS,
                AssetLibrary.PROP_HEDGEHOG, AssetLibrary.PROP_CONTAINER_A, AssetLibrary.PROP_CONTAINER_B,
                AssetLibrary.PROP_CRATE_A, AssetLibrary.PROP_CRATE_B, AssetLibrary.PROP_BARREL,
                AssetLibrary.PROP_BARRELS, AssetLibrary.PROP_LAMP_A, AssetLibrary.PROP_LAMP_B,
                AssetLibrary.PROP_FENCE, AssetLibrary.AXIS_POWER_MAST, AssetLibrary.AXIS_REPEATER,
            };
            var row = FindRow(grid, 5);
            var at = grid.GridToWorld(row.Count > 0 ? row[2] : grid.ExitPoint) ;
            at.Y = grid.GetCellHeightRange(row.Count > 0 ? row[2] : grid.ExitPoint).max;
            var tiles = new List<(Image, string)>();
            var sizes = new List<string>();
            foreach (var path in paths)
            {
                var m = AssetLibrary.InstantiateNormalized(path);
                if (m == null) { sizes.Add($"{path}: failed"); continue; }
                var holder = new Node3D();
                ctx.Tree.CurrentScene.AddChild(holder);
                holder.GlobalPosition = at;
                holder.AddChild(m);
                AssetLibrary.CenterAndGround(m);
                await Frames(ctx, 2);
                FidelityTestSuite.MeshBounds(m, out var b, true);
                string name = System.IO.Path.GetFileNameWithoutExtension(path).Replace("KB3D_FTW_", "").Replace("_grp", "");
                string size = $"{b.Size.X:F2}x{b.Size.Z:F2}x{b.Size.Y:F2}";
                sizes.Add($"{name} scale {AssetLibrary.GetNormalizedScale(path)} size {size} (w x d x h)");
                float dist = Mathf.Max(2.6f, Mathf.Max(b.Size.X, Mathf.Max(b.Size.Y, b.Size.Z)) * 2.0f);
                tiles.Add((await Shot(ctx, cam, b.GetCenter(), 30f, 24f, dist), $"{name} {size}"));
                holder.QueueFree();
                await Frames(ctx, 2);
            }
            Save(tiles, $"{_outDir}/{_tag}_kit.png", 6, 300);
            System.IO.File.WriteAllText($"{_outDir}/{_tag}_kit_sizes.txt", string.Join("\n", sizes));
        }

        /// <summary>Each tower before and after its perk (TOWER_PERKS=1).</summary>
        private async Task PerkShots(TestContext ctx, VineGrid grid, Camera3D cam, int planet)
        {
            var gm = GameManager.Instance;
            var row = FindRow(grid, 5);
            if (row.Count == 0) return;
            var tiles = new List<(Image, string)>();
            foreach (var perk in VinePerkRegistry.GetAll().Where(p => p.Offered && p.Tower != null))
            {
                var node = new VineNode();
                node.Initialize(VineNodeRegistry.Get(perk.Tower.Value));
                if (!grid.PlaceNode(node, row[2])) { node.QueueFree(); continue; }
                await Frames(ctx, 5);
                var vis = (Node)node.VisualRoot ?? node;
                FidelityTestSuite.MeshBounds(vis, out var b, true);
                var focus = node.GlobalPosition + Vector3.Up * 0.4f;
                tiles.Add((await Shot(ctx, cam, focus, 30f, 30f, 3.6f), $"{perk.Tower}"));
                gm.AddPerk(perk);
                await ctx.Tree.ToSignal(ctx.Tree.CreateTimer(0.6f), SceneTreeTimer.SignalName.Timeout);
                tiles.Add((await Shot(ctx, cam, focus, 30f, 30f, 3.6f), $"+ {perk.Name}"));
                gm.ActivePerks.RemoveAll(x => x.Id == perk.Id);
                grid.RemoveNode(node.GridPosition);
                await Frames(ctx, 2);
            }
            Save(tiles, $"{_outDir}/{_tag}_P{planet}_perks.png", 4);
        }

        /// <summary>A straight run of open, nearly flat cells (none on the enemies' path).</summary>
        internal static List<Vector2I> FindRow(VineGrid grid, int length)
        {
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            var region = grid.ActiveEntryRegions.FirstOrDefault() ?? grid.EntryRegions.FirstOrDefault();
            var path = new HashSet<Vector2I>(region != null ? pf?.GetCachedPath(region.Center) ?? new List<Vector2I>() : new List<Vector2I>());
            bool Open(Vector2I c) => grid.InBounds(c) && grid.GetCell(c) == VineCellType.Empty && !path.Contains(c);
            float bestSlope = float.MaxValue;
            List<Vector2I> best = new();
            for (int y = 2; y < grid.Height - 2; y++)
            for (int x0 = 1; x0 + length < grid.Width - 1; x0++)
            {
                var run = Enumerable.Range(x0, length).Select(x => new Vector2I(x, y)).ToList();
                if (!run.All(c => Open(c) && Open(c + Vector2I.Down))) continue;
                var hs = run.Select(c => grid.GetCellHeightRange(c)).ToList();
                float slope = hs.Max(h => h.max) - hs.Min(h => h.min);
                if (slope < bestSlope) { bestSlope = slope; best = run; }
            }
            return best;
        }

        private static async Task<Image> Shot(TestContext ctx, Camera3D cam, Vector3 focus, float yawDeg, float pitchDeg, float dist, bool square = true)
        {
            Engine.TimeScale = 1.0;
            await Frames(ctx, 2);
            Engine.TimeScale = 0.0;
            float yaw = Mathf.DegToRad(yawDeg), pitch = Mathf.DegToRad(pitchDeg);
            cam.GlobalPosition = focus + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * dist;
            cam.LookAt(focus, Vector3.Up);
            for (int i = 0; i < 3; i++)
                await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = ctx.Tree.Root.GetViewport().GetTexture().GetImage();
            img.Convert(Image.Format.Rgba8);
            Engine.TimeScale = 1.0;
            if (!square) return img;
            int side = Mathf.Min(img.GetWidth(), img.GetHeight());
            var crop = img.GetRegion(new Rect2I((img.GetWidth() - side) / 2, (img.GetHeight() - side) / 2, side, side));
            crop.Resize(360, 360);
            return crop;
        }

        private static void Save(List<(Image img, string label)> tiles, string path, int cols, int cell = 360)
        {
            if (tiles.Count == 0) return;
            if (cell == 0) { tiles[0].img.SavePng(path); return; }
            int rows = (tiles.Count + cols - 1) / cols;
            var sheet = Image.CreateEmpty(cell * cols, cell * rows, false, Image.Format.Rgba8);
            sheet.Fill(new Color(0.1f, 0.1f, 0.12f));
            for (int i = 0; i < tiles.Count; i++)
                if (tiles[i].img != null)
                {
                    if (tiles[i].img.GetWidth() != cell) tiles[i].img.Resize(cell, cell);
                    sheet.BlitRect(tiles[i].img, new Rect2I(0, 0, cell, cell), new Vector2I((i % cols) * cell, (i / cols) * cell));
                }
            sheet.SavePng(path);
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(path, ".txt"), string.Join("\n", tiles.Select(t => t.label)));
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
