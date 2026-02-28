using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Main player node. This is the root CharacterBody3D of the player scene.
    /// Wires together child components: Movement, Input, Stats, Health.
    /// </summary>
    public partial class PlayerController : CharacterBody3D, IItemReceiver
    {
        private HealthComponent _health;
        private PlayerMovement _movement;
        private PlayerInputHandler _input;
        private PlayerStats _stats;

        public HealthComponent Health => _health;
        public PlayerMovement Movement => _movement;
        public PlayerStats Stats => _stats;
        public string PlayerName { get; private set; } = "Carl";

        public override void _Ready()
        {
            _health = GetNode<HealthComponent>("HealthComponent");
            _movement = GetNode<PlayerMovement>("PlayerMovement");
            _input = GetNode<PlayerInputHandler>("PlayerInputHandler");
            _stats = GetNode<PlayerStats>("PlayerStats");

            // Register with ServiceLocator
            ServiceLocator.Register(this);

            // Wire input events to movement
            _input.OnMoveInput += _movement.HandleDirectMove;
            _input.OnClickToMove += _movement.HandleClickToMove;
            _input.OnInteract += HandleInteract;

            // Wire health events
            _health.OnDeath += HandleDeath;

            // Initialize health from stats
            float maxHp = _stats.GetStat(StatType.MaxHealth);
            _health.SetMaxHealth(maxHp, true);

            float moveSpeed = _stats.GetStat(StatType.MoveSpeed);
            if (moveSpeed > 0) _movement.SetMoveSpeed(moveSpeed);

            // Add to player group
            AddToGroup(Constants.GROUP_PLAYER);

            GD.Print("[PlayerController] Ready");
        }

        private void HandleDeath()
        {
            _input.DisableInput();
            _movement.Stop();
            GameEvents.OnPlayerDeath?.Invoke(this);
        }

        private void HandleInteract()
        {
            // Find nearby interactables using Area3D overlap
            var spaceState = GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 2f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, GlobalPosition),
                CollisionMask = Constants.MASK_INTERACTABLE
            };

            var results = spaceState.IntersectShape(queryParams);
            float closestDist = float.MaxValue;
            IInteractable closest = null;

            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                if (collider is IInteractable interactable && interactable.CanInteract)
                {
                    float dist = GlobalPosition.DistanceTo(((Node3D)collider).GlobalPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = interactable;
                    }
                }
            }

            closest?.Interact(this);
        }

        // --- IItemReceiver ---
        public string DisplayName => PlayerName;
        public bool TryAddItem(object item) => false; // Inventory not yet ported

        public override void _ExitTree()
        {
            _input.OnMoveInput -= _movement.HandleDirectMove;
            _input.OnClickToMove -= _movement.HandleClickToMove;
            _input.OnInteract -= HandleInteract;
            _health.OnDeath -= HandleDeath;

            ServiceLocator.Unregister<PlayerController>();
        }
    }
}
