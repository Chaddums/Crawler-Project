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
            const float outerRadius = 8f;
            const float innerRadius = 3.5f; // branches stop here, not at center

            var classPositions = new Dictionary<BotFrameType, Vector2>
            {
                { BotFrameType.Scrapheap, HexPos(0, outerRadius) },
                { BotFrameType.TinCan, HexPos(1, outerRadius) },
                { BotFrameType.SparkPlug, HexPos(2, outerRadius) },
                { BotFrameType.RustBucket, HexPos(3, outerRadius) },
                { BotFrameType.NoiseBox, HexPos(4, outerRadius) },
                { BotFrameType.Clunker, HexPos(5, outerRadius) },
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

            // 2. Build branch paths from each class start toward inner ring
            var classes = new[] {
                BotFrameType.Scrapheap, BotFrameType.TinCan,
                BotFrameType.SparkPlug, BotFrameType.RustBucket,
                BotFrameType.NoiseBox, BotFrameType.Clunker
            };

            var branchEndIds = new Dictionary<BotFrameType, string>();
            var branchMidIds = new Dictionary<BotFrameType, string>(); // for jewel connections

            foreach (var cls in classes)
            {
                var theme = classThemes[cls];
                var startPos = classPositions[cls];
                var endPos = startPos.Normalized() * innerRadius;
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

                // 10 nodes along a straight path from start toward inner ring
                // Notable at position 6 (requires 5 basic nodes first)
                // Second notable at position 10 (rewards full branch investment)
                for (int i = 1; i <= 10; i++)
                {
                    float t = i / 11f;
                    var pos = startPos.Lerp(endPos, t);

                    bool isNotable = (i == 6);
                    bool isEndNotable = (i == 10);
                    var nid = $"n_{cls}_{nodeId++}";

                    if (isNotable)
                    {
                        var notable = new PassiveNodeData(nid, $"{theme.primary} Notable", SkillNodeType.Notable, pos);
                        notable.AddBonus(theme.primary, ModifierType.Flat, 3f);
                        notable.AddBonus(theme.secondary, ModifierType.Flat, 2f);
                        notable.AddBonus(StatType.MaxHealth, ModifierType.Flat, 5f);
                        notable.Description = $"A notable node boosting {theme.primary} and {theme.secondary}";
                        _tree.AddNode(notable);
                        branchMidIds[cls] = nid;
                    }
                    else if (isEndNotable)
                    {
                        var notable = new PassiveNodeData(nid, $"{theme.primary} Mastery", SkillNodeType.Notable, pos);
                        notable.AddBonus(theme.primary, ModifierType.Flat, 2f);
                        notable.AddBonus(theme.secondary, ModifierType.Flat, 2f);
                        notable.AddBonus(theme.primary, ModifierType.Percent, 0.05f);
                        notable.Description = $"Deep {cls} mastery — rewards full branch investment";
                        _tree.AddNode(notable);
                    }
                    else
                    {
                        var basic = new PassiveNodeData(nid, $"+{theme.primary}", SkillNodeType.Basic, pos);
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

            // 3. Connect adjacent branch ends to form the inner ring
            for (int i = 0; i < classes.Length; i++)
            {
                int next = (i + 1) % classes.Length;
                _tree.ConnectNodes(branchEndIds[classes[i]], branchEndIds[classes[next]]);
            }

            // 4. Add cross-path bridges between adjacent classes
            // Each bridge has 2 connector nodes per side + 1 notable in the middle
            // This requires investing through the branch notable + 2 extra nodes to reach
            for (int i = 0; i < classes.Length; i++)
            {
                int next = (i + 1) % classes.Length;
                var clsA = classes[i];
                var clsB = classes[next];
                var themeA = classThemes[clsA];
                var themeB = classThemes[clsB];

                // Positions along the arc from branch A midpoint to branch B midpoint
                var posA = classPositions[clsA].Normalized() * (outerRadius * 0.55f);
                var posB = classPositions[clsB].Normalized() * (outerRadius * 0.55f);
                var midPos = (posA + posB) * 0.5f;

                // Connector nodes from branch A toward bridge
                var connA1Id = $"conn_{nodeId++}";
                var connA1Pos = posA.Lerp(midPos, 0.33f);
                var connA1 = new PassiveNodeData(connA1Id, $"+{themeA.secondary}", SkillNodeType.Basic, connA1Pos);
                connA1.AddBonus(themeA.secondary, ModifierType.Flat, 1f);
                _tree.AddNode(connA1);

                var connA2Id = $"conn_{nodeId++}";
                var connA2Pos = posA.Lerp(midPos, 0.66f);
                var connA2 = new PassiveNodeData(connA2Id, $"+{themeA.primary}", SkillNodeType.Basic, connA2Pos);
                connA2.AddBonus(themeA.primary, ModifierType.Flat, 1f);
                _tree.AddNode(connA2);

                // Connector nodes from branch B toward bridge
                var connB1Id = $"conn_{nodeId++}";
                var connB1Pos = posB.Lerp(midPos, 0.33f);
                var connB1 = new PassiveNodeData(connB1Id, $"+{themeB.secondary}", SkillNodeType.Basic, connB1Pos);
                connB1.AddBonus(themeB.secondary, ModifierType.Flat, 1f);
                _tree.AddNode(connB1);

                var connB2Id = $"conn_{nodeId++}";
                var connB2Pos = posB.Lerp(midPos, 0.66f);
                var connB2 = new PassiveNodeData(connB2Id, $"+{themeB.primary}", SkillNodeType.Basic, connB2Pos);
                connB2.AddBonus(themeB.primary, ModifierType.Flat, 1f);
                _tree.AddNode(connB2);

                // Bridge notable in the center
                var bridgeId = $"bridge_{nodeId++}";
                var bridge = new PassiveNodeData(bridgeId, "Cross-Path Notable", SkillNodeType.Notable, midPos);
                bridge.AddBonus(themeA.primary, ModifierType.Flat, 2f);
                bridge.AddBonus(themeB.primary, ModifierType.Flat, 2f);
                bridge.Description = $"A bridge between {clsA} and {clsB} paths";
                _tree.AddNode(bridge);

                // Wire: branchMid_A → conn_A1 → conn_A2 → bridge ← conn_B2 ← conn_B1 ← branchMid_B
                if (branchMidIds.ContainsKey(clsA))
                    _tree.ConnectNodes(branchMidIds[clsA], connA1Id);
                _tree.ConnectNodes(connA1Id, connA2Id);
                _tree.ConnectNodes(connA2Id, bridgeId);

                if (branchMidIds.ContainsKey(clsB))
                    _tree.ConnectNodes(branchMidIds[clsB], connB1Id);
                _tree.ConnectNodes(connB1Id, connB2Id);
                _tree.ConnectNodes(connB2Id, bridgeId);
            }

            // 5. Add generic defensive nodes in a ring at the center
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

            // 6. Add jewel sockets (one per class, off the main branch at the midpoint)
            foreach (var cls in classes)
            {
                var startPos = classPositions[cls];
                var dir = startPos.Normalized();
                var perpendicular = new Vector2(-dir.Y, dir.X);
                var jewelPos = dir * (outerRadius * 0.55f) + perpendicular * 2f;
                var jewelId = $"jewel_{cls}";
                var jewel = new PassiveNodeData(jewelId, "Jewel Socket", SkillNodeType.JewelSocket, jewelPos);
                jewel.Description = "Socket a jewel for custom bonuses";
                _tree.AddNode(jewel);

                // Connect to the branch midpoint notable
                if (branchMidIds.TryGetValue(cls, out var midId))
                    _tree.ConnectNodes(midId, jewelId);
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
