using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A player's session on Relay Station Alpha (P1 gateway) as Bruteforge, driven through the real
    /// viewport with mouse events: hover and pick every build-bar tower, sweep its ghost over the
    /// field, then check what a player reported (tooltips covering the bar, turret facing, shots in
    /// terrain, AXIS chaos). Screenshots go to test-reports/playtest/. Needs a display.
    /// </summary>
    public class PlaytestSuite : ITestSuite
    {
        public string SuiteName => "playtest";

        private string _out;
        private VineGrid _grid;

        public async Task Run(TestContext ctx)
        {
            if (DisplayServer.GetName() == "headless")
            {
                ctx.StartTest();
                ctx.Assert(false, "playtest/needs_display", "Run with a display (Xvfb)");
                return;
            }
            _out = ProjectSettings.GlobalizePath("res://test-reports/playtest");
            System.IO.Directory.CreateDirectory(_out);

            ctx.StartTest();
            bool ok = await LoadUserBattle(ctx);
            ctx.Assert(ok, "playtest/battle_loads");
            if (!ok) return;
            _grid = ServiceLocator.Get<VineGrid>();
            ServiceLocator.TryGet<VineWaveManager>(out var waves);
            if (waves != null) waves.PauseAutoStart = true;
            _grid.Harvester?.IncreaseMaxHP(100000f);
            GameManager.Instance.AddResources(5000);
            PlaceUserTowers();
            await ctx.Wait(1.5f);
            await Shot(ctx, "00_build");

            var parts = (System.Environment.GetEnvironmentVariable("PLAYTEST_PARTS") ?? "flak,hover,aim")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // The reported crash first: pick the Flak Battery and hunt for a cell with it
            if (parts.Contains("flak")) await PickTower(ctx, VineNodeType.FlakBattery, sweep: true);
            if (parts.Contains("pick"))
                foreach (var t in GameManager.Instance.AvailableNodes) await PickTower(ctx, t, sweep: false);
            if (parts.Contains("hover")) await HoverBar(ctx);
            if (parts.Contains("aim")) await AimShots(ctx);
            if (parts.Contains("enemies")) await EnemyShots(ctx);
            if (parts.Contains("card")) await WaveCards(ctx);
            if (parts.Contains("panel")) await PanelShots(ctx);

            if (waves != null) waves.PauseAutoStart = false;
        }

        // ── Setup: the user's run ──

        private static async Task<bool> LoadUserBattle(TestContext ctx)
        {
            var gm = GameManager.Instance;
            Engine.TimeScale = 1.0;
            gm.CurrentPlanet = 1;
            gm.CurrentTerritorySectionId = "p1_ng_relay";
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Bruteforge";
            gm.AvailableNodes = SpireData.Get("Bruteforge")?.Nodes ?? VineDraftScreen.GetRoleNodes(2);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            if (!await ctx.WaitForPhase(GamePhase.Build, 30f)) return false;
            // Past the intro (flyover, Spire slam, BIT emerging) so the build bar is up
            return await ctx.WaitUntil(() => BuildButtons(ctx.Tree.Root).Count >= 8 && BuildButtons(ctx.Tree.Root).All(b => b.IsVisibleInTree()), 40f);
        }

        /// <summary>The towers from the reported run, at the same cells.</summary>
        private void PlaceUserTowers()
        {
            var plan = new List<(VineNodeType, Vector2I)>
            {
                (VineNodeType.DamageTower, new(14, 14)), (VineNodeType.DamageTower, new(14, 11)),
                (VineNodeType.BuffEmitter, new(15, 13)),
                (VineNodeType.PushPull, new(22, 17)), (VineNodeType.TeslaCoil, new(19, 18)),
                (VineNodeType.ScatterCannon, new(17, 18)), (VineNodeType.ScatterCannon, new(26, 15)),
                (VineNodeType.DamageTower, new(26, 10)),
            };
            for (int y = 10; y <= 15; y++) plan.Add((VineNodeType.BarrierWall, new(12, y)));
            for (int y = 11; y <= 15; y++) plan.Add((VineNodeType.BarrierWall, new(28, y)));
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            foreach (var (type, cell) in plan)
            {
                if (!_grid.CanPlace(cell) || (pf != null && pf.WouldBlockAllPaths(cell))) continue;
                var n = new VineNode();
                n.Initialize(VineNodeRegistry.Get(type));
                if (!_grid.PlaceNode(n, cell)) n.QueueFree();
            }
        }

        // ── Build bar: hover, pick, sweep the ghost ──

        private async Task PickTower(TestContext ctx, VineNodeType type, bool sweep)
        {
            var root = ctx.Tree.Root;
            var placer = ServiceLocator.Get<VinePlacer>();
            var data = VineNodeRegistry.Get(type);
            var btn = BuildButtons(root).FirstOrDefault(b => b.Text.StartsWith(data.Name));
            ctx.StartTest();
            ctx.AssertNotNull(btn, $"playtest/pick/{type}/button");
            if (btn == null) return;
            var center = btn.GetGlobalRect().GetCenter();
            await Move(ctx, center);
            await ctx.Wait(0.3f);
            GD.Print($"[Playtest] picking {type}");
            await Click(ctx, MouseButton.Left, center);
            ctx.Assert(placer.IsPlacing && placer.SelectedType == type, $"playtest/pick/{type}/placing",
                $"Clicking the {data.Name} button should start placing it (placing={placer.IsPlacing}, type={placer.SelectedType})");
            var vis = root.GetVisibleRect().Size;
            if (sweep)
            {
                // The ghost over the field, row by row, as a player hunting for a cell
                ulong t0 = Time.GetTicksMsec();
                int frames = 0;
                for (float fy = 0.25f; fy <= 0.75f; fy += 0.25f)
                    for (float fx = 0.15f; fx <= 0.85f; fx += 0.14f)
                    {
                        await Move(ctx, vis * new Vector2(fx, fy), frames: 1);
                        frames++;
                    }
                GD.Print($"[Playtest] swept {type}: {frames} frames in {Time.GetTicksMsec() - t0} ms");
            }
            await Move(ctx, vis * new Vector2(0.5f, 0.5f));
            await ctx.Wait(0.3f);
            await Shot(ctx, $"11_ghost_{type}");
            ctx.Assert(true, $"playtest/pick/{type}/survives");
            await Click(ctx, MouseButton.Right, vis * new Vector2(0.5f, 0.5f));
            await ctx.Wait(0.2f);
        }

        /// <summary>Hover each build-bar button: its card shows, and nothing covers the other buttons.</summary>
        private async Task HoverBar(TestContext ctx)
        {
            var root = ctx.Tree.Root;
            foreach (var type in GameManager.Instance.AvailableNodes)
            {
                var data = VineNodeRegistry.Get(type);
                var btn = BuildButtons(root).FirstOrDefault(b => b.Text.StartsWith(data.Name));
                if (btn == null) continue;
                await Move(ctx, btn.GetGlobalRect().GetCenter());
                await ctx.Wait(1.0f); // longer than Godot's tooltip delay
                await Shot(ctx, $"10_hover_{type}");
                CheckBarClear(ctx, type, btn);
                var card = All<VineHUD>(root).FirstOrDefault()?.InfoCard;
                ctx.Assert(card != null && card.Visible && All<Label>(card).Any(l => l.Text.StartsWith(data.Name)),
                    $"playtest/hover/{type}/card_shows", "The hover card should name the tower");
            }
            await Move(ctx, root.GetVisibleRect().Size * 0.5f);
        }

        /// <summary>
        /// Each turning tower tracking a marker due east, from straight above: the barrel should
        /// point at the marker. Saved as 20_aim_*.png; the barrel heading is checked against the
        /// tower's muzzle, and the picture is the independent check.
        /// </summary>
        private async Task AimShots(TestContext ctx)
        {
            var scene = ctx.Tree.CurrentScene;
            var types = new[] { VineNodeType.DamageTower, VineNodeType.SlowField, VineNodeType.ScatterCannon, VineNodeType.FlakBattery, VineNodeType.PushPull };
            var row = TowerSheetSuite.FindRow(_grid, types.Length * 3 + 2);
            ctx.StartTest();
            ctx.Assert(row.Count > 0, "playtest/aim/row_found");
            if (row.Count == 0) return;
            foreach (var l in All<CanvasLayer>(ctx.Tree.Root)) l.Visible = false;
            var cam = new Camera3D { Fov = 40f };
            scene.AddChild(cam);
            var oldCam = scene.GetViewport().GetCamera3D();
            cam.Current = true;
            var marker = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.1f, 0.1f), EmissionEnabled = true, Emission = new Color(1f, 0.1f, 0.1f), EmissionEnergyMultiplier = 2f },
            };
            scene.AddChild(marker);
            for (int i = 0; i < types.Length; i++)
            {
                var cell = row[1 + i * 3];
                var node = new VineNode();
                node.Initialize(VineNodeRegistry.Get(types[i]));
                if (!_grid.PlaceNode(node, cell)) { node.QueueFree(); continue; }
                await ctx.Wait(0.2f);
                var look = node.Look;
                if (look == null || !look.CanAim) continue;
                foreach (var (name, dir) in new[] { ("east", new Vector3(1, 0, 0)), ("north", new Vector3(0, 0, -1)) })
                {
                    var tp = node.GlobalPosition + dir * 3.2f;
                    marker.GlobalPosition = new Vector3(tp.X, _grid.GetWorldHeight(tp.X, tp.Z) + 0.6f, tp.Z);
                    for (int k = 0; k < 12; k++) { look.Track(marker); await ctx.Wait(0.25f); }
                    var c = node.GlobalPosition;
                    cam.GlobalPosition = c + new Vector3(0, 7.5f, 0.001f);
                    cam.LookAt(c + dir * 1.2f, Vector3.Forward);
                    await Shot(ctx, $"20_aim_{types[i]}_{name}");
                    if (name == "east")
                    {
                        // From the side and a little behind: which end is the business end
                        cam.GlobalPosition = c + new Vector3(-1.6f, 2.0f, 3.4f);
                        cam.LookAt(c + new Vector3(0.9f, 0.5f, 0f), Vector3.Up);
                        await Shot(ctx, $"21_side_{types[i]}");
                    }
                    // The barrel heading against the heading to the marker (the tracking maths);
                    // whether that heading is the model's real front is what the pictures show
                    var to = marker.GlobalPosition - look.AimNode.GlobalPosition;
                    float err = Mathf.Abs(Mathf.Wrap(look.BarrelYawDegrees - Mathf.RadToDeg(Mathf.Atan2(to.X, to.Z)), -180f, 180f));
                    GD.Print($"[Playtest] aim {types[i]} {name}: barrel heading off by {err:F0} deg");
                    ctx.StartTest();
                    ctx.Assert(err < 8f, $"playtest/aim/{types[i]}/{name}", $"barrel heading {err:F0} deg off the target");
                }
                node.QueueFree();
                _grid.RemoveNode(cell);
            }
            // The Spire's own guns (Bruteforge): each tracks the marker on its side
            var guns = All<TowerLook>(_grid.Harvester).Where(l => l.Name.ToString().StartsWith("Autocannon")).ToList();
            if (guns.Count > 0)
            {
                var hc = _grid.Harvester.GlobalPosition;
                var dir = new Vector3(1, 0, 0);
                var tp = hc + dir * 5f;
                marker.GlobalPosition = new Vector3(tp.X, _grid.GetWorldHeight(tp.X, tp.Z) + 0.6f, tp.Z);
                for (int k = 0; k < 12; k++) { foreach (var g in guns) g.Track(marker); await ctx.Wait(0.25f); }
                cam.GlobalPosition = hc + new Vector3(0.5f, 3.2f, 4.5f);
                cam.LookAt(hc + new Vector3(1.2f, 0.6f, 0f), Vector3.Up);
                await Shot(ctx, "22_spire_guns");
                foreach (var g in guns)
                {
                    var to = marker.GlobalPosition - g.AimNode.GlobalPosition;
                    float err = Mathf.Abs(Mathf.Wrap(g.BarrelYawDegrees - Mathf.RadToDeg(Mathf.Atan2(to.X, to.Z)), -180f, 180f));
                    ctx.StartTest();
                    ctx.Assert(err < 8f, $"playtest/aim/spire_{g.Name}", $"barrel heading {err:F0} deg off the target");
                }
            }
            marker.QueueFree();
            if (oldCam != null) oldCam.Current = true;
            cam.QueueFree();
            foreach (var l in All<CanvasLayer>(ctx.Tree.Root)) l.Visible = true;
        }

        /// <summary>One of each trait beside a plain enemy, close up from the play angle (30_enemies.png).</summary>
        private async Task EnemyShots(TestContext ctx)
        {
            var scene = ctx.Tree.CurrentScene;
            var row = TowerSheetSuite.FindRow(_grid, 11);
            if (row.Count == 0) return;
            var spawned = new List<VineEnemy>();
            var lineup = new (string name, VineEnemyFaction f, EnemyTraits t)[]
            {
                ("Scrap Rat", VineEnemyFaction.Scavenger, EnemyTraits.None),
                ("Plated Rat", VineEnemyFaction.Scavenger, EnemyTraits.Armoured),
                ("Aegis Rat", VineEnemyFaction.Scavenger, EnemyTraits.Shielded),
                ("Plated Hulk", VineEnemyFaction.Brute, EnemyTraits.Armoured),
                ("Sky Buzzer", VineEnemyFaction.Swarm, EnemyTraits.Flying),
            };
            for (int i = 0; i < lineup.Length; i++)
            {
                var c = row[3 + i];
                var e = new VineEnemy();
                scene.AddChild(e);
                e.Initialize(lineup[i].name, lineup[i].f, 1e6f, 0f, 0, new Color(0.8f, 0.3f, 0.3f), c);
                var w = _grid.GridToWorld(c);
                e.GlobalPosition = new Vector3(w.X, _grid.GetWorldHeight(w.X, w.Z), w.Z);
                if (lineup[i].t != EnemyTraits.None) e.SetTraits(lineup[i].t);
                e.SetProcess(false);
                spawned.Add(e);
            }
            // Shield walls stand along the field's edges; keep them out of the picture
            var walls = All<ShieldWall>(ctx.Tree.Root).Where(w => w.Visible).ToList();
            foreach (var w in walls) w.Visible = false;
            await ctx.Wait(0.5f);
            foreach (var l in All<CanvasLayer>(ctx.Tree.Root)) l.Visible = false;
            var cam = new Camera3D { Fov = 40f };
            scene.AddChild(cam);
            var old = scene.GetViewport().GetCamera3D();
            cam.Current = true;
            var mid = (_grid.GridToWorld(row[3]) + _grid.GridToWorld(row[7])) * 0.5f;
            mid.Y = _grid.GetWorldHeight(mid.X, mid.Z);
            // About the play camera's pitch, close enough that a rat is ~100 px tall
            cam.GlobalPosition = mid + new Vector3(0, 6.2f, 6.4f);
            cam.LookAt(mid + Vector3.Up * 1.1f, Vector3.Up);
            // Front three-quarter, then from behind (the back plate)
            foreach (var (tag, yawDeg) in new[] { ("30_enemies", 35f), ("30_enemies_back", 150f) })
            {
                foreach (var e in spawned) e.TestFaceYaw(Mathf.DegToRad(yawDeg));
                await ctx.Wait(0.2f);
                await Shot(ctx, tag);
            }
            var plated = spawned.FirstOrDefault(e => e.IsArmoured && e.Faction == VineEnemyFaction.Scavenger);
            if (plated?.ArmourRig != null)
                GD.Print($"[Playtest] Plated Rat body radius {plated.BodyRadius:0.00}, rig at {plated.ArmourRig.Position.Y:0.00}, " +
                    $"parts {plated.ArmourRig.GetChildCount()}, body height {plated.ArmourHeight:0.00}");
            if (old != null) old.Current = true;
            cam.QueueFree();
            foreach (var w in walls) w.Visible = true;
            foreach (var l in All<CanvasLayer>(ctx.Tree.Root)) l.Visible = true;
            foreach (var e in spawned) e.QueueFree();
        }

        /// <summary>The wave card before the first armoured, flying and shielded waves (31_card_W#.png).</summary>
        private async Task WaveCards(TestContext ctx)
        {
            ServiceLocator.TryGet<VineWaveManager>(out var waves);
            if (waves == null) return;
            foreach (int next in new[] { 4, 6, 7, 9 })
            {
                waves.TestSetWave(next - 1);
                await ctx.Wait(0.4f);
                await Shot(ctx, $"31_card_W{next}");
            }
            waves.TestSetWave(0);
            await ctx.Wait(0.2f);
        }

        /// <summary>The tower panel at each stage: level 1, the branch choice, a branch taken (32_panel_*.png).</summary>
        private async Task PanelShots(TestContext ctx)
        {
            var insp = TowerInspector.Current;
            if (insp == null) return;
            var tower = All<VineNode>(ctx.Tree.CurrentScene).FirstOrDefault(n => n.Data?.Type == VineNodeType.DamageTower);
            if (tower == null) return;
            insp.Open(tower);
            await ctx.Wait(0.4f);
            await Shot(ctx, "32_panel_level1");
            CheckPanelOnScreen(ctx, "level1");
            tower.ForceUpgrade(3);
            insp.Open(tower);
            await ctx.Wait(0.4f);
            await Shot(ctx, "32_panel_branches");
            CheckPanelOnScreen(ctx, "branches");
            tower.TryBranch("minigun");
            insp.Open(tower);
            await ctx.Wait(0.4f);
            await Shot(ctx, "32_panel_branch_taken");
            CheckPanelOnScreen(ctx, "taken");
            insp.Close();
        }

        private void CheckPanelOnScreen(TestContext ctx, string stage)
        {
            var box = All<PanelContainer>(ctx.Tree.Root).FirstOrDefault(p => p.Name == "TowerPanelBox");
            var vis = ctx.Tree.Root.GetVisibleRect();
            ctx.StartTest();
            var r = box?.GetGlobalRect() ?? new Rect2();
            bool inside = box != null && r.Position.X >= vis.Position.X && r.Position.Y >= vis.Position.Y && r.End.X <= vis.End.X + 1 && r.End.Y <= vis.End.Y + 1;
            var bar = BuildButtons(ctx.Tree.Root).Select(b => b.GetGlobalRect()).ToList();
            bool clearOfBar = bar.All(b => b.Intersection(r).Area < 4);
            ctx.Assert(inside && clearOfBar, $"playtest/panel/{stage}/on_screen_clear_of_bar", $"panel {r}");
        }

        /// <summary>
        /// Nothing that appears on hover may cover another build-bar button: a player moving along
        /// the bar has to see what they are about to pick.
        /// </summary>
        private void CheckBarClear(TestContext ctx, VineNodeType type, Button hovered)
        {
            var bar = BuildButtons(ctx.Tree.Root).Where(b => b != hovered).Select(b => b.GetGlobalRect()).ToList();
            var covering = new List<string>();
            foreach (var layer in All<CanvasLayer>(ctx.Tree.Root))
            {
                if (!layer.Visible) continue;
                foreach (var c in All<Control>(layer))
                {
                    if (!c.IsVisibleInTree() || c is Button || c.Size.X < 4 || c.Size.Y < 4) continue;
                    if (c is not (Label or PanelContainer or Panel or RichTextLabel)) continue;
                    if (IsInsideBar(c) || c.IsAncestorOf(hovered)) continue;
                    var r = c.GetGlobalRect();
                    if (bar.Any(b => b.Intersection(r).Area > 16)) covering.Add($"{c.GetType().Name} {c.Name} \"{Text(c)}\"");
                }
            }
            // Godot's own tooltip pops up in a separate window over whatever is under the cursor
            var popups = All<Window>(ctx.Tree.Root).Where(w => w != ctx.Tree.Root && w.Visible && w is PopupPanel or Popup).ToList();
            ctx.Assert(covering.Count == 0 && popups.Count == 0, $"playtest/hover/{type}/bar_clear",
                $"Covering the build bar: {string.Join("; ", covering.Take(3))} {(popups.Count > 0 ? $"+ {popups.Count} popup tooltip(s)" : "")}");
        }

        private static bool IsInsideBar(Control c)
        {
            for (Node p = c; p != null; p = p.GetParent())
                if (p is HBoxContainer h && h.GetChildren().OfType<Button>().Count() >= 6) return true;
            return false;
        }

        private static string Text(Control c) => c switch
        {
            Label l => l.Text.Length > 30 ? l.Text[..30] + "..." : l.Text,
            RichTextLabel r => r.GetParsedText(),
            _ => "",
        };

        internal static List<Button> BuildButtons(Node root)
        {
            var names = new HashSet<string>(VineDraftScreen.GetRoleNodes(0).Select(t => VineNodeRegistry.Get(t)?.Name).Where(n => n != null));
            return All<Button>(root).Where(b => names.Any(n => b.Text.StartsWith(n + "\n"))).ToList();
        }

        // ── Input and capture ──

        private static async Task Move(TestContext ctx, Vector2 pos, int frames = 3)
        {
            ctx.Tree.Root.WarpMouse(pos);
            ctx.Tree.Root.PushInput(new InputEventMouseMotion { Position = pos, GlobalPosition = pos }, true);
            for (int i = 0; i < frames; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }

        private static async Task Click(TestContext ctx, MouseButton button, Vector2 pos)
        {
            var vp = ctx.Tree.Root;
            await Move(ctx, pos, 1);
            vp.PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = pos, GlobalPosition = pos }, true);
            for (int i = 0; i < 2; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
            vp.PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = pos, GlobalPosition = pos }, true);
            for (int i = 0; i < 3; i++) await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }

        private async Task Shot(TestContext ctx, string name)
        {
            for (int i = 0; i < 3; i++)
                await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            ctx.Tree.Root.GetTexture().GetImage().SavePng($"{_out}/{name}.png");
        }

        internal static IEnumerable<T> All<T>(Node root) where T : class
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            foreach (var child in root.GetChildren())
                foreach (var x in All<T>(child)) yield return x;
        }
    }
}
