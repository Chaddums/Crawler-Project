using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Procedurally builds the ~120 node passive tree.
    /// 6 class starts arranged in a hexagon, with paths of basic nodes between them,
    /// notables at intersections, and keystones at the edges.
    /// </summary>
    public static class PassiveTreeBuilder
    {
        private static PassiveTreeData _tree;
        private static bool _built;

        public static PassiveTreeData Tree
        {
            get
            {
                if (!_built) Build();
                return _tree;
            }
        }

        public static void Build()
        {
            if (_built) return;

            _tree = new PassiveTreeData();
            int nodeId = 0;

            // Class start positions in a hexagon (radius 8)
            var classPositions = new Dictionary<BotFrameType, Vector2>
            {
                { BotFrameType.Scrapheap, HexPos(0, 8f) },
                { BotFrameType.TinCan, HexPos(1, 8f) },
                { BotFrameType.SparkPlug, HexPos(2, 8f) },
                { BotFrameType.RustBucket, HexPos(3, 8f) },
                { BotFrameType.NoiseBox, HexPos(4, 8f) },
                { BotFrameType.Clunker, HexPos(5, 8f) },
            };

            // Stat themes per class (what their branch focuses on)
            var classThemes = new Dictionary<BotFrameType, (StatType primary, StatType secondary)>
            {
                { BotFrameType.Scrapheap, (StatType.Strength, StatType.Constitution) },
                { BotFrameType.TinCan, (StatType.Strength, StatType.Dexterity) },
                { BotFrameType.SparkPlug, (StatType.Intelligence, StatType.MaxMana) },
                { BotFrameType.RustBucket, (StatType.Dexterity, StatType.Luck) },
                { BotFrameType.NoiseBox, (StatType.Intelligence, StatType.Charisma) },
                { BotFrameType.Clunker, (StatType.Strength, StatType.AttackSpeed) },
            };

            // 1. Create class start nodes
            var classStartIds = new Dictionary<BotFrameType, string>();
            foreach (var (cls, pos) in classPositions)
            {
                var id = $"start_{cls}";
                var node = new PassiveNodeData(id, $"{cls} Start", SkillNodeType.ClassStart, pos);
                node.ClassStartFor = cls;
                _tree.AddNode(node);
                classStartIds[cls] = id;
            }

            // 2. Build branch paths from each class start toward center
            var classes = new[] {
                BotFrameType.Scrapheap, BotFrameType.TinCan,
                BotFrameType.SparkPlug, BotFrameType.RustBucket,
                BotFrameType.NoiseBox, BotFrameType.Clunker
            };

            // Each class gets a branch of ~8 basic nodes heading toward center,
            // with a notable at node 4 and a keystone at the outer edge
            var branchEndIds = new Dictionary<BotFrameType, string>();

            foreach (var cls in classes)
            {
                var theme = classThemes[cls];
                var startPos = classPositions[cls];
                var direction = -startPos.Normalized(); // toward center
                string prevId = classStartIds[cls];

                // Keystone at outer edge (beyond start)
                var keystonePos = startPos + startPos.Normalized() * 2f;
                var keystoneId = $"ks_{cls}";
                var keystone = new PassiveNodeData(keystoneId, $"{cls} Keystone", SkillNodeType.Keystone, keystonePos);
                keystone.AddBonus(theme.primary, ModifierType.Percent, 0.15f);
                keystone.AddBonus(theme.secondary, ModifierType.Percent, 0.10f);
                keystone.Description = $"Major power boost for {cls} builds";
                _tree.AddNode(keystone);
                _tree.ConnectNodes(classStartIds[cls], keystoneId);

                // 8 basic nodes heading toward center
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 9f;
                    var pos = startPos + direction * (t * 7f);
                    // Add some spread
                    pos += new Vector2((float)Math.Sin(i * 1.7f) * 0.8f, (float)Math.Cos(i * 2.3f) * 0.8f);

                    bool isNotable = (i == 4);
                    var nid = $"n_{cls}_{nodeId++}";

                    if (isNotable)
                    {
                        var notable = new PassiveNodeData(nid, $"{theme.primary} Notable", SkillNodeType.Notable, pos);
                        notable.AddBonus(theme.primary, ModifierType.Flat, 3f);
                        notable.AddBonus(theme.secondary, ModifierType.Flat, 2f);
                        notable.AddBonus(StatType.MaxHealth, ModifierType.Flat, 5f);
                        notable.Description = $"A notable node boosting {theme.primary} and {theme.secondary}";
                        _tree.AddNode(notable);
                    }
                    else
                    {
                        var basic = new PassiveNodeData(nid, $"+{theme.primary}", SkillNodeType.Basic, pos);
                        // Alternate between primary and secondary stat bonuses
                        if (i % 2 == 0)
                            basic.AddBonus(theme.primary, ModifierType.Flat, 1f);
                        else
                            basic.AddBonus(theme.secondary, ModifierType.Flat, 1f);
                        _tree.AddNode(basic);
                    }

                    _tree.ConnectNodes(prevId, nid);
                    prevId = nid;
                }

                branchEndIds[cls] = prevId;
            }

            // 3. Connect adjacent branch ends to form the center ring
            for (int i = 0; i < classes.Length; i++)
            {
                int next = (i + 1) % classes.Length;
                _tree.ConnectNodes(branchEndIds[classes[i]], branchEndIds[classes[next]]);
            }

            // 4. Add cross-branch notable bridges (between non-adjacent classes)
            // Connect every other class pair with a notable in between
            for (int i = 0; i < 3; i++)
            {
                var clsA = classes[i];
                var clsB = classes[i + 3];
                var themeA = classThemes[clsA];
                var themeB = classThemes[clsB];

                var midPos = (classPositions[clsA] + classPositions[clsB]) * 0.5f;
                var bridgeId = $"bridge_{nodeId++}";
                var bridge = new PassiveNodeData(bridgeId, "Cross-Path Notable", SkillNodeType.Notable, midPos);
                bridge.AddBonus(themeA.primary, ModifierType.Flat, 2f);
                bridge.AddBonus(themeB.primary, ModifierType.Flat, 2f);
                bridge.Description = "A bridge between two class paths";
                _tree.AddNode(bridge);

                _tree.ConnectNodes(branchEndIds[clsA], bridgeId);
                _tree.ConnectNodes(branchEndIds[clsB], bridgeId);
            }

            // 5. Add generic defensive nodes in a ring around the center
            string prevDefId = null;
            string firstDefId = null;
            for (int i = 0; i < 6; i++)
            {
                var pos = HexPos(i, 2f);
                var defId = $"def_{nodeId++}";
                var defNode = new PassiveNodeData(defId, "+Health", SkillNodeType.Basic, pos);
                defNode.AddBonus(StatType.MaxHealth, ModifierType.Flat, 3f);
                if (i % 2 == 0)
                    defNode.AddBonus(StatType.Armor, ModifierType.Flat, 1f);
                _tree.AddNode(defNode);

                if (prevDefId != null)
                    _tree.ConnectNodes(prevDefId, defId);
                else
                    firstDefId = defId;

                // Connect to nearest branch end
                _tree.ConnectNodes(branchEndIds[classes[i]], defId);
                prevDefId = defId;
            }
            // Close the ring
            if (firstDefId != null && prevDefId != null)
                _tree.ConnectNodes(prevDefId, firstDefId);

            // 6. Add jewel sockets (one per class, off the main branch)
            foreach (var cls in classes)
            {
                var startPos = classPositions[cls];
                var perpendicular = new Vector2(-startPos.Normalized().Y, startPos.Normalized().X);
                var jewelPos = startPos * 0.6f + perpendicular * 2.5f;
                var jewelId = $"jewel_{cls}";
                var jewel = new PassiveNodeData(jewelId, "Jewel Socket", SkillNodeType.JewelSocket, jewelPos);
                jewel.Description = "Socket a jewel for custom bonuses";
                _tree.AddNode(jewel);

                // Connect to the branch midpoint (the notable at node 4)
                // Find a node to connect to - use class start for now
                var connectId = $"n_{cls}_{nodeId - 55 + Array.IndexOf(classes, cls) * 8 + 3}";
                if (_tree.GetNode(connectId) != null)
                    _tree.ConnectNodes(connectId, jewelId);
                else
                    _tree.ConnectNodes(classStartIds[cls], jewelId);
            }

            _built = true;
            Godot.GD.Print($"[PassiveTreeBuilder] Built tree with {_tree.Nodes.Count} nodes");
        }

        private static Vector2 HexPos(int index, float radius)
        {
            float angle = index * Mathf.Pi * 2f / 6f - Mathf.Pi / 2f;
            return new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }
    }
}
