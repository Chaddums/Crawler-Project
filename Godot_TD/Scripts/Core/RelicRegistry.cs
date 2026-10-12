using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The relics (Data/relics.json). Each changes how something fights, with a trade-off, and
    /// takes the one slot of its kind: <c>bit</c> (BIT's weapon), <c>towers</c> or <c>spire</c>.
    /// Until 2026-10 there were nine hardcoded stat relics written for the old signal-chain game;
    /// only two did what they said, three only applied their downside.
    /// </summary>
    public static class RelicRegistry
    {
        public struct Relic
        {
            public string Id;
            public string Name;
            public string Desc;
            public string Icon;
            public string Rarity;
            public Color Tint;
            public string Tradeoff;
            /// <summary>bit, towers or spire: the slot it takes.</summary>
            public string Kind;
            public Dictionary<string, float> Tuning;

            public float T(string key, float fallback = 0f) => Tuning != null && Tuning.TryGetValue(key, out var v) ? v : fallback;
        }

        public static readonly string[] Kinds = { "bit", "towers", "spire" };

        public static string KindName(string kind) => kind switch { "bit" => "BIT's weapon", "towers" => "Towers", "spire" => "The Spire", _ => kind };
        /// <summary>"BIT's weapon slot", "tower slot", "Spire slot".</summary>
        public static string SlotName(string kind) => kind switch { "bit" => "BIT's weapon slot", "towers" => "tower slot", "spire" => "Spire slot", _ => kind + " slot" };
        /// <summary>The slot as a sentence takes it: "BIT's weapon slot", "the tower slot", "the Spire slot".</summary>
        public static string TheSlot(string kind) => kind == "bit" ? SlotName(kind) : "the " + SlotName(kind);

        /// <summary>The first sentence of a relic's description (the crate reveal's line).</summary>
        public static string Short(Relic r)
        {
            int i = r.Desc.IndexOf(". ", System.StringComparison.Ordinal);
            return i > 0 ? r.Desc[..(i + 1)] : r.Desc;
        }

        private class FileRelic
        {
            public string Id { get; set; } = "";
            public string Kind { get; set; } = "bit";
            public string Name { get; set; } = "";
            public string Rarity { get; set; } = "rare";
            public string Icon { get; set; } = "auto_awesome";
            public float[] Color { get; set; } = { 1f, 1f, 1f };
            public string Desc { get; set; } = "";
            public string Tradeoff { get; set; }
            public Dictionary<string, float> Tuning { get; set; } = new();
        }

        private class FileRoot { public List<FileRelic> Relics { get; set; } = new(); }

        private static Relic[] _all;

        public static Relic[] All => _all ??= Load();

        public static Relic? Get(string id)
        {
            foreach (var r in All) if (r.Id == id) return r;
            return null;
        }

        public static IEnumerable<Relic> OfKind(string kind) => All.Where(r => r.Kind == kind);

        public static void Reload() => _all = null;

        private static Relic[] Load()
        {
            const string path = "res://Data/relics.json";
            if (!FileAccess.FileExists(path)) { GD.PushError($"[Relics] {path} is missing"); return System.Array.Empty<Relic>(); }
            try
            {
                var root = JsonSerializer.Deserialize<FileRoot>(FileAccess.GetFileAsString(path), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
                return (root?.Relics ?? new()).Select(f => new Relic
                {
                    Id = f.Id, Kind = f.Kind, Name = f.Name, Rarity = f.Rarity, Icon = f.Icon, Desc = f.Desc, Tradeoff = f.Tradeoff,
                    Tint = new Color(f.Color.Length > 0 ? f.Color[0] : 1f, f.Color.Length > 1 ? f.Color[1] : 1f, f.Color.Length > 2 ? f.Color[2] : 1f),
                    Tuning = f.Tuning ?? new(),
                }).ToArray();
            }
            catch (JsonException e)
            {
                GD.PushError($"[Relics] {path}: {e.Message}");
                return System.Array.Empty<Relic>();
            }
        }
    }
}
