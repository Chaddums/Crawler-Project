using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Passive tree overlay panel: title, points display, canvas, respec button, close.
    /// Toggled via P key.
    /// </summary>
    public partial class PassiveTreeUI : CanvasLayer
    {
        private Control _panel;
        private Label _pointsLabel;
        private PassiveTreeCanvas _canvas;
        private bool _isOpen;

        public override void _Ready()
        {
            Layer = 21;
            ProcessMode = ProcessModeEnum.Always;
            BuildUI();
            _panel.Visible = false;
        }

        private void BuildUI()
        {
            // Full-screen panel
            _panel = new Control();
            _panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_panel);

            // Dark background
            var bg = new ColorRect();
            bg.Color = new Color(0.03f, 0.03f, 0.08f, 0.95f);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            _panel.AddChild(bg);

            // Title bar
            var titleBar = new HBoxContainer();
            titleBar.Position = new Vector2(20, 10);
            titleBar.Size = new Vector2(1880, 50);
            _panel.AddChild(titleBar);

            var title = new Label();
            title.Text = StringLoader.Get("ui.passiveTree.title");
            title.AddThemeFontSizeOverride("font_size", 32);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            titleBar.AddChild(title);

            _pointsLabel = new Label();
            _pointsLabel.Text = StringLoader.Get("ui.passiveTree.pointsLabel", ("{value}", "0"));
            _pointsLabel.AddThemeFontSizeOverride("font_size", 22);
            _pointsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 0.7f));
            titleBar.AddChild(_pointsLabel);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(20, 0);
            titleBar.AddChild(spacer);

            var respecBtn = new Button();
            respecBtn.Text = StringLoader.Get("ui.passiveTree.respecAll");
            respecBtn.CustomMinimumSize = new Vector2(120, 40);
            respecBtn.AddThemeFontSizeOverride("font_size", 16);
            respecBtn.Pressed += HandleRespec;
            titleBar.AddChild(respecBtn);

            var spacer2 = new Control();
            spacer2.CustomMinimumSize = new Vector2(10, 0);
            titleBar.AddChild(spacer2);

            var closeBtn = new Button();
            closeBtn.Text = "X";
            closeBtn.CustomMinimumSize = new Vector2(40, 40);
            closeBtn.Pressed += Close;
            titleBar.AddChild(closeBtn);

            // Tree canvas
            _canvas = new PassiveTreeCanvas();
            _canvas.Position = new Vector2(0, 60);
            _canvas.Size = new Vector2(1920, 1020);
            _panel.AddChild(_canvas);
        }

        public override void _UnhandledInput(InputEvent ev)
        {
            if (ev is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.PhysicalKeycode == Key.P)
                {
                    ToggleTree();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void ToggleTree()
        {
            if (_isOpen) Close();
            else Open();
        }

        private void Open()
        {
            _isOpen = true;
            _panel.Visible = true;
            GetTree().Paused = true;
            UpdatePointsLabel();

            // Center view on the player's class area
            if (ServiceLocator.TryGet<PlayerController>(out var player))
                _canvas.CenterOnClass(GameManager.Instance?.SelectedClass ?? player.ClassController.CurrentClass ?? BotFrameType.Scrapheap);

            _canvas.QueueRedraw();
        }

        private void Close()
        {
            _isOpen = false;
            _panel.Visible = false;
            GetTree().Paused = false;
        }

        private void HandleRespec()
        {
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;
            player.ClassController.Respec();
            UpdatePointsLabel();
            _canvas.QueueRedraw();
        }

        private void UpdatePointsLabel()
        {
            if (ServiceLocator.TryGet<PlayerController>(out var player))
                _pointsLabel.Text = StringLoader.Get("ui.passiveTree.pointsLabel", ("{value}", player.Stats.AvailableSkillPoints.ToString()));
        }

        public override void _Process(double delta)
        {
            if (_isOpen)
                UpdatePointsLabel();
        }
    }
}
