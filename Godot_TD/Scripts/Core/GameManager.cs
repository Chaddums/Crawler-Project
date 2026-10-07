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

        // ── Per-run outcome (reset in StartVineBattle) ──
        /// <summary>True if the run ended in Victory. Captured before the phase moves to Debrief.</summary>
        public bool LastRunVictory { get; private set; }
        /// <summary>Farming run reached the site's clear wave (site progress already persisted).</summary>
        public bool SiteSecuredThisRun { get; private set; }
        /// <summary>Site cleared for the first time this run (drives the debrief conquest banner). Null on replays.</summary>
        public string NewlyClearedSiteId { get; private set; }
        /// <summary>Snapshot of the build taken when the run ended, for suit capture on the debrief screen.</summary>
        public SuitSaveData PendingSuitSnapshot { get; private set; }
        private bool _debriefScheduled;
        private bool _debriefShown;

        // ── Per-run modifiers from territory (computed in StartVineBattle) ──
        // Site bonus_extraction_mult and region conquest buffs were loaded and shown in the UI
        // but never applied; these are the single source for the multipliers.
        /// <summary>Enemy drops + harvester income (conquest "resource_mult").</summary>
        public float RunResourceMult { get; private set; } = 1f;
        /// <summary>Wave-clear extraction bonus (site bonus_extraction_mult x conquest "extraction_mult").</summary>
        public float RunExtractionMult { get; private set; } = 1f;
        /// <summary>Tower damage (conquest "tower_damage_mult").</summary>
        public float RunTowerDamageMult { get; private set; } = 1f;
        /// <summary>Relic drop chance (conquest "relic_drop_mult").</summary>
        public float RunRelicDropMult { get; private set; } = 1f;

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

        private bool _quitting;

        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest && !_quitting)
            {
                _quitting = true;
                HeadlessShutdown.QuitClean(this, 0);
            }
        }

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;
            // Closing the window with a battle loaded crashed on exit (C# wrappers finalized during
            // engine teardown, exit 134). Take the close request and tear down first.
            GetTree().AutoAcceptQuit = false;
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

            // Any defeat (Spire destroyed or core lives exhausted) costs the suit on a boss run
            if (phase == GamePhase.Defeat && previous != GamePhase.Defeat && IsBossRun)
                OnBossRunFailed();

            GameEvents.OnPhaseChanged?.Invoke(phase);
        }

        public void StartPlanetSelect()
        {
            // Planet select only exists in the CEF menu. The code-built fallback has no
            // picker, so reloading the menu just looped back to it — go to Territory
            // (which has planet tabs) instead.
            if (!CefHelper.Available)
            {
                ShowTerritory();
                return;
            }

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
                ResetRunOutcome();
                // Don't carry the previous run's phase (e.g. Defeat) into the new battle —
                // the intro moves BattleLoading → Build.
                SetPhase(GamePhase.BattleLoading);
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
            // Farming run — drop any boss-run state left over from a previous run
            if (CurrentRunMode == RunMode.BossRun)
                CurrentRunMode = RunMode.Harvest;
            EquippedSuitIndex = null;
            BossSectionId = null;

            SignalTuningEditor.ResetToDefaults();
            ApplyMetaPerks();
            ActivePerks.Clear();
            ResourceCarryover = 0;
            CurrentMaterials = 0;
            SelectedMaterialType = null;
            StartVineBattle();
        }

        /// <summary>Clear everything that describes how the previous run went.</summary>
        private void ResetRunOutcome()
        {
            DifficultyMultiplier = 1f;
            LastRunVictory = false;
            SiteSecuredThisRun = false;
            MetaPointsEarnedThisRun = 0;
            MetaPointsByWave.Clear();
            NewlyClearedSiteId = null;
            PendingSuitSnapshot = null;
            _debriefScheduled = false;
            _debriefShown = false;
            ComputeRunModifiers();
        }

        private void ComputeRunModifiers()
        {
            MetaSave ??= MetaPerkSave.Load();
            var site = string.IsNullOrEmpty(CurrentTerritorySectionId)
                ? null : TerritoryManager.GetSite(CurrentTerritorySectionId);

            RunResourceMult = TerritoryManager.GetBuffMultiplier(CurrentPlanet, "resource_mult", MetaSave);
            RunExtractionMult = (site?.BonusExtractionMult ?? 1f)
                * TerritoryManager.GetBuffMultiplier(CurrentPlanet, "extraction_mult", MetaSave);
            RunTowerDamageMult = TerritoryManager.GetBuffMultiplier(CurrentPlanet, "tower_damage_mult", MetaSave);
            RunRelicDropMult = TerritoryManager.GetBuffMultiplier(CurrentPlanet, "relic_drop_mult", MetaSave);

            GD.Print($"[GameManager] Run modifiers P{CurrentPlanet}: resources x{RunResourceMult:F2}, " +
                     $"extraction x{RunExtractionMult:F2}, tower dmg x{RunTowerDamageMult:F2}, relic drops x{RunRelicDropMult:F2}");
        }

        /// <summary>
        /// Farming runs never "win" — a site counts as secured once the run survives
        /// to the site's clear wave. Progress is persisted immediately so quitting or
        /// crashing afterwards can't lose it. Called by VineWaveManager on wave clear.
        /// </summary>
        public void CheckSiteSecured(int wave)
        {
            if (SiteSecuredThisRun || IsBossRun || string.IsNullOrEmpty(CurrentTerritorySectionId)) return;

            var site = TerritoryManager.GetSite(CurrentTerritorySectionId);
            if (site == null || site.IsBossSite || wave < site.ClearWave) return;

            SiteSecuredThisRun = true;
            MetaSave ??= MetaPerkSave.Load();
            bool isNew = !TerritoryManager.IsSiteCleared(site.Id, MetaSave);
            int reward = TerritoryManager.ClearSite(site.Id, MetaSave);
            MetaPerkSave.Save(MetaSave);

            if (isNew) NewlyClearedSiteId = site.Id;
            if (reward > 0) AddResources(reward);

            GD.Print($"[GameManager] P{CurrentPlanet}-W{wave} site secured: {site.Id} (new={isNew}, +{reward})");
            GameEvents.OnAnnouncement?.Invoke(reward > 0
                ? $"SITE SECURED — {site.Name} (+{reward})"
                : $"SITE SECURED — {site.Name}");
        }

        /// <summary>Meta perk points earned during the current run (shown on the debrief).</summary>
        public int MetaPointsEarnedThisRun { get; private set; }

        /// <summary>Meta points paid this run, by milestone wave (the perk pick shows them).</summary>
        public System.Collections.Generic.Dictionary<int, int> MetaPointsByWave { get; } = new();

        /// <summary>
        /// Award meta perk points for a wave milestone, once per planet per milestone
        /// (milestones.json "metaPoints"). Saved immediately, like a secured site.
        /// </summary>
        public void AwardMetaPoints(int wave, int points)
        {
            if (points <= 0) return;
            MetaSave ??= MetaPerkSave.Load();
            string key = $"P{CurrentPlanet}-W{wave}";
            if (MetaSave.ClearedMilestones.ContainsKey(key)) return;

            MetaSave.ClearedMilestones[key] = points;
            MetaSave.AvailablePoints += points;
            MetaPointsEarnedThisRun += points;
            MetaPointsByWave[wave] = points;
            MetaPerkSave.Save(MetaSave);

            GD.Print($"[GameManager] {key} meta perk point(s) +{points} (unspent {MetaSave.AvailablePoints})");
            GameEvents.OnAnnouncement?.Invoke(points == 1
                ? "PERK POINT EARNED. Spend it on the Perk Tree between runs"
                : $"+{points} PERK POINTS. Spend them on the Perk Tree between runs");
        }

        /// <summary>Between-runs meta perk tree (reached from the Command Center).</summary>
        public void ShowMetaPerkTree()
        {
            GameEvents.ClearAll();
            Engine.TimeScale = 1.0;
            SetPhase(GamePhase.MetaHub);
            ChangeScene(Constants.SCENE_META_PERK);
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
            // Only live gameplay can cost lives — not a leak after Victory/Defeat
            // (e.g. a boss-run win must never turn into a loss that destroys the suit)
            if (CurrentPhase != GamePhase.Wave && CurrentPhase != GamePhase.Build
                && CurrentPhase != GamePhase.WaveComplete) return;
            if (CoreLives <= 0) return;
            CoreLives--;
            GameEvents.OnCoreLivesChanged?.Invoke(CoreLives);

            if (CoreLives <= 0)
            {
                SetPhase(GamePhase.Defeat); // SetPhase handles boss-run suit loss
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
            if (_debriefShown) return; // Victory/Defeat can both fire; only one debrief per run
            _debriefShown = true;

            Engine.TimeScale = 1.0;
            GetTree().Paused = false;

            // Capture outcome BEFORE the phase changes — DebriefScreen can't read it from
            // CurrentPhase because that is Debrief by the time the screen loads.
            LastRunVictory = CurrentPhase == GamePhase.Victory;

            // Snapshot the build while the battle scene (and its grid) still exists —
            // the grid is freed on scene change, so the debrief can't read it later.
            if (CurrentRunMode != RunMode.BossRun && ServiceLocator.TryGet<VineGrid>(out var grid)
                && IsInstanceValid(grid))
            {
                PendingSuitSnapshot = SuitManager.CreateSnapshot(grid, SelectedRole, CurrentPlanet,
                    SelectedMaterialType ?? MaterialType.None);
            }

            SetPhase(GamePhase.Debrief);
            ChangeScene(Constants.SCENE_DEBRIEF);
        }

        /// <summary>
        /// Schedule debrief screen after a delay. Called by VineHUD on Victory/Defeat.
        /// Idempotent per run — Spire destruction and core-lives loss can both report Defeat.
        /// </summary>
        /// <summary>
        /// Test hook: keep the battle scene alive after Victory/Defeat instead of moving to
        /// the debrief (suites that deliberately lose a run and keep probing the scene).
        /// </summary>
        public bool SuppressAutoDebrief { get; set; }

        /// <summary>
        /// Test hook: apply perk_select milestones inline (first offered perk) instead of
        /// opening the overlay, which pauses the run until someone clicks.
        /// </summary>
        public bool AutoResolvePerks { get; set; }

        /// <summary>Test hook: show the material picker even under the test harness sandbox.</summary>
        public bool PromptMaterialUnderTestHarness { get; set; }

        public void ScheduleDebrief(float delaySec = 2.0f)
        {
            if (SuppressAutoDebrief) return;
            if (_debriefScheduled) return;
            _debriefScheduled = true;
            GetTree().CreateTimer(delaySec).Timeout += () =>
            {
                // Player may have left the battle (quit to menu) before the timer fired
                if (CurrentPhase == GamePhase.Victory || CurrentPhase == GamePhase.Defeat)
                    ShowDebrief();
            };
        }

        /// <summary>
        /// End the current run voluntarily (pause menu). Goes through the debrief so the
        /// run's extraction is still banked — no run is wasted.
        /// </summary>
        public void EndRunEarly()
        {
            GetTree().Paused = false;
            if (IsBossRun)
            {
                // Walking away from a boss run is a loss
                SetPhase(GamePhase.Defeat);
            }
            ShowDebrief();
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
            // Validate territory site
            var site = TerritoryManager.GetSite(sectionId);
            if (site == null || !site.IsBossSite)
            {
                GD.PushError($"[GameManager] Invalid boss site: {sectionId}");
                return;
            }

            // Boss is playable once its region is reachable (prior regions conquered).
            // (Previously checked IsSiteCleared, so a boss could only be started after beating it.)
            if (!TerritoryManager.IsSiteAccessible(sectionId, MetaSave))
            {
                GD.PushError($"[GameManager] Boss site not accessible yet: {sectionId}");
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
            // Boss site drives the map layout (VineBattleScene reads CurrentTerritorySection)
            CurrentTerritorySectionId = sectionId;

            // Use the suit's role and set available nodes from it (draft is skipped)
            var suit = suits[suitIndex];
            SelectedRole = suit.Role;
            SelectedMaterialType = suit.Material;
            AvailableNodes = SpireData.Get(suit.Role)?.Nodes ?? VineDraftScreen.GetRoleNodes(0);

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

            // Mark site as cleared (region→site system; persisted in MetaSave)
            MetaSave ??= MetaPerkSave.Load();
            bool isNew = !TerritoryManager.IsSiteCleared(BossSectionId, MetaSave);
            int siteReward = TerritoryManager.ClearSite(BossSectionId, MetaSave);
            MetaPerkSave.Save(MetaSave);
            if (isNew) NewlyClearedSiteId = BossSectionId;

            GameEvents.OnBossSectionCleared?.Invoke(BossSectionId);
            GameEvents.OnBossRunComplete?.Invoke();

            // Site reward flows through the run's extraction, banked at the debrief
            if (siteReward > 0)
                AddResources(siteReward);

            GD.Print($"[GameManager] Boss run complete! Site {BossSectionId} cleared (new={isNew}), +{siteReward} resources");
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

        /// <summary>
        /// Return spent resources (e.g. selling a tower). Not extraction — refunds used to go
        /// through AddResources, so a buy/sell loop inflated TotalExtracted, which is banked
        /// 1:1 into meta resources at the debrief.
        /// </summary>
        public void RefundResources(int amount)
        {
            if (amount <= 0) return;
            CurrentResources += amount;
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

        /// <summary>A battle is live (loading, building, fighting, paused or just ended).</summary>
        public bool IsInRun => CurrentPhase is GamePhase.BattleLoading or GamePhase.Build or GamePhase.Wave
            or GamePhase.WaveComplete or GamePhase.Paused or GamePhase.Victory or GamePhase.Defeat;

        public static void ToggleFullscreen()
        {
            if (DisplayServer.GetName() == "headless") return;
            DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen
                ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
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

            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F11)
            {
                // F11 is labelled "Fullscreen [F11]" in the pause menu, but it used to open the level
                // editor from any phase — throwing away a live run (no debrief, tree left paused).
                // Fullscreen on F11; the editor moves to Ctrl+F11, debug builds, outside a run.
                if (key.CtrlPressed)
                {
                    if (OS.IsDebugBuild() && !IsInRun && CurrentPhase != GamePhase.LevelEditor)
                        StartLevelEditor();
                }
                else
                {
                    ToggleFullscreen();
                }
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
