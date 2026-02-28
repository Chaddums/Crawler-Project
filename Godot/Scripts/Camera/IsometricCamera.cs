using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Isometric camera that follows the player with smooth movement and scroll-wheel zoom.
    /// Uses LookAt to always face the target — no manual rotation math needed.
    /// Includes ScreenShake for combat feedback.
    /// </summary>
    public partial class IsometricCamera : Camera3D
    {
        [Export] private float _elevationAngle = Constants.CAMERA_ANGLE_X;
        [Export] private float _azimuthAngle = Constants.CAMERA_ANGLE_Y;
        [Export] private float _cameraDistance = Constants.CAMERA_DISTANCE;

        [ExportGroup("Zoom")]
        [Export] private float _zoomSpeed = 2f;
        [Export] private float _minZoom = Constants.CAMERA_MIN_ZOOM;
        [Export] private float _maxZoom = Constants.CAMERA_MAX_ZOOM;
        [Export] private float _zoomSmoothSpeed = 10f;

        [ExportGroup("Follow")]
        [Export] private float _followSmoothSpeed = 8f;

        private Node3D _followTarget;
        private float _targetZoom;
        private Vector3 _offset;
        private ScreenShake _screenShake;

        public override void _Ready()
        {
            _targetZoom = _cameraDistance;
            CalculateOffset();

            // Add screen shake
            _screenShake = new ScreenShake();
            _screenShake.Name = "ScreenShake";
            AddChild(_screenShake);

            // Register for combat VFX access
            ServiceLocator.Register(this);

            if (_followTarget == null)
            {
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                    _followTarget = player;
            }

            // Snap to position immediately
            if (_followTarget != null)
            {
                GlobalPosition = _followTarget.GlobalPosition + _offset;
                LookAt(_followTarget.GlobalPosition, Vector3.Up);
            }
        }

        public override void _Process(double delta)
        {
            HandleZoom((float)delta);
            FollowTarget((float)delta);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("zoom_in"))
            {
                _targetZoom -= _zoomSpeed;
                _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
            }
            else if (@event.IsActionPressed("zoom_out"))
            {
                _targetZoom += _zoomSpeed;
                _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
            }
        }

        public void Initialize(Node3D target)
        {
            _followTarget = target;
            CalculateOffset();

            if (_followTarget != null)
            {
                GlobalPosition = _followTarget.GlobalPosition + _offset;
                LookAt(_followTarget.GlobalPosition, Vector3.Up);
            }
        }

        /// <summary>
        /// Trigger screen shake with given trauma (0-1).
        /// </summary>
        public void Shake(float trauma)
        {
            _screenShake?.AddTrauma(trauma);
        }

        private void CalculateOffset()
        {
            float radElev = Mathf.DegToRad(_elevationAngle);
            float radAzim = Mathf.DegToRad(_azimuthAngle);

            float height = _cameraDistance * Mathf.Sin(radElev);
            float horizontalDist = _cameraDistance * Mathf.Cos(radElev);

            _offset = new Vector3(
                horizontalDist * Mathf.Sin(radAzim),
                height,
                horizontalDist * Mathf.Cos(radAzim)
            );
        }

        private void HandleZoom(float delta)
        {
            _cameraDistance = Mathf.Lerp(_cameraDistance, _targetZoom, delta * _zoomSmoothSpeed);
            CalculateOffset();
        }

        private void FollowTarget(float delta)
        {
            if (_followTarget == null)
            {
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                    _followTarget = player;
                return;
            }

            Vector3 targetPosition = _followTarget.GlobalPosition + _offset;
            GlobalPosition = GlobalPosition.Lerp(targetPosition, delta * _followSmoothSpeed);

            // Apply screen shake offset
            if (_screenShake != null)
                GlobalPosition += _screenShake.Offset;

            LookAt(_followTarget.GlobalPosition, Vector3.Up);
        }

        public void SetFollowTarget(Node3D target)
        {
            _followTarget = target;
        }

        public void SnapToTarget()
        {
            if (_followTarget == null) return;
            GlobalPosition = _followTarget.GlobalPosition + _offset;
            LookAt(_followTarget.GlobalPosition, Vector3.Up);
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<IsometricCamera>();
        }
    }
}
