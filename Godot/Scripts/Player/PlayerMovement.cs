using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Handles WASD direct movement and click-to-move via NavigationAgent3D.
    /// Must be a child of a CharacterBody3D (the PlayerController).
    /// </summary>
    public partial class PlayerMovement : Node
    {
        [Export] private float _moveSpeed = Constants.DEFAULT_MOVE_SPEED;
        [Export] private float _rotationSpeed = 720f;

        private CharacterBody3D _body;
        private NavigationAgent3D _navAgent;
        private Camera3D _camera;
        private PlayerController _playerController;
        private Vector2 _directMoveInput;
        private bool _isDirectMoving;
        private bool _hasNavTarget; // only true after click-to-move
        private Vector3 _lastMoveDirection;

        // Dash
        private const float DASH_SPEED = 35f;
        private const float DASH_DURATION = 0.15f;
        private const float DASH_CHARGE_COOLDOWN = 2.5f;
        private const int DASH_MAX_CHARGES = 2;
        private int _dashCharges = DASH_MAX_CHARGES;
        private float _dashTimer;
        private float _dashChargeCooldown;
        private Vector3 _dashDirection;
        private bool _isDashing;

        // Jump
        private const float JUMP_FORCE = 10f;
        private const float JUMP_COOLDOWN = 0.4f;
        private float _jumpCooldownTimer;

        public Vector3 LastMoveDirection => _lastMoveDirection;
        public bool IsMoving => _isDirectMoving || (_navAgent != null && !_navAgent.IsNavigationFinished());
        public bool IsDashing => _isDashing;
        public int DashCharges => _dashCharges;

        public override void _Ready()
        {
            _body = GetParent<CharacterBody3D>();
            _navAgent = _body.GetNode<NavigationAgent3D>("NavigationAgent3D");

            // NavigationAgent3D settings
            _navAgent.PathDesiredDistance = 0.5f;
            _navAgent.TargetDesiredDistance = 0.5f;
        }

        private const float GRAVITY = 20f;

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (_camera == null || !GodotObject.IsInstanceValid(_camera))
                _camera = GetViewport().GetCamera3D();
            if (_camera == null) return;

            // Grab PlayerController reference (animatable may change on rebuild)
            _playerController ??= GetParentOrNull<PlayerController>();
            var animator = _playerController?.Animatable;

            // Apply gravity — preserve vertical velocity across frames
            float verticalVelocity = _body.Velocity.Y;
            if (!_body.IsOnFloor())
                verticalVelocity -= GRAVITY * dt;
            else
                verticalVelocity = 0f;

            // Tick dash charge cooldown
            if (_dashCharges < DASH_MAX_CHARGES)
            {
                _dashChargeCooldown -= dt;
                if (_dashChargeCooldown <= 0f)
                {
                    _dashCharges++;
                    _dashChargeCooldown = DASH_CHARGE_COOLDOWN;
                }
            }

            // Tick jump cooldown
            if (_jumpCooldownTimer > 0f)
                _jumpCooldownTimer -= dt;

            // Dash overrides all movement
            if (_isDashing)
            {
                _dashTimer -= dt;
                if (_dashTimer <= 0f)
                {
                    _isDashing = false;
                }
                else
                {
                    _body.Velocity = new Vector3(_dashDirection.X * DASH_SPEED, verticalVelocity, _dashDirection.Z * DASH_SPEED);
                    _body.MoveAndSlide();
                    return;
                }
            }

            if (_isDirectMoving)
            {
                // WASD cancels any click-to-move
                _hasNavTarget = false;

                // WASD movement — camera-relative
                Vector3 moveDir = ConvertToIsometricDirection(_directMoveInput);
                var vel = new Vector3(moveDir.X * _moveSpeed, verticalVelocity, moveDir.Z * _moveSpeed);
                _body.Velocity = vel;
                _body.MoveAndSlide();
                _lastMoveDirection = moveDir;

                animator?.SetState(AnimState.Walk);
            }
            else if (_hasNavTarget && _navAgent != null && !_navAgent.IsNavigationFinished())
            {
                // Click-to-move via navigation
                Vector3 nextPos = _navAgent.GetNextPathPosition();
                Vector3 direction = (_body.GlobalPosition.DirectionTo(nextPos)).Flat().Normalized();
                _body.Velocity = new Vector3(direction.X * _moveSpeed, verticalVelocity, direction.Z * _moveSpeed);
                _body.MoveAndSlide();
                _lastMoveDirection = direction;

                animator?.SetState(AnimState.Walk);
            }
            else
            {
                _body.Velocity = new Vector3(0, verticalVelocity, 0);
                _body.MoveAndSlide();

                animator?.SetState(AnimState.Idle);
            }

            // Always face toward the cursor regardless of movement state
            FaceTowardCursor();
        }

        private Vector3 ConvertToIsometricDirection(Vector2 input)
        {
            if (_camera == null) return Vector3.Zero;

            Vector3 forward = -_camera.GlobalTransform.Basis.Z;
            Vector3 right = _camera.GlobalTransform.Basis.X;
            forward.Y = 0f;
            forward = forward.Normalized();
            right.Y = 0f;
            right = right.Normalized();

            return (forward * input.Y + right * input.X).Normalized();
        }

        public void HandleDirectMove(Vector2 input)
        {
            _directMoveInput = input;
            _isDirectMoving = input.LengthSquared() > 0.01f;
        }

        public void HandleClickToMove()
        {
            if (_camera == null) return;

            var mousePos = GetViewport().GetMousePosition();
            var from = _camera.ProjectRayOrigin(mousePos);
            var to = from + _camera.ProjectRayNormal(mousePos) * 100f;

            var spaceState = _body.GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to, Constants.MASK_GROUND);
            var result = spaceState.IntersectRay(query);

            if (result.Count > 0)
            {
                var hitPos = (Vector3)result["position"];
                _navAgent.TargetPosition = hitPos;
                _hasNavTarget = true;
                _isDirectMoving = false;
            }
        }

        /// <summary>
        /// Navigate to a world position using NavigationAgent3D pathfinding.
        /// Used by AutoPlayer for safe room-to-room movement.
        /// </summary>
        public void NavigateTo(Vector3 worldPos)
        {
            if (_navAgent == null) return;
            _navAgent.TargetPosition = worldPos;
            _hasNavTarget = true;
            _isDirectMoving = false;
        }

        public void SetMoveSpeed(float speed)
        {
            _moveSpeed = speed;
        }

        public void HandleDash()
        {
            if (_isDashing) return;
            if (_dashCharges <= 0) return;

            _dashCharges--;
            _isDashing = true;
            _dashTimer = DASH_DURATION;

            // Start charge cooldown if this was the first charge spent
            if (_dashCharges == DASH_MAX_CHARGES - 1)
                _dashChargeCooldown = DASH_CHARGE_COOLDOWN;

            // Dash in movement direction, or facing direction if standing still
            if (_isDirectMoving && _directMoveInput.LengthSquared() > 0.01f)
            {
                _dashDirection = ConvertToIsometricDirection(_directMoveInput);
            }
            else if (_lastMoveDirection.LengthSquared() > 0.01f)
            {
                _dashDirection = _lastMoveDirection;
            }
            else
            {
                // Dash toward cursor facing direction
                _dashDirection = -_body.GlobalTransform.Basis.Z;
                _dashDirection.Y = 0;
                _dashDirection = _dashDirection.Normalized();
            }

            // Cancel nav
            _hasNavTarget = false;
        }

        public void HandleJump()
        {
            if (!_body.IsOnFloor()) return;
            if (_jumpCooldownTimer > 0f) return;

            _jumpCooldownTimer = JUMP_COOLDOWN;

            // Apply upward impulse by setting vertical velocity
            var vel = _body.Velocity;
            vel.Y = JUMP_FORCE;
            _body.Velocity = vel;
        }

        public void Stop()
        {
            _isDirectMoving = false;
            _hasNavTarget = false;
            _directMoveInput = Vector2.Zero;
            if (_navAgent != null)
                _navAgent.TargetPosition = _body.GlobalPosition;
        }

        public void Warp(Vector3 position)
        {
            _body.GlobalPosition = position;
        }

        /// <summary>
        /// Rotate the player to face the cursor's world position on the ground plane.
        /// Called every physics frame for twin-stick style aiming.
        /// </summary>
        private void FaceTowardCursor()
        {
            if (_camera == null || !GodotObject.IsInstanceValid(_camera)) return;

            var mousePos = GetViewport().GetMousePosition();
            var from = _camera.ProjectRayOrigin(mousePos);
            var dir = _camera.ProjectRayNormal(mousePos);

            // Intersect with ground plane (Y = player's Y)
            Vector3 cursorPos;
            float planeY = _body.GlobalPosition.Y;
            if (Mathf.Abs(dir.Y) > 0.001f)
            {
                float t = (planeY - from.Y) / dir.Y;
                if (t > 0f)
                    cursorPos = from + dir * t;
                else
                    return;
            }
            else
            {
                return;
            }

            var faceDir = (cursorPos - _body.GlobalPosition).Flat();
            if (faceDir.LengthSquared() > 0.01f)
            {
                var target = _body.GlobalPosition + faceDir.Normalized();
                target.Y = _body.GlobalPosition.Y;
                _body.LookAt(target, Vector3.Up);
            }
        }
    }
}
