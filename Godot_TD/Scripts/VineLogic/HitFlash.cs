using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Emission flash on a model's override materials that can be undone exactly. Setting the
    /// flash colour and later only resetting the energy left things glowing the flash colour
    /// for the rest of their life (enemies white after their first hit).
    ///
    /// What a material looked like before its flash is kept here, keyed by the material, not in
    /// its metadata: a tower flashes every shot, and the metadata Array, the GetChildren Array and
    /// their enumerators were the biggest allocation left in a 300-tower field (10 MB in ten
    /// seconds of Godot wrapper bookkeeping, 2026-10). A model that flashes often can hand in the
    /// meshes it collected once with <see cref="Collect"/> instead of walking its tree each time.
    /// </summary>
    public static class HitFlash
    {
        /// <summary>A ShaderMaterial marked with this has a <c>flash</c> uniform (vec4: colour, amount) to drive instead.</summary>
        public static readonly StringName ShaderFlashMeta = "flash_uniform";
        private static readonly StringName FlashParam = "flash";

        private struct Orig
        {
            public bool On;
            public Color Color;
            public float Energy;
            public Variant Param;
        }

        private static readonly Dictionary<ulong, Orig> _orig = new();
        private static readonly List<ulong> _stale = new();
        private static int _pruneAt = 2048;

        /// <summary>Materials flashed now (for tests).</summary>
        internal static int Lit => _orig.Count;

        /// <param name="glowingOnly">Only brighten parts that already glow (a tower's band and
        /// muzzles when it fires), instead of washing the whole model in the colour.</param>
        public static void On(Node node, Color color, float energy, bool glowingOnly = false)
        {
            if (node == null) return;
            if (node is MeshInstance3D mesh) OnMesh(mesh, color, energy, glowingOnly);
            int n = node.GetChildCount();
            for (int i = 0; i < n; i++) On(node.GetChild(i), color, energy, glowingOnly);
        }

        public static void Off(Node node)
        {
            if (node == null) return;
            if (node is MeshInstance3D mesh) OffMesh(mesh);
            int n = node.GetChildCount();
            for (int i = 0; i < n; i++) Off(node.GetChild(i));
        }

        /// <summary>Flash the meshes <see cref="Collect"/> gathered.</summary>
        public static void On(List<MeshInstance3D> meshes, Color color, float energy, bool glowingOnly = false)
        {
            for (int i = 0; i < meshes.Count; i++)
                if (GodotObject.IsInstanceValid(meshes[i])) OnMesh(meshes[i], color, energy, glowingOnly);
        }

        public static void Off(List<MeshInstance3D> meshes)
        {
            for (int i = 0; i < meshes.Count; i++)
                if (GodotObject.IsInstanceValid(meshes[i])) OffMesh(meshes[i]);
        }

        /// <summary>Every mesh under <paramref name="root"/> (itself included).</summary>
        public static void Collect(Node root, List<MeshInstance3D> into)
        {
            if (root == null) return;
            if (root is MeshInstance3D mesh) into.Add(mesh);
            int n = root.GetChildCount();
            for (int i = 0; i < n; i++) Collect(root.GetChild(i), into);
        }

        private static void OnMesh(MeshInstance3D mesh, Color color, float energy, bool glowingOnly)
        {
            var m = mesh.MaterialOverride;
            if (m is StandardMaterial3D mat)
            {
                ulong id = mat.GetInstanceId();
                bool known = _orig.ContainsKey(id);
                if (glowingOnly && !known && !mat.EmissionEnabled) return;
                if (!known)
                {
                    Prune();
                    _orig[id] = new Orig { On = mat.EmissionEnabled, Color = mat.Emission, Energy = mat.EmissionEnergyMultiplier };
                }
                mat.EmissionEnabled = true;
                mat.Emission = color;
                mat.EmissionEnergyMultiplier = energy;
            }
            else if (!glowingOnly && m is ShaderMaterial sh && sh.HasMeta(ShaderFlashMeta))
            {
                ulong id = sh.GetInstanceId();
                if (!_orig.ContainsKey(id))
                {
                    Prune();
                    _orig[id] = new Orig { Param = sh.GetShaderParameter(FlashParam) };
                }
                sh.SetShaderParameter(FlashParam, new Vector4(color.R, color.G, color.B, energy * 0.4f));
            }
        }

        private static void OffMesh(MeshInstance3D mesh)
        {
            var m = mesh.MaterialOverride;
            if (m == null || !_orig.Remove(m.GetInstanceId(), out var o)) return; // the next flash captures whatever it is then
            if (m is StandardMaterial3D mat)
            {
                mat.EmissionEnabled = o.On;
                mat.Emission = o.Color;
                mat.EmissionEnergyMultiplier = o.Energy;
            }
            else if (m is ShaderMaterial sh)
                sh.SetShaderParameter(FlashParam, o.Param);
        }

        /// <summary>Drop what was kept for materials that are gone (an enemy freed mid-flash).</summary>
        private static void Prune()
        {
            if (_orig.Count < _pruneAt) return;
            _stale.Clear();
            foreach (var id in _orig.Keys)
                if (!GodotObject.IsInstanceIdValid(id)) _stale.Add(id);
            foreach (var id in _stale) _orig.Remove(id);
            // A big field lit at once is not stale: look again only once it has doubled
            _pruneAt = System.Math.Max(2048, _orig.Count * 2);
        }
    }
}
