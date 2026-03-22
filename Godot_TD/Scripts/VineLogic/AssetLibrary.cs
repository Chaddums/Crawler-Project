using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        // Grunt Mech — Robots_Grunt.FBX from InvisGun Hero pack (file kept as decoy_unit.fbx)
        public const string ENEMY_GRUNT_MECH = "res://Models/Characters/Enemies/decoy_unit.fbx";

        // Player
        public const string PLAYER_CLUNKER = "res://Models/Characters/Player/clunker.fbx";
        public const string PLAYER_RUSTBUCKET = "res://Models/Characters/Player/rustbucket.fbx";
        public const string PLAYER_SPARKPLUG = "res://Models/Characters/Player/sparkplug.fbx";
        public const string PLAYER_GUN_ROBOT = "res://Models/Characters/Player/gun_robot.fbx";

        // Companions
        public const string COMPANION_BIT = "res://Models/Characters/Companions/LilRobot.fbx";

        // ── Model texture mapping ──
        // FBX files that don't embed textures — we bind them manually after load
        private static readonly Dictionary<string, string> _playerTextures = new() {
            { PLAYER_CLUNKER, "res://Models/Characters/Player/Textures/George_Texture.png" },
            { PLAYER_RUSTBUCKET, "res://Models/Characters/Player/Textures/Leela_Texture.png" },
            { PLAYER_SPARKPLUG, "res://Models/Characters/Player/Textures/Mike_Texture.png" },
            { PLAYER_GUN_ROBOT, "res://Models/Characters/Player/Textures/Robot1.png" },
            { COMPANION_BIT, "res://Models/Characters/Companions/textures/LilRobot.png" },
            { ENEMY_SCRAP_RAT, "res://Models/Characters/Enemies/scrap_rat_BaseColor.png" },
            { ENEMY_WIRE_WORM, "res://Models/Characters/Enemies/wire_worm_Texture.png" },
            { ENEMY_GRUNT_MECH, "res://Models/Characters/Enemies/Textures/GRUNT_red.png" },
        };

        /// <summary>
        /// Apply the correct texture to an FBX model that doesn't embed textures.
        /// Call after instantiation. Safe to call on models not in the dict (no-op).
        /// </summary>
        public static void ApplyPlayerTexture(Node3D model, string modelPath)
        {
            if (!_playerTextures.TryGetValue(modelPath, out var texPath)) return;
            var texture = GD.Load<Texture2D>(texPath);
            if (texture == null)
            {
                GD.PrintErr($"[AssetLibrary] Failed to load texture: {texPath}");
                return;
            }

            // Check for companion eye texture (LilRobot has separate eyes mesh)
            string eyeTexPath = null;
            Texture2D eyeTexture = null;
            if (modelPath == COMPANION_BIT)
            {
                eyeTexPath = "res://Models/Characters/Companions/textures/LilRobotEyes.png";
                eyeTexture = GD.Load<Texture2D>(eyeTexPath);
            }

            int applied = 0;
            var meshes = model.FindChildren("*", "MeshInstance3D", true, false);
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;

                // Use eye texture for eye meshes, body texture for everything else
                bool isEyeMesh = mesh.Name.ToString().ToLower().Contains("eye");
                var texToApply = (isEyeMesh && eyeTexture != null) ? eyeTexture : texture;

                var mat = new StandardMaterial3D();
                mat.AlbedoTexture = texToApply;
                mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
                mesh.MaterialOverride = mat;
                applied++;
            }
            GD.Print($"[AssetLibrary] Applied texture to {applied} meshes on '{modelPath}'");
        }

        // ── AABB-based target heights for character models ──
        private static readonly Dictionary<string, float> _targetHeights = new() {
            { ENEMY_SCRAP_RAT, Constants.ENEMY_HEIGHT_STANDARD },
            { ENEMY_WIRE_WORM, Constants.ENEMY_HEIGHT_STANDARD },
            { ENEMY_TRILOBITE, Constants.ENEMY_HEIGHT_STANDARD },
            { ENEMY_QUAD_SHELL, Constants.ENEMY_HEIGHT_LARGE },
            { ENEMY_SPARK_DRONE, Constants.ENEMY_HEIGHT_SMALL },
            { ENEMY_GRUNT_MECH, Constants.ENEMY_HEIGHT_STANDARD },
            { PLAYER_CLUNKER, Constants.PLAYER_HEIGHT },
            { PLAYER_RUSTBUCKET, Constants.PLAYER_HEIGHT },
            { PLAYER_SPARKPLUG, Constants.PLAYER_HEIGHT },
            { PLAYER_GUN_ROBOT, Constants.PLAYER_HEIGHT },
            { COMPANION_BIT, Constants.PLAYER_HEIGHT },
        };

        // ── Normalized scale factors ──
        // Target: 1 unit ≈ 1 meter in game. These correct for FBX cm exports
        // and oversized KitBash models so everything loads at a usable size.
        private static readonly Dictionary<string, float> _scaleOverrides = new() {
            { AXIS_EYE_DRONE, 0.5f },

            // KitBash buildings — massive, scale down to ~10-15 units wide
            { BLDG_CHECKPOINT, 0.15f },
            { BLDG_BARRACKS, 0.15f },
            { BLDG_FUEL_TANKS, 0.15f },
            { BLDG_OUTPOST, 0.15f },
            { BLDG_TRENCH, 0.15f },
            { BLDG_WATER_TOWERS, 0.15f },

            // KitBash turrets — scale to ~3-4 units
            { TURRET_A, 0.3f },
            { TURRET_B, 0.3f },
            { TURRET_C, 0.3f },
            { WEAPON_A, 0.3f },
            { WEAPON_B, 0.3f },
            { ROCKET_LAUNCHER, 0.08f },
            { PLASMA_GUN, 0.06f },

            // AXIS structures
            { AXIS_REPEATER, 0.5f },
            { AXIS_POWER_MAST, 0.5f },

            // Props — small (native ~1-2 units, scale to ~2 units for game)
            { PROP_BARREL, 1.5f },
            { PROP_BARRELS, 1.5f },
            { PROP_CRATE_A, 2f },
            { PROP_CRATE_B, 2f },
            { PROP_SANDBAGS, 1.5f },
            { PROP_HEDGEHOG, 1.5f },
            { PROP_FENCE, 1.5f },
            { PROP_LAMP_A, 1.5f },
            { PROP_LAMP_B, 1.5f },

            // Props — medium (barriers ~3-5 units native)
            { PROP_BARRIER_A, 1f },
            { PROP_BARRIER_B, 1f },

            // Props — large (containers, generators, radar ~5-15 units native)
            { PROP_CONTAINER_A, 0.5f },
            { PROP_CONTAINER_B, 0.5f },
            { PROP_GENERATOR_A, 0.5f },
            { PROP_GENERATOR_B, 0.5f },
            { PROP_RADAR, 0.4f },
            { PROP_SATELLITE, 0.4f },
            { PROP_ANTENNA_A, 0.5f },
            { PROP_ANTENNA_B, 0.5f },
        };

        /// <summary>
        /// Get the normalization scale for an asset. Returns 1.0 if no override.
        /// </summary>
        public static float GetNormalizedScale(string path)
        {
            return _scaleOverrides.TryGetValue(path, out var scale) ? scale : 1f;
        }

        /// <summary>
        /// Load, instantiate, and apply normalized scale so the model is game-ready.
        /// Character models use AABB-based height targeting; others use manual scale overrides.
        /// </summary>
        public static Node3D InstantiateNormalized(string path)
        {
            if (_targetHeights.TryGetValue(path, out var targetH))
                return InstantiateToHeight(path, targetH);

            var instance = Instantiate(path);
            if (instance == null) return null;
            float scale = GetNormalizedScale(path);
            instance.Scale = Vector3.One * scale;
            return instance;
        }

        /// <summary>
        /// Instantiate a model and scale it so its AABB height matches the target.
        /// </summary>
        public static Node3D InstantiateToHeight(string path, float targetHeight)
        {
            var instance = Instantiate(path);
            if (instance == null) return null;

            var aabb = GetCombinedAABB(instance);
            float nativeHeight = aabb.Size.Y;
            if (nativeHeight < 0.001f)
            {
                instance.Scale = Vector3.One;
                return instance;
            }

            float scale = targetHeight / nativeHeight;
            instance.Scale = Vector3.One * scale;
            GD.Print($"[AssetLibrary] {path}: native AABB height={nativeHeight:F2}, target={targetHeight:F1}, scale={scale:F4}");
            return instance;
        }

        /// <summary>
        /// Read back the uniform scale factor on a model (for outline width compensation).
        /// </summary>
        public static float GetModelScale(Node3D model)
        {
            return model?.Scale.X ?? 1f;
        }

        /// <summary>
        /// Load, normalize scale, and ground the model (bottom of AABB sits at y=0).
        /// Use this for placing models in the world or preview.
        /// Must be called AFTER adding to scene tree (AABB needs global transforms).
        /// </summary>
        public static void GroundModel(Node3D model)
        {
            if (model == null) return;
            var aabb = GetCombinedAABB(model);
            // Offset so bottom of AABB is at y=0
            float bottomY = aabb.Position.Y * model.Scale.Y;
            model.Position = new Vector3(model.Position.X, -bottomY, model.Position.Z);
        }

        /// <summary>
        /// Center the model horizontally and ground it vertically.
        /// Good for preview displays.
        /// </summary>
        public static void CenterAndGround(Node3D model)
        {
            if (model == null) return;
            var aabb = GetCombinedAABB(model);
            var scale = model.Scale;
            // Center XZ, ground Y
            float centerX = (aabb.Position.X + aabb.Size.X / 2f) * scale.X;
            float centerZ = (aabb.Position.Z + aabb.Size.Z / 2f) * scale.Z;
            float bottomY = aabb.Position.Y * scale.Y;
            model.Position = new Vector3(-centerX, -bottomY, -centerZ);
        }

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

            var instance = scene.Instantiate<Node3D>();
            ApplyModelFixups(path, instance);
            return instance;
        }

        /// <summary>
        /// Load a fresh uncached copy of a scene. Used by the editor to get untouched
        /// animation data that hasn't been modified by in-game splitting.
        /// </summary>
        public static Node3D InstantiateUncached(string path)
        {
            if (!ResourceLoader.Exists(path))
            {
                GD.PushWarning($"[AssetLibrary] Asset not found: {path}");
                return null;
            }

            var scene = ResourceLoader.Load<PackedScene>(path, cacheMode: ResourceLoader.CacheMode.Ignore);
            if (scene == null)
            {
                GD.PushWarning($"[AssetLibrary] Failed to load (uncached): {path}");
                return null;
            }

            var instance = scene.Instantiate<Node3D>();
            ApplyModelFixups(path, instance);
            return instance;
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
        /// Get combined AABB of all mesh children, accounting for internal transforms.
        /// This correctly handles FBX models with root_scale and skeleton hierarchies
        /// by transforming each mesh's local AABB through the chain of parent transforms
        /// up to (but not including) the root node.
        /// </summary>
        public static Aabb GetCombinedAABB(Node3D root)
        {
            var result = new Aabb();
            bool first = true;
            CollectTransformedAABB(root, root, ref result, ref first);
            return result;
        }

        private static void CollectTransformedAABB(Node3D root, Node node, ref Aabb result, ref bool first)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                var localAabb = mesh.GetAabb();

                // Compute transform from this mesh to the root (excluding root's own transform)
                var relativeTransform = ComputeRelativeTransform(root, mesh);
                var transformedAabb = TransformAabb(localAabb, relativeTransform);

                if (first) { result = transformedAabb; first = false; }
                else result = result.Merge(transformedAabb);
            }
            foreach (var child in node.GetChildren())
                CollectTransformedAABB(root, child, ref result, ref first);
        }

        /// <summary>
        /// Walk from a descendant node up to the root, accumulating transforms.
        /// Returns the transform that converts points in the node's local space
        /// to the root's local space (excluding root's own transform).
        /// </summary>
        private static Transform3D ComputeRelativeTransform(Node3D root, Node3D node)
        {
            var chain = new List<Transform3D>();
            var current = node;
            while (current != null && current != root)
            {
                chain.Add(current.Transform);
                current = current.GetParentOrNull<Node3D>();
            }

            // Apply in reverse order: root-child transform first, then down to node
            var result = Transform3D.Identity;
            for (int i = chain.Count - 1; i >= 0; i--)
                result *= chain[i];
            return result;
        }

        /// <summary>
        /// Transform an AABB through a Transform3D by transforming all 8 corners
        /// and building a new axis-aligned bounding box.
        /// </summary>
        private static Aabb TransformAabb(Aabb aabb, Transform3D transform)
        {
            var min = aabb.Position;
            var max = aabb.Position + aabb.Size;

            var p0 = transform * new Vector3(min.X, min.Y, min.Z);
            var rMin = p0;
            var rMax = p0;

            void Expand(Vector3 p) {
                rMin = new Vector3(Mathf.Min(rMin.X, p.X), Mathf.Min(rMin.Y, p.Y), Mathf.Min(rMin.Z, p.Z));
                rMax = new Vector3(Mathf.Max(rMax.X, p.X), Mathf.Max(rMax.Y, p.Y), Mathf.Max(rMax.Z, p.Z));
            }

            Expand(transform * new Vector3(max.X, min.Y, min.Z));
            Expand(transform * new Vector3(min.X, max.Y, min.Z));
            Expand(transform * new Vector3(max.X, max.Y, min.Z));
            Expand(transform * new Vector3(min.X, min.Y, max.Z));
            Expand(transform * new Vector3(max.X, min.Y, max.Z));
            Expand(transform * new Vector3(min.X, max.Y, max.Z));
            Expand(transform * new Vector3(max.X, max.Y, max.Z));

            return new Aabb(rMin, rMax - rMin);
        }

        // ── Model fixups for broken FBX files ──

        private static void ApplyModelFixups(string path, Node3D instance)
        {
            if (instance == null) return;
            if (path == ENEMY_GRUNT_MECH)
                FixGruntMechTracks(instance);
        }

        /// <summary>
        /// Robots_Grunt.FBX diagnostic — tracks are symmetric in the FBX data.
        /// The hierarchy has a -90 X rotation on totalControl (Z-up to Y-up).
        /// root_scale=1.0 import setting is the actual fix (was 100, caused distortion).
        /// </summary>
        private static void FixGruntMechTracks(Node3D root)
        {
            // Grunt Mech hierarchy (Z-up FBX, totalControl has rot(-90,0,0) for Y-up):
            //   totalControl
            //     leftControl  pos=(1.052, -0.080, 0.520) scale=1.938
            //       Object001 (left track mesh)
            //         Leg L_GRUNT (left leg mesh)
            //     rightControl pos=(-1.056, -0.080, 0.520) scale=1.938
            //       LegR_GRUNT (right track mesh)
            //         chain1 (right leg mesh)
            //     topControl   pos=(0.005, -0.080, 2.681) (body)
            //
            // The right track visually sits wrong. Previous attempts moved rightControl
            // but the mesh child offsets compensated. Moving the actual mesh node directly.
            //
            // totalControl rot(-90,0,0) means: local X = world X, local Y = world -Z, local Z = world Y
            // So "up" in world = +local Z, "over" in world = +/-local X

            Node3D legR = null;
            FindNode(root, "LegR_GRUNT", ref legR);

            if (legR != null)
            {
                var old = legR.Position;
                // Nudge: up = +Z in local space, over = +X
                legR.Position = new Vector3(old.X, old.Y, old.Z + 0.12f);
                GD.Print($"[GruntMech] LegR_GRUNT: {old} → {legR.Position}");
            }
            else
            {
                GD.PrintErr("[GruntMech] Could not find LegR_GRUNT node");
            }
        }

        private static void FindNode(Node root, string name, ref Node3D result)
        {
            if (result != null) return;
            if (root.Name == name && root is Node3D n) { result = n; return; }
            foreach (var child in root.GetChildren())
                FindNode(child, name, ref result);
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
                ENEMY_QUAD_SHELL, ENEMY_SPARK_DRONE, ENEMY_GRUNT_MECH,
                PLAYER_CLUNKER, PLAYER_RUSTBUCKET, PLAYER_SPARKPLUG,
                COMPANION_BIT
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

        // ── BVT accessors (read-only for automated verification) ──

        /// <summary>
        /// Get all public const string fields whose values start with "res://".
        /// Returns Dictionary of fieldName → path. Self-maintaining via reflection.
        /// </summary>
        public static Dictionary<string, string> GetAllConstantsByName()
        {
            return typeof(AssetLibrary)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (f.Name, Value: (string)f.GetRawConstantValue()))
                .Where(p => p.Value.StartsWith("res://"))
                .ToDictionary(p => p.Name, p => p.Value);
        }

        /// <summary>
        /// Get all constant paths as a flat list.
        /// </summary>
        public static List<string> GetAllConstantPaths()
        {
            return GetAllConstantsByName().Values.ToList();
        }

        public static IReadOnlyDictionary<string, string> GetPlayerTextureBindings() => _playerTextures;
        public static IReadOnlyDictionary<string, float> GetTargetHeights() => _targetHeights;
        public static IReadOnlyDictionary<string, float> GetScaleOverrides() => _scaleOverrides;
    }
}
