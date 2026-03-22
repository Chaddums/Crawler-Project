using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Passive game health monitor. Always-on autoload that watches for:
    /// - FPS drops below threshold
    /// - Game state violations (negative resources, impossible states)
    /// - Signal anomalies (events fired out of order)
    /// - Unhandled errors
    /// Auto-files bug reports with context. Works alongside AutoPlayer.
    /// </summary>
    public partial class AutoBugger : Node
    {
        public static AutoBugger Instance { get; private set; }

        // Configuration
        private const float FPS_THRESHOLD = 30f;
        private const float FPS_DROP_DURATION = 2f;       // Must be below threshold for this long
        private const float FRAME_SPIKE_MS = 100f;         // Single frame spike threshold
        private const int MAX_ENEMY_COUNT = 200;
        private const float CHECK_INTERVAL = 1f;           // How often to run state checks
        private const int MAX_AUTO_BUGS_PER_SESSION = 50;  // Don't flood

        // State tracking
        private float _lowFpsTimer;
        private float _checkTimer;
        private int _lastWaveNumber = -1;
        private int _lastCoreLives = -1;
        private int _lastResources = -1;
        private bool _waveActive;
        private bool _waveCompletedForCurrent;
        private int _autoBugCount;
        private float _sessionTime;

        // FPS tracking
        private float _peakFps;
        private float _minFps = float.MaxValue;
        private float _fpsAccumulator;
        private int _fpsFrames;

        // Error collection
        private readonly List<string> _recentErrors = new();
        private readonly HashSet<string> _filedBugHashes = new(); // Avoid duplicate bug reports

        // AutoPlayer integration
        public string CurrentStrategyName { get; set; } = "";
        public string CurrentRunConfig { get; set; } = "";

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;

            // Subscribe to game events for anomaly detection
            GameEvents.OnWaveStarted += OnWaveStarted;
            GameEvents.OnWaveCompleted += OnWaveCompleted;
            GameEvents.OnCoreLivesChanged += OnCoreLivesChanged;
            GameEvents.OnResourcesChanged += OnResourcesChanged;
            GameEvents.OnPhaseChanged += OnPhaseChanged;

            GD.Print("[AutoBugger] Active — monitoring game health");
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveStarted -= OnWaveStarted;
            GameEvents.OnWaveCompleted -= OnWaveCompleted;
            GameEvents.OnCoreLivesChanged -= OnCoreLivesChanged;
            GameEvents.OnResourcesChanged -= OnResourcesChanged;
            GameEvents.OnPhaseChanged -= OnPhaseChanged;
            Instance = null;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _sessionTime += dt;

            // ── FPS monitoring ──
            float fps = (float)Engine.GetFramesPerSecond();
            _fpsAccumulator += fps;
            _fpsFrames++;
            if (fps > _peakFps) _peakFps = fps;
            if (fps < _minFps && fps > 0) _minFps = fps;

            // Frame spike detection
            float frameMs = dt * 1000f;
            if (frameMs > FRAME_SPIKE_MS && _sessionTime > 3f) // Skip first 3s (loading)
            {
                FileBug("performance", "medium",
                    $"Frame spike: {frameMs:F0}ms (threshold: {FRAME_SPIKE_MS}ms)",
                    $"Wave: {_lastWaveNumber}, FPS: {fps:F0}");
            }

            // Sustained FPS drop
            if (fps < FPS_THRESHOLD && fps > 0)
            {
                _lowFpsTimer += dt;
                if (_lowFpsTimer >= FPS_DROP_DURATION)
                {
                    FileBug("performance", "medium",
                        $"FPS below {FPS_THRESHOLD} for {_lowFpsTimer:F1}s (current: {fps:F0})",
                        $"Wave: {_lastWaveNumber}");
                    _lowFpsTimer = 0; // Reset to avoid spam
                }
            }
            else
            {
                _lowFpsTimer = 0;
            }

            // ── Periodic state checks ──
            _checkTimer -= dt;
            if (_checkTimer <= 0)
            {
                _checkTimer = CHECK_INTERVAL;
                RunStateChecks();
            }
        }

        // ── State violation checks ──

        private void RunStateChecks()
        {
            // Enemy count runaway
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            if (enemies.Count > MAX_ENEMY_COUNT)
            {
                FileBug("state", "high",
                    $"Enemy count exceeds {MAX_ENEMY_COUNT}: {enemies.Count}",
                    $"Wave: {_lastWaveNumber} — possible runaway spawning");
            }

            // Orphaned node leak detection
            int nodeCount = GetTree().Root.GetChildCount();
            // Could track trending here with a ring buffer, skip for now
        }

        // ── Event handlers for anomaly detection ──

        private void OnWaveStarted(int waveNum)
        {
            // Check: previous wave should have completed first
            if (_waveActive && !_waveCompletedForCurrent && _lastWaveNumber > 0)
            {
                FileBug("signal", "low",
                    $"OnWaveStarted({waveNum}) fired without OnWaveCompleted for wave {_lastWaveNumber}",
                    "Signal ordering anomaly");
            }

            _lastWaveNumber = waveNum;
            _waveActive = true;
            _waveCompletedForCurrent = false;

            // Wave regression
            if (waveNum < _lastWaveNumber - 1 && waveNum > 0)
            {
                FileBug("state", "high",
                    $"Wave number regressed: {_lastWaveNumber} → {waveNum}",
                    "Should never go backward in continuous mode");
            }
        }

        private void OnWaveCompleted(int waveNum)
        {
            _waveCompletedForCurrent = true;
            _waveActive = false;
        }

        private void OnCoreLivesChanged(int lives)
        {
            if (lives < 0)
            {
                FileBug("state", "high",
                    $"Core lives went negative: {lives}",
                    "Should be clamped to 0");
            }
            _lastCoreLives = lives;
        }

        private void OnResourcesChanged(int resources)
        {
            if (resources < 0)
            {
                FileBug("state", "high",
                    $"Resources went negative: {resources}",
                    "Should be clamped to 0");
            }

            // Suspicious large delta
            if (_lastResources >= 0)
            {
                int delta = Math.Abs(resources - _lastResources);
                if (delta > 1000)
                {
                    FileBug("signal", "low",
                        $"Large resource delta in single event: {_lastResources} → {resources} (delta: {delta})",
                        "Possibly duplicated event or exploit");
                }
            }
            _lastResources = resources;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            // Build phase without wave completing
            if (phase == GamePhase.Build && _waveActive && !_waveCompletedForCurrent && _lastWaveNumber > 0)
            {
                FileBug("signal", "low",
                    $"Phase changed to Build while wave {_lastWaveNumber} still active",
                    "OnWaveCompleted may not have fired");
            }
        }

        // ── Bug filing ──

        /// <summary>
        /// Programmatically file a bug report. Deduplicates by hash.
        /// </summary>
        public void FileBug(string category, string severity, string description, string context = "")
        {
            if (_autoBugCount >= MAX_AUTO_BUGS_PER_SESSION) return;

            // Deduplicate: same description = same bug
            string hash = $"{category}:{description}".GetHashCode().ToString("X8");
            if (_filedBugHashes.Contains(hash)) return;
            _filedBugHashes.Add(hash);
            _autoBugCount++;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string folderName = $"auto-{timestamp}";
            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
            string reportDir = Path.Combine(godotDir, "bugs", folderName);

            try
            {
                Directory.CreateDirectory(reportDir);

                float avgFps = _fpsFrames > 0 ? _fpsAccumulator / _fpsFrames : 0;
                string metrics = $@"- Category: {category}
- FPS: {Engine.GetFramesPerSecond():F0} (avg: {avgFps:F0}, min: {_minFps:F0}, peak: {_peakFps:F0})
- Phase: {GameManager.Instance?.CurrentPhase}
- Wave: {_lastWaveNumber}
- Core Lives: {_lastCoreLives}
- Resources: {_lastResources}
- Session Time: {_sessionTime:F0}s
- Auto-Bug #: {_autoBugCount}/{MAX_AUTO_BUGS_PER_SESSION}";

                if (!string.IsNullOrEmpty(CurrentStrategyName))
                    metrics += $"\n- AutoPlayer Strategy: {CurrentStrategyName}";
                if (!string.IsNullOrEmpty(CurrentRunConfig))
                    metrics += $"\n- Run Config: {CurrentRunConfig}";

                string report = $@"# [AUTO] Bug Report: {description}
**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}
**Severity:** {severity}
**Category:** {category}

## Description
{description}

## Context
{context}

## Metrics
{metrics}

## Recent Errors
{(_recentErrors.Count > 0 ? string.Join("\n", _recentErrors) : "(none)")}
";

                File.WriteAllText(Path.Combine(reportDir, "report.md"), report);
                GD.Print($"[AutoBugger] Filed: [{severity}] {description} → bugs/{folderName}/");
            }
            catch (Exception e)
            {
                GD.PrintErr($"[AutoBugger] Failed to file bug: {e.Message}");
            }
        }

        /// <summary>
        /// Log an error for inclusion in the next bug report.
        /// Called by external systems (ErrorCatcher, etc.)
        /// </summary>
        public void LogError(string error)
        {
            _recentErrors.Add($"[{DateTime.Now:HH:mm:ss}] {error}");
            if (_recentErrors.Count > 20)
                _recentErrors.RemoveAt(0);

            // Auto-file on exception-level errors
            if (error.Contains("Exception") || error.Contains("NullReference"))
            {
                FileBug("crash", "high", error);
            }
        }

        /// <summary>
        /// Get session FPS stats for AutoPlayer reports.
        /// </summary>
        public (float peak, float min, float avg) GetFpsStats()
        {
            float avg = _fpsFrames > 0 ? _fpsAccumulator / _fpsFrames : 0;
            return (_peakFps, _minFps == float.MaxValue ? 0 : _minFps, avg);
        }
    }
}
