using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// 2D minimap Control with _Draw() — shows colored grid cells,
    /// entry/exit markers, and optional path overlay.
    /// </summary>
    public partial class LevelEditorGrid2D : Control
    {
        public LevelEditorScene Editor { get; set; }
        public bool ShowPaths { get; set; }

        private const float CellSize = 3f;
        private const float Padding = 2f;

        // Cell colors
        private static readonly Color EmptyColor = new(0.12f, 0.12f, 0.15f);
        private static readonly Color WallColor = new(0.4f, 0.35f, 0.3f);
        private static readonly Color ElevatedColor = new(0.25f, 0.2f, 0.35f);
        private static readonly Color ChannelColor = new(0.1f, 0.15f, 0.25f);
        private static readonly Color DataStreamColor = new(0f, 0.6f, 0.7f, 0.6f);
        private static readonly Color EntryColor = new(0.2f, 0.8f, 0.3f);
        private static readonly Color ExitColor = new(0.9f, 0.3f, 0.2f);
        private static readonly Color PropColor = new(0.5f, 0.4f, 0.3f);
        private static readonly Color NodeColor = new(0f, 0.7f, 0.9f);
        private static readonly Color PathColor = new(0f, 0.85f, 0.95f, 0.5f);

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(
                Constants.VINE_MAP_WIDTH * CellSize + Padding * 2,
                Constants.VINE_MAP_HEIGHT * CellSize + Padding * 2);
        }

        public override void _Draw()
        {
            var grid = Editor?.Grid;
            if (grid == null) return;

            int w = grid.Width;
            int h = grid.Height;

            // Background
            DrawRect(new Rect2(0, 0, Size.X, Size.Y), new Color(0.06f, 0.06f, 0.08f));

            // Draw cells
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var cell = grid.GetCell(x, y);
                var color = cell switch
                {
                    VineCellType.Wall => WallColor,
                    VineCellType.Elevated => ElevatedColor,
                    VineCellType.Channel => ChannelColor,
                    VineCellType.DataStream => DataStreamColor,
                    VineCellType.Entry => EntryColor,
                    VineCellType.Exit => ExitColor,
                    VineCellType.Prop => PropColor,
                    VineCellType.Node => NodeColor,
                    _ => EmptyColor
                };

                var rect = new Rect2(
                    Padding + x * CellSize,
                    Padding + y * CellSize,
                    CellSize - 1, CellSize - 1);
                DrawRect(rect, color);
            }

            // Entry markers (green circles)
            if (grid.EntryRegions != null)
            {
                foreach (var region in grid.EntryRegions)
                {
                    foreach (var c in region.Cells)
                    {
                        var center = new Vector2(
                            Padding + c.X * CellSize + CellSize * 0.5f,
                            Padding + c.Y * CellSize + CellSize * 0.5f);
                        DrawCircle(center, CellSize * 0.3f, EntryColor);
                    }
                }
            }

            // Exit marker (red circle)
            var exitPos = grid.ExitPoint;
            if (exitPos != default)
            {
                var exitCenter = new Vector2(
                    Padding + exitPos.X * CellSize + CellSize * 0.5f,
                    Padding + exitPos.Y * CellSize + CellSize * 0.5f);
                DrawCircle(exitCenter, CellSize * 0.4f, ExitColor);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                // Click on minimap -> snap camera to that world position
                int x = (int)((mb.Position.X - Padding) / CellSize);
                int y = (int)((mb.Position.Y - Padding) / CellSize);

                if (Editor?.Grid != null && Editor.Grid.InBounds(x, y))
                {
                    var worldPos = Editor.Grid.GridToWorld(x, y);
                    // Find camera and focus
                    var camera = GetViewport().GetCamera3D() as LevelEditorCamera;
                    camera?.FocusOnPosition(worldPos);
                }
            }
        }
    }
}
