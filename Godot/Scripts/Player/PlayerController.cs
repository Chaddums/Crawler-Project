using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Main player node. This is the root CharacterBody3D of the player scene.
    /// Wires together child components: Movement, Input, Stats, Health.
    /// Builds procedural body mesh based on selected class.
    /// </summary>
    public partial class PlayerController : CharacterBody3D, IItemReceiver
    {
        private HealthComponent _health;
        private PlayerMovement _movement;
        private PlayerInputHandler _input;
        private PlayerStats _stats;
        private PlayerCombat _combat;
        private PlayerInventory _inventory;
        private PlayerClassController _classController;
        private CharacterAnimator _characterAnimator;
        private ProceduralAnimator _proceduralAnimator;
        private IAnimatable _animatable;
        private Node3D _bodyRoot;

        public HealthComponent Health => _health;
        public PlayerMovement Movement => _movement;
        public PlayerStats Stats => _stats;
        public PlayerCombat Combat => _combat;
        public PlayerInventory Inventory => _inventory;
        public PlayerClassController ClassController => _classController;
        public IAnimatable Animatable => _animatable;
        public string PlayerName { get; private set; } = "Carl";

        public override void _Ready()
        {
            _health = GetNode<HealthComponent>("HealthComponent");
            _movement = GetNode<PlayerMovement>("PlayerMovement");
            _input = GetNode<PlayerInputHandler>("PlayerInputHandler");
            _stats = GetNode<PlayerStats>("PlayerStats");
            _combat = GetNode<PlayerCombat>("PlayerCombat");
            _inventory = GetNode<PlayerInventory>("PlayerInventory");
            _classController = GetNode<PlayerClassController>("PlayerClassController");

            // Register with ServiceLocator
            ServiceLocator.Register(this);

            // Wire input events to movement
            _input.OnMoveInput += _movement.HandleDirectMove;
            _input.OnClickToMove += _movement.HandleClickToMove;
            _input.OnInteract += HandleInteract;
            _input.OnBasicAttack += _combat.HandleBasicAttack;
            _input.OnAbilityInput += _combat.HandleAbilityInput;

            // Wire health events
            _health.OnDeath += HandleDeath;

            // Initialize status effect manager
            var statusMgr = GetNodeOrNull<StatusEffectManager>("StatusEffectManager");
            statusMgr?.Initialize(_stats.Stats, _health);

            // Initialize health from stats
            float maxHp = _stats.GetStat(StatType.MaxHealth);
            _health.SetMaxHealth(maxHp, true);

            float moveSpeed = _stats.GetStat(StatType.MoveSpeed);
            if (moveSpeed > 0) _movement.SetMoveSpeed(moveSpeed);

            // Add to player group
            AddToGroup(Constants.GROUP_PLAYER);

            GD.Print("[PlayerController] Ready");
        }

        /// <summary>
        /// Build the procedural body for this player's class. Called after class is selected.
        /// </summary>
        public void BuildVisualBody(CrawlerClassName className)
        {
            // Remove old "PlayerMesh" capsule if present
            var oldMesh = GetNodeOrNull<MeshInstance3D>("PlayerMesh");
            oldMesh?.QueueFree();

            // Remove old body if rebuilding
            _bodyRoot?.QueueFree();
            _characterAnimator?.QueueFree();
            _characterAnimator = null;
            _proceduralAnimator?.QueueFree();
            _proceduralAnimator = null;
            _animatable = null;

            _bodyRoot = CharacterMeshBuilder.BuildPlayerBody(className);
            AddChild(_bodyRoot);

            // If the loaded model has an AnimationPlayer, wire up CharacterAnimator
            var animPlayer = CharacterMeshBuilder.FindAnimationPlayer(_bodyRoot);
            if (animPlayer != null)
            {
                _characterAnimator = new CharacterAnimator();
                _characterAnimator.Name = "CharacterAnimator";
                AddChild(_characterAnimator);
                _characterAnimator.Initialize(_bodyRoot);
                _animatable = _characterAnimator;
            }
            else
            {
                // No skeletal animations — use ProceduralAnimator for limb-based animation
                _proceduralAnimator = new ProceduralAnimator();
                _proceduralAnimator.Name = "ProceduralAnimator";
                AddChild(_proceduralAnimator);
                _proceduralAnimator.Initialize(_bodyRoot);
                _animatable = _proceduralAnimator;
            }
        }

        private void HandleDeath()
        {
            _input.DisableInput();
            _movement.Stop();
            GameEvents.OnPlayerDeath?.Invoke(this);

            // Show death screen after a short delay
            GetTree().CreateTimer(1.5).Timeout += ShowDeathScreen;
        }

        private void ShowDeathScreen()
        {
            var canvas = new CanvasLayer();
            canvas.Layer = 100;
            GetTree().Root.AddChild(canvas);

            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 0.7f);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            canvas.AddChild(bg);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
            vbox.GrowHorizontal = Control.GrowDirection.Both;
            vbox.GrowVertical = Control.GrowDirection.Both;
            vbox.Position = new Vector2(860, 440);
            canvas.AddChild(vbox);

            var label = new Label();
            label.Text = "YOU DIED\n\nThe dungeon claims another crawler.";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 36);
            vbox.AddChild(label);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 30);
            vbox.AddChild(spacer);

            var restartBtn = new Button();
            restartBtn.Text = "Try Again";
            restartBtn.CustomMinimumSize = new Vector2(200, 50);
            restartBtn.Pressed += () =>
            {
                canvas.QueueFree();
                GameManager.Instance?.StartNewGame();
            };
            vbox.AddChild(restartBtn);

            var menuBtn = new Button();
            menuBtn.Text = "Main Menu";
            menuBtn.CustomMinimumSize = new Vector2(200, 50);
            menuBtn.Pressed += () =>
            {
                canvas.QueueFree();
                GameManager.Instance?.ReturnToMainMenu();
            };
            vbox.AddChild(menuBtn);
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
        public bool TryAddItem(object item)
        {
            if (item is ItemInstance instance && _inventory != null)
                return _inventory.TryAddItem(instance);
            return false;
        }

        public override void _ExitTree()
        {
            _input.OnMoveInput -= _movement.HandleDirectMove;
            _input.OnClickToMove -= _movement.HandleClickToMove;
            _input.OnInteract -= HandleInteract;
            _input.OnBasicAttack -= _combat.HandleBasicAttack;
            _input.OnAbilityInput -= _combat.HandleAbilityInput;
            _health.OnDeath -= HandleDeath;

            ServiceLocator.Unregister<PlayerController>();
        }
    }
}
