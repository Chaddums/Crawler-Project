using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Main menu with Continue / New Game / Quit buttons.
    /// Attached to the root Control of MainMenu.tscn.
    /// </summary>
    public partial class MainMenuUI : Control
    {
        private Button _continueButton;
        private Button _newGameButton;
        private Button _workshopButton;
        private Button _quitButton;

        public override void _Ready()
        {
            _continueButton = GetNodeOrNull<Button>("Background/VBoxContainer/ContinueButton");
            _newGameButton = GetNode<Button>("Background/VBoxContainer/NewGameButton");
            _quitButton = GetNode<Button>("Background/VBoxContainer/QuitButton");

            // Continue button — only visible if save exists
            if (_continueButton != null)
            {
                _continueButton.Visible = SaveManager.SaveFileExists();
                _continueButton.Pressed += HandleContinue;
                _continueButton.MouseEntered += () => ScaleButton(_continueButton, 1.05f);
                _continueButton.MouseExited += () => ScaleButton(_continueButton, 1.0f);
            }

            _newGameButton.Pressed += HandleNewGame;
            _newGameButton.MouseEntered += () => ScaleButton(_newGameButton, 1.05f);
            _newGameButton.MouseExited += () => ScaleButton(_newGameButton, 1.0f);

            // Workshop button — add dynamically after New Game
            _workshopButton = new Button();
            _workshopButton.Text = StringLoader.Get("ui.mainMenu.workshop");
            _workshopButton.CustomMinimumSize = _newGameButton.CustomMinimumSize;
            _workshopButton.SizeFlagsHorizontal = _newGameButton.SizeFlagsHorizontal;
            _workshopButton.Pressed += HandleWorkshop;
            _workshopButton.MouseEntered += () => ScaleButton(_workshopButton, 1.05f);
            _workshopButton.MouseExited += () => ScaleButton(_workshopButton, 1.0f);
            var vbox = _newGameButton.GetParent();
            vbox.AddChild(_workshopButton);
            vbox.MoveChild(_workshopButton, _newGameButton.GetIndex() + 1);

            _quitButton.Pressed += HandleQuit;
            _quitButton.MouseEntered += () => ScaleButton(_quitButton, 1.05f);
            _quitButton.MouseExited += () => ScaleButton(_quitButton, 1.0f);

            // Show ascension rank if unlocked
            int ascension = MetaSaveManager.Data.AscensionRank;
            if (ascension > 0)
            {
                var ascLabel = new Label();
                ascLabel.Text = $"Ascension {ascension}";
                ascLabel.HorizontalAlignment = HorizontalAlignment.Center;
                ascLabel.AddThemeFontSizeOverride("font_size", 20);
                ascLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.4f, 0.95f));
                var vboxParent = _newGameButton.GetParent();
                vboxParent.AddChild(ascLabel);
                vboxParent.MoveChild(ascLabel, 0);
            }

            GD.Print("[MainMenuUI] Buttons wired");
        }

        private void HandleContinue()
        {
            GD.Print("[MainMenuUI] Continue clicked");
            var saveData = SaveManager.LoadGame();
            if (saveData != null && GameManager.Instance != null)
            {
                GameManager.Instance.SelectedClass = saveData.Player.ClassName;
                GameManager.Instance.CurrentSector = saveData.CurrentSector;
                GameManager.Instance.CurrentArea = saveData.CurrentArea;
                GameManager.Instance.ContinueGame();
            }
        }

        private void HandleNewGame()
        {
            GD.Print("[MainMenuUI] New Game clicked");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CurrentSector = 1;
                GameManager.Instance.CurrentArea = 1;
                GameManager.Instance.GoToCharacterCreation();
            }
        }

        private void HandleWorkshop()
        {
            GD.Print("[MainMenuUI] Workshop clicked");
            if (WorkshopUI.Instance == null)
            {
                var workshop = new WorkshopUI();
                workshop.Name = "WorkshopUI";
                GetTree().Root.AddChild(workshop);
            }
            WorkshopUI.Instance.Open();
        }

        private void HandleQuit()
        {
            GD.Print("[MainMenuUI] Quit clicked");
            if (GameManager.Instance != null)
                GameManager.Instance.QuitGame();
        }

        private void ScaleButton(Button button, float scale)
        {
            button.Scale = new Vector2(scale, scale);
            button.PivotOffset = button.Size / 2f;
        }
    }
}
