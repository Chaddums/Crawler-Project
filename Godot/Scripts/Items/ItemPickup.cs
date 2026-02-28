using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Area3D + IInteractable: floating/spinning item on the ground.
    /// Rarity-colored, pickup on interact.
    /// </summary>
    public partial class ItemPickup : Area3D, IInteractable
    {
        private ItemInstance _item;
        private MeshInstance3D _mesh;
        private Label3D _label;
        private float _bobTimer;

        public string InteractionPrompt => _item != null ? $"Pick up {_item.GetDisplayName()}" : "Pick up";
        public bool CanInteract => _item != null;

        public override void _Ready()
        {
            _mesh = GetNodeOrNull<MeshInstance3D>("ItemMesh");
            _label = GetNodeOrNull<Label3D>("ItemLabel");

            CollisionLayer = Constants.MASK_INTERACTABLE;
            CollisionMask = 0;
            AddToGroup(Constants.GROUP_INTERACTABLE);
        }

        public void Initialize(ItemInstance item)
        {
            _item = item;

            if (_label != null)
            {
                _label.Text = item.GetDisplayName();
                _label.Modulate = GetRarityColor(item.Rarity);
            }

            if (_mesh != null)
            {
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = GetRarityColor(item.Rarity);
                mat.EmissionEnabled = item.Rarity >= ItemRarity.Rare;
                if (mat.EmissionEnabled)
                {
                    mat.Emission = GetRarityColor(item.Rarity);
                    mat.EmissionEnergyMultiplier = 0.5f;
                }
                _mesh.SetSurfaceOverrideMaterial(0, mat);
            }
        }

        public override void _Process(double delta)
        {
            // Bob up and down + spin
            _bobTimer += (float)delta;
            if (_mesh != null)
            {
                _mesh.Position = new Vector3(0, 0.3f + Mathf.Sin(_bobTimer * 2f) * 0.1f, 0);
                _mesh.RotateY((float)delta * 1.5f);
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
                    QueueFree();
                }
                else
                {
                    GD.Print("[ItemPickup] Inventory full!");
                }
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
