using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Loads all game text from Data/strings.json at startup.
    /// Provides dot-path lookups: StringLoader.Get("abilities.ability_strike.name")
    /// Returns "[MISSING: path]" for missing keys so problems are immediately visible.
    /// </summary>
    public static class StringLoader
    {
        private static Dictionary<string, object> _root;
        private static bool _loaded;

        private const string FILE_PATH = "res://Data/strings.json";

        /// <summary>
        /// Load strings from Data/strings.json. Call once at startup.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public static void Load()
        {
            if (_loaded) return;

            if (!FileAccess.FileExists(FILE_PATH))
            {
                GD.PushWarning("[StringLoader] strings.json not found at " + FILE_PATH +
                               ". All string lookups will return [MISSING] placeholders.");
                _root = new Dictionary<string, object>();
                _loaded = true;
                return;
            }

            using var file = FileAccess.Open(FILE_PATH, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();

            _root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (_root == null)
            {
                GD.PushError("[StringLoader] Failed to parse strings.json.");
                _root = new Dictionary<string, object>();
            }

            _loaded = true;
            GD.Print($"[StringLoader] Loaded {CountKeys(_root)} string entries.");
        }

        /// <summary>
        /// Get a single string by dot-separated path.
        /// Example: StringLoader.Get("abilities.ability_strike.name")
        /// </summary>
        public static string Get(string path)
        {
            EnsureLoaded();

            object current = _root;
            string[] parts = path.Split('.');

            for (int i = 0; i < parts.Length; i++)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(parts[i], out current))
                        return $"[MISSING: {path}]";
                }
                else
                {
                    return $"[MISSING: {path}]";
                }
            }

            return current?.ToString() ?? $"[MISSING: {path}]";
        }

        /// <summary>
        /// Get a string with template variable replacement.
        /// Example: StringLoader.Get("commentary.level_up", ("{level}", "5"))
        /// </summary>
        public static string Get(string path, params (string key, object value)[] replacements)
        {
            string template = Get(path);
            if (template.StartsWith("[MISSING:")) return template;

            foreach (var (key, value) in replacements)
            {
                template = template.Replace(key, value?.ToString() ?? string.Empty);
            }

            return template;
        }

        /// <summary>
        /// Get an array of strings by dot-separated path.
        /// </summary>
        public static string[] GetArray(string path)
        {
            EnsureLoaded();

            object current = _root;
            string[] parts = path.Split('.');

            for (int i = 0; i < parts.Length; i++)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(parts[i], out current))
                        return new[] { $"[MISSING: {path}]" };
                }
                else
                {
                    return new[] { $"[MISSING: {path}]" };
                }
            }

            if (current is List<object> list)
            {
                var result = new string[list.Count];
                for (int i = 0; i < list.Count; i++)
                    result[i] = list[i]?.ToString() ?? string.Empty;
                return result;
            }

            if (current is string s)
                return new[] { s };

            return new[] { $"[MISSING: {path}]" };
        }

        /// <summary>
        /// Get a random string from an array at the given path.
        /// </summary>
        public static string GetRandom(string path)
        {
            var arr = GetArray(path);
            return arr[GD.RandRange(0, arr.Length - 1)];
        }

        /// <summary>
        /// Get a random string from an array at the given path, with template variable replacement.
        /// </summary>
        public static string GetRandom(string path, params (string key, object value)[] replacements)
        {
            string template = GetRandom(path);
            if (template.StartsWith("[MISSING:")) return template;
            foreach (var (key, value) in replacements)
                template = template.Replace(key, value?.ToString() ?? string.Empty);
            return template;
        }

        /// <summary>
        /// Check if a path exists in the loaded strings.
        /// </summary>
        public static bool Has(string path)
        {
            EnsureLoaded();

            object current = _root;
            string[] parts = path.Split('.');

            for (int i = 0; i < parts.Length; i++)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(parts[i], out current))
                        return false;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Force reload from disk. Useful during development.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            _root = null;
            Load();
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        private static int CountKeys(Dictionary<string, object> dict)
        {
            int count = 0;
            foreach (var kvp in dict)
            {
                if (kvp.Value is Dictionary<string, object> nested)
                    count += CountKeys(nested);
                else if (kvp.Value is List<object> list)
                    count += list.Count;
                else
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Minimal JSON parser (no external dependencies).
    /// Handles objects, arrays, strings, numbers, bools, null.
    /// </summary>
    internal static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }

        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            char c = json[index];
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return ParseString(json, ref index);
            if (c == 't' || c == 'f') return ParseBool(json, ref index);
            if (c == 'n') return ParseNull(json, ref index);
            return ParseNumber(json, ref index);
        }

        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var dict = new Dictionary<string, object>();
            index++; // skip '{'
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != '}')
            {
                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ':') index++;
                SkipWhitespace(json, ref index);
                dict[key] = ParseValue(json, ref index);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
                SkipWhitespace(json, ref index);
            }

            if (index < json.Length) index++; // skip '}'
            return dict;
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            var list = new List<object>();
            index++; // skip '['
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != ']')
            {
                list.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
                SkipWhitespace(json, ref index);
            }

            if (index < json.Length) index++; // skip ']'
            return list;
        }

        private static string ParseString(string json, ref int index)
        {
            index++; // skip opening '"'
            var sb = new System.Text.StringBuilder();

            while (index < json.Length)
            {
                char c = json[index];
                if (c == '\\')
                {
                    index++;
                    if (index >= json.Length) break;
                    char escaped = json[index];
                    switch (escaped)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 < json.Length)
                            {
                                string hex = json.Substring(index + 1, 4);
                                sb.Append((char)System.Convert.ToInt32(hex, 16));
                                index += 4;
                            }
                            break;
                        default: sb.Append(escaped); break;
                    }
                }
                else if (c == '"')
                {
                    index++;
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                }
                index++;
            }

            return sb.ToString();
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            if (index < json.Length && json[index] == '-') index++;
            while (index < json.Length && char.IsDigit(json[index])) index++;
            if (index < json.Length && json[index] == '.')
            {
                index++;
                while (index < json.Length && char.IsDigit(json[index])) index++;
            }
            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-')) index++;
                while (index < json.Length && char.IsDigit(json[index])) index++;
            }

            string numStr = json.Substring(start, index - start);
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;
            return 0;
        }

        private static object ParseBool(string json, ref int index)
        {
            if (json.Substring(index, 4) == "true") { index += 4; return true; }
            if (json.Substring(index, 5) == "false") { index += 5; return false; }
            return false;
        }

        private static object ParseNull(string json, ref int index)
        {
            if (index + 4 <= json.Length && json.Substring(index, 4) == "null") index += 4;
            return null;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }
    }
}
