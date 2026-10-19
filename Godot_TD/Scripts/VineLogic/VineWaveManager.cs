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

        // Highest wave whose milestones/boss trigger/site-clear have been processed.
        // Stacked waves (Send All) can complete several wave numbers at once.
        private int _lastProcessedWave;

        public int CurrentWave => _currentWave;
        /// <summary>Tests: pretend this many waves are done (the HUD previews the next one).</summary>
        internal void TestSetWave(int wave) => _currentWave = wave;
        public bool WaveActive => _waveActive;

        /// <summary>S2: Total hand-crafted waves for HUD display.</summary>
        public int TotalWaves => _handCraftedWaves?.Count ?? 20;

        public float AutoStartTimer => _autoStartTimer;
        /// <summary>Test hook: freeze the Build-phase countdown so a measurement window stays wave-free.</summary>
        public bool PauseAutoStart { get; set; }
        /// <summary>
        /// Resources paid when a wave clears. Authored waves pay their own BonusResources;
        /// procedural waves carry on from the last authored bonus, compounding (Climb). The meta
        /// perk "Wave Processor" raises SignalTuningEditor.WaveBonus above its default: only the
        /// perk's delta is added. (Past W20 this used to jump from 50 to 96 and then compound 12%
        /// a wave, while the climb was a wall; now both grow steadily.)
        /// </summary>
        public static int ComputeWaveBonus(int wave, VineWaveData data, int authoredCount)
        {
            int bonus = wave <= authoredCount
                ? data?.BonusResources ?? 0
                : Climb.ProceduralBonus(wave, authoredCount);
            return bonus + Mathf.Max(0, SignalTuningEditor.WaveBonus - Constants.VINE_WAVE_BONUS);
        }

        /// <summary>What a wave pays when it clears, sent <paramref name="depth"/> waves early (0: fought on its own).</summary>
        public int WaveBonusAt(int wave, int depth)
        {
            var data = WaveDataAt(wave);
            return Mathf.RoundToInt(ComputeWaveBonus(wave, data, _handCraftedWaves?.Count ?? 0) * Climb.GreedBonus(depth));
        }

        // Procedural waves are generated once and kept, so the wave card's preview is the wave that comes
        private readonly Dictionary<int, VineWaveData> _generated = new();

        /// <summary>The wave data for a wave number: authored, or procedural (generated once and kept).</summary>
        public VineWaveData WaveDataAt(int wave)
        {
            if (wave < 1) return null;
            if (_handCraftedWaves != null && wave <= _handCraftedWaves.Count) return _handCraftedWaves[wave - 1];
            if (_generated.TryGetValue(wave, out var g)) return g;
            // WAVE_SEED (balance runs): every run gets the same procedural waves, so two bots are
            // compared on the same fight rather than on which templates and commanders they drew
            var rng = _rng;
            if (ulong.TryParse(System.Environment.GetEnvironmentVariable("WAVE_SEED"), out var seed))
                rng = new RandomNumberGenerator { Seed = seed * 100003UL + (ulong)wave };
            g = VineWaveLoader.GenerateWave(wave, _handCraftedWaves, rng);
            if (g != null) _generated[wave] = g;
            // Only the next few are ever asked for
            if (_generated.Count > 16)
            {
                var old = new List<int>();
                foreach (var k in _generated.Keys) if (k < _currentWave) old.Add(k);
                foreach (var k in old) _generated.Remove(k);
            }
            return g;
        }

        /// <summary>The first wave still on the field (the one a stack was sent on top of); 0 when none is running.</summary>
        public int FieldFrom => _waveActive ? _stackBaseWave : 0;

        /// <summary>Enemies sent so far by waves still running, alive or dead (for the share still to beat).</summary>
        public int FieldTotal
        {
            get
            {
                int n = 0;
                if (!_waveActive) return 0;
                for (int w = _stackBaseWave; w <= _currentWave; w++)
                {
                    var d = WaveDataAt(w);
                    if (d != null) foreach (var s in d.Surges) n += s.Count + (s.Commander != null ? 1 : 0);
                }
                return n;
            }
        }

        /// <summary>How many waves ahead of the one being fought the next wave sent would be (0 when none is running).</summary>
        public int NextDepth => _waveActive ? _currentWave + 1 - _stackBaseWave : 0;

        /// <summary>Greed paid on top of the plain wave bonuses by the last clear (sending waves early).</summary>
        public int LastGreedBonus { get; private set; }
        /// <summary>Wave bonuses paid by the last clear, greed included (interest is separate).</summary>
        public int LastWaveBonus { get; private set; }
        private int _pendingGreed;


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
            OverclockHabit.Reset();

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
            // Sending a wave from a safe room leaves the crates for the next one
            if (InSafeRoom) LeaveSafeRoom(opened: false);

            if (!_waveActive)
            {
                StartWave();
            }
            else if (PendingStackedWaves >= Constants.MAX_STACKED_WAVES)
            {
                GameEvents.OnAnnouncement?.Invoke($"{Constants.MAX_STACKED_WAVES} waves are already on the way");
            }
            else
            {
                StartWave(stack: true);
            }
        }

        /// <summary>
        /// The wave Send All sends up to: the next milestone (multiple of SEND_ALL_MILESTONE) at
        /// or after the next wave. It sent three waves, which read as "only sends 3 waves".
        /// </summary>
        public int SendAllTarget
        {
            get
            {
                int next = _currentWave + 1;
                int m = Constants.SEND_ALL_MILESTONE;
                return (next + m - 1) / m * m;
            }
        }

        public void SendAllRemaining()
        {
            _autoStartTimer = -1f;
            int target = SendAllTarget;
            int from = _currentWave + 1;
            // Every wave to the milestone, each arriving a few seconds behind the last. Pressing it
            // again while earlier stacked waves still arrive used to pile them into the same
            // seconds (the dev PC crashed on a second Send All): a stack never runs past
            // MAX_STACKED_WAVES waiting behind the one being fought.
            if (_waveActive && PendingStackedWaves >= Constants.MAX_STACKED_WAVES)
            {
                GameEvents.OnAnnouncement?.Invoke($"Waves to {_currentWave} are already on the way");
                return;
            }
            if (!_waveActive)
                StartWave();
            int guard = 0;
            while (_currentWave < target && PendingStackedWaves < Constants.MAX_STACKED_WAVES && guard++ < 10)
                StartWave(stack: true);
            GameEvents.OnAnnouncement?.Invoke(from >= _currentWave
                ? $"Wave {_currentWave} sent"
                : $"Waves {from} to {_currentWave} sent, a few seconds apart");
        }

        /// <summary>Enemies still to beat this wave: on the field plus those not yet sent.</summary>
        public int EnemiesRemaining
        {
            get
            {
                int n = _enemiesAlive;
                foreach (var s in _activeSurges) n += Mathf.Max(0, s.Remaining);
                return n;
            }
        }

        /// <summary>Stacked waves that still have enemies to send (not the wave the stack started on).</summary>
        public int PendingStackedWaves
        {
            get
            {
                var waves = new HashSet<int>();
                foreach (var s in _activeSurges)
                    if (s.Remaining > 0 && s.Wave > _stackBaseWave) waves.Add(s.Wave);
                return waves.Count;
            }
        }

        // The wave a run of stacked waves was sent on top of
        private int _stackBaseWave;

        private void OnPhaseChanged(GamePhase phase)
        {
            // For wave 0 on start, timer is started by OnHarvesterPlaced (called from HUD).
            // For subsequent waves, timer is started by CompleteWave directly.

            // Run is over — stop spawning and don't auto-start another wave
            if (phase == GamePhase.Victory || phase == GamePhase.Defeat)
            {
                _waveActive = false;
                _activeSurges.Clear();
                _autoStartTimer = -1f;
            }
        }

        /// <summary>
        /// Called by VineHUD when it detects the harvester has been placed.
        /// Starts the first-wave countdown.
        /// </summary>
        public void OnHarvesterPlaced()
        {
            if (_currentWave == 0 && _autoStartTimer < 0)
                _autoStartTimer = WavePacing.FirstDelay;
        }

        public void StartWave(bool stack = false)
        {
            // Sending a wave from a safe room leaves the crates for the next one
            if (InSafeRoom) LeaveSafeRoom(opened: false);
            _currentWave++;
            // S2: Use hand-crafted wave if available, else generate procedurally (the one the
            // wave card previewed, if it did)
            VineWaveData data = WaveDataAt(_currentWave);

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
                _pendingGreed = 0;
                _stackBaseWave = _currentWave;
            }
            // Each stacked wave arrives STACK_STAGGER seconds after the one before it
            float stagger = stack ? Constants.STACK_STAGGER * (PendingStackedWaves + 1) : 0f;

            // The greed bet: a wave sent early pays more for its kills and its clear, more the
            // further ahead it was sent. It is paid when the whole stack clears.
            int depth = stack ? _currentWave - _stackBaseWave : 0;
            int plainBonus = ComputeWaveBonus(_currentWave, data, _handCraftedWaves?.Count ?? 0);
            int bonus = Mathf.RoundToInt(plainBonus * Climb.GreedBonus(depth));
            _pendingBonusResources += bonus;
            _pendingGreed += bonus - plainBonus;

            string addr = $"P{_currentPlanet}-W{_currentWave}";

            for (int s = 0; s < data.Surges.Count; s++)
            {
                // S2: Apply wave-based scaling to surge data
                var scaledSurge = ApplyWaveScaling(data.Surges[s], _currentWave);
                _activeSurges.Add(new ActiveSurge {
                    Data = scaledSurge,
                    Remaining = scaledSurge.Count,
                    Timer = scaledSurge.StartDelay,
                    SurgeIndex = s + 1,
                    Wave = _currentWave,
                    Accumulator = 0f,
                    UseAccumulator = scaledSurge.UseAccumulator,
                    Delay = stagger,
                    Depth = depth,
                });
            }

            GD.Print($"[VineWaveManager] {addr} \"{data.Name}\" — {data.Surges.Count} surges, mode={data.CompletionMode}{(stack ? " [STACKED]" : "")}");
            if (stack) FlightRecorder.Note($"stacked {addr} (arrives in {stagger:F0} s)");

            if (GameManager.Instance != null)
                GameManager.Instance.CurrentWave = _currentWave;
            if (!stack)
            {
                GameManager.Instance?.SetPhase(GamePhase.Wave);
                OverclockHabit.WaveStarted();
            }
            GameEvents.OnWaveStarted?.Invoke(_currentWave);
        }

        /// <summary>
        /// The climb (Climb.Hp): every enemy of every wave, commanders included, gets the wave's
        /// health. Damage grows at spawn (Climb.Damage). Clones the surge.
        /// </summary>
        public static SurgeData ApplyWaveScaling(SurgeData surge, int wave)
        {
            var scaled = surge.Clone();
            float hp = Climb.EnemyHp(wave) * (CorruptionManager.IsChaosWave(wave) ? CorruptionManager.CHAOS_WAVE_HP_SHARE : 1f);
            scaled.Health *= hp;
            if (scaled.Commander != null)
            {
                var c = scaled.Commander.MemberwiseCloneCommander();
                c.Health *= hp;
                scaled.Commander = c;
            }
            return scaled;
        }

        public override void _PhysicsProcess(double delta)
        {
            long __pt = FrameProfiler.Start();
            try
            {
                if (_waveActive) OverclockHabit.Tick((float)delta);
                // Auto-start countdown (ticks during Build phase, uses physics delta so speed toggle works)
                if (!_waveActive && _autoStartTimer > 0 && !PauseAutoStart)
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
                    if (surge.Delay > 0f)
                    {
                        surge.Delay -= dt;
                        _activeSurges[i] = surge;
                        continue;
                    }

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
                            SpawnEnemy(surge);
                            surge.Remaining--;
                        }
                    }
                    else
                    {
                        // Discrete timer-based spawning (original behavior)
                        surge.Timer -= dt;

                        if (surge.Timer <= 0)
                        {
                            SpawnEnemy(surge);
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
            finally { FrameProfiler.Stop("waves", __pt); }
        }

        /// <summary>What Salvage Drills paid when the last wave ended.</summary>
        public int LastDrillPay { get; private set; }

        /// <summary>Interest paid when the last wave ended.</summary>
        public int LastInterest { get; private set; }

        /// <summary>Interest a clear pays on <paramref name="banked"/> Resources at a wave: a share, capped (Compound Interest raises both).</summary>
        public static int InterestOn(int banked, int wave)
        {
            float pct = Climb.InterestPct + MetaRun.InterestPct;
            int cap = Climb.InterestCap(wave) * (MetaRun.InterestPct > 0f ? 2 : 1);
            return Mathf.Clamp(Mathf.FloorToInt(banked * pct / 100f), 0, cap);
        }

        private void CompleteWave()
        {
            _waveActive = false;
            string addr = $"P{_currentPlanet}-W{_currentWave}";

            LastWaveBonus = _pendingBonusResources;
            LastGreedBonus = _pendingGreed;
            _pendingGreed = 0;

            // The Spire mends a little each clear: runs end when a wave breaks the defence, not
            // from chip damage piling up over thirty waves
            if (ServiceLocator.TryGet<VineGrid>(out var g0) && g0.Harvester != null && !g0.Harvester.IsDestroyed)
                g0.Harvester.Heal(g0.Harvester.MaxHP * Climb.SpireMend);

            // Salvage Drills still standing pay for every wave this clear covers
            LastDrillPay = SalvageDrill.PayOut(Mathf.Min(_lastProcessedWave + 1, _currentWave), _currentWave);
            _pendingBonusResources += LastDrillPay;

            // Interest on what is banked: saving is a choice against building now
            LastInterest = 0;
            if (GameManager.Instance != null)
            {
                int interest = InterestOn(GameManager.Instance.CurrentResources, _currentWave);
                LastInterest = interest;
                if (interest > 0)
                {
                    _pendingBonusResources += interest;
                    GD.Print($"[VineWaveManager] {addr} interest +{interest}");
                }
            }

            // Award accumulated bonus resources from all stacked waves
            if (_pendingBonusResources > 0)
                GameEvents.OnResourcesCollected?.Invoke(_pendingBonusResources);
            if (LastGreedBonus > 0)
                GameEvents.OnAnnouncement?.Invoke($"Greed paid: +{LastGreedBonus} Resources for sending waves early");
            _pendingBonusResources = 0;

            GD.Print($"[VineWaveManager] {addr} complete — kills={_killCount}");
            OverclockHabit.WaveEnded();
            GameEvents.OnWaveCompleted?.Invoke(_currentWave);
            _currentWaveData = null;

            // Process every wave number cleared since last time — stacked waves (Send All)
            // must not skip a perk milestone, the boss wave, or a site's clear wave.
            var gm = GameManager.Instance;
            for (int w = _lastProcessedWave + 1; w <= _currentWave; w++)
            {
                if (WavePacing.IsSafeRoom(w)) _safeRoomDue = true;
                // Each wave of a stack pays its XP, supply drop and rolls
                RunRecord.WaveCleared(w, g0?.Harvester != null ? g0.Harvester.CurrentHP / Mathf.Max(1f, g0.Harvester.MaxHP) : 1f);
                GameEvents.OnWaveCleared?.Invoke(w);
                // S2: Check milestones (first-time perk points), then the run's own perk point
                int firstTime = CheckMilestone(w);
                gm?.AwardDepthPoints(w, firstTime);

                // S4: Check boss wave trigger during boss runs
                if (CheckBossWaveTrigger(w))
                {
                    _lastProcessedWave = _currentWave;
                    return; // Victory — run is over, no Build phase
                }

                // Farming runs: surviving to the site's clear wave secures the site
                gm?.CheckSiteSecured(w);
            }
            _lastProcessedWave = _currentWave;

            if (gm != null && (gm.CurrentPhase == GamePhase.Victory || gm.CurrentPhase == GamePhase.Defeat))
                return;

            // S2: Always transition to WaveComplete → Build (never Victory from wave count)
            gm?.SetPhase(GamePhase.WaveComplete);
            int cleared = _currentWave;
            bool safeRoom = _safeRoomDue;
            _safeRoomDue = false;
            // Pausable timer: holds while the perk overlay / pause menu is open
            GetTree().CreateTimer(1.5f, processAlways: false).Timeout += () =>
            {
                // Scene may be gone, or the run may have ended during the delay
                if (!IsInstanceValid(this)) return;
                var gm2 = GameManager.Instance;
                if (gm2 == null || gm2.CurrentPhase != GamePhase.WaveComplete) return;
                gm2.SetPhase(GamePhase.Build);
                // Back to back through the easy waves, a build break after; a safe room waits
                if (safeRoom && EnterSafeRoom(cleared)) return;
                _autoStartTimer = WavePacing.GapAfter(cleared);
            };
        }

        // ── Safe rooms ──

        /// <summary>The fighting has stopped for BIT's crates: nothing starts until they're opened (or a wave is sent).</summary>
        public bool InSafeRoom { get; private set; }
        /// <summary>The wave whose clear opened the safe room.</summary>
        public int SafeRoomWave { get; private set; }
        private bool _safeRoomDue;

        /// <summary>
        /// A safe room after <paramref name="wave"/>: only when BIT has crates (carried or in the
        /// field). Every crate in the field comes to BIT, the timer stops, and the HUD says to
        /// open them. Returns false (and the waves carry on) when there's nothing to open.
        /// </summary>
        internal bool EnterSafeRoom(int wave)
        {
            if (!ServiceLocator.TryGet<LootManager>(out var loot)) return false;
            loot.GatherField();
            if (loot.Carried.Count == 0) return false;
            InSafeRoom = true;
            SafeRoomWave = wave;
            _autoStartTimer = -1f;
            GD.Print($"[VineWaveManager] P{_currentPlanet}-W{wave} safe room: {loot.Carried.Count} crates to open");
            Celebration.Show("SAFE ROOM", "THE CANYON IS QUIET", $"{loot.Carried.Count} crate{(loot.Carried.Count == 1 ? "" : "s")} to open: press O", new Color(1f, 0.82f, 0.35f), 3.5f);
            GameEvents.OnAnnouncement?.Invoke("Safe room: no waves until you open your crates [O]");
            return true;
        }

        /// <summary>The safe room is over: opened (the build break follows) or skipped by sending a wave.</summary>
        internal void LeaveSafeRoom(bool opened)
        {
            if (!InSafeRoom) return;
            InSafeRoom = false;
            if (opened && !_waveActive) _autoStartTimer = WavePacing.LaterGap;
        }

        /// <summary>
        /// S2: Check if current wave triggers a milestone event.
        /// </summary>
        private int CheckMilestone(int wave)
        {
            if (_milestones == null) return 0;

            foreach (var milestone in _milestones)
            {
                if (milestone.Wave == wave)
                {
                    GD.Print($"[VineWaveManager] Milestone at wave {wave}: {milestone.Label}");
                    int awarded = milestone.MetaPoints > 0
                        ? GameManager.Instance?.AwardMetaPoints(wave, milestone.MetaPoints, announce: false) ?? 0 : 0;
                    GameEvents.OnWaveMilestone?.Invoke(wave, milestone.Type);
                    return awarded;
                }
            }
            return 0;
        }

        /// <summary>
        /// S4: Check if this wave is the boss wave for the current boss run.
        /// When reached, fire OnBossDefeated after wave clears (boss was in the wave).
        /// Returns true if the boss run was completed.
        /// </summary>
        private bool CheckBossWaveTrigger(int wave)
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsBossRun || gm.BossSectionId == null) return false;

            var site = TerritoryManager.GetSite(gm.BossSectionId);
            if (site == null || site.BossWave != wave) return false;

            GD.Print($"[VineWaveManager] P{_currentPlanet}-W{wave} boss wave cleared — boss run complete!");
            GameEvents.OnBossDefeated?.Invoke();
            gm.OnBossRunComplete();
            return true;
        }

        private void SpawnEnemy(ActiveSurge surge)
        {
            var group = surge.Data;
            // Commander rides with the first enemy of its own surge
            bool firstOfSurge = surge.Remaining == group.Count;
            string addr = $"P{_currentPlanet}-W{surge.Wave}-S{surge.SurgeIndex}";

            // Pick entry region — only use active regions (not gated by shield walls)
            var regions = _grid.ActiveEntryRegions;
            if (regions.Count == 0)
            {
                GD.PushWarning($"[VineWaveManager] {addr} No active entry regions — skipping spawn");
                return;
            }

            int entryIdx = group.EntryIndex;
            VineEntryRegion region = null;

            // If surge targets a specific region, check if it's active
            if (entryIdx >= 0 && entryIdx < _grid.EntryRegions.Count)
            {
                var target = _grid.EntryRegions[entryIdx];
                if (target.Active)
                    region = target;
            }

            // Fall back to random active region
            if (region == null)
                region = regions[_rng.RandiRange(0, regions.Count - 1)];

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
            if (path == null || path.Count == 0)
            {
                // Region is cut off (map design / mutation) — use another active entry
                // rather than silently dropping the spawn
                foreach (var alt in regions)
                {
                    if (alt == region) continue;
                    var altPath = _pathfinder.GetCachedPath(alt.Center);
                    if (altPath == null || altPath.Count == 0) continue;
                    region = alt;
                    spawnCell = alt.Center;
                    path = altPath;
                    break;
                }
            }
            if (path == null || path.Count == 0)
            {
                GD.PushWarning($"[VineWaveManager] {addr} No entry has a path to the Spire — spawn skipped");
                return;
            }

            // Parent to the battle scene (not the tree root) so enemies are freed with it —
            // root-level enemies survived scene changes and kept leaking into later runs.
            var enemy = new VineEnemy();
            EnemyParent.AddChild(enemy);
            enemy.Initialize(group.EnemyName, group.Faction,
                group.Health, group.Speed,
                group.ResourceValue, group.Color, spawnCell, group.IsBoss,
                group.AttackRange, group.AttackDamage, group.AttackInterval,
                damageMult: Climb.Damage(surge.Wave));
            enemy.BountyScale = Climb.Bounty(surge.Wave) * Climb.GreedBounty(surge.Depth);
            enemy.Wave = surge.Wave;
            if (group.Traits != EnemyTraits.None) enemy.SetTraits(group.Traits);

            // Offset spawn position behind entry for approach march
            var entryWorld = _grid.GridToWorld(spawnCell);
            float cs = Constants.VINE_CELL_SIZE;
            var gridCenter = new Vector3(_grid.Width * cs / 2f, 0, _grid.Height * cs / 2f);
            var dirToGrid = (gridCenter - entryWorld).Normalized();
            var spawnPos = entryWorld - dirToGrid * Constants.VINE_SPAWN_OFFSET;
            enemy.GlobalPosition = new Vector3(spawnPos.X, _grid.GetWorldHeight(spawnPos.X, spawnPos.Z) + (enemy.IsFlying ? VineEnemy.FLY_HEIGHT : 0f), spawnPos.Z);

            // Spawn commander with first enemy of this surge if configured.
            // (Was gated on _enemiesAlive == 0, so only a wave's very first surge could get one.)
            if (group.Commander != null && firstOfSurge)
                SpawnCommander(group, spawnCell, addr, surge.Wave, surge.Depth);

            FlightRecorder.CountSpawn();
            _enemiesAlive++;
        }

        private Node EnemyParent => GetParent() ?? GetTree().Root;

        /// <summary>
        /// AXIS reinforcements: one of this wave's enemy types, tougher (<paramref name="hpMult"/>),
        /// dropped straight onto <paramref name="cell"/>. It counts toward the wave, so the wave
        /// does not end until it is dead. Returns null if there is no wave to draw from.
        /// </summary>
        public VineEnemy SpawnReinforcement(Vector2I cell, float hpMult)
        {
            if (!_waveActive || _currentWaveData == null || _currentWaveData.Surges.Count == 0) return null;
            if (!_grid.InBounds(cell) || !_grid.IsWalkable(cell)) return null;
            // A random non-boss surge of this wave (picking a boss surge used to drop nothing)
            var pool = _currentWaveData.Surges.FindAll(x => !x.IsBoss);
            if (pool.Count == 0) return null;
            var surge = ApplyWaveScaling(pool[_rng.RandiRange(0, pool.Count - 1)], _currentWave);
            var enemy = new VineEnemy();
            EnemyParent.AddChild(enemy);
            enemy.Initialize(surge.EnemyName, surge.Faction, surge.Health * hpMult, surge.Speed,
                surge.ResourceValue, surge.Color, cell, false,
                surge.AttackRange, surge.AttackDamage, surge.AttackInterval,
                damageMult: Climb.Damage(_currentWave));
            enemy.BountyScale = Climb.Bounty(_currentWave);
            enemy.Wave = _currentWave;
            var at = _grid.GridToWorld(cell);
            enemy.GlobalPosition = new Vector3(at.X, _grid.GetWorldHeight(at.X, at.Z), at.Z);
            enemy.SkipMarch();
            if (surge.Traits != EnemyTraits.None) enemy.SetTraits(surge.Traits);
            _enemiesAlive++;
            return enemy;
        }

        /// <summary>
        /// An enemy Ascendant as a boss of the current wave: it marches in from an open entry,
        /// walks the maze to the Spire, and towers, BIT and the Spire's guns can all hit it. It
        /// counts toward the wave, so the wave holds until it dies or gets through.
        /// <paramref name="hpMult"/> scales its health, <paramref name="towerShare"/> is the share
        /// of its rival-hitting damage it does to towers and BIT.
        /// </summary>
        public VineEnemy SpawnAscendant(AscendantProfile p, float hpMult, float towerShare,
            float walkSpeed, float spireShare)
        {
            if (p == null || _grid == null) return null;
            var regions = _grid.ActiveEntryRegions;
            VineEntryRegion region = null;
            List<Vector2I> path = null;
            int start = regions.Count > 0 ? _rng.RandiRange(0, regions.Count - 1) : 0;
            for (int i = 0; i < regions.Count && path == null; i++)
            {
                var r = regions[(start + i) % regions.Count];
                var rp = _pathfinder.GetCachedPath(r.Center);
                if (rp != null && rp.Count > 0) { region = r; path = rp; }
            }
            if (region == null)
            {
                GD.PushWarning("[VineWaveManager] No open entry has a path to the Spire: Ascendant not spawned");
                return null;
            }
            var cell = region.Center;
            var color = new Color(p.ColorR, p.ColorG, p.ColorB);
            var enemy = new VineEnemy
            {
                ModelOverride = string.IsNullOrEmpty(p.Model) ? null : p.Model,
                ModelOverrideHeight = p.ModelHeight,
                IsAscendant = true,
                AscendantColor = color,
                AscendantSpireShare = spireShare,
                AscendantRivalDamage = p.Damage,
            };
            EnemyParent.AddChild(enemy);
            var faction = p.CombatStyle == "ranged_artillery" ? VineEnemyFaction.Scavenger : VineEnemyFaction.Brute;
            enemy.Initialize(p.Name, faction, p.HP * hpMult, walkSpeed, p.Reward, color, cell, true,
                p.AttackRange, p.Damage * towerShare, p.AttackInterval, damageMult: Climb.Damage(_currentWave));
            // An Ascendant pays its profile's reward in full (more as the waves go on): bounty_mult
            // trims ordinary kills, not the one prize a run may see
            enemy.BountyScale = Mathf.Max(1f, Climb.Bounty(_currentWave));
            enemy.Wave = _currentWave;

            var entryWorld = _grid.GridToWorld(cell);
            float cs = Constants.VINE_CELL_SIZE;
            var gridCenter = new Vector3(_grid.Width * cs / 2f, 0, _grid.Height * cs / 2f);
            var dirToGrid = (gridCenter - entryWorld).Normalized();
            var spawnPos = entryWorld - dirToGrid * Constants.VINE_SPAWN_OFFSET;
            enemy.GlobalPosition = new Vector3(spawnPos.X, _grid.GetWorldHeight(spawnPos.X, spawnPos.Z), spawnPos.Z);

            FlightRecorder.Note($"Ascendant {p.Name} ({enemy.MaxHealth:F0} hp) at P{_currentPlanet}-W{_currentWave}");
            FlightRecorder.CountSpawn();
            _enemiesAlive++;
            return enemy;
        }

        private void SpawnCommander(SurgeData surge, Vector2I spawnCell, string addr, int wave, int depth)
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

            GD.Print($"[VineWaveManager] {addr} Commander spawned: {cmd.EnemyName} ({cmd.Behavior})");

            var path = _pathfinder.FindPathFromPosition(spawnCell);
            if (path == null || path.Count == 0) return;

            var enemy = new VineEnemy();
            EnemyParent.AddChild(enemy);

            var factionColor = TronTheme.GetFactionColor(cmd.Faction);
            enemy.Initialize(cmd.EnemyName, cmd.Faction, cmd.Health, cmd.Speed,
                cmd.ResourceValue, factionColor, spawnCell, true, // isBoss=true for commander scaling
                0, 0, 0, damageMult: Climb.Damage(wave));
            // RelicManager's commander drop check reads this — it was never set
            enemy.IsCommander = true;
            enemy.BountyScale = Climb.Bounty(wave) * Climb.GreedBounty(depth);
            enemy.Wave = wave;

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
            else
            {
                GD.Print($"[VineWaveManager] OnEnemyDied: enemy is {enemy?.GetType().Name ?? "null"}, not VineEnemy! alive={_enemiesAlive}");
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
            public int SurgeIndex; // 1-based, for P#-W#-S# addressing
            public int Wave;       // Wave that queued this surge (stacked waves differ from _currentWave)
            /// <summary>
            /// Accumulator-based spawning: fractional spawn units accumulate per frame.
            /// When >= 1.0, spawn one enemy and subtract 1.0.
            /// Used when UseAccumulator is true (set via surge config).
            /// </summary>
            public float Accumulator;
            public bool UseAccumulator;
            /// <summary>Seconds before this surge starts at all (stacked waves queue up behind each other).</summary>
            public float Delay;
            /// <summary>How many waves early its wave was sent (the greed bet: 0 for the wave being fought).</summary>
            public int Depth;
        }
    }
}
