using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Emission flash on a model's override materials that can be undone exactly. Setting the
    /// flash colour and later only resetting the energy left things glowing the flash colour
    /// for the rest of their life (enemies white after their first hit).
    /// </summary>
    public static class HitFlash
    {
        private static readonly StringName MetaOrig = "flash_orig";

        /// <param name="glowingOnly">Only brighten parts that already glow (a tower's band and
        /// muzzles when it fires), instead of washing the whole model in the colour.</param>
        public static void On(Node node, Color color, float energy, bool glowingOnly = false)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat
                && (!glowingOnly || mat.EmissionEnabled || mat.HasMeta(MetaOrig)))
            {
                if (!mat.HasMeta(MetaOrig))
                    mat.SetMeta(MetaOrig, new Godot.Collections.Array {
                        mat.EmissionEnabled, mat.Emission, mat.EmissionEnergyMultiplier });
                mat.EmissionEnabled = true;
                mat.Emission = color;
                mat.EmissionEnergyMultiplier = energy;
            }
            foreach (var child in node.GetChildren())
                On(child, color, energy, glowingOnly);
        }

        public static void Off(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat && mat.HasMeta(MetaOrig))
            {
                var orig = mat.GetMeta(MetaOrig).AsGodotArray();
                mat.EmissionEnabled = orig[0].AsBool();
                mat.Emission = orig[1].AsColor();
                mat.EmissionEnergyMultiplier = (float)orig[2].AsDouble();
                mat.RemoveMeta(MetaOrig); // the next flash captures whatever the material is then
            }
            foreach (var child in node.GetChildren())
                Off(child);
        }
    }
}
