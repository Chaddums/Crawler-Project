using Godot;

namespace JunkbotArena
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
        public Node3D BodyRoot => _bodyRoot;
        public ProceduralAnimator ProceduralAnimator => _proceduralAnimator;
        public string PlayerName { get; private set; } = "Scrapper";

        public void ReinitializeAnimator() => _proceduralAnimator?.Initialize(_bodyRoot);

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
            _input.OnDash += _movement.HandleDash;
            _input.OnJump += _movement.HandleJump;

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
        public void BuildVisualBody(BotFrameType className)
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

            // Check for AnimationPlayer in loaded model
            var animPlayer = CharacterMeshBuilder.FindAnimationPlayer(_bodyRoot);
            if (animPlayer != null)
            {
                // Strip root motion tracks that fight CharacterBody3D physics
                StripRootMotionTracks(animPlayer);

                _characterAnimator = new CharacterAnimator();
                _characterAnimator.Name = "CharacterAnimator";
                AddChild(_characterAnimator);
                _characterAnimator.Initialize(_bodyRoot);
                _animatable = _characterAnimator;
                GD.Print($"[PlayerController] CharacterAnimator wired — anims: {string.Join(", ", animPlayer.GetAnimationList())}");
            }
            else
            {
                // No skeletal animations — use ProceduralAnimator for limb-based animation
                GD.Print($"[PlayerController] No AnimationPlayer found, using ProceduralAnimator");
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
            var gm = GameManager.Instance;
            int sector = gm?.CurrentSector ?? 1;
            int area = gm?.CurrentArea ?? 1;
            int level = Stats?.Level ?? 1;

            // Calculate scrap earned (mirrors MetaSaveManager.CalculateRunScrap)
            int kills = gm != null ? gm.RunKills : 0;
            int scrapEarned = (sector - 1) * 100 + (area - 1) * 25 + kills * 2 + level * 10;

            var canvas = new CanvasLayer();
            canvas.Layer = 100;
            GetTree().Root.AddChild(canvas);

            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 0.85f);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            canvas.AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            canvas.AddChild(center);

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(500, 0);
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.06f, 0.06f, 0.12f, 0.95f);
            panelStyle.BorderColor = new Color(0.8f, 0.2f, 0.2f);
            panelStyle.BorderWidthBottom = 2;
            panelStyle.BorderWidthTop = 2;
            panelStyle.BorderWidthLeft = 2;
            panelStyle.BorderWidthRight = 2;
            panelStyle.CornerRadiusBottomLeft = 8;
            panelStyle.CornerRadiusBottomRight = 8;
            panelStyle.CornerRadiusTopLeft = 8;
            panelStyle.CornerRadiusTopRight = 8;
            panelStyle.ContentMarginLeft = 40;
            panelStyle.ContentMarginRight = 40;
            panelStyle.ContentMarginTop = 30;
            panelStyle.ContentMarginBottom = 30;
            panel.AddThemeStyleboxOverride("panel", panelStyle);
            center.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddThemeConstantOverride("separation", 6);
            panel.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "UNIT OFFLINE";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 42);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.2f));
            vbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = "The arena claims another scrapper.";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 18);
            subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            vbox.AddChild(subtitle);

            AddSpacer(vbox, 16);

            // Run stats
            var gold = new Color(0.9f, 0.8f, 0.3f);
            var white = new Color(0.85f, 0.85f, 0.85f);

            AddStatRow(vbox, "Sector Reached", $"{sector}-{area}", white);
            AddStatRow(vbox, "Level", level.ToString(), white);
            AddStatRow(vbox, "Enemies Killed", kills.ToString(), white);

            AddSpacer(vbox, 12);

            // Scrap earned
            var scrapRow = new HBoxContainer();
            vbox.AddChild(scrapRow);
            var scrapLabel = new Label();
            scrapLabel.Text = "Scrap Earned";
            scrapLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scrapLabel.AddThemeFontSizeOverride("font_size", 26);
            scrapLabel.AddThemeColorOverride("font_color", gold);
            scrapRow.AddChild(scrapLabel);
            var scrapValue = new Label();
            scrapValue.Text = $"+{scrapEarned}";
            scrapValue.AddThemeFontSizeOverride("font_size", 26);
            scrapValue.AddThemeColorOverride("font_color", gold);
            scrapRow.AddChild(scrapValue);

            var totalLabel = new Label();
            totalLabel.Text = $"Total Scrap: {MetaSaveManager.Data.Scrap}";
            totalLabel.HorizontalAlignment = HorizontalAlignment.Center;
            totalLabel.AddThemeFontSizeOverride("font_size", 16);
            totalLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.5f, 0.2f));
            vbox.AddChild(totalLabel);

            // Threat level + Ascension
            var threatLabel = new Label();
            int ascension = MetaSaveManager.Data.AscensionRank;
            string threatText = ascension > 0
                ? $"Ascension {ascension} | Threat Level: {MetaSaveManager.ThreatLevel}"
                : $"Threat Level: {MetaSaveManager.ThreatLevel}";
            threatLabel.Text = threatText;
            threatLabel.HorizontalAlignment = HorizontalAlignment.Center;
            threatLabel.AddThemeFontSizeOverride("font_size", 16);
            threatLabel.AddThemeColorOverride("font_color", ascension > 0
                ? new Color(0.7f, 0.4f, 0.95f, 0.9f)
                : new Color(1f, 0.4f, 0.3f, 0.8f));
            vbox.AddChild(threatLabel);

            AddSpacer(vbox, 20);

            // Buttons
            var restartBtn = new Button();
            restartBtn.Text = "Try Again";
            restartBtn.CustomMinimumSize = new Vector2(200, 50);
            restartBtn.AddThemeFontSizeOverride("font_size", 20);
            restartBtn.Pressed += () =>
            {
                canvas.QueueFree();
                GameManager.Instance?.StartNewGame();
            };
            vbox.AddChild(restartBtn);

            var menuBtn = new Button();
            menuBtn.Text = "Main Menu";
            menuBtn.CustomMinimumSize = new Vector2(200, 50);
            menuBtn.AddThemeFontSizeOverride("font_size", 20);
            menuBtn.Pressed += () =>
            {
                canvas.QueueFree();
                GameManager.Instance?.ReturnToMainMenu();
            };
            vbox.AddChild(menuBtn);
        }

        private static void AddStatRow(VBoxContainer parent, string label, string value, Color color)
        {
            var row = new HBoxContainer();
            parent.AddChild(row);
            var nameLabel = new Label();
            nameLabel.Text = label;
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            nameLabel.AddThemeFontSizeOverride("font_size", 20);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            row.AddChild(nameLabel);
            var valLabel = new Label();
            valLabel.Text = value;
            valLabel.AddThemeFontSizeOverride("font_size", 20);
            valLabel.AddThemeColorOverride("font_color", color);
            row.AddChild(valLabel);
        }

        private static void AddSpacer(VBoxContainer parent, float height)
        {
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, height);
            parent.AddChild(spacer);
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

        /// <summary>
        /// Remove position/rotation tracks on the FBX root node so animations
        /// don't fight CharacterBody3D movement.
        /// </summary>
        private static void StripRootMotionTracks(AnimationPlayer animPlayer)
        {
            int stripped = 0;
            foreach (var animName in animPlayer.GetAnimationList())
            {
                var anim = animPlayer.GetAnimation(animName);
                if (anim == null) continue;

                // Walk backwards so removing tracks doesn't shift indices
                for (int t = anim.GetTrackCount() - 1; t >= 0; t--)
                {
                    string path = anim.TrackGetPath(t).ToString();
                    // Strip tracks targeting the scene root's position/rotation/transform
                    // These are typically ".:position", ".:rotation", or just "." with transform type
                    if (path.StartsWith(".:position") || path.StartsWith(".:rotation") ||
                        path.StartsWith(".:transform") || path == ".")
                    {
                        var trackType = anim.TrackGetType(t);
                        if (trackType == Animation.TrackType.Position3D ||
                            trackType == Animation.TrackType.Rotation3D ||
                            trackType == Animation.TrackType.Scale3D)
                        {
                            anim.RemoveTrack(t);
                            stripped++;
                        }
                    }
                }
            }
            if (stripped > 0)
                GD.Print($"[PlayerController] Stripped {stripped} root motion tracks from FBX animations");
        }

        public override void _ExitTree()
        {
            _input.OnMoveInput -= _movement.HandleDirectMove;
            _input.OnClickToMove -= _movement.HandleClickToMove;
            _input.OnInteract -= HandleInteract;
            _input.OnBasicAttack -= _combat.HandleBasicAttack;
            _input.OnAbilityInput -= _combat.HandleAbilityInput;
            _input.OnDash -= _movement.HandleDash;
            _input.OnJump -= _movement.HandleJump;
            _health.OnDeath -= HandleDeath;

            ServiceLocator.Unregister<PlayerController>();
        }
    }
}
