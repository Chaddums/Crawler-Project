using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Area3D + IInteractable: floating/spinning item on the ground.
    /// Type-specific mesh shapes, rarity aura for Rare+, pickup trail on collect.
    /// </summary>
    public partial class ItemPickup : Area3D, IInteractable
    {
        private ItemInstance _item;
        private MeshInstance3D _mesh;
        private Node3D _modelRoot; // For full 3D model items
        private Label3D _label;
        private float _bobTimer;

        public string InteractionPrompt => _item != null ? $"Pick up {_item.GetDisplayName()}" : "Pick up";
        public bool CanInteract => _item != null;

        public override void _Ready()
        {
            _mesh = GetNodeOrNull<MeshInstance3D>("ItemMesh");
            _label = GetNodeOrNull<Label3D>("ItemLabel");

            CollisionLayer = Constants.MASK_INTERACTABLE;
            CollisionMask = Constants.MASK_PLAYER;
            AddToGroup(Constants.GROUP_INTERACTABLE);

            // Disable monitoring until Initialize sets _item, preventing
            // BodyEntered from firing before the item reference is set.
            Monitoring = false;

            BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node3D body)
        {
            if (body is IItemReceiver receiver)
                Interact(body);
        }

        public void Initialize(ItemInstance item)
        {
            _item = item;

            if (_label != null)
            {
                _label.Text = item.GetDisplayName();
                _label.Modulate = GetRarityColor(item.Rarity);
            }

            // Try full 3D model asset first
            _modelRoot = CharacterMeshBuilder.TryLoadItemModel(item);
            if (_modelRoot != null)
            {
                _modelRoot.Position = new Vector3(0, 0.3f, 0);
                AddChild(_modelRoot);
                if (_mesh != null) _mesh.Visible = false;
            }
            else
            {
                // Use multi-part procedural item model
                _modelRoot = CharacterMeshBuilder.BuildItemModel(item);
                _modelRoot.Position = new Vector3(0, 0.3f, 0);
                AddChild(_modelRoot);
                if (_mesh != null) _mesh.Visible = false;

                // Apply rarity tinting to all meshes in the model
                ApplyRarityTint(_modelRoot, item.Rarity);
            }

            // Enable collision monitoring now that _item is set
            Monitoring = true;

            // Rarity aura for Rare+ items
            if (item.Rarity >= ItemRarity.Rare)
            {
                var aura = VfxFactory.CreateRarityAura(item.Rarity);
                aura.Position = new Vector3(0, 0.3f, 0);
                AddChild(aura);
            }

            // Epic+ ground drop celebration
            if (item.Rarity >= ItemRarity.Epic)
            {
                var pillar = VfxFactory.CreateLightPillar(item.Rarity);
                AddChild(pillar);

                float trauma = item.Rarity switch
                {
                    ItemRarity.Epic => 0.3f,
                    ItemRarity.Legendary => 0.5f,
                    ItemRarity.Absurd => 0.7f,
                    _ => 0.3f
                };
                if (ServiceLocator.TryGet<IsometricCamera>(out var camera))
                    camera.Shake(trauma);

                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("epic_drop");

                string quip = item.Rarity switch
                {
                    ItemRarity.Epic => "EPIC DROP! The arena shudders. Even the loot is showing off.",
                    ItemRarity.Legendary => "LEGENDARY! AXIS is momentarily speechless. Savor it.",
                    ItemRarity.Absurd => "ABSURD TIER! Reality itself paused to double-check.",
                    _ => "Now THAT is a find."
                };

                if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                    commentary.QueueLine("AXIS", quip,
                        CommentaryPriority.High, CommentaryCategory.LootReaction);

                TtsHelper.Speak($"AXIS: {quip}");
            }
        }

        public override void _Process(double delta)
        {
            // Bob up and down + spin
            _bobTimer += (float)delta;
            float bobY = 0.3f + Mathf.Sin(_bobTimer * 2f) * 0.1f;
            float spinAmount = (float)delta * 1.5f;

            if (_modelRoot != null)
            {
                _modelRoot.Position = new Vector3(0, bobY, 0);
                _modelRoot.RotateY(spinAmount);
            }
            else if (_mesh != null)
            {
                _mesh.Position = new Vector3(0, bobY, 0);
                _mesh.RotateY(spinAmount);
            }
        }

        public void Interact(Node playerNode)
        {
            if (_item == null) return;

            if (playerNode is IItemReceiver receiver)
            {
                if (receiver.TryAddItem(_item))
                {
                    GD.Print($"[ItemPickup] {receiver.DisplayName} picked up {_item.GetDisplayName()}");

                    // Pickup sound
                    if (ServiceLocator.TryGet<AudioManager>(out var audio))
                        audio.PlaySFXByName("pickup");

                    // Pickup trail particles
                    var trail = VfxFactory.CreatePickupTrail(GetRarityColor(_item.Rarity));
                    GetTree().Root.AddChild(trail);
                    trail.GlobalPosition = GlobalPosition;

                    QueueFree();
                }
                else
                {
                    GD.Print("[ItemPickup] Inventory full!");
                }
            }
        }

        private static void ApplyRarityTint(Node root, ItemRarity rarity)
        {
            if (rarity <= ItemRarity.Common) return;

            Color tint = GetRarityColor(rarity);
            foreach (var child in root.GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
                {
                    mat.EmissionEnabled = rarity >= ItemRarity.Rare;
                    if (mat.EmissionEnabled)
                    {
                        mat.Emission = tint;
                        mat.EmissionEnergyMultiplier = 0.5f;
                    }
                }
                if (child is Node node)
                    ApplyRarityTint(node, rarity);
            }
        }

        private static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => new Color(0.8f, 0.8f, 0.8f),
            ItemRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),
            ItemRarity.Rare => new Color(0.2f, 0.4f, 1.0f),
            ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),
            ItemRarity.Legendary => new Color(1.0f, 0.6f, 0.0f),
            ItemRarity.Absurd => new Color(1.0f, 0.0f, 0.4f),
            _ => Colors.White
        };

        /// <summary>
        /// Spawn an ItemPickup at a world position.
        /// </summary>
        public static void SpawnAt(Node parent, Vector3 position, ItemInstance item)
        {
            var scene = GD.Load<PackedScene>(Constants.SCENE_ITEM_PICKUP);
            if (scene == null)
            {
                GD.PrintErr("[ItemPickup] Could not load ItemPickup scene");
                return;
            }

            var pickup = scene.Instantiate<ItemPickup>();
            parent.AddChild(pickup);
            pickup.GlobalPosition = position + Vector3.Up * 0.5f;
            pickup.Initialize(item);
        }
    }
}
