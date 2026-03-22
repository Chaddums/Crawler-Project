using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Spawns enemies for Vine Logic TD waves.
    /// S2: Continuous wave system — no floor hierarchy, waves escalate until defeat.
    /// </summary>
    public partial class VineWaveManager : Node
    {
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private int _currentPlanet;
        private int _currentWave;
        private bool _waveActive;
        private int _enemiesAlive;

        // S2: Hand-crafted wave data (JSON or hardcoded fallback)
        private List<VineWaveData> _handCraftedWaves;

        // S2: Milestone data
        private List<MilestoneData> _milestones;

        // Active surges
        private readonly List<ActiveSurge> _activeSurges = new();
        private readonly RandomNumberGenerator _rng = new();

        // Completion tracking
        private VineWaveData _currentWaveData;
        private float _completionTimer;
        private int _killCount;

        // Auto-timer & wave stacking
        private float _autoStartTimer = -1f;
        private int _pendingBonusResources;

        public int CurrentWave => _currentWave;
        public bool WaveActive => _waveActive;

        /// <summary>
        /// S2: Returns hand-crafted wave count for backward compatibility.
        /// VineHUD reads this to display "Wave X / Y".
        /// </summary>
        [Obsolete("S2: Use hand-crafted count for HUD display. Continuous mode has no fixed total.")]
        public int TotalWavesThisFloor => _handCraftedWaves?.Count ?? 0;

        public float AutoStartTimer => _autoStartTimer;

        /// <summary>
        /// S2: Always true — continuous mode never runs out of waves.
        /// </summary>
        public bool HasMoreWaves => true;

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<VineGrid>();
            _pathfinder = ServiceLocator.Get<VinePathfinder>();
            _currentPlanet = GameManager.Instance?.CurrentPlanet ?? 1;
            _currentWave = 0;

            // S2: Load all waves for planet (single file)
            _handCraftedWaves = VineWaveLoader.LoadPlanetWaves(_currentPlanet);

            // S2: Load milestones
            _milestones = VineWaveLoader.LoadMilestones(_currentPlanet);

            GameEvents.OnEnemyKilled += OnEnemyDied;
            GameEvents.OnEnemyLeaked += OnEnemyLeaked;
            GameEvents.OnPhaseChanged += OnPhaseChanged;

            ServiceLocator.Register(this);
        }

        public void RequestNextWave()
        {
            _autoStartTimer = -1f;

            if (!_waveActive)
            {
                StartWave();
            }
            else
            {
                StartWave(stack: true);
            }
        }

        public void SendAllRemaining()
        {
            _autoStartTimer = -1f;
            if (!_waveActive)
                StartWave();
            // S2: Stack up to 3 additional waves (continuous mode, avoid infinite loop)
            int stacked = 0;
            while (stacked < 3)
            {
                StartWave(stack: true);
                stacked++;
            }
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            // For wave 0 on start, timer is started by OnHarvesterPlaced (called from HUD).
            // For subsequent waves, timer is started by CompleteWave directly.
        }

        /// <summary>
        /// Called by VineHUD when it detects the harvester has been placed.
        /// Starts the first-wave countdown.
        /// </summary>
        public void OnHarvesterPlaced()
        {
            if (_currentWave == 0 && _autoStartTimer < 0)
                _autoStartTimer = Constants.WAVE_PREP_TIME;
        }

        public void StartWave(bool stack = false)
        {
            _currentWave++;
            VineWaveData data = null;

            // S2: Use hand-crafted wave if available, else generate procedurally
            if (_handCraftedWaves != null && _currentWave <= _handCraftedWaves.Count)
            {
                data = _handCraftedWaves[_currentWave - 1];
            }
            else
            {
                data = VineWaveLoader.GenerateWave(_currentWave, _handCraftedWaves, _rng);
            }

            if (data == null)
            {
                GD.PrintErr($"[VineWaveManager] Failed to get wave data for wave {_currentWave}");
                _currentWave--;
                return;
            }

            _waveActive = true;

            if (!stack)
            {
                _currentWaveData = data;
                _activeSurges.Clear();
                _completionTimer = 0f;
                _killCount = 0;
                _pendingBonusResources = 0;
            }

            // S2: Extraction bonus replaces flat BonusResources for procedural waves
            if (_currentWave <= (_handCraftedWaves?.Count ?? 0))
            {
                _pendingBonusResources += data.BonusResources;
            }
            else
            {
                // Procedural waves use extraction curve
                if (ServiceLocator.TryGet<DifficultyScaler>(out var scaler))
                    _pendingBonusResources += scaler.ComputeExtractionBonus(_currentWave);
                else
                    _pendingBonusResources += Mathf.RoundToInt(Constants.EXTRACTION_BASE * Mathf.Pow(Constants.EXTRACTION_GROWTH, _currentWave - 1));
            }

            string addr = $"P{_currentPlanet}-W{_currentWave}";

            foreach (var surge in data.Surges)
            {
                // S2: Apply wave-based scaling to surge data
                var scaledSurge = ApplyWaveScaling(surge, _currentWave);
                _activeSurges.Add(new ActiveSurge {
                    Data = scaledSurge,
                    Remaining = scaledSurge.Count,
                    Timer = scaledSurge.StartDelay,
                    Accumulator = 0f,
                    UseAccumulator = scaledSurge.UseAccumulator
                });
            }

            GD.Print($"[VineWaveManager] {addr} \"{data.Name}\" — {data.Surges.Count} surges, mode={data.CompletionMode}{(stack ? " [STACKED]" : "")}");

            if (GameManager.Instance != null)
                GameManager.Instance.CurrentWave = _currentWave;
            if (!stack)
                GameManager.Instance?.SetPhase(GamePhase.Wave);
            GameEvents.OnWaveStarted?.Invoke(_currentWave);
        }

        /// <summary>
        /// S2: Apply wave-based scaling to a surge. Clones the surge and multiplies HP/speed/count.
        /// </summary>
        private SurgeData ApplyWaveScaling(SurgeData surge, int wave)
        {
            if (wave <= 1) return surge;

            var scaled = surge.Clone();
            if (ServiceLocator.TryGet<DifficultyScaler>(out var scaler))
            {
                scaled.Health *= scaler.GetWaveHpMultiplier(wave);
                scaled.Speed *= scaler.GetWaveSpeedMultiplier(wave);
                scaled.Count = Mathf.RoundToInt(scaled.Count * scaler.GetWaveCountMultiplier(wave));
                if (scaled.Count < 1) scaled.Count = 1;
            }

            return scaled;
        }

        public override void _PhysicsProcess(double delta)
        {
            // Auto-start countdown (ticks during Build phase, uses physics delta so speed toggle works)
            if (!_waveActive && _autoStartTimer > 0)
            {
                bool harvesterReady = ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null;
                if (!harvesterReady)
                {
                    _autoStartTimer = -1f;
                }
                else
                {
                    _autoStartTimer -= (float)delta;
                    if (_autoStartTimer <= 0)
                    {
                        _autoStartTimer = -1f;
                        StartWave();
                    }
                }
            }

            if (!_waveActive) return;

            float dt = (float)delta;
            bool anySurgesLeft = false;

            // Get difficulty surge multiplier (increases spawn rate during surges)
            float surgeSpawnMult = 1f;
            if (ServiceLocator.TryGet<DifficultyScaler>(out var scaler))
                surgeSpawnMult = scaler.GetSurgeSpawnMultiplier();

            // Spawn enemies from active surges
            for (int i = 0; i < _activeSurges.Count; i++)
            {
                var surge = _activeSurges[i];
                if (surge.Remaining <= 0) continue;

                anySurgesLeft = true;

                if (surge.UseAccumulator)
                {
                    // Accumulator-based spawning: fractional spawns per frame.
                    // Rate = 1 / SpawnInterval, scaled by surge multiplier.
                    float spawnRate = 1f / Mathf.Max(0.01f, surge.Data.SpawnInterval);
                    spawnRate *= surgeSpawnMult;
                    surge.Accumulator += spawnRate * dt;

                    while (surge.Accumulator >= 1f && surge.Remaining > 0)
                    {
                        surge.Accumulator -= 1f;
                        SpawnEnemy(surge.Data);
                        surge.Remaining--;
                    }
                }
                else
                {
                    // Discrete timer-based spawning (original behavior)
                    surge.Timer -= dt;

                    if (surge.Timer <= 0)
                    {
                        SpawnEnemy(surge.Data);
                        surge.Remaining--;
                        float jitter = surge.Data.SpawnJitter > 0
                            ? _rng.RandfRange(-surge.Data.SpawnJitter, surge.Data.SpawnJitter)
                            : 0f;
                        surge.Timer = surge.Data.SpawnInterval + jitter;
                    }
                }

                _activeSurges[i] = surge;
            }

            // Check completion based on mode
            bool waveComplete = false;
            if (_currentWaveData != null)
            {
                switch (_currentWaveData.CompletionMode)
                {
                    case WaveCompletionMode.KillAll:
                        waveComplete = !anySurgesLeft && _enemiesAlive <= 0;
                        break;

                    case WaveCompletionMode.Timer:
                        _completionTimer += dt;
                        waveComplete = _completionTimer >= _currentWaveData.CompletionTimer;
                        break;

                    case WaveCompletionMode.KillThreshold:
                        waveComplete = _killCount >= _currentWaveData.CompletionKillCount;
                        break;

                    case WaveCompletionMode.Hybrid:
                        _completionTimer += dt;
                        waveComplete = _completionTimer >= _currentWaveData.CompletionTimer
                            || _killCount >= _currentWaveData.CompletionKillCount;
                        break;
                }
            }
            else
            {
                // No wave data — fallback to KillAll
                waveComplete = !anySurgesLeft && _enemiesAlive <= 0;
            }

            if (waveComplete)
                CompleteWave();
        }

        private void CompleteWave()
        {
            _waveActive = false;
            string addr = $"P{_currentPlanet}-W{_currentWave}";

            // Award accumulated bonus resources from all stacked waves
            if (_pendingBonusResources > 0)
                GameEvents.OnResourcesCollected?.Invoke(_pendingBonusResources);
            _pendingBonusResources = 0;

            GD.Print($"[VineWaveManager] {addr} complete — kills={_killCount}");
            GameEvents.OnWaveCompleted?.Invoke(_currentWave);

            // S2: Check milestones
            CheckMilestone(_currentWave);

            // S2: Always transition to WaveComplete → Build (never Victory from wave count)
            GameManager.Instance?.SetPhase(GamePhase.WaveComplete);
            GetTree().CreateTimer(1.5f).Timeout += () =>
            {
                GameManager.Instance?.SetPhase(GamePhase.Build);
                _autoStartTimer = Constants.WAVE_PREP_TIME;
            };

            _currentWaveData = null;
        }

        /// <summary>
        /// S2: Check if current wave triggers a milestone event.
        /// </summary>
        private void CheckMilestone(int wave)
        {
            if (_milestones == null) return;

            foreach (var milestone in _milestones)
            {
                if (milestone.Wave == wave)
                {
                    GD.Print($"[VineWaveManager] Milestone at wave {wave}: {milestone.Label}");
                    GameEvents.OnWaveMilestone?.Invoke(wave, milestone.Type);
                    return;
                }
            }
        }

        private void SpawnEnemy(SurgeData group)
        {
            // Pick entry region
            int entryIdx = group.EntryIndex;
            var regions = _grid.EntryRegions;

            if (entryIdx < 0 || entryIdx >= regions.Count)
                entryIdx = _rng.RandiRange(0, regions.Count - 1);

            var region = regions[entryIdx];

            // Pick random cell within the region for chaotic spawning
            var spawnCell = region.GetRandomSpawnCell(_rng);

            // Try to find path from this specific cell
            var path = _pathfinder.FindPathFromPosition(spawnCell);
            if (path == null || path.Count == 0)
            {
                // Fallback to cached path from region center
                path = _pathfinder.GetCachedPath(region.Center);
                spawnCell = region.Center;
            }
            if (path == null || path.Count == 0) return;

            // S2: Apply time-based difficulty scaling at spawn time
            float hpMult = 1f;
            float speedMult = 1f;
            float dmgMult = 1f;
            if (ServiceLocator.TryGet<DifficultyScaler>(out var scaler))
            {
                hpMult = scaler.GetHpMultiplier();
                speedMult = scaler.GetSpeedMultiplier();
                dmgMult = scaler.GetDamageMultiplier();
            }

            var enemy = new VineEnemy();
            GetTree().Root.AddChild(enemy);
            enemy.Initialize(group.EnemyName, group.Faction,
                group.Health * hpMult,
                group.Speed * speedMult,
                group.ResourceValue, group.Color, spawnCell, group.IsBoss,
                group.AttackRange, group.AttackDamage * dmgMult, group.AttackInterval);

            // Offset spawn position behind entry for approach march
            var entryWorld = _grid.GridToWorld(spawnCell);
            float cs = Constants.VINE_CELL_SIZE;
            var gridCenter = new Vector3(_grid.Width * cs / 2f, 0, _grid.Height * cs / 2f);
            var dirToGrid = (gridCenter - entryWorld).Normalized();
            var spawnPos = entryWorld - dirToGrid * Constants.VINE_SPAWN_OFFSET;
            enemy.GlobalPosition = new Vector3(spawnPos.X, 0.3f, spawnPos.Z);

            // Spawn commander with first enemy of this surge if configured
            if (group.Commander != null && _enemiesAlive == 0)
                SpawnCommander(group, spawnCell);

            _enemiesAlive++;
        }

        private void SpawnCommander(SurgeData surge, Vector2I spawnCell)
        {
            var cmd = surge.Commander;

            // Evaluate spawn condition
            bool shouldSpawn = cmd.SpawnType switch
            {
                CommanderSpawnType.Scripted => true,
                CommanderSpawnType.Random => _rng.Randf() <= cmd.SpawnChance,
                CommanderSpawnType.Reactive => false, // TODO: evaluate reactive triggers
                _ => false
            };
            if (!shouldSpawn) return;

            string addr = $"P{_currentPlanet}-W{_currentWave}";
            GD.Print($"[VineWaveManager] {addr} Commander spawned: {cmd.EnemyName} ({cmd.Behavior})");

            var path = _pathfinder.FindPathFromPosition(spawnCell);
            if (path == null || path.Count == 0) return;

            var enemy = new VineEnemy();
            GetTree().Root.AddChild(enemy);

            var factionColor = TronTheme.GetFactionColor(cmd.Faction);
            enemy.Initialize(cmd.EnemyName, cmd.Faction, cmd.Health, cmd.Speed,
                cmd.ResourceValue, factionColor, spawnCell, true, // isBoss=true for commander scaling
                0, 0, 0);

            // Offset spawn position
            var entryWorld = _grid.GridToWorld(spawnCell);
            float cs = Constants.VINE_CELL_SIZE;
            var gridCenter = new Vector3(_grid.Width * cs / 2f, 0, _grid.Height * cs / 2f);
            var dirToGrid = (gridCenter - entryWorld).Normalized();
            enemy.GlobalPosition = entryWorld - dirToGrid * (Constants.VINE_SPAWN_OFFSET + 2f);

            _enemiesAlive++;
        }

        private void OnEnemyDied(Node enemy)
        {
            if (enemy is VineEnemy)
            {
                _enemiesAlive = Mathf.Max(0, _enemiesAlive - 1);
                _killCount++;
            }
        }

        private void OnEnemyLeaked(Node enemy, Vector3 pos)
        {
            if (enemy is VineEnemy)
                _enemiesAlive = Mathf.Max(0, _enemiesAlive - 1);
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyDied;
            GameEvents.OnEnemyLeaked -= OnEnemyLeaked;
            GameEvents.OnPhaseChanged -= OnPhaseChanged;
            ServiceLocator.Unregister<VineWaveManager>();
        }

        private struct ActiveSurge
        {
            public SurgeData Data;
            public int Remaining;
            public float Timer;
            public int SurgeIndex; // For P#-W#-S# addressing
            /// <summary>
            /// Accumulator-based spawning: fractional spawn units accumulate per frame.
            /// When >= 1.0, spawn one enemy and subtract 1.0.
            /// Used when UseAccumulator is true (set via surge config).
            /// </summary>
            public float Accumulator;
            public bool UseAccumulator;
        }
    }
}
