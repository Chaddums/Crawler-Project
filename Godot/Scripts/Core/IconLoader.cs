using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Loads icon asset paths from Data/icons.json at startup.
    /// Provides dot-path lookups: IconLoader.Get("abilities.ability_strike") → Texture2D
    /// Returns a magenta checkerboard placeholder when the asset file is missing.
    /// </summary>
    public static class IconLoader
    {
        private static Dictionary<string, object> _root;
        private static bool _loaded;
        private static readonly Dictionary<string, Texture2D> _cache = new();
        private static Texture2D _placeholder;

        private const string FILE_PATH = "res://Data/icons.json";
        private const int PLACEHOLDER_SIZE = 32;

        /// <summary>
        /// Load icon manifest from Data/icons.json. Call once at startup.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public static void Load()
        {
            if (_loaded) return;

            if (!FileAccess.FileExists(FILE_PATH))
            {
                GD.PushWarning("[IconLoader] icons.json not found at " + FILE_PATH +
                               ". All icon lookups will return placeholders.");
                _root = new Dictionary<string, object>();
                _loaded = true;
                return;
            }

            using var file = FileAccess.Open(FILE_PATH, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();

            _root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (_root == null)
            {
                GD.PushError("[IconLoader] Failed to parse icons.json.");
                _root = new Dictionary<string, object>();
            }

            _loaded = true;
            GD.Print($"[IconLoader] Loaded {CountLeaves(_root)} icon entries.");
        }

        /// <summary>
        /// Get a Texture2D by dot-separated path.
        /// Returns the loaded texture if the asset exists, or a checkerboard placeholder if not.
        /// Example: IconLoader.Get("abilities.ability_strike")
        /// </summary>
        public static Texture2D Get(string path)
        {
            EnsureLoaded();

            if (_cache.TryGetValue(path, out var cached))
                return cached;

            string resPath = GetPath(path);
            if (resPath == null)
            {
                GD.PushWarning($"[IconLoader] No manifest entry for '{path}'");
                return GetPlaceholder();
            }

            if (!ResourceLoader.Exists(resPath))
            {
                GD.PushWarning($"[IconLoader] Asset missing on disk: {resPath} (key: {path})");
                return GetPlaceholder();
            }

            var texture = GD.Load<Texture2D>(resPath);
            if (texture == null)
            {
                GD.PushWarning($"[IconLoader] Failed to load texture: {resPath} (key: {path})");
                return GetPlaceholder();
            }

            _cache[path] = texture;
            return texture;
        }

        /// <summary>
        /// Get the raw res:// path for a dot-path key without loading the texture.
        /// Returns null if the key doesn't exist in the manifest.
        /// </summary>
        public static string GetPath(string path)
        {
            EnsureLoaded();

            object current = _root;
            string[] parts = path.Split('.');

            for (int i = 0; i < parts.Length; i++)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(parts[i], out current))
                        return null;
                }
                else
                {
                    return null;
                }
            }

            return current?.ToString();
        }

        /// <summary>
        /// Check if a path exists in the loaded manifest.
        /// </summary>
        public static bool Has(string path)
        {
            return GetPath(path) != null;
        }

        /// <summary>
        /// Check if the asset file exists on disk for a given manifest key.
        /// </summary>
        public static bool AssetExists(string path)
        {
            string resPath = GetPath(path);
            return resPath != null && ResourceLoader.Exists(resPath);
        }

        /// <summary>
        /// Force reload from disk and clear the texture cache.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            _root = null;
            _cache.Clear();
            Load();
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        private static Texture2D GetPlaceholder()
        {
            if (_placeholder != null) return _placeholder;

            // Generate magenta/dark checkerboard — the classic "missing texture" look
            var image = Image.CreateEmpty(PLACEHOLDER_SIZE, PLACEHOLDER_SIZE, false, Image.Format.Rgba8);
            var magenta = new Color(1f, 0f, 1f);
            var dark = new Color(0.15f, 0f, 0.15f);
            int cellSize = PLACEHOLDER_SIZE / 4;

            for (int y = 0; y < PLACEHOLDER_SIZE; y++)
            {
                for (int x = 0; x < PLACEHOLDER_SIZE; x++)
                {
                    bool checker = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                    image.SetPixel(x, y, checker ? magenta : dark);
                }
            }

            _placeholder = ImageTexture.CreateFromImage(image);
            return _placeholder;
        }

        private static int CountLeaves(Dictionary<string, object> dict)
        {
            int count = 0;
            foreach (var kvp in dict)
            {
                if (kvp.Value is Dictionary<string, object> nested)
                    count += CountLeaves(nested);
                else
                    count++;
            }
            return count;
        }
    }
}
