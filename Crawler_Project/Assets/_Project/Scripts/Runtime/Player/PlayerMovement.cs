using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = Constants.DEFAULT_MOVE_SPEED;
        [SerializeField] private float _rotationSpeed = 720f;

        [Header("Click-to-Move")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private GameObject _clickIndicatorPrefab;

        private NavMeshAgent _agent;
        private PlayerInputHandler _input;
        private Camera _mainCamera;
        private Vector2 _directMoveInput;
        private bool _isDirectMoving;
        private Vector3 _lastMoveDirection;

        public Vector3 LastMoveDirection => _lastMoveDirection;
        public bool IsMoving => _isDirectMoving || (_agent.hasPath && _agent.remainingDistance > _agent.stoppingDistance);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _input = GetComponent<PlayerInputHandler>();
            _mainCamera = Camera.main;

            _agent.speed = _moveSpeed;
            _agent.angularSpeed = _rotationSpeed;
            _agent.updateRotation = false;
        }

        private void OnEnable()
        {
            _input.OnMoveInput += HandleDirectMove;
            _input.OnClickToMove += HandleClickToMove;
        }

        private void OnDisable()
        {
            _input.OnMoveInput -= HandleDirectMove;
            _input.OnClickToMove -= HandleClickToMove;
        }

        private void Update()
        {
            if (_isDirectMoving)
            {
                _agent.ResetPath();
                Vector3 moveDir = ConvertToIsometricDirection(_directMoveInput);
                _agent.Move(moveDir * _moveSpeed * Time.deltaTime);
                _lastMoveDirection = moveDir;
            }
            else if (_agent.hasPath && _agent.remainingDistance > _agent.stoppingDistance)
            {
                _lastMoveDirection = _agent.velocity.normalized;
            }
        }

        private Vector3 ConvertToIsometricDirection(Vector2 input)
        {
            if (_mainCamera == null) return Vector3.zero;

            Vector3 forward = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;
            forward.y = 0f;
            forward.Normalize();
            right.y = 0f;
            right.Normalize();

            return (forward * input.y + right * input.x).normalized;
        }

        private void HandleClickToMove()
        {
            if (_mainCamera == null) return;

            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _groundLayer))
            {
                _agent.SetDestination(hit.point);
                _isDirectMoving = false;

                if (_clickIndicatorPrefab != null)
                {
                    var indicator = Instantiate(_clickIndicatorPrefab, hit.point + Vector3.up * 0.1f, Quaternion.identity);
                    Destroy(indicator, 0.5f);
                }
            }
        }

        private void HandleDirectMove(Vector2 input)
        {
            _directMoveInput = input;
            _isDirectMoving = input.sqrMagnitude > 0.01f;

            if (_isDirectMoving)
            {
                _agent.ResetPath();
            }
        }

        public void SetMoveSpeed(float speed)
        {
            _moveSpeed = speed;
            _agent.speed = speed;
        }

        public void Stop()
        {
            _agent.ResetPath();
            _isDirectMoving = false;
            _directMoveInput = Vector2.zero;
        }

        public void Warp(Vector3 position)
        {
            _agent.Warp(position);
        }
    }
}
