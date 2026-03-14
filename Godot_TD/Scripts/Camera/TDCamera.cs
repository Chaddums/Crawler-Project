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

        // Screen shake
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;
        private RandomNumberGenerator _shakeRng = new();

        public override void _Ready()
        {
            _targetPosition = new Vector3(
                Constants.DEFAULT_MAP_WIDTH * Constants.CELL_SIZE / 2f,
                0,
                Constants.DEFAULT_MAP_HEIGHT * Constants.CELL_SIZE / 2f
            );
            _mapWidth = Constants.DEFAULT_MAP_WIDTH * Constants.CELL_SIZE;
            _mapHeight = Constants.DEFAULT_MAP_HEIGHT * Constants.CELL_SIZE;

            ApplyTransform();
            ServiceLocator.Register(this);
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
            // Shake decay
            if (_shakeTimer > 0)
            {
                _shakeTimer -= (float)delta;
                if (_shakeTimer <= 0)
                {
                    _shakeIntensity = 0;
                    _shakeDuration = 0;
                }
            }

            var input = Vector3.Zero;

            if (Input.IsActionPressed("camera_pan_up")) input.Z -= 1;
            if (Input.IsActionPressed("camera_pan_down")) input.Z += 1;
            if (Input.IsActionPressed("camera_pan_left")) input.X -= 1;
            if (Input.IsActionPressed("camera_pan_right")) input.X += 1;

            if (input.LengthSquared() > 0)
            {
                input = input.Normalized() * PanSpeed * (float)delta;
                _targetPosition += input;

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

        /// <summary>
        /// Trigger screen shake. Stacks with existing shake by taking the max.
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeTimer = _shakeDuration;
        }

        private void ApplyTransform()
        {
            float angleRad = Mathf.DegToRad(Constants.CAMERA_ANGLE);
            float height = _zoom * Mathf.Sin(angleRad);
            float offset = _zoom * Mathf.Cos(angleRad);

            var basePos = _targetPosition + new Vector3(0, height, offset);

            // Apply shake offset
            if (_shakeTimer > 0)
            {
                float decay = _shakeTimer / _shakeDuration;
                float shakeX = _shakeRng.RandfRange(-1f, 1f) * _shakeIntensity * decay;
                float shakeY = _shakeRng.RandfRange(-1f, 1f) * _shakeIntensity * decay * 0.5f;
                basePos += new Vector3(shakeX, shakeY, 0);
            }

            Position = basePos;
            LookAt(_targetPosition, Vector3.Up);
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<TDCamera>();
        }
    }
}
