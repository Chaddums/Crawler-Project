using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Input routing through the real viewport pipeline (Viewport.PushInput), so handler order and
    /// SetInputAsHandled matter exactly as they do for a player. Godot delivers _UnhandledInput to
    /// later/deeper nodes first and autoloads last, so an unhandled event reaches every handler.
    /// </summary>
    public class InputTestSuite : ITestSuite
    {
        public string SuiteName => "input";

        private VineGrid _grid;

        public async Task Run(TestContext ctx)
        {
            GD.Print("[InputTestSuite] Starting input routing tests...");

            var gm = GameManager.Instance;
            gm.CurrentTerritorySectionId = null;
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = "Obelisk";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 15f);
            ctx.Assert(inBuild, "input/battle_loads", "Battle should reach Build phase");
            if (!inBuild) return;
            _grid = ServiceLocator.Get<VineGrid>();
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm)) wm.PauseAutoStart = true;

            await TestTabTogglesSpeedOnce(ctx);
            await TestRightClickCancelKeepsTower(ctx);
            await TestRightDragKeepsTower(ctx);
            await TestRightClickSellsWhenNotPlacing(ctx);
            await TestMaterialChoiceEnablesMaterialsMode(ctx);
            await TestF11KeepsRun(ctx);
            await TestPickerOfferedAfterIntro(ctx);

            if (wm != null && GodotObject.IsInstanceValid(wm)) wm.PauseAutoStart = false;
            GD.Print("[InputTestSuite] Complete.");
        }

        private static async Task Frames(TestContext ctx, int n)
        {
            for (int i = 0; i < n; i++)
                await ctx.Tree.ToSignal(ctx.Tree, SceneTree.SignalName.ProcessFrame);
        }

        private static async Task PushKey(TestContext ctx, Key key)
        {
            var vp = ctx.Tree.Root;
            vp.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            await Frames(ctx, 2);
            vp.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
            await Frames(ctx, 2);
        }

        private static async Task PushMouse(TestContext ctx, MouseButton button, Vector2 pos)
        {
            var vp = ctx.Tree.Root;
            vp.PushInput(new InputEventMouseMotion { Position = pos, GlobalPosition = pos }, true);
            await Frames(ctx, 1);
            vp.PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = pos, GlobalPosition = pos }, true);
            await Frames(ctx, 2);
            vp.PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = pos, GlobalPosition = pos }, true);
            await Frames(ctx, 2);
        }

        /// <summary>Screen position of a cell's ground point (y = 0, matching RaycastGround).</summary>
        private Vector2? CellToScreen(TestContext ctx, Vector2I cell)
        {
            var camera = ctx.Tree.Root.GetCamera3D();
            if (camera == null) return null;
            var world = _grid.GridToWorld(cell);
            // On the drawn ground (a canyon's wall tops stand 3.5 up: at y = 0 the click lands on another cell)
            world.Y = _grid.GetWorldHeight(world.X, world.Z);
            if (camera.IsPositionBehind(world)) return null;
            return camera.UnprojectPosition(world);
        }

        /// <summary>Why the last search found no cell (tests' notes).</summary>
        private string _cellNote = "";

        private Vector2I? FindVisibleEmptyCell(TestContext ctx)
        {
            var size = ctx.Tree.Root.GetVisibleRect().Size;
            int open = 0, onScreen = 0, missed = 0;
            string miss = "";
            for (int x = 3; x < _grid.Width - 3; x++)
            for (int y = 3; y < _grid.Height - 3; y++)
            {
                var c = new Vector2I(x, y);
                if (!_grid.CanPlace(c)) continue;
                open++;
                var s = CellToScreen(ctx, c);
                if (s == null) continue;
                // Away from screen edges where HUD panels live, and the round trip must land on the cell
                // (on a canyon the Spire's plaza fills the middle: the wall tops are further out)
                float edge = _grid.IsCanyon ? 0.16f : 0.25f;
                if (s.Value.X < size.X * edge || s.Value.X > size.X * (1f - edge)) continue;
                if (s.Value.Y < size.Y * edge || s.Value.Y > size.Y * (1f - edge)) continue;
                onScreen++;
                var camera = ctx.Tree.Root.GetCamera3D();
                var from = camera.ProjectRayOrigin(s.Value);
                var dir = camera.ProjectRayNormal(s.Value);
                if (Mathf.Abs(dir.Y) < 0.001f) continue;
                var hit = _grid.PickGround(from, dir);
                if (hit == null || _grid.WorldToGrid(hit.Value) != c)
                {
                    if (missed++ == 0) miss = $", first {c} picked {(hit.HasValue ? _grid.WorldToGrid(hit.Value).ToString() : "nothing")}";
                    continue;
                }
                return c;
            }
            _cellNote = $"{open} buildable, {onScreen} mid-screen, {missed} round trips missed{miss} (canyon {_grid.IsCanyon})";
            return null;
        }

        private VineNode PlaceTower(Vector2I cell)
        {
            var node = new VineNode();
            node.Initialize(VineNodeRegistry.Get(VineNodeType.DamageTower));
            if (_grid.PlaceNode(node, cell)) return node;
            node.QueueFree();
            return null;
        }

        private static void ResetSpeed(GameManager gm)
        {
            for (int i = 0; i < 3 && gm.GameSpeed != Constants.SPEED_NORMAL; i++) gm.ToggleSpeed();
        }

        // ── Tab: one press = one speed step ──

        private async Task TestTabTogglesSpeedOnce(TestContext ctx)
        {
            ctx.StartTest();
            var gm = GameManager.Instance;
            ResetSpeed(gm);
            await Frames(ctx, 2);

            await PushKey(ctx, Key.Tab);
            float after = gm.GameSpeed;
            ResetSpeed(gm);

            ctx.AssertEqual(Constants.SPEED_FAST, after, "input/tab_one_speed_step",
                $"One Tab press from {Constants.SPEED_NORMAL}x should give {Constants.SPEED_FAST}x (got {after}x — handled twice?)");
        }

        // ── Right-click while placing cancels placement and must not sell what's under the cursor ──

        private async Task TestRightClickCancelKeepsTower(TestContext ctx)
        {
            ctx.StartTest();
            var cell = FindVisibleEmptyCell(ctx);
            ctx.Assert(cell != null, "input/found_visible_cell", "Need an on-screen buildable cell: " + _cellNote);
            if (cell == null) return;
            var tower = PlaceTower(cell.Value);
            ctx.AssertNotNull(tower, "input/tower_placed");
            if (tower == null) return;
            await Frames(ctx, 2);

            var placer = ServiceLocator.Get<VinePlacer>();
            placer.StartPlacing(VineNodeType.DamageTower);
            await Frames(ctx, 2);
            int resourcesBefore = GameManager.Instance.CurrentResources;

            var screen = CellToScreen(ctx, cell.Value).Value;
            await PushMouse(ctx, MouseButton.Right, screen);

            ctx.Assert(!placer.IsPlacing, "input/right_click_cancels_placement", "Right-click should cancel placement");
            ctx.Assert(_grid.GetNode(cell.Value) != null, "input/right_click_cancel_keeps_tower",
                "Cancelling placement with right-click sold the tower under the cursor");
            ctx.AssertEqual(resourcesBefore, GameManager.Instance.CurrentResources, "input/right_click_cancel_no_refund",
                "Cancelling placement should not change resources");
        }

        // ── Material picker: choosing a material makes the Materials mining mode reachable ──

        private async Task TestMaterialChoiceEnablesMaterialsMode(TestContext ctx)
        {
            ctx.StartTest();
            var harvester = _grid.Harvester;
            var placer = ServiceLocator.Get<VinePlacer>();
            if (harvester == null) { ctx.Assert(false, "input/material_picker", "No Spire"); return; }
            ctx.AssertEqual(MaterialType.None, harvester.SelectedMaterial, "input/material_starts_unset",
                "Under the test harness no material is pre-selected");

            placer.ShowMaterialTypeSelection(harvester);
            await Frames(ctx, 2);
            var picker = placer.MaterialPicker;
            ctx.Assert(GodotObject.IsInstanceValid(picker), "input/material_picker_shown", "Picker overlay should open");
            if (!GodotObject.IsInstanceValid(picker)) return;

            Button power = null;
            foreach (var n in picker.FindChildren("*", "Button", true, false))
                if (n is Button b && b.Text.StartsWith("Power", System.StringComparison.OrdinalIgnoreCase)) power = b;
            ctx.AssertNotNull(power, "input/material_picker_power_button");
            if (power == null) return;
            power.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(ctx, 2);

            ctx.AssertEqual(MaterialType.Power, harvester.SelectedMaterial, "input/material_selected");
            ctx.Assert(!GodotObject.IsInstanceValid(picker) || picker.IsQueuedForDeletion(), "input/material_picker_closes");

            GameManager.Instance.SetPhase(GamePhase.Build);
            if (harvester.CurrentMode != MiningMode.Resources) harvester.ToggleMode();
            harvester.ToggleMode();
            ctx.AssertEqual(MiningMode.Materials, harvester.CurrentMode, "input/materials_mode_reachable",
                "After choosing a material, toggling the Spire should switch to Materials mode");
            harvester.ToggleMode(); // back to Resources
        }

        // ── A fresh farming run offers the material picker once the intro ends ──

        private async Task TestPickerOfferedAfterIntro(TestContext ctx)
        {
            ctx.StartTest();
            var gm = GameManager.Instance;
            gm.PromptMaterialUnderTestHarness = true; // the sandbox normally suppresses the picker
            try
            {
                gm.CurrentTerritorySectionId = null;
                gm.CurrentRunMode = RunMode.Harvest;
                gm.StartVineRun();
                await ctx.Wait(1.0f);
                await ctx.WaitForPhase(GamePhase.Build, 15f);
                await Frames(ctx, 3);
                var placer = ServiceLocator.Get<VinePlacer>();
                ctx.Assert(placer != null && GodotObject.IsInstanceValid(placer.MaterialPicker),
                    "input/picker_after_intro", "Material picker should open when a farming run reaches Build");

                ctx.Tree.Paused = true;
                await Frames(ctx, 2);
                bool hiddenWhilePaused = placer != null && GodotObject.IsInstanceValid(placer.MaterialPicker) && !placer.MaterialPicker.Visible;
                ctx.Tree.Paused = false;
                await Frames(ctx, 2);
                ctx.Assert(hiddenWhilePaused, "input/picker_hidden_while_paused",
                    "Picker should hide while the tree is paused (it sits above the pause menu and perk screen)");
                ctx.Assert(placer != null && GodotObject.IsInstanceValid(placer.MaterialPicker) && placer.MaterialPicker.Visible,
                    "input/picker_returns_after_pause");
            }
            finally
            {
                gm.PromptMaterialUnderTestHarness = false;
            }
        }

        // ── F11 mid-run must not leave the battle ──

        private async Task TestF11KeepsRun(TestContext ctx)
        {
            ctx.StartTest();
            var scene = ctx.Tree.CurrentScene;
            await PushKey(ctx, Key.F11);
            await Frames(ctx, 5);
            bool kept = GameManager.Instance.CurrentPhase != GamePhase.LevelEditor
                        && ctx.Tree.CurrentScene == scene && GodotObject.IsInstanceValid(scene);
            await PushKey(ctx, Key.F11); // toggle fullscreen back for windowed runs
            await Frames(ctx, 2);
            ctx.Assert(kept, "input/f11_keeps_run", $"F11 during a run left the battle (phase {GameManager.Instance.CurrentPhase})");
        }

        // ── Right-drag turns the camera: starting one over a tower must not sell it ──

        private async Task TestRightDragKeepsTower(TestContext ctx)
        {
            ctx.StartTest();
            var cell = FindVisibleEmptyCell(ctx);
            if (cell == null) { ctx.Assert(false, "input/right_drag_keeps_tower", "No visible cell"); return; }
            var tower = PlaceTower(cell.Value);
            if (tower == null) { ctx.Assert(false, "input/right_drag_keeps_tower", "Could not place tower"); return; }
            await Frames(ctx, 2);
            var placer = ServiceLocator.Get<VinePlacer>();
            if (placer.IsPlacing) placer.CancelPlacing();
            var pos = CellToScreen(ctx, cell.Value).Value;
            var vp = ctx.Tree.Root;
            vp.PushInput(new InputEventMouseMotion { Position = pos, GlobalPosition = pos }, true);
            await Frames(ctx, 1);
            vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = pos, GlobalPosition = pos }, true);
            await Frames(ctx, 1);
            for (int i = 1; i <= 6; i++)
            {
                var p = pos + new Vector2(i * 12f, 0);
                vp.PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = new Vector2(12f, 0), ButtonMask = MouseButtonMask.Right }, true);
                await Frames(ctx, 1);
            }
            var end = pos + new Vector2(72f, 0);
            vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = end, GlobalPosition = end }, true);
            await Frames(ctx, 2);
            ctx.Assert(_grid.GetNode(cell.Value) == tower, "input/right_drag_keeps_tower",
                "Starting a camera turn over a tower sold it");
            if (_grid.GetNode(cell.Value) != null) _grid.RemoveNode(cell.Value);
            await Frames(ctx, 2);
        }

        // ── Positive control: right-click with nothing selected sells ──

        private async Task TestRightClickSellsWhenNotPlacing(TestContext ctx)
        {
            ctx.StartTest();
            var cell = FindVisibleEmptyCell(ctx);
            if (cell == null) { ctx.Assert(false, "input/right_click_sells", "No visible cell"); return; }
            var tower = PlaceTower(cell.Value);
            if (tower == null) { ctx.Assert(false, "input/right_click_sells", "Could not place tower"); return; }
            await Frames(ctx, 2);
            var placer = ServiceLocator.Get<VinePlacer>();
            if (placer.IsPlacing) placer.CancelPlacing();
            int before = GameManager.Instance.CurrentResources;

            await PushMouse(ctx, MouseButton.Right, CellToScreen(ctx, cell.Value).Value);

            ctx.Assert(_grid.GetNode(cell.Value) == null, "input/right_click_sells",
                "Right-click on a tower with nothing selected should sell it");
            ctx.Assert(GameManager.Instance.CurrentResources > before, "input/right_click_sell_refunds",
                $"Selling should refund resources ({before} -> {GameManager.Instance.CurrentResources})");
        }
    }
}
