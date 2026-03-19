using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// DifficultyScaler - JSON-loadable difficulty scaling system.
    /// Ported from HoldtheLine's DifficultySystem.
    ///
    /// Provides per-stat multipliers with three-tier piecewise scaling:
    /// base rate -> 2x -> 4x -> 8x as game time progresses.
    /// Also handles surge acceleration and boss spawn timing.
    /// </summary>
    public partial class DifficultyScaler : Node
    {
        // Per-minute base scaling rates
        private float _hpScale = 0.02f;
        private float _damageScale = 0.015f;
        private float _speedScale = 0.01f;
        private float _armorScale = 0.005f;

        // Acceleration phase boundaries (minutes)
        private float _phase1Start = 8f;
        private float _phase1Mult = 2f;
        private float _phase2Start = 14f;
        private float _phase2Mult = 4f;
        private float _phase3Start = 20f;
        private float _phase3Mult = 8f;

        // Surge config
        private float _surgeInterval = 25f;
        private float _surgeVariance = 8f;
        private float _surgeDuration = 5f;
        private float _surgeSpawnMult = 2.5f;
        private float _surgeAccelPerMinute = 0.1f;

        // Boss config
        private float _bossFirstSpawn = 120f;
        private float _bossRepeatInterval = 90f;
        private float _bossDecreasePercent = 0.1f;
        private float _bossLateGameStart = 15f;
        private float _bossLateGameInterval = 30f;
        private float _bossLateGameDecreasePerMin = 5f;

        // Runtime
        private float _gameTime;
        private float _surgeTimer;
        private float _nextSurgeAt;
        private float _surgeEndTimer;
        private bool _isSurgeActive;
        private int _surgeCount;

        private float _bossTimer;
        private float _nextBossAt;
        private int _bossSpawnCount;

        private bool _isActive;
        private readonly RandomNumberGenerator _rng = new();

        public bool IsSurgeActive => _isSurgeActive;
        public int SurgeCount => _surgeCount;
        public float GameTime => _gameTime;

        public override void _Ready()
        {
            LoadConfig();
            ServiceLocator.Register(this);
        }

        private void LoadConfig()
        {
            string path = "res://Data/difficulty_scaling.json";
            if (!FileAccess.FileExists(path))
            {
                GD.PushWarning("[DifficultyScaler] No difficulty_scaling.json found, using defaults");
                return;
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();
            var parsed = Json.ParseString(json);
            if (parsed.VariantType != Variant.Type.Dictionary) return;

            var data = parsed.AsGodotDictionary();

            _hpScale = GetFloat(data, "enemy_hp_scale_per_minute", _hpScale);
            _damageScale = GetFloat(data, "enemy_damage_scale_per_minute", _damageScale);
            _speedScale = GetFloat(data, "enemy_speed_scale_per_minute", _speedScale);
            _armorScale = GetFloat(data, "enemy_armor_scale_per_minute", _armorScale);

            if (data.ContainsKey("acceleration_phases"))
            {
                var phases = data["acceleration_phases"].AsGodotDictionary();
                _phase1Start = GetFloat(phases, "phase_1_start_minutes", _phase1Start);
                _phase1Mult = GetFloat(phases, "phase_1_multiplier", _phase1Mult);
                _phase2Start = GetFloat(phases, "phase_2_start_minutes", _phase2Start);
                _phase2Mult = GetFloat(phases, "phase_2_multiplier", _phase2Mult);
                _phase3Start = GetFloat(phases, "phase_3_start_minutes", _phase3Start);
                _phase3Mult = GetFloat(phases, "phase_3_multiplier", _phase3Mult);
            }

            _surgeInterval = GetFloat(data, "surge_interval_seconds", _surgeInterval);
            _surgeVariance = GetFloat(data, "surge_interval_variance", _surgeVariance);
            _surgeDuration = GetFloat(data, "surge_duration_seconds", _surgeDuration);
            _surgeSpawnMult = GetFloat(data, "surge_spawn_multiplier", _surgeSpawnMult);
            _surgeAccelPerMinute = GetFloat(data, "surge_acceleration_per_minute", _surgeAccelPerMinute);

            _bossFirstSpawn = GetFloat(data, "boss_first_spawn_seconds", _bossFirstSpawn);
            _bossRepeatInterval = GetFloat(data, "boss_repeat_interval_seconds", _bossRepeatInterval);
            _bossDecreasePercent = GetFloat(data, "boss_interval_decrease_percent", _bossDecreasePercent);
            _bossLateGameStart = GetFloat(data, "boss_late_game_start_minutes", _bossLateGameStart);
            _bossLateGameInterval = GetFloat(data, "boss_late_game_interval_seconds", _bossLateGameInterval);
            _bossLateGameDecreasePerMin = GetFloat(data, "boss_late_game_decrease_per_minute", _bossLateGameDecreasePerMin);

            GD.Print("[DifficultyScaler] Config loaded from difficulty_scaling.json");
        }

        private static float GetFloat(Godot.Collections.Dictionary data, string key, float fallback)
        {
            if (data.ContainsKey(key))
                return (float)data[key].AsDouble();
            return fallback;
        }

        /// <summary>
        /// Start the difficulty scaler. Call when wave phase begins.
        /// </summary>
        public void Activate()
        {
            _isActive = true;
            _gameTime = 0f;
            _surgeTimer = 0f;
            _surgeEndTimer = 0f;
            _isSurgeActive = false;
            _surgeCount = 0;
            _bossTimer = 0f;
            _bossSpawnCount = 0;
            _nextBossAt = _bossFirstSpawn + _rng.RandfRange(-20f, 20f);
            ComputeNextSurgeTime();
        }

        public void Deactivate()
        {
            _isActive = false;
        }

        public override void _Process(double delta)
        {
            if (!_isActive) return;

            float dt = (float)delta;
            _gameTime += dt;

            // Surge timing
            _surgeTimer += dt;
            if (!_isSurgeActive && _surgeTimer >= _nextSurgeAt)
                StartSurge();

            if (_isSurgeActive)
            {
                _surgeEndTimer += dt;
                if (_surgeEndTimer >= _surgeDuration)
                    EndSurge();
            }

            // Boss timing
            _bossTimer += dt;
            if (_bossTimer >= _nextBossAt)
            {
                _bossTimer -= _nextBossAt;
                GameEvents.OnBossSpawned?.Invoke();
                _bossSpawnCount++;
                ComputeNextBossTime();
            }
        }

        // ── Surge management ──

        private float GetCurrentSurgeInterval()
        {
            float minutesElapsed = _gameTime / 60f;
            float accelFactor = Mathf.Pow(1f - _surgeAccelPerMinute, minutesElapsed);
            return Mathf.Max(5f, _surgeInterval * accelFactor);
        }

        private void ComputeNextSurgeTime()
        {
            float baseInterval = GetCurrentSurgeInterval();
            _nextSurgeAt = baseInterval + _rng.RandfRange(-_surgeVariance, _surgeVariance);
            _nextSurgeAt = Mathf.Max(3f, _nextSurgeAt);
        }

        private void StartSurge()
        {
            _isSurgeActive = true;
            _surgeTimer = 0f;
            _surgeEndTimer = 0f;
            _surgeCount++;
            GD.Print($"[DifficultyScaler] Surge #{_surgeCount} started at {_gameTime:F0}s");
        }

        private void EndSurge()
        {
            _isSurgeActive = false;
            _surgeEndTimer = 0f;
            ComputeNextSurgeTime();
            GD.Print($"[DifficultyScaler] Surge #{_surgeCount} ended");
        }

        // ── Boss timing ──

        private void ComputeNextBossTime()
        {
            float minutesElapsed = _gameTime / 60f;

            if (minutesElapsed >= _bossLateGameStart)
            {
                float minutesPast = minutesElapsed - _bossLateGameStart;
                _nextBossAt = Mathf.Max(1f, _bossLateGameInterval - _bossLateGameDecreasePerMin * minutesPast);
            }
            else
            {
                float shrinkFactor = Mathf.Pow(1f - _bossDecreasePercent, _bossSpawnCount);
                _nextBossAt = _bossRepeatInterval * shrinkFactor;
                _nextBossAt += _rng.RandfRange(-20f, 20f);
                _nextBossAt = Mathf.Max(30f, _nextBossAt);
            }
        }

        // ── Public multiplier queries ──

        /// <summary>
        /// Get the current HP multiplier for enemy scaling.
        /// </summary>
        public float GetHpMultiplier()
        {
            return 1f + GetAcceleratedScaling(_gameTime / 60f, _hpScale);
        }

        /// <summary>
        /// Get the current damage multiplier for enemy scaling.
        /// </summary>
        public float GetDamageMultiplier()
        {
            return 1f + GetAcceleratedScaling(_gameTime / 60f, _damageScale);
        }

        /// <summary>
        /// Get the current speed multiplier for enemy scaling.
        /// </summary>
        public float GetSpeedMultiplier()
        {
            return 1f + GetAcceleratedScaling(_gameTime / 60f, _speedScale);
        }

        /// <summary>
        /// Get the current armor bonus for enemy scaling.
        /// </summary>
        public float GetArmorBonus()
        {
            return GetAcceleratedScaling(_gameTime / 60f, _armorScale);
        }

        /// <summary>
        /// Get the surge spawn multiplier (1.0 when no surge, higher during surges).
        /// </summary>
        public float GetSurgeSpawnMultiplier()
        {
            return _isSurgeActive ? _surgeSpawnMult : 1f;
        }

        /// <summary>
        /// Get the current acceleration phase name for UI display.
        /// </summary>
        public string GetAccelerationPhase()
        {
            float minutes = _gameTime / 60f;
            if (minutes < _phase1Start) return "normal";
            if (minutes < _phase2Start) return "accelerated_2x";
            if (minutes < _phase3Start) return "accelerated_4x";
            return "accelerated_8x";
        }

        // ── Piecewise scaling calculation ──

        /// <summary>
        /// Calculates effective scaling value with piecewise acceleration.
        /// Each phase contributes: duration * baseRate * phaseMultiplier.
        /// </summary>
        private float GetAcceleratedScaling(float minutesElapsed, float baseRate)
        {
            if (minutesElapsed <= _phase1Start)
                return baseRate * minutesElapsed;

            float total = baseRate * _phase1Start; // 0 to phase1 at 1x

            float phase1Minutes = Mathf.Min(minutesElapsed, _phase2Start) - _phase1Start;
            total += baseRate * phase1Minutes * _phase1Mult;

            if (minutesElapsed > _phase2Start)
            {
                float phase2Minutes = Mathf.Min(minutesElapsed, _phase3Start) - _phase2Start;
                total += baseRate * phase2Minutes * _phase2Mult;
            }

            if (minutesElapsed > _phase3Start)
            {
                float phase3Minutes = minutesElapsed - _phase3Start;
                total += baseRate * phase3Minutes * _phase3Mult;
            }

            return total;
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<DifficultyScaler>();
        }
    }
}
