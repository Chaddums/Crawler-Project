using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The battle HUD as a player sees it, in each state a player meets: build phase, placing a
    /// tower, a wave, a perk pick, the material picker, the pause menu and help. For every state:
    /// every visible element sits on the screen, no text spills out of its box, top-level panels
    /// don't overlap, and text is big enough to read. "hud" checks the layout at the design size
    /// (headless-safe). "hud-sheets" also resizes the window through common sizes and saves a
    /// screenshot of every state at every size to test-reports/hud/ (needs a display).
    /// </summary>
    public class HudTestSuite : ITestSuite
    {
        private readonly bool _shots;
        public HudTestSuite(bool shots = false) { _shots = shots; }
        public string SuiteName => _shots ? "hud-sheets" : "hud";

        /// <summary>Smallest text a player can read at the design size (1920x1080), in pixels.</summary>
        public const int MinFontPx = 14;

        private sealed record State(string Name, Func<Task> Enter, Func<Task> Exit);

        private readonly List<string> _report = new();

        public async Task Run(TestContext ctx)
        {
            if (_shots && DisplayServer.GetName() == "headless")
            {
                ctx.StartTest();
                ctx.Assert(false, "hud-sheets/needs_display", "Run with a display (Xvfb)");
                return;
            }
            var site = FidelityTestSuite.SitesToCheck().FirstOrDefault(s => s.Planet == 1);
            ctx.StartTest();
            bool ok = site != null && await FidelityTestSuite.LoadBattle(ctx, site);
            ctx.Assert(ok, $"{SuiteName}/battle_loads");
            if (!ok) return;
            ServiceLocator.TryGet<VineWaveManager>(out var waves);
            if (waves != null) waves.PauseAutoStart = true;
            var grid = ServiceLocator.Get<VineGrid>();
            ServiceLocator.TryGet<VinePlacer>(out var placer);
            grid.Harvester?.IncreaseMaxHP(100000f);
            PlaceSomeTowers(grid);
            await ctx.Wait(1f);

            var states = States(ctx, grid, placer, waves);
            // Screenshots at the window size the run was started with (--resolution WxH): resizing
            // a window from inside the game is not reliable on every display server
            var sizes = _shots ? new[] { DisplayServer.WindowGetSize() } : new[] { new Vector2I(1920, 1080) };
            string outDir = ProjectSettings.GlobalizePath("res://test-reports/hud");
            if (_shots) System.IO.Directory.CreateDirectory(outDir);

            foreach (var st in states)
            {
                await st.Enter();
                foreach (var size in sizes)
                {
                    if (_shots)
                        for (int i = 0; i < 3; i++)
                            await ctx.Tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    else await ctx.Wait(0.3f);
                    var win = size;
                    Check(ctx, st.Name, win);
                    if (_shots)
                    {
                        var img = ctx.Tree.Root.GetTexture().GetImage();
                        img.SavePng($"{outDir}/{st.Name}_{win.X}x{win.Y}.png");
                    }
                }
                await st.Exit();
            }

            System.IO.Directory.CreateDirectory(outDir);
            var tagName = _shots ? $"{SuiteName}_{sizes[0].X}x{sizes[0].Y}" : SuiteName;
            System.IO.File.WriteAllText($"{outDir}/{tagName}.txt", string.Join("\n", _report));
            if (waves != null) waves.PauseAutoStart = false;
        }

        private static void PlaceSomeTowers(VineGrid grid)
        {
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            var region = grid.ActiveEntryRegions.FirstOrDefault();
            var path = region != null && pf != null ? pf.GetCachedPath(region.Cells[region.Cells.Count / 2]) : null;
            if (path == null) return;
            var types = new[] { VineNodeType.DamageTower, VineNodeType.TeslaCoil, VineNodeType.ScatterCannon, VineNodeType.FlakBattery };
            int placed = 0;
            for (int i = Math.Max(2, path.Count - 16); i < path.Count - 3 && placed < 4; i += 3)
                foreach (var off in new[] { new Vector2I(0, 2), new Vector2I(0, -2), new Vector2I(2, 0), new Vector2I(-2, 0) })
                {
                    var c = path[i] + off;
                    if (!grid.CanPlace(c) || pf.WouldBlockAllPaths(c)) continue;
                    var node = new VineNode();
                    node.Initialize(VineNodeRegistry.Get(types[placed % types.Length]));
                    if (grid.PlaceNode(node, c)) { placed++; break; }
                    node.QueueFree();
                }
        }

        private List<State> States(TestContext ctx, VineGrid grid, VinePlacer placer, VineWaveManager waves)
        {
            var tree = ctx.Tree;
            Task Done() => Task.CompletedTask;
            async Task Key(Key k)
            {
                Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
                await ctx.Wait(0.05f);
                Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
                await ctx.Wait(0.2f);
            }
            VinePerkScreen perk = null;
            return new List<State>
            {
                new("build", Done, Done),
                new("placing", async () =>
                {
                    placer?.StartPlacing(VineNodeType.TeslaCoil);
                    var vp = tree.Root.GetVisibleRect().Size;
                    Input.WarpMouse(vp * new Vector2(0.45f, 0.55f));
                    await ctx.Wait(0.3f);
                }, async () => { placer?.CancelPlacing(); await ctx.Wait(0.1f); }),
                new("help", () => Key(Godot.Key.H), () => Key(Godot.Key.H)),
                new("picker", async () =>
                {
                    if (placer != null && grid.Harvester != null) placer.ShowMaterialTypeSelection(grid.Harvester);
                    await ctx.Wait(0.3f);
                }, async () =>
                {
                    if (placer != null && GodotObject.IsInstanceValid(placer.MaterialPicker)) placer.MaterialPicker.QueueFree();
                    await ctx.Wait(0.1f);
                }),
                new("perk", async () =>
                {
                    perk = new VinePerkScreen { InBattleOverlay = true, Wave = 5 };
                    tree.CurrentScene.AddChild(perk);
                    await ctx.Wait(0.4f);
                }, async () =>
                {
                    if (GodotObject.IsInstanceValid(perk)) perk.QueueFree();
                    tree.Paused = false;
                    await ctx.Wait(0.2f);
                }),
                new("spire", async () =>
                {
                    if (ServiceLocator.TryGet<VinePlayer>(out var pl) && grid.Harvester != null)
                    {
                        var p = grid.Harvester.GlobalPosition + new Vector3(Constants.VINE_CELL_SIZE * 1.4f, 0, 0);
                        pl.GlobalPosition = new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
                    }
                    await ctx.Wait(0.3f);
                    SpireStation.Current?.OpenPanel();
                    await ctx.Wait(0.4f);
                }, async () => { SpireStation.Current?.ClosePanel(); await ctx.Wait(0.1f); }),
                new("docked", async () =>
                {
                    SpireStation.Current?.Dock();
                    await ctx.Wait(0.4f);
                }, async () => { SpireStation.Current?.Undock(); await ctx.Wait(0.2f); }),
                new("tower", async () =>
                {
                    // A tower's panel at the branch choice (the tallest it gets)
                    var t = All<VineNode>(tree.CurrentScene).FirstOrDefault(n => n.Data?.Type == VineNodeType.DamageTower);
                    if (t != null && TowerInspector.Current != null)
                    {
                        t.ForceUpgrade(3);
                        TowerInspector.Current.Open(t);
                    }
                    await ctx.Wait(0.4f);
                }, async () => { TowerInspector.Current?.Close(); await ctx.Wait(0.1f); }),
                new("pause", async () =>
                {
                    All<PauseMenu>(tree.Root).FirstOrDefault()?.Open();
                    await ctx.Wait(0.3f);
                }, async () =>
                {
                    All<PauseMenu>(tree.Root).FirstOrDefault()?.Resume();
                    await ctx.Wait(0.2f);
                }),
                new("wave", async () =>
                {
                    if (waves == null) return;
                    waves.PauseAutoStart = false;
                    waves.StartWave();
                    await ctx.WaitUntil(() => tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY).Count >= 3, 20f);
                    await ctx.Wait(2f);
                }, Done),
            };
        }

        // ── Layout checks ──

        private void Check(TestContext ctx, string state, Vector2I win)
        {
            var root = ctx.Tree.Root;
            var vis = root.GetVisibleRect();
            // canvas_items stretch keeps the 1920x1080 design canvas and scales it to the window
            var design = new Vector2(1920, 1080);
            float scale = Mathf.Min(win.X / design.X, win.Y / design.Y);
            string tag = $"{state} {win.X}x{win.Y}";

            var offScreen = new List<string>();
            var spill = new List<string>();
            var small = new List<string>();
            var panels = new List<(string, Rect2)>();

            foreach (var layer in All<CanvasLayer>(root))
            {
                if (!layer.Visible || layer.GetParent() is SubViewport) continue;
                foreach (var c in Controls(layer))
                {
                    if (!c.IsVisibleInTree() || c.Size.X < 2 || c.Size.Y < 2) continue;
                    if (c.Modulate.A * c.SelfModulate.A < 0.05f) continue;
                    if (ClippedByAncestor(c)) continue;
                    var r = c.GetGlobalRect();
                    string name = Describe(c);
                    bool draws = c is Label { Text.Length: > 0 } or Button or TextureRect or ProgressBar or ColorRect or RichTextLabel
                        || (c is PanelContainer or Panel);
                    if (draws && (r.Position.X < vis.Position.X - 1 || r.Position.Y < vis.Position.Y - 1
                        || r.End.X > vis.End.X + 1 || r.End.Y > vis.End.Y + 1))
                        offScreen.Add($"{name} {Fmt(r)}");

                    int px = FontPx(c);
                    if (px > 0 && px < MinFontPx && TextOf(c).Length > 0)
                        small.Add($"{name} {px}px");

                    if (c is Label l && !l.ClipText && l.AutowrapMode == TextServer.AutowrapMode.Off && l.Text.Length > 0)
                    {
                        var need = l.GetThemeFont("font").GetStringSize(l.Text, l.HorizontalAlignment, -1, l.GetThemeFontSize("font_size")).X;
                        var box = c.GetParentControl();
                        if (box != null && box is not ScrollContainer && need > box.Size.X + 2 && box.ClipContents == false)
                            spill.Add($"{name} needs {need:F0} in {box.Size.X:F0}");
                    }
                }
                // Top-level panels on this layer (what a player reads as separate boxes)
                foreach (var child in layer.GetChildren().OfType<Control>())
                    foreach (var p in TopPanels(child))
                        panels.Add((Describe(p), p.GetGlobalRect()));
            }

            var overlaps = new List<string>();
            for (int i = 0; i < panels.Count; i++)
                for (int j = i + 1; j < panels.Count; j++)
                {
                    var a = panels[i].Item2; var b = panels[j].Item2;
                    var inter = a.Intersection(b);
                    if (inter.Size.X > 4 && inter.Size.Y > 4 && !Covers(a, vis) && !Covers(b, vis))
                        overlaps.Add($"{panels[i].Item1} x {panels[j].Item1}");
                }

            _report.Add($"== {tag} (scale {scale:F2})");
            foreach (var s in offScreen) _report.Add($"  OFF  {s}");
            foreach (var s in spill) _report.Add($"  SPILL {s}");
            foreach (var s in overlaps) _report.Add($"  OVER {s}");
            foreach (var s in small.Distinct()) _report.Add($"  SMALL {s} (reads as {s.Split(' ').Last().TrimEnd('p', 'x')} x {scale:F2})");

            ctx.StartTest();
            ctx.Assert(offScreen.Count == 0, $"{SuiteName}/{state}/{win.X}x{win.Y}/on_screen",
                $"{offScreen.Count} off screen: {string.Join("; ", offScreen.Take(4))}");
            ctx.Assert(spill.Count == 0, $"{SuiteName}/{state}/{win.X}x{win.Y}/text_fits",
                $"{spill.Count} spill: {string.Join("; ", spill.Take(4))}");
            ctx.Assert(overlaps.Count == 0, $"{SuiteName}/{state}/{win.X}x{win.Y}/panels_clear",
                $"{overlaps.Count} overlap: {string.Join("; ", overlaps.Take(4))}");
            if (win.X == 1920 && win.Y == 1080)
                ctx.Assert(small.Count == 0, $"{SuiteName}/{state}/readable",
                    $"{small.Distinct().Count()} under {MinFontPx}px: {string.Join("; ", small.Distinct().Take(6))}");
        }

        private static bool Covers(Rect2 r, Rect2 vis) => r.Size.X >= vis.Size.X - 2 && r.Size.Y >= vis.Size.Y - 2;

        /// <summary>Visible panels directly under a layer's root controls (full-screen wrappers skipped).</summary>
        private static IEnumerable<Control> TopPanels(Control c)
        {
            if (!c.IsVisibleInTree()) yield break;
            bool wrapper = c is CenterContainer || (c.GetType() == typeof(Control)) || c is MarginContainer && c.GetChildCount() == 1;
            if (!wrapper && (c is PanelContainer or Panel or ColorRect) && c.Size.X > 2 && c.Size.Y > 2)
            {
                yield return c;
                yield break;
            }
            foreach (var ch in c.GetChildren().OfType<Control>())
                foreach (var p in TopPanels(ch)) yield return p;
        }

        private static IEnumerable<Control> Controls(Node n)
        {
            foreach (var ch in n.GetChildren())
            {
                if (ch is Control c) yield return c;
                if (ch is CanvasLayer) continue;
                foreach (var x in Controls(ch)) yield return x;
            }
        }

        private static bool ClippedByAncestor(Control c)
        {
            for (var p = c.GetParentControl(); p != null; p = p.GetParentControl())
                if (p.ClipContents || p is ScrollContainer) return true;
            return false;
        }

        private static int FontPx(Control c) => c switch
        {
            Label l => l.GetThemeFontSize("font_size"),
            Button b => b.GetThemeFontSize("font_size"),
            RichTextLabel r => r.GetThemeFontSize("normal_font_size"),
            LineEdit e => e.GetThemeFontSize("font_size"),
            _ => 0,
        };

        private static string TextOf(Control c) => c switch
        {
            Label l => l.Text ?? "",
            Button b => b.Text ?? "",
            RichTextLabel r => r.GetParsedText() ?? "",
            _ => "",
        };

        private static string Describe(Control c)
        {
            string text = TextOf(c);
            if (text.Length > 24) text = text[..24] + "...";
            text = text.Replace("\n", " ");
            return text.Length > 0 ? $"{c.GetType().Name} \"{text}\"" : $"{c.GetType().Name} {c.Name}";
        }

        private static string Fmt(Rect2 r) => $"[{r.Position.X:F0},{r.Position.Y:F0} {r.Size.X:F0}x{r.Size.Y:F0}]";

        private static IEnumerable<T> All<T>(Node root) where T : class
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            foreach (var child in root.GetChildren())
                foreach (var x in All<T>(child)) yield return x;
        }
    }
}
