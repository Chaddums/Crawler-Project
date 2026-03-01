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
        private IAnimatable _characterAnimator;
        private Vector2 _directMoveInput;
        private bool _isDirectMoving;
        private bool _hasNavTarget; // only true after click-to-move
        private Vector3 _lastMoveDirection;

        public Vector3 LastMoveDirection => _lastMoveDirection;
        public bool IsMoving => _isDirectMoving || (_navAgent != null && !_navAgent.IsNavigationFinished());

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

            if (_camera == null)
                _camera = GetViewport().GetCamera3D();

            // Lazily grab CharacterAnimator from PlayerController
            if (_characterAnimator == null)
            {
                var pc = GetParentOrNull<PlayerController>();
                _characterAnimator = pc?.Animatable;
            }

            // Apply gravity — preserve vertical velocity across frames
            float verticalVelocity = _body.Velocity.Y;
            if (!_body.IsOnFloor())
                verticalVelocity -= GRAVITY * dt;
            else
                verticalVelocity = 0f;

            if (_isDirectMoving)
            {
                // WASD cancels any click-to-move
                _hasNavTarget = false;

                // WASD movement — camera-relative
                Vector3 moveDir = ConvertToIsometricDirection(_directMoveInput);
                _body.Velocity = new Vector3(moveDir.X * _moveSpeed, verticalVelocity, moveDir.Z * _moveSpeed);
                _body.MoveAndSlide();
                _lastMoveDirection = moveDir;

                _characterAnimator?.SetState(AnimState.Walk);
            }
            else if (_hasNavTarget && _navAgent != null && !_navAgent.IsNavigationFinished())
            {
                // Click-to-move via navigation
                Vector3 nextPos = _navAgent.GetNextPathPosition();
                Vector3 direction = (_body.GlobalPosition.DirectionTo(nextPos)).Flat().Normalized();
                _body.Velocity = new Vector3(direction.X * _moveSpeed, verticalVelocity, direction.Z * _moveSpeed);
                _body.MoveAndSlide();
                _lastMoveDirection = direction;

                _characterAnimator?.SetState(AnimState.Walk);
            }
            else
            {
                _body.Velocity = new Vector3(0, verticalVelocity, 0);
                _body.MoveAndSlide();

                _characterAnimator?.SetState(AnimState.Idle);
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

        public void SetMoveSpeed(float speed)
        {
            _moveSpeed = speed;
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
            if (_camera == null) return;

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
