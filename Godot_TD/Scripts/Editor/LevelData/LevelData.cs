using System.Collections.Generic;

namespace JunkyardTD
{
    public class LevelData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Floor { get; set; } = 1;
        public int Width { get; set; } = 40;
        public int Height { get; set; } = 24;
        public string HeightmapProfile { get; set; } = "Gentle";
        public float HeightmapSeed { get; set; } = -1;
        public List<HeightOverrideData> HeightOverrides { get; set; } = new();
        public List<CellPlacement> Cells { get; set; } = new();
        public List<EntryRegionData> EntryRegions { get; set; } = new();
        public ExitPointData ExitPoint { get; set; }
        public List<PropPlacement> Props { get; set; } = new();
        public List<AssetPlacement> Assets { get; set; } = new();
        public List<AssemblyPlacement> Assemblies { get; set; } = new();
        public List<LightPlacement> Lights { get; set; } = new();
        public List<AnimatedPropData> AnimatedProps { get; set; } = new();
        public List<EnvironmentFXData> EnvironmentFX { get; set; } = new();
        public List<TextureOverlay> TextureOverlays { get; set; } = new();
        public List<ShieldWallLevelData> ShieldWalls { get; set; } = new();
        public string PlanetTheme { get; set; } = "tron";

        /// <summary>
        /// Set or clear a texture overlay on a cell.
        /// </summary>
        public void SetCellTexture(int x, int y, string textureId)
        {
            // Remove existing
            TextureOverlays.RemoveAll(t => t.X == x && t.Y == y);

            if (!string.IsNullOrEmpty(textureId))
                TextureOverlays.Add(new TextureOverlay { X = x, Y = y, TextureId = textureId });
        }

        public string GetCellTexture(int x, int y)
        {
            foreach (var t in TextureOverlays)
                if (t.X == x && t.Y == y)
                    return t.TextureId;
            return null;
        }
    }

    public class TextureOverlay
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string TextureId { get; set; } = "ground";
    }

    public class CellPlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Type { get; set; } = "Wall";
    }

    public class HeightOverrideData
    {
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public float TargetHeight { get; set; }
    }

    public class EntryRegionData
    {
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int EndX { get; set; }
        public int EndY { get; set; }
        public string Direction { get; set; }        // "West", "North", "East", "South" — null = inferred from position
        public bool StartActive { get; set; } = true;
    }

    public class ShieldWallLevelData
    {
        public string Direction { get; set; } = "North";
        public float HP { get; set; } = 500f;
        public int EntryRegionIndex { get; set; }
        public string TriggerType { get; set; } = "TimeMilestone";
        public float TriggerTime { get; set; } = 300f;  // seconds
    }

    public class ExitPointData
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class PropPlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string PropType { get; set; } = "container";
    }

    public class AssetPlacement
    {
        public string Id { get; set; } = "";
        public string Path { get; set; } = "";
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }
        public float RotZ { get; set; }
        public float ScaleX { get; set; } = 1f;
        public float ScaleY { get; set; } = 1f;
        public float ScaleZ { get; set; } = 1f;
        public MaterialOverrideData MaterialOverride { get; set; }
    }

    public class MaterialOverrideData
    {
        public string MaterialType { get; set; } = "theme";  // theme, faction, custom, emissive, original
        public int FactionId { get; set; }                     // 0=Player,1=Scavenger,2=Brute,3=Swarm,4=Ghost
        public float TintR { get; set; }
        public float TintG { get; set; } = 0.85f;
        public float TintB { get; set; } = 0.95f;
        public float EmissiveIntensity { get; set; } = 1f;
        public int OutlineMode { get; set; }                   // 0=PerMesh,1=Silhouette,2=None
    }

    public class AssemblyPlacement
    {
        public string Id { get; set; } = "";
        public string AssemblyId { get; set; } = "";
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float RotY { get; set; }
        public float ScaleX { get; set; } = 1f;
        public float ScaleY { get; set; } = 1f;
        public float ScaleZ { get; set; } = 1f;
    }

    public class LightPlacement
    {
        public string Id { get; set; } = "";
        public string LightType { get; set; } = "Omni";
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float ColorR { get; set; } = 1f;
        public float ColorG { get; set; } = 0.9f;
        public float ColorB { get; set; } = 0.8f;
        public float Energy { get; set; } = 1f;
        public float Range { get; set; } = 10f;
        public bool Shadow { get; set; }
    }

    public class AnimatedPropData
    {
        public string Id { get; set; } = "";
        public string Path { get; set; } = "";
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public string AnimType { get; set; } = "Spin";
        public float AnimSpeed { get; set; } = 45f;
        public float AnimAmplitude { get; set; } = 0.5f;
    }

    public class EnvironmentFXData
    {
        public string Id { get; set; } = "";
        public string FXType { get; set; } = "Fire";
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float Radius { get; set; } = 2f;
        public float Intensity { get; set; } = 1f;
        public float ColorR { get; set; } = 1f;
        public float ColorG { get; set; } = 0.5f;
        public float ColorB { get; set; } = 0.1f;
    }
}
