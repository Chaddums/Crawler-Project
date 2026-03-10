using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Static registry mapping game concepts to res://Models/ asset paths.
    /// Auto-scans for .glb/.tscn files on Initialize(). Falls back gracefully
    /// when assets are not downloaded.
    /// </summary>
    public static class ModelLibrary
    {
        // category -> (id -> res:// path)
        private static readonly Dictionary<string, Dictionary<string, string>> _registry = new();
        private static readonly Dictionary<string, PackedScene> _cache = new();
        private static readonly HashSet<string> _failedPaths = new();
        private static bool _initialized;

        // Category → subfolder mapping (scans both legacy Models/ and PolygonDungeon packs)
        private static readonly Dictionary<string, string> _categoryFolders = new()
        {
            { "player",  "res://Models/Characters/Player" },
            { "enemy",   "res://Models/Characters/Enemies" },
            { "weapon",  "res://Models/Weapons" },
            { "animation", "res://Models/Animations" },
            { "dungeon", "res://Models/Dungeon" },
            { "prop",    "res://Models/Dungeon/Props" },
            { "floor",   "res://Models/Dungeon/Floors" },
            { "wall",    "res://Models/Dungeon/Walls" },
            { "door",    "res://Models/Dungeon/Doors" },
            { "detail",  "res://Models/Dungeon/Details" },
            { "item",    "res://Models/Items" },
        };

        // Additional scan folders — merged into existing categories
        private static readonly (string category, string folder)[] _extraScanFolders = new[]
        {
            ("prop",    "res://Assets/PolygonDungeon/Prefabs/Props"),
            ("prop",    "res://Assets/PolygonDungeon/Prefabs/Environments/Misc"),
            ("wall",    "res://Assets/PolygonDungeon/Prefabs/Environments/Walls"),
            ("floor",   "res://Assets/PolygonDungeon/Prefabs/Environments/Floors"),
            ("pillar",  "res://Assets/PolygonDungeon/Prefabs/Environments/Pillars"),
            ("prop",    "res://Assets/PolygonDungeon/Prefabs/Environments/Pillars"),
            ("rock",    "res://Assets/PolygonDungeon/Prefabs/Environments/Rocks"),
            ("wood",    "res://Assets/PolygonDungeon/Prefabs/Environments/Wood"),
            ("bone",    "res://Assets/PolygonDungeon/Prefabs/Environments/Bones"),
            ("item",    "res://Assets/PolygonDungeon/Prefabs/Items"),
            ("weapon",  "res://Assets/PolygonDungeon/Prefabs/Weapons"),
        };

        /// <summary>
        /// Scan res://Models/ subfolders and register all .glb and .tscn files.
        /// Safe to call multiple times (no-ops after first).
        /// </summary>
        /// <summary>
        /// When true, ModelLibrary skips scanning and all TryLoad calls return null
        /// (procedural fallback). Set to false once imported models are properly
        /// configured with correct scale and materials.
        /// </summary>
        public static bool ForceProcedural { get; set; } = false;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (ForceProcedural)
            {
                GD.Print("[ModelLibrary] Initialized: ForceProcedural=true, using procedural fallback for all models");
                return;
            }

            foreach (var (category, folder) in _categoryFolders)
            {
                if (!_registry.ContainsKey(category))
                    _registry[category] = new Dictionary<string, string>();

                ScanFolder(category, folder);
            }

            // Scan POLYGON Dungeon pack folders into categories
            foreach (var (category, folder) in _extraScanFolders)
            {
                if (!_registry.ContainsKey(category))
                    _registry[category] = new Dictionary<string, string>();

                ScanFolder(category, folder);
            }

            // Aliases — map game IDs to POLYGON prefab names and legacy models
            AddAlias("prop", "pillar", "column_1");

            // Props: map simple game IDs → POLYGON prefabs
            AddAlias("prop", "barrel",         "sm_prop_barrel_01");
            AddAlias("prop", "barrel_broken",  "sm_prop_barrel_broken_01");
            AddAlias("prop", "crate",          "sm_prop_crate_metal_01");
            AddAlias("prop", "crate_long",     "sm_prop_crate_metal_03");
            AddAlias("prop", "chest",          "sm_prop_chest_01");
            AddAlias("prop", "weapon_rack",    "sm_prop_weaponrack_01");
            AddAlias("prop", "torch",          "sm_prop_torchstick_01");
            AddAlias("prop", "statue",         "sm_env_statue_01");
            AddAlias("prop", "pedestal",       "sm_prop_stonechair_01");
            AddAlias("prop", "shelf_tall",     "sm_prop_bookcase_01");
            AddAlias("prop", "computer",       "sm_prop_tech_switchboard_01");
            AddAlias("prop", "computer_small", "sm_prop_tech_lever_01");
            AddAlias("prop", "pipes",          "sm_prop_tech_pipe_01");
            AddAlias("prop", "capsule",        "sm_prop_tech_chamber_01");
            AddAlias("prop", "pod",            "sm_prop_tech_chamber_01");
            AddAlias("prop", "vessel",         "sm_prop_vase_01");
            AddAlias("prop", "vessel_short",   "sm_prop_vase_02");
            AddAlias("prop", "vessel_tall",    "sm_prop_vase_04");
            AddAlias("prop", "laser",          "sm_prop_tech_crystal_01");
            AddAlias("prop", "portal",         "sm_prop_tech_engine_01");
            AddAlias("prop", "teleporter",     "sm_prop_tech_turbine_01");

            // Walls: map game wall IDs → POLYGON walls
            AddAlias("wall", "wall_1", "sm_env_wall_01");
            AddAlias("wall", "wall_2", "sm_env_wall_02");
            AddAlias("wall", "wall_3", "sm_env_wall_03");
            AddAlias("wall", "wall_4", "sm_env_wall_04");
            AddAlias("wall", "wall_5", "sm_env_wall_05");

            // Floors: map game floor IDs → POLYGON tiles
            AddAlias("floor", "floortile_basic",  "sm_env_tiles_01");
            AddAlias("floor", "floortile_basic2", "sm_env_tiles_02");

            // Doors: map game door IDs → POLYGON doors
            AddAlias("door", "door_frame",  "sm_env_door_frame_01");
            AddAlias("door", "door_double", "sm_env_doordouble_flat_01");

            // Pillars: shorthand aliases
            AddAlias("pillar", "column_1", "sm_env_pillar_square_01");
            AddAlias("pillar", "column_2", "sm_env_pillar_round_01");
            AddAlias("pillar", "column_3", "sm_env_pillar_round_02");

            // Weapons: map WeaponType names → FBX model IDs
            AddAlias("weapon", "pistol",     "hand_cannon");
            AddAlias("weapon", "rifle",      "assault_rifle");
            AddAlias("weapon", "shotgun",    "shotgun");
            AddAlias("weapon", "launcher",   "rocket_launcher");
            AddAlias("weapon", "repeater",   "gatling_gun");
            // Melee/AoE weapon models
            AddAlias("weapon", "blade_ring", "energy_sword");
            AddAlias("weapon", "flail_chain","war_hammer");
            AddAlias("weapon", "shock_coil", "arc_rifle");
            AddAlias("weapon", "flame_thrower", "power_rifle");

            int total = 0;
            foreach (var cat in _registry.Values)
                total += cat.Count;

            if (total > 0)
                GD.Print($"[ModelLibrary] Initialized: {total} models registered across {_registry.Count} categories");
            else
                GD.Print("[ModelLibrary] Initialized: no model assets found (using procedural fallback)");
        }

        private static void ScanFolder(string category, string folderPath)
        {
            if (!DirAccess.DirExistsAbsolute(folderPath)) return;

            using var dir = DirAccess.Open(folderPath);
            if (dir == null) return;

            dir.ListDirBegin();
            string fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (dir.CurrentIsDir() && fileName != "." && fileName != "..")
                {
                    // Recurse into subfolders
                    ScanFolder(category, folderPath + "/" + fileName);
                }
                else
                {
                    string lower = fileName.ToLower();
                    // Godot imports .glb as .glb.import, but the resource path is still .glb
                    if (lower.EndsWith(".glb") || lower.EndsWith(".tscn") || lower.EndsWith(".scn") || lower.EndsWith(".fbx"))
                    {
                        string id = System.IO.Path.GetFileNameWithoutExtension(fileName).ToLower()
                            .Replace(" ", "_").Replace("-", "_");
                        string resPath = folderPath + "/" + fileName;
                        _registry[category][id] = resPath;
                    }
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }

        /// <summary>
        /// Try to load and instantiate a model by category and id.
        /// Returns the instantiated Node3D, or null if not found (caller should fall back to procedural).
        /// </summary>
        public static Node3D TryLoad(string category, string id)
        {
            if (!_initialized) Initialize();

            id = id?.ToLower().Replace(" ", "_").Replace("-", "_");
            if (string.IsNullOrEmpty(id)) return null;

            if (!_registry.TryGetValue(category, out var entries)) return null;
            if (!entries.TryGetValue(id, out var resPath)) return null;

            // Check cache (skip paths that previously failed to load)
            if (_failedPaths.Contains(resPath)) return null;
            if (!_cache.TryGetValue(resPath, out var scene))
            {
                if (!ResourceLoader.Exists(resPath))
                {
                    _failedPaths.Add(resPath);
                    return null;
                }
                scene = GD.Load<PackedScene>(resPath);
                if (scene == null)
                {
                    _failedPaths.Add(resPath);
                    GD.PrintErr($"[ModelLibrary] Failed to load {category}/{id} from {resPath} — using procedural fallback");
                    return null;
                }
                _cache[resPath] = scene;
                GD.Print($"[ModelLibrary] Loaded {category}/{id} from {resPath}");
            }

            var instance = scene.Instantiate<Node3D>();
            return instance;
        }

        private static void AddAlias(string category, string alias, string targetId)
        {
            if (!_registry.TryGetValue(category, out var entries)) return;
            if (!entries.ContainsKey(targetId)) return;
            entries[alias] = entries[targetId];
        }

        /// <summary>
        /// Get all model IDs in a category (for random selection).
        /// </summary>
        public static string[] GetCategoryIds(string category)
        {
            if (!_initialized) Initialize();
            if (!_registry.TryGetValue(category, out var entries)) return System.Array.Empty<string>();
            var ids = new string[entries.Count];
            entries.Keys.CopyTo(ids, 0);
            return ids;
        }

        /// <summary>
        /// Check if a model exists for the given category and id without loading it.
        /// </summary>
        public static bool HasModel(string category, string id)
        {
            if (!_initialized) Initialize();

            id = id?.ToLower().Replace(" ", "_").Replace("-", "_");
            if (string.IsNullOrEmpty(id)) return false;

            return _registry.TryGetValue(category, out var entries) && entries.ContainsKey(id);
        }
    }
}
