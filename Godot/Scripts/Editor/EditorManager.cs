using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Main editor suite manager. Singleton autoload.
    /// F12 toggles the editor overlay. CanvasLayer 100 ensures it draws above everything.
    /// ProcessMode=Always so it works while game is paused.
    /// Tab bar switches between editor modules.
    /// </summary>
    public partial class EditorManager : CanvasLayer
    {
        public static EditorManager Instance { get; private set; }
        public static Image LastScreenshot { get; private set; }

        private bool _visible;
        private PanelContainer _root;
        private HBoxContainer _tabBar;
        private Control _moduleContainer;
        private Label _titleLabel;

        private readonly List<EditorPanel> _modules = new();
        private readonly List<Button> _tabButtons = new();
        private int _activeTab = -1;

        public string ActiveTabName => _activeTab >= 0 ? _modules[_activeTab].PanelName : "Unknown";

        public IReadOnlyList<EditorPanel> Modules => _modules;

        public override void _Ready()
        {
            Instance = this;
            Layer = 100;
            ProcessMode = ProcessModeEnum.Always;

            BuildRootUI();
            _root.Visible = false;

            // Register editor modules
            CallDeferred(nameof(RegisterModules));
        }

        private void RegisterModules()
        {
            RegisterModule(new BalanceEditor());
            RegisterModule(new CharacterViewer());
            RegisterModule(new BossEditor());
            RegisterModule(new DungeonVisualizer());
            RegisterModule(new AssetEditor());
            RegisterModule(new SectorEditor());
            RegisterModule(new StringEditor());
            RegisterModule(new VfxEditor());
            RegisterModule(new SoundDesigner());
            RegisterModule(new UIDesigner());
            RegisterModule(new BugReporter());
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
                else if (key.Keycode == Key.B && key.CtrlPressed)
                {
                    bool feature = key.ShiftPressed;
                    BugReportDialog.Show(GetTree(), featureRequest: feature);
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        /// <summary>Toggle editor visibility.</summary>
        public void Toggle()
        {
            _visible = !_visible;

            if (_visible)
            {
                // Capture screenshot BEFORE overlay shows
                LastScreenshot = GetViewport().GetTexture().GetImage();
                _root.Visible = true;
                GetTree().Paused = true;
            }
            else
            {
                _root.Visible = false;
                GetTree().Paused = false;
            }
        }

        /// <summary>Register an editor module panel.</summary>
        public void RegisterModule(EditorPanel panel)
        {
            _modules.Add(panel);
            panel.Visible = false;
            _moduleContainer.AddChild(panel);

            var tabBtn = new Button();
            tabBtn.Text = panel.PanelName;
            tabBtn.AddThemeFontSizeOverride("font_size", EditorStyles.FontBody);
            tabBtn.CustomMinimumSize = new Vector2(100, 32);

            int index = _modules.Count - 1;
            tabBtn.Pressed += () => SwitchTab(index);
            _tabBar.AddChild(tabBtn);
            _tabButtons.Add(tabBtn);

            panel.OnDirtyChanged += () => UpdateTabLabel(index);

            // Auto-select first tab
            if (_modules.Count == 1)
                SwitchTab(0);
        }

        /// <summary>Switch to a tab by name (e.g. "Bugs").</summary>
        public void SwitchToTab(string name)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i].PanelName == name)
                {
                    SwitchTab(i);
                    return;
                }
            }
        }

        /// <summary>Capture viewport with editor visible for bug reports from within tabs.</summary>
        public void CaptureEditorScreenshot()
        {
            LastScreenshot = GetViewport().GetTexture().GetImage();
        }

        private void SwitchTab(int index)
        {
            if (index < 0 || index >= _modules.Count) return;

            // Hide current
            if (_activeTab >= 0 && _activeTab < _modules.Count)
                _modules[_activeTab].Visible = false;

            _activeTab = index;
            _modules[index].Visible = true;

            // Update tab button styles
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool active = i == index;
                var btn = _tabButtons[i];
                if (active)
                {
                    btn.AddThemeColorOverride("font_color", _modules[i].AccentColor);
                    btn.AddThemeStyleboxOverride("normal", EditorStyles.MakeFlat(EditorStyles.BgField));
                }
                else
                {
                    btn.RemoveThemeColorOverride("font_color");
                    btn.AddThemeStyleboxOverride("normal", EditorStyles.MakeFlat(EditorStyles.BgDark));
                }
            }

            _titleLabel.Text = _modules[index].PanelName;
            _titleLabel.AddThemeColorOverride("font_color", _modules[index].AccentColor);
        }

        private void UpdateTabLabel(int index)
        {
            if (index < 0 || index >= _modules.Count) return;
            var panel = _modules[index];
            _tabButtons[index].Text = panel.IsDirty ? $"* {panel.PanelName}" : panel.PanelName;
        }

        private void BuildRootUI()
        {
            _root = new PanelContainer();
            _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _root.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(
                bg: EditorStyles.BgDark,
                border: EditorStyles.BorderAccent,
                borderWidth: 2,
                cornerRadius: 0,
                margin: 0
            ));

            var mainVBox = new VBoxContainer();
            mainVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            // Title bar
            var titleBar = new HBoxContainer();
            titleBar.AddThemeConstantOverride("separation", 12);
            var titlePanel = new PanelContainer();
            titlePanel.AddThemeStyleboxOverride("panel", EditorStyles.MakeFlat(EditorStyles.BgHeader));

            var titleContent = new HBoxContainer();
            titleContent.AddThemeConstantOverride("separation", 12);

            var editorLabel = EditorStyles.MakeLabel("  JUNKBOT EDITOR", EditorStyles.FontTitle, EditorStyles.TextAccent);
            titleContent.AddChild(editorLabel);

            _titleLabel = EditorStyles.MakeLabel("", EditorStyles.FontHeader, EditorStyles.TextSecondary);
            titleContent.AddChild(_titleLabel);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            titleContent.AddChild(spacer);

            var closeBtn = EditorStyles.MakeButton("X  Close (F12)", EditorStyles.FontSmall, EditorStyles.StatusError);
            closeBtn.Pressed += Toggle;
            titleContent.AddChild(closeBtn);

            titlePanel.AddChild(titleContent);
            mainVBox.AddChild(titlePanel);

            // Tab bar
            var tabPanel = new PanelContainer();
            tabPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakeFlat(EditorStyles.BgPanel));
            _tabBar = new HBoxContainer();
            _tabBar.AddThemeConstantOverride("separation", 2);
            tabPanel.AddChild(_tabBar);
            mainVBox.AddChild(tabPanel);

            // Module content area
            _moduleContainer = new PanelContainer();
            _moduleContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _moduleContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _moduleContainer.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(
                bg: EditorStyles.BgPanel,
                margin: 8
            ));
            mainVBox.AddChild(_moduleContainer);

            _root.AddChild(mainVBox);
            AddChild(_root);
        }
    }
}
