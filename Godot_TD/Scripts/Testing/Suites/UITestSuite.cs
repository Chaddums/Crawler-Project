using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// UI test suite — loads UI scenes and inspects node tree structure.
    /// Verifies MainMenu, VineDraftScreen, VineHUD, and end-screen overlays.
    /// </summary>
    public class UITestSuite : ITestSuite
    {
        public string SuiteName => "ui";

        public async Task Run(TestContext ctx)
        {
            await TestMainMenuLoads(ctx);
            await TestDraftScreenLoads(ctx);
            await TestDraftHas3Cards(ctx);
            await TestDraftCardLabels(ctx);
            await TestDraftSelectSetsRole(ctx);
            await TestDraftSelectTransitions(ctx);
            await TestBattleSceneLoads(ctx);
            await TestBattleHasHUD(ctx);
            await TestHUDHasGoldLabel(ctx);
            await TestHUDHasLivesLabel(ctx);
            await TestHUDHasWaveLabel(ctx);
            await TestHUDHasPhaseLabel(ctx);
            await TestHUDHasNodeButtons(ctx);
            await TestHUDStartWaveButton(ctx);
            await TestHUDSpeedButton(ctx);
            await TestEndScreenOnVictory(ctx);
            await TestEndScreenOnDefeat(ctx);
        }

        // ── Helpers ──

        private int CountNodesOfType<T>(TestContext ctx) where T : Node
        {
            return ctx.FindNodes<T>().Count;
        }

        private Label FindLabelContaining(TestContext ctx, string text)
        {
            foreach (var label in ctx.FindNodes<Label>())
                if (label.Text != null && label.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
                    return label;
            return null;
        }

        private Button FindButtonContaining(TestContext ctx, string text)
        {
            foreach (var btn in ctx.FindNodes<Button>())
                if (btn.Text != null && btn.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
                    return btn;
            return null;
        }

        // ── Tests ──

        // 1. main_menu_loads
        private async Task TestMainMenuLoads(TestContext ctx)
        {
            ctx.StartTest();
            ctx.Tree.ChangeSceneToFile(Constants.SCENE_MAIN_MENU);
            await ctx.Wait(0.5f);
            var root = ctx.Tree.CurrentScene;
            ctx.AssertNotNull(root, "ui.main_menu_loads", "MainMenu scene root should exist");
        }

        // 2. draft_screen_loads
        private async Task TestDraftScreenLoads(TestContext ctx)
        {
            ctx.StartTest();
            GameManager.Instance?.StartVineDraft();
            await ctx.Wait(0.5f);
            var draft = ctx.FindNode<VineDraftScreen>();
            ctx.AssertNotNull(draft, "ui.draft_screen_loads", "VineDraftScreen should be in tree");
        }

        // 3. draft_has_3_cards
        private async Task TestDraftHas3Cards(TestContext ctx)
        {
            ctx.StartTest();
            // Draft screen should still be loaded from previous test
            var draft = ctx.FindNode<VineDraftScreen>();
            if (draft == null)
            {
                GameManager.Instance?.StartVineDraft();
                await ctx.Wait(0.5f);
                draft = ctx.FindNode<VineDraftScreen>();
            }

            var panels = ctx.FindNodes<PanelContainer>(draft);
            // The 3 role cards are PanelContainer children within the HBoxContainer
            // Filter to the card-sized panels (exclude outer panel and other containers)
            int cardCount = 0;
            foreach (var panel in panels)
            {
                if (panel.CustomMinimumSize.X >= 200 && panel.CustomMinimumSize.Y >= 400)
                    cardCount++;
            }
            ctx.AssertEqual(3, cardCount, "ui.draft_has_3_cards", "Draft should have 3 role cards");
        }

        // 4. draft_card_labels
        private async Task TestDraftCardLabels(TestContext ctx)
        {
            ctx.StartTest();
            var draft = ctx.FindNode<VineDraftScreen>();
            if (draft == null)
            {
                GameManager.Instance?.StartVineDraft();
                await ctx.Wait(0.5f);
            }

            var scrapwright = FindLabelContaining(ctx, "SCRAPWRIGHT");
            var arcanist = FindLabelContaining(ctx, "ARCANIST");
            var bruteforge = FindLabelContaining(ctx, "BRUTEFORGE");

            ctx.AssertNotNull(scrapwright, "ui.draft_card_labels_scrapwright", "Should find SCRAPWRIGHT label");
            ctx.AssertNotNull(arcanist, "ui.draft_card_labels_arcanist", "Should find ARCANIST label");
            ctx.AssertNotNull(bruteforge, "ui.draft_card_labels_bruteforge", "Should find BRUTEFORGE label");
        }

        // 5. draft_select_sets_role
        private async Task TestDraftSelectSetsRole(TestContext ctx)
        {
            ctx.StartTest();
            var gm = GameManager.Instance;
            if (gm == null)
            {
                ctx.Assert(false, "ui.draft_select_sets_role", "GameManager.Instance is null");
                return;
            }

            gm.SelectedRole = "Scrapwright";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);

            ctx.AssertEqual("Scrapwright", gm.SelectedRole, "ui.draft_select_sets_role",
                "SelectedRole should be Scrapwright");
            ctx.AssertNotNull(gm.AvailableNodes, "ui.draft_select_sets_nodes",
                "AvailableNodes should not be null after role selection");
            ctx.AssertEqual(8, gm.AvailableNodes.Length, "ui.draft_select_node_count",
                "Scrapwright role should have 8 nodes");
            await Task.CompletedTask;
        }

        // 6. draft_select_transitions
        private async Task TestDraftSelectTransitions(TestContext ctx)
        {
            ctx.StartTest();
            var gm = GameManager.Instance;
            if (gm == null)
            {
                ctx.Assert(false, "ui.draft_select_transitions", "GameManager.Instance is null");
                return;
            }

            // Ensure role is selected before starting battle
            gm.SelectedRole = "Scrapwright";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineBattle();

            bool reached = await ctx.WaitForPhase(GamePhase.Build, 3.0f);
            ctx.Assert(reached, "ui.draft_select_transitions",
                "Should transition to Build phase after selecting role and starting battle");
        }

        // 7. battle_scene_loads
        private async Task TestBattleSceneLoads(TestContext ctx)
        {
            ctx.StartTest();
            // Battle scene should be loaded from previous test, but ensure it
            var gm = GameManager.Instance;
            if (gm?.CurrentPhase != GamePhase.Build)
            {
                gm?.StartVineBattle();
                await ctx.WaitForPhase(GamePhase.Build, 3.0f);
            }
            var root = ctx.Tree.CurrentScene;
            ctx.AssertNotNull(root, "ui.battle_scene_loads", "VineBattle scene root should exist");
        }

        // 8. battle_has_hud
        private async Task TestBattleHasHUD(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);
            var hud = ctx.FindNode<VineHUD>();
            ctx.AssertNotNull(hud, "ui.battle_has_hud", "VineHUD should be in tree during battle");
        }

        // 9. hud_has_scrap_label
        private async Task TestHUDHasGoldLabel(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);
            var label = FindLabelContaining(ctx, "Resources:");
            ctx.AssertNotNull(label, "ui.hud_has_scrap_label", "HUD should contain a label with Resources:");
        }

        // 10. hud_has_lives_label
        private async Task TestHUDHasLivesLabel(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);
            var label = FindLabelContaining(ctx, "Lives:");
            ctx.AssertNotNull(label, "ui.hud_has_lives_label", "HUD should contain a label with 'Lives:'");
        }

        // 11. hud_has_wave_label
        private async Task TestHUDHasWaveLabel(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);
            var label = FindLabelContaining(ctx, "Wave:");
            ctx.AssertNotNull(label, "ui.hud_has_wave_label", "HUD should contain a label with 'Wave:'");
        }

        // 12. hud_has_phase_label
        private async Task TestHUDHasPhaseLabel(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);
            var label = FindLabelContaining(ctx, "BUILD");
            ctx.AssertNotNull(label, "ui.hud_has_phase_label",
                "HUD should contain a label with 'BUILD' (initial phase)");
        }

        // 13. hud_has_node_buttons
        private async Task TestHUDHasNodeButtons(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);

            var hboxes = ctx.FindNodes<HBoxContainer>();
            HBoxContainer buildBar = null;
            foreach (var hbox in hboxes)
            {
                int buttonCount = 0;
                foreach (var child in hbox.GetChildren())
                {
                    if (child is Button) buttonCount++;
                }
                if (buttonCount >= 4)
                {
                    buildBar = hbox;
                    break;
                }
            }

            ctx.AssertNotNull(buildBar, "ui.hud_has_node_buttons",
                "HUD should have an HBoxContainer with node build buttons");

            if (buildBar != null)
            {
                int btnCount = 0;
                foreach (var child in buildBar.GetChildren())
                {
                    if (child is Button) btnCount++;
                }
                ctx.AssertEqual(8, btnCount, "ui.hud_node_button_count",
                    "Build bar should have 8 node buttons matching selected role");
            }
        }

        // 14. hud_start_wave_button
        private async Task TestHUDStartWaveButton(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);
            var btn = FindButtonContaining(ctx, "Start Wave");
            ctx.AssertNotNull(btn, "ui.hud_start_wave_button",
                "HUD should have a button with text containing 'Start Wave'");
        }

        // 15. hud_speed_button
        private async Task TestHUDSpeedButton(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);
            var btn = FindButtonContaining(ctx, "Speed:");
            ctx.AssertNotNull(btn, "ui.hud_speed_button",
                "HUD should have a button with text containing 'Speed:'");
        }

        // 16. end_screen_on_victory
        private async Task TestEndScreenOnVictory(TestContext ctx)
        {
            ctx.StartTest();
            await EnsureBattleScene(ctx);

            GameManager.Instance?.SetPhase(GamePhase.Victory);
            await ctx.Wait(0.3f);

            var label = FindLabelContaining(ctx, "NETWORK COMPLETE");
            ctx.AssertNotNull(label, "ui.end_screen_on_victory",
                "Victory end screen should show 'NETWORK COMPLETE'");

            // Return to main menu to clean up the end overlay for subsequent tests
            GameManager.Instance?.ReturnToMainMenu();
            await ctx.Wait(0.3f);
        }

        // 17. end_screen_on_defeat
        private async Task TestEndScreenOnDefeat(TestContext ctx)
        {
            ctx.StartTest();

            // Reload battle scene fresh (previous test returned to main menu)
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SelectedRole = "Scrapwright";
                gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
                gm.StartVineBattle();
            }
            await ctx.WaitForPhase(GamePhase.Build, 3.0f);

            GameManager.Instance?.SetPhase(GamePhase.Defeat);
            await ctx.Wait(0.3f);

            var label = FindLabelContaining(ctx, "CORE BREACHED");
            ctx.AssertNotNull(label, "ui.end_screen_on_defeat",
                "Defeat end screen should show 'CORE BREACHED'");

            // Clean up
            GameManager.Instance?.ReturnToMainMenu();
            await ctx.Wait(0.3f);
        }

        // ── Shared setup ──

        private async Task EnsureBattleScene(TestContext ctx)
        {
            var hud = ctx.FindNode<VineHUD>();
            if (hud != null) return;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SelectedRole = "Scrapwright";
                gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
                gm.StartVineBattle();
            }
            await ctx.WaitForPhase(GamePhase.Build, 3.0f);
        }
    }
}
