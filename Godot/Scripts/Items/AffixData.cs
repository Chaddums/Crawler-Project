using Godot;

namespace JunkbotArena
{
    [GlobalClass]
    public partial class AffixData : Resource
    {
        [Export] public string Id { get; set; } = "";
        [Export] public string AffixName { get; set; } = "";
        [Export] public AffixType Type { get; set; } = AffixType.Prefix;
        [Export] public StatType Stat { get; set; }
        [Export] public ModifierType ModType { get; set; } = ModifierType.Flat;
        [Export] public float MinValue { get; set; }
        [Export] public float MaxValue { get; set; }
        [Export] public int MinItemLevel { get; set; } = 1;
        [Export] public int Weight { get; set; } = 100;

        public AffixData() { }

        public AffixData(string id, string name, AffixType type, StatType stat,
            ModifierType modType, float min, float max, int minILvl = 1, int weight = 100)
        {
            Id = id;
            AffixName = name;
            Type = type;
            Stat = stat;
            ModType = modType;
            MinValue = min;
            MaxValue = max;
            MinItemLevel = minILvl;
            Weight = weight;
        }
    }
}
