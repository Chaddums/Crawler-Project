using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A single unlockable section within a planet.
    /// Deterministic cost, no RNG. The escape valve for the unlucky player.
    /// </summary>
    public class TerritorySection
    {
        public string Id;
        public string Name;
        public string Description;
        public int Cost;
        public bool UnlockedByDefault;
        public string Requires;           // Id of prerequisite section (null = no prereq)
        public string UnlocksMap;         // Map layout this section opens
        public string UnlocksWaveSet;     // Wave variant this section enables
        public bool UnlocksBoss;          // Whether this section gates a boss fight
        public string BossId;             // Boss identifier if UnlocksBoss
        public float BonusExtractionMult = 1f;  // Extraction bonus for runs in this section

        // S4: Boss run fields
        public bool GatesBoss;            // Alias: same as UnlocksBoss, used by boss run flow
        public int BossWave;              // Wave number where boss spawns in boss run
        public List<string> MapVariants = new();  // Map variant ids this section unlocks
    }

    /// <summary>
    /// All territory sections for a planet.
    /// </summary>
    public class PlanetTerritory
    {
        public string Name;
        public int PlanetId;
        public List<TerritorySection> Sections = new();
    }

    /// <summary>
    /// Loads territory data from JSON, tracks unlock state via MetaPerkSaveData.
    /// Deterministic progression: same cost, same unlock, every time.
    /// </summary>
    public static class TerritoryManager
    {
        private static Dictionary<int, PlanetTerritory> _planets = new();
        private const string DataPath = "res://Data/territory.json";

        public static void Load()
        {
            _planets.Clear();

            if (!FileAccess.FileExists(DataPath))
            {
                GD.PushWarning("[TerritoryManager] No territory.json found");
                return;
            }

            var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
            if (file == null) return;

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok) { file.Close(); return; }
            file.Close();

            if (json.Data.Obj is not Godot.Collections.Dictionary root) return;
            if (!root.ContainsKey("planets")) return;
            if (root["planets"].Obj is not Godot.Collections.Dictionary planets) return;

            foreach (var planetKey in planets.Keys)
            {
                int planetId = int.Parse(planetKey.AsString());
                if (planets[planetKey].Obj is not Godot.Collections.Dictionary pData) continue;

                var territory = new PlanetTerritory
                {
                    PlanetId = planetId,
                    Name = pData.ContainsKey("name") ? (string)pData["name"] : $"Planet {planetId}"
                };

                if (pData.ContainsKey("sections") && pData["sections"].Obj is Godot.Collections.Array sections)
                {
                    foreach (var item in sections)
                    {
                        if (item.Obj is not Godot.Collections.Dictionary s) continue;

                        var section = new TerritorySection
                        {
                            Id = GetStr(s, "id"),
                            Name = GetStr(s, "name"),
                            Description = GetStr(s, "description"),
                            Cost = GetInt(s, "cost"),
                            UnlockedByDefault = GetBool(s, "unlocked_by_default"),
                            Requires = GetStr(s, "requires"),
                            UnlocksMap = GetStr(s, "unlocks_map"),
                            UnlocksWaveSet = GetStr(s, "unlocks_wave_set"),
                            UnlocksBoss = GetBool(s, "unlocks_boss"),
                            BossId = GetStr(s, "boss_id"),
                            BonusExtractionMult = GetFloat(s, "bonus_extraction_mult", 1f),
                            // S4: Boss run fields
                            GatesBoss = GetBool(s, "gates_boss") || GetBool(s, "unlocks_boss"),
                            BossWave = GetInt(s, "boss_wave"),
                        };

                        // S4: Read map_variants array
                        if (s.ContainsKey("map_variants") && s["map_variants"].Obj is Godot.Collections.Array mvArr)
                        {
                            foreach (var mv in mvArr)
                                section.MapVariants.Add(mv.AsString());
                        }

                        territory.Sections.Add(section);
                    }
                }

                _planets[planetId] = territory;
                GD.Print($"[TerritoryManager] Loaded planet {planetId} ({territory.Name}): {territory.Sections.Count} sections");
            }
        }

        /// <summary>Get all territory data for a planet.</summary>
        public static PlanetTerritory GetPlanet(int planetId)
        {
            _planets.TryGetValue(planetId, out var territory);
            return territory;
        }

        /// <summary>Check if a section is unlocked (by default or by player spending resources).</summary>
        public static bool IsSectionUnlocked(string sectionId, MetaPerkSaveData save)
        {
            // Check all planets for this section
            foreach (var planet in _planets.Values)
            {
                foreach (var section in planet.Sections)
                {
                    if (section.Id == sectionId)
                    {
                        if (section.UnlockedByDefault) return true;
                        return save?.UnlockedTerritories?.Contains(sectionId) ?? false;
                    }
                }
            }
            return false;
        }

        /// <summary>Check if a section can be unlocked (prerequisites met + enough resources).</summary>
        public static bool CanUnlock(string sectionId, MetaPerkSaveData save, int availableResources)
        {
            foreach (var planet in _planets.Values)
            {
                foreach (var section in planet.Sections)
                {
                    if (section.Id != sectionId) continue;
                    if (section.UnlockedByDefault) return false; // Already unlocked
                    if (IsSectionUnlocked(sectionId, save)) return false; // Already bought

                    // Check prerequisite
                    if (!string.IsNullOrEmpty(section.Requires) && !IsSectionUnlocked(section.Requires, save))
                        return false;

                    // Check cost
                    return availableResources >= section.Cost;
                }
            }
            return false;
        }

        /// <summary>
        /// Unlock a territory section. Deducts cost from meta resources.
        /// Returns true if successful.
        /// </summary>
        public static bool TryUnlock(string sectionId, MetaPerkSaveData save, ref int metaResources)
        {
            foreach (var planet in _planets.Values)
            {
                foreach (var section in planet.Sections)
                {
                    if (section.Id != sectionId) continue;
                    if (!CanUnlock(sectionId, save, metaResources)) return false;

                    metaResources -= section.Cost;
                    save.UnlockedTerritories ??= new List<string>();
                    save.UnlockedTerritories.Add(sectionId);

                    GD.Print($"[TerritoryManager] Unlocked '{section.Name}' for {section.Cost} resources. Remaining: {metaResources}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>Get all unlocked sections for a planet.</summary>
        public static List<TerritorySection> GetUnlockedSections(int planetId, MetaPerkSaveData save)
        {
            var result = new List<TerritorySection>();
            if (!_planets.TryGetValue(planetId, out var planet)) return result;

            foreach (var section in planet.Sections)
            {
                if (section.UnlockedByDefault || (save?.UnlockedTerritories?.Contains(section.Id) ?? false))
                    result.Add(section);
            }
            return result;
        }

        /// <summary>Is the boss section unlocked for this planet?</summary>
        public static bool IsBossUnlocked(int planetId, MetaPerkSaveData save)
        {
            if (!_planets.TryGetValue(planetId, out var planet)) return false;
            foreach (var section in planet.Sections)
            {
                if (section.UnlocksBoss && IsSectionUnlocked(section.Id, save))
                    return true;
            }
            return false;
        }

        // ── JSON helpers ──

        private static string GetStr(Godot.Collections.Dictionary d, string key)
            => d.ContainsKey(key) ? d[key].AsString() : null;

        private static int GetInt(Godot.Collections.Dictionary d, string key)
            => d.ContainsKey(key) ? d[key].AsInt32() : 0;

        private static float GetFloat(Godot.Collections.Dictionary d, string key, float fallback = 0f)
            => d.ContainsKey(key) ? (float)d[key].AsDouble() : fallback;

        private static bool GetBool(Godot.Collections.Dictionary d, string key)
            => d.ContainsKey(key) && d[key].AsBool();

        /// <summary>S4: Get a section by id across all planets.</summary>
        public static TerritorySection GetSection(string sectionId)
        {
            foreach (var planet in _planets.Values)
                foreach (var section in planet.Sections)
                    if (section.Id == sectionId)
                        return section;
            return null;
        }

        /// <summary>S4: Get the boss section for a planet (if unlocked).</summary>
        public static TerritorySection GetBossSection(int planetId, MetaPerkSaveData save)
        {
            if (!_planets.TryGetValue(planetId, out var planet)) return null;
            foreach (var section in planet.Sections)
            {
                if (section.GatesBoss && IsSectionUnlocked(section.Id, save))
                    return section;
            }
            return null;
        }
    }

    // ── S4: Compatibility aliases used by TerritoryScreen, BossConfirmScreen, GameManager ──

    /// <summary>
    /// Alias for TerritoryManager — used by S4 UI code.
    /// Ensures TerritoryManager.Load() is called on first access.
    /// </summary>
    public static class TerritoryLoader
    {
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (!_loaded)
            {
                TerritoryManager.Load();
                _loaded = true;
            }
        }

        public static Dictionary<int, PlanetTerritory> LoadAll()
        {
            EnsureLoaded();
            var result = new Dictionary<int, PlanetTerritory>();
            // Try planets 1-5
            for (int i = 1; i <= 5; i++)
            {
                var p = TerritoryManager.GetPlanet(i);
                if (p != null) result[i] = p;
            }
            return result;
        }

        public static PlanetTerritory GetPlanet(int planetId)
        {
            EnsureLoaded();
            return TerritoryManager.GetPlanet(planetId);
        }

        public static TerritorySection GetSection(string sectionId)
        {
            EnsureLoaded();
            return TerritoryManager.GetSection(sectionId);
        }

        public static bool IsUnlocked(string sectionId, TerritorySaveData save)
        {
            EnsureLoaded();
            return TerritoryManager.IsSectionUnlocked(sectionId, save?.MetaSave);
        }

        public static bool CanUnlock(string sectionId, TerritorySaveData save, int availableResources)
        {
            EnsureLoaded();
            return TerritoryManager.CanUnlock(sectionId, save?.MetaSave, availableResources);
        }

        public static bool TryUnlock(string sectionId, TerritorySaveData save, ref int resources)
        {
            EnsureLoaded();
            if (!TerritoryManager.TryUnlock(sectionId, save?.MetaSave, ref resources))
                return false;
            MetaPerkSave.Save(save?.MetaSave);
            GameEvents.OnTerritoryUnlocked?.Invoke(sectionId);
            return true;
        }

        public static TerritorySection GetBossSection(int planetId, TerritorySaveData save)
        {
            EnsureLoaded();
            return TerritoryManager.GetBossSection(planetId, save?.MetaSave);
        }
    }

    /// <summary>
    /// S4: Wrapper around MetaPerkSaveData for territory persistence.
    /// Territory unlock state is stored in MetaPerkSaveData.UnlockedTerritories.
    /// </summary>
    public class TerritorySaveData
    {
        public MetaPerkSaveData MetaSave;
        public List<string> ClearedBossSections = new();

        public List<string> UnlockedSections => MetaSave?.UnlockedTerritories ?? new List<string>();
    }

    /// <summary>
    /// S4: Load/save territory data using MetaPerkSave as backend.
    /// </summary>
    public static class TerritorySave
    {
        public static TerritorySaveData Load()
        {
            return new TerritorySaveData { MetaSave = MetaPerkSave.Load() };
        }

        public static void Save(TerritorySaveData data)
        {
            if (data?.MetaSave != null)
                MetaPerkSave.Save(data.MetaSave);
        }

        public static void Reset()
        {
            MetaPerkSave.Reset();
        }
    }

    /// <summary>
    /// S4: Alias — TerritoryPlanetData maps to PlanetTerritory.
    /// </summary>
    public class TerritoryPlanetData : PlanetTerritory { }
}
