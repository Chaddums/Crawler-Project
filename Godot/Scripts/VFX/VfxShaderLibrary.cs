using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Loads and applies spatial shaders from res://Shaders/ to 3D meshes.
    /// Provides tween-based animation helpers for common effects.
    /// </summary>
    public static class VfxShaderLibrary
    {
        private static readonly Dictionary<string, Shader> _cache = new();

        private static Shader Load(string name)
        {
            if (_cache.TryGetValue(name, out var cached)) return cached;
            string path = $"res://Shaders/{name}.gdshader";
            if (!ResourceLoader.Exists(path)) return null;
            var shader = GD.Load<Shader>(path);
            if (shader != null) _cache[name] = shader;
            return shader;
        }

        /// <summary>
        /// Apply hit flash to all MeshInstance3D children. Returns the tween for chaining.
        /// Flashes white then fades back over duration.
        /// </summary>
        public static Tween ApplyHitFlash(Node3D root, Color flashColor = default, float duration = 0.15f)
        {
            if (flashColor == default) flashColor = Colors.White;
            var shader = Load("flash_white");
            if (shader == null || !GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return null;

            var meshes = CollectMeshInstances(root);
            if (meshes.Count == 0) return null;

            // Store original materials and apply shader overlays
            var originals = new List<(MeshInstance3D mesh, Material orig)>();
            foreach (var mi in meshes)
            {
                var orig = mi.MaterialOverride;
                originals.Add((mi, orig));

                var mat = new ShaderMaterial();
                mat.Shader = shader;
                mat.SetShaderParameter("flash_color", flashColor);
                mat.SetShaderParameter("flash_amount", 1.0f);
                if (orig is StandardMaterial3D std)
                    mat.SetShaderParameter("albedo_color", std.AlbedoColor);
                mi.MaterialOverride = mat;
            }

            var tween = root.CreateTween();
            foreach (var (mi, _) in originals)
            {
                if (mi.MaterialOverride is ShaderMaterial sm)
                    tween.Parallel().TweenProperty(sm, "shader_parameter/flash_amount", 0.0f, duration);
            }

            // Restore original materials after flash
            tween.TweenCallback(Callable.From(() =>
            {
                foreach (var (mi, orig) in originals)
                {
                    if (GodotObject.IsInstanceValid(mi))
                        mi.MaterialOverride = orig;
                }
            }));

            return tween;
        }

        /// <summary>
        /// Dissolve a node over duration, then QueueFree. Great for death effects.
        /// </summary>
        public static void ApplyDissolve(Node3D root, Color edgeColor = default, float duration = 0.8f, bool freeAfter = true)
        {
            if (edgeColor == default) edgeColor = new Color(1f, 0.5f, 0f);
            var shader = Load("dissolve");
            if (shader == null || !GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;

            var meshes = CollectMeshInstances(root);
            var shaderMats = new List<ShaderMaterial>();

            foreach (var mi in meshes)
            {
                var mat = new ShaderMaterial();
                mat.Shader = shader;
                mat.SetShaderParameter("dissolve_amount", 0.0f);
                mat.SetShaderParameter("edge_color", edgeColor);
                if (mi.MaterialOverride is StandardMaterial3D std)
                    mat.SetShaderParameter("albedo_color", std.AlbedoColor);
                else
                    mat.SetShaderParameter("albedo_color", Colors.White);
                mi.MaterialOverride = mat;
                shaderMats.Add(mat);
            }

            var tween = root.CreateTween();
            foreach (var sm in shaderMats)
                tween.Parallel().TweenProperty(sm, "shader_parameter/dissolve_amount", 1.0f, duration);

            if (freeAfter)
            {
                tween.TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(root)) root.QueueFree();
                }));
            }
        }

        /// <summary>
        /// Apply frozen ice effect, animate freeze_amount to target value.
        /// </summary>
        public static void ApplyFrozen(Node3D root, float freezeAmount = 0.8f, float duration = 0.3f)
        {
            var shader = Load("frozen");
            if (shader == null || !GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;

            var meshes = CollectMeshInstances(root);
            var tween = root.CreateTween();

            foreach (var mi in meshes)
            {
                var mat = new ShaderMaterial();
                mat.Shader = shader;
                mat.SetShaderParameter("freeze_amount", 0.0f);
                if (mi.MaterialOverride is StandardMaterial3D std)
                    mat.SetShaderParameter("albedo_color", std.AlbedoColor);
                else
                    mat.SetShaderParameter("albedo_color", Colors.White);
                mi.MaterialOverride = mat;
                tween.Parallel().TweenProperty(mat, "shader_parameter/freeze_amount", freezeAmount, duration);
            }
        }

        /// <summary>
        /// Apply burning overlay effect.
        /// </summary>
        public static void ApplyBurning(Node3D root, float burnAmount = 0.6f, float duration = 0.3f)
        {
            var shader = Load("burning");
            if (shader == null || !GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;

            var meshes = CollectMeshInstances(root);
            var tween = root.CreateTween();

            foreach (var mi in meshes)
            {
                var mat = new ShaderMaterial();
                mat.Shader = shader;
                mat.SetShaderParameter("burn_amount", 0.0f);
                if (mi.MaterialOverride is StandardMaterial3D std)
                    mat.SetShaderParameter("albedo_color", std.AlbedoColor);
                else
                    mat.SetShaderParameter("albedo_color", Colors.White);
                mi.MaterialOverride = mat;
                tween.Parallel().TweenProperty(mat, "shader_parameter/burn_amount", burnAmount, duration);
            }
        }

        /// <summary>
        /// Create a hex energy barrier sphere around a position. Returns the node (caller adds to tree).
        /// </summary>
        public static Node3D CreateEnergyBarrier(Color color = default, float radius = 1.5f)
        {
            if (color == default) color = new Color(0.3f, 0.7f, 1.0f, 0.8f);
            var shader = Load("energy_barrier");
            if (shader == null) return null;

            var root = new Node3D();
            root.Name = "EnergyBarrier";

            var meshInst = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = radius;
            sphere.Height = radius * 2f;
            sphere.RadialSegments = 32;
            sphere.Rings = 16;
            meshInst.Mesh = sphere;

            var mat = new ShaderMaterial();
            mat.Shader = shader;
            mat.SetShaderParameter("barrier_color", color);
            mat.SetShaderParameter("hex_scale", 12.0f);
            mat.SetShaderParameter("pulse_speed", 3.0f);
            mat.SetShaderParameter("edge_brightness", 2.0f);
            mat.SetShaderParameter("opacity", 0.6f);
            meshInst.MaterialOverride = mat;

            root.AddChild(meshInst);
            return root;
        }

        /// <summary>
        /// Remove all shader overrides from a node tree, restoring default materials.
        /// </summary>
        public static void ClearShaderEffects(Node3D root)
        {
            if (!GodotObject.IsInstanceValid(root)) return;
            foreach (var mi in CollectMeshInstances(root))
            {
                if (mi.MaterialOverride is ShaderMaterial)
                    mi.MaterialOverride = null;
            }
        }

        private static List<MeshInstance3D> CollectMeshInstances(Node root)
        {
            var result = new List<MeshInstance3D>();
            CollectRecursive(root, result);
            return result;
        }

        private static void CollectRecursive(Node node, List<MeshInstance3D> result)
        {
            if (node is MeshInstance3D mi)
                result.Add(mi);
            foreach (var child in node.GetChildren())
                CollectRecursive(child, result);
        }
    }
}
