using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    [GlobalClass]
    public partial class BotFrameData : Resource
    {
        [Export] public BotFrameType ClassName { get; set; }
        [Export] public string DisplayName { get; set; } = "";
        [Export] public string Description { get; set; } = "";
        [Export] public string Lore { get; set; } = "";
        [Export] public StatType PrimaryStat { get; set; }
        [Export] public StatType SecondaryStat { get; set; }

        // Base stats at level 1
        public StatBlock BaseStats { get; set; } = new();

        // Per-level stat growth
        public float HpPerLevel { get; set; } = 8f;
        public float ManaPerLevel { get; set; } = 3f;
        public float PrimaryStatPerLevel { get; set; } = 2f;
        public float SecondaryStatPerLevel { get; set; } = 1f;

        // Starting ability IDs
        public List<string> StartingAbilities { get; set; } = new();

        // Abilities unlocked at specific levels: level -> ability ID
        public Dictionary<int, string> AbilityProgression { get; set; } = new();

        // Passive tree start node ID
        public string TreeStartNodeId { get; set; } = "";

        public BotFrameData() { }
    }
}
