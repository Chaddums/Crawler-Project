using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    [GlobalClass]
    public partial class PassiveNodeData : Resource
    {
        [Export] public string Id { get; set; } = "";
        [Export] public string NodeName { get; set; } = "";
        [Export] public SkillNodeType NodeType { get; set; } = SkillNodeType.Basic;
        [Export] public string Description { get; set; } = "";

        /// <summary>
        /// Unique perk identifier for gameplay-changing effects.
        /// Other systems query PassiveTree.HasPerk() to check if active.
        /// Empty string means this node only grants stat bonuses.
        /// </summary>
        [Export] public string PerkId { get; set; } = "";

        // Stat bonuses this node grants
        public List<StatModifier> StatBonuses { get; set; } = new();

        // IDs of connected nodes in the tree graph
        public List<string> Connections { get; set; } = new();

        // Position on the tree UI (grid coords)
        [Export] public Vector2 TreePosition { get; set; }

        // For class start nodes
        [Export] public BotFrameType ClassStartFor { get; set; }

        /// <summary>
        /// The graft installed in this socket (CoreSocket nodes only).
        /// Null if no graft is socketed.
        /// </summary>
        public SalvageCoreData SocketedCore { get; set; }

        public PassiveNodeData() { }

        public PassiveNodeData(string id, string name, SkillNodeType type, Vector2 pos)
        {
            Id = id;
            NodeName = name;
            NodeType = type;
            TreePosition = pos;
        }

        public void AddBonus(StatType stat, ModifierType mod, float value)
        {
            StatBonuses.Add(new StatModifier(stat, mod, value));
        }

        public void ConnectTo(string otherId)
        {
            if (!Connections.Contains(otherId))
                Connections.Add(otherId);
        }
    }
}
