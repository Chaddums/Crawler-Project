using Godot;
using System.Collections.Generic;
using System.Text;

namespace JunkbotArena
{
    /// <summary>
    /// Captures a snapshot of the current scene state for bug reports.
    /// Includes player position, room info, and nearby named nodes with
    /// their types, positions, and collision shape details.
    /// </summary>
    public static class SceneContext
    {
        /// <summary>
        /// Capture full scene context as a formatted string for bug reports.
        /// </summary>
        public static string Capture(SceneTree tree)
        {
            var sb = new StringBuilder();

            // Game state
            var gm = GameManager.Instance;
            if (gm != null)
            {
                sb.AppendLine($"State: {gm.CurrentState}");
                sb.AppendLine($"Sector: {gm.CurrentSector}, Area: {gm.CurrentArea}");
                sb.AppendLine($"Ascension: {MetaSaveManager.Data.AscensionRank}");
            }
            else
            {
                sb.AppendLine("State: No GameManager");
            }

            // Player info
            var player = PlayerManager.P1;
            if (player != null)
            {
                sb.AppendLine($"Player Pos: ({player.GlobalPosition.X:F1}, {player.GlobalPosition.Y:F1}, {player.GlobalPosition.Z:F1})");
                sb.AppendLine($"Player Level: {player.Stats?.Level ?? 0}");
                sb.AppendLine($"Player HP: {player.Health?.CurrentHealth:F0}/{player.Health?.MaxHealth:F0}");
                var gm2 = GameManager.Instance;
                if (gm2 != null)
                    sb.AppendLine($"Bot Frame: {gm2.SelectedClass}");
            }
            else
            {
                sb.AppendLine("Player: Not spawned");
            }

            sb.AppendLine();

            // Find the room the player is in
            var roomNode = FindCurrentRoom(tree, player);
            if (roomNode != null)
            {
                sb.AppendLine($"--- Room: {roomNode.Name} ---");
                sb.AppendLine($"Room Pos: ({roomNode.GlobalPosition.X:F1}, {roomNode.GlobalPosition.Y:F1}, {roomNode.GlobalPosition.Z:F1})");
                sb.AppendLine();

                // List all named nodes in the room
                int count = 0;
                CollectNodeInfo(roomNode, sb, 0, ref count, maxDepth: 4, maxNodes: 60);
                sb.AppendLine($"--- {count} nodes listed ---");
            }
            else
            {
                // No room found — list nearby 3D nodes
                sb.AppendLine("--- Nearby Nodes (no room parent found) ---");
                if (player != null)
                {
                    int count = 0;
                    ListNearbyNodes(tree, player.GlobalPosition, sb, ref count, radius: 40f);
                    sb.AppendLine($"--- {count} nearby nodes ---");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Capture a compact one-liner for log/status messages.
        /// </summary>
        public static string CaptureShort(SceneTree tree)
        {
            var gm = GameManager.Instance;
            var player = PlayerManager.P1;

            string state = gm != null ? $"S{gm.CurrentSector}A{gm.CurrentArea}" : "?";
            string pos = player != null
                ? $"({player.GlobalPosition.X:F0},{player.GlobalPosition.Y:F0},{player.GlobalPosition.Z:F0})"
                : "(no player)";

            return $"{state} {pos}";
        }

        private static Node3D FindCurrentRoom(SceneTree tree, PlayerController player)
        {
            if (player == null) return null;

            // Walk up the tree from player to find a node with "Room" in its name
            Node current = player.GetParent();
            while (current != null)
            {
                if (current is Node3D n3d && n3d.Name.ToString().Contains("Room"))
                    return n3d;
                current = current.GetParent();
            }

            // Fallback: search the scene for Room nodes near the player
            var root = tree.CurrentScene;
            if (root == null) return null;

            Node3D closest = null;
            float closestDist = float.MaxValue;
            FindRoomNodes(root, player.GlobalPosition, ref closest, ref closestDist);
            return closest;
        }

        private static void FindRoomNodes(Node node, Vector3 playerPos, ref Node3D closest, ref float closestDist)
        {
            if (node is Node3D n3d && n3d.Name.ToString().StartsWith("Room_"))
            {
                float dist = n3d.GlobalPosition.DistanceTo(playerPos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = n3d;
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    FindRoomNodes(n, playerPos, ref closest, ref closestDist);
            }
        }

        private static void CollectNodeInfo(Node node, StringBuilder sb, int depth, ref int count, int maxDepth, int maxNodes)
        {
            if (depth > maxDepth || count >= maxNodes) return;

            if (node is Node3D n3d && depth > 0)
            {
                string name = node.Name.ToString();
                bool isInteresting = node is StaticBody3D || node is Area3D ||
                    node is MeshInstance3D || node is Marker3D ||
                    !name.StartsWith("@");

                if (isInteresting)
                {
                    string indent = new string(' ', depth * 2);
                    string className = node.GetClass();
                    var pos = n3d.GlobalPosition;
                    string posStr = $"({pos.X:F1},{pos.Y:F1},{pos.Z:F1})";

                    string extra = "";
                    foreach (var child in node.GetChildren())
                    {
                        if (child is CollisionShape3D col && col.Shape != null)
                        {
                            extra = GetShapeInfo(col.Shape);
                            break;
                        }
                    }

                    sb.AppendLine($"{indent}{name} [{className}] {posStr}{extra}");
                    count++;
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    CollectNodeInfo(n, sb, depth + 1, ref count, maxDepth, maxNodes);
            }
        }

        private static void ListNearbyNodes(SceneTree tree, Vector3 center, StringBuilder sb, ref int count, float radius)
        {
            var root = tree.CurrentScene;
            if (root == null) return;
            ListNearbyRecursive(root, center, sb, ref count, radius, 0);
        }

        private static void ListNearbyRecursive(Node node, Vector3 center, StringBuilder sb, ref int count, float radius, int depth)
        {
            if (count >= 40 || depth > 6) return;

            if (node is Node3D n3d)
            {
                float dist = n3d.GlobalPosition.DistanceTo(center);
                if (dist <= radius)
                {
                    string name = node.Name.ToString();
                    bool isInteresting = node is StaticBody3D || node is Area3D || !name.StartsWith("@");

                    if (isInteresting)
                    {
                        var pos = n3d.GlobalPosition;
                        string extra = "";
                        foreach (var child in node.GetChildren())
                        {
                            if (child is CollisionShape3D col && col.Shape != null)
                            {
                                extra = GetShapeInfo(col.Shape);
                                break;
                            }
                        }
                        sb.AppendLine($"  {name} [{node.GetClass()}] ({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) d={dist:F1}{extra}");
                        count++;
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    ListNearbyRecursive(n, center, sb, ref count, radius, depth + 1);
            }
        }

        private static string GetShapeInfo(Shape3D shape)
        {
            if (shape is BoxShape3D box)
                return $" Box({box.Size.X:F1},{box.Size.Y:F1},{box.Size.Z:F1})";
            if (shape is CylinderShape3D cyl)
                return $" Cyl(r={cyl.Radius:F1},h={cyl.Height:F1})";
            if (shape is SphereShape3D sph)
                return $" Sphere(r={sph.Radius:F1})";
            return "";
        }
    }
}
