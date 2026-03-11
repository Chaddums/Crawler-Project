using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Maps FBX bone node names to game pivot names by creating alias child nodes.
    /// Aliases are zero-transform children that move with the bone automatically.
    /// This avoids renaming FBX nodes (which would break AnimationPlayer track paths).
    /// Also creates a WeaponMount Marker3D on the right hand for weapon attachment.
    /// For any pivots not found via bone mapping, creates synthetic pivots at AABB-relative positions.
    /// </summary>
    public static class FbxPivotMapper
    {
        // FBX bone name → game pivot name (case-insensitive matching via OrdinalIgnoreCase)
        private static readonly Dictionary<string, string> BoneMap = new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Head
            { "Head", "Head" },

            // Torso
            { "Spine", "Torso" },
            { "Spine1", "Torso" },
            { "Spine2", "Torso" },
            { "Chest", "Torso" },
            { "UpperBody", "Torso" },
            { "Spine.001", "Torso" },
            { "Spine.002", "Torso" },

            // Body / root
            { "Hips", "Body" },
            { "Root", "Body" },
            { "Pelvis", "Body" },

            // Right arm chain
            { "UpperArm_R", "RightArm" },
            { "Shoulder_R", "RightArm" },
            { "RightUpperArm", "RightArm" },
            { "Arm.Upper.R", "RightArm" },
            { "ArmUpper.R", "RightArm" },
            { "Upper_Arm.R", "RightArm" },
            { "Upper Arm.R", "RightArm" },
            { "Arm_Upper_R", "RightArm" },
            { "LowerArm_R", "RightElbow" },
            { "Forearm_R", "RightElbow" },
            { "RightLowerArm", "RightElbow" },
            { "Arm.Lower.R", "RightElbow" },
            { "ArmLower.R", "RightElbow" },
            { "Lower_Arm.R", "RightElbow" },
            { "Lower Arm.R", "RightElbow" },
            { "Arm_Lower_R", "RightElbow" },
            { "Hand_R", "RightHand" },
            { "RightHand", "RightHand" },
            { "Hand.R", "RightHand" },

            // Left arm chain
            { "UpperArm_L", "LeftArm" },
            { "Shoulder_L", "LeftArm" },
            { "LeftUpperArm", "LeftArm" },
            { "Arm.Upper.L", "LeftArm" },
            { "ArmUpper.L", "LeftArm" },
            { "Upper_Arm.L", "LeftArm" },
            { "Upper Arm.L", "LeftArm" },
            { "Arm_Upper_L", "LeftArm" },
            { "LowerArm_L", "LeftElbow" },
            { "Forearm_L", "LeftElbow" },
            { "LeftLowerArm", "LeftElbow" },
            { "Arm.Lower.L", "LeftElbow" },
            { "ArmLower.L", "LeftElbow" },
            { "Lower_Arm.L", "LeftElbow" },
            { "Lower Arm.L", "LeftElbow" },
            { "Arm_Lower_L", "LeftElbow" },
            { "Hand_L", "LeftHand" },
            { "LeftHand", "LeftHand" },
            { "Hand.L", "LeftHand" },

            // Right leg chain
            { "UpperLeg_R", "RightLeg" },
            { "Thigh_R", "RightLeg" },
            { "RightUpperLeg", "RightLeg" },
            { "Leg.Upper.R", "RightLeg" },
            { "LegUpper.R", "RightLeg" },
            { "Upper_Leg.R", "RightLeg" },
            { "Upper Leg.R", "RightLeg" },
            { "Leg_Upper_R", "RightLeg" },
            { "LowerLeg_R", "RightKnee" },
            { "Shin_R", "RightKnee" },
            { "RightLowerLeg", "RightKnee" },
            { "Leg.Lower.R", "RightKnee" },
            { "LegLower.R", "RightKnee" },
            { "Lower_Leg.R", "RightKnee" },
            { "Lower Leg.R", "RightKnee" },
            { "Leg_Lower_R", "RightKnee" },
            { "Foot_R", "RightAnkle" },
            { "RightFoot", "RightAnkle" },
            { "Foot.R", "RightAnkle" },

            // Left leg chain
            { "UpperLeg_L", "LeftLeg" },
            { "Thigh_L", "LeftLeg" },
            { "LeftUpperLeg", "LeftLeg" },
            { "Leg.Upper.L", "LeftLeg" },
            { "LegUpper.L", "LeftLeg" },
            { "Upper_Leg.L", "LeftLeg" },
            { "Upper Leg.L", "LeftLeg" },
            { "Leg_Upper_L", "LeftLeg" },
            { "LowerLeg_L", "LeftKnee" },
            { "Shin_L", "LeftKnee" },
            { "LeftLowerLeg", "LeftKnee" },
            { "Leg.Lower.L", "LeftKnee" },
            { "LegLower.L", "LeftKnee" },
            { "Lower_Leg.L", "LeftKnee" },
            { "Lower Leg.L", "LeftKnee" },
            { "Leg_Lower_L", "LeftKnee" },
            { "Foot_L", "LeftAnkle" },
            { "LeftFoot", "LeftAnkle" },
            { "Foot.L", "LeftAnkle" },
        };

        // Partial match fallbacks: if no exact match, check if node name contains these (lowercase)
        private static readonly (string Pattern, string PivotName)[] PartialMatches =
        {
            // Right side
            ("upperarm_r", "RightArm"),
            ("upper_arm_r", "RightArm"),
            ("shoulder_r", "RightArm"),
            ("lowerarm_r", "RightElbow"),
            ("lower_arm_r", "RightElbow"),
            ("forearm_r", "RightElbow"),
            ("hand_r", "RightHand"),
            ("upperleg_r", "RightLeg"),
            ("upper_leg_r", "RightLeg"),
            ("thigh_r", "RightLeg"),
            ("lowerleg_r", "RightKnee"),
            ("lower_leg_r", "RightKnee"),
            ("shin_r", "RightKnee"),
            ("foot_r", "RightAnkle"),
            // Left side
            ("upperarm_l", "LeftArm"),
            ("upper_arm_l", "LeftArm"),
            ("shoulder_l", "LeftArm"),
            ("lowerarm_l", "LeftElbow"),
            ("lower_arm_l", "LeftElbow"),
            ("forearm_l", "LeftElbow"),
            ("hand_l", "LeftHand"),
            ("upperleg_l", "LeftLeg"),
            ("upper_leg_l", "LeftLeg"),
            ("thigh_l", "LeftLeg"),
            ("lowerleg_l", "LeftKnee"),
            ("lower_leg_l", "LeftKnee"),
            ("shin_l", "LeftKnee"),
            ("foot_l", "LeftAnkle"),
            // .R / .L suffix (Blender style)
            ("arm.r", "RightArm"),
            ("arm.l", "LeftArm"),
            ("hand.r", "RightHand"),
            ("hand.l", "LeftHand"),
            ("leg.r", "RightLeg"),
            ("leg.l", "LeftLeg"),
            ("foot.r", "RightAnkle"),
            ("foot.l", "LeftAnkle"),
            // Generic (last resort)
            ("head", "Head"),
            ("spine", "Torso"),
            ("chest", "Torso"),
            ("hip", "Body"),
            ("pelvis", "Body"),
        };

        // Synthetic pivot positions as fractions of AABB: (xFrac, yFrac, zFrac)
        // x: 0=center, +1=right edge; y: 0=bottom, 1=top; z: 0=center, -1=front
        private static readonly (string Name, float XFrac, float YFrac, float ZFrac)[] SyntheticPivotFractions =
        {
            ("Body",      0f,     0.35f,  0f),
            ("Torso",     0f,     0.6f,   0f),
            ("Head",      0f,     0.9f,   0f),
            ("RightArm",  0.45f,  0.65f,  0f),
            ("LeftArm",  -0.45f,  0.65f,  0f),
            ("RightHand", 0.6f,   0.5f,  -0.1f),
            ("LeftHand", -0.6f,   0.5f,  -0.1f),
            ("RightLeg",  0.2f,   0.2f,   0f),
            ("LeftLeg",  -0.2f,   0.2f,   0f),
        };

        // Required game pivots — synthetics are created for any missing after bone mapping
        private static readonly string[] RequiredPivots =
        {
            "Body", "Torso", "Head", "RightArm", "LeftArm", "RightHand", "LeftHand", "RightLeg", "LeftLeg"
        };

        // Cache: model instance ID → (pivot name → node)
        private static readonly Dictionary<ulong, Dictionary<string, Node3D>> _cache = new();

        /// <summary>
        /// Walk the FBX hierarchy, create alias nodes for mapped bones, and add a WeaponMount.
        /// Creates synthetic pivots for any required game pivots not found via bone mapping.
        /// Call once after loading the model, before returning from BuildPlayerBody.
        /// </summary>
        public static void MapHierarchy(Node3D model)
        {
            // Debug: dump all node names in the hierarchy
            var allNames = new List<string>();
            CollectNodeNames(model, allNames, 0);
            GD.Print($"[FbxPivotMapper] Full hierarchy for '{model.Name}':\n{string.Join("\n", allNames)}");

            // Phase 1: Map FBX bones to game pivot names via aliases
            var mapping = new Dictionary<string, Node3D>();
            WalkAndMap(model, model, mapping);

            int boneMatches = mapping.Count;
            GD.Print($"[FbxPivotMapper] Bone mapping found {boneMatches} matches");

            // Phase 2: Create synthetic pivots for any required pivots still missing
            EnsureRequiredPivots(model, mapping);

            // Store in cache
            _cache[model.GetInstanceId()] = mapping;

            // Phase 3: Create WeaponMount
            Node3D mountParent = null;
            if (mapping.TryGetValue("RightHand", out var hand))
                mountParent = hand;
            else if (mapping.TryGetValue("RightArm", out var arm))
                mountParent = arm;
            else
                mountParent = model;

            if (FindNodeRecursive(model, "WeaponMount") == null)
            {
                var mount = new Marker3D();
                mount.Name = "WeaponMount";
                mount.Position = new Vector3(0, 0, -0.15f);
                mountParent.AddChild(mount);
                GD.Print($"[FbxPivotMapper] Created WeaponMount on '{mountParent.Name}'");
            }

            GD.Print($"[FbxPivotMapper] Final pivots ({mapping.Count}): {string.Join(", ", mapping.Keys)}");
        }

        /// <summary>
        /// Get the FBX bone node for a given game pivot name.
        /// </summary>
        public static Node3D GetMappedBone(Node3D model, string pivotName)
        {
            if (model == null) return null;
            if (_cache.TryGetValue(model.GetInstanceId(), out var mapping))
            {
                if (mapping.TryGetValue(pivotName, out var bone))
                    return bone;
            }
            return null;
        }

        /// <summary>
        /// Recursively search for a node by name in the hierarchy.
        /// </summary>
        public static Node3D FindNodeRecursive(Node root, string name)
        {
            if (root is Node3D n3d && n3d.Name == name)
                return n3d;
            foreach (var child in root.GetChildren())
            {
                if (child is Node node)
                {
                    var found = FindNodeRecursive(node, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

        /// <summary>
        /// Compute the combined AABB of all MeshInstance3D nodes in the hierarchy,
        /// in model-local space (before the model's own transform is applied).
        /// </summary>
        public static Aabb ComputeLocalAabb(Node3D model)
        {
            var aabb = new Aabb();
            bool first = true;
            CollectAabb(model, model, ref aabb, ref first);
            if (first) // no meshes found — use a sensible default
                aabb = new Aabb(new Vector3(-0.5f, 0, -0.5f), new Vector3(1, 2, 1));
            return aabb;
        }

        private static void CollectAabb(Node node, Node3D model, ref Aabb aabb, ref bool first)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var meshAabb = mi.Mesh.GetAabb();
                // Transform mesh AABB to model-local space
                var toModel = model.GlobalTransform.AffineInverse() * mi.GlobalTransform;
                // Transform the 8 corners of the mesh AABB
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? meshAabb.Position.X : meshAabb.End.X,
                        (i & 2) == 0 ? meshAabb.Position.Y : meshAabb.End.Y,
                        (i & 4) == 0 ? meshAabb.Position.Z : meshAabb.End.Z
                    );
                    var worldCorner = toModel * corner;
                    if (first)
                    {
                        aabb = new Aabb(worldCorner, Vector3.Zero);
                        first = false;
                    }
                    else
                    {
                        aabb = aabb.Expand(worldCorner);
                    }
                }
            }
            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    CollectAabb(childNode, model, ref aabb, ref first);
            }
        }

        private static void CollectNodeNames(Node node, List<string> names, int depth)
        {
            string indent = new string(' ', depth * 2);
            string type = node.GetType().Name;
            names.Add($"{indent}{node.Name} [{type}]");
            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    CollectNodeNames(childNode, names, depth + 1);
            }
        }

        private static void WalkAndMap(Node node, Node3D model, Dictionary<string, Node3D> mapping)
        {
            if (node is Node3D bone)
            {
                string boneName = bone.Name;

                // Try exact match
                if (BoneMap.TryGetValue(boneName, out var pivotName))
                {
                    AddAlias(bone, pivotName, mapping);
                }
                else
                {
                    // Try partial match
                    string lowerName = boneName.ToLower();
                    foreach (var (pattern, pivot) in PartialMatches)
                    {
                        if (lowerName.Contains(pattern) && !mapping.ContainsKey(pivot))
                        {
                            AddAlias(bone, pivot, mapping);
                            break;
                        }
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    WalkAndMap(childNode, model, mapping);
            }
        }

        private static void AddAlias(Node3D bone, string pivotName, Dictionary<string, Node3D> mapping)
        {
            if (mapping.ContainsKey(pivotName))
                return;

            // If the bone already has the game name, just register it
            if (bone.Name == pivotName)
            {
                mapping[pivotName] = bone;
                GD.Print($"[FbxPivotMapper]   '{bone.Name}' already matches pivot '{pivotName}'");
                return;
            }

            // Check if alias already exists
            foreach (var child in bone.GetChildren())
            {
                if (child is Node3D existing && existing.Name == pivotName)
                {
                    mapping[pivotName] = existing;
                    return;
                }
            }

            // Create zero-transform alias
            var alias = new Node3D();
            alias.Name = pivotName;
            bone.AddChild(alias);
            mapping[pivotName] = alias;

            GD.Print($"[FbxPivotMapper]   '{bone.Name}' -> alias '{pivotName}'");
        }

        /// <summary>
        /// For each required game pivot not already in the mapping,
        /// create a synthetic pivot at an AABB-relative position on the model root.
        /// </summary>
        private static void EnsureRequiredPivots(Node3D model, Dictionary<string, Node3D> mapping)
        {
            // Figure out which pivots are still missing
            var missing = new List<string>();
            foreach (var name in RequiredPivots)
            {
                if (!mapping.ContainsKey(name) && FindNodeRecursive(model, name) == null)
                    missing.Add(name);
            }

            if (missing.Count == 0) return;

            // Compute AABB to position synthetics proportionally
            // Note: model is NOT in the scene tree yet, so we use local transforms
            var aabb = ComputeLocalAabbFallback(model);
            float height = Mathf.Max(aabb.Size.Y, 0.1f);
            float width = Mathf.Max(aabb.Size.X, 0.1f);
            float depth = Mathf.Max(aabb.Size.Z, 0.1f);
            var bottom = aabb.Position;

            GD.Print($"[FbxPivotMapper] Model AABB: pos={aabb.Position}, size={aabb.Size}");

            foreach (var (name, xFrac, yFrac, zFrac) in SyntheticPivotFractions)
            {
                if (!missing.Contains(name)) continue;

                var pos = new Vector3(
                    bottom.X + width * 0.5f + xFrac * width,
                    bottom.Y + yFrac * height,
                    bottom.Z + depth * 0.5f + zFrac * depth
                );

                var pivot = new Node3D();
                pivot.Name = name;
                pivot.Position = pos;
                model.AddChild(pivot);
                mapping[name] = pivot;
                GD.Print($"[FbxPivotMapper]   Synthetic '{name}' at {pos}");
            }
        }

        /// <summary>
        /// Compute AABB from mesh data without requiring the scene tree.
        /// Walks MeshInstance3D nodes and accumulates their mesh AABBs
        /// using local transforms relative to model root.
        /// </summary>
        private static Aabb ComputeLocalAabbFallback(Node3D model)
        {
            var aabb = new Aabb();
            bool first = true;
            CollectAabbLocal(model, Transform3D.Identity, ref aabb, ref first);
            if (first)
                aabb = new Aabb(new Vector3(-0.5f, 0, -0.5f), new Vector3(1, 2, 1));
            return aabb;
        }

        private static void CollectAabbLocal(Node node, Transform3D localToModel, ref Aabb aabb, ref bool first)
        {
            Transform3D nodeTransform = localToModel;
            if (node is Node3D n3d && node != node.GetParent())
            {
                nodeTransform = localToModel * n3d.Transform;
            }

            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var meshAabb = mi.Mesh.GetAabb();
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? meshAabb.Position.X : meshAabb.End.X,
                        (i & 2) == 0 ? meshAabb.Position.Y : meshAabb.End.Y,
                        (i & 4) == 0 ? meshAabb.Position.Z : meshAabb.End.Z
                    );
                    var transformed = nodeTransform * corner;
                    if (first)
                    {
                        aabb = new Aabb(transformed, Vector3.Zero);
                        first = false;
                    }
                    else
                    {
                        aabb = aabb.Expand(transformed);
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                {
                    var childTransform = childNode is Node3D cn3d ? nodeTransform * cn3d.Transform : nodeTransform;
                    CollectAabbLocal(childNode, childTransform, ref aabb, ref first);
                }
            }
        }

        /// <summary>
        /// Clean up cache entries for freed models.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
