using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Runtime loader for ui_config.json — reads UI property overrides
    /// saved by the editor's UI/UX tab.
    /// Call UIConfigLoader.Get("HUD", "HealthBar", "AccentColor", defaultValue) to read.
    /// </summary>
    public static class UIConfigLoader
    {
        private const string CONFIG_PATH = "res://Data/ui_config.json";

        private static Dictionary<string, object> _cache;
        private static bool _loaded;

        public static void Reload()
        {
            _cache = null;
            _loaded = false;
            Load();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            if (!FileAccess.FileExists(CONFIG_PATH)) return;
            using var file = FileAccess.Open(CONFIG_PATH, FileAccess.ModeFlags.Read);
            if (file == null) return;
            var text = file.GetAsText();
            if (string.IsNullOrWhiteSpace(text)) return;
            _cache = MiniJson.Deserialize(text) as Dictionary<string, object>;
        }

        /// <summary>
        /// Get a string property value. Returns defaultValue if no override exists.
        /// </summary>
        public static string Get(string screen, string element, string property, string defaultValue = "")
        {
            Load();
            if (_cache == null) return defaultValue;
            if (!_cache.TryGetValue(screen, out var screenObj)) return defaultValue;
            if (screenObj is not Dictionary<string, object> screenData) return defaultValue;
            if (!screenData.TryGetValue(element, out var elObj)) return defaultValue;
            if (elObj is not Dictionary<string, object> elData) return defaultValue;
            if (!elData.TryGetValue(property, out var val)) return defaultValue;
            return val?.ToString() ?? defaultValue;
        }

        /// <summary>
        /// Get a float property value.
        /// </summary>
        public static float GetFloat(string screen, string element, string property, float defaultValue = 0f)
        {
            string val = Get(screen, element, property, null);
            if (val == null) return defaultValue;
            if (float.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f))
                return f;
            return defaultValue;
        }

        /// <summary>
        /// Get an int property value.
        /// </summary>
        public static int GetInt(string screen, string element, string property, int defaultValue = 0)
        {
            string val = Get(screen, element, property, null);
            if (val == null) return defaultValue;
            if (int.TryParse(val, out int i)) return i;
            // Try parsing as float then truncating
            if (float.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f))
                return (int)f;
            return defaultValue;
        }

        /// <summary>
        /// Get a Color property value. Format: "R,G,B" with values 0-1.
        /// </summary>
        public static Color GetColor(string screen, string element, string property, Color defaultValue)
        {
            string val = Get(screen, element, property, null);
            if (val == null) return defaultValue;

            var parts = val.Split(',');
            if (parts.Length < 3) return defaultValue;

            if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float r)
                && float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float g)
                && float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float b))
            {
                return new Color(r, g, b);
            }

            return defaultValue;
        }

        /// <summary>
        /// Get a bool property value.
        /// </summary>
        public static bool GetBool(string screen, string element, string property, bool defaultValue = false)
        {
            string val = Get(screen, element, property, null);
            if (val == null) return defaultValue;
            return val == "true" || val == "1";
        }

        /// <summary>
        /// Check if a specific property has an override in the config.
        /// </summary>
        public static bool HasOverride(string screen, string element, string property)
        {
            Load();
            if (_cache == null) return false;
            if (!_cache.TryGetValue(screen, out var screenObj)) return false;
            if (screenObj is not Dictionary<string, object> screenData) return false;
            if (!screenData.TryGetValue(element, out var elObj)) return false;
            if (elObj is not Dictionary<string, object> elData) return false;
            return elData.ContainsKey(property);
        }
    }
}
