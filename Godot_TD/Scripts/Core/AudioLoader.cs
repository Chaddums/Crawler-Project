using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Metadata for an audio manifest entry: asset path, volume, and bus.
    /// </summary>
    public class AudioEntry
    {
        public string Path { get; set; }
        public float VolumeDb { get; set; }
        public string Bus { get; set; } = "SFX";
    }

    /// <summary>
    /// Loads audio asset paths from Data/audio.json at startup.
    /// Provides dot-path lookups: AudioLoader.Get("sfx.hit") → AudioStream
    /// Returns null when the asset file is missing (procedural fallback continues).
    /// Supports both object leaves { "path", "volume_db", "bus" } and plain string leaves.
    /// </summary>
    public static class AudioLoader
    {
        private static Dictionary<string, object> _root;
        private static bool _loaded;
        private static readonly Dictionary<string, AudioStream> _cache = new();

        private const string FILE_PATH = "res://Data/audio.json";

        /// <summary>
        /// Load audio manifest from Data/audio.json. Call once at startup.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public static void Load()
        {
            if (_loaded) return;

            if (!FileAccess.FileExists(FILE_PATH))
            {
                GD.PushWarning("[AudioLoader] audio.json not found at " + FILE_PATH +
                               ". All audio lookups will return null (fallbacks active).");
                _root = new Dictionary<string, object>();
                _loaded = true;
                return;
            }

            using var file = FileAccess.Open(FILE_PATH, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();

            _root = DeserializeJson(json);
            if (_root == null)
            {
                GD.PushError("[AudioLoader] Failed to parse audio.json.");
                _root = new Dictionary<string, object>();
            }

            _loaded = true;
            GD.Print($"[AudioLoader] Loaded {CountLeaves(_root)} audio entries.");
        }

        /// <summary>
        /// Get an AudioStream by dot-separated path.
        /// Returns null if the key doesn't exist or the asset is missing on disk.
        /// Example: AudioLoader.Get("sfx.hit")
        /// </summary>
        public static AudioStream Get(string path)
        {
            EnsureLoaded();

            if (_cache.TryGetValue(path, out var cached))
                return cached;

            var entry = GetEntry(path);
            if (entry == null) return null;

            if (!ResourceLoader.Exists(entry.Path))
                return null;

            var stream = GD.Load<AudioStream>(entry.Path);
            if (stream != null)
                _cache[path] = stream;

            return stream;
        }

        /// <summary>
        /// Get the full AudioEntry metadata for a dot-path key.
        /// Returns null if the key doesn't exist in the manifest.
        /// </summary>
        public static AudioEntry GetEntry(string path)
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

            // Object leaf: { "path": "...", "volume_db": N, "bus": "..." }
            if (current is Dictionary<string, object> obj)
            {
                var entry = new AudioEntry();

                if (obj.TryGetValue("path", out var p))
                    entry.Path = p?.ToString();
                else
                    return null; // No path means invalid entry

                if (obj.TryGetValue("volume_db", out var vol))
                {
                    if (vol is double d)
                        entry.VolumeDb = (float)d;
                }

                if (obj.TryGetValue("bus", out var bus))
                    entry.Bus = bus?.ToString() ?? "SFX";

                return entry;
            }

            // Plain string leaf: just a path with defaults
            if (current is string s && !string.IsNullOrEmpty(s))
            {
                return new AudioEntry { Path = s };
            }

            return null;
        }

        /// <summary>
        /// Check if a path exists in the loaded manifest.
        /// </summary>
        public static bool Has(string path)
        {
            return GetEntry(path) != null;
        }

        /// <summary>
        /// Check if the asset file exists on disk for a given manifest key.
        /// </summary>
        public static bool AssetExists(string path)
        {
            var entry = GetEntry(path);
            return entry != null && ResourceLoader.Exists(entry.Path);
        }

        /// <summary>
        /// Force reload from disk and clear the audio cache.
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

        private static int CountLeaves(Dictionary<string, object> dict)
        {
            int count = 0;
            foreach (var kvp in dict)
            {
                if (kvp.Value is Dictionary<string, object> nested)
                {
                    // Check if this is a leaf object (has "path" key) vs. a category
                    if (nested.ContainsKey("path"))
                        count++;
                    else
                        count += CountLeaves(nested);
                }
                else
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Parse a JSON string into a Dictionary tree using System.Text.Json.
        /// Preserves the same Dictionary&lt;string, object&gt; structure the rest of the class navigates.
        /// </summary>
        private static Dictionary<string, object> DeserializeJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return ElementToDict(doc.RootElement);
            }
            catch (JsonException ex)
            {
                GD.PushError($"[AudioLoader] JSON parse error: {ex.Message}");
                return null;
            }
        }

        private static Dictionary<string, object> ElementToDict(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;

            var dict = new Dictionary<string, object>();
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = ElementToObject(prop.Value);
            }
            return dict;
        }

        private static object ElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ElementToDict(element),
                JsonValueKind.Array => ElementToList(element),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static List<object> ElementToList(JsonElement element)
        {
            var list = new List<object>();
            foreach (var item in element.EnumerateArray())
                list.Add(ElementToObject(item));
            return list;
        }
    }
}
