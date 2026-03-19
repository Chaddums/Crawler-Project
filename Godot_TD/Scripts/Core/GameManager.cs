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
        public int ScrapCarryover { get; set; }

        // Meta perk persistence
        public MetaPerkSaveData MetaSave { get; set; }

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;
            MetaSave = MetaPerkSave.Load();
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
            GD.Print($"[GameManager] Starting battle on Planet {CurrentPlanet}");
            PlanetTheme.Current = CurrentPlanet switch {
                2 => new ScrapyardPlanetTheme(),
                _ => new TronPlanetTheme()
            };
            GD.Print($"[GameManager] Theme set to: {PlanetTheme.Current.PlanetName}");
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_BATTLE);
        }

        public void StartVineRun()
        {
            CurrentFloor = 1;
            SignalTuningEditor.ResetToDefaults();
            ApplyMetaPerks();
            ActivePerks.Clear();
            ScrapCarryover = 0;
            CurrentMagic = 0;
            SelectedMagicType = null;
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
            ScrapCarryover = CurrentScrap;
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_PERK);
        }

        public void ShowMetaPerkOrPerkSelect()
        {
            ScrapCarryover = CurrentScrap;

            // Award milestone points for this floor
            if (MetaSave == null)
                MetaSave = MetaPerkSave.Load();
            int awarded = MetaPerkSave.TryAwardFloorPoints(MetaSave, CurrentPlanet, CurrentFloor);
            if (awarded > 0)
            {
                MetaPerkSave.Save(MetaSave);
                GD.Print($"[MetaPerk] Awarded {awarded} points for planet {CurrentPlanet} floor {CurrentFloor}");
            }

            // Show meta perk tree if player has unspent points
            if (MetaSave.AvailablePoints > 0)
                GetTree().ChangeSceneToFile(Constants.SCENE_META_PERK);
            else
                GetTree().ChangeSceneToFile(Constants.SCENE_VINE_PERK);
        }

        public void ApplyMetaPerks()
        {
            if (MetaSave == null)
                MetaSave = MetaPerkSave.Load();

            foreach (int id in MetaSave.AllocatedIds)
            {
                var node = MetaPerkRegistry.GetNode(id);
                node?.Apply?.Invoke();
            }

            GD.Print($"[MetaPerk] Applied {MetaSave.AllocatedIds.Count} meta perks");
        }

        public void AddPerk(PerkData perk)
        {
            ActivePerks.Add(perk);
            perk.Apply?.Invoke();
            GameEvents.OnPerkSelected?.Invoke(perk);
        }

        public void StartLevelEditor()
        {
            GameEvents.ClearAll();
            Engine.TimeScale = 1.0;
            GetTree().ChangeSceneToFile(Constants.SCENE_LEVEL_EDITOR);
            SetPhase(GamePhase.LevelEditor);
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

        // ── Economy: Scrap + Magic ──
        // Scrap = universal resource for vine nodes, infrastructure, terrain
        // Magic = harvested resource for per-floor shop upgrades (accumulated, not spent like currency)

        public float CurrentMagic { get; private set; }
        public MagicType? SelectedMagicType { get; set; }  // Chosen at Mining Building placement

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

        public void AddMagic(float amount)
        {
            CurrentMagic += amount;
            GameEvents.OnMagicChanged?.Invoke(CurrentMagic);
        }

        public void SetMagic(float amount)
        {
            CurrentMagic = amount;
            GameEvents.OnMagicChanged?.Invoke(CurrentMagic);
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

            // F11 opens level editor from anywhere
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F11)
            {
                if (CurrentPhase != GamePhase.LevelEditor)
                    StartLevelEditor();
            }
        }
    }
}
