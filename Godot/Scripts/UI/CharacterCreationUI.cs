using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Frame selection screen. Shows 6 bot frame cards, a description panel,
    /// and an Enter Arena button.
    /// </summary>
    public partial class CharacterCreationUI : Control
    {
        private HBoxContainer _classContainer;
        private Label _selectedClassName;
        private Label _descriptionLabel;
        private Label _loreLabel;
        private GridContainer _statsGrid;
        private Button _startButton;
        private Button _backButton;

        private BotFrameType? _selectedClass;
        private readonly Dictionary<BotFrameType, PanelContainer> _classCards = new();

        private static readonly Color GoldColor = new(0.9f, 0.8f, 0.3f);
        private static readonly Color DimGold = new(0.6f, 0.5f, 0.2f);
        private static readonly Color SelectedBorder = new(0.9f, 0.8f, 0.3f);
        private static readonly Color CardBg = new(0.12f, 0.12f, 0.18f);
        private static readonly Color CardHover = new(0.16f, 0.16f, 0.22f);
        private static readonly Color CardSelected = new(0.18f, 0.16f, 0.1f);
        private static readonly Color LockedBg = new(0.06f, 0.06f, 0.08f);
        private static readonly Color LockedBorder = new(0.3f, 0.3f, 0.3f);

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

            // Title — anchor-centered so it works at any resolution
            var title = new Label();
            title.Text = StringLoader.Get("ui.characterCreation.title");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.SetAnchorsPreset(LayoutPreset.CenterTop);
            title.GrowHorizontal = GrowDirection.Both;
            title.OffsetLeft = -300;
            title.OffsetRight = 300;
            title.OffsetTop = 40;
            title.OffsetBottom = 120;
            title.AddThemeFontSizeOverride("font_size", 48);
            title.AddThemeColorOverride("font_color", GoldColor);
            AddChild(title);

            // Class cards container
            _classContainer = new HBoxContainer();
            _classContainer.Position = new Vector2(60, 140);
            _classContainer.Size = new Vector2(1800, 480);
            _classContainer.AddThemeConstantOverride("separation", 12);
            _classContainer.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(_classContainer);

            // Description panel
            var descPanel = new PanelContainer();
            descPanel.Position = new Vector2(160, 660);
            descPanel.Size = new Vector2(1600, 220);
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
            _selectedClassName.Text = StringLoader.Get("ui.characterCreation.selectPrompt");
            _selectedClassName.AddThemeFontSizeOverride("font_size", 32);
            _selectedClassName.AddThemeColorOverride("font_color", GoldColor);
            descVBox.AddChild(_selectedClassName);

            _descriptionLabel = new Label();
            _descriptionLabel.Text = "";
            _descriptionLabel.AddThemeFontSizeOverride("font_size", 18);
            _descriptionLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            descVBox.AddChild(_descriptionLabel);

            // Lore text
            _loreLabel = new Label();
            _loreLabel.Text = "";
            _loreLabel.AddThemeFontSizeOverride("font_size", 14);
            _loreLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.6f));
            _loreLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            descVBox.AddChild(_loreLabel);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 4);
            descVBox.AddChild(spacer);

            _statsGrid = new GridContainer();
            _statsGrid.Columns = 7;
            _statsGrid.AddThemeConstantOverride("h_separation", 30);
            _statsGrid.AddThemeConstantOverride("v_separation", 6);
            descVBox.AddChild(_statsGrid);

            // Button row
            var buttonRow = new HBoxContainer();
            buttonRow.Position = new Vector2(660, 910);
            buttonRow.Size = new Vector2(600, 60);
            buttonRow.AddThemeConstantOverride("separation", 40);
            buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(buttonRow);

            _backButton = new Button();
            _backButton.Text = StringLoader.Get("ui.characterCreation.backButton");
            _backButton.CustomMinimumSize = new Vector2(200, 50);
            _backButton.AddThemeFontSizeOverride("font_size", 22);
            _backButton.Pressed += () => GameManager.Instance?.ReturnToMainMenu();
            buttonRow.AddChild(_backButton);

            _startButton = new Button();
            _startButton.Text = StringLoader.Get("ui.characterCreation.startButton");
            _startButton.CustomMinimumSize = new Vector2(240, 50);
            _startButton.AddThemeFontSizeOverride("font_size", 22);
            _startButton.Disabled = true;
            _startButton.Pressed += HandleStartGame;
            buttonRow.AddChild(_startButton);
        }

        private void PopulateClassCards()
        {
            foreach (var kvp in BotFrameRegistry.Classes)
            {
                var classData = kvp.Value;
                var card = CreateClassCard(classData);
                _classContainer.AddChild(card);
                _classCards[kvp.Key] = card;
            }
        }

        private PanelContainer CreateClassCard(BotFrameData classData)
        {
            bool unlocked = MetaSaveManager.IsFrameUnlocked(classData.ClassName);

            var card = new PanelContainer();
            card.CustomMinimumSize = new Vector2(270, 460);

            var style = new StyleBoxFlat();
            style.BgColor = unlocked ? CardBg : LockedBg;
            style.BorderColor = unlocked ? DimGold : LockedBorder;
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
            nameLabel.Text = unlocked ? classData.DisplayName : "???";
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", 24);
            nameLabel.AddThemeColorOverride("font_color", unlocked ? GoldColor : LockedBorder);
            vbox.AddChild(nameLabel);

            var separator = new HSeparator();
            vbox.AddChild(separator);

            // 3D bot preview — taller viewport + pulled-back camera to frame full body + head
            var viewportContainer = new SubViewportContainer();
            viewportContainer.CustomMinimumSize = new Vector2(180, 200);
            viewportContainer.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            viewportContainer.StretchShrink = 1;
            viewportContainer.Stretch = true;
            vbox.AddChild(viewportContainer);

            var viewport = new SubViewport();
            viewport.Size = new Vector2I(180, 200);
            viewport.TransparentBg = true;
            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            viewport.OwnWorld3D = true;
            viewportContainer.AddChild(viewport);

            // Camera looking at the bot — pulled back and looking at center mass
            var camera = new Camera3D();
            camera.Position = new Vector3(0, 1.0f, 2.8f);
            camera.LookAtFromPosition(camera.Position, new Vector3(0, 0.6f, 0));
            camera.Fov = 28f;
            viewport.AddChild(camera);

            // Lighting
            var light = new DirectionalLight3D();
            light.RotationDegrees = new Vector3(-40, 30, 0);
            light.LightEnergy = unlocked ? 1.2f : 0.3f;
            viewport.AddChild(light);

            // Bot model (shown dimly even when locked — silhouette tease)
            var botModel = CharacterMeshBuilder.BuildPlayerBody(classData.ClassName);
            viewport.AddChild(botModel);

            var sep2 = new HSeparator();
            vbox.AddChild(sep2);

            if (unlocked)
            {
                var hpLabel = new Label();
                hpLabel.Text = StringLoader.Get("ui.characterCreation.hpLabel", ("{value}", classData.BaseStats.GetBaseStat(StatType.MaxHealth).ToString("F0")));
                hpLabel.AddThemeFontSizeOverride("font_size", 14);
                hpLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.6f));
                vbox.AddChild(hpLabel);

                var manaLabel = new Label();
                manaLabel.Text = StringLoader.Get("ui.characterCreation.manaLabel", ("{value}", classData.BaseStats.GetBaseStat(StatType.MaxMana).ToString("F0")));
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
            }
            else
            {
                // Locked overlay content
                var lockIcon = new Label();
                lockIcon.Text = "LOCKED";
                lockIcon.HorizontalAlignment = HorizontalAlignment.Center;
                lockIcon.AddThemeFontSizeOverride("font_size", 20);
                lockIcon.AddThemeColorOverride("font_color", new Color(0.5f, 0.3f, 0.3f));
                vbox.AddChild(lockIcon);

                var hintLabel = new Label();
                hintLabel.Text = classData.UnlockHint;
                hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
                hintLabel.AutowrapMode = TextServer.AutowrapMode.Word;
                hintLabel.AddThemeFontSizeOverride("font_size", 14);
                bool reqMet = MetaSaveManager.MeetsFrameRequirement(classData.ClassName);
                hintLabel.AddThemeColorOverride("font_color", reqMet ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.6f, 0.5f, 0.5f));
                vbox.AddChild(hintLabel);

                var spacer2 = new Control();
                spacer2.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(spacer2);

                if (classData.UnlockCost > 0)
                {
                    var unlockBtn = new Button();
                    bool canUnlock = MetaSaveManager.CanUnlockFrame(classData.ClassName);
                    unlockBtn.Text = $"Unlock ({classData.UnlockCost} Scrap)";
                    unlockBtn.CustomMinimumSize = new Vector2(200, 40);
                    unlockBtn.AddThemeFontSizeOverride("font_size", 16);
                    unlockBtn.Disabled = !canUnlock;
                    var frameType = classData.ClassName;
                    unlockBtn.Pressed += () =>
                    {
                        if (MetaSaveManager.UnlockFrame(frameType))
                            RebuildCards();
                    };
                    vbox.AddChild(unlockBtn);
                }
            }

            // Click handling
            card.GuiInput += (InputEvent ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    if (MetaSaveManager.IsFrameUnlocked(classData.ClassName))
                        SelectClass(classData.ClassName);
                }
            };

            if (unlocked)
            {
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
            }

            return card;
        }

        private void RebuildCards()
        {
            _selectedClass = null;
            _startButton.Disabled = true;
            _classCards.Clear();
            foreach (var child in _classContainer.GetChildren())
                child.QueueFree();
            // Defer so QueueFree completes first
            CallDeferred(nameof(PopulateClassCards));
        }

        private void SelectClass(BotFrameType className)
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
            var classData = BotFrameRegistry.GetClass(className);
            if (classData == null) return;

            _selectedClassName.Text = classData.DisplayName;
            _descriptionLabel.Text = classData.Description;
            _loreLabel.Text = classData.Lore;

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
