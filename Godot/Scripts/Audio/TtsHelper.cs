using Godot;
using System.Linq;

namespace JunkbotArena
{
    /// <summary>
    /// Static utility wrapping Godot's DisplayServer TTS for AXIS announcer voice.
    /// Lazy-initializes with first available English voice.
    /// </summary>
    public static class TtsHelper
    {
        private static bool _initialized;
        private static int _voiceIdx = -1;

        /// <summary>
        /// Toggle TTS on/off from settings.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        // Announcer cadence: low pitch, slightly fast
        private const float Pitch = 0.8f;
        private const float Rate = 1.3f;
        private const int Volume = 70;

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            var voices = DisplayServer.TtsGetVoices();
            if (voices == null || voices.Count == 0)
            {
                GD.Print("[TtsHelper] No TTS voices available");
                return;
            }

            // Prefer an English voice
            for (int i = 0; i < voices.Count; i++)
            {
                var lang = voices[i]["language"].AsString();
                if (lang.StartsWith("en"))
                {
                    _voiceIdx = i;
                    GD.Print($"[TtsHelper] Using voice: {voices[i]["name"]} ({lang})");
                    return;
                }
            }

            // Fallback to first available voice
            _voiceIdx = 0;
            GD.Print($"[TtsHelper] No English voice found, using: {voices[0]["name"]}");
        }

        /// <summary>
        /// Speak text via TTS. Strips speaker prefixes like "AXIS: ".
        /// Stops any current speech first.
        /// </summary>
        public static void Speak(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text)) return;

            EnsureInitialized();
            if (_voiceIdx < 0) return;

            // Strip speaker prefix
            int colonIdx = text.IndexOf(": ");
            if (colonIdx >= 0 && colonIdx < 10)
                text = text[(colonIdx + 2)..];

            var voices = DisplayServer.TtsGetVoices();
            if (voices == null || _voiceIdx >= voices.Count) return;

            var voiceId = voices[_voiceIdx]["id"].AsString();

            DisplayServer.TtsStop();
            DisplayServer.TtsSpeak(text, voiceId, Volume, Pitch, Rate);
        }

        /// <summary>
        /// Stop any current TTS speech.
        /// </summary>
        public static void Stop()
        {
            DisplayServer.TtsStop();
        }
    }
}
