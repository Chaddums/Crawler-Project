using System;
using System.Collections.Generic;
using System.Linq;
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
            VisualWalk.TryCreate(this);

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
            if (_state is State.BuildPhase or State.WavePhase) _gameSeconds += dt;

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


        // ── Starting a run ──

        private CampaignPilot.Plan _campaign;
        private float _gameSeconds;
        private bool _leftEarly;

        /// <summary>
        /// Starts the run the config calls for: a plain run on the default map, the config's
        /// "site", or (campaign) whatever the save's campaign needs next, a boss fight included.
        /// False when the campaign has nothing left to play.
        /// </summary>
        private bool LaunchRun(GameManager gm)
        {
            gm.CurrentPlanet = _currentConfig.Planet;
            gm.CurrentTerritorySectionId = null;
            gm.SelectedRole = _currentConfig.Role;
            var spireData = SpireData.Get(_currentConfig.Role);
            gm.AvailableNodes = spireData?.Nodes ?? VineDraftScreen.GetRoleNodes(0);
            Engine.TimeScale = _currentConfig.GameSpeed;
            // Direct (synchronous) scene changes: async tweens don't tick headless, and disabling
            // the TransitionManager node froze its fade so the first scene change never completed
            TransitionManager.Instance = null;
            _campaign = null;
            _gameSeconds = 0f;
            _leftEarly = false;
            _shotStage = 0; _shotClock = 0;
            BitAce.Reset();
            if (_currentConfig.Campaign || !string.IsNullOrEmpty(_currentConfig.Site))
            {
                _campaign = CampaignPilot.Begin(gm, _currentConfig);
                if (_campaign == null) return false;
                gm.CurrentPlanet = _campaign.Planet;
                if (_campaign.Boss) gm.StartBossRun(_campaign.Planet, _campaign.SuitIndex, _campaign.Site.Id);
                else
                {
                    gm.CurrentTerritorySectionId = _campaign.Site.Id;
                    gm.StartVineRun();
                }
            }
            else gm.StartVineRun();
            // Re-subscribe after GameEvents.ClearAll inside the run start
            GameEvents.OnPhaseChanged += OnPhaseChanged;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnEnemyLeaked += OnEnemyLeaked;
            GameEvents.OnVineNodePlaced += OnNodePlaced;
            GameEvents.OnWaveMilestone += OnWaveMilestone;
            return true;
        }

        private void FinishAll()
        {
            _state = State.AllComplete;
            GD.Print($"[AutoPlayer] All {_runsCompleted} runs complete.");
            HeadlessShutdown.QuitClean(this, 0);
        }

        /// <summary>The wave after which this run stops: the config's cap, or sooner when a campaign run leaves after its goal.</summary>
        private int WaveCap => _campaign != null && _currentConfig.Campaign ? Mathf.Min(_currentConfig.MaxWaves, _campaign.CapWave) : _currentConfig.MaxWaves;

        // ── State Handlers ──

        private void HandleWaitMenu()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Accept MainMenu OR Boot phase (headless may not fully init MainMenu scene)
            if (gm.CurrentPhase == GamePhase.MainMenu || gm.CurrentPhase == GamePhase.Boot)
            {
                // HUB_LOOK: no run, only the screens between runs as this save sees them
                if (HubLook.Wanted)
                {
                    _state = State.AllComplete;
                    _ = HubLook.Run(this);
                    return;
                }
                if (!StartNextRun())
                {
                    _state = State.AllComplete;
                    GD.Print($"[AutoPlayer] All {_runsCompleted} runs complete.");
                    HeadlessShutdown.QuitClean(this, 0);
                    return;
                }

                // Skip menu/territory/draft — go straight to battle
                GD.Print($"[AutoPlayer] Jumping to battle: {_currentConfig}");
                if (!LaunchRun(gm)) { FinishAll(); return; }
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
                ApplyConfiguredRelics();
                GD.Print($"[AutoPlayer] Build phase — executing {_currentStrategy.Name} strategy");
            }
        }

        /// <summary>The config's "relics": found and equipped at the start of the run (a loadout to test).</summary>
        private void ApplyConfiguredRelics()
        {
            var ids = _currentConfig?.Relics;
            if (ids == null || ids.Length == 0) return;
            bool changed = false;
            foreach (var id in ids)
            {
                if (RelicRegistry.Get(id) == null) { GD.PrintErr($"[AutoPlayer] unknown relic {id}"); continue; }
                if (RelicRun.Has(id)) continue;
                RelicInventory.ForceOwn(id);
                if (RelicInventory.Equip(id)) changed = true;
            }
            if (changed) { RelicRun.Begin(); GD.Print($"[AutoPlayer] Relics: {string.Join(", ", ids)}"); }
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

        /// <summary>
        /// "equipRelics": a relic from a crate goes in its slot when the slot is empty, as a player
        /// would take it, unless it only changes towers this build doesn't stand (Living Walls with no
        /// walls, Gravity Rams with no rams) or Molten Rounds where Tesla Coils and Flak Batteries,
        /// which it weakens, outnumber the Junk Turrets and Scatter Cannons it lights.
        /// </summary>
        private void EquipFoundRelics()
        {
            if (_currentConfig?.EquipRelics != true || !ServiceLocator.TryGet<RelicManager>(out var relics)) return;
            var standing = new Dictionary<VineNodeType, int>();
            foreach (var n in Roster.Nodes(GetTree()))
                if (IsInstanceValid(n) && n.Data != null && !(n.IsDestroyed && !n.IsWrecked))
                    standing[n.Data.Type] = standing.TryGetValue(n.Data.Type, out var c) ? c + 1 : 1;
            int Count(VineNodeType t) => standing.TryGetValue(t, out var c) ? c : 0;
            foreach (var id in RelicInventory.Owned.ToList())
            {
                if (!RelicInventory.FoundThisRun(id) || RelicInventory.IsEquipped(id)) continue;
                var r = RelicRegistry.Get(id);
                if (r == null || RelicInventory.EquippedOfKind(r.Value.Kind) != null) continue;
                bool suits = id switch
                {
                    "living_walls" => Count(VineNodeType.BarrierWall) > 0,
                    "gravity_rams" => Count(VineNodeType.PushPull) > 0,
                    "molten_rounds" => Count(VineNodeType.DamageTower) + Count(VineNodeType.ScatterCannon) >= Count(VineNodeType.TeslaCoil) + Count(VineNodeType.FlakBattery),
                    _ => true,
                };
                if (!suits) { GD.Print($"[AutoPlayer] Relic left off: {r.Value.Name} (nothing in this build for it)"); continue; }
                if (relics.EquipNow(id)) GD.Print($"[AutoPlayer] Relic on: {r.Value.Name} ({r.Value.Kind}) at W{GameManager.Instance?.CurrentWave ?? 0}");
            }
        }

        /// <summary>
        /// "bitUpgrades": BIT's own upgrades at the Spire, paid in banked Materials (mined on the
        /// waves "materialsEvery" picks), the cheapest first. The tower bots never bought one.
        /// </summary>
        private void BuyBitUpgrades()
        {
            var st = SpireStation.Current;
            if (st == null) return;
            for (int guard = 0; guard < 12; guard++)
            {
                var pick = new[] { "bit_weapon", "bit_trigger", "bit_reach", "bit_core" }.Where(id => st.CantBuy(id) == null)
                    .OrderBy(id => st.Upgrade(id).CostAt(st.LevelOf(id))).FirstOrDefault();
                if (pick == null || !st.Buy(pick)) break;
                GD.Print($"[AutoPlayer] BIT upgrade {pick} -> {st.LevelOf(pick)}");
            }
        }

        private void HandleBuildPhase(float dt)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            // Between waves the ace is still at the controls: crates, then back to its post
            if (_currentConfig.Ace && _currentStrategy is not BitOnlyStrategy) BitAce.Tick(grid, dt);
            // A player takes a while between waves (the Spire keeps mining): "buildSeconds"
            // (short of the wave's own auto-start, or the wave goes without the bot)
            // A safe room: the bot opens what it carries (no ceremony to click through) and goes on
            // (a visual run goes through it as a player does: VisualWalk)
            if (VisualWalk.Instance != null && ServiceLocator.TryGet<VineWaveManager>(out var vwm) && ServiceLocator.TryGet<LootManager>(out var vloot)
                && (vwm.InSafeRoom || VisualWalk.Instance.InSafeFlow))
            {
                if (!VisualWalk.Instance.SafeRoom(vwm, vloot)) return;
                EquipFoundRelics();
            }
            if (ServiceLocator.TryGet<VineWaveManager>(out var swm) && swm.InSafeRoom && ServiceLocator.TryGet<LootManager>(out var sloot))
            {
                sloot.OpenQuietly();
                EquipFoundRelics();
                swm.LeaveSafeRoom(opened: true);
            }
            float gap = WavePacing.GapAfter(GameManager.Instance?.CurrentWave ?? 0);
            float wait = Mathf.Min(_currentConfig.BuildSeconds, gap - 0.5f);
            if (wait > 0f && _buildWait < wait) { _buildWait += dt; return; }
            _buildWait = 0f;

            int resources = GameManager.Instance?.CurrentResources ?? 0;
            int wave = GameManager.Instance?.CurrentWave ?? 0;

            // Check max waves BEFORE starting another one (used to start one extra wave)
            if (wave >= WaveCap)
            {
                _currentReport.WaveReached = wave;
                if (_campaign != null && _currentConfig.Campaign)
                {
                    // The goal is behind it: leave as a player does, through the pause menu's End
                    // Run, so the run is banked and the debrief is gone through
                    GD.Print($"[AutoPlayer] W{wave}: leaving (End Run) {wave - _campaign.GoalWave} wave(s) past the goal");
                    _currentReport.Result = "left";
                    _leftEarly = true;
                    _state = State.WaitDebrief;
                    _stateTimer = 0.2f;
                    GameManager.Instance.EndRunEarly();
                    return;
                }
                _currentReport.Result = "timeout";
                _state = State.RunComplete;
                return;
            }

            // Execute strategy placement
            _currentStrategy.OnBuildPhase(grid, resources, wave);
            if (_currentConfig.BitAssist && _currentStrategy is not BitOnlyStrategy)
            {
                if (_currentConfig.Ace) BitAce.NewWave(grid, wave);
                else BitPilot.NewWave(grid, wave);
            }
            if (_currentConfig.BitUpgrades) BuyBitUpgrades();

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
                foreach (TowerLoss src in System.Enum.GetValues(typeof(TowerLoss)))
                {
                    _lostAtOpen[(int)src] = Losses.Lost(src);
                    _takenAtOpen[(int)src] = Losses.Taken(src);
                }
                _currentReport.Waves.Add(_openLog);
                GD.Print($"[AutoPlayer] W{wave + 1}: {t.VerdictWord} {t.Ratio:0.00}x, predicted leaks {t.Leaks} (Spire -{t.SpireDamage:0}), breaks at {_openLog.BreaksAt}, resources {_openLog.Resources}, towers {_openLog.Towers}");
                // The mix (what the bot built) and the weakest group, for reading a run back
                var mix = new System.Collections.Generic.SortedDictionary<string, int>();
                for (int x = 0; x < grid.Width; x++)
                    for (int y = 0; y < grid.Height; y++)
                        if (grid.GetNode(x, y) is { } v && !v.IsDestroyed && v.Data?.Category == VineNodeCategory.Effect)
                        {
                            string k = v.Branch != null ? $"{v.Data.Type}:{v.Branch.Id}" : v.Data.Type.ToString();
                            mix[k] = mix.TryGetValue(k, out int c) ? c + 1 : 1;
                        }
                var weak = t.Groups.Where(g => g.Health > 0.04f * t.Health).OrderBy(g => g.Ratio).FirstOrDefault();
                GD.Print($"[AutoPlayer] W{wave + 1} mix: {string.Join(", ", mix.Select(kv => $"{kv.Key} {kv.Value}"))} | weakest {weak.Traits} x{weak.Ratio:0.00}");
            }

            // "materialsEvery": a player who mines Materials some waves (BIT, the power type's tier)
            if (_currentConfig.MaterialsEvery > 0 && grid.Harvester != null && grid.Harvester.SelectedMaterial != MaterialType.None)
            {
                bool want = (wave + 1) % _currentConfig.MaterialsEvery == 0;
                if ((grid.Harvester.CurrentMode == MiningMode.Materials) != want) grid.Harvester.ToggleMode();
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
        private readonly int[] _lostAtOpen = new int[Losses.Count];
        private readonly float[] _takenAtOpen = new float[Losses.Count];
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
                _openLog.WrecksAfter = VineNode.Wrecks(g.GetTree()).Count;
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
            foreach (TowerLoss src in System.Enum.GetValues(typeof(TowerLoss)))
            {
                _openLog.Lost[(int)src] = Losses.Lost(src) - _lostAtOpen[(int)src];
                _openLog.Taken[(int)src] = Losses.Taken(src) - _takenAtOpen[(int)src];
            }
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm))
            {
                _openLog.Stack = wm.CurrentWave - _openLog.Wave;
                _openLog.Pay = wm.LastWaveBonus + wm.LastDrillPay + wm.LastInterest;
            }
            GD.Print($"[AutoPlayer] W{_openLog.Wave}{(_openLog.Stack > 0 ? $"+{_openLog.Stack}" : "")} done: leaks {_openLog.Leaks} (predicted {_openLog.PredictedLeaks}), Spire {_openLog.SpireBefore:0} -> {_openLog.SpireAfter:0}, {_openLog.Seconds:0} s");
            _openLog = null;
        }

        // ANIM_WATCH=1: is BIT (and the frame it rides) animating during the fights? Sampled every
        // frame of a wave; a line a wave says how much of it each clip was up and how much of it
        // the player was stopped on a clip's last frame ("the big unit isn't animating").
        private static readonly bool AnimWatch = OS.GetEnvironment("ANIM_WATCH") == "1";
        private int _animSamples, _animStopped, _animWave;
        private readonly Dictionary<string, int> _animClips = new();

        private void WatchAnimation()
        {
            if (!ServiceLocator.TryGet<VinePlayer>(out var bit) || !bit.IsAlive || bit.IsDocked) return;
            int wave = GameManager.Instance?.CurrentWave ?? 0;
            if (wave != _animWave)
            {
                if (_animSamples > 0)
                    GD.Print($"[AnimWatch] W{_animWave}: {(bit.Frame != null ? "frame " + bit.Frame.Id : "on foot")}, stopped on a last frame {_animStopped * 100 / _animSamples}% of the fight; "
                        + string.Join(", ", _animClips.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value * 100 / _animSamples}%")));
                _animWave = wave; _animSamples = _animStopped = 0; _animClips.Clear();
            }
            var ap = bit.FrameAnimator?.AnimPlayer ?? CharacterAnimator.FindAnimationPlayerPublic(bit.ModelRoot);
            if (ap == null) return;
            _animSamples++;
            if (!ap.IsPlaying()) _animStopped++;
            string clip = ap.IsPlaying() ? ap.CurrentAnimation.ToString() : ap.AssignedAnimation.ToString() + " (ended)";
            _animClips[clip] = _animClips.TryGetValue(clip, out int n) ? n + 1 : 1;
        }

        private void HandleWavePhase(float dt)
        {
            _waveClock += dt;
            if (AnimWatch) WatchAnimation();
            _currentStrategy.OnWavePhase(dt);
            if (_currentConfig.BitAssist && _currentStrategy is not BitOnlyStrategy && ServiceLocator.TryGet<VineGrid>(out var pilotGrid))
            {
                if (_currentConfig.Ace) BitAce.Tick(pilotGrid, dt);
                else BitPilot.Tick(pilotGrid, dt);
            }

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

        private int _debriefFrames;
        private int _shotStage;
        private ulong _shotClock;

        private void HandleDebrief()
        {
            if (_campaign != null)
            {
                // The debrief banks the run (DebriefScreen) and holds the suit to save: go through it
                var gm = GameManager.Instance;
                // CAMPAIGN_SHOTS: the screen the run ended on, then its debrief, as the player sees them
                string shots = OS.GetEnvironment("CAMPAIGN_SHOTS");
                bool shoot = !string.IsNullOrEmpty(shots) && HubLook.CanShoot;
                string shotName = $"run{_campaign.RunNumber:00}_{_campaign.Site.Id}";
                ulong now = Time.GetTicksMsec();
                if (shoot && _shotStage == 0)
                {
                    // (a bot run goes to the debrief when the bot says: time for the end's words to be up)
                    if (_shotClock == 0) { _shotClock = now; Engine.TimeScale = 1.0; }
                    if (now - _shotClock < 2500) return;
                    HubLook.Save(GetTree(), shots, shotName + "_1_end");
                    _shotStage = 1;
                }
                if (gm.CurrentPhase != GamePhase.Debrief) { _debriefFrames = 0; gm.ShowDebrief(); _shotClock = now; return; }
                if (shoot && _shotStage == 1)
                {
                    if (now - _shotClock < 7000) return;
                    HubLook.Save(GetTree(), shots, shotName + "_2_debrief");
                    _shotStage = 2;
                }
                if (++_debriefFrames < 5) return;
                int errors = ErrorCaptureLogger.Instance != null ? ErrorCaptureLogger.Instance.Snapshot().Item3 : 0;
                CampaignPilot.End(gm, _currentConfig, _currentReport.Result ?? (_leftEarly ? "left" : "defeat"), gm.CurrentWave, _gameSeconds, _towersPlacedThisRun, errors);
                _state = State.RunComplete;
                return;
            }
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
                // Reset core state from previous defeat before starting next run
                gm.SetCoreLives(Constants.CORE_LIVES);

                GD.Print($"[AutoPlayer] Starting next run: {_currentConfig}");
                if (!LaunchRun(gm)) { FinishAll(); return; }

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
                    _stateTimer = VisualWalk.Instance != null ? 120f : 1f; // Let debrief render (and be shot)
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
            if (node is VineNode vn && vn.Data?.AutoFires == true)
            {
                _towersPlacedThisRun++;
                if (_currentReport != null)
                {
                    string k = vn.Data.Type.ToString();
                    _currentReport.PlacedByType[k] = _currentReport.PlacedByType.TryGetValue(k, out var n) ? n + 1 : 1;
                }
            }
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

            GD.Print($"[AutoPlayer] Headless start: {_currentConfig}");
            if (!LaunchRun(gm)) { FinishAll(); return; }

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
