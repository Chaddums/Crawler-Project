using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Pause menu overlay. ESC key to toggle. Pauses the game tree.
    /// </summary>
    public partial class PauseMenuUI : CanvasLayer
    {
        private Control _panel;
        private bool _isOpen;

        public override void _Ready()
        {
            Layer = 30;
            ProcessMode = ProcessModeEnum.Always; // Process even when paused
            BuildUI();
            _panel.Visible = false;
        }

        private void BuildUI()
        {
            _panel = new Control();
            _panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _panel.ProcessMode = ProcessModeEnum.Always;
            AddChild(_panel);

            // Dim background
            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 0.7f);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            _panel.AddChild(bg);

            // Center container
            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            vbox.GrowHorizontal = Control.GrowDirection.Both;
            vbox.GrowVertical = Control.GrowDirection.Both;
            vbox.CustomMinimumSize = new Vector2(300, 0);
            vbox.AddThemeConstantOverride("separation", 12);
            _panel.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "PAUSED";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 42);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            vbox.AddChild(title);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 20);
            vbox.AddChild(spacer);

            // Buttons
            AddButton(vbox, "Resume", () => Close());
            AddButton(vbox, "Save Game", HandleSave);

            // World Loot Table — unlocked after first AXIS defeat
            if (MetaSaveManager.Data.TimesAxisDefeated >= 1)
                AddButton(vbox, "World Loot Table", HandleWorldLootTable);

            AddButton(vbox, "Main Menu", HandleMainMenu);
            AddButton(vbox, "Quit", HandleQuit);
        }

        private void AddButton(VBoxContainer parent, string text, System.Action action)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(260, 50);
            btn.AddThemeFontSizeOverride("font_size", 20);
            btn.ProcessMode = ProcessModeEnum.Always;
            btn.Pressed += () => action();
            parent.AddChild(btn);
        }

        public override void _UnhandledInput(InputEvent ev)
        {
            if (ev.IsActionPressed("pause"))
            {
                if (_isOpen) Close();
                else Open();
                GetViewport().SetInputAsHandled();
            }
        }

        private void Open()
        {
            _isOpen = true;
            _panel.Visible = true;
            GetTree().Paused = true;
        }

        private void Close()
        {
            _isOpen = false;
            _panel.Visible = false;
            GetTree().Paused = false;
        }

        private void HandleSave()
        {
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                int sector = GameManager.Instance?.CurrentSector ?? 1;
                SaveManager.SaveGame(player, sector);
                GD.Print("[PauseMenu] Game saved");
            }
        }

        private void HandleWorldLootTable()
        {
            var ui = new WorldLootTableUI();
            ui.Name = "WorldLootTable";
            GetTree().Root.AddChild(ui);
        }

        private void HandleMainMenu()
        {
            GetTree().Paused = false;
            _isOpen = false;
            GameManager.Instance?.ReturnToMainMenu();
        }

        private void HandleQuit()
        {
            // Save before quit
            HandleSave();
            GetTree().Paused = false;
            GameManager.Instance?.QuitGame();
        }
    }
}
