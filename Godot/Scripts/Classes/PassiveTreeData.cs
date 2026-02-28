using System.Collections.Generic;

namespace DungeonCrawlerCarl
{
    public class PassiveTreeData
    {
        public Dictionary<string, PassiveNodeData> Nodes { get; set; } = new();
        public Dictionary<CrawlerClassName, string> ClassStartNodes { get; set; } = new();

        public PassiveNodeData GetNode(string id)
        {
            return Nodes.TryGetValue(id, out var node) ? node : null;
        }

        public void AddNode(PassiveNodeData node)
        {
            Nodes[node.Id] = node;

            if (node.NodeType == SkillNodeType.ClassStart)
                ClassStartNodes[node.ClassStartFor] = node.Id;
        }

        public void ConnectNodes(string idA, string idB)
        {
            if (Nodes.TryGetValue(idA, out var a) && Nodes.TryGetValue(idB, out var b))
            {
                a.ConnectTo(idB);
                b.ConnectTo(idA);
            }
        }
    }
}
