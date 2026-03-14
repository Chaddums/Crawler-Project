using System;
using Godot;

namespace JunkyardTD
{
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;
        public int CurrentWave { get; set; }
        public int CoreLives { get; private set; } = Constants.CORE_LIVES;
        public float GameSpeed { get; private set; } = 1f;
        public string SelectedMapId { get; set; } = "scrapyard";
        public float DifficultyMultiplier { get; set; } = 1f;

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;
            SetPhase(GamePhase.MainMenu);
        }

        public void SetPhase(GamePhase phase)
        {
            var previous = CurrentPhase;
            CurrentPhase = phase;
            GD.Print($"[GameManager] Phase: {previous} -> {phase}");
            GameEvents.OnPhaseChanged?.Invoke(phase);
        }

        public void GoToMapSelect()
        {
            GameEvents.ClearAll();
            GetTree().ChangeSceneToFile(Constants.SCENE_MAP_SELECT);
            SetPhase(GamePhase.MapSelect);
        }

        public void StartBattle()
        {
            GameEvents.ClearAll();
            CurrentWave = 0;
            CoreLives = Constants.CORE_LIVES;
            GetTree().ChangeSceneToFile(Constants.SCENE_BATTLE);
        }

        public void ReturnToMainMenu()
        {
            GameEvents.ClearAll();
            Engine.TimeScale = 1.0;
            GetTree().ChangeSceneToFile(Constants.SCENE_MAIN_MENU);
            SetPhase(GamePhase.MainMenu);
        }

        public void OnEnemyReachedCore()
        {
            CoreLives--;
            GameEvents.OnCoreLivesChanged?.Invoke(CoreLives);

            if (CoreLives <= 0)
            {
                SetPhase(GamePhase.Defeat);
                GameEvents.OnCoreDestroyed?.Invoke();
            }
        }

        public void ToggleSpeed()
        {
            if (GameSpeed == Constants.SPEED_NORMAL)
                GameSpeed = Constants.SPEED_FAST;
            else if (GameSpeed == Constants.SPEED_FAST)
                GameSpeed = Constants.SPEED_ULTRA;
            else
                GameSpeed = Constants.SPEED_NORMAL;

            Engine.TimeScale = GameSpeed;
        }

        public void TogglePause()
        {
            if (CurrentPhase == GamePhase.Paused)
            {
                GetTree().Paused = false;
                SetPhase(GamePhase.Build); // Resume to build phase
            }
            else if (CurrentPhase != GamePhase.MainMenu)
            {
                GetTree().Paused = true;
                SetPhase(GamePhase.Paused);
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("speed_up"))
                ToggleSpeed();
        }
    }
}
