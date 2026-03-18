using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;
        public int CurrentWave { get; set; }
        public int CoreLives { get; private set; } = Constants.CORE_LIVES;
        public int CurrentScrap { get; private set; } = Constants.STARTING_SCRAP;
        public float GameSpeed { get; private set; } = 1f;
        public string SelectedMapId { get; set; } = "scrapyard";
        public float DifficultyMultiplier { get; set; } = 1f;
        public string SelectedRole { get; set; } = "Scrapwright";
        public VineNodeType[] AvailableNodes { get; set; }

        // Planet + Floor progression
        public int CurrentPlanet { get; set; } = 1;  // 1=Grid Prime, 2=Scrapyard
        public int CurrentFloor { get; set; } = 1;
        public List<PerkData> ActivePerks { get; private set; } = new();
        public int GoldCarryover { get; set; }

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

        public void StartVineDraft()
        {
            GameEvents.ClearAll();
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_DRAFT);
        }

        public void StartVineBattle()
        {
            GameEvents.ClearAll();
            CurrentWave = 0;
            // Set planet theme based on CurrentPlanet
            PlanetTheme.Current = CurrentPlanet switch {
                2 => new ScrapyardPlanetTheme(),
                _ => new TronPlanetTheme()
            };
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_BATTLE);
        }

        public void StartVineRun()
        {
            CurrentFloor = 1;
            ActivePerks.Clear();
            GoldCarryover = 0;
            StartVineBattle();
        }

        public void StartVineFloor(int floor)
        {
            GameEvents.ClearAll();
            CurrentFloor = floor;
            CurrentWave = 0;
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_BATTLE);
        }

        public void ShowPerkSelect()
        {
            GoldCarryover = CurrentScrap;
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_PERK);
        }

        public void AddPerk(PerkData perk)
        {
            ActivePerks.Add(perk);
            perk.Apply?.Invoke();
            GameEvents.OnPerkSelected?.Invoke(perk);
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
            if (CoreLives <= 0) return; // Already defeated
            CoreLives--;
            GameEvents.OnCoreLivesChanged?.Invoke(CoreLives);

            if (CoreLives <= 0)
            {
                SetPhase(GamePhase.Defeat);
                GameEvents.OnCoreDestroyed?.Invoke();
            }
        }

        // ── Economy ──

        public void SetScrap(int amount)
        {
            CurrentScrap = amount;
            GameEvents.OnScrapChanged?.Invoke(CurrentScrap);
        }

        public void AddScrap(int amount)
        {
            CurrentScrap += amount;
            GameEvents.OnScrapChanged?.Invoke(CurrentScrap);
        }

        public bool SpendScrap(int amount)
        {
            if (CurrentScrap < amount) return false;
            CurrentScrap -= amount;
            GameEvents.OnScrapChanged?.Invoke(CurrentScrap);
            return true;
        }

        public void SetCoreLives(int lives)
        {
            CoreLives = lives;
            GameEvents.OnCoreLivesChanged?.Invoke(CoreLives);
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
