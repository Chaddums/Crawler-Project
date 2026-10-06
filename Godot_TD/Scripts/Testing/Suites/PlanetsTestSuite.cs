using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Loads a real battle on every planet, through the same territory-site path the menu uses,
    /// and checks what a player would see first: no script/shader errors, nothing opaque between
    /// the camera and the grid, and HUD panels that don't sit on top of each other.
    ///
    /// Scrapyard once loaded as a flat haze: the Conversion Dome recolored the grid-centered
    /// haze layers into opaque metal sheets. Every other suite only ever loaded Planet 1.
    /// </summary>
    public class PlanetsTestSuite : ITestSuite
    {
        public string SuiteName => "planets";

        private static readonly int[] Planets = { 1, 2 };

        public async Task Run(TestContext ctx)
        {
            TestDomeConversionScope(ctx);

            var logger = ErrorCaptureLogger.Install();
            foreach (int planet in Planets)
            {
                string p = $"P{planet}";
                logger.Reset();
                bool inBuild = await LoadBattle(ctx, planet);
                ctx.Assert(inBuild, $"planets/{p}/battle_loads", "Battle should reach Build phase");
                if (!inBuild) continue;
                await WaitFrames(ctx, 30); // shaders compile on first draw; dome converts on its first update

                ctx.AssertEqual(planet == 2 ? "Scrapyard" : "Grid Prime", PlanetTheme.Current.PlanetName,
                    $"planets/{p}/theme");
                PerfTestSuite.CheckNoEngineErrors(ctx, logger, $"planets/{p}/load_errors");
                TestNothingOpaqueCoversGrid(ctx, p);
                TestTopRightPanelsBelowTopBar(ctx, p);
            }
            GameManager.Instance.CurrentPlanet = 1;
            GameManager.Instance.CurrentTerritorySectionId = null;
        }

        private static async Task<bool> LoadBattle(TestContext ctx, int planet)
        {
            var gm = GameManager.Instance;
            gm.CurrentPlanet = planet;
            // First site of the planet: same map the territory screen's first Launch button uses
            var first = TerritoryManager.GetPlanet(planet)?.Regions.FirstOrDefault()?.Sites.FirstOrDefault();
            gm.CurrentTerritorySectionId = first?.Id;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            return await ctx.WaitForPhase(GamePhase.Build, 20f);
        }

        // ── Dome only recolors things that fit inside it, and never see-through ones ──

        private static void TestDomeConversionScope(TestContext ctx)
        {
            ctx.StartTest();
            var root = new Node3D();
            ctx.Tree.Root.AddChild(root);
            var center = new Vector3(20, 0, 14);
            const float radius = 8f;

            MeshInstance3D Make(Mesh mesh, Vector3 pos, Material mat = null)
            {
                var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };
                root.AddChild(m);
                m.GlobalPosition = pos;
                return m;
            }

            var prop = Make(new BoxMesh { Size = Vector3.One }, center + new Vector3(2, 0, 1));
            var hazeSheet = Make(new PlaneMesh { Size = new Vector2(120, 120) }, center + new Vector3(0, 8, 0));
            var ground = Make(new PlaneMesh { Size = new Vector2(200, 200) }, center);
            var outside = Make(new BoxMesh { Size = Vector3.One }, center + new Vector3(12, 0, 0));
            var glow = Make(new BoxMesh { Size = Vector3.One }, center,
                new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha });

            ctx.Assert(ConversionDome.FitsInsideDome(prop, center, radius), "planets/dome/converts_small_prop");
            ctx.Assert(!ConversionDome.FitsInsideDome(hazeSheet, center, radius), "planets/dome/skips_haze_sheet",
                "A 120x120 haze layer centered on the grid is not 'inside' an 8-radius dome");
            ctx.Assert(!ConversionDome.FitsInsideDome(ground, center, radius), "planets/dome/skips_ground_plane");
            ctx.Assert(!ConversionDome.FitsInsideDome(outside, center, radius), "planets/dome/skips_outside");
            ctx.Assert(ConversionDome.IsSeeThrough(glow) && !ConversionDome.IsSeeThrough(prop),
                "planets/dome/skips_transparent", "Transparent materials keep their look");

            root.QueueFree();
        }

        // ── Nothing opaque and grid-sized between the camera and the playfield ──

        private static void TestNothingOpaqueCoversGrid(TestContext ctx, string p)
        {
            ctx.StartTest();
            var scene = ctx.Tree.CurrentScene;
            var cam = scene?.GetViewport().GetCamera3D();
            var grid = FindFirst<VineGrid>(scene);
            ctx.AssertNotNull(cam, $"planets/{p}/camera_exists");
            ctx.AssertNotNull(grid, $"planets/{p}/grid_exists");
            if (cam == null || grid == null) return;

            float gridW = grid.Width * Constants.VINE_CELL_SIZE;
            float gridH = grid.Height * Constants.VINE_CELL_SIZE;
            float camY = cam.GlobalPosition.Y;

            var blockers = new List<string>();
            foreach (var mesh in All<MeshInstance3D>(scene))
            {
                if (mesh.Mesh == null || !mesh.IsVisibleInTree() || ConversionDome.IsSeeThrough(mesh)) continue;
                var aabb = mesh.GlobalTransform * mesh.GetAabb();
                bool sheet = aabb.Size.X >= gridW * 0.5f && aabb.Size.Z >= gridH * 0.5f;
                bool overGround = aabb.Position.Y > 3f && aabb.Position.Y < camY;
                if (sheet && overGround) blockers.Add($"{mesh.GetPath()} y={aabb.Position.Y:0.0} size={aabb.Size.X:0}x{aabb.Size.Z:0}");
            }

            ctx.Assert(blockers.Count == 0, $"planets/{p}/view_not_blocked",
                blockers.Count == 0 ? "Grid visible from the build camera"
                    : $"{blockers.Count} opaque grid-sized mesh(es) between camera (y={camY:0.0}) and grid: {string.Join(" | ", blockers.Take(3))}");
        }

        // ── HUD: shield-wall and next-wave panels start below the top bar ──

        private static void TestTopRightPanelsBelowTopBar(TestContext ctx, string p)
        {
            ctx.StartTest();
            var hud = FindFirst<VineHUD>(ctx.Tree.CurrentScene);
            ctx.AssertNotNull(hud, $"planets/{p}/hud_exists");
            if (hud == null) return;

            var controls = hud.GetChildren().OfType<Control>().ToList();
            var topBar = controls.FirstOrDefault(c => c is PanelContainer && c.AnchorLeft == 0 && c.AnchorRight == 1 && c.AnchorTop == 0 && c.AnchorBottom == 0);
            ctx.AssertNotNull(topBar, $"planets/{p}/hud_top_bar");
            if (topBar == null) return;
            float barBottom = topBar.GetGlobalRect().End.Y;

            var overlapping = controls
                .Where(c => c != topBar && c.Visible && c.AnchorLeft == 1 && c.AnchorRight == 1 && c.AnchorTop == 0)
                .Where(c => c.GetGlobalRect().Position.Y < barBottom)
                .Select(c => $"{c.Name} top={c.GetGlobalRect().Position.Y:0}")
                .ToList();

            ctx.Assert(overlapping.Count == 0, $"planets/{p}/hud_top_right_clear",
                overlapping.Count == 0 ? "Top-right panels sit below the top bar"
                    : $"Overlaps top bar (bottom={barBottom:0}): {string.Join(", ", overlapping)}");
        }

        private static T FindFirst<T>(Node root) where T : class
        {
            if (root == null) return null;
            if (root is T t) return t;
            foreach (var child in root.GetChildren())
            {
                var found = FindFirst<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private static IEnumerable<T> All<T>(Node root) where T : class
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            foreach (var child in root.GetChildren())
                foreach (var x in All<T>(child)) yield return x;
        }

        private static async Task WaitFrames(TestContext ctx, int frames)
        {
            for (int i = 0; i < frames; i++)
                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }
    }
}
