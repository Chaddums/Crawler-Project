using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Applies MaterialOverrideData to placed 3D nodes using existing theme machinery.
    /// </summary>
    public static class LevelEditorMaterialPainter
    {
        public static void Apply(Node3D node, MaterialOverrideData data)
        {
            if (node == null || data == null) return;

            switch (data.MaterialType)
            {
                case "theme":
                    var tint = new Color(data.TintR, data.TintG, data.TintB);
                    if (PlanetTheme.Current is TronPlanetTheme tron)
                        tron.OutlineMode = data.OutlineMode;
                    PlanetTheme.Current.ApplyToNode(node, tint);
                    break;

                case "faction":
                    var faction = data.FactionId switch
                    {
                        0 => VineEnemyFaction.Scavenger, // Player uses theme default
                        1 => VineEnemyFaction.Scavenger,
                        2 => VineEnemyFaction.Brute,
                        3 => VineEnemyFaction.Swarm,
                        4 => VineEnemyFaction.Ghost,
                        _ => VineEnemyFaction.Scavenger
                    };
                    if (data.FactionId == 0)
                    {
                        // Player faction — use BitPalette
                        BitPalette.ApplyToNode(node);
                    }
                    else
                    {
                        if (PlanetTheme.Current is TronPlanetTheme t)
                            t.OutlineMode = data.OutlineMode;
                        PlanetTheme.Current.ApplyEnemyTheme(node, faction);
                    }
                    break;

                case "custom":
                    var customColor = new Color(data.TintR, data.TintG, data.TintB);
                    var customMat = new StandardMaterial3D();
                    customMat.AlbedoColor = customColor;
                    customMat.Roughness = 0.6f;
                    customMat.Metallic = 0.3f;
                    AssetLibrary.ApplyMaterialRecursive(node, customMat);
                    break;

                case "emissive":
                    var emitColor = new Color(data.TintR, data.TintG, data.TintB);
                    var emitMat = AssetLibrary.MakeEmissive(emitColor, data.EmissiveIntensity);
                    AssetLibrary.ApplyMaterialRecursive(node, emitMat);
                    break;

                case "original":
                    // Reload from disk to restore original materials
                    break;
            }
        }

        /// <summary>
        /// Creates a default MaterialOverrideData for a given faction preset.
        /// </summary>
        public static MaterialOverrideData MakeFactionPreset(int factionId)
        {
            return new MaterialOverrideData
            {
                MaterialType = "faction",
                FactionId = factionId
            };
        }

        /// <summary>
        /// Creates an emissive MaterialOverrideData.
        /// </summary>
        public static MaterialOverrideData MakeEmissivePreset(Color color, float intensity = 2f)
        {
            return new MaterialOverrideData
            {
                MaterialType = "emissive",
                TintR = color.R,
                TintG = color.G,
                TintB = color.B,
                EmissiveIntensity = intensity
            };
        }

        /// <summary>
        /// Creates a custom solid-color MaterialOverrideData.
        /// </summary>
        public static MaterialOverrideData MakeCustomPreset(Color color)
        {
            return new MaterialOverrideData
            {
                MaterialType = "custom",
                TintR = color.R,
                TintG = color.G,
                TintB = color.B
            };
        }

        /// <summary>
        /// Creates a "restore original" MaterialOverrideData.
        /// </summary>
        public static MaterialOverrideData MakeOriginalPreset()
        {
            return new MaterialOverrideData
            {
                MaterialType = "original"
            };
        }
    }
}
