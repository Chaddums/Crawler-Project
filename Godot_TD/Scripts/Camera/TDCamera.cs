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

        // Flyover / Intro sequence
        private bool _flyoverActive;
        private float _flyoverTime;
        private float _flyoverDuration;
        private float _flyoverStartYaw;
        private Vector3 _introFocusPoint;  // Spire ground position

        // Intro phase timings (fractions of total duration)
        // Phase 1: 0.0–0.25  Wide establishing shot, slight zoom toward Spire
        // Phase 2: 0.25–0.55 Track Spire falling (synced with SlamIn)
        // Phase 3: 0.55–0.72 Post-impact settle, pull back
        // Phase 4: 0.72–1.0  Ease to gameplay position, BIT emerges

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

        /// <summary>
        /// Start the intro camera sequence focused on the Spire landing point.
        /// </summary>
        public void StartFlyover(float duration = 5.5f)
        {
            _flyoverActive = true;
            _flyoverTime = 0f;
            _flyoverDuration = duration;
            _flyoverStartYaw = _orbitYaw;

            // Focus on the Spire location
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null)
            {
                _introFocusPoint = grid.GridToWorld(grid.ExitPoint);
                _introFocusPoint.Y = grid.GetWorldHeight(_introFocusPoint.X, _introFocusPoint.Z);
            }
            else
            {
                _introFocusPoint = new Vector3(_mapWidth / 2f, 0, _mapHeight / 2f);
            }

            // Start zoomed out, looking at the Spire area
            _zoom = Constants.CAMERA_MAX_ZOOM * 0.45f;
            _targetPosition = _introFocusPoint;
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

            // ── Intro sequence: Spire slam + BIT emergence ──
            if (_flyoverActive)
            {
                _flyoverTime += dt;
                float t = Mathf.Clamp(_flyoverTime / _flyoverDuration, 0f, 1f);

                float wideZoom = Constants.CAMERA_MAX_ZOOM * 0.45f;
                float closeZoom = Constants.CAMERA_HEIGHT * 0.6f;
                float gameplayZoom = Constants.CAMERA_HEIGHT;

                // Zoom curve: hold wide for ~0.5s, then smooth zoom to close and stay
                if (t < 0.09f)
                {
                    // 0–9% (~0.5s): hold wide — watch the Spire fall from a distance
                    _zoom = wideZoom;
                }
                else
                {
                    // 9–100%: smooth zoom from wide to close
                    float zt = (t - 0.09f) / 0.91f;
                    float eased = zt * zt; // Ease-in
                    _zoom = Mathf.Lerp(wideZoom, closeZoom, eased);
                }

                // Orbit: gentle drift, then hold
                float orbitT = Mathf.Clamp(t / 0.72f, 0f, 1f);
                _orbitYaw = _flyoverStartYaw + orbitT * Mathf.DegToRad(20f);

                // Target position: always the Spire ground point
                _targetPosition = _introFocusPoint;

                if (ServiceLocator.TryGet<VineGrid>(out var flyGrid))
                {
                    float terrainY = flyGrid.GetWorldHeight(_targetPosition.X, _targetPosition.Z);
                    _targetPosition.Y = Mathf.Max(_targetPosition.Y, terrainY);
                }

                ApplyTransform();

                if (t >= 1f)
                {
                    _flyoverActive = false;
                    _zoom = closeZoom;
                    ApplyTransform();
                }
                return;
            }

            // Always follow player when alive
            if (ServiceLocator.TryGet<VinePlayer>(out var p) && p.IsAlive)
            {
                _targetPosition = _targetPosition.Lerp(p.GlobalPosition, dt * 5f);
            }
            else
            {
                // WASD pan fallback (no player or player dead)
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
                // Any key/click skips intro
                if ((@event is InputEventKey key && key.Pressed && !key.Echo)
                    || (@event is InputEventMouseButton skip && skip.Pressed))
                {
                    _flyoverActive = false;
                    _orbitYaw = _flyoverStartYaw;
                    _zoom = Constants.CAMERA_HEIGHT;
                    _targetPosition = _introFocusPoint;
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
