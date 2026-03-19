using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// All UI panel construction for the level editor.
    /// Left panel (file controls, minimap, tool bar, palette),
    /// Top bar (level info, theme, camera, undo/redo, test play),
    /// Right panel (inspector, validation).
    /// Code-built, no .tscn.
    /// </summary>
    public partial class LevelEditorUI : CanvasLayer
    {
        public LevelEditorScene Editor { get; set; }

        private const int LeftPanelWidth = 280;
        private const int RightPanelWidth = 280;
        private const int TopBarHeight = 40;

        // Left panel controls
        private LineEdit _nameEdit;
        private SpinBox _floorSpin;
        private Label _dimensionsLabel;
        private LevelEditorGrid2D _minimap;
        private VBoxContainer _paletteContainer;

        // Top bar
        private Label _levelNameLabel;
        private Button _themeToggle;
        private Button _scaleToggle;

        // Right panel
        private LevelEditorInspector _inspector;

        // Tool buttons
        private Button[] _toolButtons;
        private static readonly string[] ToolNames = { "Select", "Paint", "Height", "Asset", "Light", "FX", "Erase", "Texture", "Material" };
        private static readonly string[] ToolKeys = { "Q", "W", "E", "R", "T", "Y", "X", "C", "V" };

        public override void _Ready()
        {
            Layer = 10;

            BuildTopBar();
            BuildLeftPanel();
            BuildRightPanel();
        }

        private void BuildTopBar()
        {
            var bar = new PanelContainer();
            bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            bar.CustomMinimumSize = new Vector2(0, TopBarHeight);
            bar.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgDark, EditorStyles.Border));

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bar.AddChild(hbox);

            // Left spacer for left panel
            var spacerL = new Control();
            spacerL.CustomMinimumSize = new Vector2(LeftPanelWidth, 0);
            hbox.AddChild(spacerL);

            // Level name
            _levelNameLabel = EditorStyles.MakeLabel("Untitled", 16, EditorStyles.TextPrimary);
            hbox.AddChild(_levelNameLabel);

            // Theme toggle
            _themeToggle = EditorStyles.MakeButton("Tron", 12, EditorStyles.AccentSignals);
            _themeToggle.CustomMinimumSize = new Vector2(80, 0);
            _themeToggle.Pressed += OnThemeToggle;
            hbox.AddChild(_themeToggle);

            // Camera rotation controls
            var camBox = new HBoxContainer();
            camBox.AddThemeConstantOverride("separation", 2);

            var rotLBtn = EditorStyles.MakeButton("<", 12);
            rotLBtn.TooltipText = "Rotate Left (Numpad 4)";
            rotLBtn.CustomMinimumSize = new Vector2(28, 0);
            rotLBtn.Pressed += () => Editor?.Camera?.RotateYaw(45f);
            camBox.AddChild(rotLBtn);

            var rotRBtn = EditorStyles.MakeButton(">", 12);
            rotRBtn.TooltipText = "Rotate Right (Numpad 6)";
            rotRBtn.CustomMinimumSize = new Vector2(28, 0);
            rotRBtn.Pressed += () => Editor?.Camera?.RotateYaw(-45f);
            camBox.AddChild(rotRBtn);

            var topBtn = EditorStyles.MakeButton("Top", 10);
            topBtn.TooltipText = "Top View (Numpad 5)";
            topBtn.CustomMinimumSize = new Vector2(36, 0);
            topBtn.Pressed += () => Editor?.Camera?.SetViewPreset("Top");
            camBox.AddChild(topBtn);

            var isoBtn = EditorStyles.MakeButton("Iso", 10);
            isoBtn.TooltipText = "Isometric View (Numpad 7)";
            isoBtn.CustomMinimumSize = new Vector2(36, 0);
            isoBtn.Pressed += () => Editor?.Camera?.SetViewPreset("Iso");
            camBox.AddChild(isoBtn);

            var frontBtn = EditorStyles.MakeButton("Front", 10);
            frontBtn.TooltipText = "Front View (Numpad 1)";
            frontBtn.CustomMinimumSize = new Vector2(42, 0);
            frontBtn.Pressed += () => Editor?.Camera?.SetViewPreset("Front");
            camBox.AddChild(frontBtn);

            hbox.AddChild(camBox);

            // Scale refs toggle
            _scaleToggle = EditorStyles.MakeButton("Scale", 11);
            _scaleToggle.TooltipText = "Toggle scale reference objects";
            _scaleToggle.CustomMinimumSize = new Vector2(50, 0);
            _scaleToggle.Pressed += OnScaleToggle;
            hbox.AddChild(_scaleToggle);

            // Undo/Redo
            var undoBtn = EditorStyles.MakeButton("Undo", 12);
            undoBtn.Pressed += () => Editor?.Undo();
            hbox.AddChild(undoBtn);

            var redoBtn = EditorStyles.MakeButton("Redo", 12);
            redoBtn.Pressed += () => Editor?.Redo();
            hbox.AddChild(redoBtn);

            // Spacer
            var spacerM = new Control();
            spacerM.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(spacerM);

            // Test Play
            var testBtn = EditorStyles.MakeButton("Test Play", 13, EditorStyles.AccentNodes);
            testBtn.CustomMinimumSize = new Vector2(100, 0);
            testBtn.Pressed += () => Editor?.TestPlay();
            hbox.AddChild(testBtn);

            // Export
            var exportBtn = EditorStyles.MakeButton("Export All", 12);
            exportBtn.Pressed += () => Editor?.ExportHardcodedFloors();
            hbox.AddChild(exportBtn);

            // Back
            var backBtn = EditorStyles.MakeButton("Menu", 12, EditorStyles.StatusError);
            backBtn.Pressed += () => Editor?.ReturnToMenu();
            hbox.AddChild(backBtn);

            // Right spacer
            var spacerR = new Control();
            spacerR.CustomMinimumSize = new Vector2(RightPanelWidth, 0);
            hbox.AddChild(spacerR);

            AddChild(bar);
        }

        private void BuildLeftPanel()
        {
            // Use a plain Control with explicit anchors so ScrollContainer clips properly
            var panel = new Control();
            panel.AnchorLeft = 0;
            panel.AnchorRight = 0;
            panel.AnchorTop = 0;
            panel.AnchorBottom = 1;
            panel.OffsetRight = LeftPanelWidth;
            panel.OffsetTop = TopBarHeight;

            // Background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = EditorStyles.BgDark;
            panel.AddChild(bg);

            var scroll = new ScrollContainer();
            scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            scroll.OffsetLeft = 6;
            scroll.OffsetRight = -6;
            scroll.OffsetTop = 4;
            scroll.OffsetBottom = -4;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            panel.AddChild(scroll);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(vbox);

            // ── File Controls ──
            vbox.AddChild(EditorStyles.MakeLabel("FILE", 12, EditorStyles.TextSecondary));
            var fileRow = new HBoxContainer();
            fileRow.AddThemeConstantOverride("separation", 4);

            var newBtn = EditorStyles.MakeButton("New", 11);
            newBtn.Pressed += () => Editor?.NewLevel();
            fileRow.AddChild(newBtn);

            var loadBtn = EditorStyles.MakeButton("Load", 11);
            loadBtn.Pressed += OnLoadPressed;
            fileRow.AddChild(loadBtn);

            var saveBtn = EditorStyles.MakeButton("Save", 11, EditorStyles.AccentNodes);
            saveBtn.Pressed += () => Editor?.SaveLevel();
            fileRow.AddChild(saveBtn);

            vbox.AddChild(fileRow);

            // ── Load Existing Floors ──
            vbox.AddChild(EditorStyles.MakeLabel("EXISTING LEVELS", 12, EditorStyles.TextSecondary));
            var floorRow = new HBoxContainer();
            floorRow.AddThemeConstantOverride("separation", 4);

            for (int f = 1; f <= 3; f++)
            {
                int floor = f;
                string[] floorNames = { "", "Gateway", "Conduit", "Arena" };
                var floorBtn = EditorStyles.MakeButton($"F{floor}: {floorNames[floor]}", 11, EditorStyles.AccentWaves);
                floorBtn.CustomMinimumSize = new Vector2(0, 26);
                floorBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                floorBtn.Pressed += () => Editor?.LoadHardcodedFloor(floor);
                floorRow.AddChild(floorBtn);
            }
            vbox.AddChild(floorRow);

            // Crossroads (bonus layout)
            var crossBtn = EditorStyles.MakeButton("Crossroads (test)", 10, EditorStyles.TextMuted);
            crossBtn.CustomMinimumSize = new Vector2(0, 22);
            crossBtn.Pressed += () => Editor?.LoadHardcodedFloor(4);
            vbox.AddChild(crossBtn);

            // ── Level Info ──
            vbox.AddChild(EditorStyles.MakeSeparator());
            vbox.AddChild(EditorStyles.MakeLabel("LEVEL INFO", 12, EditorStyles.TextSecondary));

            var nameRow = new HBoxContainer();
            nameRow.AddChild(EditorStyles.MakeLabel("Name:", 12, EditorStyles.TextMuted));
            _nameEdit = EditorStyles.MakeLineEdit("Level Name");
            _nameEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _nameEdit.TextChanged += t =>
            {
                if (Editor?.CurrentLevel != null)
                {
                    Editor.CurrentLevel.Name = t;
                    _levelNameLabel.Text = t;
                }
            };
            nameRow.AddChild(_nameEdit);
            vbox.AddChild(nameRow);

            var floorInfoRow = new HBoxContainer();
            floorInfoRow.AddChild(EditorStyles.MakeLabel("Floor:", 12, EditorStyles.TextMuted));
            _floorSpin = EditorStyles.MakeSpinBox(1, 1, 10, 1);
            _floorSpin.ValueChanged += v =>
            {
                if (Editor?.CurrentLevel != null)
                    Editor.CurrentLevel.Floor = (int)v;
            };
            floorInfoRow.AddChild(_floorSpin);
            vbox.AddChild(floorInfoRow);

            _dimensionsLabel = EditorStyles.MakeLabel($"{Constants.VINE_MAP_WIDTH} x {Constants.VINE_MAP_HEIGHT}", 12);
            vbox.AddChild(_dimensionsLabel);

            // ── 2D Minimap ──
            vbox.AddChild(EditorStyles.MakeSeparator());
            vbox.AddChild(EditorStyles.MakeLabel("MINIMAP", 12, EditorStyles.TextSecondary));
            _minimap = new LevelEditorGrid2D();
            _minimap.Editor = Editor;
            vbox.AddChild(_minimap);

            // ── Tool Bar ──
            vbox.AddChild(EditorStyles.MakeSeparator());
            vbox.AddChild(EditorStyles.MakeLabel("TOOLS", 12, EditorStyles.TextSecondary));

            var toolGrid = new GridContainer();
            toolGrid.Columns = 4;
            toolGrid.AddThemeConstantOverride("h_separation", 4);
            toolGrid.AddThemeConstantOverride("v_separation", 4);

            _toolButtons = new Button[ToolNames.Length];
            for (int i = 0; i < ToolNames.Length; i++)
            {
                int idx = i;
                var btn = EditorStyles.MakeButton($"{ToolKeys[i]}: {ToolNames[i]}", 10);
                btn.CustomMinimumSize = new Vector2(60, 28);
                btn.Pressed += () => OnToolSelected(idx);
                toolGrid.AddChild(btn);
                _toolButtons[i] = btn;
            }
            vbox.AddChild(toolGrid);

            // ── Palette (context-sensitive) ──
            vbox.AddChild(EditorStyles.MakeSeparator());
            vbox.AddChild(EditorStyles.MakeLabel("PALETTE", 12, EditorStyles.TextSecondary));
            _paletteContainer = new VBoxContainer();
            _paletteContainer.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(_paletteContainer);

            LevelEditorPalette.BuildCellPalette(_paletteContainer, Editor);

            AddChild(panel);
        }

        private void BuildRightPanel()
        {
            var panel = new PanelContainer();
            panel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
            panel.CustomMinimumSize = new Vector2(RightPanelWidth, 0);
            panel.OffsetTop = TopBarHeight;
            panel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgDark, EditorStyles.Border));

            var scroll = new ScrollContainer();
            scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            panel.AddChild(scroll);

            _inspector = new LevelEditorInspector();
            _inspector.Editor = Editor;
            _inspector.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(_inspector);

            AddChild(panel);
        }

        // ── Tool Selection ──

        private void OnToolSelected(int index)
        {
            var mode = (EditorToolMode)index;
            var tools = Editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
            tools?.SetMode(mode);

            // Update palette
            foreach (var c in _paletteContainer.GetChildren()) c.QueueFree();
            switch (mode)
            {
                case EditorToolMode.CellPaint: LevelEditorPalette.BuildCellPalette(_paletteContainer, Editor); break;
                case EditorToolMode.HeightPaint: LevelEditorPalette.BuildHeightPalette(_paletteContainer, Editor); break;
                case EditorToolMode.AssetPlace: LevelEditorPalette.BuildAssetPalette(_paletteContainer, Editor); break;
                case EditorToolMode.LightPlace: LevelEditorPalette.BuildLightPalette(_paletteContainer, Editor); break;
                case EditorToolMode.FXPlace: LevelEditorPalette.BuildFXPalette(_paletteContainer, Editor); break;
                case EditorToolMode.TexturePaint: LevelEditorPalette.BuildTexturePalette(_paletteContainer, Editor); break;
                case EditorToolMode.MaterialPaint: LevelEditorPalette.BuildMaterialPaintPalette(_paletteContainer, Editor); break;
            }

            // Highlight active tool button
            for (int i = 0; i < _toolButtons.Length; i++)
            {
                _toolButtons[i].AddThemeColorOverride("font_color",
                    i == index ? EditorStyles.AccentNodes : EditorStyles.TextPrimary);
            }
        }

        // Palettes are now in LevelEditorPalette.cs

        // ── Events ──

        private void OnThemeToggle()
        {
            if (Editor?.CurrentLevel == null) return;
            var current = Editor.CurrentLevel.PlanetTheme;
            var next = current == "tron" ? "scrapyard" : "tron";
            Editor.SetPlanetTheme(next);
            _themeToggle.Text = next == "tron" ? "Tron" : "Scrapyard";
        }

        private void OnScaleToggle()
        {
            Editor?.ToggleScaleReferences();
            bool visible = Editor?.ScaleRefsVisible ?? false;
            _scaleToggle.AddThemeColorOverride("font_color",
                visible ? EditorStyles.AccentNodes : EditorStyles.TextPrimary);
        }

        private void OnLoadPressed()
        {
            var files = LevelSerializer.ListLevelFiles();
            if (files.Length == 0)
            {
                GD.Print("[LevelEditor] No level files found. Use 'Export All' or load a hardcoded floor.");
                return;
            }

            // Create a popup menu with available files
            var popup = new PopupMenu();
            for (int i = 0; i < files.Length; i++)
                popup.AddItem(files[i], i);

            popup.IdPressed += id =>
            {
                Editor?.LoadLevel(files[(int)id]);
                popup.QueueFree();
            };

            AddChild(popup);
            popup.Popup(new Rect2I(200, 60, 200, 0));
        }

        // ── Refresh ──

        public void RefreshAll()
        {
            if (Editor?.CurrentLevel == null) return;
            var data = Editor.CurrentLevel;

            if (_nameEdit != null) _nameEdit.Text = data.Name;
            _floorSpin?.SetValueNoSignal(data.Floor);
            _levelNameLabel.Text = data.Name;
            _themeToggle.Text = data.PlanetTheme == "tron" ? "Tron" : "Scrapyard";
            _minimap?.QueueRedraw();
        }
    }
}
