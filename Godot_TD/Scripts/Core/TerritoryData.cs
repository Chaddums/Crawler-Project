using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A single unlockable section of a planet's territory.
    /// </summary>
    public class TerritorySection
    {
        public string Id;
        public string Name;
        public int Cost;
        public bool UnlockedByDefault;
        public List<string> Requires = new();
        public List<string> MapVariants = new();
        public bool GatesBoss;
        public int BossWave;
    }

    /// <summary>
    /// All territory sections for one planet.
    /// </summary>
    public class TerritoryPlanetData
    {
        public string Name;
        public int PlanetId;
        public List<TerritorySection> Sections = new();
    }

    /// <summary>
    /// Persistent save data for territory unlocks.
    /// Stored at user://territory.json.
    /// </summary>
    public class TerritorySaveData
    {
        public List<string> UnlockedSections = new();
        public List<string> ClearedBossSections = new();
        public int TotalResourcesSpent;
    }

    /// <summary>
    /// Static helper for loading/saving territory data.
    /// Follows MetaPerkSave pattern.
    /// </summary>
    public static class TerritorySave
    {
        private const string SavePath = "user://territory.json";

        public static TerritorySaveData Load()
        {
            if (!FileAccess.FileExists(SavePath))
                return CreateDefault();

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
                return CreateDefault();

            var text = file.GetAsText();
            var json = new Json();
            var err = json.Parse(text);
            if (err != Error.Ok)
            {
                GD.PushWarning($"[TerritorySave] Failed to parse save: {json.GetErrorMessage()}");
                return CreateDefault();
            }

            var dict = json.Data.AsGodotDictionary();
            var data = new TerritorySaveData();

            if (dict.ContainsKey("unlocked"))
            {
                var arr = dict["unlocked"].AsGodotArray();
                foreach (var id in arr)
                    data.UnlockedSections.Add(id.AsString());
            }

            if (dict.ContainsKey("cleared_bosses"))
            {
                var arr = dict["cleared_bosses"].AsGodotArray();
                foreach (var id in arr)
                    data.ClearedBossSections.Add(id.AsString());
            }

            if (dict.ContainsKey("total_spent"))
                data.TotalResourcesSpent = dict["total_spent"].AsInt32();

            return data;
        }

        public static void Save(TerritorySaveData data)
        {
            var dict = new Godot.Collections.Dictionary();

            var unlocked = new Godot.Collections.Array();
            foreach (var id in data.UnlockedSections)
                unlocked.Add(id);
            dict["unlocked"] = unlocked;

            var cleared = new Godot.Collections.Array();
            foreach (var id in data.ClearedBossSections)
                cleared.Add(id);
            dict["cleared_bosses"] = cleared;

            dict["total_spent"] = data.TotalResourcesSpent;

            var text = Json.Stringify(dict, "  ");
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError("[TerritorySave] Failed to open save file for writing");
                return;
            }
            file.StoreString(text);
            GD.Print("[TerritorySave] Saved to disk");
        }

        public static void Reset()
        {
            Save(CreateDefault());
        }

        private static TerritorySaveData CreateDefault()
        {
            return new TerritorySaveData();
        }
    }

    /// <summary>
    /// Loads territory definitions from Data/territory.json.
    /// </summary>
    public static class TerritoryLoader
    {
        private const string DataPath = "res://Data/territory.json";

        private static Dictionary<int, TerritoryPlanetData> _cache;

        public static Dictionary<int, TerritoryPlanetData> LoadAll()
        {
            if (_cache != null)
                return _cache;

            _cache = new Dictionary<int, TerritoryPlanetData>();

            if (!FileAccess.FileExists(DataPath))
            {
                GD.PushWarning("[TerritoryLoader] No territory.json found — using empty data");
                return _cache;
            }

            using var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
            if (file == null) return _cache;

            var text = file.GetAsText();
            var json = new Json();
            if (json.Parse(text) != Error.Ok)
            {
                GD.PushWarning($"[TerritoryLoader] Parse error: {json.GetErrorMessage()}");
                return _cache;
            }

            var root = json.Data.AsGodotDictionary();
            if (!root.ContainsKey("planets")) return _cache;

            var planets = root["planets"].AsGodotDictionary();
            foreach (var key in planets.Keys)
            {
                int planetId = key.AsString().ToInt();
                var planetDict = planets[key].AsGodotDictionary();
                var planetData = new TerritoryPlanetData
                {
                    PlanetId = planetId,
                    Name = planetDict.ContainsKey("name") ? planetDict["name"].AsString() : $"Planet {planetId}"
                };

                if (planetDict.ContainsKey("sections"))
                {
                    var sections = planetDict["sections"].AsGodotArray();
                    foreach (var sectionVar in sections)
                    {
                        var sd = sectionVar.AsGodotDictionary();
                        var section = new TerritorySection
                        {
                            Id = sd.ContainsKey("id") ? sd["id"].AsString() : "",
                            Name = sd.ContainsKey("name") ? sd["name"].AsString() : "",
                            Cost = sd.ContainsKey("cost") ? sd["cost"].AsInt32() : 0,
                            UnlockedByDefault = sd.ContainsKey("unlocked_by_default") && sd["unlocked_by_default"].AsBool(),
                            GatesBoss = sd.ContainsKey("gates_boss") && sd["gates_boss"].AsBool(),
                            BossWave = sd.ContainsKey("boss_wave") ? sd["boss_wave"].AsInt32() : 0
                        };

                        if (sd.ContainsKey("requires"))
                        {
                            var reqs = sd["requires"].AsGodotArray();
                            foreach (var r in reqs)
                                section.Requires.Add(r.AsString());
                        }

                        if (sd.ContainsKey("map_variants"))
                        {
                            var vars = sd["map_variants"].AsGodotArray();
                            foreach (var v in vars)
                                section.MapVariants.Add(v.AsString());
                        }

                        planetData.Sections.Add(section);
                    }
                }

                _cache[planetId] = planetData;
            }

            GD.Print($"[TerritoryLoader] Loaded {_cache.Count} planets");
            return _cache;
        }

        public static TerritoryPlanetData GetPlanet(int planetId)
        {
            var all = LoadAll();
            return all.TryGetValue(planetId, out var data) ? data : null;
        }

        public static TerritorySection GetSection(string sectionId)
        {
            var all = LoadAll();
            foreach (var planet in all.Values)
                foreach (var section in planet.Sections)
                    if (section.Id == sectionId)
                        return section;
            return null;
        }

        /// <summary>
        /// Check if a section is unlocked (either by default or via save data).
        /// </summary>
        public static bool IsUnlocked(string sectionId, TerritorySaveData save)
        {
            var section = GetSection(sectionId);
            if (section == null) return false;
            if (section.UnlockedByDefault) return true;
            return save.UnlockedSections.Contains(sectionId);
        }

        /// <summary>
        /// Check if all requirements for a section are met.
        /// </summary>
        public static bool CanUnlock(string sectionId, TerritorySaveData save, int availableResources)
        {
            var section = GetSection(sectionId);
            if (section == null) return false;
            if (IsUnlocked(sectionId, save)) return false;
            if (section.Cost > availableResources) return false;

            foreach (var req in section.Requires)
            {
                if (!IsUnlocked(req, save))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Attempt to unlock a territory section. Returns true on success.
        /// </summary>
        public static bool TryUnlock(string sectionId, TerritorySaveData save, ref int resources)
        {
            if (!CanUnlock(sectionId, save, resources))
                return false;

            var section = GetSection(sectionId);
            resources -= section.Cost;
            save.TotalResourcesSpent += section.Cost;
            save.UnlockedSections.Add(sectionId);
            TerritorySave.Save(save);

            GD.Print($"[Territory] Unlocked section: {section.Name} (cost {section.Cost})");
            return true;
        }

        /// <summary>
        /// Get the boss section for a planet (if it exists and is unlocked).
        /// </summary>
        public static TerritorySection GetBossSection(int planetId, TerritorySaveData save)
        {
            var planet = GetPlanet(planetId);
            if (planet == null) return null;

            foreach (var section in planet.Sections)
            {
                if (section.GatesBoss && IsUnlocked(section.Id, save))
                    return section;
            }

            return null;
        }
    }
}
