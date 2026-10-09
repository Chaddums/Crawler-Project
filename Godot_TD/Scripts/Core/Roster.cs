using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// This frame's enemies and towers with their positions, read from the scene once a frame and
    /// shared by everything that searches them. Each tower and enemy used to fetch the group and
    /// every member's position for itself, many times a frame: with 40 turrets and 120 enemies
    /// that was most of the frame's script time. Entries can die or be freed during the frame, so
    /// callers check <c>IsAlive</c> / <see cref="GodotObject.IsInstanceValid"/> as before.
    /// </summary>
    public static class Roster
    {
        private static ulong _enemyFrame = ulong.MaxValue, _nodeFrame = ulong.MaxValue;
        /// <summary>Changes every process frame and every physics step.</summary>
        private static ulong Stamp => Engine.GetProcessFrames() * 1000003UL + Engine.GetPhysicsFrames();
        // Rebuilt as new lists, never cleared, so a loop over this frame's list that spawns
        // something (and so rebuilds it) keeps walking the list it started with
        private static List<VineEnemy> _enemies = new();
        private static List<Vector3> _enemyPos = new();
        private static List<VineNode> _nodes = new();
        private static List<Vector3> _nodePos = new();

        /// <summary>Enemies in the scene this frame (alive or dying), with <see cref="EnemyPositions"/> in step.</summary>
        public static List<VineEnemy> Enemies(SceneTree tree)
        {
            ulong f = Stamp;
            if (f != _enemyFrame || tree == null)
            {
                _enemyFrame = f;
                _enemies = new List<VineEnemy>(_enemies.Count + 8);
                _enemyPos = new List<Vector3>(_enemies.Capacity);
                if (tree != null)
                    foreach (var n in tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
                        if (n is VineEnemy e && e.IsInsideTree())
                        {
                            _enemies.Add(e);
                            _enemyPos.Add(e.GlobalPosition);
                        }
            }
            return _enemies;
        }

        public static List<Vector3> EnemyPositions(SceneTree tree)
        {
            Enemies(tree);
            return _enemyPos;
        }

        /// <summary>Placed nodes (towers, walls, relays) this frame, with <see cref="NodePositions"/> in step.</summary>
        public static List<VineNode> Nodes(SceneTree tree)
        {
            ulong f = Stamp;
            if (f != _nodeFrame || tree == null)
            {
                _nodeFrame = f;
                _nodes = new List<VineNode>(_nodes.Count + 8);
                _nodePos = new List<Vector3>(_nodes.Capacity);
                if (tree != null)
                    foreach (var n in tree.GetNodesInGroup(Constants.GROUP_VINE_NODE))
                        if (n is VineNode v && v.IsInsideTree())
                        {
                            _nodes.Add(v);
                            _nodePos.Add(v.GlobalPosition);
                        }
            }
            return _nodes;
        }

        public static List<Vector3> NodePositions(SceneTree tree)
        {
            Nodes(tree);
            return _nodePos;
        }

        /// <summary>Forget the lists (an enemy spawned or a node was placed, so everyone sees it at once).</summary>
        public static void Invalidate()
        {
            _enemyFrame = ulong.MaxValue;
            _nodeFrame = ulong.MaxValue;
        }
    }
}
