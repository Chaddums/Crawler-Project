using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// In-game editor for live tuning. F12 to toggle.
    /// Tabbed panel system — each module handles a specific data domain.
    /// Pauses game while open so you can tweak mid-battle.
    /// </summary>
    public partial class EditorManager : CanvasLayer
    {
        public static EditorManager Instance { get; private set; }

        private PanelContainer _root;
        private HBoxContainer _tabBar;
        private Control _moduleContainer;
        private Label _titleLabel;
        private Label _statusLabel;

        private readonly List<EditorModule> _modules = new();
        private readonly List<Button> _tabButtons = new();
        private int _activeTab = -1;
        private bool _visible;
        private bool _bvtRanThisSession;
        private int _bvtTabIndex = -1;

        public override void _Ready()
        {
            Instance = this;
            Layer = 100;
            ProcessMode = ProcessModeEnum.Always;

            BuildUI();
            RegisterModules();
            _root.Visible = false;
        }

        private void BuildUI()
        {
            _root = new PanelContainer();
            _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _root.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgDark));
            AddChild(_root);

            var mainVBox = new VBoxContainer();
            mainVBox.AddThemeConstantOverride("separation", 0);
            _root.AddChild(mainVBox);

            // ── Header bar ──
            var headerPanel = new PanelContainer();
            headerPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgHeader));
            mainVBox.AddChild(headerPanel);

            var headerHBox = new HBoxContainer();
            headerHBox.AddThemeConstantOverride("separation", 15);
            headerPanel.AddChild(headerHBox);

            _titleLabel = EditorStyles.MakeLabel("VINE LOGIC EDITOR", 20, EditorStyles.AccentNodes);
            headerHBox.AddChild(_titleLabel);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            headerHBox.AddChild(spacer);

            _statusLabel = EditorStyles.MakeLabel("F12 to close", 12, EditorStyles.TextMuted);
            _statusLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            headerHBox.AddChild(_statusLabel);

            var closeBtn = EditorStyles.MakeButton("X", 16, EditorStyles.StatusError);
            closeBtn.CustomMinimumSize = new Vector2(30, 30);
            closeBtn.Pressed += Toggle;
            headerHBox.AddChild(closeBtn);

            // ── Tab bar ──
            var tabPanel = new PanelContainer();
            tabPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(new Color(0.09f, 0.09f, 0.11f)));
            mainVBox.AddChild(tabPanel);

            _tabBar = new HBoxContainer();
            _tabBar.AddThemeConstantOverride("separation", 2);
            tabPanel.AddChild(_tabBar);

            // ── Module container — PanelContainer so children inherit full size ──
            var moduleWrapper = new PanelContainer();
            moduleWrapper.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            moduleWrapper.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var modStyle = new StyleBoxFlat();
            modStyle.BgColor = EditorStyles.BgDark;
            moduleWrapper.AddThemeStyleboxOverride("panel", modStyle);
            mainVBox.AddChild(moduleWrapper);
            _moduleContainer = moduleWrapper;
        }

        private void RegisterModules()
        {
            RegisterModule(new AssetSandboxEditor());
            RegisterModule(new NodeBalanceEditor());
            RegisterModule(new WaveEditor());
            RegisterModule(new ExtractionCurveEditor());
            RegisterModule(new SignalTuningEditor());
            RegisterModule(new CharacterViewerEditor());
            RegisterModule(new SoundDesigner());
            _bvtTabIndex = _modules.Count;
            RegisterModule(new EditorBVT());

            // Select first tab
            if (_modules.Count > 0)
                SwitchToTab(0);
        }

        public void RegisterModule(EditorModule module)
        {
            _modules.Add(module);
            _moduleContainer.AddChild(module);
            module.Visible = false;
            module.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            module.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var btn = EditorStyles.MakeButton(module.ModuleName, 13, module.AccentColor);
            btn.CustomMinimumSize = new Vector2(100, 32);
            int idx = _modules.Count - 1;
            btn.Pressed += () => SwitchToTab(idx);
            _tabBar.AddChild(btn);
            _tabButtons.Add(btn);
        }

        public void SwitchToTab(int index)
        {
            if (index < 0 || index >= _modules.Count) return;

            // Hide current
            if (_activeTab >= 0 && _activeTab < _modules.Count)
                _modules[_activeTab].Visible = false;

            _activeTab = index;
            _modules[index].Visible = true;
            _modules[index].OnActivated();

            _titleLabel.Text = _modules[index].ModuleName.ToUpper();
            _titleLabel.AddThemeColorOverride("font_color", _modules[index].AccentColor);

            // Update tab button states
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                _tabButtons[i].AddThemeColorOverride("font_color",
                    i == index ? _modules[i].AccentColor : EditorStyles.TextMuted);
            }
        }

        public void Toggle()
        {
            _visible = !_visible;
            _root.Visible = _visible;

            if (_visible)
            {
                GetTree().Paused = true;

                // Auto-open BVT tab on first editor open per session
                if (!_bvtRanThisSession && _bvtTabIndex >= 0)
                {
                    _bvtRanThisSession = true;
                    SwitchToTab(_bvtTabIndex);
                }
                else if (_activeTab >= 0 && _activeTab < _modules.Count)
                {
                    _modules[_activeTab].OnActivated();
                }
            }
            else
            {
                GetTree().Paused = false;
            }
        }

        public void SetStatus(string text, Color? color = null)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = text;
                _statusLabel.AddThemeColorOverride("font_color", color ?? EditorStyles.TextMuted);
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.F12)
                {
                    Toggle();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.Keycode == Key.B && key.CtrlPressed && key.ShiftPressed)
                {
                    // Bug reporter works even when editor is open / game is paused
                    if (_visible) Toggle(); // Close editor first so screenshot captures the game
                    BugReportDialog.Show(GetTree());
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public override void _ExitTree()
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Base class for editor modules. Each module is a tab in the editor.
    /// </summary>
    public abstract partial class EditorModule : VBoxContainer
    {
        public abstract string ModuleName { get; }
        public abstract Color AccentColor { get; }

        /// <summary>
        /// Called when this tab becomes active. Refresh data if needed.
        /// </summary>
        public virtual void OnActivated() { }
    }
}
