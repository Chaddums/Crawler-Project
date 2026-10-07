using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Every buildable tower is built from its Data/Towers sheet the way a battle builds it:
    /// a real model that fits its cell and stands on its base, big enough to read; turrets turn
    /// to their target and fire from the barrel; recoil and rams move; the build ghost matches;
    /// walls join up with their neighbours and open again when one goes. The Bruteforge Spire's
    /// guns are kit turrets on its platform, and the Arcanist's shield dome drops when it breaks.
    /// Headless-safe.
    /// </summary>
    public class TowerLookSuite : ITestSuite
    {
        public string SuiteName => "towers";

        private const float OverhangTol = 0.05f;   // same as the fidelity suite
        private const float AimTol = 8f;           // degrees

        public async Task Run(TestContext ctx)
        {
            CheckSheets(ctx);

            var sites = FidelityTestSuite.SitesToCheck();
            var site = sites.FirstOrDefault(s => s.Planet == 2) ?? sites.FirstOrDefault();
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, "towers/battle_loads");
            if (!ok) return;
            await Frames(ctx, 10);
            var grid = ServiceLocator.Get<VineGrid>();
            var row = TowerSheetSuite.FindRow(grid, FidelityTestSuite.Towers.Length * 2 + 1);
            ctx.StartTest();
            ctx.Assert(row.Count > 0, "towers/row_found", "No flat open row to build on");
            if (row.Count == 0) return;

            await CheckTowers(ctx, grid, row);
            await CheckFiring(ctx, grid, row);
            await CheckWalls(ctx, grid, row);
            await CheckSpires(ctx, grid, row);

            Engine.TimeScale = 1.0;
            GameManager.Instance.CurrentPlanet = 1;
            GameManager.Instance.CurrentTerritorySectionId = null;
        }

        // ── Data ──

        private static void CheckSheets(TestContext ctx)
        {
            foreach (var type in FidelityTestSuite.Towers)
            {
                ctx.StartTest();
                var data = VineNodeRegistry.Get(type);
                var sheet = TowerSheet.Load(data?.Id);
                ctx.Assert(sheet != null && sheet.Parts.Count > 0, $"towers/{type}/sheet_loads",
                    $"Data/Towers/{data?.Id}.json should load with parts");
                if (sheet == null) continue;
                var missing = sheet.Parts.Where(p => p.Model.Length > 0 && !ResourceLoader.Exists(p.Model)).Select(p => p.Model).ToList();
                ctx.Assert(missing.Count == 0, $"towers/{type}/models_exist", $"Missing: {string.Join(", ", missing)}");
            }
            foreach (var id in new[] { "spire_forge", "spire_autocannon" })
            {
                ctx.StartTest();
                ctx.Assert(TowerSheet.Load(id) != null, $"towers/{id}/sheet_loads");
            }
        }

        // ── Each tower ──

        private static async Task CheckTowers(TestContext ctx, VineGrid grid, List<Vector2I> row)
        {
            float half = Constants.VINE_CELL_SIZE / 2f;
            var types = FidelityTestSuite.Towers;
            var placed = new List<(VineNodeType type, VineNode node)>();
            for (int i = 0; i < types.Length; i++)
            {
                var node = new VineNode();
                node.Initialize(VineNodeRegistry.Get(types[i]));
                if (grid.PlaceNode(node, row[1 + i * 2])) placed.Add((types[i], node));
                else node.QueueFree();
            }
            await Frames(ctx, 5);

            foreach (var (type, node) in placed)
            {
                string p = $"towers/{type}";
                ctx.StartTest();
                var look = node.Look;
                ctx.Assert(look != null, $"{p}/has_look", "Built without its sheet");
                if (look == null || !FidelityTestSuite.MeshBounds(node.VisualRoot, out var box, true)) continue;

                var c = node.GlobalPosition;
                float baseY = c.Y - Constants.NODE_ORIGIN_HEIGHT;
                float overhang = Mathf.Max(Mathf.Max(c.X - half - box.Position.X, box.End.X - (c.X + half)),
                                           Mathf.Max(c.Z - half - box.Position.Z, box.End.Z - (c.Z + half)));
                ctx.Assert(overhang <= OverhangTol, $"{p}/within_cell", $"Reaches {overhang:F2} past its cell edge");
                ctx.Assert(Mathf.Abs(box.Position.Y - baseY) <= 0.03f, $"{p}/on_its_base",
                    $"Model bottom {box.Position.Y - baseY:F2} from the tower's base");
                float footprint = Mathf.Max(box.Size.X, box.Size.Z);
                ctx.Assert(box.Size.Y is >= 0.7f and <= 2.4f && footprint >= 1.0f, $"{p}/reads_at_play_distance",
                    $"{footprint:F2} across and {box.Size.Y:F2} tall (want at least 1.0 across, 0.7 to 2.4 tall)");
                ctx.Assert(box.Grow(0.25f).HasPoint(look.MuzzleGlobal), $"{p}/muzzle_on_model",
                    $"Muzzle {look.MuzzleGlobal} is off the model ({box})");

                // The build ghost is the same model
                var ghost = VineNode.TryLoadModelForType(type);
                ctx.Assert(ghost != null && ghost.GetChild(0) is TowerLook, $"{p}/ghost_matches", "The build ghost isn't the tower's look");
                ghost?.Free();
            }

            // Turrets turn to a target and their barrel ends up pointing at it
            var marker = new Node3D { Name = "AimTarget" };
            grid.AddChild(marker);
            foreach (var (type, node) in placed.Where(t => t.node.Look?.CanAim == true))
            {
                ctx.StartTest();
                float want = 150f;
                marker.GlobalPosition = node.GlobalPosition + new Vector3(Mathf.Sin(Mathf.DegToRad(want)), 0, Mathf.Cos(Mathf.DegToRad(want))) * 5f;
                // Real time, not frames: headless frames run faster than 60 a second
                node.Look.Track(marker);
                await ctx.Wait(1.5f);
                float off = Mathf.Abs(Mathf.Wrap(node.Look.BarrelYawDegrees - want, -180f, 180f));
                ctx.Assert(off <= AimTol, $"towers/{type}/aims_at_target", $"Barrel {off:F0} degrees off the target after 1.5 s");
                var m = node.Look.MuzzleGlobal - node.GlobalPosition;
                float muzzleYaw = Mathf.RadToDeg(Mathf.Atan2(m.X, m.Z));
                ctx.Assert(new Vector2(m.X, m.Z).Length() < 0.15f || Mathf.Abs(Mathf.Wrap(muzzleYaw - want, -180f, 180f)) <= 35f,
                    $"towers/{type}/muzzle_faces_target", $"Muzzle is {muzzleYaw:F0} degrees round, target at {want:F0}");

                // Firing kicks the barrel back, or punches the ram out
                FidelityTestSuite.MeshBounds(node.Look.AimNode, out var before, true);
                node.Look.Fire();
                await ctx.Wait(0.05f);
                FidelityTestSuite.MeshBounds(node.Look.AimNode, out var after, true);
                float moved = (after.GetCenter() - before.GetCenter()).Length() + Mathf.Abs(after.Size.Length() - before.Size.Length());
                bool moves = node.Look.Sheet.Recoil > 0f || node.Look.Sheet.Parts.Any(pt => pt.Punch > 0f);
                if (moves) ctx.Assert(moved > 0.01f, $"towers/{type}/fires_visibly", "Nothing moved when it fired");
            }
            marker.QueueFree();

            foreach (var (_, node) in placed) grid.RemoveNode(node.GridPosition);
            await Frames(ctx, 2);
        }

        /// <summary>A Junk Turret with an enemy in range shoots from its barrel tip at it.</summary>
        private static async Task CheckFiring(TestContext ctx, VineGrid grid, List<Vector2I> row)
        {
            ctx.StartTest();
            var node = new VineNode();
            node.Initialize(VineNodeRegistry.Get(VineNodeType.DamageTower));
            if (!grid.PlaceNode(node, row[2])) { ctx.Assert(false, "towers/firing/placed"); node.QueueFree(); return; }
            var enemy = new VineEnemy();
            grid.GetParent().AddChild(enemy);
            enemy.Initialize("Tower test target", VineEnemyFaction.Scavenger, 1_000_000f, 0.01f, 0,
                new Color(0.8f, 0.4f, 0.3f), row[6]);
            VineProjectile shot = null;
            for (int f = 0; f < 80 && shot == null; f++)
            {
                await ctx.Wait(0.05f);
                shot = All<VineProjectile>(ctx.Tree.Root).FirstOrDefault(pr => pr.Origin.DistanceTo(node.GlobalPosition) < 2.5f);
            }
            ctx.Assert(shot != null, "towers/firing/shoots", "No shot within 4 s of an enemy in range");
            if (shot != null)
            {
                float d = shot.Origin.DistanceTo(node.Look.MuzzleGlobal);
                ctx.Assert(d < 0.35f, "towers/firing/from_the_barrel", $"Shot left {d:F2} from the barrel tip");
                var toEnemy = enemy.GlobalPosition - node.GlobalPosition;
                float want = Mathf.RadToDeg(Mathf.Atan2(toEnemy.X, toEnemy.Z));
                await ctx.Wait(1.2f);
                toEnemy = enemy.GlobalPosition - node.GlobalPosition;
                want = Mathf.RadToDeg(Mathf.Atan2(toEnemy.X, toEnemy.Z));
                float off = Mathf.Abs(Mathf.Wrap(node.Look.BarrelYawDegrees - want, -180f, 180f));
                ctx.Assert(off <= 15f, "towers/firing/faces_what_it_shoots", $"Barrel {off:F0} degrees off its target");
            }
            enemy.QueueFree();
            grid.RemoveNode(node.GridPosition);
            await Frames(ctx, 2);
        }

        // ── Walls ──

        private static async Task CheckWalls(TestContext ctx, VineGrid grid, List<Vector2I> row)
        {
            // A line of three along x with a corner turning +z at its end, and one on its own
            var line = new[] { row[2], row[3], row[4] };
            var corner = row[4] + new Vector2I(0, 1);
            var lone = row[7];
            var walls = new Dictionary<Vector2I, VineNode>();
            foreach (var c in line.Append(corner).Append(lone))
            {
                var w = new VineNode();
                w.Initialize(VineNodeRegistry.Get(VineNodeType.BarrierWall));
                if (grid.PlaceNode(w, c)) walls[c] = w; else w.QueueFree();
            }
            ctx.StartTest();
            ctx.Assert(walls.Count == 5, "towers/walls/placed", $"{walls.Count} of 5 walls placed");
            if (walls.Count < 5) { foreach (var w in walls.Values) grid.RemoveNode(w.GridPosition); return; }
            await ctx.Wait(0.6f); // wall faces refresh a few times a second

            void Expect(Vector2I cell, string name, params Vector2I[] joined)
            {
                ctx.StartTest();
                var look = walls[cell].Look;
                var wrong = new List<string>();
                foreach (var (part, dir, link) in look.WallParts)
                {
                    bool join = joined.Contains(dir);
                    bool shouldShow = link ? join : !join;
                    if (part.Visible != shouldShow) wrong.Add($"{(link ? "link" : "face")} {dir} {(part.Visible ? "shown" : "hidden")}");
                }
                ctx.Assert(look.WallParts.Any() && wrong.Count == 0, $"towers/walls/{name}", string.Join("; ", wrong));
            }
            var east = new Vector2I(1, 0); var west = new Vector2I(-1, 0); var south = new Vector2I(0, 1);
            Expect(row[3], "middle_of_line_joins_both_ends", east, west);
            Expect(row[4], "corner_joins_both_ways", west, south);
            Expect(corner, "end_joins_one_way", new Vector2I(0, -1));
            Expect(lone, "lone_wall_closed_on_all_sides");

            // Take the middle one away: its neighbours close up on that side
            grid.RemoveNode(row[3]);
            walls.Remove(row[3]);
            await ctx.Wait(0.6f);
            Expect(row[2], "opens_again_when_neighbour_goes");
            Expect(row[4], "corner_after_removal", south);

            foreach (var w in walls.Values) grid.RemoveNode(w.GridPosition);
            await Frames(ctx, 2);
        }

        // ── Spires ──

        private static async Task CheckSpires(TestContext ctx, VineGrid grid, List<Vector2I> row)
        {
            var gm = GameManager.Instance;
            string saved = gm.SelectedRole;
            var at = grid.GridToWorld(row[row.Count / 2]);

            gm.SelectedRole = "Bruteforge";
            var forge = new VineHarvester();
            grid.AddChild(forge);
            forge.SeatOn(grid, at);
            await Frames(ctx, 3);
            ctx.StartTest();
            var data = SpireData.Get("Bruteforge");
            var guns = All<TowerLook>(forge).Where(l => l.Name.ToString().StartsWith("Autocannon")).ToList();
            ctx.Assert(guns.Count == data.AutocannonCount, "towers/spire/Bruteforge/guns_are_turrets",
                $"{guns.Count} turret looks for {data.AutocannonCount} autocannons");
            var plat = All<TowerLook>(forge).FirstOrDefault(l => l.Name == "SpireBase");
            ctx.Assert(plat != null, "towers/spire/Bruteforge/has_platform");
            if (plat != null && plat.Sheet.Pedestal != null)
            {
                float reach = plat.Sheet.Pedestal.Radius + 0.1f;
                var off = guns.Where(g =>
                {
                    var d = g.GlobalPosition - forge.GlobalPosition;
                    return new Vector2(d.X, d.Z).Length() > reach || Mathf.Abs(g.GlobalPosition.Y - (plat.GlobalPosition.Y + plat.Sheet.Pedestal.Height)) > 0.05f;
                }).Select(g => g.Name.ToString()).ToList();
                ctx.Assert(off.Count == 0, "towers/spire/Bruteforge/guns_on_platform", $"Off the platform: {string.Join(", ", off)}");
            }
            forge.QueueFree();
            await Frames(ctx, 2);

            gm.SelectedRole = "Arcanist";
            var arc = new VineHarvester();
            grid.AddChild(arc);
            arc.SeatOn(grid, at);
            await Frames(ctx, 3);
            ctx.StartTest();
            var dome = arc.GetNodeOrNull<MeshInstance3D>("ShieldDome");
            ctx.Assert(dome != null && dome.Visible, "towers/spire/Arcanist/shield_dome_up", "No shield dome while the shield is up");
            if (dome != null && arc.VisualRoot != null && FidelityTestSuite.MeshBounds(arc.VisualRoot, out var sb))
                ctx.Assert(dome.Scale.X > sb.Size.X * 0.5f && dome.Scale.Y >= sb.Size.Y * 0.95f, "towers/spire/Arcanist/dome_covers_spire",
                    $"Dome {dome.Scale} over a Spire {sb.Size}");
            arc.TakeDamage(1f);
            await ctx.Wait(1.0f);
            ctx.Assert(dome != null && !dome.Visible, "towers/spire/Arcanist/dome_drops_when_broken", "Dome still up a second after the shield broke");
            arc.QueueFree();
            await Frames(ctx, 2);
            gm.SelectedRole = saved;
        }

        // ── Helpers ──

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
