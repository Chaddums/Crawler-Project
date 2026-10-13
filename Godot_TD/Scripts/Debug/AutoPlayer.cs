using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Automated game player. Drives the game through full run cycles:
    /// Menu → Draft → Build → Wave → ... → Defeat/Victory → Report → Next Run.
    ///
    /// Activation: godot --autoplay
    /// Batch mode: godot --autoplay --batch=configs/
    /// Single config: godot --autoplay --config=configs/turretspam.json
    /// </summary>
    public partial class AutoPlayer : Node
    {
        public static AutoPlayer Instance { get; private set; }

        public bool IsActive => _enabled;

        private enum State
        {
            Idle,           // Not activated
            WaitMenu,       // Waiting for main menu to appear
            WaitDraft,      // Waiting for draft screen
            WaitBattle,     // Waiting for battle scene to load
            BuildPhase,     // Placing towers
            WavePhase,      // Wave in progress
            WaitDebrief,    // Waiting for debrief/death
            RunComplete,    // Run finished, writing report
            AllComplete     // All runs done
        }

        private State _state = State.Idle;
        private bool _enabled;

        // Run queue
        private readonly Queue<AutoPlayerConfig> _configQueue = new();
        private AutoPlayerConfig _currentConfig;
        private IAutoPlayerStrategy _currentStrategy;
        private AutoPlayReport _currentReport;
        private int _runsCompleted;

        // Timing
        private float _stateTimer;
        private float _runTimer;
        private const float STATE_TRANSITION_DELAY = 0.5f; // Brief pause between states

        // Tracking
        private int _nodesPlacedThisRun;
        private int _towersPlacedThisRun;
        private int _enemiesKilledThisRun;

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;

            // Check command-line activation
            // Check both engine args and user args (after --)
            var allArgs = new System.Collections.Generic.List<string>(OS.GetCmdlineArgs());
            allArgs.AddRange(OS.GetCmdlineUserArgs());
            var args = allArgs.ToArray();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--autoplay")
                {
                    _enabled = true;
                }
                else if (args[i] == "--config" && i + 1 < args.Length)
                {
                    var config = AutoPlayerConfig.LoadFromJson(args[i + 1]);
                    _configQueue.Enqueue(config);
                }
                else if (args[i] == "--batch" && i + 1 < args.Length)
                {
                    LoadBatchConfigs(args[i + 1]);
                }
            }

            if (_enabled)
            {
                // Default batch if no configs specified
                if (_configQueue.Count == 0)
                {
                    foreach (var config in AutoPlayerConfig.GetDefaultBatch())
                        _configQueue.Enqueue(config);
                }

                GD.Print($"[AutoPlayer] Activated with {_configQueue.Count} configs queued");
                ErrorCaptureLogger.Install();
                _state = State.WaitMenu;

                // Subscribe to game events
                GameEvents.OnPhaseChanged += OnPhaseChanged;
                GameEvents.OnEnemyKilled += OnEnemyKilled;
                GameEvents.OnEnemyLeaked += OnEnemyLeaked;
                GameEvents.OnVineNodePlaced += OnNodePlaced;
                GameEvents.OnWaveMilestone += OnWaveMilestone;

                // In headless mode, skip MainMenu scene entirely (CEF blocks frame loop)
                if (DisplayServer.GetName() == "headless" || OS.HasFeature("headless"))
                {
                    GD.Print("[AutoPlayer] Headless detected — will jump to battle after autoloads init");
                    _state = State.Idle; // Prevent HandleWaitMenu from also firing
                    CallDeferred(nameof(StartFirstRunHeadless));
                }
            }
        }

        public override void _ExitTree()
        {
            if (_enabled)
            {
                GameEvents.OnPhaseChanged -= OnPhaseChanged;
                GameEvents.OnEnemyKilled -= OnEnemyKilled;
                GameEvents.OnEnemyLeaked -= OnEnemyLeaked;
                GameEvents.OnVineNodePlaced -= OnNodePlaced;
                GameEvents.OnWaveMilestone -= OnWaveMilestone;
            }
            Instance = null;
        }

        public override void _Process(double delta)
        {
            if (!_enabled || _state == State.Idle || _state == State.AllComplete) return;

            float dt = (float)delta;
            // Real (unscaled) time so the perf log cadence doesn't depend on game speed
            float realDt = Engine.TimeScale > 0 ? dt / (float)Engine.TimeScale : dt;
            if (_currentReport != null && _currentConfig != null)
                _currentReport.SamplePerf(realDt, GameManager.Instance?.CurrentWave ?? 0);

            _stateTimer -= dt;
            if (_stateTimer > 0) return;

            _runTimer += dt;

            switch (_state)
            {
                case State.WaitMenu:
                    HandleWaitMenu();
                    break;
                case State.WaitBattle:
                    HandleWaitBattle();
                    break;
                case State.BuildPhase:
                    HandleBuildPhase(dt);
                    break;
                case State.WavePhase:
                    HandleWavePhase(dt);
                    break;
                case State.WaitDebrief:
                    HandleDebrief();
                    break;
                case State.RunComplete:
                    HandleRunComplete();
                    break;
            }
        }

        // ── State Handlers ──

        private void HandleWaitMenu()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Accept MainMenu OR Boot phase (headless may not fully init MainMenu scene)
            if (gm.CurrentPhase == GamePhase.MainMenu || gm.CurrentPhase == GamePhase.Boot)
            {
                if (!StartNextRun())
                {
                    _state = State.AllComplete;
                    GD.Print($"[AutoPlayer] All {_runsCompleted} runs complete.");
                    HeadlessShutdown.QuitClean(this, 0);
                    return;
                }

                // Skip menu/territory/draft — go straight to battle
                gm.CurrentPlanet = _currentConfig.Planet;
                gm.CurrentTerritorySectionId = null;
                gm.SelectedRole = _currentConfig.Role;
                var spireData = SpireData.Get(_currentConfig.Role);
                gm.AvailableNodes = spireData?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
                Engine.TimeScale = _currentConfig.GameSpeed;
                GD.Print($"[AutoPlayer] Jumping to battle: {_currentConfig}");
                // Direct (synchronous) scene changes. Disabling the TransitionManager node
                // instead froze its fade tween, so the first scene change never completed.
                TransitionManager.Instance = null;
                gm.StartVineRun();
                // Re-subscribe after GameEvents.ClearAll inside StartVineRun
                GameEvents.OnPhaseChanged += OnPhaseChanged;
                GameEvents.OnEnemyKilled += OnEnemyKilled;
                GameEvents.OnEnemyLeaked += OnEnemyLeaked;
                GameEvents.OnVineNodePlaced += OnNodePlaced;
                GameEvents.OnWaveMilestone += OnWaveMilestone;
                _state = State.WaitBattle;
                _stateTimer = STATE_TRANSITION_DELAY * 3;
            }
        }

        private void HandleWaitBattle()
        {
            var gm = GameManager.Instance;
            if (gm?.CurrentPhase == GamePhase.Build)
            {
                _state = State.BuildPhase;
                ApplyConfiguredMaterial();
                GD.Print($"[AutoPlayer] Build phase — executing {_currentStrategy.Name} strategy");
            }
        }

        /// <summary>The config's materialType was parsed but never applied, so mining-mode strategies couldn't switch.</summary>
        private void ApplyConfiguredMaterial()
        {
            if (!System.Enum.TryParse<MaterialType>(_currentConfig?.MaterialType, true, out var type)
                || type == MaterialType.None) return;
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null
                && grid.Harvester.SelectedMaterial == MaterialType.None)
            {
                grid.Harvester.SelectMaterialType(type);
                if (GameManager.Instance != null) GameManager.Instance.SelectedMaterialType = type;
            }
        }

        private float _buildWait;

        private void HandleBuildPhase(float dt)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            // A player takes a while between waves (the Spire keeps mining): "buildSeconds"
            // (short of the wave's own auto-start, or the wave goes without the bot)
            float wait = Mathf.Min(_currentConfig.BuildSeconds, Constants.WAVE_PREP_TIME - 2f);
            if (wait > 0f && _buildWait < wait) { _buildWait += dt; return; }
            _buildWait = 0f;

            int resources = GameManager.Instance?.CurrentResources ?? 0;
            int wave = GameManager.Instance?.CurrentWave ?? 0;

            // Check max waves BEFORE starting another one (used to start one extra wave)
            if (wave >= _currentConfig.MaxWaves)
            {
                _currentReport.WaveReached = wave;
                _currentReport.Result = "timeout";
                _state = State.RunComplete;
                return;
            }

            // Execute strategy placement
            _currentStrategy.OnBuildPhase(grid, resources, wave);

            // Screenshot at intervals
            if (!OS.HasFeature("headless") && _currentConfig.ScreenshotEveryNWaves > 0 && wave > 0 && wave % _currentConfig.ScreenshotEveryNWaves == 0)
                _currentReport.CaptureScreenshot($"wave_{wave}", GetViewport());

            // What the wave card says about the wave about to go (balance runs compare it with what happens)
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm0) && _currentReport != null)
            {
                // The whole stack the bot sends ("stack" waves on top), as the card's Send All line reads it
                var t = ThreatModel.Assess(wave + 1, wave + 1 + Mathf.Max(0, _currentConfig.Stack));
                _openLog = new AutoPlayReport.WaveLog
                {
                    Wave = wave + 1, Verdict = t.VerdictWord, Ratio = t.Ratio, PredictedLeaks = t.Leaks, PredictedSpire = t.SpireDamage,
                    BreaksAt = t.Verdict == ThreatModel.Verdict.Breaking ? wave + 1 : ThreatModel.BreaksAt(wave + 2, 6),
                    SpireBefore = grid.Harvester?.CurrentHP ?? 0f, Resources = GameManager.Instance?.CurrentResources ?? 0,
                    Towers = CountTowers(grid), Drills = SalvageDrill.All().Count,
                };
                _leaksThisWave = 0;
                _waveClock = 0f;
                _extractedAtStart = GameManager.Instance?.TotalExtracted ?? 0;
                _closesAtOpen = Income.Closes;
                _currentReport.Waves.Add(_openLog);
                GD.Print($"[AutoPlayer] W{wave + 1}: {t.VerdictWord} {t.Ratio:0.00}x, predicted leaks {t.Leaks} (Spire -{t.SpireDamage:0}), breaks at {_openLog.BreaksAt}, resources {_openLog.Resources}, towers {_openLog.Towers}");
            }

            // Start the wave
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm))
                wm.RequestNextWave();

            _state = State.WavePhase;
        }

        private float _sendAllClock;
        private int _sendAllDoneForWave = -1;
        private AutoPlayReport.WaveLog _openLog;
        private int _extractedAtStart, _closesAtOpen;
        private int _leaksThisWave;
        private float _waveClock;

        private static int CountTowers(VineGrid grid)
        {
            int n = 0;
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.GetNode(x, y) is { } v && !v.IsDestroyed && v.Data?.AutoFires == true) n++;
            return n;
        }

        /// <summary>The wave (or stack) the open log is for has cleared: what actually happened.</summary>
        private void CloseLog()
        {
            if (_openLog == null) return;
            _openLog.Leaks = _leaksThisWave;
            _openLog.SpireAfter = ServiceLocator.TryGet<VineGrid>(out var g) ? g.Harvester?.CurrentHP ?? 0f : 0f;
            _openLog.Seconds = _waveClock;
            if (g != null)
            {
                _openLog.TowersAfter = CountTowers(g);
                // The layout: how far out the guns stand, and how many sit in the dome
                float sum = 0f; int n = 0, dome = 0;
                for (int x = 0; x < g.Width; x++)
                    for (int y = 0; y < g.Height; y++)
                        if (g.GetNode(x, y) is { } v && !v.IsDestroyed && v.Data?.AutoFires == true)
                        {
                            sum += (new Vector2I(x, y) - g.ExitPoint).Length(); n++;
                            if (v.InDome) dome++;
                        }
                _openLog.MeanDist = n > 0 ? sum / n : 0f;
                _openLog.InDome = dome;
            }
            _openLog.Earned = (GameManager.Instance?.TotalExtracted ?? 0) - _extractedAtStart;
            bool cleared = Income.Closes != _closesAtOpen;
            foreach (IncomeSource src in System.Enum.GetValues(typeof(IncomeSource)))
                _openLog.Income[(int)src] = cleared ? Income.Last(src) : Income.Wave(src);
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm))
            {
                _openLog.Stack = wm.CurrentWave - _openLog.Wave;
                _openLog.Pay = wm.LastWaveBonus + wm.LastDrillPay + wm.LastInterest;
            }
            GD.Print($"[AutoPlayer] W{_openLog.Wave}{(_openLog.Stack > 0 ? $"+{_openLog.Stack}" : "")} done: leaks {_openLog.Leaks} (predicted {_openLog.PredictedLeaks}), Spire {_openLog.SpireBefore:0} -> {_openLog.SpireAfter:0}, {_openLog.Seconds:0} s");
            _openLog = null;
        }

        private void HandleWavePhase(float dt)
        {
            _waveClock += dt;
            _currentStrategy.OnWavePhase(dt);

            // Send All (Shift+Space) mid-wave, as a player would
            if (_currentConfig.SendAllAtWaves.Length > 0 && ServiceLocator.TryGet<VineWaveManager>(out var wm) && wm.WaveActive)
            {
                int w = wm.CurrentWave;
                if (System.Array.IndexOf(_currentConfig.SendAllAtWaves, w) >= 0 && _sendAllDoneForWave != w)
                {
                    _sendAllClock += dt;
                    if (_sendAllClock >= 2f)
                    {
                        _sendAllClock = 0f;
                        _sendAllDoneForWave = w;
                        GD.Print($"[AutoPlayer] Send All during wave {w}");
                        wm.SendAllRemaining();
                        _sendAllDoneForWave = wm.CurrentWave;
                    }
                }
            }

            // Check for phase transitions (handled by OnPhaseChanged callback)
        }

        private void HandleDebrief()
        {
            if (_currentConfig.ScreenshotOnDeath && !OS.HasFeature("headless"))
                _currentReport.CaptureScreenshot("death", GetViewport());

            _state = State.RunComplete;
        }

        private void HandleRunComplete()
        {
            // Collect final stats for every outcome (the max-wave "timeout" path skipped
            // HandleDebrief, so its reports had 0 kills / 0 extracted / 0 nodes)
            _currentReport.WaveReached = GameManager.Instance?.CurrentWave ?? 0;
            _currentReport.TotalExtracted = GameManager.Instance?.TotalExtracted ?? 0;
            _currentReport.CoreLivesRemaining = GameManager.Instance?.CoreLives ?? 0;
            _currentReport.NodesPlaced = _nodesPlacedThisRun;
            _currentReport.TowersPlaced = _towersPlacedThisRun;
            _currentReport.EnemiesKilled = _enemiesKilledThisRun;
            if (ErrorCaptureLogger.Instance != null)
            {
                var (errors, warnings, errorCount, warningCount) = ErrorCaptureLogger.Instance.Snapshot();
                _currentReport.Errors.AddRange(errors);
                _currentReport.Warnings.AddRange(warnings);
                _currentReport.ErrorCount = errorCount;
                _currentReport.WarningCount = warningCount;
            }

            _currentReport.EndRun(_currentReport.Result);
            _currentReport.WriteReport();
            _runsCompleted++;

            GD.Print($"[AutoPlayer] Run {_runsCompleted} complete: {_currentConfig} → {_currentReport.Result}");

            Engine.TimeScale = 1f;

            // In headless, skip ReturnToMainMenu (CEF blocks) — jump straight to next run
            if (OS.HasFeature("headless"))
            {
                if (!StartNextRun())
                {
                    _state = State.AllComplete;
                    GD.Print($"[AutoPlayer] All {_runsCompleted} runs complete.");
                    HeadlessShutdown.QuitClean(this, 0);
                    return;
                }

                var gm = GameManager.Instance;
                gm.CurrentPlanet = _currentConfig.Planet;
                gm.CurrentTerritorySectionId = null;
                gm.SelectedRole = _currentConfig.Role;
                var spireData = SpireData.Get(_currentConfig.Role);
                gm.AvailableNodes = spireData?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
                Engine.TimeScale = _currentConfig.GameSpeed;

                // Reset core state from previous defeat before starting next run
                gm.SetCoreLives(Constants.CORE_LIVES);

                GD.Print($"[AutoPlayer] Starting next run: {_currentConfig}");
                gm.StartVineRun();

                // Re-subscribe after ClearAll
                GameEvents.OnPhaseChanged += OnPhaseChanged;
                GameEvents.OnEnemyKilled += OnEnemyKilled;
                GameEvents.OnEnemyLeaked += OnEnemyLeaked;
                GameEvents.OnVineNodePlaced += OnNodePlaced;
                GameEvents.OnWaveMilestone += OnWaveMilestone;

                _state = State.WaitBattle;
                _stateTimer = STATE_TRANSITION_DELAY * 3;
            }
            else
            {
                GameManager.Instance?.ReturnToMainMenu();
                _state = State.WaitMenu;
                _stateTimer = STATE_TRANSITION_DELAY;
            }
        }

        // ── Event Handlers ──

        private void OnPhaseChanged(GamePhase phase)
        {
            if (!_enabled) return;

            switch (phase)
            {
                case GamePhase.WaveComplete:
                    CloseLog();
                    break;
                case GamePhase.Build:
                    CloseLog();
                    if (_state == State.WavePhase)
                        _state = State.BuildPhase;
                    break;
                case GamePhase.Wave:
                    if (_state == State.BuildPhase)
                        _state = State.WavePhase;
                    break;
                case GamePhase.Defeat:
                    CloseLog();
                    _currentReport.Result = "defeat";
                    _state = State.WaitDebrief;
                    _stateTimer = 1f; // Let debrief render
                    break;
                case GamePhase.Victory:
                    _currentReport.Result = "victory";
                    _state = State.WaitDebrief;
                    _stateTimer = 1f;
                    break;
            }
        }

        private void OnEnemyKilled(Node enemy) => _enemiesKilledThisRun++;
        private void OnEnemyLeaked(Node enemy, Vector3 at)
        {
            if (enemy is not VineEnemy e) return;
            _leaksThisWave++;
            if (_openLog == null) return;
            if (e.IsFlying) _openLog.LeakFly++;
            if (e.IsArmoured) _openLog.LeakArmour++;
            if (e.IsShielded) _openLog.LeakShield++;
            if (e.IsBoss || e.IsCommander || e.IsAscendant) _openLog.LeakBig++;
        }
        private void OnNodePlaced(Node node)
        {
            _nodesPlacedThisRun++;
            // Towers = nodes that fire on their own (report field was never populated)
            if (node is VineNode vn && vn.Data?.AutoFires == true) _towersPlacedThisRun++;
        }

        private void OnWaveMilestone(int wave, string type)
        {
            _currentStrategy.OnMilestone(wave, type);
        }

        // ── Run Management ──

        private bool StartNextRun()
        {
            if (_configQueue.Count == 0) return false;

            _currentConfig = _configQueue.Dequeue();
            _currentStrategy = StrategyRegistry.Create(_currentConfig.Strategy);
            if (_currentStrategy is IConfigurableStrategy configurable) configurable.Configure(_currentConfig);
            _currentReport = new AutoPlayReport();
            _currentReport.StartRun(_currentConfig);
            _nodesPlacedThisRun = 0;
            _towersPlacedThisRun = 0;
            ErrorCaptureLogger.Instance?.Reset();
            _enemiesKilledThisRun = 0;
            _runTimer = 0;

            // Wire AutoBugger context
            if (AutoBugger.Instance != null)
            {
                AutoBugger.Instance.CurrentStrategyName = _currentConfig.Strategy;
                AutoBugger.Instance.CurrentRunConfig = _currentConfig.ToString();
            }

            GD.Print($"[AutoPlayer] Starting run: {_currentConfig}");
            return true;
        }

        private void StartFirstRunHeadless()
        {
            if (!StartNextRun()) { HeadlessShutdown.QuitClean(this, 0); return; }

            var gm = GameManager.Instance;
            if (gm == null) { GD.PrintErr("[AutoPlayer] No GameManager!"); HeadlessShutdown.QuitClean(this, 1); return; }

            gm.CurrentPlanet = _currentConfig.Planet;
            gm.CurrentTerritorySectionId = null;
            gm.SelectedRole = _currentConfig.Role;
            var spireData = SpireData.Get(_currentConfig.Role);
            gm.AvailableNodes = spireData?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            Engine.TimeScale = _currentConfig.GameSpeed;

            // Null out TransitionManager so ChangeScene falls through to direct scene change
            // (async tweens don't tick in headless)
            TransitionManager.Instance = null;

            GD.Print($"[AutoPlayer] Headless start: {_currentConfig}");

            // Null out TransitionManager so ChangeScene falls through to direct scene change
            // (async tweens don't tick in headless)
            TransitionManager.Instance = null;

            // Use normal StartVineRun flow — it handles ClearAll + scene change
            gm.StartVineRun();

            // Re-subscribe after ClearAll inside StartVineRun
            GameEvents.OnPhaseChanged += OnPhaseChanged;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
                GameEvents.OnEnemyLeaked += OnEnemyLeaked;
            GameEvents.OnVineNodePlaced += OnNodePlaced;
            GameEvents.OnWaveMilestone += OnWaveMilestone;

            _state = State.WaitBattle;
            _stateTimer = 1f;
        }

        private void LoadBatchConfigs(string dirPath)
        {
            using var dir = DirAccess.Open(dirPath);
            if (dir == null)
            {
                GD.PrintErr($"[AutoPlayer] Batch directory not found: {dirPath}");
                return;
            }

            dir.ListDirBegin();
            string fileName;
            while ((fileName = dir.GetNext()) != "")
            {
                if (fileName.EndsWith(".json"))
                {
                    var config = AutoPlayerConfig.LoadFromJson($"{dirPath}/{fileName}");
                    _configQueue.Enqueue(config);
                }
            }
            dir.ListDirEnd();
            GD.Print($"[AutoPlayer] Loaded {_configQueue.Count} configs from {dirPath}");
        }
    }
}
