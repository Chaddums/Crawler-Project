using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Subscribes to OnRoomCleared and OnRoomEntered: spawns celebration text, particles,
    /// screen shake, reward chests in cleared combat rooms, and treasure in treasure rooms.
    /// </summary>
    public partial class RoomClearManager : Node
    {
        public override void _Ready()
        {
            GameEvents.OnRoomCleared += OnRoomCleared;
            GameEvents.OnRoomEntered += OnRoomEntered;
        }

        private void OnRoomEntered(Node roomNode)
        {
            if (roomNode is not RoomController room) return;

            if (room.RoomType == RoomType.Treasure)
                SpawnTreasureChest(room);
        }

        private void OnRoomCleared(Node roomNode)
        {
            if (roomNode is not RoomController room) return;

            Vector3 center = room.GlobalPosition + Vector3.Up * 2f;

            // "ROOM CLEARED!" text
            SpawnClearedText(center);

            // Celebration particles
            var particles = VfxFactory.CreateCelebrationParticles();
            GetTree().Root.AddChild(particles);
            particles.GlobalPosition = room.GlobalPosition + Vector3.Up * 0.5f;

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
            label.Scale = Vector3.One * 0.01f;

            GetTree().Root.AddChild(label);
            label.GlobalPosition = position;

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
            var chestPos = room.GlobalPosition + new Vector3(0, 0.3f, 0);
            var chest = new Area3D();
            chest.CollisionLayer = 0;
            chest.CollisionMask = Constants.MASK_PLAYER;

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
            chest.GlobalPosition = chestPos;

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
                var burstPos = chest.GlobalPosition;
                var burst = VfxFactory.CreateLootBurstParticles(new Color(1f, 0.85f, 0.3f));
                GetTree().Root.AddChild(burst);
                burst.GlobalPosition = burstPos;

                chest.QueueFree();
            };
        }

        private static ItemInstance CreateRewardItem(RandomNumberGenerator rng)
        {
            int roll = rng.RandiRange(0, 2);
            ItemData data;
            if (roll == 0)
            {
                data = ConsumableRegistry.Get("potion_health_small")
                    ?? new ConsumableData { Id = "reward_potion", ItemName = "Repair Kit", HealAmount = 25f };
            }
            else if (roll == 1)
            {
                var name = StringLoader.Get("equipment.ring");
                data = new EquipmentData($"reward_ring_{rng.Randi() % 999}", name, ItemRarity.Common, EquipmentSlot.Ring1, 1);
            }
            else
            {
                var name = StringLoader.Get("equipment.chestplate");
                data = new EquipmentData($"reward_gear_{rng.Randi() % 999}", name, ItemRarity.Common, EquipmentSlot.Chest, 1);
            }

            var rarity = LootTableResolver.RollRarityPublic();
            return new ItemInstance(data, rarity);
        }

        /// <summary>
        /// Spawns a treasure chest on the central pedestal in Treasure rooms.
        /// Drops 2-4 items with boosted rarity when the player walks up.
        /// </summary>
        private void SpawnTreasureChest(RoomController room)
        {
            // Chest sits on the pedestal at room center
            var chestPos = room.GlobalPosition + new Vector3(0, 0.55f, 0);
            var chest = new Area3D();
            chest.CollisionLayer = 0;
            chest.CollisionMask = Constants.MASK_PLAYER;

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(2f, 2f, 2f);
            shape.Shape = box;
            chest.AddChild(shape);

            // Use the loot box procedural model (Gold tier for treasure rooms)
            var chestModel = CharacterMeshBuilder.BuildLootBoxModel(LootBoxTier.Gold);
            chestModel.Scale = new Vector3(2f, 2f, 2f);
            chest.AddChild(chestModel);

            // Floating label
            var label = new Label3D();
            label.Text = "Treasure Chest";
            label.FontSize = 28;
            label.Position = new Vector3(0, 1.2f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(1f, 0.85f, 0.3f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 4;
            chest.AddChild(label);

            // Light pillar effect
            var pillar = VfxFactory.CreateLightPillar(ItemRarity.Rare);
            chest.AddChild(pillar);

            GetTree().Root.AddChild(chest);
            chest.GlobalPosition = chestPos;

            // Open on player approach
            chest.BodyEntered += (body) =>
            {
                if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;

                // Treasure rooms drop more and better loot
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                int count = rng.RandiRange(2, 4);
                for (int i = 0; i < count; i++)
                {
                    float angle = (float)i / count * Mathf.Tau;
                    var spawnPos = chest.GlobalPosition + new Vector3(
                        Mathf.Cos(angle) * 2f, 0.5f, Mathf.Sin(angle) * 2f);
                    var item = CreateTreasureItem(rng);
                    if (item != null)
                        ItemPickup.SpawnAt(GetTree().Root, spawnPos, item);
                }

                // Celebration particles + gold burst
                var burstPos = chest.GlobalPosition;
                var burst = VfxFactory.CreateLootBurstParticles(new Color(1f, 0.85f, 0.3f));
                GetTree().Root.AddChild(burst);
                burst.GlobalPosition = burstPos;

                var celebration = VfxFactory.CreateCelebrationParticles();
                GetTree().Root.AddChild(celebration);
                celebration.GlobalPosition = burstPos + Vector3.Up * 0.5f;

                if (ServiceLocator.TryGet<IsometricCamera>(out var camera))
                    camera.Shake(0.3f);

                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("epic_drop");

                chest.QueueFree();
            };
        }

        /// <summary>
        /// Creates treasure room items with boosted rarity (Uncommon+ floor).
        /// </summary>
        private static ItemInstance CreateTreasureItem(RandomNumberGenerator rng)
        {
            int roll = rng.RandiRange(0, 3);
            ItemData data;
            if (roll == 0)
            {
                data = new ConsumableData { Id = "treasure_potion", ItemName = "Greater Potion", HealAmount = 50f };
            }
            else if (roll == 1)
            {
                data = new EquipmentData($"treasure_ring_{rng.Randi() % 999}", "Salvaged Ring", ItemRarity.Uncommon, EquipmentSlot.Ring1, 1);
            }
            else if (roll == 2)
            {
                data = new EquipmentData($"treasure_armor_{rng.Randi() % 999}", "Plated Chassis", ItemRarity.Uncommon, EquipmentSlot.Chest, 1);
            }
            else
            {
                data = new EquipmentData($"treasure_weapon_{rng.Randi() % 999}", "Arc Emitter", ItemRarity.Uncommon, EquipmentSlot.MainHand, 1);
            }

            // Treasure rooms roll higher rarity
            var rarity = LootTableResolver.RollRarityPublic();
            if (rarity < ItemRarity.Uncommon)
                rarity = ItemRarity.Uncommon;
            return new ItemInstance(data, rarity);
        }

        public override void _ExitTree()
        {
            GameEvents.OnRoomCleared -= OnRoomCleared;
            GameEvents.OnRoomEntered -= OnRoomEntered;
        }
    }
}
