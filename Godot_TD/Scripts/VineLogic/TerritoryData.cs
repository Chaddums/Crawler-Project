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
                            BonusExtractionMult = GetFloat(s, "bonus_extraction_mult", 1f)
                        };
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
    }
}
