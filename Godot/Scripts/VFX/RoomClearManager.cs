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
            else if (room.RoomType == RoomType.Event)
                SpawnEventTerminal(room);
            else if (room.RoomType == RoomType.Shop)
                SpawnShopItems(room);
            else if (room.RoomType == RoomType.Puzzle)
                SpawnPuzzleChallenge(room);
        }

        private void OnRoomCleared(Node roomNode)
        {
            if (roomNode is not RoomController room) return;

            Vector3 center = room.GlobalPosition + Vector3.Up * 2f;

            // "ROOM CLEARED!" text
            SpawnClearedText(center);

            // Megabonk rooms handle their own rewards/celebration via MegabonkArena
            if (room.RoomType == RoomType.Megabonk)
                return;

            CelebrationVfxManager.Play(GetTree().Root, room.GlobalPosition + Vector3.Up * 0.5f, CelebrationTier.Decent);

            // Reward chest
            SpawnRewardChest(room);

            // Relic cache (boss rooms guaranteed)
            OnRoomCleared_SpawnRelicCache(room);
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

            // Offset from center to avoid spawning inside room obstacles
            var rng2 = new RandomNumberGenerator();
            rng2.Randomize();
            float angle = rng2.RandfRange(0, Mathf.Tau);
            var chestPos = room.GlobalPosition + new Vector3(Mathf.Cos(angle) * 4f, 0.3f, Mathf.Sin(angle) * 4f);
            var chest = new Area3D();
            chest.CollisionLayer = 0;
            chest.CollisionMask = Constants.MASK_PLAYER;

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(1.5f, 1.5f, 1.5f);
            shape.Shape = box;
            chest.AddChild(shape);

            // Reward chest uses Bronze loot box model with idle effects
            var rewardModel = CharacterMeshBuilder.BuildLootBoxModel(LootBoxTier.Bronze);
            rewardModel.Scale = Vector3.One;
            LootBoxPresenter.Attach(rewardModel, LootBoxTier.Bronze);
            chest.AddChild(rewardModel);

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

                // Count as a Junk loot box for the tracker
                GameEvents.OnLootBoxOpened?.Invoke(new LootBoxOpenedData { Tier = LootBoxTier.Junk });

                // Reward chest celebration
                CelebrationVfxManager.Play(GetTree().Root, chest.GlobalPosition + Vector3.Up * 0.5f, CelebrationTier.Decent);

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
        /// Spawns an interactive data terminal in Event rooms.
        /// Grants a random stat buff when the player walks up.
        /// </summary>
        private void SpawnEventTerminal(RoomController room)
        {
            var terminalPos = room.GlobalPosition + new Vector3(0, 0.5f, 0);
            var terminal = new Area3D();
            terminal.CollisionLayer = 0;
            terminal.CollisionMask = Constants.MASK_PLAYER;

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(3f, 3f, 3f);
            shape.Shape = box;
            terminal.AddChild(shape);

            // Floating label
            var label = new Label3D();
            label.Text = "Data Terminal";
            label.FontSize = 28;
            label.Position = new Vector3(0, 1.5f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(0.6f, 0.4f, 1f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 4;
            terminal.AddChild(label);

            GetTree().Root.AddChild(terminal);
            terminal.GlobalPosition = terminalPos;

            bool used = false;
            terminal.BodyEntered += (body) =>
            {
                if (used) return;
                if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;
                used = true;

                // Pick a random stat buff
                var rng = new RandomNumberGenerator();
                rng.Randomize();

                var buffStats = new[] {
                    (StatType.Strength, "Strength"),
                    (StatType.Dexterity, "Dexterity"),
                    (StatType.Constitution, "Constitution"),
                    (StatType.Intelligence, "Intelligence"),
                    (StatType.MaxHealth, "Max Health"),
                    (StatType.Armor, "Armor"),
                    (StatType.CritChance, "Crit Chance"),
                    (StatType.AttackSpeed, "Attack Speed"),
                    (StatType.MoveSpeed, "Move Speed"),
                };

                var (statType, statName) = buffStats[rng.RandiRange(0, buffStats.Length - 1)];

                // Flat +5 for most stats, +0.05 for rate stats
                float value = statType switch
                {
                    StatType.CritChance => 0.05f,
                    StatType.AttackSpeed => 0.1f,
                    StatType.MoveSpeed => 0.5f,
                    _ => 5f
                };

                // Apply permanent buff to player
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                {
                    var mod = new StatModifier(statType, ModifierType.Flat, value, "event_terminal");
                    player.Stats.Stats.AddModifier(mod);
                }

                // Update label to show what was granted
                string displayVal = statType is StatType.CritChance ? $"+{value * 100:0}%" : $"+{value:0.#}";
                label.Text = $"{displayVal} {statName}!";
                label.Modulate = new Color(0.3f, 1f, 0.5f);

                // Tiered celebration for event terminal
                CelebrationVfxManager.Play(GetTree().Root, terminalPos + Vector3.Up * 0.5f, CelebrationTier.Decent);

                // AXIS commentary
                if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                {
                    commentary.QueueLine("AXIS",
                        $"Oh good, you found a data terminal. Enjoy your {statName} boost. You'll still lose.",
                        CommentaryPriority.Medium, CommentaryCategory.RoomReaction);
                }

                GD.Print($"[RoomClearManager] Event terminal used: +{value} {statName}");

                // Fade out after a moment
                var tween = label.CreateTween();
                tween.TweenInterval(2.0);
                tween.TweenProperty(label, "modulate:a", 0f, 1.0f);
            };
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
            chestModel.Scale = new Vector3(1.2f, 1.2f, 1.2f);
            LootBoxPresenter.Attach(chestModel, LootBoxTier.Gold);
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

                // Treasure chest gets an Exciting celebration
                CelebrationVfxManager.Play(GetTree().Root, chest.GlobalPosition + Vector3.Up * 0.5f, CelebrationTier.Exciting);

                // Chance to also spawn a relic cache near treasure (scales with sector, 0% at S1-2)
                int treasureSector = GameManager.Instance?.CurrentSector ?? 1;
                float relicChance = treasureSector <= 2 ? 0f : (treasureSector - 2) * 0.10f; // 10% at S3, 20% at S4, 30% at S5
                if (GD.Randf() < relicChance)
                    SpawnRelicCache(chest.GlobalPosition + new Vector3(3f, 0f, 0f));

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
                ((EquipmentData)data).WeaponType = WeaponType.Pistol;
            }

            // Treasure rooms roll higher rarity
            var rarity = LootTableResolver.RollRarityPublic();
            if (rarity < ItemRarity.Uncommon)
                rarity = ItemRarity.Uncommon;
            return new ItemInstance(data, rarity);
        }

        /// <summary>
        /// Spawns a Relic Cache pickup in the world. When picked up, triggers the unique ceremony.
        /// </summary>
        private void SpawnRelicCache(Vector3 position)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var relic = RelicRegistry.PickRandom(gm.FoundRelicsThisRun);
            if (relic == null) return;

            // Build a glowing pickup node
            var cache = new Area3D();
            cache.CollisionLayer = 0;
            cache.CollisionMask = Constants.MASK_PLAYER;

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(2f, 2.5f, 2f);
            shape.Shape = box;
            cache.AddChild(shape);

            // Ornate visual — Diamond-tier loot box model with relic glow color
            var model = CharacterMeshBuilder.BuildLootBoxModel(LootBoxTier.Diamond);
            model.Scale = new Vector3(1.3f, 1.3f, 1.3f);
            LootBoxPresenter.Attach(model, LootBoxTier.Legendary);
            cache.AddChild(model);

            // Floating label
            var label = new Label3D();
            label.Text = "RELIC CACHE";
            label.FontSize = 32;
            label.Position = new Vector3(0, 1.5f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = relic.GlowColor;
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 5;
            cache.AddChild(label);

            // Light pillar in relic color
            var pillar = VfxFactory.CreateLightPillar(ItemRarity.Absurd);
            cache.AddChild(pillar);

            // Relic-colored omni light
            var light = new OmniLight3D();
            light.LightColor = relic.GlowColor;
            light.LightEnergy = 2.5f;
            light.OmniRange = 5f;
            light.Position = new Vector3(0, 0.5f, 0);
            cache.AddChild(light);

            GetTree().Root.AddChild(cache);
            cache.GlobalPosition = position;

            // Capture relic for closure
            var capturedRelic = relic;

            cache.BodyEntered += (body) =>
            {
                if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;

                // Track this relic as found
                gm.FoundRelicsThisRun.Add(capturedRelic.Id);

                // Launch the unique ceremony
                var ceremony = new RelicCacheUI();
                GetTree().Root.AddChild(ceremony);
                ceremony.StartCeremony(capturedRelic);

                cache.QueueFree();
            };
        }

        /// <summary>
        /// Spawns purchasable items on the 3 shop pedestals.
        /// Walking into a pedestal buys the item if the player has enough scrap.
        /// </summary>
        private void SpawnShopItems(RoomController room)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            int sector = GameManager.Instance?.CurrentSector ?? 1;

            for (int i = -1; i <= 1; i++)
            {
                float x = i * 4f;
                var itemPos = room.GlobalPosition + new Vector3(x, 1.2f, 2f);
                int cost = (10 + sector * 5) * (i == 0 ? 2 : 1);

                var item = CreateShopItem(rng, sector);
                if (item == null) continue;

                var pickup = new Area3D();
                pickup.CollisionLayer = Constants.MASK_INTERACTABLE;
                pickup.CollisionMask = Constants.MASK_PLAYER;
                pickup.Monitoring = true;

                var shape = new CollisionShape3D();
                var box = new BoxShape3D();
                box.Size = new Vector3(2f, 2.5f, 2f);
                shape.Shape = box;
                pickup.AddChild(shape);

                // Visible item model on the pedestal
                var model = CharacterMeshBuilder.BuildItemModel(item);
                if (model != null)
                {
                    CharacterMeshBuilder.ScaleModelToFit(model, 0.5f);
                    model.Position = new Vector3(0, 0.3f, 0);
                    pickup.AddChild(model);

                    // Slow spin
                    var spinTween = pickup.CreateTween().SetLoops();
                    spinTween.TweenProperty(model, "rotation:y", Mathf.Tau, 4f)
                        .AsRelative();
                }

                // Price label above item
                var label = new Label3D();
                label.Text = $"{item.GetDisplayName()}\n{cost} Scrap";
                label.FontSize = 48;
                label.PixelSize = 0.01f;
                label.Position = new Vector3(0, 1.5f, 0);
                label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                label.Modulate = new Color(0.9f, 0.7f, 0.2f);
                label.OutlineModulate = new Color(0, 0, 0);
                label.OutlineSize = 8;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.NoDepthTest = true;
                pickup.AddChild(label);

                GetTree().Root.AddChild(pickup);
                pickup.GlobalPosition = itemPos;

                var capturedItem = item;
                int capturedCost = cost;
                bool purchased = false;
                pickup.BodyEntered += (body) =>
                {
                    if (purchased) return;
                    if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;

                    if (MetaSaveManager.Data.Scrap < capturedCost)
                    {
                        label.Text = "Not enough Scrap!";
                        label.Modulate = new Color(1f, 0.3f, 0.3f);
                        // Reset label after 2s
                        var tree = pickup.GetTree();
                        if (tree != null)
                        {
                            tree.CreateTimer(2.0).Timeout += () =>
                            {
                                if (GodotObject.IsInstanceValid(label))
                                {
                                    label.Text = $"{capturedItem.GetDisplayName()}\n{capturedCost} Scrap";
                                    label.Modulate = new Color(0.9f, 0.7f, 0.2f);
                                }
                            };
                        }
                        return;
                    }

                    purchased = true;
                    MetaSaveManager.SpendScrap(capturedCost);
                    if (ServiceLocator.TryGet<PlayerController>(out var player))
                    {
                        player.Inventory.TryAddItem(capturedItem);
                        GameEvents.OnItemPickedUp?.Invoke(capturedItem.BaseData);
                    }

                    if (ServiceLocator.TryGet<AudioManager>(out var audio))
                        audio.PlaySFXByName("pickup");

                    CelebrationVfxManager.Play(pickup.GetTree().Root,
                        pickup.GlobalPosition + Vector3.Up * 0.5f, CelebrationTier.Decent);
                    pickup.QueueFree();
                };
            }

            // AXIS commentary
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                commentary.QueueLine("AXIS",
                    "Welcome to my shop. Everything is overpriced. You're welcome.",
                    CommentaryPriority.Medium, CommentaryCategory.RoomReaction);
        }

        private static ItemInstance CreateShopItem(RandomNumberGenerator rng, int sector)
        {
            int roll = rng.RandiRange(0, 3);
            ItemData data;
            if (roll == 0)
            {
                data = ConsumableRegistry.Get("potion_health_small")
                    ?? new ConsumableData { Id = "shop_potion", ItemName = "Repair Kit", HealAmount = 25f };
            }
            else if (roll == 1)
            {
                data = new EquipmentData($"shop_weapon_{rng.Randi() % 999}", "Shop Weapon", ItemRarity.Uncommon, EquipmentSlot.MainHand, sector);
                ((EquipmentData)data).WeaponType = WeaponType.Pistol;
            }
            else if (roll == 2)
            {
                data = new EquipmentData($"shop_armor_{rng.Randi() % 999}", "Shop Armor", ItemRarity.Uncommon, EquipmentSlot.Chest, sector);
            }
            else
            {
                data = new EquipmentData($"shop_ring_{rng.Randi() % 999}", "Shop Accessory", ItemRarity.Uncommon, EquipmentSlot.Ring1, sector);
            }

            var rarity = LootTableResolver.RollRarityPublic();
            if (rarity < ItemRarity.Uncommon) rarity = ItemRarity.Uncommon;
            return new ItemInstance(data, rarity);
        }

        /// <summary>
        /// Spawns a timed combat challenge in Puzzle rooms.
        /// Kill all spawned enemies within the time limit for a bonus reward.
        /// </summary>
        private void SpawnPuzzleChallenge(RoomController room)
        {
            // Puzzle rooms become timed combat arenas — spawn a wave of enemies
            // and give a bonus chest if cleared fast
            var label = new Label3D();
            label.Text = "TIMED CHALLENGE";
            label.FontSize = 36;
            label.Position = new Vector3(0, 3f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(0.9f, 0.6f, 0.1f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 5;
            room.AddChild(label);

            // Override room type to combat so it spawns enemies and tracks kills
            room.RoomType = RoomType.Combat;
            room.SpawnEnemies();

            // Fade out label
            var tween = label.CreateTween();
            tween.TweenInterval(2.0);
            tween.TweenProperty(label, "modulate:a", 0f, 1.0f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(label)) label.QueueFree();
            }));

            // AXIS commentary
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                commentary.QueueLine("AXIS",
                    "A puzzle? No. I don't do puzzles. Fight or die. Those are your options.",
                    CommentaryPriority.Medium, CommentaryCategory.RoomReaction);
        }

        private void OnRoomCleared_SpawnRelicCache(RoomController room)
        {
            int sector = GameManager.Instance?.CurrentSector ?? 1;

            // Boss rooms: relic cache from sector 2+, guaranteed from sector 3+
            if (room.RoomType == RoomType.Boss)
            {
                if (sector < 2) return; // No relic on first boss
                if (sector < 3 && GD.Randf() > 0.35f) return; // 35% chance at sector 2
                var pos = room.GlobalPosition + new Vector3(2f, 0.3f, 0);
                SpawnRelicCache(pos);
                return;
            }

            // Megabonk rooms handle their own rewards via MegabonkArena
        }

        public override void _ExitTree()
        {
            GameEvents.OnRoomCleared -= OnRoomCleared;
            GameEvents.OnRoomEntered -= OnRoomEntered;
        }
    }
}
