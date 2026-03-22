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
                _state = State.WaitMenu;

                // Subscribe to game events
                GameEvents.OnPhaseChanged += OnPhaseChanged;
                GameEvents.OnEnemyKilled += OnEnemyKilled;
                GameEvents.OnVineNodePlaced += OnNodePlaced;
                GameEvents.OnWaveMilestone += OnWaveMilestone;
            }
        }

        public override void _ExitTree()
        {
            if (_enabled)
            {
                GameEvents.OnPhaseChanged -= OnPhaseChanged;
                GameEvents.OnEnemyKilled -= OnEnemyKilled;
                GameEvents.OnVineNodePlaced -= OnNodePlaced;
                GameEvents.OnWaveMilestone -= OnWaveMilestone;
            }
            Instance = null;
        }

        public override void _Process(double delta)
        {
            if (!_enabled || _state == State.Idle || _state == State.AllComplete) return;

            float dt = (float)delta;
            _stateTimer -= dt;
            if (_stateTimer > 0) return;

            _runTimer += dt;

            switch (_state)
            {
                case State.WaitMenu:
                    HandleWaitMenu();
                    break;
                case State.WaitDraft:
                    HandleWaitDraft();
                    break;
                case State.WaitBattle:
                    HandleWaitBattle();
                    break;
                case State.BuildPhase:
                    HandleBuildPhase();
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

            if (gm.CurrentPhase == GamePhase.MainMenu)
            {
                if (!StartNextRun())
                {
                    _state = State.AllComplete;
                    GD.Print($"[AutoPlayer] All {_runsCompleted} runs complete.");
                    GetTree().Quit(0);
                    return;
                }

                // Navigate: menu → draft
                gm.CurrentPlanet = _currentConfig.Planet;
                gm.StartVineDraft();
                Engine.TimeScale = _currentConfig.GameSpeed;
                _state = State.WaitDraft;
                _stateTimer = STATE_TRANSITION_DELAY;
            }
        }

        private void HandleWaitDraft()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Auto-select role matching config
            gm.SelectedRole = _currentConfig.Role;
            var roleIndex = _currentConfig.Role switch {
                "Obelisk" => 0,
                "Arcanist" => 1,
                "Bruteforge" => 2,
                _ => 0
            };
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(roleIndex);
            gm.StartVineRun();
            _state = State.WaitBattle;
            _stateTimer = STATE_TRANSITION_DELAY * 2; // Extra time for scene load
        }

        private void HandleWaitBattle()
        {
            var gm = GameManager.Instance;
            if (gm?.CurrentPhase == GamePhase.Build)
            {
                _state = State.BuildPhase;
                GD.Print($"[AutoPlayer] Build phase — executing {_currentStrategy.Name} strategy");
            }
        }

        private void HandleBuildPhase()
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;

            int resources = GameManager.Instance?.CurrentResources ?? 0;
            int wave = GameManager.Instance?.CurrentWave ?? 0;

            // Execute strategy placement
            _currentStrategy.OnBuildPhase(grid, resources, wave);

            // Screenshot at intervals
            if (_currentConfig.ScreenshotEveryNWaves > 0 && wave > 0 && wave % _currentConfig.ScreenshotEveryNWaves == 0)
                _currentReport.CaptureScreenshot($"wave_{wave}", GetViewport());

            // Start the wave
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm))
                wm.RequestNextWave();

            _state = State.WavePhase;

            // Check max waves
            if (wave >= _currentConfig.MaxWaves)
            {
                _currentReport.WaveReached = wave;
                _currentReport.Result = "timeout";
                _state = State.RunComplete;
            }
        }

        private void HandleWavePhase(float dt)
        {
            _currentStrategy.OnWavePhase(dt);

            // Check for phase transitions (handled by OnPhaseChanged callback)
        }

        private void HandleDebrief()
        {
            // Collect final stats
            _currentReport.WaveReached = GameManager.Instance?.CurrentWave ?? 0;
            _currentReport.TotalExtracted = GameManager.Instance?.TotalExtracted ?? 0;
            _currentReport.CoreLivesRemaining = GameManager.Instance?.CoreLives ?? 0;
            _currentReport.NodesPlaced = _nodesPlacedThisRun;
            _currentReport.EnemiesKilled = _enemiesKilledThisRun;

            if (_currentConfig.ScreenshotOnDeath)
                _currentReport.CaptureScreenshot("death", GetViewport());

            _state = State.RunComplete;
        }

        private void HandleRunComplete()
        {
            _currentReport.EndRun(_currentReport.Result);
            _currentReport.WriteReport();
            _runsCompleted++;

            GD.Print($"[AutoPlayer] Run {_runsCompleted} complete: {_currentConfig} → {_currentReport.Result}");

            // Return to menu for next run
            Engine.TimeScale = 1f;
            GameManager.Instance?.ReturnToMainMenu();
            _state = State.WaitMenu;
            _stateTimer = STATE_TRANSITION_DELAY;
        }

        // ── Event Handlers ──

        private void OnPhaseChanged(GamePhase phase)
        {
            if (!_enabled) return;

            switch (phase)
            {
                case GamePhase.Build:
                    if (_state == State.WavePhase)
                        _state = State.BuildPhase;
                    break;
                case GamePhase.Wave:
                    if (_state == State.BuildPhase)
                        _state = State.WavePhase;
                    break;
                case GamePhase.Defeat:
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
        private void OnNodePlaced(Node node) => _nodesPlacedThisRun++;

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
            _currentReport = new AutoPlayReport();
            _currentReport.StartRun(_currentConfig);
            _nodesPlacedThisRun = 0;
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
