using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A single playable site within a region. Each site is a specific
    /// map + wave variant + difficulty + reward the player can select.
    /// Clearing a site marks it as conquered. Clearing all sites in a region
    /// conquers the region and grants a persistent planet-wide buff.
    /// </summary>
    public class TerritorySite
    {
        public string Id;
        public string Name;
        public int Difficulty;          // 1-7, shown as stars/skulls
        public string MapLayout;        // Which map to load
        public string WaveSet;          // Which wave variant
        public int RewardResources;     // Meta resources earned on clear
        public string RewardLabel;      // Description shown to player
        public float BonusExtractionMult = 1f;  // Extraction bonus during this run
        public bool IsBossSite;         // Is this a boss fight?
        public string BossId;
        public int BossWave;
    }

    /// <summary>
    /// A region containing 3-5 sites. Clearing all sites conquers the region
    /// and grants a persistent buff. Regions unlock sequentially.
    /// </summary>
    public class TerritoryRegion
    {
        public string Id;
        public string Name;
        public string Description;
        public string RequiresRegion;   // Previous region that must be conquered first
        public bool IsBossRegion;
        public List<TerritorySite> Sites = new();
        public ConquestBuff Buff;       // Buff granted when region is conquered
    }

    /// <summary>
    /// Persistent buff granted when a region is conquered.
    /// Stacks across regions on the same planet.
    /// </summary>
    public class ConquestBuff
    {
        public string Type;     // "resource_mult", "extraction_mult", "tower_damage_mult", "relic_drop_mult", "unlock"
        public float Value;
        public string Label;    // Human-readable description
    }

    /// <summary>
    /// All territory data for a planet: regions → sites hierarchy.
    /// </summary>
    public class PlanetTerritory
    {
        public string Name;
        public int PlanetId;
        public string RequiresPlanetBoss;   // Boss site ID that must be cleared to unlock this planet
        public List<TerritoryRegion> Regions = new();

        /// <summary>Compat: old code reads Sections — flatten all sites from all regions.</summary>
        public List<TerritorySection> Sections
        {
            get
            {
                var result = new List<TerritorySection>();
                foreach (var region in Regions)
                    foreach (var site in region.Sites)
                        result.Add(TerritoryManager.GetSection(site.Id));
                return result.Where(s => s != null).ToList();
            }
        }
    }

    // ── Kept for backward compatibility ──
    public class TerritorySection
    {
        public string Id;
        public string Name;
        public string Description;
        public int Cost;
        public bool UnlockedByDefault;
        public string Requires;
        public string UnlocksMap;
        public string UnlocksWaveSet;
        public bool UnlocksBoss;
        public string BossId;
        public float BonusExtractionMult = 1f;
        public bool GatesBoss;
        public int BossWave;
        public List<string> MapVariants = new();
    }

    /// <summary>
    /// Loads territory hierarchy from JSON, tracks conquest state.
    /// Planet → Regions → Sites. Clearing sites conquers regions. Conquering regions grants buffs.
    /// </summary>
    public static class TerritoryManager
    {
        private static Dictionary<int, PlanetTerritory> _planets = new();
        private const string DataPath = "res://Data/territory.json";
        private static bool _loaded;

        public static void Load()
        {
            _planets.Clear();
            _loaded = true;

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
                    Name = GetStr(pData, "name") ?? $"Planet {planetId}",
                    RequiresPlanetBoss = GetStr(pData, "requires_planet_boss")
                };

                if (pData.ContainsKey("regions") && pData["regions"].Obj is Godot.Collections.Array regions)
                {
                    foreach (var regionItem in regions)
                    {
                        if (regionItem.Obj is not Godot.Collections.Dictionary rd) continue;

                        var region = new TerritoryRegion
                        {
                            Id = GetStr(rd, "id"),
                            Name = GetStr(rd, "name"),
                            Description = GetStr(rd, "description"),
                            RequiresRegion = GetStr(rd, "requires_region"),
                            IsBossRegion = GetBool(rd, "is_boss_region")
                        };

                        // Conquest buff
                        if (rd.ContainsKey("conquest_buff") && rd["conquest_buff"].Obj is Godot.Collections.Dictionary bd)
                        {
                            region.Buff = new ConquestBuff
                            {
                                Type = GetStr(bd, "type"),
                                Value = GetFloat(bd, "value", 1f),
                                Label = GetStr(bd, "label")
                            };
                        }

                        // Sites
                        if (rd.ContainsKey("sites") && rd["sites"].Obj is Godot.Collections.Array sites)
                        {
                            foreach (var siteItem in sites)
                            {
                                if (siteItem.Obj is not Godot.Collections.Dictionary sd) continue;
                                region.Sites.Add(new TerritorySite
                                {
                                    Id = GetStr(sd, "id"),
                                    Name = GetStr(sd, "name"),
                                    Difficulty = GetInt(sd, "difficulty"),
                                    MapLayout = GetStr(sd, "map_layout") ?? "gateway",
                                    WaveSet = GetStr(sd, "wave_set") ?? "standard",
                                    RewardResources = GetInt(sd, "reward_resources"),
                                    RewardLabel = GetStr(sd, "reward_label"),
                                    BonusExtractionMult = GetFloat(sd, "bonus_extraction_mult", 1f),
                                    IsBossSite = GetBool(sd, "is_boss_site"),
                                    BossId = GetStr(sd, "boss_id"),
                                    BossWave = GetInt(sd, "boss_wave")
                                });
                            }
                        }

                        territory.Regions.Add(region);
                    }
                }

                _planets[planetId] = territory;
                int totalSites = territory.Regions.Sum(r => r.Sites.Count);
                GD.Print($"[TerritoryManager] Planet {planetId} ({territory.Name}): {territory.Regions.Count} regions, {totalSites} sites");
            }
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        // ── Queries ──

        public static PlanetTerritory GetPlanet(int planetId)
        {
            EnsureLoaded();
            _planets.TryGetValue(planetId, out var p);
            return p;
        }

        public static TerritorySite GetSite(string siteId)
        {
            EnsureLoaded();
            foreach (var planet in _planets.Values)
                foreach (var region in planet.Regions)
                    foreach (var site in region.Sites)
                        if (site.Id == siteId) return site;
            return null;
        }

        public static TerritoryRegion GetRegion(string regionId)
        {
            EnsureLoaded();
            foreach (var planet in _planets.Values)
                foreach (var region in planet.Regions)
                    if (region.Id == regionId) return region;
            return null;
        }

        /// <summary>Is a specific site cleared (conquered)?</summary>
        public static bool IsSiteCleared(string siteId, MetaPerkSaveData save)
        {
            return save?.ClearedSites?.Contains(siteId) ?? false;
        }

        /// <summary>Is an entire region conquered (all sites cleared)?</summary>
        public static bool IsRegionConquered(string regionId, MetaPerkSaveData save)
        {
            var region = GetRegion(regionId);
            if (region == null) return false;
            return region.Sites.All(s => IsSiteCleared(s.Id, save));
        }

        /// <summary>Is a region accessible (prerequisite region conquered or no prereq)?</summary>
        public static bool IsRegionAccessible(string regionId, MetaPerkSaveData save)
        {
            var region = GetRegion(regionId);
            if (region == null) return false;
            if (string.IsNullOrEmpty(region.RequiresRegion)) return true;
            return IsRegionConquered(region.RequiresRegion, save);
        }

        /// <summary>Is a planet accessible (prerequisite boss cleared or no prereq)?</summary>
        public static bool IsPlanetAccessible(int planetId, MetaPerkSaveData save)
        {
            EnsureLoaded();
            if (!_planets.TryGetValue(planetId, out var planet)) return false;
            if (string.IsNullOrEmpty(planet.RequiresPlanetBoss)) return true;
            return IsSiteCleared(planet.RequiresPlanetBoss, save);
        }

        /// <summary>Mark a site as cleared. Returns reward resources.</summary>
        public static int ClearSite(string siteId, MetaPerkSaveData save)
        {
            if (IsSiteCleared(siteId, save)) return 0;

            save.ClearedSites ??= new List<string>();
            save.ClearedSites.Add(siteId);

            var site = GetSite(siteId);
            int reward = site?.RewardResources ?? 0;

            GD.Print($"[TerritoryManager] Site cleared: {site?.Name ?? siteId} (+{reward} resources)");

            // Check if this completes a region
            foreach (var planet in _planets.Values)
            {
                foreach (var region in planet.Regions)
                {
                    if (region.Sites.Any(s => s.Id == siteId) && IsRegionConquered(region.Id, save))
                    {
                        GD.Print($"[TerritoryManager] REGION CONQUERED: {region.Name}");
                        if (region.Buff != null)
                            GD.Print($"[TerritoryManager] Buff granted: {region.Buff.Label}");

                        save.ConqueredRegions ??= new List<string>();
                        if (!save.ConqueredRegions.Contains(region.Id))
                            save.ConqueredRegions.Add(region.Id);

                        GameEvents.OnTerritoryUnlocked?.Invoke(region.Id);
                    }
                }
            }

            return reward;
        }

        /// <summary>Get all active conquest buffs for a planet.</summary>
        public static List<ConquestBuff> GetActiveBuffs(int planetId, MetaPerkSaveData save)
        {
            EnsureLoaded();
            var buffs = new List<ConquestBuff>();
            if (!_planets.TryGetValue(planetId, out var planet)) return buffs;

            foreach (var region in planet.Regions)
            {
                if (region.Buff != null && IsRegionConquered(region.Id, save))
                    buffs.Add(region.Buff);
            }
            return buffs;
        }

        /// <summary>Get combined multiplier for a specific buff type across all conquered regions on a planet.</summary>
        public static float GetBuffMultiplier(int planetId, string buffType, MetaPerkSaveData save)
        {
            float mult = 1f;
            foreach (var buff in GetActiveBuffs(planetId, save))
            {
                if (buff.Type == buffType)
                    mult *= buff.Value;
            }
            return mult;
        }

        /// <summary>Progress summary: sites cleared / total sites for a planet.</summary>
        public static (int cleared, int total) GetPlanetProgress(int planetId, MetaPerkSaveData save)
        {
            EnsureLoaded();
            if (!_planets.TryGetValue(planetId, out var planet)) return (0, 0);

            int total = 0, cleared = 0;
            foreach (var region in planet.Regions)
            {
                foreach (var site in region.Sites)
                {
                    total++;
                    if (IsSiteCleared(site.Id, save)) cleared++;
                }
            }
            return (cleared, total);
        }

        // ── Backward compat (old TerritorySection queries used by S4 screens) ──

        public static TerritorySection GetSection(string sectionId)
        {
            // Try to find as a site and wrap
            var site = GetSite(sectionId);
            if (site == null) return null;
            return new TerritorySection
            {
                Id = site.Id,
                Name = site.Name,
                BonusExtractionMult = site.BonusExtractionMult,
                MapVariants = new List<string> { site.MapLayout },
                GatesBoss = site.IsBossSite,
                BossId = site.BossId,
                BossWave = site.BossWave
            };
        }

        public static bool IsSectionUnlocked(string sectionId, MetaPerkSaveData save) =>
            IsSiteCleared(sectionId, save);

        public static bool IsBossUnlocked(int planetId, MetaPerkSaveData save)
        {
            EnsureLoaded();
            if (!_planets.TryGetValue(planetId, out var planet)) return false;
            foreach (var region in planet.Regions)
            {
                if (region.IsBossRegion && IsRegionAccessible(region.Id, save))
                    return true;
            }
            return false;
        }

        // ── JSON helpers ──

        private static string GetStr(Godot.Collections.Dictionary d, string k)
            => d.ContainsKey(k) ? d[k].AsString() : null;
        private static int GetInt(Godot.Collections.Dictionary d, string k, int fb = 0)
            => d.ContainsKey(k) ? d[k].AsInt32() : fb;
        private static float GetFloat(Godot.Collections.Dictionary d, string k, float fb = 0)
            => d.ContainsKey(k) ? (float)d[k].AsDouble() : fb;
        private static bool GetBool(Godot.Collections.Dictionary d, string k)
            => d.ContainsKey(k) && d[k].AsBool();
    }

    // ── Backward compat wrappers used by S4 UI screens ──

    public static class TerritoryLoader
    {
        public static PlanetTerritory GetPlanet(int planetId) => TerritoryManager.GetPlanet(planetId);
        public static TerritorySection GetSection(string sectionId) => TerritoryManager.GetSection(sectionId);
        public static bool IsUnlocked(string sectionId, TerritorySaveData save) =>
            TerritoryManager.IsSiteCleared(sectionId, save?.MetaSave);
        public static bool CanUnlock(string sectionId, TerritorySaveData save, int resources) => false;
        public static bool TryUnlock(string sectionId, TerritorySaveData save, ref int resources) => false; // Old cost system removed — sites cleared by playing
        public static TerritorySection GetBossSection(int planetId, TerritorySaveData save) => null;

        public static Dictionary<int, PlanetTerritory> LoadAll()
        {
            var result = new Dictionary<int, PlanetTerritory>();
            for (int i = 1; i <= 5; i++)
            {
                var p = TerritoryManager.GetPlanet(i);
                if (p != null) result[i] = p;
            }
            return result;
        }
    }

    public class TerritorySaveData
    {
        public MetaPerkSaveData MetaSave;
        public List<string> ClearedBossSections = new();
        public List<string> UnlockedSections => MetaSave?.UnlockedTerritories ?? new List<string>();
    }

    public static class TerritorySave
    {
        public static TerritorySaveData Load() => new TerritorySaveData { MetaSave = MetaPerkSave.Load() };
        public static void Save(TerritorySaveData data) { if (data?.MetaSave != null) MetaPerkSave.Save(data.MetaSave); }
        public static void Reset() => MetaPerkSave.Reset();
    }

    public class TerritoryPlanetData : PlanetTerritory { }
}
