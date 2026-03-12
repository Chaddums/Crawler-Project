using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Loads character_config.json (saved by the editor's Characters tab)
    /// and provides part overrides, color overrides, growth piece overrides,
    /// detail pieces, and fire point data at runtime.
    /// </summary>
    public static class CharacterConfigLoader
    {
        private const string CONFIG_PATH = "res://Data/character_config.json";

        private static Dictionary<string, object> _cache;
        private static bool _loaded;

        /// <summary>
        /// Force reload from disk. Called automatically on first access.
        /// </summary>
        public static void Reload()
        {
            _cache = null;
            _loaded = false;
            Load();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            if (!FileAccess.FileExists(CONFIG_PATH))
            {
                GD.PrintErr($"[CharacterConfigLoader] Config file not found: {CONFIG_PATH}");
                return;
            }
            using var file = FileAccess.Open(CONFIG_PATH, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr("[CharacterConfigLoader] Failed to open config file");
                return;
            }
            var text = file.GetAsText();
            if (string.IsNullOrWhiteSpace(text))
            {
                GD.PrintErr("[CharacterConfigLoader] Config file is empty");
                return;
            }
            _cache = MiniJson.Deserialize(text) as Dictionary<string, object>;
            GD.Print($"[CharacterConfigLoader] Loaded config with {_cache?.Count ?? 0} frames");
        }

        /// <summary>
        /// Apply saved part position/rotation/color overrides to a player body node tree.
        /// Also applies PartParents reparenting so positions are in the correct coordinate space.
        /// </summary>
        public static void ApplyPartOverrides(Node3D body, BotFrameType frame)
        {
            ApplyPartOverrides(body, frame, WeaponType.None);
        }

        /// <summary>
        /// Apply saved part overrides including weapon-specific overrides for the given weapon type.
        /// </summary>
        public static void ApplyPartOverrides(Node3D body, BotFrameType frame, WeaponType weapon)
        {
            Load();
            if (_cache == null) return;

            string key = frame.ToString();
            if (!_cache.TryGetValue(key, out var frameObj)) return;
            if (frameObj is not Dictionary<string, object> frameData) return;

            // Reparent parts first so position overrides apply in the correct coordinate space
            if (frameData.TryGetValue("PartParents", out var ppObj) && ppObj is Dictionary<string, object> partParents)
                ApplyPartParents(body, partParents);

            if (frameData.TryGetValue("Parts", out var partsObj) && partsObj is Dictionary<string, object> parts)
                ApplyRecursive(body, parts);

            // Apply weapon-specific part overrides
            if (weapon != WeaponType.None
                && frameData.TryGetValue("WeaponPartOverrides", out var wpObj)
                && wpObj is Dictionary<string, object> allWeaponParts
                && allWeaponParts.TryGetValue(weapon.ToString(), out var wpData)
                && wpData is Dictionary<string, object> weaponParts)
            {
                ApplyRecursive(body, weaponParts);
            }
        }

        private static void ApplyPartParents(Node3D body, Dictionary<string, object> partParents)
        {
            foreach (var kvp in partParents)
            {
                string partName = kvp.Key;
                string targetName = kvp.Value?.ToString() ?? "Body";

                var part = FindPartByName(body, partName);
                if (part == null) continue;

                Node3D target = (targetName == "Body" || targetName == "PlayerBody")
                    ? body
                    : FindPartByName(body, targetName);
                if (target == null) target = body;

                // Skip if already correct, or if reparenting to self/descendant
                if (part.GetParent() == target) continue;
                if (target == part) continue;
                if (IsDescendantOf(target, part)) continue;

                // Convert position to target-local space before reparenting
                Vector3 bodySpacePos = GetPositionRelativeTo(part, body);
                Vector3 targetInBodySpace = (target == body) ? Vector3.Zero
                    : GetPositionRelativeTo(target, body);
                Vector3 localPos = bodySpacePos - targetInBodySpace;

                var oldParent = part.GetParent();
                oldParent?.RemoveChild(part);
                target.AddChild(part);
                part.Position = localPos;
            }
        }

        private static Vector3 GetPositionRelativeTo(Node3D child, Node3D ancestor)
        {
            Vector3 pos = Vector3.Zero;
            Node3D current = child;
            while (current != null && current != ancestor)
            {
                pos += current.Position;
                current = current.GetParent() as Node3D;
            }
            return pos;
        }

        private static bool IsDescendantOf(Node potentialDescendant, Node potentialAncestor)
        {
            var current = potentialDescendant.GetParent();
            while (current != null)
            {
                if (current == potentialAncestor) return true;
                current = current.GetParent();
            }
            return false;
        }

        private static void ApplyRecursive(Node node, Dictionary<string, object> parts)
        {
            if (node is Node3D n3d)
            {
                string name = n3d.Name.ToString();
                if (parts.TryGetValue(name, out var partObj) && partObj is Dictionary<string, object> pd)
                {
                    if (pd.TryGetValue("PosX", out var px) && pd.TryGetValue("PosY", out var py) && pd.TryGetValue("PosZ", out var pz))
                        n3d.Position = new Vector3(Convert.ToSingle(px), Convert.ToSingle(py), Convert.ToSingle(pz));
                    if (pd.TryGetValue("RotX", out var rx) && pd.TryGetValue("RotY", out var ry) && pd.TryGetValue("RotZ", out var rz))
                        n3d.RotationDegrees = new Vector3(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz));

                    // Apply color override
                    if (pd.TryGetValue("ColorR", out var cr) && pd.TryGetValue("ColorG", out var cg) && pd.TryGetValue("ColorB", out var cb))
                    {
                        var color = new Color(Convert.ToSingle(cr), Convert.ToSingle(cg), Convert.ToSingle(cb));
                        ApplyColorToMesh(n3d, color);
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    ApplyRecursive(childNode, parts);
            }
        }

        /// <summary>
        /// Apply saved growth piece overrides (position, rotation, scale, color) to growth tier nodes.
        /// </summary>
        public static void ApplyGrowthOverrides(Node3D growthRoot, BotFrameType frame, GrowthTier tier)
        {
            Load();
            if (_cache == null || growthRoot == null) return;

            string key = frame.ToString();
            if (!_cache.TryGetValue(key, out var frameObj)) return;
            if (frameObj is not Dictionary<string, object> frameData) return;
            if (!frameData.TryGetValue("GrowthParts", out var gpObj)) return;
            if (gpObj is not Dictionary<string, object> growthParts) return;

            ApplyGrowthRecursive(growthRoot, growthParts, tier);
        }

        /// <summary>
        /// Load saved growth piece parent overrides and reparent growth pieces onto animated body pivots.
        /// Call this after building growth pieces and applying overrides, but before initializing the animator.
        /// </summary>
        public static void AttachGrowthPiecesToSkeleton(Node3D body, Node3D growthRoot, BotFrameType frame, GrowthTier tier)
        {
            Load();
            Dictionary<string, string> parentOverrides = null;

            if (_cache != null)
            {
                string key = frame.ToString();
                if (_cache.TryGetValue(key, out var frameObj)
                    && frameObj is Dictionary<string, object> frameData)
                {
                    string overrideKey = $"GrowthParents_{tier}";
                    if (frameData.TryGetValue(overrideKey, out var gpObj)
                        && gpObj is Dictionary<string, object> raw)
                    {
                        parentOverrides = new Dictionary<string, string>();
                        foreach (var kvp in raw)
                            parentOverrides[kvp.Key] = kvp.Value?.ToString() ?? "Body";
                    }
                }
            }

            CharacterMeshBuilder.AttachGrowthToSkeleton(body, growthRoot, parentOverrides);
        }

        private static void ApplyGrowthRecursive(Node node, Dictionary<string, object> growthParts, GrowthTier tier)
        {
            if (node is Node3D n3d)
            {
                string name = n3d.Name.ToString();
                if (name.StartsWith("_T1_") || name.StartsWith("_T2_") || name.StartsWith("_T3_") || name.StartsWith("_T4_")
                    || name.StartsWith("_G1_") || name.StartsWith("_G2_") || name.StartsWith("_G3_") || name.StartsWith("_G4_"))
                {
                    string gpKey = $"{tier}_{name}";
                    if (growthParts.TryGetValue(gpKey, out var gpObj) && gpObj is Dictionary<string, object> pd)
                    {
                        if (pd.TryGetValue("PosX", out var px) && pd.TryGetValue("PosY", out var py) && pd.TryGetValue("PosZ", out var pz))
                            n3d.Position = new Vector3(Convert.ToSingle(px), Convert.ToSingle(py), Convert.ToSingle(pz));
                        if (pd.TryGetValue("RotX", out var rx) && pd.TryGetValue("RotY", out var ry) && pd.TryGetValue("RotZ", out var rz))
                            n3d.RotationDegrees = new Vector3(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz));
                        if (pd.TryGetValue("ScaleX", out var sx))
                        {
                            float scaleX = Convert.ToSingle(sx);
                            if (pd.TryGetValue("ScaleY", out var sy) && pd.TryGetValue("ScaleZ", out var sz))
                                n3d.Scale = new Vector3(scaleX, Convert.ToSingle(sy), Convert.ToSingle(sz));
                            else
                                n3d.Scale = Vector3.One * scaleX;
                        }

                        if (pd.TryGetValue("ColorR", out var cr) && pd.TryGetValue("ColorG", out var cg) && pd.TryGetValue("ColorB", out var cb))
                        {
                            var color = new Color(Convert.ToSingle(cr), Convert.ToSingle(cg), Convert.ToSingle(cb));
                            ApplyColorToMesh(n3d, color);
                        }
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node cn)
                    ApplyGrowthRecursive(cn, growthParts, tier);
            }
        }

        /// <summary>
        /// Spawn saved detail pieces (bolts, rivets, plates, etc.) onto a player body.
        /// </summary>
        public static void SpawnDetailPieces(Node3D body, BotFrameType frame)
        {
            Load();
            if (_cache == null || body == null) return;

            string key = frame.ToString();
            if (!_cache.TryGetValue(key, out var frameObj)) return;
            if (frameObj is not Dictionary<string, object> frameData) return;
            if (!frameData.TryGetValue("Details", out var detailsObj)) return;
            if (detailsObj is not List<object> details) return;

            int counter = 0;
            foreach (var item in details)
            {
                if (item is not Dictionary<string, object> dd) continue;

                string type = dd.TryGetValue("Type", out var t) ? t.ToString() : "Box";
                string parentName = dd.TryGetValue("Parent", out var p) ? p.ToString() : "Root";

                var detail = CreateDetailMesh(type, counter++);
                if (detail == null) continue;

                if (dd.TryGetValue("PosX", out var px) && dd.TryGetValue("PosY", out var py) && dd.TryGetValue("PosZ", out var pz))
                    detail.Position = new Vector3(Convert.ToSingle(px), Convert.ToSingle(py), Convert.ToSingle(pz));
                if (dd.TryGetValue("RotX", out var rx) && dd.TryGetValue("RotY", out var ry) && dd.TryGetValue("RotZ", out var rz))
                    detail.RotationDegrees = new Vector3(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz));
                if (dd.TryGetValue("Scale", out var s))
                    detail.Scale = Vector3.One * Convert.ToSingle(s);

                if (dd.TryGetValue("ColorR", out var cr) && dd.TryGetValue("ColorG", out var cg) && dd.TryGetValue("ColorB", out var cb))
                {
                    var color = new Color(Convert.ToSingle(cr), Convert.ToSingle(cg), Convert.ToSingle(cb));
                    if (detail.MaterialOverride is StandardMaterial3D mat)
                        mat.AlbedoColor = color;
                }

                Node3D parent = body;
                if (parentName != "Root" && parentName != "PlayerBody")
                {
                    var found = FindPartByName(body, parentName);
                    if (found != null) parent = found;
                }

                parent.AddChild(detail);
            }
        }

        private static MeshInstance3D CreateDetailMesh(string type, int index)
        {
            var node = new MeshInstance3D();
            node.Name = $"_Detail_{type}_{index}";

            Color defaultColor = new Color(0.35f, 0.35f, 0.38f);
            Color boltColor = new Color(0.5f, 0.5f, 0.52f);

            Mesh mesh;
            Color color;
            switch (type)
            {
                case "Bolt":
                    mesh = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = 0.015f, RadialSegments = 6 };
                    color = boltColor;
                    break;
                case "Rivet":
                    mesh = new SphereMesh { Radius = 0.012f, Height = 0.024f, RadialSegments = 6, Rings = 3 };
                    color = boltColor;
                    break;
                case "PanelLine":
                    mesh = new BoxMesh { Size = new Vector3(0.15f, 0.004f, 0.004f) };
                    color = new Color(0.2f, 0.2f, 0.22f);
                    break;
                case "Scratch":
                    mesh = new BoxMesh { Size = new Vector3(0.1f, 0.002f, 0.002f) };
                    color = new Color(0.55f, 0.5f, 0.45f);
                    break;
                case "PipeStub":
                    mesh = new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.025f, Height = 0.06f, RadialSegments = 8 };
                    color = defaultColor;
                    break;
                case "Plate":
                    mesh = new BoxMesh { Size = new Vector3(0.08f, 0.008f, 0.06f) };
                    color = defaultColor;
                    break;
                case "Wire":
                    mesh = new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.005f, Height = 0.12f, RadialSegments = 4 };
                    color = new Color(0.15f, 0.15f, 0.18f);
                    break;
                case "Antenna":
                    mesh = new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.01f, Height = 0.15f, RadialSegments = 4 };
                    color = defaultColor;
                    break;
                case "Box":
                    mesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.06f) };
                    color = defaultColor;
                    break;
                case "Cylinder":
                    mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.06f, RadialSegments = 8 };
                    color = defaultColor;
                    break;
                case "Sphere":
                    mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 8, Rings = 4 };
                    color = defaultColor;
                    break;
                case "Vent":
                    mesh = new BoxMesh { Size = new Vector3(0.05f, 0.03f, 0.008f) };
                    color = new Color(0.18f, 0.18f, 0.2f);
                    break;
                default:
                    mesh = new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.04f) };
                    color = defaultColor;
                    break;
            }

            node.Mesh = mesh;
            var mat = new StandardMaterial3D { AlbedoColor = color };
            node.MaterialOverride = mat;
            return node;
        }

        private static void ApplyColorToMesh(Node3D node, Color color)
        {
            if (node is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D mat)
            {
                var cloned = (StandardMaterial3D)mat.Duplicate();
                cloned.AlbedoColor = color;
                mi.MaterialOverride = cloned;
                return;
            }

            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D childMat)
                {
                    var cloned = (StandardMaterial3D)childMat.Duplicate();
                    cloned.AlbedoColor = color;
                    mesh.MaterialOverride = cloned;
                    break;
                }
            }
        }

        private static Node3D FindPartByName(Node root, string name)
        {
            if (root is Node3D n3d && n3d.Name.ToString() == name) return n3d;
            foreach (var child in root.GetChildren())
            {
                if (child is Node cn)
                {
                    var found = FindPartByName(cn, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

        /// <summary>
        /// Get the fire point offset for a given frame.
        /// Returns (height above player origin, forward distance from player).
        /// Defaults to (0.0, 0.9, 0.8) if no config exists.
        /// </summary>
        public static (float side, float height, float forward) GetFirePoint(BotFrameType frame)
        {
            Load();
            if (_cache == null) return (0f, 0.9f, 0.8f);

            string key = frame.ToString();
            if (!_cache.TryGetValue(key, out var frameObj)) return (0f, 0.9f, 0.8f);
            if (frameObj is not Dictionary<string, object> frameData) return (0f, 0.9f, 0.8f);

            float side = 0f;
            float height = 0.9f;
            float forward = 0.8f;

            if (frameData.TryGetValue("FirePointX", out var fx))
                side = Convert.ToSingle(fx);
            if (frameData.TryGetValue("FirePointY", out var fy))
                height = Convert.ToSingle(fy);
            if (frameData.TryGetValue("FirePointForward", out var ff))
                forward = Convert.ToSingle(ff);

            return (side, height, forward);
        }

        /// <summary>
        /// Get the configured weapon mount type for a given frame (frame-level default).
        /// Defaults to HandHeld if no config exists.
        /// </summary>
        public static WeaponMountType GetWeaponMountType(BotFrameType frame)
        {
            return GetWeaponMountType(frame, WeaponType.None);
        }

        /// <summary>
        /// Get the configured weapon mount type for a specific weapon on a given frame.
        /// Checks per-weapon overrides first, then falls back to frame-level default.
        /// </summary>
        public static WeaponMountType GetWeaponMountType(BotFrameType frame, WeaponType weapon)
        {
            Load();
            if (_cache == null) return WeaponMountType.HandHeld;

            string key = frame.ToString();
            if (!_cache.TryGetValue(key, out var frameObj)) return WeaponMountType.HandHeld;
            if (frameObj is not Dictionary<string, object> frameData) return WeaponMountType.HandHeld;

            // Per-weapon mount takes priority
            if (weapon != WeaponType.None
                && frameData.TryGetValue("WeaponMounts", out var wm)
                && wm is Dictionary<string, object> mounts
                && mounts.TryGetValue(weapon.ToString(), out var wmt)
                && wmt is string weaponMountStr)
            {
                if (Enum.TryParse<WeaponMountType>(weaponMountStr, out var parsed))
                    return parsed;
            }

            // Fall back to frame-level default
            if (frameData.TryGetValue("WeaponMountType", out var mt) && mt is string mountStr)
            {
                if (Enum.TryParse<WeaponMountType>(mountStr, out var parsed))
                    return parsed;
            }

            return WeaponMountType.HandHeld;
        }
    }
}
