using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Loads character_config.json (saved by the editor's Characters tab)
    /// and provides part overrides + fire point data at runtime.
    /// </summary>
    public static class CharacterConfigLoader
    {
        private const string CONFIG_PATH = "res://Data/character_config.json";

        private static Dictionary<string, object> _cache;
        private static bool _loaded;

        /// <summary>
        /// Force reload from disk. Called automatically on first access.
        /// </summary>
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
        /// Apply saved part position/rotation overrides to a player body node tree.
        /// </summary>
        public static void ApplyPartOverrides(Node3D body, BotFrameType frame)
        {
            Load();
            if (_cache == null) return;

            string key = frame.ToString();
            if (!_cache.TryGetValue(key, out var frameObj)) return;
            if (frameObj is not Dictionary<string, object> frameData) return;
            if (!frameData.TryGetValue("Parts", out var partsObj)) return;
            if (partsObj is not Dictionary<string, object> parts) return;

            ApplyRecursive(body, parts);
        }

        private static void ApplyRecursive(Node node, Dictionary<string, object> parts)
        {
            if (node is Node3D n3d)
            {
                string name = n3d.Name.ToString();
                if (parts.TryGetValue(name, out var partObj) && partObj is Dictionary<string, object> pd)
                {
                    if (pd.TryGetValue("PosX", out var px) && pd.TryGetValue("PosY", out var py) && pd.TryGetValue("PosZ", out var pz))
                        n3d.Position = new Vector3(Convert.ToSingle(px), Convert.ToSingle(py), Convert.ToSingle(pz));
                    if (pd.TryGetValue("RotX", out var rx) && pd.TryGetValue("RotY", out var ry) && pd.TryGetValue("RotZ", out var rz))
                        n3d.RotationDegrees = new Vector3(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz));
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    ApplyRecursive(childNode, parts);
            }
        }

        /// <summary>
        /// Get the fire point offset for a given frame.
        /// Returns (height above player origin, forward distance from player).
        /// Defaults to (0.9, 0.8) if no config exists.
        /// </summary>
        public static (float height, float forward) GetFirePoint(BotFrameType frame)
        {
            Load();
            if (_cache == null) return (0.9f, 0.8f);

            string key = frame.ToString();
            if (!_cache.TryGetValue(key, out var frameObj)) return (0.9f, 0.8f);
            if (frameObj is not Dictionary<string, object> frameData) return (0.9f, 0.8f);

            float height = 0.9f;
            float forward = 0.8f;

            if (frameData.TryGetValue("FirePointY", out var fy))
                height = Convert.ToSingle(fy);
            if (frameData.TryGetValue("FirePointForward", out var ff))
                forward = Convert.ToSingle(ff);

            return (height, forward);
        }
    }
}
