using Godot;
using System;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Wraps a HUD element preview control to make it draggable and resizable
    /// in the UIDesigner's Layout Mode. Reports position/size changes via events.
    /// </summary>
    public partial class DraggableHUDElement : Control
    {
        public string ElementName { get; set; }
        public bool Selected { get; set; }
        public bool SnapToGrid { get; set; } = true;
        public float GridSize { get; set; } = 10f;

        /// <summary>Fired when the element is dragged. Args: elementName, newPosition (in game coords 1920x1080).</summary>
        public event Action<string, Vector2> OnMoved;

        /// <summary>Fired when the element is resized. Args: elementName, newSize (in game coords).</summary>
        public event Action<string, Vector2> OnResized;

        /// <summary>Fired when the element is clicked (for selection).</summary>
        public event Action<string> OnSelected;

        private bool _dragging;
        private bool _resizing;
        private Vector2 _dragOffset;
        private const float RESIZE_HANDLE = 8f;
        private const float PREVIEW_SCALE = 0.5f; // preview is 960x540, game is 1920x1080

        // Colors
        private static readonly Color SelectionColor = new(0.9f, 0.7f, 0.2f);
        private static readonly Color HoverColor = new(0.5f, 0.5f, 0.6f);
        private static readonly Color HandleColor = new(0.9f, 0.7f, 0.2f, 0.8f);

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);

            if (Selected)
            {
                // Selection border
                DrawRect(r, SelectionColor, false, 2f);

                // Corner resize handle (bottom-right)
                var handleRect = new Rect2(Size - new Vector2(RESIZE_HANDLE, RESIZE_HANDLE),
                    new Vector2(RESIZE_HANDLE, RESIZE_HANDLE));
                DrawRect(handleRect, HandleColor, true);

                // Element name label above
                DrawString(ThemeDB.FallbackFont, new Vector2(2, -4), ElementName,
                    HorizontalAlignment.Left, -1, 10, SelectionColor);
            }
            else if (GetGlobalRect().HasPoint(GetGlobalMousePosition()))
            {
                // Hover border
                DrawRect(r, HoverColor, false, 1f);
            }
        }

        public override void _GuiInput(InputEvent ev)
        {
            if (ev is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        OnSelected?.Invoke(ElementName);
                        AcceptEvent();

                        var localPos = mb.Position;

                        // Check if clicking resize handle
                        if (Selected && localPos.X > Size.X - RESIZE_HANDLE && localPos.Y > Size.Y - RESIZE_HANDLE)
                        {
                            _resizing = true;
                            _dragOffset = Size - localPos;
                        }
                        else
                        {
                            _dragging = true;
                            _dragOffset = localPos;
                        }
                    }
                    else
                    {
                        if (_dragging)
                        {
                            _dragging = false;
                            EmitMoved();
                        }
                        if (_resizing)
                        {
                            _resizing = false;
                            EmitResized();
                        }
                    }
                }
            }
            else if (ev is InputEventMouseMotion mm)
            {
                if (_dragging)
                {
                    var newPos = Position + mm.Relative;
                    // Clamp to preview bounds (960x540)
                    newPos.X = Mathf.Clamp(newPos.X, 0, 960 - Size.X);
                    newPos.Y = Mathf.Clamp(newPos.Y, 0, 540 - Size.Y);

                    if (SnapToGrid && GridSize > 0)
                    {
                        float gs = GridSize * PREVIEW_SCALE;
                        newPos.X = Mathf.Round(newPos.X / gs) * gs;
                        newPos.Y = Mathf.Round(newPos.Y / gs) * gs;
                    }

                    Position = newPos;
                    QueueRedraw();
                    AcceptEvent();
                }
                else if (_resizing)
                {
                    var newSize = mm.Position + _dragOffset;
                    newSize.X = Mathf.Max(20, newSize.X);
                    newSize.Y = Mathf.Max(20, newSize.Y);

                    if (SnapToGrid && GridSize > 0)
                    {
                        float gs = GridSize * PREVIEW_SCALE;
                        newSize.X = Mathf.Round(newSize.X / gs) * gs;
                        newSize.Y = Mathf.Round(newSize.Y / gs) * gs;
                    }

                    Size = newSize;
                    QueueRedraw();
                    AcceptEvent();
                }
            }

            // Constantly redraw for hover effect
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            // Keyboard nudge when selected
            if (!Selected) return;

            float step = Input.IsKeyPressed(Key.Shift) ? 10f * PREVIEW_SCALE : 1f * PREVIEW_SCALE;
            bool moved = false;
            var pos = Position;

            if (Input.IsActionJustPressed("ui_left") || Input.IsKeyPressed(Key.Left))
            { pos.X -= step; moved = true; }
            if (Input.IsActionJustPressed("ui_right") || Input.IsKeyPressed(Key.Right))
            { pos.X += step; moved = true; }
            if (Input.IsActionJustPressed("ui_up") || Input.IsKeyPressed(Key.Up))
            { pos.Y -= step; moved = true; }
            if (Input.IsActionJustPressed("ui_down") || Input.IsKeyPressed(Key.Down))
            { pos.Y += step; moved = true; }

            if (moved)
            {
                pos.X = Mathf.Clamp(pos.X, 0, 960 - Size.X);
                pos.Y = Mathf.Clamp(pos.Y, 0, 540 - Size.Y);
                Position = pos;
                EmitMoved();
                QueueRedraw();
            }
        }

        private void EmitMoved()
        {
            // Convert preview coords (960x540) to game coords (1920x1080)
            var gamePos = Position / PREVIEW_SCALE;
            OnMoved?.Invoke(ElementName, gamePos);
        }

        private void EmitResized()
        {
            var gameSize = Size / PREVIEW_SCALE;
            OnResized?.Invoke(ElementName, gameSize);
        }
    }
}
