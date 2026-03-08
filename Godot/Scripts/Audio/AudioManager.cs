using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Central audio manager: SFX pool, music, and voice playback.
    /// Generates procedural sound effects from raw PCM data when no audio files exist.
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

        public override void _Ready()
        {
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

            // Subscribe to combat events for auto-SFX
            GameEvents.OnDamageDealt += OnDamageDealt;
            GameEvents.OnEnemyKilled += OnEnemyKilledSfx;
            GameEvents.OnPlayerLevelUp += OnPlayerLevelUpSfx;
            GameEvents.OnCommentaryTriggered += OnCommentaryTriggered;

            GD.Print("[AudioManager] Ready — procedural SFX enabled");
        }

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

        private void OnEnemyKilledSfx(Node _) => PlaySFXByName("enemy_death");
        private void OnPlayerLevelUpSfx(int _) => PlaySFXByName("level_up");

        private void OnDamageDealt(DamageInfo damage)
        {
            if (damage.IsCritical)
                PlaySFXByName("crit_hit");
            else
                PlaySFXByName("hit");
        }

        private void OnCommentaryTriggered(CommentaryEntry entry)
        {
            if (entry?.VoiceClip != null)
                PlayVoice(entry.VoiceClip);
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
                "level_up" => GenerateLevelUpSound(),
                "projectile" => GenerateProjectileSound(),
                "heal" => GenerateHealSound(),
                "achievement" => GenerateAchievementSound(),
                "box_shake" => GenerateBoxShakeSound(),
                "box_open" => GenerateBoxOpenSound(),
                "item_reveal" => GenerateItemRevealSound(),
                "heartbeat" => GenerateHeartbeatSound(),
                "epic_drop" => GenerateEpicDropSound(),
                "celebration_junk" => GenerateJunkCelebrationSound(),
                "celebration_legendary" => GenerateLegendaryCelebrationSound(),
                "celebration_absurd" => GenerateAbsurdCelebrationSound(),
                "rifle" => GenerateRifleSound(),
                "shotgun_blast" => GenerateShotgunBlastSound(),
                "launcher_fire" => GenerateLauncherFireSound(),
                "reload" => GenerateReloadSound(),
                _ => null
            };
        }

        private static AudioStreamWav GenerateHitSound()
        {
            // Short noise burst — metallic clang feel
            float duration = 0.08f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(42);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = 1f - (t / duration); // linear decay
                envelope *= envelope; // quadratic decay

                // Band-limited noise + sine for metallic tone
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone = MathF.Sin(2f * MathF.PI * 800f * t);
                float sample = (noise * 0.6f + tone * 0.4f) * envelope * 0.7f;

                samples[i] = (short)(sample * 16000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateSwingSound()
        {
            // Whoosh: sine sweep 400Hz → 100Hz
            float duration = 0.15f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = MathF.Sin(progress * MathF.PI); // bell curve

                float freq = Lerp(400f, 100f, progress);
                float sample = MathF.Sin(2f * MathF.PI * freq * t) * envelope * 0.5f;

                // Add some noise for airiness
                var rng = new Random(i);
                sample += (float)(rng.NextDouble() * 2.0 - 1.0) * envelope * 0.15f;

                samples[i] = (short)(sample * 14000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateDeathSound()
        {
            // Descending tone 300Hz → 50Hz with noise
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
            // Ascending chime: two quick sine tones
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
            // Ascending arpeggio: C-E-G-C (262-330-392-523Hz)
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
                    envelope *= MathF.Min(1f, progress * 10f); // attack

                    float sample = MathF.Sin(2f * MathF.PI * freqs[n] * t) * envelope * 0.45f;
                    // Add harmonic
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
            // Zap: sawtooth sweep 800Hz → 200Hz
            float duration = 0.12f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = 1f - progress;
                envelope *= MathF.Min(1f, progress * 20f); // quick attack

                float freq = Lerp(800f, 200f, progress);
                // Sawtooth via modulo
                float phase = (t * freq) % 1f;
                float saw = phase * 2f - 1f;
                float sample = saw * envelope * 0.4f;

                samples[i] = (short)(sample * 12000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateHealSound()
        {
            // Gentle rising shimmer: sine 400 → 600Hz with volume swell
            float duration = 0.25f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                // Bell-shaped envelope
                float envelope = MathF.Sin(progress * MathF.PI);

                float freq = Lerp(400f, 600f, progress);
                float sample = MathF.Sin(2f * MathF.PI * freq * t) * envelope * 0.4f;
                // Add shimmer harmonic
                sample += MathF.Sin(2f * MathF.PI * freq * 3f * t) * envelope * 0.1f;

                samples[i] = (short)(sample * 12000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateCritSound()
        {
            // Hit sound + higher pitch overlay + longer tail
            float duration = 0.15f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(42);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = 1f - progress;
                envelope *= envelope;

                // Base hit
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone = MathF.Sin(2f * MathF.PI * 800f * t);
                float baseSample = (noise * 0.5f + tone * 0.3f) * envelope;

                // Higher pitch overlay for "crit" feel
                float highTone = MathF.Sin(2f * MathF.PI * 1200f * t);
                float crit = highTone * envelope * 0.4f;

                float sample = (baseSample + crit) * 0.6f;
                samples[i] = (short)(sample * 16000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateAchievementSound()
        {
            // Rising arpeggio fanfare: G-B-D-G (392-494-587-784Hz)
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
            // Rattling: short bursts of filtered noise
            float duration = 0.8f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(77);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float rattleFreq = 20f;
                float rattle = (MathF.Sin(2f * MathF.PI * rattleFreq * t) > 0.3f) ? 1f : 0.2f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float envelope = 0.3f + t / duration * 0.5f; // Grows louder
                float sample = noise * rattle * envelope * 0.4f;
                samples[i] = (short)(sample * 10000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateBoxOpenSound()
        {
            // Burst: noise + ascending sweep
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
            // Sparkle chime: high sine with quick decay
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
            // Deep double-thump heartbeat
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
            // Deep impact thump + ascending shimmer sweep
            float duration = 0.5f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(123);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;

                // Deep impact thump (60Hz, 0-0.15s, quadratic decay + noise)
                if (t < 0.15f)
                {
                    float thumpProgress = t / 0.15f;
                    float thumpEnvelope = (1f - thumpProgress) * (1f - thumpProgress);
                    float thump = MathF.Sin(2f * MathF.PI * 60f * t) * thumpEnvelope * 0.8f;
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * thumpEnvelope * 0.2f;
                    sample += thump + noise;
                }

                // Ascending shimmer sweep (400→1200Hz, 0.1-0.5s, bell envelope + harmonic)
                if (t >= 0.1f)
                {
                    float shimmerT = (t - 0.1f) / 0.4f;
                    float shimmerEnvelope = MathF.Sin(shimmerT * MathF.PI);
                    float freq = Lerp(400f, 1200f, shimmerT);
                    float shimmer = MathF.Sin(2f * MathF.PI * freq * t) * shimmerEnvelope * 0.4f;
                    // Add harmonic for shimmer quality
                    shimmer += MathF.Sin(2f * MathF.PI * freq * 2.5f * t) * shimmerEnvelope * 0.15f;
                    sample += shimmer;
                }

                samples[i] = (short)(sample * 14000);
            }

            return CreateWavStream(samples);
        }

        // ── Celebration Sounds ──
        // Art plug-in: Replace these procedural sounds with real WAV/OGG files
        // placed at res://Audio/SFX/{name}.wav — AudioManager auto-loads them first.

        private static AudioStreamWav GenerateJunkCelebrationSound()
        {
            // Sad trombone: descending Bb-A-Ab-G (466-440-415-392Hz) with wobble
            // The comedy "wah wah wah wahhh" — Balatro failure energy
            float noteDuration = 0.25f;
            float totalDuration = noteDuration * 4.5f;
            var samples = new short[(int)(SAMPLE_RATE * totalDuration)];
            float[] freqs = { 466f, 440f, 415f, 392f };
            float[] noteLengths = { 0.2f, 0.2f, 0.2f, 0.5f }; // Last note lingers

            for (int n = 0; n < freqs.Length; n++)
            {
                int startSample = (int)(n * noteDuration * SAMPLE_RATE);
                int noteLen = (int)(noteLengths[n] * SAMPLE_RATE);

                for (int i = 0; i < noteLen && startSample + i < samples.Length; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    float progress = (float)i / noteLen;
                    float envelope = (1f - progress) * MathF.Min(1f, progress * 20f);

                    // Trombone-like: fundamental + sub-octave + slight vibrato
                    float vibrato = MathF.Sin(2f * MathF.PI * 5f * t) * 4f; // 5Hz wobble
                    float freq = freqs[n] + vibrato;
                    float sample = MathF.Sin(2f * MathF.PI * freq * t) * 0.5f;
                    sample += MathF.Sin(2f * MathF.PI * freq * 0.5f * t) * 0.2f; // Sub-octave warmth
                    sample += MathF.Sin(2f * MathF.PI * freq * 3f * t) * 0.08f; // Brass overtone
                    sample *= envelope;

                    // Last note gets extra slow decay for maximum sadness
                    if (n == 3)
                        sample *= 0.7f;

                    int idx = startSample + i;
                    int val = samples[idx] + (short)(sample * 12000);
                    samples[idx] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
                }
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateLegendaryCelebrationSound()
        {
            // Massive fanfare: Low impact thump → rising sweep → triumphant chord → shimmer tail
            // POE2 exalt-drop energy — you KNOW something incredible happened
            float totalDuration = 1.2f;
            var samples = new short[(int)(SAMPLE_RATE * totalDuration)];
            var rng = new Random(999);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;

                // Phase 1: Deep impact (0-0.15s) — subwoofer thump
                if (t < 0.15f)
                {
                    float p = t / 0.15f;
                    float env = (1f - p) * (1f - p) * (1f - p); // Cubic decay
                    sample += MathF.Sin(2f * MathF.PI * 45f * t) * env * 0.9f;
                    sample += (float)(rng.NextDouble() * 2 - 1) * env * 0.3f;
                }

                // Phase 2: Rising sweep (0.1-0.5s) — ascending anticipation
                if (t >= 0.1f && t < 0.5f)
                {
                    float p = (t - 0.1f) / 0.4f;
                    float env = MathF.Sin(p * MathF.PI) * 0.8f;
                    float freq = Lerp(200f, 1800f, p * p); // Accelerating sweep
                    sample += MathF.Sin(2f * MathF.PI * freq * t) * env * 0.4f;
                    sample += MathF.Sin(2f * MathF.PI * freq * 1.5f * t) * env * 0.15f;
                }

                // Phase 3: Triumphant chord (0.4-1.0s) — C major power chord
                if (t >= 0.4f && t < 1.0f)
                {
                    float p = (t - 0.4f) / 0.6f;
                    float env = (1f - p) * MathF.Min(1f, (t - 0.4f) * 12f);
                    // C-E-G-C power chord (523-659-784-1047Hz)
                    sample += MathF.Sin(2f * MathF.PI * 523f * t) * env * 0.3f;
                    sample += MathF.Sin(2f * MathF.PI * 659f * t) * env * 0.25f;
                    sample += MathF.Sin(2f * MathF.PI * 784f * t) * env * 0.2f;
                    sample += MathF.Sin(2f * MathF.PI * 1047f * t) * env * 0.15f;
                }

                // Phase 4: Shimmer tail (0.8-1.2s) — high sparkle decay
                if (t >= 0.8f)
                {
                    float p = (t - 0.8f) / 0.4f;
                    float env = (1f - p) * (1f - p);
                    sample += MathF.Sin(2f * MathF.PI * 2093f * t) * env * 0.12f; // High C
                    sample += MathF.Sin(2f * MathF.PI * 2637f * t) * env * 0.08f; // High E
                    sample += MathF.Sin(2f * MathF.PI * 3136f * t) * env * 0.05f; // High G
                }

                samples[i] = (short)(sample * 14000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateAbsurdCelebrationSound()
        {
            // Unhinged chaos: Distorted fanfare + glitch artifacts + rising chaos
            // The audio equivalent of the screen going nuts
            float totalDuration = 1.8f;
            var samples = new short[(int)(SAMPLE_RATE * totalDuration)];
            var rng = new Random(666);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;

                // Phase 1: Glitchy impact (0-0.2s) — corrupted data burst
                if (t < 0.2f)
                {
                    float p = t / 0.2f;
                    float env = (1f - p) * (1f - p);
                    // Bitcrushed noise — quantize for that digital artifact feel
                    float noise = (float)(rng.NextDouble() * 2 - 1);
                    noise = MathF.Round(noise * 4f) / 4f; // 4-bit quantize
                    sample += noise * env * 0.7f;
                    sample += MathF.Sin(2f * MathF.PI * 35f * t) * env * 0.8f; // Sub bass
                }

                // Phase 2: Chaotic ascending (0.15-0.7s) — multiple sweeps at once
                if (t >= 0.15f && t < 0.7f)
                {
                    float p = (t - 0.15f) / 0.55f;
                    float env = MathF.Sin(p * MathF.PI);

                    // Three sweeps at different rates — creates chaos
                    float f1 = Lerp(150f, 2400f, p);
                    float f2 = Lerp(300f, 1800f, p * p);
                    float f3 = Lerp(80f, 3200f, MathF.Sqrt(p));
                    sample += MathF.Sin(2f * MathF.PI * f1 * t) * env * 0.25f;
                    sample += MathF.Sin(2f * MathF.PI * f2 * t) * env * 0.2f;
                    sample += MathF.Sin(2f * MathF.PI * f3 * t) * env * 0.15f;

                    // Random glitch pops
                    if (rng.NextDouble() < 0.03)
                        sample += (float)(rng.NextDouble() * 2 - 1) * env * 0.5f;
                }

                // Phase 3: Triumphant chord + overtones (0.5-1.4s) — even BIGGER than legendary
                if (t >= 0.5f && t < 1.4f)
                {
                    float p = (t - 0.5f) / 0.9f;
                    float env = (1f - p) * MathF.Min(1f, (t - 0.5f) * 10f);

                    // Full orchestral chord — more notes, more power
                    sample += MathF.Sin(2f * MathF.PI * 262f * t) * env * 0.2f;  // C4
                    sample += MathF.Sin(2f * MathF.PI * 523f * t) * env * 0.3f;  // C5
                    sample += MathF.Sin(2f * MathF.PI * 659f * t) * env * 0.25f; // E5
                    sample += MathF.Sin(2f * MathF.PI * 784f * t) * env * 0.2f;  // G5
                    sample += MathF.Sin(2f * MathF.PI * 1047f * t) * env * 0.2f; // C6
                    sample += MathF.Sin(2f * MathF.PI * 1319f * t) * env * 0.12f;// E6
                    sample += MathF.Sin(2f * MathF.PI * 1568f * t) * env * 0.08f;// G6

                    // Distortion on the chord for "overwhelming" feel
                    sample = MathF.Tanh(sample * 1.5f);
                }

                // Phase 4: Extended shimmer + glitch tail (1.2-1.8s)
                if (t >= 1.2f)
                {
                    float p = (t - 1.2f) / 0.6f;
                    float env = (1f - p) * (1f - p);

                    // Sparkle harmonics
                    sample += MathF.Sin(2f * MathF.PI * 2093f * t) * env * 0.1f;
                    sample += MathF.Sin(2f * MathF.PI * 3136f * t) * env * 0.06f;
                    sample += MathF.Sin(2f * MathF.PI * 4186f * t) * env * 0.03f;

                    // Random digital artifacts in the tail
                    if (rng.NextDouble() < 0.05)
                        sample += (float)(rng.NextDouble() * 2 - 1) * env * 0.3f;
                }

                samples[i] = (short)(Math.Clamp(sample, -1f, 1f) * 14000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateRifleSound()
        {
            // Heavy thud + low bass: deep gunshot feel
            float duration = 0.25f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(77);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = MathF.Pow(1f - progress, 3f); // steep decay

                // Low frequency thump
                float bass = MathF.Sin(2f * MathF.PI * 60f * t) * 0.5f;
                // Mid crack
                float crack = MathF.Sin(2f * MathF.PI * 200f * t) * MathF.Pow(1f - progress, 6f) * 0.4f;
                // Noise burst for attack transient
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * MathF.Pow(1f - progress, 8f) * 0.6f;

                float sample = (bass + crack + noise) * envelope;
                samples[i] = (short)(sample * 18000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateShotgunBlastSound()
        {
            // Wide noise burst with bass punch — boom
            float duration = 0.3f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(55);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;
                float envelope = MathF.Pow(1f - progress, 2.5f);

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float bass = MathF.Sin(2f * MathF.PI * 45f * t) * 0.6f;
                float mid = MathF.Sin(2f * MathF.PI * 150f * t) * MathF.Pow(1f - progress, 5f) * 0.3f;

                float sample = (noise * 0.5f + bass + mid) * envelope;
                samples[i] = (short)(sample * 20000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateLauncherFireSound()
        {
            // Thump + whoosh: low launch sound
            float duration = 0.35f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(33);

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = t / duration;

                // Initial thump
                float thump = MathF.Sin(2f * MathF.PI * 40f * t) * MathF.Pow(1f - progress, 4f) * 0.7f;
                // Whoosh sweep 300 → 80 Hz
                float freq = Lerp(300f, 80f, progress);
                float whoosh = MathF.Sin(2f * MathF.PI * freq * t) * MathF.Sin(progress * MathF.PI) * 0.3f;
                // Noise
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * MathF.Pow(1f - progress, 3f) * 0.2f;

                float sample = thump + whoosh + noise;
                samples[i] = (short)(sample * 16000);
            }

            return CreateWavStream(samples);
        }

        private static AudioStreamWav GenerateReloadSound()
        {
            // Click-clack: two short metallic clicks
            float duration = 0.3f;
            var samples = new short[(int)(SAMPLE_RATE * duration)];
            var rng = new Random(22);

            // First click at 0.05s
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;

                // Click 1: 0.0-0.04s
                if (t < 0.04f)
                {
                    float env = 1f - (t / 0.04f);
                    env *= env;
                    float tone = MathF.Sin(2f * MathF.PI * 1200f * t);
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    sample += (tone * 0.5f + noise * 0.5f) * env * 0.6f;
                }

                // Click 2: 0.15-0.19s
                if (t >= 0.15f && t < 0.19f)
                {
                    float lt = t - 0.15f;
                    float env = 1f - (lt / 0.04f);
                    env *= env;
                    float tone = MathF.Sin(2f * MathF.PI * 900f * lt);
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    sample += (tone * 0.5f + noise * 0.5f) * env * 0.6f;
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

            // Convert short[] to byte[]
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
            GameEvents.OnPlayerLevelUp -= OnPlayerLevelUpSfx;
            GameEvents.OnCommentaryTriggered -= OnCommentaryTriggered;
            ServiceLocator.Unregister<AudioManager>();
        }
    }
}
