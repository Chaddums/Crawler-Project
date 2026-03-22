using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// Full-screen loadout management screen with two views:
    /// 1. Selection view — 6 loadout cards in a grid
    /// 2. Detail view — renamable name, save button, 2x2 tower grid, 3 relic equip slots
    /// Inventory (right) and Relics (bottom) panels persist across both views.
    /// Drag from inventory/relics onto detail slots to equip.
    /// </summary>
    public partial class LoadoutsScreen : CanvasLayer
    {
        private const int LOADOUT_COUNT = 6;
        private const float SLOT_SIZE = 231f; // 210 * 1.1
        private const int TOWER_SLOT_COUNT = 4;
        private const int RELIC_SLOT_COUNT = 3;

        private static readonly Color Accent = new(0.9f, 0.7f, 0.3f);
        private static readonly Color SlotBg = new(0.05f, 0.04f, 0.03f, 0.9f);
        private static readonly Color SlotBorder = new(0.4f, 0.32f, 0.14f);
        private static readonly Color ModColor = new(0.6f, 0.5f, 0.9f);
        private static readonly Color RelicColor = new(0.3f, 0.8f, 0.6f);
        private static readonly Color EmptyText = new(0.35f, 0.35f, 0.35f);
        private static readonly Color TowerBaseColor = new(0.9f, 0.6f, 0.2f);
        private static readonly Color FuncBaseColor = new(0.4f, 0.7f, 1.0f);

        // ── Drag type constants ──
        private const string DRAG_TOWER_BASE = "tower_base";
        private const string DRAG_FUNC_BASE = "func_base";
        private const string DRAG_MODIFIER = "modifier";
        private const string DRAG_RELIC = "relic";

        // ── Temp Inventory Data ──

        private struct ItemData
        {
            public string Name;
            public string Desc;
            public ItemData(string name, string desc) { Name = name; Desc = desc; }
        }

        private static readonly ItemData[] TowerBases = new[]
        {
            new ItemData("Blaster",   "Fires rapid energy bolts in a narrow cone"),
            new ItemData("Arc",       "Chains lightning between nearby targets"),
            new ItemData("Spiker",    "Launches piercing projectiles through lines"),
            new ItemData("Wave",      "Emits pulsing shockwaves in all directions"),
            new ItemData("Force",     "Concentrated kinetic beam, single target"),
            new ItemData("Radiance",  "Slow-burning area denial field"),
        };

        private static readonly ItemData[] FuncBases = new[]
        {
            new ItemData("Merge",     "Combines two incoming signals into one"),
            new ItemData("Amp",       "Boosts signal strength by 40%"),
            new ItemData("Scramble",  "Randomizes enemy pathing on signal"),
            new ItemData("Focus",     "Narrows effect range, doubles intensity"),
            new ItemData("Mimic",     "Copies the last signal that passed through"),
            new ItemData("Reflect",   "Bounces signal back toward its source"),
        };

        private static readonly ItemData[] Modifiers = new[]
        {
            new ItemData("Target Lock",       "Prioritizes highest-HP enemy in range"),
            new ItemData("Transcapacitor",    "Stores excess signal energy between waves"),
            new ItemData("Meta-Skeleton",     "Reduces base weight, +20% rotation speed"),
            new ItemData("Triage Plates",     "Auto-repairs adjacent nodes when idle"),
            new ItemData("Solid Propellant",  "Projectiles travel 35% faster"),
            new ItemData("Surge Conductors",  "Signal chains deal +10% per hop"),
        };

        private struct RelicData
        {
            public string Name;
            public string Desc;
            public Color Tint;
            public RelicData(string name, string desc, Color tint) { Name = name; Desc = desc; Tint = tint; }
        }

        private static readonly RelicData[] AllRelics = new[]
        {
            new RelicData("Null Shard",        "Negates the first hit each wave",               new Color(0.6f, 0.2f, 0.8f)),
            new RelicData("Hex Capacitor",     "+15% signal travel speed",                      new Color(0.2f, 0.8f, 0.4f)),
            new RelicData("Phantom Register",  "Towers fire once at ghosts that aren't there",  new Color(0.8f, 0.3f, 0.5f)),
            new RelicData("Aether Coil",       "Passive regen: 2 HP/sec to all nodes",          new Color(0.3f, 0.6f, 0.9f)),
            new RelicData("Entropic Lens",     "Critical hits deal 3x instead of 2x",           new Color(0.9f, 0.4f, 0.1f)),
            new RelicData("Runic Transistor",  "Routing nodes gain +1 signal power",            new Color(0.4f, 0.9f, 0.7f)),
            new RelicData("Void Beacon",       "Reveals cloaked enemies within 12 range",       new Color(0.5f, 0.1f, 0.7f)),
            new RelicData("Flux Mandala",      "Slow fields also reduce armor by 2",            new Color(0.9f, 0.8f, 0.2f)),
            new RelicData("Quantum Splicer",   "10% chance to duplicate any placed node",       new Color(0.1f, 0.7f, 0.8f)),
        };

        // ── Loadout Data ──

        private string[] _loadoutNames;
        private LoadoutData[] _loadouts;
        private int _editingIndex = -1;
        private LoadoutData _editSnapshot;
        private string _editNameSnapshot;

        // UI references
        private Control _selectionView;
        private Control _detailView;
        private Label _detailTitle;
        private Button _backBtn;

        // Card references for dynamic updates
        private LineEdit[] _cardNameEdits = new LineEdit[LOADOUT_COUNT];
        private Control[] _cardContentAreas = new Control[LOADOUT_COUNT];

        private struct LoadoutData
        {
            public string[] TowerBaseNames;   // [4]
            public string[] FuncBaseNames;    // [4]
            public string[] TowerModNames;    // [4]
            public string[] FuncModNames;     // [4]
            public string[] RelicNames;       // [3]

            public static LoadoutData Empty()
            {
                return new LoadoutData
                {
                    TowerBaseNames = new string[TOWER_SLOT_COUNT],
                    FuncBaseNames = new string[TOWER_SLOT_COUNT],
                    TowerModNames = new string[TOWER_SLOT_COUNT],
                    FuncModNames = new string[TOWER_SLOT_COUNT],
                    RelicNames = new string[RELIC_SLOT_COUNT]
                };
            }

            public LoadoutData Clone()
            {
                return new LoadoutData
                {
                    TowerBaseNames = (string[])TowerBaseNames.Clone(),
                    FuncBaseNames = (string[])FuncBaseNames.Clone(),
                    TowerModNames = (string[])TowerModNames.Clone(),
                    FuncModNames = (string[])FuncModNames.Clone(),
                    RelicNames = (string[])RelicNames.Clone()
                };
            }
        }

        // ════════════════════════════════════════
        // DRAG/DROP INNER CLASSES
        // ════════════════════════════════════════

        /// <summary>Inventory/relic item that can be dragged onto a DropSlot.</summary>
        private partial class DragItem : PanelContainer
        {
            private string _dragType;
            private string _dragName;
            private Color _dragColor;

            private string _dragDesc;

            public void SetDragInfo(string type, string name, Color color, string desc = "")
            {
                _dragType = type;
                _dragName = name;
                _dragColor = color;
                _dragDesc = desc;
            }

            public override Variant _GetDragData(Vector2 atPosition)
            {
                var dict = new Dictionary();
                dict["type"] = _dragType;
                dict["name"] = _dragName;
                dict["color_r"] = _dragColor.R;
                dict["color_g"] = _dragColor.G;
                dict["color_b"] = _dragColor.B;
                dict["desc"] = _dragDesc ?? "";

                var preview = new PanelContainer();
                var style = new StyleBoxFlat();
                style.BgColor = new Color(_dragColor.R * 0.15f, _dragColor.G * 0.15f, _dragColor.B * 0.15f, 0.95f);
                style.BorderColor = _dragColor;
                style.SetBorderWidthAll(1);
                style.SetCornerRadiusAll(4);
                style.ContentMarginLeft = 10;
                style.ContentMarginRight = 10;
                style.ContentMarginTop = 5;
                style.ContentMarginBottom = 5;
                preview.AddThemeStyleboxOverride("panel", style);

                var label = new Label();
                label.Text = _dragName;
                label.AddThemeFontSizeOverride("font_size", 14);
                label.AddThemeColorOverride("font_color", _dragColor);
                preview.AddChild(label);

                SetDragPreview(preview);
                return dict;
            }
        }

        /// <summary>Loadout slot that accepts drops of a specific drag type.</summary>
        private partial class DropSlot : PanelContainer
        {
            private string _acceptType;
            private System.Action<string> _onItemChanged;
            private Label _contentLabel;
            private Color _accentColor;
            private string _emptyText;
            private StyleBoxFlat _normalStyle;
            private StyleBoxFlat _highlightStyle;

            public void Setup(string acceptType, string emptyText, Color accent,
                              float width, float height, System.Action<string> onItemChanged)
            {
                _acceptType = acceptType;
                _emptyText = emptyText;
                _accentColor = accent;
                _onItemChanged = onItemChanged;
                CustomMinimumSize = new Vector2(width, height);

                _normalStyle = new StyleBoxFlat();
                _normalStyle.BgColor = SlotBg;
                _normalStyle.BorderColor = new Color(accent.R * 0.4f, accent.G * 0.4f, accent.B * 0.4f);
                _normalStyle.SetBorderWidthAll(2);
                _normalStyle.SetCornerRadiusAll(6);
                _normalStyle.ContentMarginLeft = 6;
                _normalStyle.ContentMarginRight = 6;
                _normalStyle.ContentMarginTop = 6;
                _normalStyle.ContentMarginBottom = 6;
                AddThemeStyleboxOverride("panel", _normalStyle);

                _highlightStyle = (StyleBoxFlat)_normalStyle.Duplicate();
                _highlightStyle.BorderColor = accent;
                _highlightStyle.BgColor = new Color(accent.R * 0.1f, accent.G * 0.1f, accent.B * 0.1f, 0.9f);

                var center = new CenterContainer();
                center.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                center.SizeFlagsVertical = SizeFlags.ExpandFill;
                AddChild(center);

                _contentLabel = new Label();
                _contentLabel.Text = emptyText;
                _contentLabel.HorizontalAlignment = HorizontalAlignment.Center;
                _contentLabel.AddThemeFontSizeOverride("font_size", 13);
                _contentLabel.AddThemeColorOverride("font_color", EmptyText);
                center.AddChild(_contentLabel);
            }

            public void SetItem(string name, Color? color = null, string tooltip = null)
            {
                if (string.IsNullOrEmpty(name))
                {
                    _contentLabel.Text = _emptyText;
                    _contentLabel.AddThemeColorOverride("font_color", EmptyText);
                    TooltipText = "";
                    // Reset border to default
                    _normalStyle.BorderColor = new Color(_accentColor.R * 0.4f, _accentColor.G * 0.4f, _accentColor.B * 0.4f);
                    _highlightStyle.BorderColor = _accentColor;
                    _highlightStyle.BgColor = new Color(_accentColor.R * 0.1f, _accentColor.G * 0.1f, _accentColor.B * 0.1f, 0.9f);
                    AddThemeStyleboxOverride("panel", _normalStyle);
                }
                else
                {
                    var c = color ?? _accentColor;
                    _contentLabel.Text = name;
                    _contentLabel.AddThemeColorOverride("font_color", c);
                    TooltipText = tooltip ?? "";
                    // Update border to match item color
                    _normalStyle.BorderColor = new Color(c.R * 0.5f, c.G * 0.5f, c.B * 0.5f);
                    _highlightStyle.BorderColor = c;
                    _highlightStyle.BgColor = new Color(c.R * 0.1f, c.G * 0.1f, c.B * 0.1f, 0.9f);
                    AddThemeStyleboxOverride("panel", _normalStyle);
                }
            }

            public override bool _CanDropData(Vector2 atPosition, Variant data)
            {
                if (data.VariantType != Variant.Type.Dictionary) return false;
                var dict = data.AsGodotDictionary();
                bool valid = dict.ContainsKey("type") && dict["type"].AsString() == _acceptType;
                AddThemeStyleboxOverride("panel", valid ? _highlightStyle : _normalStyle);
                return valid;
            }

            public override void _DropData(Vector2 atPosition, Variant data)
            {
                AddThemeStyleboxOverride("panel", _normalStyle);
                var dict = data.AsGodotDictionary();
                var name = dict["name"].AsString();
                Color? dragColor = null;
                if (dict.ContainsKey("color_r"))
                    dragColor = new Color(dict["color_r"].AsSingle(), dict["color_g"].AsSingle(), dict["color_b"].AsSingle());
                string desc = dict.ContainsKey("desc") ? dict["desc"].AsString() : "";
                SetItem(name, dragColor, desc);
                _onItemChanged?.Invoke(name);
            }

            public override void _Notification(int what)
            {
                if (what == NotificationDragEnd)
                    AddThemeStyleboxOverride("panel", _normalStyle);
            }

            public override void _GuiInput(InputEvent @event)
            {
                // Right-click to clear
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
                {
                    SetItem(null);
                    _onItemChanged?.Invoke(null);
                    AcceptEvent();
                }
            }
        }

        // ════════════════════════════════════════
        // LIFECYCLE
        // ════════════════════════════════════════

        public override void _Ready()
        {
            Layer = 10;

            // Instant tooltips while on this screen
            GetTree().Root.GuiEmbedSubwindows = true;
            ProjectSettings.SetSetting("gui/timers/tooltip_delay_sec", 0.0f);

            // Load saved loadouts from disk
            var saveData = LoadoutSave.Load();
            _loadoutNames = new string[LOADOUT_COUNT];
            _loadouts = new LoadoutData[LOADOUT_COUNT];
            for (int i = 0; i < LOADOUT_COUNT; i++)
            {
                var saved = saveData.Loadouts[i];
                _loadoutNames[i] = saved.Name ?? $"Loadout {i + 1}";
                _loadouts[i] = new LoadoutData
                {
                    TowerBaseNames = saved.TowerBaseNames ?? new string[TOWER_SLOT_COUNT],
                    FuncBaseNames = saved.FuncBaseNames ?? new string[TOWER_SLOT_COUNT],
                    TowerModNames = saved.TowerModNames ?? new string[TOWER_SLOT_COUNT],
                    FuncModNames = saved.FuncModNames ?? new string[TOWER_SLOT_COUNT],
                    RelicNames = saved.RelicNames ?? new string[RELIC_SLOT_COUNT],
                };
            }

            BuildUI();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
            {
                GetViewport().SetInputAsHandled();
                if (_editingIndex >= 0)
                    ShowSelectionView();
                else
                    GameManager.Instance?.ReturnToMainMenu();
            }
        }

        // ── View Switching ──

        private void ShowSelectionView()
        {
            if (_editingIndex >= 0)
            {
                // Discard unsaved changes
                int idx = _editingIndex;
                _loadouts[idx] = _editSnapshot;
                _loadoutNames[idx] = _editNameSnapshot;
                _editingIndex = -1;
                RefreshCard(idx);
            }
            else
            {
                _editingIndex = -1;
            }
            _selectionView.Visible = true;
            _detailView.Visible = false;
            _detailTitle.Text = "LOADOUTS";
            _backBtn.Pressed -= OnBackPressed;
            _backBtn.Pressed += OnBackToMenu;
        }

        private void RefreshCard(int index)
        {
            _cardNameEdits[index].Text = _loadoutNames[index];
            RefreshCardContent(index);
        }

        private void RefreshCardContent(int index)
        {
            var area = _cardContentAreas[index];
            foreach (var child in area.GetChildren())
                child.QueueFree();

            var data = _loadouts[index];
            bool hasAnything = false;

            // Show tower bases
            for (int i = 0; i < TOWER_SLOT_COUNT; i++)
            {
                if (!string.IsNullOrEmpty(data.TowerBaseNames[i]))
                {
                    var label = new Label();
                    label.Text = data.TowerBaseNames[i];
                    label.AddThemeFontSizeOverride("font_size", 12);
                    label.AddThemeColorOverride("font_color", TowerBaseColor);
                    area.AddChild(label);
                    hasAnything = true;
                }
            }

            // Show function bases
            for (int i = 0; i < TOWER_SLOT_COUNT; i++)
            {
                if (!string.IsNullOrEmpty(data.FuncBaseNames[i]))
                {
                    var label = new Label();
                    label.Text = data.FuncBaseNames[i];
                    label.AddThemeFontSizeOverride("font_size", 12);
                    label.AddThemeColorOverride("font_color", FuncBaseColor);
                    area.AddChild(label);
                    hasAnything = true;
                }
            }

            if (!hasAnything)
            {
                var center = new CenterContainer();
                center.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                area.AddChild(center);

                var emptyLabel = new Label();
                emptyLabel.Text = "Empty";
                emptyLabel.AddThemeFontSizeOverride("font_size", 15);
                emptyLabel.AddThemeColorOverride("font_color", EmptyText);
                center.AddChild(emptyLabel);
            }
        }

        private void ShowDetailView(int index)
        {
            _editingIndex = index;
            _editSnapshot = _loadouts[index].Clone();
            _editNameSnapshot = _loadoutNames[index];
            _selectionView.Visible = false;
            _detailView.Visible = true;
            _detailTitle.Text = _loadoutNames[index].ToUpper();
            _backBtn.Pressed -= OnBackToMenu;
            _backBtn.Pressed += OnBackPressed;
            RebuildDetailContent();
        }

        private void OnBackToMenu() => GameManager.Instance?.ReturnToMainMenu();
        private void OnBackPressed() => ShowSelectionView();

        // ════════════════════════════════════════
        // UI BUILDING
        // ════════════════════════════════════════

        private void BuildUI()
        {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = TronTheme.Background;
            AddChild(bg);

            var root = new MarginContainer();
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddThemeConstantOverride("margin_left", 24);
            root.AddThemeConstantOverride("margin_right", 24);
            root.AddThemeConstantOverride("margin_top", 16);
            root.AddThemeConstantOverride("margin_bottom", 16);
            AddChild(root);

            var mainVBox = new VBoxContainer();
            mainVBox.AddThemeConstantOverride("separation", 12);
            root.AddChild(mainVBox);

            BuildTopBar(mainVBox);

            // Content area: swappable left + persistent inventory right
            var contentHBox = new HBoxContainer();
            contentHBox.AddThemeConstantOverride("separation", 16);
            contentHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVBox.AddChild(contentHBox);

            // Selection view (loadout grid)
            _selectionView = new VBoxContainer();
            ((VBoxContainer)_selectionView).AddThemeConstantOverride("separation", 12);
            _selectionView.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _selectionView.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            contentHBox.AddChild(_selectionView);
            BuildLoadoutsPanel(_selectionView);

            // Detail view (tower editor) — hidden initially
            _detailView = new VBoxContainer();
            ((VBoxContainer)_detailView).AddThemeConstantOverride("separation", 12);
            _detailView.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _detailView.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _detailView.Visible = false;
            contentHBox.AddChild(_detailView);

            // Persistent inventory panel on right
            BuildInventoryPanel(contentHBox);

            // Persistent relic panel on bottom
            BuildRelicPanel(mainVBox);
        }

        private void BuildTopBar(VBoxContainer parent)
        {
            var topBar = new HBoxContainer();
            topBar.AddThemeConstantOverride("separation", 16);
            parent.AddChild(topBar);

            _backBtn = MakeStyledButton("< Back", Accent);
            _backBtn.CustomMinimumSize = new Vector2(100, 40);
            _backBtn.Pressed += OnBackToMenu;
            topBar.AddChild(_backBtn);

            var spacerL = new Control();
            spacerL.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            topBar.AddChild(spacerL);

            _detailTitle = new Label();
            _detailTitle.Text = "LOADOUTS";
            _detailTitle.AddThemeFontSizeOverride("font_size", 32);
            _detailTitle.AddThemeColorOverride("font_color", Accent);
            topBar.AddChild(_detailTitle);

            var spacerR = new Control();
            spacerR.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            topBar.AddChild(spacerR);

            var balance = new Control();
            balance.CustomMinimumSize = new Vector2(100, 0);
            topBar.AddChild(balance);
        }

        // ════════════════════════════════════════
        // SELECTION VIEW
        // ════════════════════════════════════════

        private void BuildLoadoutsPanel(Control parent)
        {
            var centerPanel = new PanelContainer();
            centerPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            centerPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            centerPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(0.6f));
            parent.AddChild(centerPanel);

            var centerVBox = new VBoxContainer();
            centerVBox.AddThemeConstantOverride("separation", 12);
            centerPanel.AddChild(centerVBox);

            var sectionLabel = new Label();
            sectionLabel.Text = "Select a Loadout";
            sectionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            sectionLabel.AddThemeFontSizeOverride("font_size", 18);
            sectionLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.5f));
            centerVBox.AddChild(sectionLabel);

            var gridCenter = new CenterContainer();
            gridCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            gridCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            centerVBox.AddChild(gridCenter);

            var grid = new GridContainer();
            grid.Columns = 3;
            grid.AddThemeConstantOverride("h_separation", 20);
            grid.AddThemeConstantOverride("v_separation", 20);
            gridCenter.AddChild(grid);

            for (int i = 0; i < LOADOUT_COUNT; i++)
                BuildLoadoutCard(grid, i);
        }

        private void BuildLoadoutCard(GridContainer parent, int index)
        {
            var card = new PanelContainer();
            card.CustomMinimumSize = new Vector2(SLOT_SIZE, SLOT_SIZE + 40);
            var cardStyle = MakeCardStyle(false);
            card.AddThemeStyleboxOverride("panel", cardStyle);
            parent.AddChild(card);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            card.AddChild(vbox);

            var nameEdit = new LineEdit();
            nameEdit.Text = _loadoutNames[index];
            nameEdit.Alignment = HorizontalAlignment.Center;
            nameEdit.AddThemeFontSizeOverride("font_size", 16);
            nameEdit.AddThemeColorOverride("font_color", Accent);

            var editNormal = new StyleBoxFlat();
            editNormal.BgColor = Colors.Transparent;
            editNormal.BorderColor = Colors.Transparent;
            editNormal.SetBorderWidthAll(0);
            editNormal.SetCornerRadiusAll(3);
            editNormal.ContentMarginLeft = 4;
            editNormal.ContentMarginRight = 4;
            nameEdit.AddThemeStyleboxOverride("normal", editNormal);

            var editFocus = new StyleBoxFlat();
            editFocus.BgColor = new Color(Accent.R * 0.1f, Accent.G * 0.1f, Accent.B * 0.1f, 0.8f);
            editFocus.BorderColor = Accent;
            editFocus.SetBorderWidthAll(1);
            editFocus.SetCornerRadiusAll(3);
            editFocus.ContentMarginLeft = 4;
            editFocus.ContentMarginRight = 4;
            nameEdit.AddThemeStyleboxOverride("focus", editFocus);

            int idx = index;
            nameEdit.TextSubmitted += (text) => { _loadoutNames[idx] = text; nameEdit.ReleaseFocus(); };
            nameEdit.FocusExited += () => { _loadoutNames[idx] = nameEdit.Text; };
            vbox.AddChild(nameEdit);
            _cardNameEdits[index] = nameEdit;

            // Dynamic content area — will show equipped items or "Empty"
            var contentArea = new VBoxContainer();
            ((VBoxContainer)contentArea).AddThemeConstantOverride("separation", 2);
            contentArea.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            vbox.AddChild(contentArea);
            _cardContentAreas[index] = contentArea;

            RefreshCardContent(index);

            var hint = new Label();
            hint.Text = "Click to edit";
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.AddThemeFontSizeOverride("font_size", 12);
            hint.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.3f));
            vbox.AddChild(hint);

            var clickArea = new Button();
            clickArea.Flat = true;
            clickArea.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            clickArea.Pressed += () => ShowDetailView(idx);
            card.AddChild(clickArea);
            clickArea.MouseFilter = Control.MouseFilterEnum.Pass;

            card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", MakeCardStyle(true));
            card.MouseExited += () => card.AddThemeStyleboxOverride("panel", MakeCardStyle(false));
        }

        // ════════════════════════════════════════
        // INVENTORY PANEL (persistent, right side)
        // ════════════════════════════════════════

        private void BuildInventoryPanel(Control parent)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(280, 0);
            panel.AddThemeStyleboxOverride("panel", MakePanelStyle(0.6f));
            parent.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            panel.AddChild(vbox);

            vbox.AddChild(MakeSectionHeader("INVENTORY"));
            vbox.AddChild(new HSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            vbox.AddChild(scroll);

            var listVBox = new VBoxContainer();
            listVBox.AddThemeConstantOverride("separation", 4);
            listVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(listVBox);

            // Tower Bases
            var towerHeader = new Label();
            towerHeader.Text = "Tower Bases";
            towerHeader.AddThemeFontSizeOverride("font_size", 14);
            towerHeader.AddThemeColorOverride("font_color", TowerBaseColor);
            listVBox.AddChild(towerHeader);

            foreach (var item in TowerBases)
                listVBox.AddChild(MakeInventoryItem(item.Name, item.Desc, TowerBaseColor, DRAG_TOWER_BASE));

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 8);
            listVBox.AddChild(spacer);

            // Function Bases
            var funcHeader = new Label();
            funcHeader.Text = "Function Bases";
            funcHeader.AddThemeFontSizeOverride("font_size", 14);
            funcHeader.AddThemeColorOverride("font_color", FuncBaseColor);
            listVBox.AddChild(funcHeader);

            foreach (var item in FuncBases)
                listVBox.AddChild(MakeInventoryItem(item.Name, item.Desc, FuncBaseColor, DRAG_FUNC_BASE));

            var spacer2 = new Control();
            spacer2.CustomMinimumSize = new Vector2(0, 8);
            listVBox.AddChild(spacer2);

            // Modifiers
            var modHeader = new Label();
            modHeader.Text = "Modifiers";
            modHeader.AddThemeFontSizeOverride("font_size", 14);
            modHeader.AddThemeColorOverride("font_color", ModColor);
            listVBox.AddChild(modHeader);

            foreach (var item in Modifiers)
                listVBox.AddChild(MakeInventoryItem(item.Name, item.Desc, ModColor, DRAG_MODIFIER));
        }

        private DragItem MakeInventoryItem(string name, string desc, Color color, string dragType)
        {
            var item = new DragItem();
            item.SetDragInfo(dragType, name, color, desc);
            item.TooltipText = desc;

            var itemStyle = new StyleBoxFlat();
            itemStyle.BgColor = new Color(color.R * 0.05f, color.G * 0.05f, color.B * 0.05f, 0.8f);
            itemStyle.BorderColor = new Color(color.R * 0.25f, color.G * 0.25f, color.B * 0.25f);
            itemStyle.SetBorderWidthAll(1);
            itemStyle.SetCornerRadiusAll(4);
            itemStyle.ContentMarginLeft = 8;
            itemStyle.ContentMarginRight = 8;
            itemStyle.ContentMarginTop = 5;
            itemStyle.ContentMarginBottom = 5;
            item.AddThemeStyleboxOverride("panel", itemStyle);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            item.AddChild(vbox);

            var nameLabel = new Label();
            nameLabel.Text = name;
            nameLabel.AddThemeFontSizeOverride("font_size", 14);
            nameLabel.AddThemeColorOverride("font_color", color);
            vbox.AddChild(nameLabel);

            var descLabel = new Label();
            descLabel.Text = desc;
            descLabel.AddThemeFontSizeOverride("font_size", 11);
            descLabel.AddThemeColorOverride("font_color", new Color(color.R * 0.5f, color.G * 0.5f, color.B * 0.5f));
            descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(descLabel);

            item.MouseEntered += () =>
            {
                var hover = (StyleBoxFlat)itemStyle.Duplicate();
                hover.BgColor = new Color(color.R * 0.12f, color.G * 0.12f, color.B * 0.12f, 0.9f);
                hover.BorderColor = color;
                item.AddThemeStyleboxOverride("panel", hover);
            };
            item.MouseExited += () => item.AddThemeStyleboxOverride("panel", itemStyle);

            return item;
        }

        // ════════════════════════════════════════
        // RELIC PANEL (persistent, bottom)
        // ════════════════════════════════════════

        private void BuildRelicPanel(Control parent)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(0, 140);
            panel.AddThemeStyleboxOverride("panel", MakePanelStyle(0.6f));
            parent.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            panel.AddChild(vbox);

            var headerRow = new HBoxContainer();
            headerRow.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(headerRow);

            var relicTitle = MakeSectionHeader("RELICS");
            relicTitle.HorizontalAlignment = HorizontalAlignment.Left;
            relicTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            headerRow.AddChild(relicTitle);

            var countLabel = new Label();
            countLabel.Text = $"{AllRelics.Length} collected";
            countLabel.AddThemeFontSizeOverride("font_size", 13);
            countLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.45f));
            headerRow.AddChild(countLabel);

            vbox.AddChild(new HSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
            vbox.AddChild(scroll);

            var relicRow = new HBoxContainer();
            relicRow.AddThemeConstantOverride("separation", 10);
            scroll.AddChild(relicRow);

            foreach (var relic in AllRelics)
                relicRow.AddChild(MakeRelicCard(relic));
        }

        private DragItem MakeRelicCard(RelicData relic)
        {
            var card = new DragItem();
            card.SetDragInfo(DRAG_RELIC, relic.Name, relic.Tint, relic.Desc);
            card.TooltipText = relic.Desc;
            card.CustomMinimumSize = new Vector2(130, 80);

            var cardStyle = new StyleBoxFlat();
            cardStyle.BgColor = new Color(relic.Tint.R * 0.06f, relic.Tint.G * 0.06f, relic.Tint.B * 0.06f, 0.9f);
            cardStyle.BorderColor = new Color(relic.Tint.R * 0.35f, relic.Tint.G * 0.35f, relic.Tint.B * 0.35f);
            cardStyle.SetBorderWidthAll(1);
            cardStyle.SetCornerRadiusAll(5);
            cardStyle.ContentMarginLeft = 8;
            cardStyle.ContentMarginRight = 8;
            cardStyle.ContentMarginTop = 6;
            cardStyle.ContentMarginBottom = 6;
            card.AddThemeStyleboxOverride("panel", cardStyle);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 3);
            card.AddChild(vbox);

            var nameLabel = new Label();
            nameLabel.Text = relic.Name;
            nameLabel.AddThemeFontSizeOverride("font_size", 13);
            nameLabel.AddThemeColorOverride("font_color", relic.Tint);
            vbox.AddChild(nameLabel);

            var descLabel = new Label();
            descLabel.Text = relic.Desc;
            descLabel.AddThemeFontSizeOverride("font_size", 10);
            descLabel.AddThemeColorOverride("font_color", new Color(relic.Tint.R * 0.55f, relic.Tint.G * 0.55f, relic.Tint.B * 0.55f));
            descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(descLabel);

            card.MouseEntered += () =>
            {
                var hover = (StyleBoxFlat)cardStyle.Duplicate();
                hover.BgColor = new Color(relic.Tint.R * 0.12f, relic.Tint.G * 0.12f, relic.Tint.B * 0.12f, 0.9f);
                hover.BorderColor = relic.Tint;
                card.AddThemeStyleboxOverride("panel", hover);
            };
            card.MouseExited += () => card.AddThemeStyleboxOverride("panel", cardStyle);

            return card;
        }

        // ════════════════════════════════════════
        // DETAIL VIEW
        // ════════════════════════════════════════

        private void RebuildDetailContent()
        {
            foreach (var child in _detailView.GetChildren())
                child.QueueFree();

            var data = _loadouts[_editingIndex];

            // ── Name + Save row at top ──
            var headerPanel = new PanelContainer();
            headerPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(0.4f));
            _detailView.AddChild(headerPanel);

            var headerVBox = new VBoxContainer();
            headerVBox.AddThemeConstantOverride("separation", 8);
            headerPanel.AddChild(headerVBox);

            var nameEdit = MakeNameEdit(_loadoutNames[_editingIndex]);
            int editIdx = _editingIndex;
            nameEdit.TextSubmitted += (text) =>
            {
                _loadoutNames[editIdx] = text;
                _detailTitle.Text = text.ToUpper();
                nameEdit.ReleaseFocus();
            };
            nameEdit.FocusExited += () =>
            {
                _loadoutNames[editIdx] = nameEdit.Text;
                _detailTitle.Text = nameEdit.Text.ToUpper();
            };
            headerVBox.AddChild(nameEdit);

            var saveBtnCenter = new CenterContainer();
            saveBtnCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            headerVBox.AddChild(saveBtnCenter);

            var saveBtn = MakeStyledButton("Save Loadout", new Color(0.3f, 0.8f, 0.4f));
            saveBtn.CustomMinimumSize = new Vector2(180, 40);
            saveBtn.AddThemeFontSizeOverride("font_size", 18);
            saveBtn.Pressed += OnSave;
            saveBtnCenter.AddChild(saveBtn);

            // ── Main content: relics on left, tower grid on right ──
            var contentHBox = new HBoxContainer();
            contentHBox.AddThemeConstantOverride("separation", 16);
            contentHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _detailView.AddChild(contentHBox);

            // Left column: Starting Relics (vertical)
            var relicCol = new VBoxContainer();
            relicCol.AddThemeConstantOverride("separation", 8);
            contentHBox.AddChild(relicCol);

            relicCol.AddChild(MakeSectionHeader("STARTING\nRELICS"));

            for (int i = 0; i < RELIC_SLOT_COUNT; i++)
                BuildRelicEquipSlot(relicCol, i, data);

            // Right area: Tower Slots in 2x2 grid
            var towerVBox = new VBoxContainer();
            towerVBox.AddThemeConstantOverride("separation", 10);
            towerVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            contentHBox.AddChild(towerVBox);

            towerVBox.AddChild(MakeSectionHeader("TOWER SLOTS"));

            var gridCenter = new CenterContainer();
            gridCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            gridCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            towerVBox.AddChild(gridCenter);

            var towerGrid = new GridContainer();
            towerGrid.Columns = 2;
            towerGrid.AddThemeConstantOverride("h_separation", 16);
            towerGrid.AddThemeConstantOverride("v_separation", 16);
            gridCenter.AddChild(towerGrid);

            for (int i = 0; i < TOWER_SLOT_COUNT; i++)
                BuildTowerSlot(towerGrid, i, data);
        }

        private LineEdit MakeNameEdit(string currentName)
        {
            var nameEdit = new LineEdit();
            nameEdit.Text = currentName;
            nameEdit.Alignment = HorizontalAlignment.Center;
            nameEdit.AddThemeFontSizeOverride("font_size", 22);
            nameEdit.AddThemeColorOverride("font_color", Accent);

            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = Colors.Transparent;
            normalStyle.BorderColor = new Color(Accent.R * 0.3f, Accent.G * 0.3f, Accent.B * 0.3f);
            normalStyle.SetBorderWidthAll(1);
            normalStyle.SetCornerRadiusAll(4);
            normalStyle.ContentMarginLeft = 8;
            normalStyle.ContentMarginRight = 8;
            normalStyle.ContentMarginTop = 4;
            normalStyle.ContentMarginBottom = 4;
            nameEdit.AddThemeStyleboxOverride("normal", normalStyle);

            var focusStyle = new StyleBoxFlat();
            focusStyle.BgColor = new Color(Accent.R * 0.08f, Accent.G * 0.08f, Accent.B * 0.08f, 0.8f);
            focusStyle.BorderColor = Accent;
            focusStyle.SetBorderWidthAll(1);
            focusStyle.SetCornerRadiusAll(4);
            focusStyle.ContentMarginLeft = 8;
            focusStyle.ContentMarginRight = 8;
            focusStyle.ContentMarginTop = 4;
            focusStyle.ContentMarginBottom = 4;
            nameEdit.AddThemeStyleboxOverride("focus", focusStyle);

            return nameEdit;
        }

        private void BuildTowerSlot(GridContainer parent, int slotIndex, LoadoutData data)
        {
            var slotPanel = new PanelContainer();
            slotPanel.CustomMinimumSize = new Vector2(270, 315);

            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0.03f, 0.03f, 0.04f, 0.9f);
            slotStyle.BorderColor = SlotBorder;
            slotStyle.SetBorderWidthAll(2);
            slotStyle.SetCornerRadiusAll(8);
            slotStyle.ContentMarginLeft = 16;
            slotStyle.ContentMarginRight = 16;
            slotStyle.ContentMarginTop = 14;
            slotStyle.ContentMarginBottom = 14;
            slotPanel.AddThemeStyleboxOverride("panel", slotStyle);
            parent.AddChild(slotPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 10);
            slotPanel.AddChild(vbox);

            var header = new Label();
            header.Text = $"Tower Slot {slotIndex + 1}";
            header.HorizontalAlignment = HorizontalAlignment.Center;
            header.AddThemeFontSizeOverride("font_size", 16);
            header.AddThemeColorOverride("font_color", Accent);
            vbox.AddChild(header);

            vbox.AddChild(new HSeparator());

            // Two base columns side by side
            var basesRow = new HBoxContainer();
            basesRow.AddThemeConstantOverride("separation", 16);
            basesRow.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            vbox.AddChild(basesRow);

            // Tower Base column
            int si = slotIndex;
            var towerCol = new VBoxContainer();
            towerCol.AddThemeConstantOverride("separation", 6);
            basesRow.AddChild(towerCol);

            var towerLabel = new Label();
            towerLabel.Text = "Tower Base";
            towerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            towerLabel.AddThemeFontSizeOverride("font_size", 12);
            towerLabel.AddThemeColorOverride("font_color", new Color(TowerBaseColor.R * 0.7f, TowerBaseColor.G * 0.7f, TowerBaseColor.B * 0.7f));
            towerCol.AddChild(towerLabel);

            var towerSlot = new DropSlot();
            towerSlot.Setup(DRAG_TOWER_BASE, "Tower\nBase", TowerBaseColor, 100, 100,
                (name) => { _loadouts[_editingIndex].TowerBaseNames[si] = name; });
            towerSlot.SetItem(data.TowerBaseNames[slotIndex]);
            towerCol.AddChild(towerSlot);

            var towerModLabel = new Label();
            towerModLabel.Text = "Modifier";
            towerModLabel.HorizontalAlignment = HorizontalAlignment.Center;
            towerModLabel.AddThemeFontSizeOverride("font_size", 11);
            towerModLabel.AddThemeColorOverride("font_color", new Color(ModColor.R * 0.5f, ModColor.G * 0.5f, ModColor.B * 0.5f));
            towerCol.AddChild(towerModLabel);

            var towerModCenter = new CenterContainer();
            towerModCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            towerCol.AddChild(towerModCenter);

            var towerModSlot = new DropSlot();
            towerModSlot.Setup(DRAG_MODIFIER, "Mod", ModColor, 63, 63,
                (name) => { _loadouts[_editingIndex].TowerModNames[si] = name; });
            towerModSlot.SetItem(data.TowerModNames[slotIndex]);
            towerModCenter.AddChild(towerModSlot);

            // Function Base column
            var funcCol = new VBoxContainer();
            funcCol.AddThemeConstantOverride("separation", 6);
            basesRow.AddChild(funcCol);

            var funcLabel = new Label();
            funcLabel.Text = "Function Base";
            funcLabel.HorizontalAlignment = HorizontalAlignment.Center;
            funcLabel.AddThemeFontSizeOverride("font_size", 12);
            funcLabel.AddThemeColorOverride("font_color", new Color(FuncBaseColor.R * 0.7f, FuncBaseColor.G * 0.7f, FuncBaseColor.B * 0.7f));
            funcCol.AddChild(funcLabel);

            var funcSlot = new DropSlot();
            funcSlot.Setup(DRAG_FUNC_BASE, "Function\nBase", FuncBaseColor, 100, 100,
                (name) => { _loadouts[_editingIndex].FuncBaseNames[si] = name; });
            funcSlot.SetItem(data.FuncBaseNames[slotIndex]);
            funcCol.AddChild(funcSlot);

            var funcModLabel = new Label();
            funcModLabel.Text = "Modifier";
            funcModLabel.HorizontalAlignment = HorizontalAlignment.Center;
            funcModLabel.AddThemeFontSizeOverride("font_size", 11);
            funcModLabel.AddThemeColorOverride("font_color", new Color(ModColor.R * 0.5f, ModColor.G * 0.5f, ModColor.B * 0.5f));
            funcCol.AddChild(funcModLabel);

            var funcModCenter = new CenterContainer();
            funcModCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            funcCol.AddChild(funcModCenter);

            var funcModSlot = new DropSlot();
            funcModSlot.Setup(DRAG_MODIFIER, "Mod", ModColor, 63, 63,
                (name) => { _loadouts[_editingIndex].FuncModNames[si] = name; });
            funcModSlot.SetItem(data.FuncModNames[slotIndex]);
            funcModCenter.AddChild(funcModSlot);
        }

        private void BuildRelicEquipSlot(Control parent, int relicIndex, LoadoutData data)
        {
            int ri = relicIndex;
            var slot = new DropSlot();
            slot.Setup(DRAG_RELIC, "Empty", RelicColor, 120, 100,
                (name) => { _loadouts[_editingIndex].RelicNames[ri] = name; });

            // Restore saved relic with its original color and tooltip
            string savedName = data.RelicNames[relicIndex];
            if (!string.IsNullOrEmpty(savedName))
            {
                foreach (var relic in AllRelics)
                {
                    if (relic.Name == savedName)
                    {
                        slot.SetItem(savedName, relic.Tint, relic.Desc);
                        break;
                    }
                }
            }

            parent.AddChild(slot);
        }

        private void OnSave()
        {
            if (_editingIndex < 0) return;
            _editSnapshot = _loadouts[_editingIndex].Clone();
            _editNameSnapshot = _loadoutNames[_editingIndex];
            RefreshCard(_editingIndex);
            SaveAllToDisk();
            GD.Print($"[Loadouts] Saved loadout {_editingIndex + 1}: {_loadoutNames[_editingIndex]}");
        }

        private void SaveAllToDisk()
        {
            var saveData = new LoadoutSave.LoadoutSaveData
            {
                Loadouts = new LoadoutSave.SavedLoadout[LOADOUT_COUNT]
            };
            for (int i = 0; i < LOADOUT_COUNT; i++)
            {
                saveData.Loadouts[i] = new LoadoutSave.SavedLoadout
                {
                    Name = _loadoutNames[i],
                    TowerBaseNames = _loadouts[i].TowerBaseNames,
                    FuncBaseNames = _loadouts[i].FuncBaseNames,
                    TowerModNames = _loadouts[i].TowerModNames,
                    FuncModNames = _loadouts[i].FuncModNames,
                    RelicNames = _loadouts[i].RelicNames,
                };
            }
            LoadoutSave.Save(saveData);
        }

        // ════════════════════════════════════════
        // STYLE HELPERS
        // ════════════════════════════════════════

        private StyleBoxFlat MakePanelStyle(float alpha)
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(TronTheme.PanelBg.R, TronTheme.PanelBg.G, TronTheme.PanelBg.B, alpha);
            style.BorderColor = new Color(Accent.R * 0.4f, Accent.G * 0.4f, Accent.B * 0.4f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(6);
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 16;
            style.ContentMarginBottom = 16;
            return style;
        }

        private StyleBoxFlat MakeCardStyle(bool hovered)
        {
            var style = new StyleBoxFlat();
            style.BgColor = hovered
                ? new Color(Accent.R * 0.1f, Accent.G * 0.1f, Accent.B * 0.1f, 0.9f)
                : new Color(Accent.R * 0.06f, Accent.G * 0.06f, Accent.B * 0.06f, 0.9f);
            style.BorderColor = hovered ? Accent : new Color(Accent.R * 0.3f, Accent.G * 0.3f, Accent.B * 0.3f);
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(6);
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 10;
            style.ContentMarginBottom = 10;
            return style;
        }

        private Label MakeSectionHeader(string text)
        {
            var label = new Label();
            label.Text = text;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 20);
            label.AddThemeColorOverride("font_color", Accent);
            return label;
        }

        private Button MakeStyledButton(string text, Color color)
        {
            var btn = new Button();
            btn.Text = text;
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.AddThemeColorOverride("font_color", color);

            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(color.R * 0.15f, color.G * 0.15f, color.B * 0.15f, 0.9f);
            normalStyle.BorderColor = new Color(color.R * 0.5f, color.G * 0.5f, color.B * 0.5f);
            normalStyle.SetBorderWidthAll(1);
            normalStyle.SetCornerRadiusAll(4);
            normalStyle.ContentMarginLeft = 12;
            normalStyle.ContentMarginRight = 12;
            normalStyle.ContentMarginTop = 6;
            normalStyle.ContentMarginBottom = 6;
            btn.AddThemeStyleboxOverride("normal", normalStyle);

            var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
            hoverStyle.BgColor = new Color(color.R * 0.3f, color.G * 0.3f, color.B * 0.3f, 0.9f);
            hoverStyle.BorderColor = color;
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
            pressedStyle.BgColor = new Color(color.R * 0.45f, color.G * 0.45f, color.B * 0.45f, 0.9f);
            pressedStyle.BorderColor = color;
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);

            return btn;
        }
    }
}
