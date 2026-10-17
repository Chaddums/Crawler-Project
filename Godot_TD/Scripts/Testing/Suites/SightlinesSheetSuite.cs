using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Renders a canyon fight for judging the walls, sightlines and the Spire's mortar by eye
    /// (needs a display): Junk Turrets on the lip along the first route and a wave walking it
    /// (tough enough to last), seen from the play camera with BIT (whom it follows, auto-fire
    /// off) at the Spire, the middle of the route and near its entry, from the front and from
    /// behind; then the build ghost's sight overlay on the lip and one cell back; then the gunner
    /// view with the mortar's arc and shells in the air. Writes
    /// test-reports/sightlines/{wall}_{name}.png (wall: CANYON_WALL or the maps' own height).
    /// SIGHT_SITE picks the site (p1_ng_relay).
    /// </summary>
    public class SightlinesSheetSuite : ITestSuite
    {
        public string SuiteName => "sightlines-sheets";

        private string _dir, _tag;
        private int _shots;

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            ctx.StartTest();
            gm.CurrentPlanet = 1;
            gm.CurrentTerritorySectionId = System.Environment.GetEnvironmentVariable("SIGHT_SITE") ?? "p1_ng_relay";
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 40f);
            ctx.Assert(inBuild, "sightlines-sheets/battle_loads");
            if (!inBuild) return;
            var grid = ServiceLocator.Get<VineGrid>();
            var waves = ServiceLocator.Get<VineWaveManager>();
            var placer = ServiceLocator.Get<VinePlacer>();
            var bit = ServiceLocator.Get<VinePlayer>();
            var cam = ServiceLocator.Get<TDCamera>();
            waves.PauseAutoStart = true;
            gm.AddResources(5000);
            gm.SetCoreLives(999);
            grid.Harvester?.IncreaseMaxHP(1e6f);
            var route = ServiceLocator.Get<VinePathfinder>().GetCachedPath(grid.ActiveEntryRegions[0].Center) ?? new List<Vector2I>();
            int placed = 0;
            foreach (var c in TestCells.BesideRoute(grid, route, 8, route.Count / 4))
                if (placer.TestPlaceAt(VineNodeType.DamageTower, c) != null) placed++;
            _tag = System.Environment.GetEnvironmentVariable("CANYON_WALL") ?? grid.CanyonWallHeight.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            _dir = ProjectSettings.GlobalizePath("res://test-reports/sightlines");
            System.IO.Directory.CreateDirectory(_dir);
            bool autoFire = GameSettings.BitAutoFire;
            GameSettings.BitAutoFire = false;
            try
            {
                // The fight: walkers that last, on the route under the turrets
                foreach (var e in Roster.Enemies(ctx.Tree)) e.QueueFree();
                waves.StartWave();
                var tough = new HashSet<VineEnemy>();
                for (int i = 0; i < 300; i++)
                {
                    await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.PhysicsFrame);
                    foreach (var e in Roster.Enemies(ctx.Tree))
                        if (GodotObject.IsInstanceValid(e) && e.IsAlive && tough.Add(e)) e.SetChaosHP(1e6f);
                }
                var spots = new List<(string name, Vector2I cell)> { ("spire", grid.ExitPoint + new Vector2I(0, 2)) };
                if (route.Count > 8) { spots.Add(("route", route[route.Count / 2])); spots.Add(("entry", route[Mathf.Min(route.Count - 1, 4)])); }
                foreach (var (name, s) in spots)
                    foreach (var (side, yaw) in new[] { ("front", 0f), ("back", Mathf.Pi) })
                    {
                        Put(grid, bit, s);
                        if (cam != null) cam.OrbitYaw = yaw;
                        await ctx.Wait(0.5f);
                        foreach (var e in Roster.Enemies(ctx.Tree))
                            if (GodotObject.IsInstanceValid(e) && e.IsAlive && tough.Add(e)) e.SetChaosHP(1e6f);
                        await Shot(ctx, $"fight_{name}_{side}");
                    }
                if (cam != null) cam.OrbitYaw = 0f;

                // The build ghost's sight: on the lip, and one cell back
                var mid = route.Count > 0 ? route[route.Count / 2] : grid.ExitPoint;
                Put(grid, bit, mid);
                var lip = Lip(grid, mid, 2);
                var back = Lip(grid, mid, 3);
                foreach (var (name, cell) in new[] { ("ghost_lip", lip), ("ghost_back", back) })
                {
                    if (cell == null) continue;
                    placer.StartPlacing(VineNodeType.DamageTower);
                    placer.TestSetGhost(cell.Value);
                    await ctx.Wait(0.4f);
                    await Shot(ctx, name);
                    placer.CancelPlacing();
                }

                // The gunner's mortar: the arc to the marker and shells in the air
                Put(grid, bit, grid.ExitPoint + new Vector2I(0, 2));
                await ctx.Wait(0.3f);
                var st = SpireStation.Current;
                if (st != null)
                {
                    st.Dock();
                    await ctx.Wait(0.3f);
                    var gcam = st.GunnerCamera;
                    if (gcam != null && route.Count > 6)
                    {
                        var aimAt = grid.GridToWorld(route[route.Count * 2 / 3]);
                        for (int pass = 0; pass < 3; pass++)
                        {
                            var to = aimAt - gcam.GlobalPosition;
                            st.GunnerYaw = Mathf.Atan2(-to.X, -to.Z);
                            st.GunnerPitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length()) + 0.12f;
                            await ctx.Wait(0.1f);
                        }
                        st.FireHeld = true;
                        await ctx.Wait(0.9f);
                        await Shot(ctx, "mortar_gunner");
                        st.FireHeld = false;
                        await ctx.Wait(0.4f);
                        await Shot(ctx, "mortar_landing");
                    }
                    st.Undock();
                }
            }
            finally { GameSettings.BitAutoFire = autoFire; }
            ctx.Assert(_shots >= 8 && placed >= 4, "sightlines-sheets/rendered", $"{_shots} shots, {placed} turrets on the lip");
        }

        private static void Put(VineGrid grid, VinePlayer bit, Vector2I cell)
        {
            if (bit == null) return;
            var p = grid.GridToWorld(cell);
            bit.GlobalPosition = new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
        }

        /// <summary>A free wall top <paramref name="fromFloor"/> cells (8 ways) from the floor, near <paramref name="near"/>.</summary>
        private static Vector2I? Lip(VineGrid grid, Vector2I near, int fromFloor)
        {
            Vector2I? best = null;
            float bd = float.MaxValue;
            for (int dx = -6; dx <= 6; dx++)
                for (int dy = -6; dy <= 6; dy++)
                {
                    var c = near + new Vector2I(dx, dy);
                    if (!grid.InBounds(c) || grid.GetCell(c) != VineCellType.Ledge || !grid.CanPlace(c) || grid.GetNode(c) != null) continue;
                    int d = 99;
                    for (int ex = -3; ex <= 3; ex++)
                        for (int ey = -3; ey <= 3; ey++)
                            if (grid.IsWalkable(c + new Vector2I(ex, ey))) d = Mathf.Min(d, Mathf.Max(Mathf.Abs(ex), Mathf.Abs(ey)));
                    if (d != fromFloor) continue;
                    float dist = ((Vector2)(c - near)).Length();
                    if (dist < bd) { bd = dist; best = c; }
                }
            return best;
        }

        private async Task Shot(TestContext ctx, string name)
        {
            await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = ctx.Tree.Root.GetTexture().GetImage();
            string path = System.IO.Path.Combine(_dir, $"{_tag}_{name}.png");
            img.SavePng(path);
            _shots++;
            GD.Print($"[SightlinesSheet] {path}");
        }
    }
}
