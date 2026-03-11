using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Interactable crafting bench in the safe room.
    /// Opens the CraftingBenchUI when the player presses E.
    /// </summary>
    public partial class CraftingBench : Area3D, IInteractable
    {
        private CraftingBenchUI _ui;
        private Label3D _promptLabel;

        public string InteractionPrompt => "[E] Craft Gear";
        public bool CanInteract => true;

        public void Initialize(Vector3 position)
        {
            Name = "CraftingBench";
            Position = position;

            CollisionLayer = Constants.MASK_INTERACTABLE;
            CollisionMask = Constants.MASK_PLAYER;
            AddToGroup(Constants.GROUP_INTERACTABLE);

            var shape = new CollisionShape3D();
            var box = new BoxShape3D { Size = new Vector3(2f, 2f, 2f) };
            shape.Shape = box;
            shape.Position = new Vector3(0, 1, 0);
            AddChild(shape);

            BuildVisuals();
        }

        private void BuildVisuals()
        {
            // Try to load a desk model; fall back to a simple CSGBox
            var model = ModelLibrary.TryLoad("prop", "desk_small")
                     ?? ModelLibrary.TryLoad("prop", "desk_medium");

            if (model != null)
            {
                CharacterMeshBuilder.ScaleModelToFit(model, 1.8f);
                AddChild(model);
            }
            else
            {
                // Fallback: dark metal table
                var mesh = new MeshInstance3D();
                var box = new BoxMesh { Size = new Vector3(1.6f, 0.9f, 0.8f) };
                mesh.Mesh = box;
                var mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.15f, 0.15f, 0.2f),
                    Metallic = 0.7f,
                    Roughness = 0.4f
                };
                mesh.MaterialOverride = mat;
                mesh.Position = new Vector3(0, 0.45f, 0);
                AddChild(mesh);

                // Glowing top surface to indicate it's a crafting station
                var topMesh = new MeshInstance3D();
                var topBox = new BoxMesh { Size = new Vector3(1.5f, 0.05f, 0.75f) };
                topMesh.Mesh = topBox;
                var topMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.2f, 0.6f, 0.9f, 0.5f),
                    EmissionEnabled = true,
                    Emission = new Color(0.1f, 0.4f, 0.8f),
                    EmissionEnergyMultiplier = 0.8f,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                };
                topMesh.MaterialOverride = topMat;
                topMesh.Position = new Vector3(0, 0.93f, 0);
                AddChild(topMesh);
            }

            // Prompt label
            _promptLabel = new Label3D();
            _promptLabel.Text = "CRAFTING BENCH";
            _promptLabel.FontSize = 14;
            _promptLabel.Modulate = new Color(0.4f, 0.8f, 1f);
            _promptLabel.Position = new Vector3(0, 1.4f, 0);
            _promptLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            AddChild(_promptLabel);
        }

        public void Interact(PlayerController player)
        {
            if (_ui == null || !GodotObject.IsInstanceValid(_ui))
            {
                _ui = new CraftingBenchUI();
                GetTree().Root.AddChild(_ui);
            }
            _ui.Open(player.Inventory);
        }
    }
}
