using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Central audio manager: SFX pool, music, and voice playback.
    /// Registers itself via ServiceLocator for global access.
    /// </summary>
    public partial class AudioManager : Node
    {
        private const int SFX_POOL_SIZE = 8;
        private AudioStreamPlayer[] _sfxPlayers;
        private int _sfxIndex;
        private AudioStreamPlayer _musicPlayer;
        private AudioStreamPlayer _voicePlayer;

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

            GD.Print("[AudioManager] Ready");
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
        /// Play a named SFX. When actual audio files are added, load them here.
        /// For now, logs the event for future wiring.
        /// </summary>
        public void PlaySFXByName(string sfxName)
        {
            // Placeholder — wire to actual AudioStream resources when assets are added
            // e.g.: var stream = GD.Load<AudioStream>($"res://Audio/SFX/{sfxName}.wav");
            // PlaySFX(stream);
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

        public override void _ExitTree()
        {
            GameEvents.OnDamageDealt -= OnDamageDealt;
            GameEvents.OnCommentaryTriggered -= OnCommentaryTriggered;
            ServiceLocator.Unregister<AudioManager>();
        }
    }
}
