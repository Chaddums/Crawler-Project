using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Isometric camera that follows the player with smooth movement and scroll-wheel zoom.
    /// Uses LookAt to always face the target — no manual rotation math needed.
    /// Includes ScreenShake for combat feedback.
    /// Debug Freecam: Press F9 to detach camera and pan freely. Click to teleport player.
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
        private Node3D _followTarget2; // P2 for co-op
        private float _targetZoom;
        private float _baseZoom; // Store the base zoom before co-op adjustments
        private Vector3 _offset;
        private ScreenShake _screenShake;

        // ── Ceremony mode ──
        private bool _ceremonyMode;
        private Vector3 _ceremonyLookAt;

        // ── Debug Freecam ──
        private bool _freecamActive;
        private Vector3 _freecamLookAt;
        private float _freecamPanSpeed = 40f;
        private Label _freecamLabel;

        public override void _Ready()
        {
            // Tighten near clip to reduce z-fighting (default 0.05 wastes depth precision)
            Near = 0.5f;
            Far = 200f;

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

            if (_freecamActive)
                ProcessFreecam((float)delta);
            else if (_ceremonyMode)
                FollowCeremony((float)delta);
            else
                FollowTarget((float)delta);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // F9 toggles freecam
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F9)
            {
                ToggleFreecam();
                GetViewport().SetInputAsHandled();
                return;
            }

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

            // Freecam: left-click to teleport player
            if (_freecamActive && @event is InputEventMouseButton mb
                && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                TeleportPlayerToFreecam();
                GetViewport().SetInputAsHandled();
            }
        }

        public void Initialize(Node3D target)
        {
            _followTarget = target;
            _baseZoom = _targetZoom;
            CalculateOffset();
            Current = true;

            if (_followTarget != null)
            {
                GlobalPosition = _followTarget.GlobalPosition + _offset;
                LookAt(_followTarget.GlobalPosition, Vector3.Up);
            }
        }

        /// <summary>
        /// Initialize for co-op — camera tracks midpoint of both players.
        /// </summary>
        public void Initialize(Node3D target1, Node3D target2)
        {
            _followTarget = target1;
            _followTarget2 = target2;
            _baseZoom = _targetZoom;
            CalculateOffset();
            Current = true;

            var midpoint = (target1.GlobalPosition + target2.GlobalPosition) * 0.5f;
            GlobalPosition = midpoint + _offset;
            LookAt(midpoint, Vector3.Up);
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
                var p1 = PlayerManager.P1;
                if (p1 != null) _followTarget = p1;
                else return;
            }

            // Co-op: track midpoint between players and adapt zoom
            Vector3 lookAtPos;
            if (_followTarget2 != null && GodotObject.IsInstanceValid(_followTarget2))
            {
                lookAtPos = (_followTarget.GlobalPosition + _followTarget2.GlobalPosition) * 0.5f;

                // Adaptive zoom: zoom out as players spread apart
                float spread = _followTarget.GlobalPosition.FlatDistance(_followTarget2.GlobalPosition);
                float spreadZoom = _baseZoom + spread * 0.6f;
                _targetZoom = Mathf.Clamp(spreadZoom, _baseZoom, _maxZoom);
            }
            else
            {
                lookAtPos = _followTarget.GlobalPosition;
            }

            Vector3 targetPosition = lookAtPos + _offset;
            GlobalPosition = GlobalPosition.Lerp(targetPosition, delta * _followSmoothSpeed);

            // Apply screen shake offset
            if (_screenShake != null)
                GlobalPosition += _screenShake.Offset;

            LookAt(lookAtPos, Vector3.Up);
        }

        /// <summary>
        /// Smoothly zoom camera to look at a world position. Used for loot box ceremony.
        /// </summary>
        public void ZoomToTarget(Vector3 lookAt, float zoomDistance, float duration = 0.8f)
        {
            _ceremonyMode = true;
            _ceremonyLookAt = lookAt;

            var tween = CreateTween();
            tween.TweenProperty(this, "_targetZoom_bridge", zoomDistance, duration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        // Tween bridge for _targetZoom (tweens can't set private fields directly)
        private float _targetZoom_bridge { get => _targetZoom; set => _targetZoom = value; }

        /// <summary>
        /// Return camera to normal follow mode after ceremony.
        /// </summary>
        public void ReturnToFollow(float duration = 0.8f)
        {
            _ceremonyMode = false;

            var tween = CreateTween();
            tween.TweenProperty(this, "_targetZoom_bridge", _baseZoom, duration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private void FollowCeremony(float delta)
        {
            Vector3 targetPosition = _ceremonyLookAt + _offset;
            GlobalPosition = GlobalPosition.Lerp(targetPosition, delta * _followSmoothSpeed);

            if (_screenShake != null)
                GlobalPosition += _screenShake.Offset;

            LookAt(_ceremonyLookAt, Vector3.Up);
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

        // ── Debug Freecam ──

        private void ToggleFreecam()
        {
            _freecamActive = !_freecamActive;

            if (_freecamActive)
            {
                // Capture current look-at point as freecam origin
                _freecamLookAt = _followTarget != null
                    ? _followTarget.GlobalPosition
                    : GlobalPosition - _offset;

                ShowFreecamLabel(true);
                GD.Print("[Camera] Freecam ON — WASD to pan, click to teleport, F9 to exit");
            }
            else
            {
                ShowFreecamLabel(false);
                GD.Print("[Camera] Freecam OFF — following player");
            }
        }

        private void ProcessFreecam(float delta)
        {
            // WASD pans the camera look-at point along the XZ plane
            var input = Vector3.Zero;
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
                input.Z -= 1;
            if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
                input.Z += 1;
            if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
                input.X -= 1;
            if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
                input.X += 1;

            // Shift to go faster
            float speed = _freecamPanSpeed;
            if (Input.IsKeyPressed(Key.Shift))
                speed *= 3f;

            if (input.LengthSquared() > 0)
            {
                input = input.Normalized() * speed * delta;

                // Rotate input by azimuth so WASD aligns with camera facing direction
                float rad = Mathf.DegToRad(_azimuthAngle);
                float sin = Mathf.Sin(rad);
                float cos = Mathf.Cos(rad);
                var rotated = new Vector3(
                    input.X * cos + input.Z * sin,
                    0,
                    -input.X * sin + input.Z * cos
                );

                _freecamLookAt += rotated;
            }

            // Smooth move to the freecam target
            Vector3 targetPosition = _freecamLookAt + _offset;
            GlobalPosition = GlobalPosition.Lerp(targetPosition, delta * _followSmoothSpeed);
            LookAt(_freecamLookAt, Vector3.Up);

            // Update label with coordinates
            if (_freecamLabel != null)
                _freecamLabel.Text = $"FREECAM  ({_freecamLookAt.X:F0}, {_freecamLookAt.Z:F0})  Click to teleport";
        }

        private void TeleportPlayerToFreecam()
        {
            if (_followTarget == null) return;

            // Teleport player to freecam look-at point (ground level)
            var teleportPos = new Vector3(_freecamLookAt.X, 0.9f, _freecamLookAt.Z);

            if (_followTarget is CharacterBody3D body)
            {
                body.GlobalPosition = teleportPos;
                body.Velocity = Vector3.Zero;
            }
            else
            {
                _followTarget.GlobalPosition = teleportPos;
            }

            GD.Print($"[Camera] Teleported player to ({teleportPos.X:F1}, {teleportPos.Z:F1})");

            // Exit freecam after teleport
            _freecamActive = false;
            ShowFreecamLabel(false);
        }

        private void ShowFreecamLabel(bool show)
        {
            if (show)
            {
                if (_freecamLabel == null)
                {
                    _freecamLabel = new Label();
                    _freecamLabel.Name = "FreecamLabel";
                    _freecamLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    _freecamLabel.AnchorLeft = 0.5f;
                    _freecamLabel.AnchorRight = 0.5f;
                    _freecamLabel.AnchorTop = 0;
                    _freecamLabel.GrowHorizontal = Control.GrowDirection.Both;
                    _freecamLabel.OffsetTop = 8;
                    _freecamLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f));
                    _freecamLabel.AddThemeFontSizeOverride("font_size", 20);

                    // Outline for readability
                    _freecamLabel.AddThemeConstantOverride("outline_size", 3);
                    _freecamLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));

                    // Add to CanvasLayer so it renders on screen
                    var layer = new CanvasLayer();
                    layer.Name = "FreecamOverlay";
                    AddChild(layer);
                    layer.AddChild(_freecamLabel);
                }

                _freecamLabel.Text = "FREECAM  Click to teleport  |  F9 to exit";
                _freecamLabel.Visible = true;
            }
            else if (_freecamLabel != null)
            {
                _freecamLabel.Visible = false;
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<IsometricCamera>();
        }
    }
}
