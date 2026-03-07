using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Bridges class selection, passive tree, and player stats.
    /// Handles level-up point grants and class-aware stat growth.
    /// </summary>
    public partial class PlayerClassController : Node
    {
        private PlayerStats _stats;
        private BotFrameData _classData;
        private PassiveTree _passiveTree;

        public BotFrameData ClassData => _classData;
        public PassiveTree PassiveTree => _passiveTree;
        public BotFrameType? CurrentClass => _classData?.ClassName;

        /// <summary>
        /// Check if a gameplay perk is active in the passive tree.
        /// Convenience wrapper for combat/movement systems.
        /// </summary>
        public bool HasPerk(string perkId) => _passiveTree?.HasPerk(perkId) ?? false;

        private int _nextAbilitySlot = 1; // Slot 0 is the starting ability

        public override void _Ready()
        {
            _stats = GetParent().GetNode<PlayerStats>("PlayerStats");
            GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;
        }

        public override void _ExitTree()
        {
            GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
        }

        private void OnPlayerLevelUp(int level)
        {
            if (_classData?.AbilityProgression == null) return;
            if (!_classData.AbilityProgression.TryGetValue(level, out var abilityId)) return;

            var abilityData = AbilityRegistry.Get(abilityId);
            if (abilityData == null) return;

            var player = GetParent<PlayerController>();
            if (player?.Combat == null) return;

            int slot = _nextAbilitySlot;
            player.Combat.SetAbility(slot, abilityData);
            _nextAbilitySlot++;

            GameEvents.OnAbilityUnlocked?.Invoke(abilityData);
            GameEvents.OnSystemMessage?.Invoke("Ability",
                $"NEW ABILITY UNLOCKED: {abilityData.AbilityName} — assigned to slot {slot + 1}.");
            GD.Print($"[PlayerClassController] Unlocked ability '{abilityData.AbilityName}' at level {level}, slot {slot + 1}");
        }

        /// <summary>
        /// Initialize this player with a class. Call once at game start.
        /// </summary>
        public void SelectClass(BotFrameType className)
        {
            _classData = BotFrameRegistry.GetClass(className);
            if (_classData == null)
            {
                GD.PrintErr($"[PlayerClassController] Class not found: {className}");
                return;
            }

            // Copy base stats from class
            _stats.Stats.CopyBaseStatsFrom(_classData.BaseStats);
            _stats.SetClassData(_classData);

            // Initialize passive tree
            var treeData = PassiveTreeBuilder.Tree;
            _passiveTree = new PassiveTree(treeData, className);

            // Reinitialize health/mana from new stats
            var player = GetParent<PlayerController>();
            if (player?.Health != null)
            {
                float maxHp = _stats.GetStat(StatType.MaxHealth);
                player.Health.SetMaxHealth(maxHp, true);
            }

            float moveSpeed = _stats.GetStat(StatType.MoveSpeed);
            if (moveSpeed > 0)
                player?.Movement?.SetMoveSpeed(moveSpeed);

            // Assign starting abilities to combat slots
            if (player?.Combat != null && _classData.StartingAbilities != null)
            {
                for (int i = 0; i < _classData.StartingAbilities.Count; i++)
                {
                    var abilityData = AbilityRegistry.Get(_classData.StartingAbilities[i]);
                    if (abilityData != null)
                    {
                        player.Combat.SetAbility(i, abilityData);
                        GD.Print($"[PlayerClassController] Assigned ability '{abilityData.AbilityName}' to slot {i + 1}");
                    }
                }
            }

            // Build procedural player body + weapon for this class
            player?.BuildVisualBody(className);

            // Attach visual progression manager
            if (player != null)
            {
                var visualMgr = new VisualProgressionManager();
                visualMgr.Name = "VisualProgressionManager";
                player.AddChild(visualMgr);
                visualMgr.Initialize(player, className);
            }

            // Give starter items for testing
            GiveStarterItems(player);

            GameEvents.OnClassSelected?.Invoke(_classData);
            GD.Print($"[PlayerClassController] Selected class: {_classData.DisplayName}");
        }

        private static string GetStarterGunId(BotFrameType className) => className switch
        {
            BotFrameType.Scrapheap => "base_shotgun",
            BotFrameType.TinCan    => "base_rifle",
            BotFrameType.SparkPlug => "base_launcher",
            BotFrameType.RustBucket => "base_pistol",
            BotFrameType.NoiseBox  => "base_launcher",
            BotFrameType.Clunker   => "base_repeater",
            _ => "base_pistol"
        };

        private void GiveStarterItems(PlayerController player)
        {
            if (player?.Inventory == null) return;

            // Starter weapon — class-appropriate gun
            var gunId = GetStarterGunId(_classData.ClassName);
            var gun = ItemRegistry.GetItem(gunId);
            if (gun != null)
                player.Inventory.TryAddItem(new ItemInstance(gun, ItemRarity.Uncommon));

            // Starter armor
            var chest = ItemRegistry.GetItem("base_chestplate");
            if (chest != null)
                player.Inventory.TryAddItem(new ItemInstance(chest, ItemRarity.Common));

            var boots = ItemRegistry.GetItem("base_boots");
            if (boots != null)
                player.Inventory.TryAddItem(new ItemInstance(boots, ItemRarity.Common));

            // Consumables
            var healthPot = ConsumableRegistry.Get("potion_health_medium");
            if (healthPot != null)
            {
                var stack = new ItemInstance(healthPot, ItemRarity.Common);
                stack.StackCount = 3;
                player.Inventory.TryAddItem(stack);
            }

            var manaPot = ConsumableRegistry.Get("potion_mana_small");
            if (manaPot != null)
            {
                var stack = new ItemInstance(manaPot, ItemRarity.Common);
                stack.StackCount = 3;
                player.Inventory.TryAddItem(stack);
            }

            GD.Print("[PlayerClassController] Starter items given");
        }

        /// <summary>
        /// Try to allocate a passive node using available skill points.
        /// </summary>
        public bool AllocatePassiveNode(string nodeId)
        {
            if (_passiveTree == null) return false;

            if (_passiveTree.AllocateNode(nodeId, _stats.Stats, _stats.AvailableSkillPoints))
            {
                _stats.SpendSkillPoint();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Respec the entire passive tree.
        /// </summary>
        public void Respec()
        {
            if (_passiveTree == null) return;
            int refunded = _passiveTree.Respec(_stats.Stats);
            _stats.RefundSkillPoints(refunded);
        }
    }
}
