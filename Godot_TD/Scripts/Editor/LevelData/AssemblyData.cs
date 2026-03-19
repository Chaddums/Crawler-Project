using System.Collections.Generic;

namespace JunkyardTD
{
    public class AssemblyData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<AssemblyChildData> Children { get; set; } = new();
    }

    public class AssemblyChildData
    {
        public string AssetPath { get; set; } = "";
        public float RelPosX { get; set; }
        public float RelPosY { get; set; }
        public float RelPosZ { get; set; }
        public float RelRotY { get; set; }
        public float ScaleX { get; set; } = 1f;
        public float ScaleY { get; set; } = 1f;
        public float ScaleZ { get; set; } = 1f;
        public MaterialOverrideData MaterialOverride { get; set; }
    }
}
