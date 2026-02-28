using Godot;
using System;
using System.Collections.Generic;

namespace DungeonCrawlerCarl
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
            GameEvents.OnEnemyKilled += _ => PlaySFXByName("enemy_death");
            GameEvents.OnPlayerLevelUp += _ => PlaySFXByName("level_up");
            GameEvents.OnCommentaryTriggered += OnCommentaryTriggered;

            GD.Print("[AudioManager] Ready — procedural SFX enabled");
        }

        /// <summary>
        /// Play an AudioStream as a one-shot SFX.
        /// </summary>
        public void PlaySFX(AudioStream stream, float volumeDb = 0f)
        {
            if (stream == null) return;

            var player = _sfxPlayers[_sfxIndex];
            player.Stream = stream;
            player.VolumeDb = volumeDb;
            player.Play();

            _sfxIndex = (_sfxIndex + 1) % SFX_POOL_SIZE;
        }

        /// <summary>
        /// Play a named SFX using procedural generation when no audio files exist.
        /// </summary>
        public void PlaySFXByName(string sfxName)
        {
            if (string.IsNullOrEmpty(sfxName)) return;

            // Try loading an actual audio file first
            string path = $"res://Audio/SFX/{sfxName}.wav";
            if (ResourceLoader.Exists(path))
            {
                PlaySFX(GD.Load<AudioStream>(path));
                return;
            }

            // Generate and cache procedural sound
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
            GameEvents.OnCommentaryTriggered -= OnCommentaryTriggered;
            ServiceLocator.Unregister<AudioManager>();
        }
    }
}
