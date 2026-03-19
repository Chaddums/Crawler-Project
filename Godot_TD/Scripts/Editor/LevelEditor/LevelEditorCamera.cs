using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Orbit/pan/zoom camera for the level editor.
    /// WASD pan, scroll zoom, middle/right-drag orbit.
    /// Q/E rotate 45deg. Numpad view presets. Left-drag orbit when Alt held.
    /// </summary>
    public partial class LevelEditorCamera : Camera3D
    {
        private Vector3 _lookAt;
        private float _distance = 30f;
        private float _yaw = 45f;
        private float _pitch = -45f;

        // Smooth animation targets
        private float _targetYaw;
        private float _targetPitch;
        private float _targetDistance;
        private bool _animating;
        private float _animSpeed = 8f;

        private const float MinPitch = -89f;
        private const float MaxPitch = -5f;
        private const float MinDistance = 5f;
        private const float MaxDistance = 80f;
        private const float PanSpeed = 25f;
        private const float OrbitSpeed = 0.3f;
        private const float ZoomSpeed = 2f;
        private const float RotateStep = 45f;

        private bool _orbiting;

        public override void _Ready()
        {
            // Default: look at grid center
            float cx = Constants.VINE_MAP_WIDTH * Constants.VINE_CELL_SIZE * 0.5f;
            float cz = Constants.VINE_MAP_HEIGHT * Constants.VINE_CELL_SIZE * 0.5f;
            _lookAt = new Vector3(cx, 0, cz);
            _targetYaw = _yaw;
            _targetPitch = _pitch;
            _targetDistance = _distance;
            UpdateTransform();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                switch (mb.ButtonIndex)
                {
                    case MouseButton.WheelUp:
                        _targetDistance = Mathf.Max(MinDistance, _targetDistance - ZoomSpeed);
                        _animating = true;
                        GetViewport().SetInputAsHandled();
                        break;
                    case MouseButton.WheelDown:
                        _targetDistance = Mathf.Min(MaxDistance, _targetDistance + ZoomSpeed);
                        _animating = true;
                        GetViewport().SetInputAsHandled();
                        break;
                    case MouseButton.Middle:
                        _orbiting = mb.Pressed;
                        GetViewport().SetInputAsHandled();
                        break;
                    case MouseButton.Right:
                        _orbiting = mb.Pressed;
                        GetViewport().SetInputAsHandled();
                        break;
                }
            }
            else if (@event is InputEventMouseMotion mm)
            {
                if (_orbiting)
                {
                    _yaw += mm.Relative.X * OrbitSpeed;
                    _pitch = Mathf.Clamp(_pitch + mm.Relative.Y * OrbitSpeed, MinPitch, MaxPitch);
                    _targetYaw = _yaw;
                    _targetPitch = _pitch;
                    UpdateTransform();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventKey key && key.Pressed)
            {
                // View rotation hotkeys
                switch (key.Keycode)
                {
                    // Numpad view presets
                    case Key.Kp5: // Top-down
                        AnimateTo(-90f, _yaw);
                        GetViewport().SetInputAsHandled();
                        break;
                    case Key.Kp1: // Front
                        AnimateTo(-15f, 0f);
                        GetViewport().SetInputAsHandled();
                        break;
                    case Key.Kp3: // Right side
                        AnimateTo(-15f, -90f);
                        GetViewport().SetInputAsHandled();
                        break;
                    case Key.Kp7: // Isometric (default)
                        AnimateTo(-45f, 45f);
                        GetViewport().SetInputAsHandled();
                        break;
                    case Key.Kp4: // Rotate left 45
                        AnimateTo(_targetPitch, _targetYaw + RotateStep);
                        GetViewport().SetInputAsHandled();
                        break;
                    case Key.Kp6: // Rotate right 45
                        AnimateTo(_targetPitch, _targetYaw - RotateStep);
                        GetViewport().SetInputAsHandled();
                        break;
                    case Key.Kp8: // Tilt up
                        AnimateTo(Mathf.Clamp(_targetPitch + 15f, MinPitch, MaxPitch), _targetYaw);
                        GetViewport().SetInputAsHandled();
                        break;
                    case Key.Kp2: // Tilt down
                        AnimateTo(Mathf.Clamp(_targetPitch - 15f, MinPitch, MaxPitch), _targetYaw);
                        GetViewport().SetInputAsHandled();
                        break;
                }
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // WASD panning on XZ plane
            var dir = Vector3.Zero;
            if (Input.IsKeyPressed(Key.W)) dir.Z -= 1;
            if (Input.IsKeyPressed(Key.S)) dir.Z += 1;
            if (Input.IsKeyPressed(Key.A)) dir.X -= 1;
            if (Input.IsKeyPressed(Key.D)) dir.X += 1;

            if (dir != Vector3.Zero)
            {
                // Rotate pan direction by camera yaw
                float yawRad = Mathf.DegToRad(_yaw);
                var forward = new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad));
                var right = new Vector3(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));
                _lookAt += (right * dir.X + forward * dir.Z) * PanSpeed * dt;
                UpdateTransform();
            }

            // Smooth animation interpolation
            if (_animating)
            {
                _yaw = Mathf.Lerp(_yaw, _targetYaw, _animSpeed * dt);
                _pitch = Mathf.Lerp(_pitch, _targetPitch, _animSpeed * dt);
                _distance = Mathf.Lerp(_distance, _targetDistance, _animSpeed * dt);

                if (Mathf.Abs(_yaw - _targetYaw) < 0.1f &&
                    Mathf.Abs(_pitch - _targetPitch) < 0.1f &&
                    Mathf.Abs(_distance - _targetDistance) < 0.05f)
                {
                    _yaw = _targetYaw;
                    _pitch = _targetPitch;
                    _distance = _targetDistance;
                    _animating = false;
                }

                UpdateTransform();
            }
        }

        public void FocusOnPosition(Vector3 worldPos)
        {
            _lookAt = worldPos;
            UpdateTransform();
        }

        /// <summary>
        /// Snap rotate yaw by a step (for UI buttons / hotkeys).
        /// </summary>
        public void RotateYaw(float degrees)
        {
            _targetYaw += degrees;
            _animating = true;
        }

        /// <summary>
        /// Snap tilt pitch by a step.
        /// </summary>
        public void RotatePitch(float degrees)
        {
            _targetPitch = Mathf.Clamp(_targetPitch + degrees, MinPitch, MaxPitch);
            _animating = true;
        }

        /// <summary>
        /// Animate to specific pitch/yaw values.
        /// </summary>
        public void AnimateTo(float pitch, float yaw)
        {
            _targetPitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            _targetYaw = yaw;
            _animating = true;
        }

        /// <summary>
        /// Set to a named view preset.
        /// </summary>
        public void SetViewPreset(string preset)
        {
            switch (preset)
            {
                case "Top": AnimateTo(-89f, _targetYaw); break;
                case "Front": AnimateTo(-15f, 0f); break;
                case "Right": AnimateTo(-15f, -90f); break;
                case "Back": AnimateTo(-15f, 180f); break;
                case "Left": AnimateTo(-15f, 90f); break;
                case "Iso": AnimateTo(-45f, 45f); break;
            }
        }

        private void UpdateTransform()
        {
            float yawRad = Mathf.DegToRad(_yaw);
            float pitchRad = Mathf.DegToRad(_pitch);

            var offset = new Vector3(
                _distance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
                -_distance * Mathf.Sin(pitchRad),
                _distance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
            );

            GlobalPosition = _lookAt + offset;
            LookAt(_lookAt, Vector3.Up);
        }
    }
}
