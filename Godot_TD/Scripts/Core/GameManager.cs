using System;
using System.Collections.Generic;
using Godot;
using Sentry;

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

        // S4: Territory + Boss Run
        public TerritorySaveData TerritorySave { get; set; }
        public int? EquippedSuitIndex { get; set; }
        public string BossSectionId { get; set; }
        public bool IsBossRun => CurrentRunMode == RunMode.BossRun;

        // Territory section → Run start: which section the player selected determines map + waves
        public string CurrentTerritorySectionId { get; set; }
        public TerritorySection CurrentTerritorySection =>
            string.IsNullOrEmpty(CurrentTerritorySectionId) ? null : TerritoryManager.GetSection(CurrentTerritorySectionId);

        /// <summary>
        /// Launch a run from a specific territory section. Sets planet, section, map variant.
        /// Called from the meta hub when player selects a section and hits Start Run.
        /// </summary>
        public void LaunchFromTerritorySection(int planet, string sectionId, RunMode mode = RunMode.Harvest)
        {
            CurrentPlanet = planet;
            CurrentTerritorySectionId = sectionId;
            CurrentRunMode = mode;

            var section = TerritoryManager.GetSection(sectionId);
            GD.Print($"[GameManager] Launching P{planet} section={sectionId} ({section?.Name ?? "unknown"}) mode={mode}");

            // Skip cinematic — go straight to draft screen
            GameEvents.ClearAll();
            ChangeScene(Constants.SCENE_VINE_DRAFT);
        }

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;
            MetaSave = MetaPerkSave.Load();
            TerritorySave = new TerritorySaveData { MetaSave = MetaSave };
            SetPhase(GamePhase.MainMenu);

            // Set Sentry context tags for this session
            SentrySdk.ConfigureScope(scope =>
            {
                scope.SetTag("game.version", Constants.GAME_VERSION);
                scope.SetTag("game.engine", "godot-4.6");
            });
        }

        /// <summary>
        /// Transition-aware scene change. Uses TransitionManager fade if available,
        /// otherwise falls back to direct scene change.
        /// </summary>
        private void ChangeScene(string scenePath)
        {
            if (TransitionManager.Instance != null)
                TransitionManager.Instance.TransitionToScene(scenePath);
            else
                GetTree().ChangeSceneToFile(scenePath);
        }

        public void SetPhase(GamePhase phase)
        {
            var previous = CurrentPhase;
            CurrentPhase = phase;
            GD.Print($"[GameManager] Phase: {previous} -> {phase}");
            SentryInit.AddBreadcrumb($"Phase: {previous} -> {phase}", "game.phase");
            GameEvents.OnPhaseChanged?.Invoke(phase);
        }

        public void StartPlanetSelect()
        {
            GameEvents.ClearAll();
            SetPhase(GamePhase.PlanetSelect);
            MainMenuUI.StartOnPlanetSelect = true;
            ChangeScene(Constants.SCENE_MAIN_MENU);
        }

        public void LaunchFromPlanetSelect(int planet, RunMode mode)
        {
            CurrentPlanet = planet;
            CurrentRunMode = mode;
            GD.Print($"[GameManager] Planet={planet}, RunMode={mode} → showing territory map");
            // Go to territory map so player picks a region + site before launching
            ShowTerritory();
        }

        public void StartVineDraft()
        {
            GameEvents.ClearAll();
            ChangeScene(Constants.SCENE_VINE_DRAFT);
        }

        public void StartVineBattle()
        {
            try
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

                SentryInit.AddBreadcrumb($"Starting battle P{CurrentPlanet} mode={CurrentRunMode}", "game.flow");
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("game.planet", CurrentPlanet.ToString());
                    scope.SetTag("game.run_mode", CurrentRunMode.ToString());
                });

                ChangeScene(Constants.SCENE_VINE_BATTLE);
            }
            catch (Exception ex)
            {
                GD.PushError($"[GameManager] StartVineBattle failed: {ex.Message}");
                SentryInit.CaptureException(ex);
                throw;
            }
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
            ChangeScene(Constants.SCENE_VINE_PERK);
        }

        // S1: Simplified — no floor-based point awarding (milestones replace floors)
        public void ShowMetaPerkOrPerkSelect()
        {
            ResourceCarryover = CurrentResources;

            if (MetaSave == null)
                MetaSave = MetaPerkSave.Load();

            // Show meta perk tree if player has unspent points
            if (MetaSave.AvailablePoints > 0)
                ChangeScene(Constants.SCENE_META_PERK);
            else
                ChangeScene(Constants.SCENE_VINE_PERK);
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
            ChangeScene(Constants.SCENE_LEVEL_EDITOR);
            SetPhase(GamePhase.LevelEditor);
        }

        public void ReturnToMainMenu()
        {
            GameEvents.ClearAll();
            Engine.TimeScale = 1.0;
            ChangeScene(Constants.SCENE_MAIN_MENU);
            SetPhase(GamePhase.MainMenu);
        }

        public void OnEnemyReachedCore()
        {
            if (CoreLives <= 0) return;
            CoreLives--;
            GameEvents.OnCoreLivesChanged?.Invoke(CoreLives);

            if (CoreLives <= 0)
            {
                if (IsBossRun)
                    OnBossRunFailed();
                SetPhase(GamePhase.Defeat);
                GameEvents.OnCoreDestroyed?.Invoke();
            }
        }

        // ── UX6: Meta Hub ──

        public void ShowMetaHub()
        {
            GameEvents.ClearAll();
            Engine.TimeScale = 1.0;
            SetPhase(GamePhase.MetaHub);
            ChangeScene(Constants.SCENE_META_HUB);
        }

        // ── UX11: Debrief ──

        /// <summary>
        /// Navigate to the debrief screen. Called after a 2s delay from Victory/Defeat.
        /// </summary>
        public void ShowDebrief()
        {
            Engine.TimeScale = 1.0;

            // Clear the territory site on victory (farming runs)
            // Boss runs clear via OnBossRunComplete() which fires earlier
            if (CurrentPhase != GamePhase.Defeat && !string.IsNullOrEmpty(CurrentTerritorySectionId))
            {
                int reward = TerritoryManager.ClearSite(CurrentTerritorySectionId, MetaSave);
                if (reward > 0)
                {
                    GD.Print($"[GameManager] Site cleared: {CurrentTerritorySectionId} (+{reward} resources)");
                    MetaSave.MetaResources += reward;
                }
                MetaPerkSave.Save(MetaSave);
            }

            SetPhase(GamePhase.Debrief);
            ChangeScene(Constants.SCENE_DEBRIEF);
        }

        /// <summary>
        /// Schedule debrief screen after a delay. Called by VineHUD on Victory/Defeat.
        /// </summary>
        public void ScheduleDebrief(float delaySec = 2.0f)
        {
            GetTree().CreateTimer(delaySec).Timeout += ShowDebrief;
        }

        // ── S4: Territory + Boss Run Flow ──

        public void ShowTerritory()
        {
            GameEvents.ClearAll();
            SetPhase(GamePhase.Territory);
            ChangeScene(Constants.SCENE_TERRITORY);
        }

        public void ShowSuitInventory()
        {
            SetPhase(GamePhase.SuitInventory);
            ChangeScene(Constants.SCENE_SUIT_ARMORY);
        }

        public void ShowRelicInventory()
        {
            GameEvents.ClearAll();
            SetPhase(GamePhase.RelicInventory);
            ChangeScene(Constants.SCENE_RELIC_INVENTORY);
        }

        public void ShowBossConfirmation(int planet, int suitIndex, string sectionId)
        {
            CurrentPlanet = planet;
            EquippedSuitIndex = suitIndex;
            BossSectionId = sectionId;
            SetPhase(GamePhase.BossConfirm);
            ChangeScene(Constants.SCENE_BOSS_CONFIRM);
        }

        /// <summary>
        /// S4: Start a boss run — skip draft, load suit onto grid.
        /// </summary>
        public void StartBossRun(int planet, int suitIndex, string sectionId)
        {
            // Validate territory section
            var section = TerritoryLoader.GetSection(sectionId);
            if (section == null || !section.GatesBoss)
            {
                GD.PushError($"[GameManager] Invalid boss section: {sectionId}");
                return;
            }

            if (!TerritoryLoader.IsUnlocked(sectionId, TerritorySave))
            {
                GD.PushError($"[GameManager] Boss section not unlocked: {sectionId}");
                return;
            }

            // Validate suit
            var suits = SuitManager.GetAll();
            if (suitIndex < 0 || suitIndex >= suits.Length || suits[suitIndex] == null || suits[suitIndex].Consumed)
            {
                GD.PushError($"[GameManager] Invalid suit index: {suitIndex}");
                return;
            }

            CurrentPlanet = planet;
            CurrentRunMode = RunMode.BossRun;
            EquippedSuitIndex = suitIndex;
            BossSectionId = sectionId;

            // Use the suit's role and set available nodes from it
            var suit = suits[suitIndex];
            SelectedRole = suit.Role;
            SelectedMaterialType = suit.Material;

            GD.Print($"[GameManager] Starting boss run on P{planet} section {sectionId} with suit '{suit.Name}'");

            // Skip draft — go straight to battle
            SignalTuningEditor.ResetToDefaults();
            ApplyMetaPerks();
            ActivePerks.Clear();
            ResourceCarryover = 0;
            CurrentMaterials = 0;
            StartVineBattle();
        }

        /// <summary>
        /// S4: Called when boss is defeated in a boss run.
        /// </summary>
        public void OnBossRunComplete()
        {
            if (!IsBossRun || BossSectionId == null) return;

            // Mark section as cleared (old system — backward compat)
            if (!TerritorySave.ClearedBossSections.Contains(BossSectionId))
            {
                TerritorySave.ClearedBossSections.Add(BossSectionId);
                JunkyardTD.TerritorySave.Save(TerritorySave);
            }

            // Mark site as cleared (new region→site system)
            int siteReward = TerritoryManager.ClearSite(BossSectionId, MetaSave);
            MetaPerkSave.Save(MetaSave);

            GameEvents.OnBossSectionCleared?.Invoke(BossSectionId);
            GameEvents.OnBossRunComplete?.Invoke();

            // Award bonus resources
            var section = TerritoryLoader.GetSection(BossSectionId);
            int bonus = (section?.Cost ?? 500) + siteReward;
            AddResources(bonus);

            GD.Print($"[GameManager] Boss run complete! Section {BossSectionId} cleared, +{bonus} resources");
            SetPhase(GamePhase.Victory);
        }

        /// <summary>
        /// S4: Called when spire destroyed during a boss run — destroys the suit.
        /// </summary>
        public void OnBossRunFailed()
        {
            if (!IsBossRun || EquippedSuitIndex == null) return;

            SuitManager.DestroySuit(EquippedSuitIndex.Value);
            GD.Print($"[GameManager] Boss run failed — suit in slot {EquippedSuitIndex} destroyed");

            EquippedSuitIndex = null;
            BossSectionId = null;
        }

        /// <summary>
        /// S4: Unlock a territory section. Returns true if successful.
        /// Uses TotalExtracted as the meta resource currency.
        /// </summary>
        public bool UnlockSection(string sectionId)
        {
            int resources = TotalExtracted;
            if (TerritoryLoader.TryUnlock(sectionId, TerritorySave, ref resources))
            {
                TotalExtracted = resources;
                GameEvents.OnTerritoryUnlocked?.Invoke(sectionId);
                return true;
            }
            return false;
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
