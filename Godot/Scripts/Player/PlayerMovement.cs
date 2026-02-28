using Godot;

namespace DungeonCrawlerCarl
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

        public override void _PhysicsProcess(double delta)
        {
            if (_camera == null)
                _camera = GetViewport().GetCamera3D();

            if (_isDirectMoving)
            {
                // WASD cancels any click-to-move
                _hasNavTarget = false;

                // WASD movement — camera-relative
                Vector3 moveDir = ConvertToIsometricDirection(_directMoveInput);
                _body.Velocity = moveDir * _moveSpeed;
                _body.MoveAndSlide();
                _lastMoveDirection = moveDir;
                FaceDirection(moveDir, (float)delta);
            }
            else if (_hasNavTarget && _navAgent != null && !_navAgent.IsNavigationFinished())
            {
                // Click-to-move via navigation
                Vector3 nextPos = _navAgent.GetNextPathPosition();
                Vector3 direction = (_body.GlobalPosition.DirectionTo(nextPos)).Flat().Normalized();
                _body.Velocity = direction * _moveSpeed;
                _body.MoveAndSlide();
                _lastMoveDirection = direction;
                FaceDirection(direction, (float)delta);
            }
            else
            {
                _body.Velocity = Vector3.Zero;
                _body.MoveAndSlide();
            }
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

        private void FaceDirection(Vector3 direction, float delta)
        {
            if (direction.LengthSquared() < 0.01f) return;
            var target = _body.GlobalPosition + direction.Normalized();
            target.Y = _body.GlobalPosition.Y;
            _body.LookAt(target, Vector3.Up);
        }
    }
}
