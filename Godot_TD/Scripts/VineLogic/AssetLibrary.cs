using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Central registry for loading 3D model assets. Handles GLB/FBX loading,
    /// material overrides, and scale normalization.
    /// All models are loaded as PackedScene and cached.
    /// </summary>
    public static class AssetLibrary
    {
        private static readonly Dictionary<string, PackedScene> _cache = new();

        // ── Asset paths ──

        // AXIS
        public const string AXIS_REPEATER = "res://Models/AXIS/KB3D_FTW_PropRepeater_A_grp.glb";
        public const string AXIS_POWER_MAST = "res://Models/AXIS/KB3D_FTW_PropPwerMast_A_grp.glb";
        public const string AXIS_EYE_DRONE = "res://Models/AXIS/eye_drone.fbx";

        // Buildings
        public const string BLDG_CHECKPOINT = "res://Models/Buildings/KB3D_FTW_BldgSmCheckPoint_A_grp.glb";
        public const string BLDG_BARRACKS = "res://Models/Buildings/KB3D_FTW_BldgSmFieldBarracks_A_grp.glb";
        public const string BLDG_FUEL_TANKS = "res://Models/Buildings/KB3D_FTW_BldgSmFuelTanks_A_grp.glb";
        public const string BLDG_OUTPOST = "res://Models/Buildings/KB3D_FTW_BldgSmOutpost_A_grp.glb";
        public const string BLDG_TRENCH = "res://Models/Buildings/KB3D_FTW_BldgSmTrench_A_grp.glb";
        public const string BLDG_WATER_TOWERS = "res://Models/Buildings/KB3D_FTW_BldgSmWaterTowers_A_grp.glb";

        // Turrets
        public const string TURRET_A = "res://Models/Turrets/KB3D_FTW_PropTurret_A_grp.glb";
        public const string TURRET_B = "res://Models/Turrets/KB3D_FTW_PropTurret_B_grp.glb";
        public const string TURRET_C = "res://Models/Turrets/KB3D_FTW_PropTurret_C_grp.glb";
        public const string WEAPON_A = "res://Models/Turrets/KB3D_FTW_PropWeapon_A_grp.glb";
        public const string WEAPON_B = "res://Models/Turrets/KB3D_FTW_PropWeapon_B_grp.glb";
        public const string ROCKET_LAUNCHER = "res://Models/Turrets/KB3D_FTW_HeroPropMultiRocketLauncher_A_grp.glb";
        public const string PLASMA_GUN = "res://Models/Turrets/KB3D_FTW_HeroPropPlasmaGun_A_grp.glb";

        // Props
        public const string PROP_BARRIER_A = "res://Models/Props/KB3D_FTW_PropBarrier_A_grp.glb";
        public const string PROP_BARRIER_B = "res://Models/Props/KB3D_FTW_PropBarrier_B_grp.glb";
        public const string PROP_CONTAINER_A = "res://Models/Props/KB3D_FTW_PropContainer_A_grp.glb";
        public const string PROP_CONTAINER_B = "res://Models/Props/KB3D_FTW_PropContainer_B_grp.glb";
        public const string PROP_CRATE_A = "res://Models/Props/KB3D_FTW_PropCrate_A_grp.glb";
        public const string PROP_CRATE_B = "res://Models/Props/KB3D_FTW_PropCrate_B_grp.glb";
        public const string PROP_BARREL = "res://Models/Props/KB3D_FTW_PropBarrel_A_grp.glb";
        public const string PROP_BARRELS = "res://Models/Props/KB3D_FTW_PropBarrels_A_grp.glb";
        public const string PROP_GENERATOR_A = "res://Models/Props/KB3D_FTW_PropGenerator_A_grp.glb";
        public const string PROP_GENERATOR_B = "res://Models/Props/KB3D_FTW_PropGenerator_B_grp.glb";
        public const string PROP_RADAR = "res://Models/Props/KB3D_FTW_PropRadar_A_grp.glb";
        public const string PROP_SATELLITE = "res://Models/Props/KB3D_FTW_PropSatellite_A_grp.glb";
        public const string PROP_ANTENNA_A = "res://Models/Props/KB3D_FTW_PropAntenna_A_grp.glb";
        public const string PROP_ANTENNA_B = "res://Models/Props/KB3D_FTW_PropAntenna_B_grp.glb";
        public const string PROP_LAMP_A = "res://Models/Props/KB3D_FTW_PropLampPost_A_grp.glb";
        public const string PROP_LAMP_B = "res://Models/Props/KB3D_FTW_PropLampPost_B_grp.glb";
        public const string PROP_SANDBAGS = "res://Models/Props/KB3D_FTW_PropSandbags_A_grp.glb";
        public const string PROP_HEDGEHOG = "res://Models/Props/KB3D_FTW_PropAntiTankHedgehog_A_grp.glb";
        public const string PROP_FENCE = "res://Models/Props/KB3D_FTW_PropFencePost_A_grp.glb";

        // Enemies
        public const string ENEMY_SCRAP_RAT = "res://Models/Characters/Enemies/scrap_rat.fbx";
        public const string ENEMY_WIRE_WORM = "res://Models/Characters/Enemies/wire_worm.fbx";
        public const string ENEMY_TRILOBITE = "res://Models/Characters/Enemies/trilobite.fbx";
        public const string ENEMY_QUAD_SHELL = "res://Models/Characters/Enemies/quad_shell.fbx";
        public const string ENEMY_SPARK_DRONE = "res://Models/Characters/Enemies/spark_drone.fbx";
        public const string ENEMY_DECOY = "res://Models/Characters/Enemies/decoy_unit.fbx";

        // Player
        public const string PLAYER_CLUNKER = "res://Models/Characters/Player/clunker.fbx";
        public const string PLAYER_RUSTBUCKET = "res://Models/Characters/Player/rustbucket.fbx";
        public const string PLAYER_SPARKPLUG = "res://Models/Characters/Player/sparkplug.fbx";

        // Grouped lists for convenience
        public static readonly string[] AllBuildings = {
            BLDG_CHECKPOINT, BLDG_BARRACKS, BLDG_FUEL_TANKS,
            BLDG_OUTPOST, BLDG_TRENCH, BLDG_WATER_TOWERS
        };

        public static readonly string[] AllTurrets = {
            TURRET_A, TURRET_B, TURRET_C, WEAPON_A, WEAPON_B
        };

        public static readonly string[] SmallProps = {
            PROP_BARRIER_A, PROP_BARRIER_B, PROP_CRATE_A, PROP_CRATE_B,
            PROP_BARREL, PROP_BARRELS, PROP_SANDBAGS, PROP_HEDGEHOG,
            PROP_FENCE, PROP_LAMP_A, PROP_LAMP_B
        };

        public static readonly string[] LargeProps = {
            PROP_CONTAINER_A, PROP_CONTAINER_B, PROP_GENERATOR_A,
            PROP_GENERATOR_B, PROP_RADAR, PROP_SATELLITE,
            PROP_ANTENNA_A, PROP_ANTENNA_B
        };

        // ── Loading ──

        /// <summary>
        /// Load and instantiate a model. Returns null if not found.
        /// Caches the PackedScene for reuse.
        /// </summary>
        public static Node3D Instantiate(string path)
        {
            if (!ResourceLoader.Exists(path))
            {
                GD.PushWarning($"[AssetLibrary] Asset not found: {path}");
                return null;
            }

            if (!_cache.TryGetValue(path, out var scene))
            {
                scene = GD.Load<PackedScene>(path);
                if (scene == null)
                {
                    GD.PushWarning($"[AssetLibrary] Failed to load: {path}");
                    return null;
                }
                _cache[path] = scene;
            }

            return scene.Instantiate<Node3D>();
        }

        /// <summary>
        /// Load, instantiate, and apply a material override to all mesh children.
        /// </summary>
        public static Node3D InstantiateWithMaterial(string path, StandardMaterial3D material)
        {
            var instance = Instantiate(path);
            if (instance == null) return null;

            ApplyMaterialRecursive(instance, material);
            return instance;
        }

        /// <summary>
        /// Load, instantiate, scale to fit a target size, and return.
        /// </summary>
        public static Node3D InstantiateScaled(string path, float targetSize)
        {
            var instance = Instantiate(path);
            if (instance == null) return null;

            // Defer AABB calculation since node needs to be in tree first
            instance.SetMeta("target_size", targetSize);
            instance.SetMeta("needs_scale", true);
            return instance;
        }

        /// <summary>
        /// Call after adding the node to the scene tree to normalize its scale.
        /// </summary>
        public static void FinalizeScale(Node3D instance)
        {
            if (instance == null || !instance.HasMeta("needs_scale")) return;
            float targetSize = (float)instance.GetMeta("target_size");
            instance.RemoveMeta("needs_scale");
            instance.RemoveMeta("target_size");

            var aabb = GetCombinedAABB(instance);
            if (aabb.Size.Length() < 0.001f) return;

            float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
            if (maxDim > 0)
            {
                float scale = targetSize / maxDim;
                instance.Scale = Vector3.One * scale;
            }
        }

        /// <summary>
        /// Apply a material to all MeshInstance3D children recursively.
        /// </summary>
        public static void ApplyMaterialRecursive(Node node, StandardMaterial3D material)
        {
            if (node is MeshInstance3D mesh)
                mesh.MaterialOverride = material;

            foreach (var child in node.GetChildren())
                ApplyMaterialRecursive(child, material);
        }

        /// <summary>
        /// Create a rusty metallic material.
        /// </summary>
        public static StandardMaterial3D MakeRustyMetal(Color tint)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = tint;
            mat.Roughness = 0.85f;
            mat.Metallic = 0.6f;
            return mat;
        }

        /// <summary>
        /// Create a dark silhouette material (flat black, unshaded).
        /// </summary>
        public static StandardMaterial3D MakeSilhouette()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.02f, 0.01f, 0.03f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            return mat;
        }

        /// <summary>
        /// Create a glowing emissive material.
        /// </summary>
        public static StandardMaterial3D MakeEmissive(Color color, float intensity = 2f)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = intensity;
            return mat;
        }

        /// <summary>
        /// Get combined AABB of all mesh children.
        /// </summary>
        public static Aabb GetCombinedAABB(Node3D root)
        {
            var result = new Aabb();
            bool first = true;
            CollectAABB(root, ref result, ref first);
            return result;
        }

        private static void CollectAABB(Node node, ref Aabb result, ref bool first)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                var aabb = mesh.GetAabb();
                if (first) { result = aabb; first = false; }
                else result = result.Merge(aabb);
            }
            foreach (var child in node.GetChildren())
                CollectAABB(child, ref result, ref first);
        }

        /// <summary>
        /// Verify all registered assets exist. Returns count of missing.
        /// Call from editor or debug to check asset integrity.
        /// </summary>
        public static int VerifyAll()
        {
            int missing = 0;
            var allPaths = new List<string> {
                AXIS_REPEATER, AXIS_POWER_MAST, AXIS_EYE_DRONE,
                ENEMY_SCRAP_RAT, ENEMY_WIRE_WORM, ENEMY_TRILOBITE,
                ENEMY_QUAD_SHELL, ENEMY_SPARK_DRONE, ENEMY_DECOY,
                PLAYER_CLUNKER, PLAYER_RUSTBUCKET, PLAYER_SPARKPLUG
            };
            allPaths.AddRange(AllBuildings);
            allPaths.AddRange(AllTurrets);
            allPaths.AddRange(SmallProps);
            allPaths.AddRange(LargeProps);

            foreach (var path in allPaths)
            {
                if (!ResourceLoader.Exists(path))
                {
                    GD.PrintErr($"[AssetLibrary] MISSING: {path}");
                    missing++;
                }
                else
                {
                    GD.Print($"[AssetLibrary] OK: {path}");
                }
            }

            GD.Print($"[AssetLibrary] Verification complete: {allPaths.Count - missing}/{allPaths.Count} found, {missing} missing");
            return missing;
        }
    }
}
