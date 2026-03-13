using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Top-down camera with WASD pan, scroll zoom, and edge-of-map clamping.
    /// </summary>
    public partial class TDCamera : Camera3D
    {
        [Export] public float PanSpeed { get; set; } = Constants.CAMERA_PAN_SPEED;
        [Export] public float ZoomSpeed { get; set; } = 2f;

        private float _zoom = Constants.CAMERA_HEIGHT;
        private Vector3 _targetPosition;
        private float _mapWidth;
        private float _mapHeight;

        public override void _Ready()
        {
            // Default position — will be overridden by BattleScene
            _targetPosition = new Vector3(
                Constants.DEFAULT_MAP_WIDTH * Constants.CELL_SIZE / 2f,
                0,
                Constants.DEFAULT_MAP_HEIGHT * Constants.CELL_SIZE / 2f
            );
            _mapWidth = Constants.DEFAULT_MAP_WIDTH * Constants.CELL_SIZE;
            _mapHeight = Constants.DEFAULT_MAP_HEIGHT * Constants.CELL_SIZE;

            ApplyTransform();
        }

        public void SetMapBounds(float width, float height)
        {
            _mapWidth = width;
            _mapHeight = height;
            _targetPosition = new Vector3(width / 2f, 0, height / 2f);
            ApplyTransform();
        }

        public override void _Process(double delta)
        {
            var input = Vector3.Zero;

            if (Input.IsActionPressed("camera_pan_up")) input.Z -= 1;
            if (Input.IsActionPressed("camera_pan_down")) input.Z += 1;
            if (Input.IsActionPressed("camera_pan_left")) input.X -= 1;
            if (Input.IsActionPressed("camera_pan_right")) input.X += 1;

            if (input.LengthSquared() > 0)
            {
                input = input.Normalized() * PanSpeed * (float)delta;
                _targetPosition += input;

                // Clamp to map bounds with some padding
                float pad = 5f;
                _targetPosition.X = Mathf.Clamp(_targetPosition.X, -pad, _mapWidth + pad);
                _targetPosition.Z = Mathf.Clamp(_targetPosition.Z, -pad, _mapHeight + pad);
            }

            ApplyTransform();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    _zoom = Mathf.Max(Constants.CAMERA_MIN_ZOOM, _zoom - ZoomSpeed);
                    ApplyTransform();
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    _zoom = Mathf.Min(Constants.CAMERA_MAX_ZOOM, _zoom + ZoomSpeed);
                    ApplyTransform();
                }
            }
        }

        private void ApplyTransform()
        {
            float angleRad = Mathf.DegToRad(Constants.CAMERA_ANGLE);
            float height = _zoom * Mathf.Sin(angleRad);
            float offset = _zoom * Mathf.Cos(angleRad);

            Position = _targetPosition + new Vector3(0, height, offset);
            LookAt(_targetPosition, Vector3.Up);
        }
    }
}
