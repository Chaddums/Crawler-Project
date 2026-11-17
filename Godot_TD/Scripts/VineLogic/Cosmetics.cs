using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>One thing the Company Store sells (Data/cosmetics.json).</summary>
    public class CosmeticItem
    {
        public string Id, Slot, Name, Line, Crown;
        public int Price, RequiresCampaigns;
        public Color? HullDark, HullLight, Glow, Rim;
        public float RimStrength = 1f, CrownScale = 1f;

        /// <summary>The colour that stands for it on a swatch.</summary>
        public Color Swatch => Glow ?? Rim ?? Colors.White;
    }

    public class CosmeticSlot
    {
        public string Id, Name, What;
    }

    /// <summary>
    /// The Company Store: banked Resources (every run's extraction, which nothing spent until
    /// 2026-11) buy looks nobody starts with: a paint for BIT and its frames, a trim for the
    /// towers' edge light, and lights and a crown for the Spire. Looks only: nothing sold here
    /// changes how anything fights. One item is worn per slot; some ask for a finished campaign.
    /// What is owned and worn is in the meta save (cosmetics_owned, cosmetics_worn).
    /// </summary>
    public static class Cosmetics
    {
        public const string Paint = "paint", Trim = "trim", Spire = "spire";
        private const string DataPath = "res://Data/cosmetics.json";

        private static readonly List<CosmeticSlot> _slots = new();
        private static readonly List<CosmeticItem> _items = new();
        private static bool _loaded;

        public static IReadOnlyList<CosmeticSlot> Slots { get { Load(); return _slots; } }
        public static IReadOnlyList<CosmeticItem> All { get { Load(); return _items; } }
        public static IEnumerable<CosmeticItem> In(string slot) => All.Where(i => i.Slot == slot);
        public static CosmeticItem Get(string id) { Load(); return id == null ? null : _items.FirstOrDefault(i => i.Id == id); }

        // ── What is worn now (the game reads these when it builds BIT, a tower, the Spire) ──

        private static MetaPerkSaveData Save => GameManager.Instance?.MetaSave;

        // The store's screen shows an item before it is bought: worn for the preview only
        private static readonly Dictionary<string, CosmeticItem> _preview = new();
        private static int _version;
        private static MetaPerkSaveData _cacheSave;
        private static int _cacheVersion = -1;
        private static CosmeticItem _paint, _trim, _spire;

        /// <summary>Show an item in a slot whatever the save wears (null: back to the save's). The store's preview.</summary>
        public static void SetPreview(string slot, CosmeticItem item, bool standardIssue = false)
        {
            if (item == null && !standardIssue) _preview.Remove(slot);
            else _preview[slot] = item;
            _version++;
        }

        public static void ClearPreview() { _preview.Clear(); _version++; }

        /// <summary>Something outside Buy and Wear changed what a save wears (tests).</summary>
        public static void Changed() => _version++;

        private static void Refresh()
        {
            var save = Save;
            if (ReferenceEquals(save, _cacheSave) && _cacheVersion == _version) return;
            _cacheSave = save;
            _cacheVersion = _version;
            _paint = Worn(save, Paint);
            _trim = Worn(save, Trim);
            _spire = Worn(save, Spire);
        }

        /// <summary>The item worn in a slot, or null for the standard issue.</summary>
        public static CosmeticItem Worn(string slot)
        {
            Refresh();
            return slot == Paint ? _paint : slot == Trim ? _trim : slot == Spire ? _spire : Worn(Save, slot);
        }

        public static CosmeticItem Worn(MetaPerkSaveData save, string slot)
        {
            if (_preview.TryGetValue(slot, out var shown)) return shown;
            if (save == null || !save.WornCosmetics.TryGetValue(slot, out var id)) return null;
            var item = Get(id);
            // Worn means owned: a save edited by hand, or an item taken out of the file, wears nothing
            return item != null && item.Slot == slot && save.OwnedCosmetics.Contains(id) ? item : null;
        }

        /// <summary>BIT's paint (its frames wear it too), or null.</summary>
        public static CosmeticItem BitPaint => Worn(Paint);
        /// <summary>The towers' edge light, or null for the planet's.</summary>
        public static Color? TowerRim => Worn(Trim)?.Rim;
        /// <summary>
        /// How strong an edge light is drawn: the planet's own strength, or for a bought one at
        /// least 0.7 of full (Scrapyard draws its rim at 0.4, where a bought colour barely showed)
        /// times the item's own.
        /// </summary>
        public static float RimStrength(CosmeticItem item, float planetStrength)
            => item?.Rim == null ? planetStrength : Mathf.Max(planetStrength, 0.7f) * item.RimStrength;
        /// <summary>The Spire's edge light and crown, or null.</summary>
        public static CosmeticItem SpireLights => Worn(Spire);

        // ── Owning and buying ──

        public static bool Owns(MetaPerkSaveData save, string id) => save != null && id != null && save.OwnedCosmetics.Contains(id);

        public static (int owned, int total) Counts(MetaPerkSaveData save)
        {
            Load();
            return (save == null ? 0 : _items.Count(i => save.OwnedCosmetics.Contains(i.Id)), _items.Count);
        }

        /// <summary>Why this can't be bought now, or null when it can.</summary>
        public static string WhyNot(MetaPerkSaveData save, CosmeticItem item)
        {
            if (save == null || item == null) return "Not for sale";
            if (Owns(save, item.Id)) return "Owned";
            if (save.CampaignsCompleted < item.RequiresCampaigns)
                return item.RequiresCampaigns == 1 ? "Finish a campaign first" : $"Finish {item.RequiresCampaigns} campaigns first";
            if (save.MetaResources < item.Price) return $"{item.Price - save.MetaResources:N0} Resources short";
            return null;
        }

        /// <summary>Buy it with banked Resources and put it on. Saved at once. False when it can't be bought.</summary>
        public static bool Buy(MetaPerkSaveData save, CosmeticItem item)
        {
            if (WhyNot(save, item) != null) return false;
            save.MetaResources -= item.Price;
            save.OwnedCosmetics.Add(item.Id);
            save.WornCosmetics[item.Slot] = item.Id;
            _version++;
            MetaPerkSave.Save(save);
            GD.Print($"[Store] Bought {item.Name} ({item.Slot}) for {item.Price}; {save.MetaResources} banked");
            return true;
        }

        /// <summary>Wear an owned item (or null: the standard issue) in a slot. Saved at once.</summary>
        public static bool Wear(MetaPerkSaveData save, string slot, CosmeticItem item)
        {
            if (save == null || string.IsNullOrEmpty(slot)) return false;
            if (item == null) save.WornCosmetics.Remove(slot);
            else
            {
                if (item.Slot != slot || !Owns(save, item.Id)) return false;
                save.WornCosmetics[slot] = item.Id;
            }
            _version++;
            MetaPerkSave.Save(save);
            return true;
        }

        /// <summary>
        /// How big a crown is on a Spire whose column is <paramref name="columnDiameter"/> across:
        /// a little wider than the column (a sheet's band is about 1.1 across), within what reads
        /// on a thin Obelisk and a broad Bruteforge alike, times the item's own crown_scale.
        /// </summary>
        public static float CrownScaleFor(CosmeticItem item, float columnDiameter)
            => Mathf.Clamp(columnDiameter * 1.25f, 1.3f, 3.4f) / 1.1f * (item?.CrownScale ?? 1f);

        /// <summary>Tests: read the file again.</summary>
        public static void Reload() { _loaded = false; Load(); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _slots.Clear();
            _items.Clear();
            if (!FileAccess.FileExists(DataPath)) return;
            using var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) { GD.PushWarning("[Store] cosmetics.json did not parse"); return; }
            var root = parsed.AsGodotDictionary();
            if (root.ContainsKey("slots") && root["slots"].VariantType == Variant.Type.Array)
                foreach (var s in root["slots"].AsGodotArray())
                {
                    if (s.VariantType != Variant.Type.Dictionary) continue;
                    var d = s.AsGodotDictionary();
                    _slots.Add(new CosmeticSlot { Id = Str(d, "id"), Name = Str(d, "name"), What = Str(d, "what") });
                }
            if (root.ContainsKey("items") && root["items"].VariantType == Variant.Type.Array)
                foreach (var it in root["items"].AsGodotArray())
                {
                    if (it.VariantType != Variant.Type.Dictionary) continue;
                    var d = it.AsGodotDictionary();
                    var item = new CosmeticItem
                    {
                        Id = Str(d, "id"), Slot = Str(d, "slot"), Name = Str(d, "name"), Line = Str(d, "line") ?? "",
                        Crown = Str(d, "crown"),
                        Price = d.ContainsKey("price") ? d["price"].AsInt32() : 0,
                        RequiresCampaigns = d.ContainsKey("requires_campaigns") ? d["requires_campaigns"].AsInt32() : 0,
                        HullDark = Col(d, "hull_dark"), HullLight = Col(d, "hull_light"), Glow = Col(d, "glow"), Rim = Col(d, "rim"),
                    };
                    if (d.ContainsKey("rim_strength")) item.RimStrength = (float)d["rim_strength"].AsDouble();
                    if (d.ContainsKey("crown_scale")) item.CrownScale = (float)d["crown_scale"].AsDouble();
                    if (string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Slot)) continue;
                    _items.Add(item);
                }
        }

        private static string Str(Godot.Collections.Dictionary d, string k) => d.ContainsKey(k) ? d[k].AsString() : null;

        private static Color? Col(Godot.Collections.Dictionary d, string k)
        {
            if (!d.ContainsKey(k) || d[k].VariantType != Variant.Type.Array) return null;
            var a = d[k].AsGodotArray();
            return a.Count >= 3 ? new Color((float)a[0].AsDouble(), (float)a[1].AsDouble(), (float)a[2].AsDouble()) : null;
        }
    }
}
