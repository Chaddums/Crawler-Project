using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Subscribes to OnRoomCleared: spawns celebration text, particles, screen shake,
    /// and a reward chest in cleared combat rooms.
    /// </summary>
    public partial class RoomClearManager : Node
    {
        public override void _Ready()
        {
            GameEvents.OnRoomCleared += OnRoomCleared;
        }

        private void OnRoomCleared(Node roomNode)
        {
            if (roomNode is not RoomController room) return;

            Vector3 center = room.GlobalPosition + Vector3.Up * 2f;

            // "ROOM CLEARED!" text
            SpawnClearedText(center);

            // Celebration particles
            var particles = VfxFactory.CreateCelebrationParticles();
            particles.GlobalPosition = room.GlobalPosition + Vector3.Up * 0.5f;
            GetTree().Root.AddChild(particles);

            // Screen shake
            if (ServiceLocator.TryGet<IsometricCamera>(out var camera))
                camera.Shake(0.2f);

            // Reward chest
            SpawnRewardChest(room);
        }

        private void SpawnClearedText(Vector3 position)
        {
            var label = new Label3D();
            label.Text = "ROOM CLEARED!";
            label.FontSize = 48;
            label.Modulate = new Color(1f, 0.85f, 0.2f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 6;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.NoDepthTest = true;
            label.GlobalPosition = position;
            label.Scale = Vector3.Zero;

            GetTree().Root.AddChild(label);

            // Scale in (Back easing), hold, then fade out
            var tween = label.CreateTween();
            tween.TweenProperty(label, "scale", new Vector3(1.5f, 1.5f, 1.5f), 0.3f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(label, "scale", Vector3.One, 0.15f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.InOut);
            tween.TweenInterval(1.0);
            tween.TweenProperty(label, "modulate:a", 0f, 0.5f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(label.QueueFree));
        }

        private void SpawnRewardChest(RoomController room)
        {
            if (room.RoomType != RoomType.Combat) return;

            // Gold chest mesh
            var chest = new Area3D();
            chest.CollisionLayer = 0;
            chest.CollisionMask = Constants.MASK_PLAYER;
            chest.GlobalPosition = room.GlobalPosition + new Vector3(0, 0.3f, 0);

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(1.5f, 1.5f, 1.5f);
            shape.Shape = box;
            chest.AddChild(shape);

            // Chest mesh
            var meshNode = new MeshInstance3D();
            var boxMesh = new BoxMesh();
            boxMesh.Size = new Vector3(0.5f, 0.35f, 0.35f);
            meshNode.Mesh = boxMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.75f, 0.6f, 0.15f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.6f, 0.45f, 0.1f);
            mat.EmissionEnergyMultiplier = 0.8f;
            meshNode.MaterialOverride = mat;
            chest.AddChild(meshNode);

            // Lid
            var lid = new MeshInstance3D();
            var lidMesh = new BoxMesh();
            lidMesh.Size = new Vector3(0.52f, 0.08f, 0.37f);
            lid.Mesh = lidMesh;
            lid.Position = new Vector3(0, 0.2f, 0);

            var lidMat = new StandardMaterial3D();
            lidMat.AlbedoColor = new Color(0.8f, 0.65f, 0.2f);
            lid.MaterialOverride = lidMat;
            chest.AddChild(lid);

            // Label
            var label = new Label3D();
            label.Text = "Reward";
            label.FontSize = 24;
            label.Position = new Vector3(0, 0.8f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(1f, 0.85f, 0.3f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 3;
            chest.AddChild(label);

            GetTree().Root.AddChild(chest);

            // Auto-pickup on body entered
            chest.BodyEntered += (body) =>
            {
                if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;

                // Drop 1-3 random items
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                int count = rng.RandiRange(1, 3);
                for (int i = 0; i < count; i++)
                {
                    float angle = (float)i / count * Mathf.Tau;
                    var spawnPos = chest.GlobalPosition + new Vector3(Mathf.Cos(angle) * 1.5f, 0.5f, Mathf.Sin(angle) * 1.5f);
                    var item = CreateRewardItem(rng);
                    if (item != null)
                        ItemPickup.SpawnAt(GetTree().Root, spawnPos, item);
                }

                // Gold burst particles
                var burst = VfxFactory.CreateLootBurstParticles(new Color(1f, 0.85f, 0.3f));
                burst.GlobalPosition = chest.GlobalPosition;
                GetTree().Root.AddChild(burst);

                chest.QueueFree();
            };
        }

        private static ItemInstance CreateRewardItem(RandomNumberGenerator rng)
        {
            int roll = rng.RandiRange(0, 2);
            ItemData data;
            if (roll == 0)
            {
                data = new ConsumableData { Id = "reward_potion", ItemName = "Health Potion", HealAmount = 25f };
            }
            else if (roll == 1)
            {
                data = new EquipmentData($"reward_ring_{rng.Randi() % 999}", "Ring", ItemRarity.Common, EquipmentSlot.Ring1, 1);
            }
            else
            {
                data = new EquipmentData($"reward_gear_{rng.Randi() % 999}", "Armor Scrap", ItemRarity.Common, EquipmentSlot.Chest, 1);
            }

            var rarity = LootTableResolver.RollRarityPublic();
            return new ItemInstance(data, rarity);
        }

        public override void _ExitTree()
        {
            GameEvents.OnRoomCleared -= OnRoomCleared;
        }
    }
}
