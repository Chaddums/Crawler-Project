using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Small minimap in the top-right corner showing the dungeon room layout.
    /// Rooms drawn as colored rectangles, corridors as lines.
    /// Player position shown as a blinking dot.
    /// Reads color overrides from UIConfigLoader (edited via the UI Designer tab).
    /// </summary>
    public partial class MinimapUI : Control
    {
        private const float CELL_SIZE = 18f;
        private const float PADDING = 8f;

        private IReadOnlyDictionary<Vector2I, RoomType> _roomGrid;
        private HashSet<Vector2I> _discoveredRooms;
        private Vector2I _gridCenter;
        private float _blinkTimer;

        private Color _bgColor;
        private float _bgOpacity;
        private Color _playerColor;

        public override void _Ready()
        {
            _bgColor = UIConfigLoader.GetColor("HUD", "Minimap", "BgColor", new Color(0.05f, 0.05f, 0.08f));
            _bgOpacity = UIConfigLoader.GetFloat("HUD", "Minimap", "BgOpacity", 0.85f);
            _playerColor = UIConfigLoader.GetColor("HUD", "Minimap", "PlayerColor", new Color(0.2f, 0.8f, 0.3f));

            GameEvents.OnFogUpdated += OnFogUpdated;
        }

        public override void _ExitTree()
        {
            GameEvents.OnFogUpdated -= OnFogUpdated;
        }

        private void OnFogUpdated(HashSet<Vector2I> discovered)
        {
            _discoveredRooms = discovered;
            QueueRedraw();
        }

        public void SetRoomGrid(IReadOnlyDictionary<Vector2I, RoomType> grid)
        {
            _roomGrid = grid;

            // Find center for offset
            if (grid != null && grid.Count > 0)
            {
                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;
                foreach (var pos in grid.Keys)
                {
                    if (pos.X < minX) minX = pos.X;
                    if (pos.X > maxX) maxX = pos.X;
                    if (pos.Y < minY) minY = pos.Y;
                    if (pos.Y > maxY) maxY = pos.Y;
                }
                _gridCenter = new Vector2I((minX + maxX) / 2, (minY + maxY) / 2);
            }

            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            _blinkTimer += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_roomGrid == null || _roomGrid.Count == 0) return;

            // Background
            var bgRect = new Rect2(Vector2.Zero, Size);
            DrawRect(bgRect, new Color(_bgColor.R, _bgColor.G, _bgColor.B, _bgOpacity));
            DrawRect(bgRect, new Color(0.4f, 0.35f, 0.2f), false, 1.5f);

            var center = Size / 2f;

            // Draw corridors (connections between adjacent discovered rooms)
            foreach (var pos in _roomGrid.Keys)
            {
                if (!IsRoomDiscovered(pos)) continue;

                var neighbors = new Vector2I[]
                {
                    pos + new Vector2I(1, 0),
                    pos + new Vector2I(0, 1)
                };

                foreach (var n in neighbors)
                {
                    if (!_roomGrid.ContainsKey(n)) continue;
                    if (!IsRoomDiscovered(n)) continue;
                    var fromScreen = GridToMinimap(pos, center);
                    var toScreen = GridToMinimap(n, center);
                    DrawLine(fromScreen, toScreen, new Color(0.4f, 0.4f, 0.4f), 2f);
                }
            }

            // Draw discovered rooms only
            foreach (var (pos, type) in _roomGrid)
            {
                if (!IsRoomDiscovered(pos)) continue;

                var screenPos = GridToMinimap(pos, center);
                var halfCell = CELL_SIZE / 2f - 1f;
                var roomRect = new Rect2(screenPos.X - halfCell, screenPos.Y - halfCell,
                    halfCell * 2, halfCell * 2);

                DrawRect(roomRect, GetMinimapRoomColor(type));
                DrawRect(roomRect, new Color(0.5f, 0.5f, 0.5f), false, 1f);
            }

            // Draw player positions with direction arrows
            float alpha = 0.5f + 0.5f * Mathf.Sin(_blinkTimer * 4f);
            foreach (var player in PlayerManager.Players)
            {
                if (player == null || !GodotObject.IsInstanceValid(player)) continue;
                var playerGrid = WorldToGrid(player.GlobalPosition);
                var playerScreen = GridToMinimap(playerGrid, center);

                var dotColor = player.PlayerIndex == 0
                    ? new Color(1, 1, 1, alpha)
                    : new Color(_playerColor.R, _playerColor.G, _playerColor.B, alpha);
                DrawCircle(playerScreen, 4f, dotColor);

                // Direction arrow — get facing direction from the player's forward vector
                var forward = -player.GlobalTransform.Basis.Z;
                // Project XZ to minimap 2D (X->right, Z->down on minimap)
                var dir2D = new Vector2(forward.X, forward.Z);
                if (dir2D.LengthSquared() > 0.01f)
                {
                    dir2D = dir2D.Normalized();
                    float arrowLen = 8f;
                    var tip = playerScreen + dir2D * arrowLen;
                    // Draw arrow line
                    DrawLine(playerScreen, tip, dotColor, 2f);
                    // Draw arrowhead wings
                    var wingL = tip - dir2D.Rotated(0.5f) * 4f;
                    var wingR = tip - dir2D.Rotated(-0.5f) * 4f;
                    DrawLine(tip, wingL, dotColor, 1.5f);
                    DrawLine(tip, wingR, dotColor, 1.5f);
                }
            }
        }

        private Vector2 GridToMinimap(Vector2I gridPos, Vector2 center)
        {
            float x = (gridPos.X - _gridCenter.X) * CELL_SIZE + center.X;
            float y = (gridPos.Y - _gridCenter.Y) * CELL_SIZE + center.Y;
            return new Vector2(x, y);
        }

        private static Vector2I WorldToGrid(Vector3 worldPos)
        {
            return new Vector2I(
                Mathf.RoundToInt(worldPos.X / Constants.ROOM_SPACING),
                Mathf.RoundToInt(worldPos.Z / Constants.ROOM_SPACING));
        }

        private bool IsRoomDiscovered(Vector2I pos)
        {
            // If fog hasn't been initialized yet, show nothing
            if (_discoveredRooms == null) return false;
            return _discoveredRooms.Contains(pos);
        }

        private static Color GetMinimapRoomColor(RoomType type) => type switch
        {
            RoomType.Entrance => new Color(0.3f, 0.6f, 0.3f),
            RoomType.Boss => UIConfigLoader.GetColor("HUD", "Minimap", "BossColor", new Color(0.7f, 0.2f, 0.2f)),
            RoomType.Treasure => new Color(0.7f, 0.6f, 0.2f),
            RoomType.Shop => new Color(0.2f, 0.5f, 0.2f),
            RoomType.SafeRoom => new Color(0.2f, 0.3f, 0.6f),
            RoomType.Event => new Color(0.5f, 0.25f, 0.65f),
            RoomType.Puzzle => new Color(0.65f, 0.4f, 0.1f),
            RoomType.Megabonk => new Color(0.7f, 0.1f, 0.4f),
            _ => UIConfigLoader.GetColor("HUD", "Minimap", "RoomColor", new Color(0.25f, 0.22f, 0.2f)),
        };
    }
}
