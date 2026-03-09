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

        // Category → subfolder mapping
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

            // Aliases — alternative IDs that map to the same model
            AddAlias("prop", "pillar", "column_1");

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
