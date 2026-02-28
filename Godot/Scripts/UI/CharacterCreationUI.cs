using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Class selection screen. Shows 6 class cards, a description panel,
    /// and a Begin Crawl button.
    /// </summary>
    public partial class CharacterCreationUI : Control
    {
        private HBoxContainer _classContainer;
        private Label _selectedClassName;
        private Label _descriptionLabel;
        private GridContainer _statsGrid;
        private Button _startButton;
        private Button _backButton;

        private CrawlerClassName? _selectedClass;
        private readonly Dictionary<CrawlerClassName, PanelContainer> _classCards = new();

        private static readonly Color GoldColor = new(0.9f, 0.8f, 0.3f);
        private static readonly Color DimGold = new(0.6f, 0.5f, 0.2f);
        private static readonly Color SelectedBorder = new(0.9f, 0.8f, 0.3f);
        private static readonly Color CardBg = new(0.12f, 0.12f, 0.18f);
        private static readonly Color CardHover = new(0.16f, 0.16f, 0.22f);
        private static readonly Color CardSelected = new(0.18f, 0.16f, 0.1f);

        public override void _Ready()
        {
            BuildUI();
            PopulateClassCards();
        }

        private void BuildUI()
        {
            // Background
            var bg = new ColorRect();
            bg.Color = new Color(0.05f, 0.05f, 0.12f);
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(bg);

            // Title
            var title = new Label();
            title.Text = "CHOOSE YOUR CLASS";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.SetAnchorsPreset(LayoutPreset.CenterTop);
            title.GrowHorizontal = GrowDirection.Both;
            title.Position = new Vector2(960 - 300, 40);
            title.Size = new Vector2(600, 80);
            title.AddThemeFontSizeOverride("font_size", 48);
            title.AddThemeColorOverride("font_color", GoldColor);
            AddChild(title);

            // Class cards container
            _classContainer = new HBoxContainer();
            _classContainer.Position = new Vector2(60, 140);
            _classContainer.Size = new Vector2(1800, 320);
            _classContainer.AddThemeConstantOverride("separation", 12);
            _classContainer.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(_classContainer);

            // Description panel
            var descPanel = new PanelContainer();
            descPanel.Position = new Vector2(160, 500);
            descPanel.Size = new Vector2(1600, 320);
            var descStyle = new StyleBoxFlat();
            descStyle.BgColor = new Color(0.08f, 0.08f, 0.14f);
            descStyle.BorderColor = DimGold;
            descStyle.BorderWidthBottom = 2;
            descStyle.BorderWidthTop = 2;
            descStyle.BorderWidthLeft = 2;
            descStyle.BorderWidthRight = 2;
            descStyle.CornerRadiusBottomLeft = 8;
            descStyle.CornerRadiusBottomRight = 8;
            descStyle.CornerRadiusTopLeft = 8;
            descStyle.CornerRadiusTopRight = 8;
            descStyle.ContentMarginLeft = 30;
            descStyle.ContentMarginRight = 30;
            descStyle.ContentMarginTop = 20;
            descStyle.ContentMarginBottom = 20;
            descPanel.AddThemeStyleboxOverride("panel", descStyle);
            AddChild(descPanel);

            var descVBox = new VBoxContainer();
            descVBox.AddThemeConstantOverride("separation", 8);
            descPanel.AddChild(descVBox);

            _selectedClassName = new Label();
            _selectedClassName.Text = "Select a class above";
            _selectedClassName.AddThemeFontSizeOverride("font_size", 32);
            _selectedClassName.AddThemeColorOverride("font_color", GoldColor);
            descVBox.AddChild(_selectedClassName);

            _descriptionLabel = new Label();
            _descriptionLabel.Text = "";
            _descriptionLabel.AddThemeFontSizeOverride("font_size", 18);
            _descriptionLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            descVBox.AddChild(_descriptionLabel);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 8);
            descVBox.AddChild(spacer);

            _statsGrid = new GridContainer();
            _statsGrid.Columns = 7;
            _statsGrid.AddThemeConstantOverride("h_separation", 30);
            _statsGrid.AddThemeConstantOverride("v_separation", 6);
            descVBox.AddChild(_statsGrid);

            // Button row
            var buttonRow = new HBoxContainer();
            buttonRow.Position = new Vector2(660, 860);
            buttonRow.Size = new Vector2(600, 60);
            buttonRow.AddThemeConstantOverride("separation", 40);
            buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(buttonRow);

            _backButton = new Button();
            _backButton.Text = "Back";
            _backButton.CustomMinimumSize = new Vector2(200, 50);
            _backButton.AddThemeFontSizeOverride("font_size", 22);
            _backButton.Pressed += () => GameManager.Instance?.ReturnToMainMenu();
            buttonRow.AddChild(_backButton);

            _startButton = new Button();
            _startButton.Text = "Begin Crawl";
            _startButton.CustomMinimumSize = new Vector2(240, 50);
            _startButton.AddThemeFontSizeOverride("font_size", 22);
            _startButton.Disabled = true;
            _startButton.Pressed += HandleStartGame;
            buttonRow.AddChild(_startButton);
        }

        private void PopulateClassCards()
        {
            foreach (var kvp in CrawlerClassRegistry.Classes)
            {
                var classData = kvp.Value;
                var card = CreateClassCard(classData);
                _classContainer.AddChild(card);
                _classCards[kvp.Key] = card;
            }
        }

        private PanelContainer CreateClassCard(CrawlerClassData classData)
        {
            var card = new PanelContainer();
            card.CustomMinimumSize = new Vector2(270, 300);

            var style = new StyleBoxFlat();
            style.BgColor = CardBg;
            style.BorderColor = DimGold;
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 8;
            style.CornerRadiusBottomRight = 8;
            style.CornerRadiusTopLeft = 8;
            style.CornerRadiusTopRight = 8;
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 16;
            style.ContentMarginBottom = 16;
            card.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            card.AddChild(vbox);

            var nameLabel = new Label();
            nameLabel.Text = classData.DisplayName;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", 24);
            nameLabel.AddThemeColorOverride("font_color", GoldColor);
            vbox.AddChild(nameLabel);

            var separator = new HSeparator();
            vbox.AddChild(separator);

            var primaryLabel = new Label();
            primaryLabel.Text = $"Primary: {classData.PrimaryStat}";
            primaryLabel.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(primaryLabel);

            var secondaryLabel = new Label();
            secondaryLabel.Text = $"Secondary: {classData.SecondaryStat}";
            secondaryLabel.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(secondaryLabel);

            var hpLabel = new Label();
            hpLabel.Text = $"HP: {classData.BaseStats.GetBaseStat(StatType.MaxHealth):F0}";
            hpLabel.AddThemeFontSizeOverride("font_size", 14);
            hpLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.6f));
            vbox.AddChild(hpLabel);

            var manaLabel = new Label();
            manaLabel.Text = $"Mana: {classData.BaseStats.GetBaseStat(StatType.MaxMana):F0}";
            manaLabel.AddThemeFontSizeOverride("font_size", 14);
            manaLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.6f, 0.9f));
            vbox.AddChild(manaLabel);

            var spacer2 = new Control();
            spacer2.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(spacer2);

            var descSnippet = new Label();
            descSnippet.Text = classData.Description.Length > 60
                ? classData.Description[..57] + "..."
                : classData.Description;
            descSnippet.AutowrapMode = TextServer.AutowrapMode.Word;
            descSnippet.AddThemeFontSizeOverride("font_size", 13);
            descSnippet.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            vbox.AddChild(descSnippet);

            // Click handling
            card.GuiInput += (InputEvent ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    SelectClass(classData.ClassName);
            };

            card.MouseEntered += () =>
            {
                if (_selectedClass != classData.ClassName)
                {
                    var s = (StyleBoxFlat)card.GetThemeStylebox("panel");
                    s.BgColor = CardHover;
                }
            };

            card.MouseExited += () =>
            {
                if (_selectedClass != classData.ClassName)
                {
                    var s = (StyleBoxFlat)card.GetThemeStylebox("panel");
                    s.BgColor = CardBg;
                }
            };

            return card;
        }

        private void SelectClass(CrawlerClassName className)
        {
            _selectedClass = className;
            _startButton.Disabled = false;

            // Update card visuals
            foreach (var (cls, card) in _classCards)
            {
                var style = (StyleBoxFlat)card.GetThemeStylebox("panel");
                if (cls == className)
                {
                    style.BgColor = CardSelected;
                    style.BorderColor = SelectedBorder;
                }
                else
                {
                    style.BgColor = CardBg;
                    style.BorderColor = DimGold;
                }
            }

            // Update description panel
            var classData = CrawlerClassRegistry.GetClass(className);
            if (classData == null) return;

            _selectedClassName.Text = classData.DisplayName;
            _descriptionLabel.Text = classData.Description;

            // Update stats grid
            foreach (var child in _statsGrid.GetChildren())
                child.QueueFree();

            var statTypes = new[] {
                StatType.Strength, StatType.Dexterity, StatType.Constitution,
                StatType.Intelligence, StatType.Charisma, StatType.Luck, StatType.MaxHealth
            };

            // Header row
            foreach (var stat in statTypes)
            {
                var header = new Label();
                header.Text = stat.ToString()[..3].ToUpper();
                header.HorizontalAlignment = HorizontalAlignment.Center;
                header.AddThemeFontSizeOverride("font_size", 14);
                header.AddThemeColorOverride("font_color", DimGold);
                _statsGrid.AddChild(header);
            }

            // Value row
            foreach (var stat in statTypes)
            {
                var value = new Label();
                value.Text = $"{classData.BaseStats.GetBaseStat(stat):F0}";
                value.HorizontalAlignment = HorizontalAlignment.Center;
                value.AddThemeFontSizeOverride("font_size", 18);
                _statsGrid.AddChild(value);
            }

            GD.Print($"[CharacterCreationUI] Selected: {classData.DisplayName}");
        }

        private void HandleStartGame()
        {
            if (_selectedClass == null) return;

            GD.Print($"[CharacterCreationUI] Starting game with: {_selectedClass}");
            GameManager.Instance?.StartGameWithClass(_selectedClass.Value);
        }
    }
}
