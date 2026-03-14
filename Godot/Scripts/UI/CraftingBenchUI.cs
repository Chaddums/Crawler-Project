using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Crafting bench UI panel. Shows available Scrap Components and recipe list.
    /// Players craft random equipment by spending components.
    /// </summary>
    public partial class CraftingBenchUI : CanvasLayer
    {
        private Control _panel;
        private Label _componentLabel;
        private VBoxContainer _recipeList;
        private Label _feedbackLabel;
        private float _feedbackTimer;
        private PlayerInventory _inventory;

        private static readonly Color Gold        = new(0.9f, 0.8f, 0.3f);
        private static readonly Color DimText     = new(0.6f, 0.6f, 0.65f);
        private static readonly Color BgDark      = new(0.05f, 0.05f, 0.1f, 0.97f);
        private static readonly Color BorderColor = new(0.3f, 0.6f, 0.9f);

        public override void _Ready()
        {
            Layer = 25;
            BuildUI();
            _panel.Visible = false;
        }

        private void BuildUI()
        {
            // Dim overlay — close on click
            var overlay = new ColorRect();
            overlay.Color = new Color(0, 0, 0, 0.5f);
            overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            overlay.MouseFilter = Control.MouseFilterEnum.Stop;
            overlay.GuiInput += (InputEvent ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    Close();
            };
            AddChild(overlay);

            // Main panel
            _panel = new Control();
            _panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_panel);
            _panel.AddChild(overlay);

            var container = new PanelContainer();
            container.Position = new Vector2(580, 200);
            container.Size = new Vector2(760, 560);

            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = BgDark;
            panelStyle.BorderColor = BorderColor;
            panelStyle.SetBorderWidthAll(2);
            panelStyle.SetCornerRadiusAll(8);
            panelStyle.ContentMarginLeft = 24;
            panelStyle.ContentMarginRight = 24;
            panelStyle.ContentMarginTop = 16;
            panelStyle.ContentMarginBottom = 16;
            container.AddThemeStyleboxOverride("panel", panelStyle);
            _panel.AddChild(container);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            container.AddChild(vbox);

            // Title row
            var titleRow = new HBoxContainer();
            vbox.AddChild(titleRow);

            var titleLabel = new Label();
            titleLabel.Text = "CRAFTING BENCH";
            titleLabel.AddThemeFontSizeOverride("font_size", 24);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1f));
            titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            titleRow.AddChild(titleLabel);

            var closeBtn = new Button();
            closeBtn.Text = "✕";
            closeBtn.AddThemeFontSizeOverride("font_size", 18);
            closeBtn.Pressed += Close;
            titleRow.AddChild(closeBtn);

            // Component count
            var compRow = new HBoxContainer();
            vbox.AddChild(compRow);

            var compIcon = new Label();
            compIcon.Text = "⚙";
            compIcon.AddThemeFontSizeOverride("font_size", 18);
            compIcon.AddThemeColorOverride("font_color", Gold);
            compRow.AddChild(compIcon);

            _componentLabel = new Label();
            _componentLabel.AddThemeFontSizeOverride("font_size", 16);
            _componentLabel.AddThemeColorOverride("font_color", Gold);
            compRow.AddChild(_componentLabel);

            var hintLabel = new Label();
            hintLabel.Text = "  — salvage equipment from your bag to earn more";
            hintLabel.AddThemeFontSizeOverride("font_size", 13);
            hintLabel.AddThemeColorOverride("font_color", DimText);
            compRow.AddChild(hintLabel);

            // Separator
            var sep = new HSeparator();
            vbox.AddChild(sep);

            // Recipe list header
            var recipesHeader = new Label();
            recipesHeader.Text = "Recipes";
            recipesHeader.AddThemeFontSizeOverride("font_size", 16);
            recipesHeader.AddThemeColorOverride("font_color", DimText);
            vbox.AddChild(recipesHeader);

            // Recipe rows
            _recipeList = new VBoxContainer();
            _recipeList.AddThemeConstantOverride("separation", 6);
            vbox.AddChild(_recipeList);

            // Feedback label
            _feedbackLabel = new Label();
            _feedbackLabel.AddThemeFontSizeOverride("font_size", 15);
            _feedbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _feedbackLabel.Visible = false;
            vbox.AddChild(_feedbackLabel);
        }

        public void Open(PlayerInventory inventory)
        {
            _inventory = inventory;
            RefreshUI();
            _panel.Visible = true;
            GetViewport().GuiReleaseFocus();
        }

        public void Close()
        {
            _panel.Visible = false;
            GetViewport().GuiReleaseFocus();
        }

        private void RefreshUI()
        {
            int components = CraftingSystem.GetComponentCount(_inventory);
            _componentLabel.Text = $"Scrap Components: {components}";

            // Rebuild recipe rows
            foreach (var child in _recipeList.GetChildren())
                child.QueueFree();

            foreach (var recipe in CraftingSystem.Recipes)
            {
                var row = BuildRecipeRow(recipe, components);
                _recipeList.AddChild(row);
            }
        }

        private Control BuildRecipeRow(CraftRecipe recipe, int available)
        {
            var row = new PanelContainer();

            var rowStyle = new StyleBoxFlat();
            rowStyle.BgColor = new Color(0.08f, 0.08f, 0.14f);
            rowStyle.SetBorderWidthAll(1);
            rowStyle.BorderColor = new Color(0.2f, 0.2f, 0.3f);
            rowStyle.SetCornerRadiusAll(4);
            rowStyle.ContentMarginLeft = 12;
            rowStyle.ContentMarginRight = 12;
            rowStyle.ContentMarginTop = 8;
            rowStyle.ContentMarginBottom = 8;
            row.AddThemeStyleboxOverride("panel", rowStyle);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 12);
            row.AddChild(hbox);

            // Name + rarity
            var nameLabel = new Label();
            nameLabel.Text = $"{recipe.DisplayName}";
            nameLabel.AddThemeFontSizeOverride("font_size", 15);
            nameLabel.AddThemeColorOverride("font_color", GetRarityColor(recipe.OutputRarity));
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(nameLabel);

            var rarityLabel = new Label();
            rarityLabel.Text = recipe.RarityLabel;
            rarityLabel.AddThemeFontSizeOverride("font_size", 13);
            rarityLabel.AddThemeColorOverride("font_color", GetRarityColor(recipe.OutputRarity));
            rarityLabel.CustomMinimumSize = new Vector2(80, 0);
            hbox.AddChild(rarityLabel);

            // Cost
            bool canAfford = available >= recipe.ComponentCost;
            var costLabel = new Label();
            costLabel.Text = $"⚙ {recipe.ComponentCost}";
            costLabel.AddThemeFontSizeOverride("font_size", 14);
            costLabel.AddThemeColorOverride("font_color", canAfford ? Gold : new Color(0.5f, 0.3f, 0.3f));
            costLabel.CustomMinimumSize = new Vector2(60, 0);
            hbox.AddChild(costLabel);

            // Craft button
            var craftBtn = new Button();
            craftBtn.Text = "Craft";
            craftBtn.Disabled = !canAfford;
            craftBtn.CustomMinimumSize = new Vector2(70, 0);
            craftBtn.Pressed += () => OnCraft(recipe);
            hbox.AddChild(craftBtn);

            return row;
        }

        private void OnCraft(CraftRecipe recipe)
        {
            if (_inventory == null) return;

            var crafted = CraftingSystem.CraftItem(recipe, _inventory);
            if (crafted == null)
            {
                ShowFeedback("Not enough Scrap Components!", new Color(0.9f, 0.3f, 0.3f));
                return;
            }

            _inventory.TryAddItem(crafted);
            ShowFeedback($"Crafted: {crafted.GetDisplayName()}!", GetRarityColor(crafted.Rarity));
            RefreshUI();

            // Sprite VFX at the crafting bench
            if (PlayerManager.P1 != null && GodotObject.IsInstanceValid(PlayerManager.P1))
                SpriteVfxLibrary.SpawnCraftEffect(GetTree().Root, PlayerManager.P1.GlobalPosition);

            // AXIS quip
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                string line = crafted.Rarity >= ItemRarity.Rare
                    ? $"AXIS: Impressive fabrication. {crafted.GetDisplayName()} — not bad for scrap."
                    : $"AXIS: You built {crafted.GetDisplayName()} out of garbage. Fitting.";
                commentary.QueueLine("AXIS", line, CommentaryPriority.Low, CommentaryCategory.LootReaction);
            }
        }

        private void ShowFeedback(string message, Color color)
        {
            _feedbackLabel.Text = message;
            _feedbackLabel.AddThemeColorOverride("font_color", color);
            _feedbackLabel.Visible = true;
            _feedbackTimer = 2.5f;
        }

        public override void _Process(double delta)
        {
            if (_feedbackTimer > 0)
            {
                _feedbackTimer -= (float)delta;
                if (_feedbackTimer <= 0)
                    _feedbackLabel.Visible = false;
            }
        }

        private static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common    => new Color(0.7f, 0.7f, 0.7f),
            ItemRarity.Uncommon  => new Color(0f,   1f,   0f),
            ItemRarity.Rare      => new Color(0.27f, 0.53f, 1f),
            ItemRarity.Epic      => new Color(0.67f, 0.27f, 1f),
            ItemRarity.Legendary => new Color(1f,   0.53f, 0f),
            ItemRarity.Absurd    => new Color(0.8f, 0.8f,  0.2f),
            _                    => new Color(0.5f, 0.5f,  0.5f),
        };
    }
}
