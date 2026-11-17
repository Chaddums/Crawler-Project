using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>One boss fight's boss, as Data/bosses.json describes it.</summary>
    public class SiteBossProfile
    {
        public string Id, Name, Trick, What, Answer, Arrives, AxisLine, BitLine, Model, Rig;
        public float ModelHeight = 3.2f, RigHeight = 2.4f, RigScale = 1.5f;
        public Color Color = new(0.8f, 0.35f, 1f);
        public float Hp = 5000f, Speed = 1f, WaveHp = 1f;
        public float Radius = 8f, Interval = 10f, Charge = 3f, TowerShare = 0.6f, BitShare = 0.35f;
        public float SpireHitShare = 0.05f, SpireHitInterval = 1f;
        public float ContactDamage = 40f;
        /// <summary>A barricade in its way loses this share of its full health each second it is hit.</summary>
        public float BarricadeShare = 0.5f;
        /// <summary>After a stun or freeze lets go, seconds before another takes hold.</summary>
        public float StunGuard = 6f;
        public int Reward = 400, WarnWaves = 3;

        public bool Jams => Trick == "jam";
        public bool Overloads => Trick == "overload";

        /// <summary>What it does, in a sentence, with its numbers filled in.</summary>
        public string WhatWords => Fill(What);
        public string AnswerWords => Fill(Answer);

        private string Fill(string s) => (s ?? "")
            .Replace("{cells}", (Radius / Constants.VINE_CELL_SIZE).ToString("0.#"))
            .Replace("{interval}", Interval.ToString("0.#"))
            .Replace("{charge}", Charge.ToString("0.#"))
            .Replace("{tower_pct}", Mathf.RoundToInt(TowerShare * 100f).ToString())
            .Replace("{bit_pct}", Mathf.RoundToInt(BitShare * 100f).ToString());
    }

    /// <summary>
    /// The boss fights' bosses (Data/bosses.json), by territory.json boss_id. Until 2026-11 a boss
    /// fight was the ordinary waves held to a wave: the boss_id and the site's description of a
    /// boss were on the map and nothing else knew of them.
    /// </summary>
    public static class SiteBosses
    {
        private const string DataPath = "res://Data/bosses.json";
        private static readonly Dictionary<string, SiteBossProfile> _bosses = new();
        private static bool _loaded;

        public static IEnumerable<SiteBossProfile> All { get { Load(); return _bosses.Values; } }

        public static SiteBossProfile Get(string id)
        {
            Load();
            return id != null && _bosses.TryGetValue(id, out var p) ? p : null;
        }

        /// <summary>The boss of a boss site, or null (not a boss site, or its boss isn't in the file).</summary>
        public static SiteBossProfile ForSite(TerritorySite site) => site != null && site.IsBossSite ? Get(site.BossId) : null;

        public static SiteBossProfile ForSite(string siteId) => ForSite(TerritoryManager.GetSite(siteId ?? ""));

        /// <summary>The boss of the boss fight being played, or null.</summary>
        public static SiteBossProfile Current
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && gm.IsBossRun ? ForSite(gm.BossSectionId) : null;
            }
        }

        /// <summary>Tests: read the file again.</summary>
        public static void Reload() { _loaded = false; Load(); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _bosses.Clear();
            if (!FileAccess.FileExists(DataPath)) return;
            using var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) { GD.PushWarning("[SiteBosses] bosses.json did not parse"); return; }
            var root = parsed.AsGodotDictionary();
            if (!root.ContainsKey("bosses") || root["bosses"].VariantType != Variant.Type.Array) return;
            foreach (var item in root["bosses"].AsGodotArray())
            {
                if (item.VariantType != Variant.Type.Dictionary) continue;
                var d = item.AsGodotDictionary();
                string S(string k, string v = null) => d.ContainsKey(k) ? d[k].AsString() : v;
                float F(string k, float v) => d.ContainsKey(k) ? (float)d[k].AsDouble() : v;
                var p = new SiteBossProfile();
                p.Id = S("id");
                if (string.IsNullOrEmpty(p.Id)) continue;
                p.Name = S("name", p.Id);
                p.Trick = S("trick", "");
                p.What = S("what", "");
                p.Answer = S("answer", "");
                p.Arrives = S("arrives", "");
                p.AxisLine = S("axis_line", "");
                p.BitLine = S("bit_line", "");
                p.Model = S("model");
                p.Rig = S("rig");
                p.ModelHeight = F("model_height", p.ModelHeight);
                p.RigHeight = F("rig_height", p.RigHeight);
                p.RigScale = F("rig_scale", p.RigScale);
                if (d.ContainsKey("color") && d["color"].VariantType == Variant.Type.Array && d["color"].AsGodotArray() is { Count: >= 3 } c)
                    p.Color = new Color((float)c[0].AsDouble(), (float)c[1].AsDouble(), (float)c[2].AsDouble());
                p.Hp = F("hp", p.Hp);
                p.Speed = F("speed", p.Speed);
                p.WaveHp = Mathf.Max(0.1f, F("wave_hp", p.WaveHp));
                p.Radius = F("radius", p.Radius);
                p.Interval = Mathf.Max(1f, F("interval", p.Interval));
                p.Charge = Mathf.Clamp(F("charge", p.Charge), 0.2f, p.Interval);
                p.TowerShare = F("tower_share", p.TowerShare);
                p.BitShare = F("bit_share", p.BitShare);
                p.SpireHitShare = F("spire_hit_share", p.SpireHitShare);
                p.SpireHitInterval = Mathf.Max(0.2f, F("spire_hit_interval", p.SpireHitInterval));
                p.ContactDamage = F("contact_damage", p.ContactDamage);
                p.BarricadeShare = Mathf.Clamp(F("barricade_share", p.BarricadeShare), 0f, 1f);
                p.StunGuard = Mathf.Max(0f, F("stun_guard", p.StunGuard));
                p.Reward = (int)F("reward", p.Reward);
                p.WarnWaves = (int)F("warn_waves", p.WarnWaves);
                _bosses[p.Id] = p;
            }
        }
    }
}
