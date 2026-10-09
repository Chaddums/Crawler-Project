using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>Tower ranks from kills (Data/veterancy.json): the "natural upgrade" a tower earns by fighting.</summary>
    public static class Veterancy
    {
        private static bool _loaded;
        public static int[] Kills { get; private set; } = { 10, 30, 75, 150 };
        public static string[] Names { get; private set; } = { "Recruit", "Veteran", "Veteran II", "Veteran III", "Elite" };
        public static float DamagePerRank { get; private set; } = 0.08f;
        public static float RatePerRank { get; private set; } = 0.05f;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            const string path = "res://Data/veterancy.json";
            if (!FileAccess.FileExists(path)) return;
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var text = System.Text.RegularExpressions.Regex.Replace(f.GetAsText(), @"^\s*//.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            var v = Json.ParseString(text);
            if (v.VariantType != Variant.Type.Dictionary) return;
            var d = v.AsGodotDictionary();
            if (d.ContainsKey("kills"))
            {
                var list = new List<int>();
                foreach (var k in d["kills"].AsGodotArray()) list.Add(k.AsInt32());
                if (list.Count > 0) Kills = list.ToArray();
            }
            if (d.ContainsKey("names"))
            {
                var list = new List<string>();
                foreach (var k in d["names"].AsGodotArray()) list.Add(k.AsString());
                if (list.Count > Kills.Length) Names = list.ToArray();
            }
            if (d.ContainsKey("damage")) DamagePerRank = (float)d["damage"].AsDouble();
            if (d.ContainsKey("rate")) RatePerRank = (float)d["rate"].AsDouble();
        }

        public static int RankFor(int kills)
        {
            Load();
            int r = 0;
            while (r < Kills.Length && kills >= Kills[r]) r++;
            return r;
        }

        public static string NameOf(int rank) { Load(); return Names[Mathf.Clamp(rank, 0, Names.Length - 1)]; }

        /// <summary>Kills needed for the next rank, or -1 at the top.</summary>
        public static int NextAt(int rank) { Load(); return rank < Kills.Length ? Kills[rank] : -1; }
    }
}
