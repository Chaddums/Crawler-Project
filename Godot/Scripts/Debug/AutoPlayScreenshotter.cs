using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Contextual screenshot system for AutoPlayer.
    /// Captures screenshots on game events with metadata sidecars,
    /// plus anomaly detection for HP spikes, falls, and stuck UI.
    /// </summary>
    public partial class AutoPlayScreenshotter : Node
    {
        private const float MIN_CAPTURE_INTERVAL = 1.0f;
        private const float PERIODIC_INTERVAL = 5.0f;
        private const float HP_SPIKE_THRESHOLD = 0.30f;

        public string RunId { get; private set; }
        public string RunDir { get; private set; }
        public string ScreenshotDir { get; private set; }

        private int _captureIndex;
        private float _lastCaptureTime;
        private float _periodicTimer;
        private float _lastHpPercent = 1f;
        private readonly List<ScreenshotEntry> _entries = new();
        private readonly List<string> _anomalies = new();
        private bool _subscribed;
        private int _eventsVersion = -1;
        private float _stateChangeGracePeriod; // suppress fall anomaly during dungeon intro

        // Known-good reference library — builds over time
        private string _knownGoodDir;
        private readonly HashSet<string> _existingReferences = new();

        public IReadOnlyList<ScreenshotEntry> Entries => _entries;
        public IReadOnlyList<string> Anomalies => _anomalies;

        public override void _Ready()
        {
            RunId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            RunDir = ProjectSettings.GlobalizePath($"user://autoplay/{RunId}");
            ScreenshotDir = RunDir + "/screenshots";

            DirAccess.MakeDirRecursiveAbsolute(ScreenshotDir);

            // Known-good reference directory (persists across runs)
            _knownGoodDir = ProjectSettings.GlobalizePath("user://autoplay/known_good");
            DirAccess.MakeDirRecursiveAbsolute(_knownGoodDir);
            LoadExistingReferences();

            Subscribe();
            GD.Print($"[Screenshotter] Initialized — run {RunId}, dir: {RunDir}, known_good refs: {_existingReferences.Count}");
        }

        public override void _ExitTree()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            _subscribed = true;
            _eventsVersion = GameEvents.Version;

            GameEvents.OnRoomEntered += OnRoomEntered;
            GameEvents.OnRoomCleared += OnRoomCleared;
            GameEvents.OnBossSpawned += OnBossSpawned;
            GameEvents.OnBossDefeated += OnBossDefeated;
            GameEvents.OnPlayerDeath += OnPlayerDeath;
            GameEvents.OnLootBoxOpened += OnLootBoxOpened;
            GameEvents.OnGameStateChanged += OnStateChanged;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            GameEvents.OnRoomEntered -= OnRoomEntered;
            GameEvents.OnRoomCleared -= OnRoomCleared;
            GameEvents.OnBossSpawned -= OnBossSpawned;
            GameEvents.OnBossDefeated -= OnBossDefeated;
            GameEvents.OnPlayerDeath -= OnPlayerDeath;
            GameEvents.OnLootBoxOpened -= OnLootBoxOpened;
            GameEvents.OnGameStateChanged -= OnStateChanged;
        }

        // ---- Event handlers ----

        private void OnRoomEntered(Node room)
        {
            var rc = room as RoomController;
            string desc = rc != null ? $"Entered {rc.RoomType} room" : "Entered room";
            Capture("room_entry", desc);
        }

        private void OnRoomCleared(Node room)
        {
            Capture("room_cleared", "Room cleared");
        }

        private void OnBossSpawned(Node boss)
        {
            Capture("boss_spawn", $"Boss spawned: {boss?.Name ?? "unknown"}");
        }

        private void OnBossDefeated(Node boss)
        {
            Capture("boss_killed", $"Boss defeated: {boss?.Name ?? "unknown"}");
        }

        private void OnPlayerDeath(Node player)
        {
            // Death always captures, ignore throttle
            Capture("death", "Player died", forceCapture: true);
        }

        private void OnLootBoxOpened(LootBoxOpenedData data)
        {
            Capture("loot_ceremony", "Loot box opened");
        }

        private void OnStateChanged(GameState state)
        {
            _stateChangeGracePeriod = 10f; // suppress fall detection for 10s after state change
            Capture("state_change", $"State → {state}");
        }

        // ---- Anomaly detection in _Process ----

        public override void _Process(double delta)
        {
            // Re-subscribe after GameEvents.ClearAll() wipes our handlers
            if (_eventsVersion != GameEvents.Version)
                Subscribe();

            var player = PlayerManager.P1;
            if (player == null) return;

            float dt = (float)delta;

            // HP spike detection
            if (player.Health != null && player.Health.IsAlive)
            {
                float hpPct = player.Health.HealthPercent;
                if (_lastHpPercent - hpPct > HP_SPIKE_THRESHOLD)
                {
                    string msg = $"HP spike: {_lastHpPercent * 100:F0}% → {hpPct * 100:F0}%";
                    _anomalies.Add(msg);
                    Capture("anomaly_hp_spike", msg);
                }
                _lastHpPercent = hpPct;
            }

            // Grace period countdown (suppress fall detection during dungeon intro)
            if (_stateChangeGracePeriod > 0f)
                _stateChangeGracePeriod -= dt;

            // Fall detection (suppressed during state transition grace period)
            if (player.GlobalPosition.Y < -3f && _stateChangeGracePeriod <= 0f)
            {
                string msg = $"Player falling at Y={player.GlobalPosition.Y:F1}";
                _anomalies.Add(msg);
                Capture("anomaly_fall", msg);
            }

            // UI stuck detection — check if a blocking UI has been visible too long
            DetectUIStuck(dt);

            // Periodic capture during gameplay
            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentState == GameState.InSector)
            {
                _periodicTimer -= dt;
                if (_periodicTimer <= 0f)
                {
                    _periodicTimer = PERIODIC_INTERVAL;
                    Capture("periodic", "Periodic capture");
                }
            }
        }

        private float _uiStuckAccumulator;
        private bool _lastHadBlockingUI;

        private void DetectUIStuck(float dt)
        {
            bool hasBlockingUI = false;
            foreach (var child in GetTree().Root.GetChildren())
            {
                if (child is LootBoxCeremonyUI or RelicCacheUI)
                {
                    hasBlockingUI = true;
                    break;
                }
            }

            if (hasBlockingUI)
            {
                _uiStuckAccumulator += dt;
                if (_uiStuckAccumulator > 10f && !_lastHadBlockingUI)
                {
                    _lastHadBlockingUI = true;
                    string msg = $"UI blocking for {_uiStuckAccumulator:F0}s";
                    _anomalies.Add(msg);
                    Capture("anomaly_ui_stuck", msg);
                }
            }
            else
            {
                _uiStuckAccumulator = 0f;
                _lastHadBlockingUI = false;
            }
        }

        // ---- Core capture ----

        public void Capture(string category, string description, bool forceCapture = false)
        {
            float now = (float)Time.GetTicksMsec() / 1000f;
            if (!forceCapture && now - _lastCaptureTime < MIN_CAPTURE_INTERVAL) return;
            _lastCaptureTime = now;

            var viewport = GetViewport();
            if (viewport == null) return;

            var img = viewport.GetTexture()?.GetImage();
            if (img == null) return;

            _captureIndex++;
            var gm = GameManager.Instance;
            var player = PlayerManager.P1;

            string sector = gm != null ? $"S{gm.CurrentSector}A{gm.CurrentArea}" : "??";
            string roomType = "";
            if (player != null && gm != null)
            {
                var grid = WorldToGrid(player.GlobalPosition);
                var gen = FindGenerator();
                if (gen != null && gen.RoomControllers.TryGetValue(grid, out var rc))
                    roomType = $"_{rc.RoomType}";
            }

            string baseName = $"{_captureIndex:D3}_{category}_{sector}{roomType}";
            string pngPath = $"{ScreenshotDir}/{baseName}.png";
            string jsonPath = $"{ScreenshotDir}/{baseName}.json";

            img.SavePng(pngPath);

            // Build metadata
            var entry = new ScreenshotEntry
            {
                Index = _captureIndex,
                Timestamp = DateTime.Now.ToString("o"),
                Category = category,
                Description = description,
                PngFile = $"{baseName}.png",
                GameState = gm?.CurrentState.ToString() ?? "Unknown",
                Sector = gm?.CurrentSector ?? 0,
                Area = gm?.CurrentArea ?? 0,
                PlayerHp = player?.Health?.CurrentHealth ?? 0,
                PlayerMaxHp = player?.Health?.MaxHealth ?? 0,
                PlayerMana = player?.Stats?.CurrentMana ?? 0,
                PlayerLevel = player?.Stats?.Level ?? 0,
                BotFrame = player?.ClassController?.CurrentClass?.ToString() ?? "Unknown",
                PlayerPos = player != null
                    ? $"({player.GlobalPosition.X:F1},{player.GlobalPosition.Y:F1},{player.GlobalPosition.Z:F1})"
                    : "(none)",
                EnemyCount = player?.GetTree().GetNodesInGroup(Constants.GROUP_ENEMY).Count ?? 0,
                InventoryCount = player?.Inventory?.ItemCount ?? 0,
                EquippedWeapon = GetEquippedWeaponName(player),
                SceneContext = SceneContext.CaptureShort(GetTree())
            };

            _entries.Add(entry);

            // Write sidecar JSON
            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
            using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Write);
            file?.StoreString(json);

            // Also save latest.png for quick viewing
            img.SavePng(RunDir + "/latest.png");

            // Build known-good reference if conditions are stable
            TrySaveKnownGoodReference(category, img, entry);
        }

        // ---- Known-good reference library ----

        /// <summary>
        /// Scan existing known_good directory to see which reference keys we already have.
        /// </summary>
        private void LoadExistingReferences()
        {
            var dir = DirAccess.Open(_knownGoodDir);
            if (dir == null) return;

            dir.ListDirBegin();
            string file = dir.GetNext();
            while (!string.IsNullOrEmpty(file))
            {
                if (file.EndsWith(".png"))
                    _existingReferences.Add(file.Replace(".png", ""));
                file = dir.GetNext();
            }
            dir.ListDirEnd();
        }

        /// <summary>
        /// Determines a reference key for this capture context.
        /// Categories that are meaningful for visual regression:
        ///   hud_combat, safe_room, boss_fight, loot_ceremony, room_cleared, character_creation
        /// Returns null if this category isn't worth a reference.
        /// </summary>
        private static string GetReferenceKey(string category, ScreenshotEntry entry)
        {
            // Map capture categories to stable reference keys
            // Include bot frame so we have per-frame references
            string frame = entry.BotFrame ?? "Unknown";

            return category switch
            {
                "room_entry" => $"room_entry_{frame}_S{entry.Sector}",
                "room_cleared" => $"room_cleared_{frame}_S{entry.Sector}",
                "boss_spawn" => $"boss_spawn_{frame}_S{entry.Sector}",
                "boss_killed" => $"boss_killed_{frame}_S{entry.Sector}",
                "loot_ceremony" => $"loot_ceremony_{frame}",
                "safe_room" => $"safe_room_{frame}",
                "state_change" when entry.GameState == "CharacterCreation" => "character_creation",
                "state_change" when entry.GameState == "MainMenu" => "main_menu",
                "periodic" => $"periodic_{frame}_S{entry.Sector}",
                _ => null // anomalies, deaths — not reference material
            };
        }

        /// <summary>
        /// Save a known-good reference screenshot if:
        /// 1. We don't already have one for this context key
        /// 2. The game state looks healthy (player alive, HP > 50%, no recent anomalies)
        /// </summary>
        private void TrySaveKnownGoodReference(string category, Image img, ScreenshotEntry entry)
        {
            var key = GetReferenceKey(category, entry);
            if (key == null) return;
            if (_existingReferences.Contains(key)) return;

            // Only save references when things look healthy
            bool isHealthy = entry.PlayerHp > entry.PlayerMaxHp * 0.5f || entry.PlayerMaxHp == 0;
            bool noRecentAnomalies = _anomalies.Count == 0;

            if (!isHealthy || !noRecentAnomalies) return;

            // Save reference PNG + JSON sidecar
            string pngPath = $"{_knownGoodDir}/{key}.png";
            string jsonPath = $"{_knownGoodDir}/{key}.json";

            img.SavePng(pngPath);

            var refData = new
            {
                key,
                captured_run = RunId,
                captured_at = entry.Timestamp,
                source_category = category,
                entry.BotFrame,
                entry.Sector,
                entry.Area,
                entry.GameState,
                entry.PlayerLevel,
                entry.EnemyCount,
                note = "Auto-captured known-good reference. Delete to re-capture on next run."
            };
            var json = System.Text.Json.JsonSerializer.Serialize(refData,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Write);
            file?.StoreString(json);

            _existingReferences.Add(key);
            GD.Print($"[Screenshotter] Saved known-good reference: {key}");
        }

        private string GetEquippedWeaponName(PlayerController player)
        {
            if (player?.Inventory == null) return "None";
            if (player.Inventory.Equipped.TryGetValue(EquipmentSlot.MainHand, out var weapon))
                return weapon?.GetDisplayName() ?? "None";
            return "None";
        }

        private DungeonGenerator FindGenerator()
        {
            var sm = FindSectorManager(GetTree().Root);
            return sm?.Generator;
        }

        private static SectorManager FindSectorManager(Node root)
        {
            if (root is SectorManager sm) return sm;
            foreach (var child in root.GetChildren())
            {
                var found = FindSectorManager(child);
                if (found != null) return found;
            }
            return null;
        }

        private static Vector2I WorldToGrid(Vector3 worldPos)
        {
            return new Vector2I(
                Mathf.RoundToInt(worldPos.X / DungeonGenerator.ROOM_SPACING),
                Mathf.RoundToInt(worldPos.Z / DungeonGenerator.ROOM_SPACING));
        }
    }

    public class ScreenshotEntry
    {
        public int Index { get; set; }
        public string Timestamp { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string PngFile { get; set; }
        public string GameState { get; set; }
        public int Sector { get; set; }
        public int Area { get; set; }
        public float PlayerHp { get; set; }
        public float PlayerMaxHp { get; set; }
        public float PlayerMana { get; set; }
        public int PlayerLevel { get; set; }
        public string BotFrame { get; set; }
        public string PlayerPos { get; set; }
        public int EnemyCount { get; set; }
        public int InventoryCount { get; set; }
        public string EquippedWeapon { get; set; }
        public string SceneContext { get; set; }
    }
}
