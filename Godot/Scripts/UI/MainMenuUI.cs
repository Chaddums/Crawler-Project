using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Main menu with New Game / Quit buttons.
    /// Attached to the root Control of MainMenu.tscn.
    /// </summary>
    public partial class MainMenuUI : Control
    {
        private Button _newGameButton;
        private Button _quitButton;

        public override void _Ready()
        {
            _newGameButton = GetNode<Button>("Background/VBoxContainer/NewGameButton");
            _quitButton = GetNode<Button>("Background/VBoxContainer/QuitButton");

            _newGameButton.Pressed += HandleNewGame;
            _newGameButton.MouseEntered += () => ScaleButton(_newGameButton, 1.05f);
            _newGameButton.MouseExited += () => ScaleButton(_newGameButton, 1.0f);

            _quitButton.Pressed += HandleQuit;
            _quitButton.MouseEntered += () => ScaleButton(_quitButton, 1.05f);
            _quitButton.MouseExited += () => ScaleButton(_quitButton, 1.0f);

            GD.Print("[MainMenuUI] Buttons wired");
        }

        private void HandleNewGame()
        {
            GD.Print("[MainMenuUI] New Game clicked");
            if (GameManager.Instance != null)
                GameManager.Instance.StartNewGame();
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
