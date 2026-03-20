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

        // Flyover
        private bool _flyoverActive;
        private float _flyoverTime;
        private float _flyoverDuration;
        private float _flyoverStartYaw;

        public bool FlyoverActive => _flyoverActive;

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

        public void StartFlyover(float duration = 5f)
        {
            _flyoverActive = true;
            _flyoverTime = 0f;
            _flyoverDuration = duration;
            _flyoverStartYaw = _orbitYaw;
            _zoom = Constants.CAMERA_MAX_ZOOM * 0.6f;
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

            // ── Flyover mode ──
            if (_flyoverActive)
            {
                _flyoverTime += dt;
                float t = Mathf.Clamp(_flyoverTime / _flyoverDuration, 0f, 1f);

                // Orbit ~270° over the duration
                _orbitYaw = _flyoverStartYaw + t * Mathf.DegToRad(270f);

                // Zoom from wide to normal over duration
                float startZoom = Constants.CAMERA_MAX_ZOOM * 0.6f;
                _zoom = Mathf.Lerp(startZoom, Constants.CAMERA_HEIGHT, t * t); // Ease-in

                // Pan from map center toward player in the last 30% of the flyover
                var mapCenter = new Vector3(_mapWidth / 2f, 0, _mapHeight / 2f);
                Vector3 endTarget = mapCenter;
                if (ServiceLocator.TryGet<VinePlayer>(out var flyPlayer))
                    endTarget = flyPlayer.GlobalPosition;
                float panT = Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f); // 0 until 70%, then ramps to 1
                _targetPosition = mapCenter.Lerp(endTarget, panT * panT);

                if (ServiceLocator.TryGet<VineGrid>(out var flyGrid))
                    _targetPosition.Y = flyGrid.GetWorldHeight(_targetPosition.X, _targetPosition.Z);

                ApplyTransform();

                if (t >= 1f)
                {
                    _flyoverActive = false;
                    _orbitYaw = _flyoverStartYaw; // Reset to original orientation
                    _zoom = Constants.CAMERA_HEIGHT;
                    _targetPosition = endTarget;
                    ApplyTransform();
                }
                return;
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
                    // Rotate input by orbit yaw so WASD is always screen-relative
                    float sin = Mathf.Sin(_orbitYaw);
                    float cos = Mathf.Cos(_orbitYaw);
                    var rotated = new Vector3(
                        input.X * cos + input.Z * sin,
                        0,
                        -input.X * sin + input.Z * cos);
                    _targetPosition += rotated;
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
            if (_flyoverActive)
            {
                // Any key/click skips flyover
                if ((@event is InputEventKey key && key.Pressed && !key.Echo)
                    || (@event is InputEventMouseButton skip && skip.Pressed))
                {
                    _flyoverActive = false;
                    _orbitYaw = _flyoverStartYaw; // Reset to original orientation
                    _zoom = Constants.CAMERA_HEIGHT;
                    ApplyTransform();
                }
                return;
            }

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
