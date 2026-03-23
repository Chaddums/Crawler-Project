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
        public int CurrentResources { get; private set; } = Constants.STARTING_RESOURCES;
        /// <summary>S2: Running total of all resources earned this run (never decreases). Used for extraction score.</summary>
        public int TotalExtracted { get; private set; }
        public float GameSpeed { get; private set; } = 1f;
        public string SelectedMapId { get; set; } = "scrapyard";
        public float DifficultyMultiplier { get; set; } = 1f;
        public string SelectedRole { get; set; } = "Obelisk";
        public VineNodeType[] AvailableNodes { get; set; }

        // Planet progression (no floors — continuous run per planet)
        public int CurrentPlanet { get; set; } = 1;  // 1=Grid Prime, 2=Scrapyard
        public RunMode CurrentRunMode { get; set; } = RunMode.Harvest;
        public List<PerkData> ActivePerks { get; private set; } = new();
        public int ResourceCarryover { get; set; }

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

        public void StartPlanetSelect()
        {
            GameEvents.ClearAll();
            SetPhase(GamePhase.PlanetSelect);
            GetTree().ChangeSceneToFile(Constants.SCENE_PLANET_SELECT);
        }

        public void LaunchFromPlanetSelect(int planet, RunMode mode)
        {
            CurrentPlanet = planet;
            CurrentRunMode = mode;
            GD.Print($"[GameManager] Planet={planet}, RunMode={mode}");
            GameEvents.ClearAll();
            GetTree().ChangeSceneToFile(Constants.SCENE_INTRO_CINEMATIC);
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
            TotalExtracted = 0;
            GD.Print($"[GameManager] Starting battle on Planet {CurrentPlanet}");
            PlanetTheme.Current = CurrentPlanet switch {
                2 => new ScrapyardPlanetTheme(),
                _ => new TronPlanetTheme()
            };
            GD.Print($"[GameManager] Theme set to: {PlanetTheme.Current.PlanetName}");
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_BATTLE);
        }

        // S1: Replaces StartVineRun — no floors, continuous run
        public void StartVineRun()
        {
            SignalTuningEditor.ResetToDefaults();
            ApplyMetaPerks();
            ActivePerks.Clear();
            ResourceCarryover = 0;
            CurrentMaterials = 0;
            SelectedMaterialType = null;
            StartVineBattle();
        }

        public void ShowPerkSelect()
        {
            ResourceCarryover = CurrentResources;
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_PERK);
        }

        // S1: Simplified — no floor-based point awarding (milestones replace floors)
        public void ShowMetaPerkOrPerkSelect()
        {
            ResourceCarryover = CurrentResources;

            if (MetaSave == null)
                MetaSave = MetaPerkSave.Load();

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
            if (CoreLives <= 0) return;
            CoreLives--;
            GameEvents.OnCoreLivesChanged?.Invoke(CoreLives);

            if (CoreLives <= 0)
            {
                SetPhase(GamePhase.Defeat);
                GameEvents.OnCoreDestroyed?.Invoke();
            }
        }

        // ── Economy: Resources + Materials ──
        // Resources = universal currency for vine nodes, infrastructure
        // Materials = harvested resource for ability upgrades (accumulated, not spent like currency)

        public float CurrentMaterials { get; private set; }
        public MaterialType? SelectedMaterialType { get; set; }

        public void SetResources(int amount)
        {
            CurrentResources = amount;
            GameEvents.OnResourcesChanged?.Invoke(CurrentResources);
        }

        public void AddResources(int amount)
        {
            CurrentResources += amount;
            if (amount > 0) TotalExtracted += amount;  // S2: track extraction score
            GameEvents.OnResourcesChanged?.Invoke(CurrentResources);
        }

        public bool SpendResources(int amount)
        {
            if (CurrentResources < amount) return false;
            CurrentResources -= amount;
            GameEvents.OnResourcesChanged?.Invoke(CurrentResources);
            return true;
        }

        public void AddMaterials(float amount)
        {
            CurrentMaterials += amount;
            GameEvents.OnMaterialsChanged?.Invoke(CurrentMaterials);
        }

        public void SetMaterials(float amount)
        {
            CurrentMaterials = amount;
            GameEvents.OnMaterialsChanged?.Invoke(CurrentMaterials);
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

        private GamePhase _prePausePhase = GamePhase.Wave;

        public void TogglePause()
        {
            if (CurrentPhase == GamePhase.Paused)
            {
                GetTree().Paused = false;
                SetPhase(_prePausePhase);
            }
            else if (CurrentPhase != GamePhase.MainMenu)
            {
                _prePausePhase = CurrentPhase;
                GetTree().Paused = true;
                SetPhase(GamePhase.Paused);
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("speed_up"))
                ToggleSpeed();

            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F11)
            {
                if (CurrentPhase != GamePhase.LevelEditor)
                    StartLevelEditor();
            }
        }
    }
}
