using Godot;
using System;
using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// Central audio manager: SFX pool, music, and voice playback.
    /// Generates procedural sound effects from raw PCM data when no audio files exist.
    /// Creates audio buses programmatically (SFX, Music, Voice).
    /// </summary>
    public partial class AudioManager : Node
    {
        private const int SFX_POOL_SIZE = 8;
        private const int SAMPLE_RATE = 22050;
        private AudioStreamPlayer[] _sfxPlayers;
        private int _sfxIndex;
        private AudioStreamPlayer _musicPlayer;
        private AudioStreamPlayer _voicePlayer;

        private static readonly Dictionary<string, AudioStreamWav> _cachedSounds = new();
        private static readonly Random _variantRng = new();

        public override void _Ready()
        {
            // Create audio buses programmatically — TD has no bus layout file
            EnsureBus("SFX");
            EnsureBus("Music");
            EnsureBus("Voice");

            // SFX pool — round-robin for overlapping sounds
            _sfxPlayers = new AudioStreamPlayer[SFX_POOL_SIZE];
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                _sfxPlayers[i] = new AudioStreamPlayer();
                _sfxPlayers[i].Bus = "SFX";
                AddChild(_sfxPlayers[i]);
            }

            // Music player
            _musicPlayer = new AudioStreamPlayer();
            _musicPlayer.Bus = "Music";
            AddChild(_musicPlayer);

            // Voice/commentary player
            _voicePlayer = new AudioStreamPlayer();
            _voicePlayer.Bus = "Voice";
            AddChild(_voicePlayer);

            ServiceLocator.Register(this);

            // Subscribe to TD game events
            GameEvents.OnDamageDealt += OnDamageDealt;
            GameEvents.OnEnemyKilled += OnEnemyKilledSfx;
            GameEvents.OnEnemyLeaked += OnEnemyLeakedSfx;
            GameEvents.OnVineNodePlaced += OnVineNodePlacedSfx;
            GameEvents.OnVineNodeSold += OnVineNodeSoldSfx;
            GameEvents.OnVineNodeDestroyed += OnVineNodeDestroyedSfx;
            GameEvents.OnTowerPlaced += OnTowerPlacedSfx;
            GameEvents.OnTowerSold += OnTowerSoldSfx;
            GameEvents.OnSignalFired += OnSignalFiredSfx;
            GameEvents.OnSignalReceived += OnSignalReceivedSfx;
            GameEvents.OnGateStateChanged += OnGateStateChangedSfx;
            GameEvents.OnSwitchToggled += OnSwitchToggledSfx;
            GameEvents.OnWaveStarted += OnWaveStartedSfx;
            GameEvents.OnWaveCompleted += OnWaveCompletedSfx;
            // S1: OnFloorCompleted removed — S2 will add OnWaveMilestone
            GameEvents.OnAllWavesCleared += OnAllWavesClearedSfx;
            GameEvents.OnBossSpawned += OnBossSpawnedSfx;
            GameEvents.OnSurgeStarted += OnSurgeStartedSfx;
            GameEvents.OnSurgeEnded += OnSurgeEndedSfx;
            GameEvents.OnPlayerHPChanged += OnPlayerHPChangedSfx;
            GameEvents.OnPlayerDied += OnPlayerDiedSfx;
            GameEvents.OnHarvesterDamaged += OnHarvesterDamagedSfx;
            GameEvents.OnDomeCollapsed += OnDomeCollapsedSfx;
            GameEvents.OnMiningModeChanged += OnMiningModeChangedSfx;
            GameEvents.OnResourcesDropped += OnResourcesDroppedSfx;
            GameEvents.OnResourcesCollected += OnResourcesCollectedSfx;
            GameEvents.OnBuffApplied += OnBuffAppliedSfx;
            GameEvents.OnDebuffApplied += OnDebuffAppliedSfx;
            GameEvents.OnPhaseChanged += OnPhaseChangedSfx;
            GameEvents.OnCommentary += OnCommentarySfx;
            GameEvents.OnAbilityCooldownChanged += OnAbilityCooldownChangedSfx;

            GD.Print("[AudioManager] Ready — SFX/Music/Voice buses created");
        }

        private static void EnsureBus(string busName)
        {
            if (AudioServer.GetBusIndex(busName) >= 0) return;
            int idx = AudioServer.BusCount;
            AudioServer.AddBus();
            AudioServer.SetBusName(idx, busName);
            AudioServer.SetBusSend(idx, "Master");
        }

        // ── Playback ──

        /// <summary>
        /// Play an AudioStream as a one-shot SFX.
        /// </summary>
        public void PlaySFX(AudioStream stream, float volumeDb = 0f)
        {
            if (stream == null) return;
            if (_sfxPlayers == null) return;

            var player = _sfxPlayers[_sfxIndex];
            if (!GodotObject.IsInstanceValid(player)) return;

            player.Stream = stream;
            player.VolumeDb = volumeDb;
            player.Play();

            _sfxIndex = (_sfxIndex + 1) % SFX_POOL_SIZE;
        }

        /// <summary>
        /// Play a named SFX. Checks three sources in order:
        /// 1. AudioLoader manifest (uses volume_db metadata from audio.json)
        /// 2. Convention path res://Audio/SFX/{name}.wav
        /// 3. Procedural generation fallback
        /// </summary>
        public void PlaySFXByName(string sfxName)
        {
            if (string.IsNullOrEmpty(sfxName)) return;

            // 1. AudioLoader manifest lookup
            var entry = AudioLoader.GetEntry($"sfx.{sfxName}");
            if (entry != null && ResourceLoader.Exists(entry.Path))
            {
                PlaySFX(GD.Load<AudioStream>(entry.Path), entry.VolumeDb);
                return;
            }

            // Also check abilities category
            entry = AudioLoader.GetEntry($"abilities.{sfxName}");
            if (entry != null && ResourceLoader.Exists(entry.Path))
            {
                PlaySFX(GD.Load<AudioStream>(entry.Path), entry.VolumeDb);
                return;
            }

            // 2. Convention path fallback
            string path = $"res://Audio/SFX/{sfxName}.wav";
            if (ResourceLoader.Exists(path))
            {
                PlaySFX(GD.Load<AudioStream>(path));
                return;
            }

            // 3. Procedural generation fallback
            if (!_cachedSounds.TryGetValue(sfxName, out var stream))
            {
                stream = GenerateSound(sfxName);
                if (stream != null)
                    _cachedSounds[sfxName] = stream;
            }

            if (stream != null)
                PlaySFX(stream, -6f);
        }

        /// <summary>
        /// Play a named SFX with random variation. Looks for manifest entries
        /// named {sfxName}_1, {sfxName}_2, ... {sfxName}_N and picks one at random.
        /// Falls back to PlaySFXByName(sfxName) if no variants exist.
        /// </summary>
        public void PlayRandomSFXByName(string sfxName, int maxVariants = 4)
        {
            if (string.IsNullOrEmpty(sfxName)) return;

            // Collect available variants
            var available = new List<string>();
            for (int i = 1; i <= maxVariants; i++)
            {
                string variantKey = $"{sfxName}_{i}";
                var entry = AudioLoader.GetEntry($"sfx.{variantKey}");
                if (entry != null && ResourceLoader.Exists(entry.Path))
                    available.Add(variantKey);
            }

            if (available.Count > 0)
            {
                string picked = available[_variantRng.Next(available.Count)];
                PlaySFXByName(picked);
            }
            else
            {
                PlaySFXByName(sfxName);
            }
        }

        /// <summary>
        /// Play background music. Loops by default.
        /// </summary>
        public void PlayMusic(AudioStream stream, float volumeDb = -10f)
        {
            if (stream == null) return;
            _musicPlayer.Stream = stream;
            _musicPlayer.VolumeDb = volumeDb;
            _musicPlayer.Play();
        }

        public void StopMusic()
        {
            _musicPlayer.Stop();
        }

        /// <summary>
        /// Play ambient audio for a battle floor. Tries manifest ambient.battle_floor_N,
        /// falls back to procedural ambient generation.
        /// </summary>
        public void PlayBattleAmbience(int floor)
        {
            string key = $"ambient.battle_floor_{floor}";
            var entry = AudioLoader.GetEntry(key);
            if (entry != null && ResourceLoader.Exists(entry.Path))
            {
                var stream = GD.Load<AudioStream>(entry.Path);
                if (stream != null)
                {
                    PlayMusic(stream, entry.VolumeDb);
                    return;
                }
            }

            // Procedural fallback
            string cacheKey = $"ambience_floor_{floor}";
            if (!_cachedSounds.TryGetValue(cacheKey, out var wavStream))
            {
                wavStream = GenerateBattleAmbience(floor);
                if (wavStream != null)
                    _cachedSounds[cacheKey] = wavStream;
            }

            if (wavStream != null)
            {
                wavStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wavStream.LoopEnd = (int)(SAMPLE_RATE * 8);
                PlayMusic(wavStream, -14f);
            }
        }

        private static AudioStreamWav GenerateBattleAmbience(int floor)
        {
            float duration = 8f;
            int sampleCount = (int)(SAMPLE_RATE * duration);
            var samples = new short[sampleCount];
            var rng = new Random(floor * 1000 + 42);

            float baseDroneHz = floor switch
            {
                1 => 55f,
                2 => 49f,
                3 => 41f,
                4 => 46.25f,
                5 => 36.7f,
                _ => 55f * MathF.Pow(0.9f, floor - 1),
            };

            float dissonanceHz = baseDroneHz * 1.498f;
            float noiseFloor = 0.02f + floor * 0.008f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float loopFade = t > duration - 0.5f ? (duration - t) * 2f : 1f;
                if (t < 0.5f) loopFade = MathF.Min(loopFade, t * 2f);

                float drone = MathF.Sin(2f * MathF.PI * baseDroneHz * t) * 0.3f;
                drone += MathF.Sin(2f * MathF.PI * baseDroneHz * 2f * t) * 0.15f;
                drone += MathF.Sin(2f * MathF.PI * baseDroneHz * 3f * t) * 0.08f;

                float dissonance = MathF.Sin(2f * MathF.PI * dissonanceHz * t) * 0.08f;

                float pulseRate = 0.3f + floor * 0.05f;
                float subPulse = MathF.Sin(2f * MathF.PI * pulseRate * t);
                float sub = MathF.Sin(2f * MathF.PI * baseDroneHz * 0.5f * t) * 0.2f * (0.5f + subPulse * 0.5f);

                float noise = ((float)rng.NextDouble() * 2f - 1f) * noiseFloor;

                float sample = (drone + dissonance + sub + noise) * loopFade;
                sample = MathF.Max(-0.9f, MathF.Min(0.9f, sample));
                samples[i] = (short)(sample * 12000);
            }

            return CreateWavStream(samples);
        }

        /// <summary>
        /// Play a voice line (commentary). Interrupts current voice.
        /// </summary>
        public void PlayVoice(AudioStream stream, float volumeDb = 0f)
        {
            if (stream == null) return;
            _voicePlayer.Stream = stream;
            _voicePlayer.VolumeDb = volumeDb;
            _voicePlayer.Play();
        }

        public bool IsVoicePlaying => _voicePlayer.Playing;

        // ── TD Event Handlers ──

        private void OnDamageDealt(DamageInfo damage)
        {
            if (damage.IsCritical)
                PlayRandomSFXByName("crit_hit");
            else
                PlayRandomSFXByName("hit");
        }

        private void OnEnemyKilledSfx(Node _) => PlayRandomSFXByName("enemy_death");
        private void OnEnemyLeakedSfx(Node _, Vector3 __) => PlaySFXByName("enemy_leak");
        private void OnVineNodePlacedSfx(Node _) => PlaySFXByName("node_place");
        private void OnVineNodeSoldSfx(Node _) => PlaySFXByName("node_sell");
        private void OnVineNodeDestroyedSfx(Node _) => PlaySFXByName("node_destroy");
        private void OnTowerPlacedSfx(Node _) => PlaySFXByName("tower_place");
        private void OnTowerSoldSfx(Node _) => PlaySFXByName("node_sell");
        private void OnSignalFiredSfx(Node _, SignalType __) => PlaySFXByName("signal_fire");
        private void OnSignalReceivedSfx(Node _, SignalType __) => PlaySFXByName("signal_receive");

        private void OnGateStateChangedSfx(Node _, bool isOpen)
        {
            PlaySFXByName(isOpen ? "gate_open" : "gate_close");
        }

        private void OnSwitchToggledSfx(Node _, int __) => PlaySFXByName("switch_toggle");
        private void OnWaveStartedSfx(int _) => PlaySFXByName("wave_start");
        private void OnWaveCompletedSfx(int _) => PlaySFXByName("wave_complete");
        private void OnFloorCompletedSfx(int _) => PlaySFXByName("floor_complete");
        private void OnAllWavesClearedSfx(int _) => PlaySFXByName("victory");
        private void OnBossSpawnedSfx() => PlaySFXByName("boss_spawn");
        private void OnSurgeStartedSfx() => PlaySFXByName("surge_start");
        private void OnSurgeEndedSfx() => PlaySFXByName("surge_end");

        private void OnPlayerHPChangedSfx(float current, float max)
        {
            if (max > 0 && current / max < 0.25f)
                PlaySFXByName("heartbeat");
        }

        private void OnPlayerDiedSfx() => PlaySFXByName("player_death");
        private void OnHarvesterDamagedSfx(float _) => PlayRandomSFXByName("harvester_hit");
        private void OnDomeCollapsedSfx() => PlaySFXByName("dome_collapse");
        private void OnMiningModeChangedSfx(MiningMode _) => PlaySFXByName("mode_switch");
        private void OnResourcesDroppedSfx(Vector3 _, int __) => PlaySFXByName("scrap_drop");
        private void OnResourcesCollectedSfx(int _) => PlaySFXByName("scrap_collect");
        private void OnBuffAppliedSfx(Node _, string __, float ___) => PlaySFXByName("buff_apply");
        private void OnDebuffAppliedSfx(Node _, string __, float ___) => PlaySFXByName("debuff_apply");

        private void OnPhaseChangedSfx(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Build:
                    PlaySFXByName("phase_build");
                    break;
                case GamePhase.Defeat:
                    PlaySFXByName("defeat");
                    break;
            }
        }

        private void OnCommentarySfx(string _, string __) => PlaySFXByName("commentary_ping");

        private void OnAbilityCooldownChangedSfx(int slot, float cooldown)
        {
            if (cooldown <= 0f)
                PlaySFXByName("ability_ready");
        }

        // ── Procedural Sound Generation ──

        private static AudioStreamWav GenerateSound(string name)
        {
            return name switch
            {
                "hit" => GenerateHitSound(),
                "crit_hit" => GenerateCritSound(),
                "enemy_death" => GenerateDeathSound(),
                "swing" => GenerateSwingSound(),
                "pickup" => GeneratePickupSound(),
                "projectile" => GenerateProjectileSound(),
                "heal" => GenerateHealSound(),
                "achievement" => GenerateAchievementSound(),
                "heartbeat" => GenerateHeartbeatSound(),
                "epic_drop" => GenerateEpicDropSound(),
                "item_reveal" => GenerateItemRevealSound(),
                "box_open" => GenerateBoxOpenSound(),
                "box_shake" => GenerateBoxShakeSound(),
                // TD-specific procedural mappings
                "node_place" => GeneratePickupSound(),
                "node_sell" => GeneratePickupSound(),
                "node_destroy" => GenerateDeathSound(),
                "tower_place" => GenerateAchievementSound(),
                "signal_fire" => GenerateProjectileSound(),
                "signal_receive" => GenerateItemRevealSound(),
                "gate_open" => GenerateBoxOpenSound(),
                "gate_close" => GenerateBoxShakeSound(),
                "switch_toggle" => GeneratePickupSound(),
                "wave_start" => GenerateLevelUpSound(),
                "wave_complete" => GenerateAchievementSound(),
                "floor_complete" => GenerateEpicDropSound(),
                "victory" => GenerateEpicDropSound(),
                "defeat" => GenerateHeartbeatSound(),
                "boss_spawn" => GenerateEpicDropSound(),
                "surge_start" => GenerateLevelUpSound(),
                "surge_end" => GenerateAchievementSound(),
                "enemy_leak" => GenerateHeartbeatSound(),
                "harvester_hit" => GenerateHitSound(),
                "dome_collapse" => GenerateDeathSound(),
                "scrap_drop" => GeneratePickupSound(),
                "scrap_collect" => GenerateItemRevealSound(),
                "buff_apply" => GenerateHealSound(),
                "debuff_apply" => GenerateSwingSound(),
                "mode_switch" => GeneratePickupSound(),
                "player_death" => GenerateDeathSound(),
                "commentary_ping" => GenerateItemRevealSound(),
                "ability_ready" => GeneratePickupSound(),
                "phase_build" => GenerateAchievementSound(),
                "shock_blast" => GenerateProjectileSound(),
                "repair_pulse" => GenerateHealSound(),
                "overclock" => GenerateLevelUpSound(),
                "button_click" => GeneratePickupSound(),
                "button_hover" => GenerateItemRevealSound(),
                _ => null
            };
        }

        private static AudioStreamWav GenerateHitSound()
        {
            float duration = 0.08f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(42);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = 1f - (t / duration);
                envelope *= envelope;

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone = MathF.Sin(2f * MathF.PI * 800f * t);
                float sample = (noise * 0.6f + tone * 0.4f) * envelope * 0.7f;

                samples[i] = (short)(sample * 16000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateSwingSound()
        {
            float duration = 0.15f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = MathF.Sin(progress * MathF.PI);

                float freq = Lerp(400f, 100f, progress);
                float sample = MathF.Sin(2f * MathF.PI * freq * t) * envelope * 0.5f;

                var rng = new Random(i);
                sample += (float)(rng.NextDouble() * 2.0 - 1.0) * envelope * 0.15f;

                samples[i] = (short)(sample * 14000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateDeathSound()
        {
            float duration = 0.35f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(99);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = 1f - progress;

                float freq = Lerp(300f, 50f, progress);
                float tone = MathF.Sin(2f * MathF.PI * freq * t);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float sample = (tone * 0.6f + noise * 0.3f) * envelope * 0.6f;

                samples[i] = (short)(sample * 14000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GeneratePickupSound()
        {
            float noteDuration = 0.08f;
            float totalDuration = noteDuration * 2.5f;
            var samples = new short[(int)(SAMPLE_RATE * totalDuration)];
            float[] freqs = { 440f, 660f };
            float[] starts = { 0f, noteDuration * 1.2f };

            for (int n = 0; n < freqs.Length; n++)
            {
                int startSample = (int)(starts[n] * SAMPLE_RATE);
                int noteLength = (int)(noteDuration * SAMPLE_RATE);

                for (int i = 0; i < noteLength && startSample + i < samples.Length; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    float envelope = MathF.Sin((float)i / noteLength * MathF.PI);
                    float sample = MathF.Sin(2f * MathF.PI * freqs[n] * t) * envelope * 0.5f;
                    samples[startSample + i] += (short)(sample * 12000);
                }
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateLevelUpSound()
        {
            float noteDuration = 0.08f;
            float totalDuration = noteDuration * 5f;
            var samples = new short[(int)(SAMPLE_RATE * totalDuration)];
            float[] freqs = { 262f, 330f, 392f, 523f };

            for (int n = 0; n < freqs.Length; n++)
            {
                int startSample = (int)(n * noteDuration * 1.1f * SAMPLE_RATE);
                int noteLength = (int)(noteDuration * 1.5f * SAMPLE_RATE);

                for (int i = 0; i < noteLength && startSample + i < samples.Length; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    float progress = (float)i / noteLength;
                    float envelope = 1f - progress;
                    envelope *= MathF.Min(1f, progress * 10f);

                    float sample = MathF.Sin(2f * MathF.PI * freqs[n] * t) * envelope * 0.45f;
                    sample += MathF.Sin(2f * MathF.PI * freqs[n] * 2f * t) * envelope * 0.15f;

                    int idx = startSample + i;
                    int val = samples[idx] + (short)(sample * 12000);
                    samples[idx] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
                }
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateProjectileSound()
        {
            float duration = 0.12f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = 1f - progress;
                envelope *= MathF.Min(1f, progress * 20f);

                float freq = Lerp(800f, 200f, progress);
                float phase = (t * freq) % 1f;
                float saw = phase * 2f - 1f;
                float sample = saw * envelope * 0.4f;

                samples[i] = (short)(sample * 12000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateHealSound()
        {
            float duration = 0.25f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = MathF.Sin(progress * MathF.PI);

                float freq = Lerp(400f, 600f, progress);
                float sample = MathF.Sin(2f * MathF.PI * freq * t) * envelope * 0.4f;
                sample += MathF.Sin(2f * MathF.PI * freq * 3f * t) * envelope * 0.1f;

                samples[i] = (short)(sample * 12000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateCritSound()
        {
            float duration = 0.15f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(42);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = 1f - progress;
                envelope *= envelope;

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone = MathF.Sin(2f * MathF.PI * 800f * t);
                float baseSample = (noise * 0.5f + tone * 0.3f) * envelope;

                float highTone = MathF.Sin(2f * MathF.PI * 1200f * t);
                float crit = highTone * envelope * 0.4f;

                float sample = (baseSample + crit) * 0.6f;
                samples[i] = (short)(sample * 16000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateAchievementSound()
        {
            float noteDuration = 0.07f;
            float totalDuration = noteDuration * 5.5f;
            var samples = new short[(int)(SAMPLE_RATE * totalDuration)];
            float[] freqs = { 392f, 494f, 587f, 784f };

            for (int n = 0; n < freqs.Length; n++)
            {
                int startSample = (int)(n * noteDuration * 1.1f * SAMPLE_RATE);
                int noteLength = (int)(noteDuration * 2f * SAMPLE_RATE);

                for (int i = 0; i < noteLength && startSample + i < samples.Length; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    float progress = (float)i / noteLength;
                    float envelope = (1f - progress) * MathF.Min(1f, progress * 15f);

                    float sample = MathF.Sin(2f * MathF.PI * freqs[n] * t) * envelope * 0.4f;
                    sample += MathF.Sin(2f * MathF.PI * freqs[n] * 2f * t) * envelope * 0.12f;

                    int idx = startSample + i;
                    int val = samples[idx] + (short)(sample * 13000);
                    samples[idx] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
                }
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateBoxShakeSound()
        {
            float duration = 0.8f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(77);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float rattleFreq = 20f;
                float rattle = (MathF.Sin(2f * MathF.PI * rattleFreq * t) > 0.3f) ? 1f : 0.2f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float envelope = 0.3f + t / duration * 0.5f;
                float sample = noise * rattle * envelope * 0.4f;
                samples[i] = (short)(sample * 10000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateBoxOpenSound()
        {
            float duration = 0.25f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(55);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = (1f - progress) * MathF.Min(1f, progress * 30f);

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float sweep = MathF.Sin(2f * MathF.PI * Lerp(200f, 1200f, progress) * t);
                float sample = (noise * 0.5f + sweep * 0.5f) * envelope * 0.6f;

                samples[i] = (short)(sample * 14000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateItemRevealSound()
        {
            float duration = 0.12f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = MathF.Sin(progress * MathF.PI);

                float sample = MathF.Sin(2f * MathF.PI * 1047f * t) * envelope * 0.35f;
                sample += MathF.Sin(2f * MathF.PI * 1568f * t) * envelope * 0.2f;

                samples[i] = (short)(sample * 12000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateHeartbeatSound()
        {
            float duration = 0.6f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];

            float[] beats = { 0f, 0.18f };
            foreach (float beatStart in beats)
            {
                float beatDuration = 0.1f;
                int startSample = (int)(beatStart * SAMPLE_RATE);
                int beatLength = (int)(beatDuration * SAMPLE_RATE);

                for (int i = 0; i < beatLength && startSample + i < samples.Length; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    float progress = (float)i / beatLength;
                    float envelope = (1f - progress) * (1f - progress);

                    float sample = MathF.Sin(2f * MathF.PI * 50f * t) * envelope * 0.8f;

                    int idx = startSample + i;
                    int val = samples[idx] + (short)(sample * 16000);
                    samples[idx] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
                }
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateEpicDropSound()
        {
            float duration = 0.5f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(123);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;

                if (t < 0.15f)
                {
                    float thumpProgress = t / 0.15f;
                    float thumpEnvelope = (1f - thumpProgress) * (1f - thumpProgress);
                    float thump = MathF.Sin(2f * MathF.PI * 60f * t) * thumpEnvelope * 0.8f;
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * thumpEnvelope * 0.2f;
                    sample += thump + noise;
                }

                if (t >= 0.1f)
                {
                    float shimmerT = (t - 0.1f) / 0.4f;
                    float shimmerEnvelope = MathF.Sin(shimmerT * MathF.PI);
                    float freq = Lerp(400f, 1200f, shimmerT);
                    float shimmer = MathF.Sin(2f * MathF.PI * freq * t) * shimmerEnvelope * 0.4f;
                    shimmer += MathF.Sin(2f * MathF.PI * freq * 2.5f * t) * shimmerEnvelope * 0.15f;
                    sample += shimmer;
                }

                samples[i] = (short)(sample * 14000);
            }

            return CreateWavStream(samples);
        }

        // ── Helpers ──

        private static AudioStreamWav CreateWavStream(short[] samples)
        {
            var wav = new AudioStreamWav();
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
            wav.MixRate = SAMPLE_RATE;
            wav.Stereo = false;

            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            wav.Data = bytes;

            return wav;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public override void _ExitTree()
        {
            GameEvents.OnDamageDealt -= OnDamageDealt;
            GameEvents.OnEnemyKilled -= OnEnemyKilledSfx;
            GameEvents.OnEnemyLeaked -= OnEnemyLeakedSfx;
            GameEvents.OnVineNodePlaced -= OnVineNodePlacedSfx;
            GameEvents.OnVineNodeSold -= OnVineNodeSoldSfx;
            GameEvents.OnVineNodeDestroyed -= OnVineNodeDestroyedSfx;
            GameEvents.OnTowerPlaced -= OnTowerPlacedSfx;
            GameEvents.OnTowerSold -= OnTowerSoldSfx;
            GameEvents.OnSignalFired -= OnSignalFiredSfx;
            GameEvents.OnSignalReceived -= OnSignalReceivedSfx;
            GameEvents.OnGateStateChanged -= OnGateStateChangedSfx;
            GameEvents.OnSwitchToggled -= OnSwitchToggledSfx;
            GameEvents.OnWaveStarted -= OnWaveStartedSfx;
            GameEvents.OnWaveCompleted -= OnWaveCompletedSfx;
            // S1: OnFloorCompleted removed
            GameEvents.OnAllWavesCleared -= OnAllWavesClearedSfx;
            GameEvents.OnBossSpawned -= OnBossSpawnedSfx;
            GameEvents.OnSurgeStarted -= OnSurgeStartedSfx;
            GameEvents.OnSurgeEnded -= OnSurgeEndedSfx;
            GameEvents.OnPlayerHPChanged -= OnPlayerHPChangedSfx;
            GameEvents.OnPlayerDied -= OnPlayerDiedSfx;
            GameEvents.OnHarvesterDamaged -= OnHarvesterDamagedSfx;
            GameEvents.OnDomeCollapsed -= OnDomeCollapsedSfx;
            GameEvents.OnMiningModeChanged -= OnMiningModeChangedSfx;
            GameEvents.OnResourcesDropped -= OnResourcesDroppedSfx;
            GameEvents.OnResourcesCollected -= OnResourcesCollectedSfx;
            GameEvents.OnBuffApplied -= OnBuffAppliedSfx;
            GameEvents.OnDebuffApplied -= OnDebuffAppliedSfx;
            GameEvents.OnPhaseChanged -= OnPhaseChangedSfx;
            GameEvents.OnCommentary -= OnCommentarySfx;
            GameEvents.OnAbilityCooldownChanged -= OnAbilityCooldownChangedSfx;
            ServiceLocator.Unregister<AudioManager>();
        }
    }
}
