using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Central registry for all active players. Replaces single-player
    /// ServiceLocator lookups to support local co-op.
    /// </summary>
    public static class PlayerManager
    {
        private static readonly List<PlayerController> _players = new();

        public static IReadOnlyList<PlayerController> Players => _players;
        public static int PlayerCount => _players.Count;

        /// <summary>Player 1 (keyboard+mouse). Null if no players.</summary>
        public static PlayerController P1 => _players.Count > 0 ? _players[0] : null;

        /// <summary>Player 2 (gamepad). Null if single-player.</summary>
        public static PlayerController P2 => _players.Count > 1 ? _players[1] : null;

        public static void Register(PlayerController player)
        {
            if (!_players.Contains(player))
            {
                _players.Add(player);
                GD.Print($"[PlayerManager] Registered player {_players.Count} (index {player.PlayerIndex})");
            }
        }

        public static void Unregister(PlayerController player)
        {
            _players.Remove(player);
        }

        public static void Clear()
        {
            _players.Clear();
        }

        /// <summary>
        /// Find the nearest alive player to a world position.
        /// Used by enemy AI for targeting.
        /// </summary>
        public static PlayerController GetNearestPlayer(Vector3 worldPos)
        {
            PlayerController nearest = null;
            float bestDist = float.MaxValue;

            foreach (var p in _players)
            {
                if (p == null || !GodotObject.IsInstanceValid(p)) continue;
                if (!p.Health.IsAlive) continue;

                float dist = worldPos.FlatDistance(p.GlobalPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = p;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find the nearest alive player within a max range.
        /// Returns null if no player is in range.
        /// </summary>
        public static PlayerController GetNearestPlayerInRange(Vector3 worldPos, float maxRange)
        {
            var nearest = GetNearestPlayer(worldPos);
            if (nearest == null) return null;

            float dist = worldPos.FlatDistance(nearest.GlobalPosition);
            return dist <= maxRange ? nearest : null;
        }

        /// <summary>
        /// Get the midpoint between all alive players. Used by camera.
        /// Falls back to P1 position if only one player.
        /// </summary>
        public static Vector3 GetPlayerMidpoint()
        {
            var sum = Vector3.Zero;
            int count = 0;

            foreach (var p in _players)
            {
                if (p == null || !GodotObject.IsInstanceValid(p)) continue;
                if (!p.Health.IsAlive) continue;
                sum += p.GlobalPosition;
                count++;
            }

            if (count == 0)
            {
                // Fall back to any player, even dead
                foreach (var p in _players)
                {
                    if (p != null && GodotObject.IsInstanceValid(p))
                        return p.GlobalPosition;
                }
                return Vector3.Zero;
            }

            return sum / count;
        }

        /// <summary>
        /// Get the max distance between any two alive players.
        /// Used by camera for adaptive zoom.
        /// </summary>
        public static float GetPlayerSpread()
        {
            if (_players.Count < 2) return 0f;

            float maxDist = 0f;
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i] == null || !GodotObject.IsInstanceValid(_players[i])) continue;
                for (int j = i + 1; j < _players.Count; j++)
                {
                    if (_players[j] == null || !GodotObject.IsInstanceValid(_players[j])) continue;
                    float dist = _players[i].GlobalPosition.FlatDistance(_players[j].GlobalPosition);
                    if (dist > maxDist) maxDist = dist;
                }
            }

            return maxDist;
        }
    }
}
