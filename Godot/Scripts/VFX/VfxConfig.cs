using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Loads VFX particle configuration from vfx_config.json.
    /// Provides per-effect overrides that VfxFactory can read at runtime.
    /// If no config file exists, returns null (VfxFactory uses its hardcoded defaults).
    /// </summary>
    public static class VfxConfig
    {
        private static Dictionary<string, Dictionary<string, object>> _config;
        private static bool _loaded;

        /// <summary>
        /// Get config for a named effect, or null if no override exists.
        /// </summary>
        public static Dictionary<string, object> Get(string effectName)
        {
            EnsureLoaded();
            if (_config != null && _config.TryGetValue(effectName, out var cfg))
                return cfg;
            return null;
        }

        /// <summary>
        /// Get a float value from an effect's config, with fallback.
        /// </summary>
        public static float GetFloat(string effectName, string key, float fallback)
        {
            var cfg = Get(effectName);
            if (cfg != null && cfg.TryGetValue(key, out var v))
                return Convert.ToSingle(v);
            return fallback;
        }

        /// <summary>
        /// Get an int value from an effect's config, with fallback.
        /// </summary>
        public static int GetInt(string effectName, string key, int fallback)
        {
            var cfg = Get(effectName);
            if (cfg != null && cfg.TryGetValue(key, out var v))
                return Convert.ToInt32(v);
            return fallback;
        }

        /// <summary>
        /// Get the color from an effect's config, with fallback.
        /// </summary>
        public static Color GetColor(string effectName, Color fallback)
        {
            var cfg = Get(effectName);
            if (cfg == null) return fallback;
            float r = cfg.TryGetValue("ColorR", out var vr) ? Convert.ToSingle(vr) : fallback.R;
            float g = cfg.TryGetValue("ColorG", out var vg) ? Convert.ToSingle(vg) : fallback.G;
            float b = cfg.TryGetValue("ColorB", out var vb) ? Convert.ToSingle(vb) : fallback.B;
            return new Color(r, g, b);
        }

        /// <summary>
        /// Force reload from disk (called after VfxEditor saves).
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            _config = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            const string path = "res://Data/vfx_config.json";
            if (!FileAccess.FileExists(path)) return;

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return;
            var text = file.GetAsText();
            if (string.IsNullOrWhiteSpace(text)) return;

            var parsed = MiniJson.Deserialize(text) as Dictionary<string, object>;
            if (parsed == null) return;

            _config = new();
            foreach (var kvp in parsed)
            {
                if (kvp.Value is Dictionary<string, object> entry)
                    _config[kvp.Key] = entry;
            }
        }
    }
}
