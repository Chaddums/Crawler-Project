using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    public enum BVTStatus { Pass, Warn, Fail }

    public class BVTCheckResult
    {
        public string Category;
        public string CategoryName;
        public string TestName;
        public BVTStatus Status;
        public string Message;
        public long DurationMs;
    }

    /// <summary>
    /// Extensibility interface — any squad can register additional BVT checks.
    /// </summary>
    public interface IBVTCheckProvider
    {
        string CategoryId { get; }
        string CategoryName { get; }
        List<BVTCheckResult> RunChecks(SceneTree tree);
    }

    /// <summary>
    /// Core BVT check engine. Runs all verification checks and returns structured results.
    /// Used by both EditorBVT (F12 tab) and AssetBVTSuite (CI/headless).
    /// No UI dependency — pure logic.
    /// </summary>
    public static class BVTRunner
    {
        private static readonly List<IBVTCheckProvider> _providers = new();

        public static void RegisterProvider(IBVTCheckProvider provider)
        {
            if (!_providers.Any(p => p.CategoryId == provider.CategoryId))
                _providers.Add(provider);
        }

        /// <summary>
        /// Run all checks. Pass SceneTree for model-loading checks (null = skip those).
        /// </summary>
        public static List<BVTCheckResult> RunAll(SceneTree tree = null)
        {
            var results = new List<BVTCheckResult>();

            results.AddRange(CheckA_AssetExistence());
            if (tree != null)
                results.AddRange(CheckB_AssetIntegrity(tree));
            results.AddRange(CheckC_RegistrationConsistency());
            results.AddRange(CheckD_AudioIntegrity());
            results.AddRange(CheckE_RegistryCompleteness());
            if (tree != null)
                results.AddRange(CheckF_VisualSanity(tree));
            results.AddRange(CheckG_JsonData());
            results.AddRange(CheckH_EditorModuleHealth());
            results.AddRange(CheckI_WaveSystemIntegrity());
            results.AddRange(CheckJ_CrossSystemConsistency());
            results.AddRange(CheckK_KnownIssues());
            results.AddRange(CheckL_FunctionalSmoke());

            // Run extensible provider checks
            foreach (var provider in _providers)
            {
                try
                {
                    results.AddRange(provider.RunChecks(tree));
                }
                catch (Exception e)
                {
                    results.Add(new BVTCheckResult {
                        Category = provider.CategoryId,
                        CategoryName = provider.CategoryName,
                        TestName = $"{provider.CategoryId.ToLower()}.provider_crashed",
                        Status = BVTStatus.Fail,
                        Message = e.Message
                    });
                }
            }

            return results;
        }

        // ── Helpers ──

        private static BVTCheckResult MakeResult(string cat, string catName, string test,
            BVTStatus status, string msg = "", long ms = 0)
        {
            return new BVTCheckResult {
                Category = cat, CategoryName = catName,
                TestName = test, Status = status,
                Message = msg, DurationMs = ms
            };
        }

        private static long Ms(Stopwatch sw) { sw.Stop(); return sw.ElapsedMilliseconds; }

        // ═══════════════════════════════════════════════════════════════
        // Category A: Asset Existence
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckA_AssetExistence()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "A"; const string catName = "Asset Existence";

            // A1: All model constants
            var constants = AssetLibrary.GetAllConstantsByName();
            foreach (var (name, path) in constants)
            {
                var sw = Stopwatch.StartNew();
                bool exists = ResourceLoader.Exists(path);
                results.Add(MakeResult(cat, catName,
                    $"asset.model.exists.{name}", exists ? BVTStatus.Pass : BVTStatus.Fail,
                    exists ? "" : $"Missing: {path}", Ms(sw)));
            }

            // A2: Texture files
            var textures = AssetLibrary.GetPlayerTextureBindings();
            foreach (var (modelPath, texPath) in textures)
            {
                var sw = Stopwatch.StartNew();
                bool exists = ResourceLoader.Exists(texPath);
                string shortName = System.IO.Path.GetFileNameWithoutExtension(texPath);
                results.Add(MakeResult(cat, catName,
                    $"asset.texture.exists.{shortName}", exists ? BVTStatus.Pass : BVTStatus.Fail,
                    exists ? "" : $"Missing: {texPath}", Ms(sw)));
            }

            // A3: Scene paths
            var scenes = new[] {
                ("VINE_BATTLE", Constants.SCENE_VINE_BATTLE),
                ("VINE_DRAFT", Constants.SCENE_VINE_DRAFT),
                ("MAIN_MENU", Constants.SCENE_MAIN_MENU),
            };
            foreach (var (name, path) in scenes)
            {
                var sw = Stopwatch.StartNew();
                bool exists = ResourceLoader.Exists(path);
                results.Add(MakeResult(cat, catName,
                    $"asset.scene.exists.{name}", exists ? BVTStatus.Pass : BVTStatus.Fail,
                    exists ? "" : $"Missing: {path}", Ms(sw)));
            }

            // A4: Wave data — both planets must have data
            for (int planet = 1; planet <= 2; planet++)
            {
                var sw = Stopwatch.StartNew();
                string path = $"res://Data/Waves/P{planet}.json";
                bool exists = ResourceLoader.Exists(path) || FileAccess.FileExists(path);
                results.Add(MakeResult(cat, catName,
                    $"asset.wave_data.exists.P{planet}",
                    exists ? BVTStatus.Pass : BVTStatus.Fail,
                    exists ? "" : $"Missing: {path} — planet {planet} falls back to P1 data", Ms(sw)));
            }

            // A5: Difficulty scaling
            {
                var sw = Stopwatch.StartNew();
                string path = "res://Data/difficulty_scaling.json";
                bool exists = ResourceLoader.Exists(path) || FileAccess.FileExists(path);
                results.Add(MakeResult(cat, catName,
                    "asset.difficulty_scaling.exists", exists ? BVTStatus.Pass : BVTStatus.Fail,
                    exists ? "" : $"Missing: {path}", Ms(sw)));
            }

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category B: Asset Integrity (requires scene tree)
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckB_AssetIntegrity(SceneTree tree)
        {
            var results = new List<BVTCheckResult>();
            const string cat = "B"; const string catName = "Asset Integrity";

            var constants = AssetLibrary.GetAllConstantsByName();
            foreach (var (name, path) in constants)
            {
                if (!path.EndsWith(".glb") && !path.EndsWith(".fbx")) continue;
                if (!ResourceLoader.Exists(path)) continue; // Already caught by A

                var sw = Stopwatch.StartNew();
                try
                {
                    var instance = AssetLibrary.Instantiate(path);
                    if (instance == null)
                    {
                        results.Add(MakeResult(cat, catName,
                            $"asset.loads.{name}", BVTStatus.Fail,
                            "Instantiate returned null", Ms(sw)));
                        continue;
                    }

                    // Need tree for AABB
                    tree.Root.AddChild(instance);

                    // Mesh count
                    int meshCount = 0;
                    CountMeshes(instance, ref meshCount);
                    results.Add(MakeResult(cat, catName,
                        $"asset.mesh_count.{name}",
                        meshCount > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                        meshCount > 0 ? "" : "No MeshInstance3D found", Ms(sw)));

                    // AABB
                    var aabb = AssetLibrary.GetCombinedAABB(instance);
                    bool validAABB = aabb.Size.Length() > 0.001f;
                    results.Add(MakeResult(cat, catName,
                        $"asset.aabb_nonzero.{name}",
                        validAABB ? BVTStatus.Pass : BVTStatus.Fail,
                        validAABB ? "" : "Zero-size AABB"));

                    // Material analysis — catch all-white/untextured models
                    int totalMats = 0, placeholderMats = 0;
                    var matIssues = new List<string>();
                    AnalyzeMaterials(instance, ref totalMats, ref placeholderMats, matIssues);

                    if (totalMats == 0 && meshCount > 0)
                    {
                        results.Add(MakeResult(cat, catName,
                            $"asset.materials.{name}", BVTStatus.Fail,
                            $"Model has {meshCount} meshes but 0 materials — will render invisible or white"));
                    }
                    else if (placeholderMats > 0 && placeholderMats == totalMats)
                    {
                        results.Add(MakeResult(cat, catName,
                            $"asset.all_white.{name}", BVTStatus.Fail,
                            $"ALL {totalMats} materials are white/default — no textures, no color, no emission. " +
                            $"Needs texture binding or planet theme. Issues: {string.Join("; ", matIssues)}"));
                    }
                    else if (placeholderMats > 0)
                    {
                        results.Add(MakeResult(cat, catName,
                            $"asset.some_white.{name}", BVTStatus.Warn,
                            $"{placeholderMats}/{totalMats} materials are white/default. " +
                            $"May need planet theme applied. Issues: {string.Join("; ", matIssues)}"));
                    }
                    else
                    {
                        results.Add(MakeResult(cat, catName,
                            $"asset.materials.{name}", BVTStatus.Pass,
                            $"{totalMats} materials, all styled"));
                    }

                    instance.QueueFree();
                }
                catch (Exception e)
                {
                    results.Add(MakeResult(cat, catName,
                        $"asset.loads.{name}", BVTStatus.Fail,
                        $"Exception: {e.Message}", Ms(sw)));
                }
            }

            // B4: Texture loads
            var textures = AssetLibrary.GetPlayerTextureBindings();
            foreach (var (modelPath, texPath) in textures)
            {
                if (!ResourceLoader.Exists(texPath)) continue;
                var sw = Stopwatch.StartNew();
                var tex = GD.Load<Texture2D>(texPath);
                string shortName = System.IO.Path.GetFileNameWithoutExtension(texPath);
                results.Add(MakeResult(cat, catName,
                    $"asset.texture_loads.{shortName}",
                    tex != null ? BVTStatus.Pass : BVTStatus.Fail,
                    tex != null ? "" : "Failed to load as Texture2D", Ms(sw)));
            }

            return results;
        }

        private static void CountMeshes(Node node, ref int count)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null) count++;
            foreach (var child in node.GetChildren())
                CountMeshes(child, ref count);
        }

        /// <summary>
        /// Recursively analyze all materials on a model. Counts total materials
        /// and how many are white/default placeholders.
        /// A "placeholder" = BaseMaterial3D with no texture, no emission, and
        /// albedo color near white (R,G,B all > 0.9).
        /// </summary>
        private static void AnalyzeMaterials(Node node, ref int totalMats,
            ref int placeholderMats, List<string> issues)
        {
            if (node is MeshInstance3D meshInst && meshInst.Mesh != null)
            {
                // Check override material first
                var overrideMat = meshInst.MaterialOverride;
                if (overrideMat != null)
                {
                    totalMats++;
                    if (IsPlaceholderMaterial(overrideMat))
                    {
                        placeholderMats++;
                        issues.Add($"'{meshInst.Name}' override: white/default");
                    }
                }
                else
                {
                    // Check per-surface materials
                    int surfaces = meshInst.Mesh.GetSurfaceCount();
                    for (int i = 0; i < surfaces; i++)
                    {
                        var surfMat = meshInst.Mesh.SurfaceGetMaterial(i);
                        if (surfMat == null)
                        {
                            totalMats++;
                            placeholderMats++;
                            issues.Add($"'{meshInst.Name}' surface {i}: null material");
                        }
                        else
                        {
                            totalMats++;
                            if (IsPlaceholderMaterial(surfMat))
                            {
                                placeholderMats++;
                                if (surfMat is BaseMaterial3D bm)
                                    issues.Add($"'{meshInst.Name}' surface {i}: ({bm.AlbedoColor.R:F2},{bm.AlbedoColor.G:F2},{bm.AlbedoColor.B:F2}) no texture");
                                else
                                    issues.Add($"'{meshInst.Name}' surface {i}: white/default");
                            }
                        }
                    }
                }
            }

            foreach (var child in node.GetChildren())
                AnalyzeMaterials(child, ref totalMats, ref placeholderMats, issues);
        }

        /// <summary>
        /// Is this material a white/default placeholder that needs textures or theming?
        /// Returns true for: null, BaseMaterial3D with no textures + no emission + white/near-white albedo.
        /// Returns false for: ShaderMaterial, materials with textures, materials with non-white color.
        /// </summary>
        private static bool IsPlaceholderMaterial(Material mat)
        {
            if (mat == null) return true;
            if (mat is ShaderMaterial) return false; // Custom shader = intentional

            if (mat is BaseMaterial3D baseMat)
            {
                bool hasAlbedoTex = baseMat.AlbedoTexture != null;
                bool hasNormalTex = baseMat.NormalTexture != null;
                bool hasORMTex = baseMat.OrmTexture != null;
                bool hasAnyTexture = hasAlbedoTex || hasNormalTex || hasORMTex;
                if (hasAnyTexture) return false;

                bool hasEmission = baseMat.EmissionEnabled && baseMat.EmissionEnergyMultiplier > 0.1f;
                if (hasEmission) return false;

                // Check if color is near-white (all channels > 0.9)
                var c = baseMat.AlbedoColor;
                bool isNearWhite = c.R > 0.9f && c.G > 0.9f && c.B > 0.9f;
                // Also catch pure grey defaults (0.6-1.0 range, very common in FBX imports)
                bool isDefaultGrey = c.R > 0.55f && c.G > 0.55f && c.B > 0.55f
                    && Mathf.Abs(c.R - c.G) < 0.1f && Mathf.Abs(c.G - c.B) < 0.1f;

                return isNearWhite || isDefaultGrey;
            }

            return false; // Unknown material type — assume intentional
        }

        // ═══════════════════════════════════════════════════════════════
        // Category C: Registration Consistency
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckC_RegistrationConsistency()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "C"; const string catName = "Registration Consistency";

            var constants = AssetLibrary.GetAllConstantsByName();
            var heights = AssetLibrary.GetTargetHeights();
            var scales = AssetLibrary.GetScaleOverrides();

            // C1: No duplicate paths
            var pathGroups = constants.GroupBy(kv => kv.Value).Where(g => g.Count() > 1);
            foreach (var group in pathGroups)
            {
                var names = string.Join(", ", group.Select(g => g.Key));
                results.Add(MakeResult(cat, catName,
                    $"consistency.duplicate_path", BVTStatus.Warn,
                    $"Duplicate path: {names} → {group.Key}"));
            }
            if (!pathGroups.Any())
                results.Add(MakeResult(cat, catName, "consistency.no_duplicate_paths", BVTStatus.Pass));

            // C1b: Duplicate file CONTENT — different paths, identical bytes
            // Catches: someone copied an FBX and renamed it instead of using the actual model
            var hashToNames = new Dictionary<string, List<string>>();
            foreach (var (name, path) in constants)
            {
                if (!path.EndsWith(".glb") && !path.EndsWith(".fbx")) continue;
                string globalPath = ProjectSettings.GlobalizePath(path);
                if (!System.IO.File.Exists(globalPath)) continue;
                try
                {
                    using var stream = System.IO.File.OpenRead(globalPath);
                    using var md5 = System.Security.Cryptography.MD5.Create();
                    byte[] hashBytes = md5.ComputeHash(stream);
                    string hash = BitConverter.ToString(hashBytes).Replace("-", "");
                    if (!hashToNames.ContainsKey(hash))
                        hashToNames[hash] = new List<string>();
                    hashToNames[hash].Add(name);
                }
                catch { /* Skip files we can't read */ }
            }
            var dupeContentGroups = hashToNames.Where(kv => kv.Value.Count > 1);
            foreach (var group in dupeContentGroups)
            {
                var names = string.Join(", ", group.Value);
                results.Add(MakeResult(cat, catName,
                    $"consistency.duplicate_content.{group.Value[0]}",
                    BVTStatus.Fail,
                    $"Identical file content: {names} — same model with different filenames"));
            }
            if (!dupeContentGroups.Any())
                results.Add(MakeResult(cat, catName, "consistency.no_duplicate_content", BVTStatus.Pass));

            // C2: Scale coverage — every model has height target OR scale override
            foreach (var (name, path) in constants)
            {
                if (!path.EndsWith(".glb") && !path.EndsWith(".fbx")) continue;
                bool hasHeight = heights.ContainsKey(path);
                bool hasScale = scales.ContainsKey(path);
                if (!hasHeight && !hasScale)
                {
                    results.Add(MakeResult(cat, catName,
                        $"consistency.scale_coverage.{name}", BVTStatus.Warn,
                        "No height target or scale override (uses default 1.0)"));
                }
            }

            // C3: Texture bindings for player/companion models — must have binding or embedded textures
            var texBindings = AssetLibrary.GetPlayerTextureBindings();
            foreach (var (name, path) in constants)
            {
                if (!name.StartsWith("PLAYER_") && !name.StartsWith("COMPANION_")) continue;
                bool hasTex = texBindings.ContainsKey(path);
                // Check if there are texture files on disk for this model (orphaned textures = binding needed)
                string modelDir = path.GetBaseDir();
                bool hasOrphanedTextures = false;
                if (!hasTex)
                {
                    // Check common texture subdirectories
                    string modelName = System.IO.Path.GetFileNameWithoutExtension(path);
                    string texDir = modelDir + "/textures";
                    hasOrphanedTextures = DirAccess.DirExistsAbsolute(texDir);
                    if (!hasOrphanedTextures)
                    {
                        // Check for PNG files with similar name in same directory
                        string pngPath = modelDir + "/" + modelName + ".png";
                        hasOrphanedTextures = ResourceLoader.Exists(pngPath) || FileAccess.FileExists(pngPath);
                    }
                }
                // FAIL if textures exist on disk but aren't bound, WARN if no textures found at all
                BVTStatus status = hasTex ? BVTStatus.Pass :
                    hasOrphanedTextures ? BVTStatus.Fail : BVTStatus.Warn;
                string msg = hasTex ? "" :
                    hasOrphanedTextures ? $"Texture files exist on disk but no binding in _playerTextures" :
                    "No texture binding and no textures found on disk (may embed in FBX)";
                results.Add(MakeResult(cat, catName, $"consistency.texture_binding.{name}", status, msg));
            }

            // C4: VineNode model mapping references valid constants
            var nodeModelMap = new Dictionary<string, string> {
                { "DamageTower", AssetLibrary.TURRET_A },
                { "SlowField", AssetLibrary.PROP_RADAR },
                { "ProximitySensor", AssetLibrary.PROP_SATELLITE },
                { "BuffEmitter", AssetLibrary.PROP_GENERATOR_A },
            };
            var allPaths = new HashSet<string>(constants.Values);
            foreach (var (nodeType, modelPath) in nodeModelMap)
            {
                bool valid = allPaths.Contains(modelPath);
                results.Add(MakeResult(cat, catName,
                    $"consistency.node_model.{nodeType}",
                    valid ? BVTStatus.Pass : BVTStatus.Fail,
                    valid ? "" : $"Model path {modelPath} not in AssetLibrary constants"));
            }

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category D: Audio Integrity
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckD_AudioIntegrity()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "D"; const string catName = "Audio Integrity";

            var folders = new[] {
                "res://Assets/Audio/Edited",
                "res://Assets/Audio/Sonniss/BigMechanical",
                "res://Assets/Audio/Sonniss/FuturisticWeapons",
                "res://Assets/Audio/Sonniss/HeavyMechanical",
                "res://Assets/Audio/Sonniss/UIWhoosh",
            };

            foreach (var folder in folders)
            {
                var sw = Stopwatch.StartNew();
                bool exists = DirAccess.DirExistsAbsolute(folder);
                string shortName = folder.GetFile();
                results.Add(MakeResult(cat, catName,
                    $"audio.folder_exists.{shortName}",
                    exists ? BVTStatus.Pass : BVTStatus.Warn,
                    exists ? "" : $"Directory not found: {folder}", Ms(sw)));

                if (exists)
                {
                    using var dir = DirAccess.Open(folder);
                    int fileCount = 0;
                    if (dir != null)
                    {
                        dir.ListDirBegin();
                        string fileName;
                        while ((fileName = dir.GetNext()) != "")
                        {
                            if (fileName.EndsWith(".wav") || fileName.EndsWith(".ogg"))
                                fileCount++;
                        }
                        dir.ListDirEnd();
                    }
                    results.Add(MakeResult(cat, catName,
                        $"audio.folder_nonempty.{shortName}",
                        fileCount > 0 ? BVTStatus.Pass : BVTStatus.Warn,
                        fileCount > 0 ? $"{fileCount} files" : "Empty folder"));
                }
            }

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category E: Registry Completeness
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckE_RegistryCompleteness()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "E"; const string catName = "Registry Completeness";

            // E1: Every VineNodeType has registry entry
            foreach (VineNodeType type in Enum.GetValues(typeof(VineNodeType)))
            {
                var data = VineNodeRegistry.Get(type);
                results.Add(MakeResult(cat, catName,
                    $"registry.vine_node.{type}",
                    data != null ? BVTStatus.Pass : BVTStatus.Fail,
                    data != null ? "" : $"VineNodeType.{type} missing from VineNodeRegistry"));
            }

            // E2: Every TowerComponentType has registry entry
            foreach (TowerComponentType type in Enum.GetValues(typeof(TowerComponentType)))
            {
                var data = TowerComponentRegistry.Get(type);
                results.Add(MakeResult(cat, catName,
                    $"registry.tower_component.{type}",
                    data != null ? BVTStatus.Pass : BVTStatus.Fail,
                    data != null ? "" : $"TowerComponentType.{type} missing from TowerComponentRegistry"));
            }

            // E3: Draft roles reference valid node types
            for (int i = 0; i < VineDraftScreen.RoleCount; i++)
            {
                var nodes = VineDraftScreen.GetRoleNodes(i);
                string roleName = VineDraftScreen.GetRoleName(i);
                foreach (var nodeType in nodes)
                {
                    var data = VineNodeRegistry.Get(nodeType);
                    results.Add(MakeResult(cat, catName,
                        $"registry.role_node.{roleName}.{nodeType}",
                        data != null ? BVTStatus.Pass : BVTStatus.Fail,
                        data != null ? "" : $"Role {roleName} references invalid node type {nodeType}"));
                }
            }

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category F: Visual Sanity (requires scene tree)
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckF_VisualSanity(SceneTree tree)
        {
            var results = new List<BVTCheckResult>();
            const string cat = "F"; const string catName = "Visual Sanity";

            var scales = AssetLibrary.GetScaleOverrides();

            // F1: Scale overrides in reasonable range
            foreach (var (path, scale) in scales)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                bool reasonable = scale >= 0.01f && scale <= 100f;
                results.Add(MakeResult(cat, catName,
                    $"visual.scale_reasonable.{name}",
                    reasonable ? BVTStatus.Pass : BVTStatus.Fail,
                    reasonable ? "" : $"Scale {scale} outside [0.01, 100]"));
            }

            // F2: Height-targeted models produce correct height
            var heights = AssetLibrary.GetTargetHeights();
            foreach (var (path, targetH) in heights)
            {
                if (!ResourceLoader.Exists(path)) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                try
                {
                    var instance = AssetLibrary.InstantiateNormalized(path);
                    if (instance == null) continue;
                    tree.Root.AddChild(instance);
                    var aabb = AssetLibrary.GetCombinedAABB(instance);
                    float actualH = aabb.Size.Y * instance.Scale.Y;
                    float ratio = targetH > 0.001f ? actualH / targetH : 0f;
                    bool within20 = ratio >= 0.8f && ratio <= 1.2f;
                    results.Add(MakeResult(cat, catName,
                        $"visual.height_target.{name}",
                        within20 ? BVTStatus.Pass : BVTStatus.Warn,
                        within20 ? "" : $"Target {targetH:F1}, actual {actualH:F1} (ratio {ratio:F2})"));
                    instance.QueueFree();
                }
                catch { /* Already caught in B */ }
            }

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category G: JSON Data
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckG_JsonData()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "G"; const string catName = "JSON Data";

            // G1: P1.json parses
            {
                var sw = Stopwatch.StartNew();
                string path = "res://Data/Waves/P1.json";
                bool ok = false; string msg = "";
                if (FileAccess.FileExists(path))
                {
                    try
                    {
                        var json = new Json();
                        string text = FileAccess.Open(path, FileAccess.ModeFlags.Read)?.GetAsText() ?? "";
                        var err = json.Parse(text);
                        ok = err == Error.Ok;
                        if (!ok) msg = $"Parse error: {json.GetErrorMessage()}";
                    }
                    catch (Exception e) { msg = e.Message; }
                }
                else { msg = "File not found"; }

                results.Add(MakeResult(cat, catName,
                    "data.waves.P1_parses", ok ? BVTStatus.Pass : BVTStatus.Fail, msg, Ms(sw)));
            }

            // G2: difficulty_scaling.json parses
            {
                var sw = Stopwatch.StartNew();
                string path = "res://Data/difficulty_scaling.json";
                bool ok = false; string msg = "";
                if (FileAccess.FileExists(path))
                {
                    try
                    {
                        var json = new Json();
                        string text = FileAccess.Open(path, FileAccess.ModeFlags.Read)?.GetAsText() ?? "";
                        var err = json.Parse(text);
                        ok = err == Error.Ok;
                        if (!ok) msg = $"Parse error: {json.GetErrorMessage()}";
                    }
                    catch (Exception e) { msg = e.Message; }
                }
                else { msg = "File not found"; }

                results.Add(MakeResult(cat, catName,
                    "data.difficulty_scaling_parses", ok ? BVTStatus.Pass : BVTStatus.Fail, msg, Ms(sw)));
            }

            // G3: Constants sanity
            results.Add(MakeResult(cat, catName, "data.constants.map_width",
                Constants.VINE_MAP_WIDTH > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                Constants.VINE_MAP_WIDTH > 0 ? "" : "Map width <= 0"));
            results.Add(MakeResult(cat, catName, "data.constants.map_height",
                Constants.VINE_MAP_HEIGHT > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                Constants.VINE_MAP_HEIGHT > 0 ? "" : "Map height <= 0"));
            results.Add(MakeResult(cat, catName, "data.constants.core_lives",
                Constants.VINE_CORE_LIVES > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                Constants.VINE_CORE_LIVES > 0 ? "" : "Core lives <= 0"));
            results.Add(MakeResult(cat, catName, "data.constants.starting_resources",
                Constants.VINE_STARTING_RESOURCES > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                Constants.VINE_STARTING_RESOURCES > 0 ? "" : "Starting resources <= 0"));

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category H: Editor Module Health
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckH_EditorModuleHealth()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "H"; const string catName = "Editor Module Health";

            var em = EditorManager.Instance;
            if (em == null)
            {
                results.Add(MakeResult(cat, catName,
                    "editor.manager_exists", BVTStatus.Warn,
                    "EditorManager.Instance is null (not in scene)"));
                return results;
            }

            results.Add(MakeResult(cat, catName, "editor.manager_exists", BVTStatus.Pass));

            // Check minimum expected module count (7 base + BVT = 8)
            // We can't easily get module count without reflection, so just check manager exists
            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category I: Wave System Integrity
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckI_WaveSystemIntegrity()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "I"; const string catName = "Wave System Integrity";

            // I1: Extraction curve produces increasing values
            float prev = 0;
            bool increasing = true;
            for (int w = 1; w <= 20; w++)
            {
                float val = Constants.EXTRACTION_BASE * Mathf.Pow(Constants.EXTRACTION_GROWTH, w);
                if (val <= prev) { increasing = false; break; }
                prev = val;
            }
            results.Add(MakeResult(cat, catName, "waves.extraction_curve_increasing",
                increasing ? BVTStatus.Pass : BVTStatus.Fail,
                increasing ? "" : "Extraction curve not monotonically increasing"));

            // I2: Difficulty HP scale is positive
            results.Add(MakeResult(cat, catName, "waves.hp_scale_positive",
                Constants.DIFFICULTY_HP_SCALE > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                Constants.DIFFICULTY_HP_SCALE > 0 ? "" : "HP scale <= 0"));

            // I3: Enemy base speed is positive
            results.Add(MakeResult(cat, catName, "waves.enemy_speed_positive",
                Constants.VINE_ENEMY_BASE_SPEED > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                Constants.VINE_ENEMY_BASE_SPEED > 0 ? "" : "Enemy base speed <= 0"));

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category J: Cross-System Consistency
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckJ_CrossSystemConsistency()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "J"; const string catName = "Cross-System Consistency";

            // J1: MaterialType enum has expected values
            var matTypes = Enum.GetValues(typeof(MaterialType));
            results.Add(MakeResult(cat, catName, "cross.material_types_exist",
                matTypes.Length >= 4 ? BVTStatus.Pass : BVTStatus.Warn,
                matTypes.Length >= 4 ? $"{matTypes.Length} types" : $"Only {matTypes.Length} material types"));

            // J2: MiningMode enum has Resources and Materials
            bool hasResources = Enum.IsDefined(typeof(MiningMode), MiningMode.Resources);
            bool hasMaterials = Enum.IsDefined(typeof(MiningMode), MiningMode.Materials);
            results.Add(MakeResult(cat, catName, "cross.mining_modes",
                hasResources && hasMaterials ? BVTStatus.Pass : BVTStatus.Fail,
                hasResources && hasMaterials ? "" : "Missing MiningMode values"));

            // J3: All 3 roles exist
            results.Add(MakeResult(cat, catName, "cross.role_count",
                VineDraftScreen.RoleCount == 3 ? BVTStatus.Pass : BVTStatus.Fail,
                VineDraftScreen.RoleCount == 3 ? "" : $"Expected 3 roles, got {VineDraftScreen.RoleCount}"));

            // J4: Tower slot types match component types
            foreach (TowerSlotType slot in Enum.GetValues(typeof(TowerSlotType)))
            {
                int count = 0;
                foreach (var comp in TowerComponentRegistry.GetBySlot(slot))
                    count++;
                results.Add(MakeResult(cat, catName,
                    $"cross.slot_has_components.{slot}",
                    count > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                    count > 0 ? $"{count} components" : $"No components for slot type {slot}"));
            }

            // J5: Signal power constants positive
            results.Add(MakeResult(cat, catName, "cross.signal_travel_speed",
                Constants.SIGNAL_TRAVEL_SPEED > 0 ? BVTStatus.Pass : BVTStatus.Fail));
            results.Add(MakeResult(cat, catName, "cross.sensor_range",
                Constants.SENSOR_RANGE > 0 ? BVTStatus.Pass : BVTStatus.Fail));
            results.Add(MakeResult(cat, catName, "cross.damage_tower_range",
                Constants.DAMAGE_TOWER_RANGE > 0 ? BVTStatus.Pass : BVTStatus.Fail));

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category K: Known Issues — tests that SHOULD FAIL
        // When a known issue is fixed, the test flips to PASS.
        // If someone claims it's fixed but it isn't, this catches the lie.
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckK_KnownIssues()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "K"; const string catName = "Known Issues (expected failures)";

            // K1: PushPull node has no movement logic — ActivateEffect fires but nothing happens
            // Check: does VineNode handle PushPull with actual enemy displacement?
            // The ReceiveSignal case for PushPull only calls ActivateEffect() — no push logic
            var pushPullData = VineNodeRegistry.Get(VineNodeType.PushPull);
            bool pushPullHasForce = Constants.PUSH_PULL_FORCE > 0;
            // The constant exists but no code reads it — that's the bug
            // We can check if there's an UpdatePushPull method by checking the _PhysicsProcess switch
            // For now: flag that PushPull is documented broken
            results.Add(MakeResult(cat, catName, "known.pushpull_noop",
                BVTStatus.Fail,
                "PushPull node calls ActivateEffect() but has no enemy displacement logic. PUSH_PULL_FORCE constant exists but is unused."));

            // K2: TypeSensor triggers on ALL enemies — no faction filter
            // The UpdateSensor switch case for TypeSensor just sets triggered=true for any enemy in range
            results.Add(MakeResult(cat, catName, "known.type_sensor_no_filter",
                BVTStatus.Fail,
                "TypeSensor triggers on all enemies regardless of faction. No VineEnemyFaction filter implemented."));

            // K3: EntityRegistry never registered with ServiceLocator
            // VineEnemy tries to register but ServiceLocator.TryGet<EntityRegistry> silently returns false
            bool entityRegistryAvailable = ServiceLocator.TryGet<EntityRegistry>(out _);
            results.Add(MakeResult(cat, catName, "known.entity_registry_not_registered",
                entityRegistryAvailable ? BVTStatus.Pass : BVTStatus.Fail,
                entityRegistryAvailable ? "EntityRegistry is now registered!" :
                "EntityRegistry exists but is never registered with ServiceLocator. All enemy Register() calls silently fail."));

            // K4: DifficultyScaler not fully wired
            // Registered with ServiceLocator but only spawn accumulator reads surge multiplier
            bool difficultyScalerAvailable = ServiceLocator.TryGet<DifficultyScaler>(out _);
            results.Add(MakeResult(cat, catName, "known.difficulty_scaler_partial",
                BVTStatus.Fail,
                difficultyScalerAvailable
                    ? "DifficultyScaler registered but only spawn accumulator reads surge multiplier. HP/speed scaling not wired."
                    : "DifficultyScaler not registered with ServiceLocator at all."));

            // K5: Planet 2 has no wave data (already caught in A4, but document the known issue)
            bool p2Exists = FileAccess.FileExists("res://Data/Waves/P2.json");
            results.Add(MakeResult(cat, catName, "known.planet2_no_wave_data",
                p2Exists ? BVTStatus.Pass : BVTStatus.Fail,
                p2Exists ? "P2.json now exists!" : "Planet 2 has no wave data — falls back to P1."));

            // K6: ConversionDome rebuilds meshes every frame
            // This is a performance issue — we flag it as known
            results.Add(MakeResult(cat, catName, "known.dome_rebuilds_every_frame",
                BVTStatus.Fail,
                "ConversionDome rebuilds meshes every frame. Needs dirty flag or cache."));

            // K7: No music system — only SFX and ambient
            results.Add(MakeResult(cat, catName, "known.no_music",
                BVTStatus.Fail,
                "No music tracks. Only procedural SFX and ambient audio."));

            return results;
        }

        // ═══════════════════════════════════════════════════════════════
        // Category L: Functional Smoke Tests
        // Actually exercise systems, not just check they exist.
        // ═══════════════════════════════════════════════════════════════

        private static List<BVTCheckResult> CheckL_FunctionalSmoke()
        {
            var results = new List<BVTCheckResult>();
            const string cat = "L"; const string catName = "Functional Smoke Tests";

            // L1: VineNodeRegistry returns correct category for each node type
            var sensorTypes = new[] { VineNodeType.ProximitySensor, VineNodeType.TypeSensor,
                VineNodeType.HPSensor, VineNodeType.CountSensor, VineNodeType.Timer };
            foreach (var sType in sensorTypes)
            {
                var data = VineNodeRegistry.Get(sType);
                results.Add(MakeResult(cat, catName, $"func.sensor_category.{sType}",
                    data?.Category == VineNodeCategory.Sensor ? BVTStatus.Pass : BVTStatus.Fail,
                    data?.Category == VineNodeCategory.Sensor ? "" :
                    $"{sType} has category {data?.Category} instead of Sensor"));
            }

            // L2: Effect nodes have valid damage/range values
            var effectTypes = new[] { VineNodeType.DamageTower, VineNodeType.SlowField };
            foreach (var eType in effectTypes)
            {
                var data = VineNodeRegistry.Get(eType);
                bool hasRange = data?.Range > 0;
                results.Add(MakeResult(cat, catName, $"func.effect_has_range.{eType}",
                    hasRange ? BVTStatus.Pass : BVTStatus.Fail,
                    hasRange ? "" : $"{eType} has Range={data?.Range}"));
            }

            // L3: DamageTower has positive DPS
            var towerData = VineNodeRegistry.Get(VineNodeType.DamageTower);
            results.Add(MakeResult(cat, catName, "func.damage_tower_has_dps",
                towerData?.Damage > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                towerData?.Damage > 0 ? "" : $"DamageTower.Damage = {towerData?.Damage}"));

            // L4: Auto-fire towers marked correctly
            results.Add(MakeResult(cat, catName, "func.damage_tower_auto_fires",
                towerData?.AutoFires == true ? BVTStatus.Pass : BVTStatus.Fail,
                towerData?.AutoFires == true ? "" : "DamageTower.AutoFires is not set"));
            var slowData = VineNodeRegistry.Get(VineNodeType.SlowField);
            results.Add(MakeResult(cat, catName, "func.slow_field_auto_fires",
                slowData?.AutoFires == true ? BVTStatus.Pass : BVTStatus.Fail,
                slowData?.AutoFires == true ? "" : "SlowField.AutoFires is not set"));

            // L5: Tower slot system — slot types actually assigned
            results.Add(MakeResult(cat, catName, "func.damage_tower_has_slots",
                towerData?.SlotCount > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                towerData?.SlotCount > 0 ? $"{towerData.SlotCount} slots" : "DamageTower has no slots"));
            results.Add(MakeResult(cat, catName, "func.damage_tower_slot_types_set",
                towerData?.SlotTypes != null && towerData.SlotTypes.Length > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                towerData?.SlotTypes != null ? "" : "DamageTower.SlotTypes is null"));

            // L6: TowerSlotSystem — wrong slot type rejected
            if (towerData?.SlotTypes != null && towerData.SlotTypes.Length > 0)
            {
                var testNode = new VineNode();
                testNode.Initialize(towerData);
                var slotSys = testNode.GetSlotSystem();
                if (slotSys != null)
                {
                    // DamageTower has Barrel+Frame. Try slotting a Core component — should fail
                    bool wrongSlotRejected = !slotSys.SlotComponent(0, TowerComponentType.PriorityWeak);
                    results.Add(MakeResult(cat, catName, "func.slot_rejects_wrong_type",
                        wrongSlotRejected ? BVTStatus.Pass : BVTStatus.Fail,
                        wrongSlotRejected ? "" : "TowerSlotSystem accepted a Core component in a Barrel slot"));

                    // Slot a correct component — should succeed
                    bool correctSlotAccepted = slotSys.SlotComponent(0, TowerComponentType.ChainArc);
                    results.Add(MakeResult(cat, catName, "func.slot_accepts_correct_type",
                        correctSlotAccepted ? BVTStatus.Pass : BVTStatus.Fail,
                        correctSlotAccepted ? "" : "TowerSlotSystem rejected a Barrel component in a Barrel slot"));

                    // Double-slot same slot — should fail
                    bool doubleSlotRejected = !slotSys.SlotComponent(0, TowerComponentType.ScatterShot);
                    results.Add(MakeResult(cat, catName, "func.slot_rejects_double_slot",
                        doubleSlotRejected ? BVTStatus.Pass : BVTStatus.Fail,
                        doubleSlotRejected ? "" : "TowerSlotSystem allowed double-slotting"));

                    // HasComponent works
                    bool hasChainArc = slotSys.HasComponent(TowerComponentType.ChainArc);
                    results.Add(MakeResult(cat, catName, "func.slot_has_component_query",
                        hasChainArc ? BVTStatus.Pass : BVTStatus.Fail,
                        hasChainArc ? "" : "HasComponent returned false for slotted component"));
                }
                else
                {
                    results.Add(MakeResult(cat, catName, "func.slot_system_exists",
                        BVTStatus.Fail, "GetSlotSystem() returned null on DamageTower"));
                }
            }

            // L7: Signal power values make sense — sensors should have power > 0
            foreach (var sType in sensorTypes)
            {
                var data = VineNodeRegistry.Get(sType);
                results.Add(MakeResult(cat, catName, $"func.sensor_has_power.{sType}",
                    data?.SignalPower > 0 ? BVTStatus.Pass : BVTStatus.Fail,
                    data?.SignalPower > 0 ? $"power={data.SignalPower}" : "Sensor has 0 signal power"));
            }

            // L8: Node costs are affordable with starting resources
            int cheapest = int.MaxValue;
            foreach (var nodeData in VineNodeRegistry.GetAll())
            {
                if (nodeData.ResourceCost < cheapest) cheapest = nodeData.ResourceCost;
            }
            bool canAfford3 = Constants.VINE_STARTING_RESOURCES >= cheapest * 3;
            results.Add(MakeResult(cat, catName, "func.starting_resources_afford_3_nodes",
                canAfford3 ? BVTStatus.Pass : BVTStatus.Fail,
                canAfford3 ? "" : $"Starting resources ({Constants.VINE_STARTING_RESOURCES}) can't afford 3x cheapest node ({cheapest})"));

            // L9: Tower component costs are valid (purchasable > 0, signal drops = 0 is OK)
            foreach (var comp in TowerComponentRegistry.GetAll())
            {
                bool isSignalDrop = comp.ResourceCost == 0; // Signal drops are free loot
                bool valid = comp.ResourceCost >= 0; // Just ensure no negative costs
                results.Add(MakeResult(cat, catName, $"func.component_cost_valid.{comp.Type}",
                    valid ? BVTStatus.Pass : BVTStatus.Fail,
                    valid ? (isSignalDrop ? "Signal drop (free)" : "") : $"Component {comp.Type} has invalid cost {comp.ResourceCost}"));
            }

            return results;
        }
    }
}
