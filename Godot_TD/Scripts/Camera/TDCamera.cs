using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Top-down camera with player-follow, WASD pan fallback, scroll zoom,
    /// and right-click orbit.
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

        // Orbit
        private bool _orbiting;
        private float _orbitYaw;

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
            float dt = (float)delta;

            // Shake decay
            if (_shakeTimer > 0)
            {
                _shakeTimer -= dt;
                if (_shakeTimer <= 0)
                {
                    _shakeIntensity = 0;
                    _shakeDuration = 0;
                }
            }

            // Check if player exists and is alive
            bool playerActive = false;
            VinePlayer player = null;
            if (ServiceLocator.TryGet<VinePlayer>(out var p) && p.IsAlive)
            {
                player = p;
                var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
                // Follow player during wave phases
                playerActive = phase == GamePhase.Wave || phase == GamePhase.WaveComplete;
            }

            if (playerActive && player != null)
            {
                // Player-follow mode — lerp to player position
                _targetPosition = _targetPosition.Lerp(player.GlobalPosition, dt * 5f);
            }
            else
            {
                // WASD pan mode (build phase, or no player)
                var input = Vector3.Zero;
                if (Input.IsActionPressed("camera_pan_up")) input.Z -= 1;
                if (Input.IsActionPressed("camera_pan_down")) input.Z += 1;
                if (Input.IsActionPressed("camera_pan_left")) input.X -= 1;
                if (Input.IsActionPressed("camera_pan_right")) input.X += 1;

                if (input.LengthSquared() > 0)
                {
                    input = input.Normalized() * PanSpeed * dt;
                    _targetPosition += input;
                }
            }

            // Clamp target
            float pad = 5f;
            _targetPosition.X = Mathf.Clamp(_targetPosition.X, -pad, _mapWidth + pad);
            _targetPosition.Z = Mathf.Clamp(_targetPosition.Z, -pad, _mapHeight + pad);

            // Sample terrain height so camera follows terrain elevation
            if (ServiceLocator.TryGet<VineGrid>(out var grid))
                _targetPosition.Y = grid.GetWorldHeight(_targetPosition.X, _targetPosition.Z);

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
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    _orbiting = mb.Pressed;
                }
            }
            else if (@event is InputEventMouseMotion mm && _orbiting)
            {
                _orbitYaw += mm.Relative.X * 0.005f;
                ApplyTransform();
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

            // Apply orbit yaw rotation
            float orbX = Mathf.Sin(_orbitYaw) * offset;
            float orbZ = Mathf.Cos(_orbitYaw) * offset;

            var basePos = _targetPosition + new Vector3(orbX, height, orbZ);

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
